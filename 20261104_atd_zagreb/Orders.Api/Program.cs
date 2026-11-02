using System.Diagnostics;
using System.Net;
using RestaurantDemo;
using Orders.Api.Components;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoTelemetry("Zagreb.Orders");
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpClient("orders", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Orders:BaseUrl"] ?? "http://localhost:5270");
    client.Timeout = TimeSpan.FromSeconds(25);
});
builder.Services.AddHttpClient("kitchen", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Kitchen:BaseUrl"] ?? "http://localhost:5271");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient("notifications", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Notifications:BaseUrl"] ?? "http://localhost:5272");
    client.Timeout = TimeSpan.FromSeconds(2);
});
var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/menu", () => Menu.Items);
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/orders", async (OrderRequest order, IHttpClientFactory clients,
    ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    using var activity = Telemetry.Source.StartActivity("order.confirm");
    var traceId = Activity.Current?.TraceId.ToString();
    var timer = Stopwatch.StartNew();
    var outcome = "accepted";
    var kitchenAccepted = false;
    try
    {
        var dish = Menu.Find(order.Dish);
        if (dish is null)
        {
            outcome = "rejected";
            return Results.BadRequest(new { message = "Please choose a dish from the menu.", traceId });
        }

        activity?.SetTag("order.dish", dish.Id); // A bounded menu, no customer data.
        using var client = clients.CreateClient("kitchen");
        using var response = await client.PostAsJsonAsync("/commands", new OrderRequest(dish.Id), cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            outcome = "unavailable";
            logger.LogInformation("Order declined: {Dish} is sold out", dish.Id);
            return Results.Conflict(new { message = $"Sorry, {dish.Name} is sold out. Please choose another dish.", traceId });
        }
        response.EnsureSuccessStatusCode();
        var reply = await response.Content.ReadFromJsonAsync<KitchenReply>(cancellationToken);
        if (reply is not { Accepted: true } || reply.Dish != dish.Id)
            throw new HttpRequestException("Invalid kitchen confirmation.");
        kitchenAccepted = true;

        // Notify only after the kitchen has accepted and saved the order.
        // Notification failures fail this request, even though the kitchen has saved the order.
        var notificationSent = false;
        using (var notificationActivity = Telemetry.Source.StartActivity("order.notify"))
        {
            try
            {
                using var notifications = clients.CreateClient("notifications");
                using var notificationResponse = await notifications.PostAsJsonAsync(
                    "/notifications", new OrderRequest(dish.Id), cancellationToken);
                notificationResponse.EnsureSuccessStatusCode();
                var notification = await notificationResponse.Content.ReadFromJsonAsync<NotificationReply>(cancellationToken);
                if (notification is not { Accepted: true })
                    throw new HttpRequestException("Invalid notification confirmation.");
                notificationSent = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            {
                notificationActivity?.SetStatus(ActivityStatusCode.Error, "Order notification failed.");
                notificationActivity?.AddException(exception);
                throw;
            }
            notificationActivity?.SetTag("notification.sent", notificationSent);
        }

        logger.LogInformation("Kitchen accepted {Dish}; confirmation in {ElapsedMs} ms; notification sent: {NotificationSent}",
            dish.Id, timer.ElapsedMilliseconds, notificationSent);
        return Results.Ok(new { message = $"The kitchen has received your order: {dish.Name}.", dish = dish.Name, notificationSent, traceId });
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        outcome = "cancelled";
        throw;
    }
    catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
    {
        outcome = "error";
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.AddException(exception);
        logger.LogError(exception, "Order processing failed; kitchen accepted: {KitchenAccepted}", kitchenAccepted);
        var message = kitchenAccepted
            ? "The kitchen accepted your order, but the notification failed. Please ask your waiter; do not place the order again."
            : "We couldn't confirm your order. Please ask your waiter before trying again.";
        return Results.Json(new { message, kitchenAccepted, traceId },
            statusCode: exception is OperationCanceledException ? 504 : 502);
    }
    finally
    {
        activity?.SetTag("order.outcome", outcome);
        Telemetry.Orders.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        Telemetry.Duration.Record(timer.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("outcome", outcome));
    }
});

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

app.Run();
