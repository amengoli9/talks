namespace Orders.Api.Domain;

public interface IOrderService
{
    Task<OrderConfirmation> ConfirmAsync(string? dish, CancellationToken cancellationToken);
}
