using System.Diagnostics;
using ThirdPartyDemo;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5281");
builder.AddDemoTelemetry("Zagreb.ThirdParty.Inventory");
builder.Services.AddHttpClient("supplier", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Supplier:BaseUrl"] ?? "http://localhost:5282");
    client.Timeout = TimeSpan.FromSeconds(5);
});
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
app.MapPost("/availability", async (OrderMessage order, IHttpClientFactory clients, ILogger<Program> logger, CancellationToken ct) =>
{
    using var activity = Telemetry.Source.StartActivity("inventory.check");
    activity?.SetTag("order.id", order.Id);
    using var client = clients.CreateClient("supplier");
    try
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"/stock/{Uri.EscapeDataString(order.Dish)}?scenario={Uri.EscapeDataString(order.Scenario)}");
            // Attempt is request-local: demonstrations do not mutate global supplier state.
            request.Headers.Add("X-Demo-Attempt", attempt.ToString());
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && attempt == 1)
            {
                Telemetry.Retries.Add(1, new KeyValuePair<string, object?>("dependency", "supplier"));
                logger.LogWarning("Supplier returned 503 for order {OrderId}; retrying once", order.Id);
                await Task.Delay(150, ct);
                continue;
            }
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AvailabilityReply>(ct)
                ?? throw new HttpRequestException("Supplier returned no availability.");
            logger.LogInformation("Inventory check for order {OrderId}: available={Available}", order.Id, result.Available);
            return Results.Ok(result);
        }
        throw new HttpRequestException("Supplier retry exhausted.");
    }
    catch (Exception e) when (e is HttpRequestException or OperationCanceledException && !ct.IsCancellationRequested)
    {
        Telemetry.Fail(activity, e);
        logger.LogError(e, "Supplier unavailable for order {OrderId}", order.Id);
        return Results.Json(new { message = "Supplier unavailable" }, statusCode: 503);
    }
});
app.Run();
