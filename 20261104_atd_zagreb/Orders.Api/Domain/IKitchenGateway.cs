namespace Orders.Api.Domain;

public interface IKitchenGateway
{
    Task<bool> AcceptAsync(string dish, CancellationToken cancellationToken);
}
