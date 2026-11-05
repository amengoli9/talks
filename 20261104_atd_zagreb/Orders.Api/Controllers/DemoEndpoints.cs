namespace Orders.Api.Controllers;

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        // Local presenter controls, never part of the customer's order contract.
        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/demo/{scenario}", async (string scenario, IHttpClientFactory clients, CancellationToken ct) =>
            {
                if (scenario is not ("normal" or "slow" or "proactive" or "intermittent"))
                    return Results.BadRequest(new { message = "Unknown demo scenario." });
                try
                {
                    using var client = clients.CreateClient("kitchen");
                    using var response = await client.PostAsync($"/demo/{scenario}", null, ct);
                    return Results.Content(await response.Content.ReadAsStringAsync(ct), "application/json", statusCode: (int)response.StatusCode);
                }
                catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
                {
                    return Results.Json(new { message = "Start Kitchen.Api as well, then try again." }, statusCode: 502);
                }
            });
        }
    }
}
