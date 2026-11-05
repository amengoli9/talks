namespace Kitchen.Api.Domain;

public interface IAcceptedOrderRepository
{
    Task AddAsync(AcceptedOrder order, CancellationToken cancellationToken);
}
