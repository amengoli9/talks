using Orders.Api.Domain;
using RestaurantDemo;

namespace Orders.Api.Infrastructure;

public sealed class HttpNotificationGateway(HttpClient client) : INotificationGateway
{
    public async Task NotifyAsync(string dish, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("/notifications", new OrderRequest(dish), cancellationToken);
            response.EnsureSuccessStatusCode();
            var reply = await response.Content.ReadFromJsonAsync<NotificationReply>(cancellationToken);
            if (reply is not { Accepted: true })
                throw new HttpRequestException("Invalid notification confirmation.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            throw new GatewayUnavailableException(exception.Message, exception is OperationCanceledException, exception);
        }
    }
}
