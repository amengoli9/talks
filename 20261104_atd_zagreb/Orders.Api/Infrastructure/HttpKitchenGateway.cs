using System.Net;
using Orders.Api.Domain;
using RestaurantDemo;

namespace Orders.Api.Infrastructure;

public sealed class HttpKitchenGateway(HttpClient client) : IKitchenGateway
{
    public async Task<bool> AcceptAsync(string dish, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("/commands", new OrderRequest(dish), cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict)
                return false;
            response.EnsureSuccessStatusCode();
            var reply = await response.Content.ReadFromJsonAsync<KitchenReply>(cancellationToken);
            if (reply is not { Accepted: true } || reply.Dish != dish)
                throw new HttpRequestException("Invalid kitchen confirmation.");
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            throw new GatewayUnavailableException(exception.Message, exception is OperationCanceledException, exception);
        }
    }
}
