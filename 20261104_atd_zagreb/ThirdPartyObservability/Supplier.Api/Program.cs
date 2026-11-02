using System.Diagnostics;
using ThirdPartyDemo;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5282");
builder.AddDemoTelemetry("Zagreb.ThirdParty.Supplier");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
app.MapGet("/stock/{dish}", async (string dish, string? scenario, HttpRequest request, ILogger<Program> logger, CancellationToken ct) =>
{
    Activity.Current?.SetTag("demo.scenario", scenario);
    if (!Infrastructure.Dishes.Contains(dish)) return Results.NotFound();
    if (scenario == "slow-supplier") await Task.Delay(TimeSpan.FromSeconds(2), ct);
    if (scenario == "supplier-down" || scenario == "supplier-retry" && request.Headers["X-Demo-Attempt"] != "2")
    {
        logger.LogWarning("Demo supplier outage for {Dish}, scenario {Scenario}", dish, scenario);
        return Results.Json(new { message = "Simulated supplier outage" }, statusCode: 503);
    }
    return Results.Ok(new AvailabilityReply(scenario != "sold-out"));
});
app.Run();
