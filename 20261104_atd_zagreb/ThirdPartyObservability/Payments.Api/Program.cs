using System.Diagnostics;
using ThirdPartyDemo;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5283");
builder.AddDemoTelemetry("Zagreb.ThirdParty.Payments");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
app.MapPost("/authorizations", async (OrderMessage order, ILogger<Program> logger, CancellationToken ct) =>
{
    using var activity = Telemetry.Source.StartActivity("payment.authorize");
    activity?.SetTag("order.id", order.Id);
    // Demonstration only: no cards, payment provider, or money movement.
    if (order.Scenario == "payment-timeout") await Task.Delay(TimeSpan.FromSeconds(5), ct);
    var authorized = order.Scenario != "payment-declined";
    activity?.SetTag("payment.outcome", authorized ? "authorized" : "declined");
    logger.LogInformation("Simulated authorization for order {OrderId}: {Outcome}", order.Id,
        authorized ? "authorized" : "declined");
    return Results.Ok(new PaymentReply(authorized));
});
app.Run();
