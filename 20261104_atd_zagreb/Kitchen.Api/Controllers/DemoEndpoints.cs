using Kitchen.Api.Infrastructure;

namespace Kitchen.Api.Controllers;

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
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
    }
}
