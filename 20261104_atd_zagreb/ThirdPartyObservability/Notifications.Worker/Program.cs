using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ThirdPartyDemo;

var builder = Host.CreateApplicationBuilder(args);
builder.AddDemoTelemetry("Zagreb.ThirdParty.Notifications");
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddHostedService<NotificationsWorker>();
await builder.Build().RunAsync();

sealed class NotificationsWorker(IConfiguration config, NpgsqlDataSource database, ILogger<NotificationsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Infrastructure.InitializeDatabaseAsync(database, stoppingToken);
        await using var broker = await Infrastructure.ConnectRabbitAsync(config, "third-party-notifications", stoppingToken);
        await using var channel = await broker.CreateChannelAsync(cancellationToken: stoppingToken);
        await Infrastructure.DeclareQueuesAsync(channel, stoppingToken);
        await channel.BasicQosAsync(0, 1, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var context = Infrastructure.ExtractContext(delivery.BasicProperties.Headers);
            var previous = Baggage.Current;
            Baggage.Current = context.Baggage;
            using var activity = Infrastructure.StartMessaging("process", ActivityKind.Consumer,
                context.ActivityContext, Infrastructure.Notifications);
            var outcome = "delivered";
            try
            {
                var message = JsonSerializer.Deserialize<NotificationMessage>(delivery.Body.Span)
                    ?? throw new JsonException("Missing notification.");
                activity?.SetTag("messaging.message.id", message.Id.ToString());
                if (message.Scenario == "notification-fail")
                    throw new InvalidOperationException("Simulated notification provider failure.");
                await using var command = database.CreateCommand("""
                    INSERT INTO notifications (order_id, status) VALUES ($1, $2)
                    ON CONFLICT (order_id) DO NOTHING
                    """);
                command.Parameters.AddWithValue(message.Id);
                command.Parameters.AddWithValue(message.Status);
                var inserted = await command.ExecuteNonQueryAsync(stoppingToken);
                if (inserted == 0) outcome = "duplicate";
                logger.LogInformation("Notification {Outcome} for order {OrderId}, status {Status}; no real email sent",
                    outcome, message.Id, message.Status);
                await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { outcome = "cancelled"; }
            catch (Exception e)
            {
                outcome = "error";
                Telemetry.Fail(activity, e);
                logger.LogError(e, "Notification failed; moving to notification dead-letter queue");
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
            }
            finally
            {
                Telemetry.Notifications.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
                Baggage.Current = previous;
            }
        };
        await channel.BasicConsumeAsync(Infrastructure.Notifications, false, consumer, stoppingToken);
        logger.LogInformation("Notifications listening on {Queue}", Infrastructure.Notifications);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
