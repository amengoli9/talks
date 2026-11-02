using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Kitchen.Api;
using RestaurantDemo;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoTelemetry("Zagreb.Kitchen");
// A local relational database gives the demo real EF command spans, with no database server.
builder.Services.AddDbContext<KitchenDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("Kitchen")
    ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "kitchen.db")}"));
builder.Services.AddSingleton<AvailabilityCache>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<AvailabilityCache>());
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<KitchenDbContext>();
    await database.EnsureSchemaAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/commands", async (OrderRequest order, AvailabilityCache availability, KitchenDbContext database,
    ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    using var activity = Telemetry.Source.StartActivity("kitchen.accept_order");
    var dish = Menu.Find(order.Dish);
    if (dish is null)
        return Results.BadRequest(new { message = "Unknown dish." });

    bool available;
    try { available = await availability.IsAvailableAsync(dish.Id, cancellationToken); }
    catch (ManagementUnavailableException exception)
    {
        activity?.SetTag("order.outcome", "error");
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.AddException(exception);
        return Results.Problem(statusCode: 503, title: "Kitchen availability service unavailable");
    }
    activity?.SetTag("order.outcome", available ? "accepted" : "unavailable");
    if (!available)
    {
        // A business refusal is not a broken kitchen: no error status or exception.
        logger.LogInformation("Cannot accept {Dish}: sold out", dish.Id);
        return Results.Conflict(new KitchenReply(false, dish.Id));
    }

    database.AcceptedOrders.Add(new AcceptedOrder { Dish = dish.Id, DishName = dish.Name, AcceptedAt = DateTimeOffset.UtcNow });
    try
    {
        // EF instrumentation creates a database span beneath kitchen.accept_order.
        await database.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception)
    {
        activity?.SetTag("order.outcome", "error");
        activity?.SetStatus(ActivityStatusCode.Error, "Could not save the order.");
        activity?.AddException(exception);
        logger.LogError(exception, "Could not save the comanda for {Dish}", dish.Id);
        return Results.Problem(statusCode: 503, title: "Kitchen order storage unavailable");
    }

    logger.LogInformation("Comanda saved for {Dish}; cooking happens after confirmation", dish.Id);
    return Results.Ok(new KitchenReply(true, dish.Id));
});

if (app.Environment.IsDevelopment())
{
    app.MapPost("/demo/{scenario}", async (string scenario, AvailabilityCache cache, CancellationToken ct) =>
    {
        if (scenario is not ("normal" or "slow" or "proactive" or "intermittent"))
            return Results.BadRequest(new { message = "Unknown demo scenario." });
        await cache.SetScenarioAsync(scenario, ct);
        return Results.Ok(new { scenario, message = scenario switch
        {
            "normal" => "Normal service. You can place an order.",
            "slow" => "Cache expired. The next order will wait 10 seconds for the management system.",
            "intermittent" => "Intermittent service: 50% fast, 25% slow (up to 5 s), 25% errors. Keep ordering.",
            _ => "Background refresh enabled. The management system is still slow: try ordering during a refresh."
        }});
    });
    app.MapGet("/demo/state", (AvailabilityCache cache) => cache.State);
}

app.Run();
