using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RabbitMQ.Client;
using ThirdPartyDemo;

sealed class OutboxDispatcher(IConfiguration config, NpgsqlDataSource database, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Infrastructure.InitializeDatabaseAsync(database, stoppingToken);
        await using var broker = await Infrastructure.ConnectRabbitAsync(config, "third-party-outbox", stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await database.OpenConnectionAsync(stoppingToken);
                await using var transaction = await connection.BeginTransactionAsync(stoppingToken);
                await using var claim = connection.CreateCommand();
                claim.Transaction = transaction;
                claim.CommandText = """
                    SELECT order_id, payload, traceparent FROM notification_outbox
                    WHERE dispatched_at IS NULL ORDER BY order_id LIMIT 1 FOR UPDATE SKIP LOCKED
                    """;
                Guid id = default;
                string? payload = null, traceparent = null;
                // Polling with no work should not fill the dashboard with unrelated SQL traces.
                using (OpenTelemetry.SuppressInstrumentationScope.Begin())
                await using (var reader = await claim.ExecuteReaderAsync(stoppingToken))
                {
                    if (await reader.ReadAsync(stoppingToken))
                    {
                        id = reader.GetGuid(0);
                        payload = reader.GetString(1);
                        traceparent = reader.IsDBNull(2) ? null : reader.GetString(2);
                    }
                }
                if (payload is not null)
                {
                    ActivityContext.TryParse(traceparent, null, out var parent);
                    using var activity = Infrastructure.StartMessaging("send", ActivityKind.Producer, parent, Infrastructure.Notifications);
                    activity?.SetTag("messaging.message.id", id.ToString());
                    try
                    {
                        await using var channel = await broker.CreateChannelAsync(new CreateChannelOptions(true, true), stoppingToken);
                        await Infrastructure.DeclareQueuesAsync(channel, stoppingToken);
                        await channel.BasicPublishAsync("", Infrastructure.Notifications, true,
                            new BasicProperties
                            {
                                Persistent = true, ContentType = "application/json", MessageId = id.ToString(),
                                Headers = Infrastructure.InjectContext()
                            }, Encoding.UTF8.GetBytes(payload), stoppingToken);
                        await using var mark = connection.CreateCommand();
                        mark.Transaction = transaction;
                        mark.CommandText = "UPDATE notification_outbox SET dispatched_at = now() WHERE order_id = $1";
                        mark.Parameters.AddWithValue(id);
                        await mark.ExecuteNonQueryAsync(stoppingToken);
                        await transaction.CommitAsync(stoppingToken);
                        Telemetry.Outbox.Add(1);
                        logger.LogInformation("Outbox dispatched notification for order {OrderId}", id);
                    }
                    catch (Exception e)
                    {
                        Telemetry.Fail(activity, e);
                        logger.LogError(e, "Notification publication failed for order {OrderId}; intent remains pending", id);
                        throw;
                    }
                }
                else
                {
                    using (OpenTelemetry.SuppressInstrumentationScope.Begin())
                        await transaction.CommitAsync(stoppingToken);
                    await Task.Delay(500, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                logger.LogError(e, "Outbox dispatch failed; transaction rolls back and intent remains pending");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
