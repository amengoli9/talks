namespace Kitchen.Api.Domain;

public interface IKitchenService
{
    Task<KitchenAcceptance> AcceptAsync(string? dish, CancellationToken cancellationToken);
}
