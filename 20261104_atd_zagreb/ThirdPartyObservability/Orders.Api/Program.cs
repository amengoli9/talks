using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using RabbitMQ.Client;
using StackExchange.Redis;
using ThirdPartyDemo;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5280");
builder.AddDemoTelemetry("Zagreb.ThirdParty.Orders");
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:56379,abortConnect=false,asyncTimeout=2000");
    // Redis is optional: do not queue cache commands while its connection is down.
    options.BacklogPolicy = BacklogPolicy.FailFast;
    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddSingleton<OrderPublisher>();
var app = builder.Build();
await Infrastructure.InitializeDatabaseAsync(app.Services.GetRequiredService<NpgsqlDataSource>());
await app.Services.GetRequiredService<OrderPublisher>().StartAsync();

app.MapGet("/", () => new
{
    demo = "RabbitMQ + Redis + PostgreSQL",
    menu = Infrastructure.Dishes,
    submit = "POST /orders: {\"dish\":\"margherita\",\"scenario\":\"normal\"}",
    scenarios = Infrastructure.Scenarios,
    status = "GET /orders/{id}",
    dashboard = "http://localhost:18898"
});
app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (NpgsqlDataSource database, OrderPublisher publisher, CancellationToken ct) =>
{
    try
    {
        await using var command = database.CreateCommand("SELECT 1");
        await command.ExecuteScalarAsync(ct);
        return publisher.IsConnected ? Results.Ok(new { status = "ready" })
            : Results.Json(new { status = "broker unavailable" }, statusCode: 503);
    }
    catch (NpgsqlException) { return Results.Json(new { status = "database unavailable" }, statusCode: 503); }
});

app.MapGet("/orders/{id:guid}/notification", async (Guid id, NpgsqlDataSource database, CancellationToken ct) =>
{
    await using var command = database.CreateCommand("""
        SELECT o.status, b.dispatched_at, n.delivered_at FROM orders o
        LEFT JOIN notification_outbox b ON b.order_id = o.id
        LEFT JOIN notifications n ON n.order_id = o.id WHERE o.id = $1
        """);
    command.Parameters.AddWithValue(id);
    await using var reader = await command.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct)) return Results.NotFound();
    return Results.Ok(new { id, orderStatus = reader.GetString(0),
        notification = !reader.IsDBNull(2) ? "delivered" : !reader.IsDBNull(1) ? "dispatched" : "pending" });
});

app.MapPost("/orders", async (OrderRequest request, OrderPublisher publisher, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!Infrastructure.Dishes.Contains(request.Dish) || !Infrastructure.Scenarios.Contains(request.Scenario))
        return Results.BadRequest(new { message = "Choose a menu dish and a scenario from GET /.", scenarios = Infrastructure.Scenarios });

    var order = new OrderMessage(Guid.NewGuid(), request.Dish!, request.Scenario, DateTimeOffset.UtcNow);
    try
    {
        await publisher.PublishAsync(order, ct);
        Telemetry.Orders.Add(1, new KeyValuePair<string, object?>("outcome", "queued"));
        logger.LogInformation("Order {OrderId} published; kitchen processing is asynchronous", order.Id);
        return Results.Accepted($"/orders/{order.Id}", new
        {
            order.Id, status = "queued", traceId = Activity.Current?.TraceId.ToString()
        });
    }
    catch (Exception e) when (!ct.IsCancellationRequested)
    {
        logger.LogError(e, "Publishing order {OrderId} failed; delivery may be uncertain", order.Id);
        Telemetry.Orders.Add(1, new KeyValuePair<string, object?>("outcome", "publish-error"));
        return Results.Json(new { message = "Broker confirmation failed. Check this ID before retrying.",
            order.Id, traceId = Activity.Current?.TraceId.ToString() }, statusCode: 503);
    }
});

app.MapGet("/orders/{id:guid}", async (Guid id, NpgsqlDataSource database,
    IConnectionMultiplexer redis, ILogger<Program> logger, CancellationToken ct) =>
{
    var cache = redis.GetDatabase();
    var key = $"order:{id}";
    var cacheAvailable = true;
    using var lookup = Telemetry.Source.StartActivity("order.status");
    try
    {
        using var get = Telemetry.Source.StartActivity("GET order", ActivityKind.Client);
        get?.SetTag("db.system.name", "redis");
        get?.SetTag("db.operation.name", "GET");
        try
        {
            var cached = await cache.StringGetAsync(key);
            var hit = cached.HasValue;
            get?.SetTag("cache.hit", hit);
            Telemetry.Cache.Add(1, new KeyValuePair<string, object?>("outcome", hit ? "hit" : "miss"));
            logger.LogInformation("Order status cache {CacheOutcome} for {OrderId}", hit ? "hit" : "miss", id);
            if (hit) return Results.Content(cached.ToString(), "application/json");
        }
        catch (RedisException e)
        {
            cacheAvailable = false;
            Telemetry.Fail(get, e);
            Telemetry.Cache.Add(1, new KeyValuePair<string, object?>("outcome", "error"));
            logger.LogWarning(e, "Redis unavailable; falling back to PostgreSQL");
        }
    }
    catch (Exception e) { Telemetry.Fail(lookup, e); throw; }

    await using var command = database.CreateCommand("SELECT dish, status, updated_at FROM orders WHERE id = $1");
    command.Parameters.AddWithValue(id);
    OrderStatus status;
    await using (var reader = await command.ExecuteReaderAsync(ct))
    {
        if (!await reader.ReadAsync(ct))
            return Results.NotFound(new { id, message = "Not processed yet, or unknown ID. Try again shortly." });
        status = new(id, reader.GetString(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2));
    }

    var json = JsonSerializer.Serialize(status, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    // Only terminal states are cached, so a cached value cannot hide worker progress.
    if (cacheAvailable)
    {
        using var set = Telemetry.Source.StartActivity("SET order", ActivityKind.Client);
        set?.SetTag("db.system.name", "redis");
        set?.SetTag("db.operation.name", "SET");
        try { await cache.StringSetAsync(key, json, TimeSpan.FromSeconds(30)); }
        catch (RedisException e)
        {
            Telemetry.Fail(set, e);
            logger.LogWarning(e, "Unable to cache order status; database result still returned");
        }
    }
    return Results.Content(json, "application/json");
});

app.Run();

// Each request owns its channel. Concurrent publishes never share a channel.
sealed class OrderPublisher(IConfiguration config) : IAsyncDisposable
{
    private IConnection? connection;
    public bool IsConnected => connection?.IsOpen == true;
    public async Task StartAsync() => connection = await Infrastructure.ConnectRabbitAsync(config, "third-party-orders");

    public async Task PublishAsync(OrderMessage order, CancellationToken ct)
    {
        using var activity = Infrastructure.StartMessaging("send", ActivityKind.Producer, Activity.Current?.Context ?? default);
        activity?.SetTag("messaging.message.id", order.Id.ToString());
        try
        {
            await using var channel = await connection!.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
            await Infrastructure.DeclareQueuesAsync(channel, ct);
            var properties = new BasicProperties
            {
                ContentType = "application/json", Persistent = true, MessageId = order.Id.ToString(),
                Headers = Infrastructure.InjectContext()
            };
            await channel.BasicPublishAsync("", Infrastructure.Queue, mandatory: true, properties,
                JsonSerializer.SerializeToUtf8Bytes(order), ct);
        }
        catch (Exception e) { Telemetry.Fail(activity, e); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null) await connection.DisposeAsync();
    }
}
