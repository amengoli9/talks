using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace ThirdPartyDemo;

public record OrderRequest(string? Dish, string Scenario = "normal");
public record OrderMessage(Guid Id, string Dish, string Scenario, DateTimeOffset CreatedAt);
public record OrderStatus(Guid Id, string Dish, string Status, DateTimeOffset UpdatedAt);
public record AvailabilityReply(bool Available);
public record PaymentReply(bool Authorized);
public record NotificationMessage(Guid Id, string Dish, string Status, string Scenario);

public static class Infrastructure
{
    public const string Queue = "restaurant.orders";
    public const string DeadQueue = "restaurant.orders.failed";
    public const string Notifications = "restaurant.notifications";
    public const string FailedNotifications = "restaurant.notifications.failed";
    public static readonly string[] Dishes = ["margherita", "carbonara", "risotto"];
    public static readonly string[] Scenarios = ["normal", "slow-db", "fail", "slow-supplier",
        "supplier-retry", "supplier-down", "sold-out", "payment-declined", "payment-timeout", "notification-fail"];

    public static void AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(_ => NpgsqlDataSource.Create(config.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=15432;Database=restaurant;Username=demo;Password=demo"));
    }

    public static async Task InitializeDatabaseAsync(NpgsqlDataSource database, CancellationToken ct = default)
    {
        // Both processes can start together; serialize their first schema creation.
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT pg_advisory_xact_lock(5280);
            CREATE TABLE IF NOT EXISTS orders (
                id uuid PRIMARY KEY,
                dish text NOT NULL,
                status text NOT NULL,
                updated_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE TABLE IF NOT EXISTS notification_outbox (
                order_id uuid PRIMARY KEY REFERENCES orders(id),
                payload text NOT NULL,
                traceparent text,
                dispatched_at timestamptz
            );
            CREATE TABLE IF NOT EXISTS notifications (
                order_id uuid PRIMARY KEY REFERENCES orders(id),
                status text NOT NULL,
                delivered_at timestamptz NOT NULL DEFAULT now()
            )
            """;
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public static Task<IConnection> ConnectRabbitAsync(IConfiguration config, string name, CancellationToken ct = default)
        => new ConnectionFactory
        {
            Uri = new Uri(config["RabbitMQ:Uri"] ?? "amqp://demo:demo@localhost:5673/"),
            ClientProvidedName = name,
            AutomaticRecoveryEnabled = true
        }.CreateConnectionAsync(ct);

    public static async Task DeclareQueuesAsync(IChannel channel, CancellationToken ct = default)
    {
        await channel.QueueDeclareAsync(DeadQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: ct);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = DeadQueue
            }, cancellationToken: ct);
        await channel.QueueDeclareAsync(FailedNotifications, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: ct);
        await channel.QueueDeclareAsync(Notifications, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = FailedNotifications
            }, cancellationToken: ct);
    }

    public static Activity? StartMessaging(string operation, ActivityKind kind, ActivityContext parent = default,
        string queue = Queue)
    {
        var activity = Telemetry.Source.StartActivity($"{queue} {operation}", kind, parent);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag("messaging.operation.type", operation);
        return activity;
    }

    public static Dictionary<string, object?> InjectContext()
    {
        var headers = new Dictionary<string, object?>();
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(Activity.Current?.Context ?? default, Baggage.Current), headers,
            (carrier, key, value) => carrier[key] = Encoding.UTF8.GetBytes(value));
        return headers;
    }

    public static PropagationContext ExtractContext(IDictionary<string, object?>? headers)
        => Propagators.DefaultTextMapPropagator.Extract(default, headers,
            (carrier, key) => carrier is not null && carrier.TryGetValue(key, out var value)
                && value is byte[] bytes ? [Encoding.UTF8.GetString(bytes)] : []);
}
