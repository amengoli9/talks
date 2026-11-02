using System.Diagnostics;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ThirdPartyDemo;

var builder = Host.CreateApplicationBuilder(args);
builder.AddDemoTelemetry("Zagreb.ThirdParty.Kitchen");
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddHttpClient("inventory", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Inventory:BaseUrl"] ?? "http://localhost:5281");
    client.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddHttpClient("payments", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Payments:BaseUrl"] ?? "http://localhost:5283");
    client.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddHostedService<KitchenWorker>();
builder.Services.AddHostedService<OutboxDispatcher>();
await builder.Build().RunAsync();

sealed class KitchenWorker(IConfiguration config, NpgsqlDataSource database,
    IHttpClientFactory clients, ILogger<KitchenWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Infrastructure.InitializeDatabaseAsync(database, stoppingToken);
        await using var connection = await Infrastructure.ConnectRabbitAsync(config, "third-party-kitchen", stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await Infrastructure.DeclareQueuesAsync(channel, stoppingToken);
        await channel.BasicQosAsync(0, 1, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var context = Infrastructure.ExtractContext(delivery.BasicProperties.Headers);
            var previousBaggage = Baggage.Current;
            Baggage.Current = context.Baggage;
            using var activity = Infrastructure.StartMessaging("process", ActivityKind.Consumer, context.ActivityContext);
            var timer = Stopwatch.StartNew();
            var outcome = "error";
            OrderMessage? order = null;
            try
            {
                // Deserialize inside the callback: RabbitMQ owns the body buffer.
                order = JsonSerializer.Deserialize<OrderMessage>(delivery.Body.Span)
                    ?? throw new JsonException("Missing order payload.");
                activity?.SetTag("messaging.message.id", order.Id.ToString());
                activity?.SetTag("order.scenario", order.Scenario);
                Telemetry.QueueAge.Record(Math.Max(0, (DateTimeOffset.UtcNow - order.CreatedAt).TotalSeconds));
                logger.LogInformation("Processing order {OrderId}, scenario {Scenario}, redelivered {Redelivered}",
                    order.Id, order.Scenario, delivery.Redelivered);

                if (order.Scenario == "fail")
                {
                    throw new InvalidOperationException("Demonstration: kitchen rejected processing. Message goes to the failed queue.");
                }

                using var inventory = clients.CreateClient("inventory");
                using var stockResponse = await inventory.PostAsJsonAsync("/availability", order, stoppingToken);
                stockResponse.EnsureSuccessStatusCode();
                var stock = await stockResponse.Content.ReadFromJsonAsync<AvailabilityReply>(stoppingToken)
                    ?? throw new HttpRequestException("Missing inventory response.");
                var status = "unavailable";
                if (stock.Available)
                {
                    using var payments = clients.CreateClient("payments");
                    using var paymentResponse = await payments.PostAsJsonAsync("/authorizations", order, stoppingToken);
                    paymentResponse.EnsureSuccessStatusCode();
                    var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentReply>(stoppingToken)
                        ?? throw new HttpRequestException("Missing payment response.");
                    status = payment.Authorized ? "accepted" : "declined";
                }

                // pg_sleep executes in PostgreSQL: the visible latency belongs to a real database command.
                if (order.Scenario == "slow-db")
                {
                    await using var slow = database.CreateCommand("SELECT pg_sleep(3)");
                    await slow.ExecuteNonQueryAsync(stoppingToken);
                }
                await SaveAsync(order, status, stoppingToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                outcome = status;
                logger.LogInformation("Order {OrderId} committed to PostgreSQL with status {Status}", order.Id, status);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Leave unacked: closing the connection makes RabbitMQ redeliver.
                outcome = "cancelled";
            }
            catch (Exception e)
            {
                Telemetry.Fail(activity, e);
                logger.LogError(e, "Order processing failed; dead-lettering without an infinite retry loop");
                if (order is not null)
                {
                    try { await SaveAsync(order, "failed", stoppingToken); }
                    catch (Exception saveError) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError(saveError, "Unable to persist failed status for {OrderId}; inspect dead-letter queue", order.Id);
                    }
                }
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false,
                    cancellationToken: stoppingToken);
            }
            finally
            {
                activity?.SetTag("order.outcome", outcome);
                Telemetry.Orders.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
                Telemetry.Processing.Record(timer.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("outcome", outcome));
                Baggage.Current = previousBaggage;
            }
        };
        await channel.BasicConsumeAsync(Infrastructure.Queue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Kitchen listening on {Queue}", Infrastructure.Queue);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task SaveAsync(OrderMessage order, string status, CancellationToken ct)
    {
        // Order and notification intent commit atomically. A duplicate never changes a terminal status.
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            WITH inserted AS (
                INSERT INTO orders (id, dish, status) VALUES ($1, $2, $3)
                ON CONFLICT (id) DO NOTHING RETURNING id
            )
            INSERT INTO notification_outbox (order_id, payload, traceparent)
            SELECT id, $4, $5 FROM inserted
            """;
        command.Parameters.AddWithValue(order.Id);
        command.Parameters.AddWithValue(order.Dish);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(new NotificationMessage(order.Id, order.Dish, status, order.Scenario)));
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)Activity.Current?.Id ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
