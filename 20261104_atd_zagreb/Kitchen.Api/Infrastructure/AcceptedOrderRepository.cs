using Kitchen.Api.Domain;
using Kitchen.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kitchen.Api.Infrastructure;

public sealed class AcceptedOrderRepository(KitchenDbContext database) : IAcceptedOrderRepository
{
    public async Task AddAsync(AcceptedOrder order, CancellationToken cancellationToken)
    {
        database.AcceptedOrders.Add(order);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new OrderStorageException(exception);
        }
    }
}
