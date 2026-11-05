using Notifications.Api.Infrastructure;

namespace Notifications.Api.Controllers;

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/demo/{scenario}", (string scenario, DemoNotificationDelivery delivery) =>
            {
                if (scenario is not ("normal" or "error" or "slow"))
                    return Results.BadRequest(new { message = "Unknown notification scenario." });
                delivery.SetScenario(scenario);
                return Results.Ok(new { scenario });
            });
        }
    }
}
