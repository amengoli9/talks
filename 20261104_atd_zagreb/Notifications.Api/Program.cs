using RestaurantDemo;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoTelemetry("Zagreb.Notifications");
var app = builder.Build();
var failureMode = 0;

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/notifications", async (OrderRequest order, ILogger<Program> logger, CancellationToken ct) =>
{
    using var activity = Telemetry.Source.StartActivity("notification.accept_order");
    var dish = Menu.Find(order.Dish);
    if (dish is null)
        return Results.BadRequest(new { message = "Unknown dish." });

    activity?.SetTag("order.dish", dish.Id);
    try
    {
        var mode = Volatile.Read(ref failureMode);
        if (mode == 1)
            throw new InvalidOperationException("Demo notification delivery failed.");
        if (mode == 2)
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
    }
    catch (Exception exception)
    {
        activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, exception.Message);
        activity?.AddException(exception);
        throw;
    }
    // Demo delivery: record the notification in logs, without sending email or SMS.
    logger.LogInformation("Order notification received for {Dish}", dish.Id);
    return Results.Ok(new NotificationReply(true));
});

if (app.Environment.IsDevelopment())
{
    app.MapPost("/demo/{scenario}", (string scenario) =>
    {
        if (scenario is not ("normal" or "error" or "slow"))
            return Results.BadRequest(new { message = "Unknown notification scenario." });
        Interlocked.Exchange(ref failureMode, scenario switch { "error" => 1, "slow" => 2, _ => 0 });
        return Results.Ok(new { scenario });
    });
}

app.Run();
