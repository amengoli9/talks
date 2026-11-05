using System.Diagnostics;
using RestaurantDemo;
using Kitchen.Api.Utilities;

namespace Kitchen.Api.Domain;

public sealed class KitchenService(
    IAvailability availability, IAcceptedOrderRepository orders, ILogger<KitchenService> logger) : IKitchenService
{
    public async Task<KitchenAcceptance> AcceptAsync(string? dishId, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Source.StartActivity("kitchen.accept_order");
        var dish = Menu.Find(dishId);
        if (dish is null)
            return new(KitchenOutcome.Rejected);

        bool available;
        try { available = await availability.IsAvailableAsync(dish.Id, cancellationToken); }
        catch (ManagementUnavailableException exception)
        {
            activity?.SetTag("order.outcome", "error");
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            return new(KitchenOutcome.ManagementUnavailable);
        }
        activity?.SetTag("order.outcome", available ? "accepted" : "unavailable");
        if (!available)
        {
            logger.DishSoldOut(dish.Id);
            return new(KitchenOutcome.Unavailable, dish.Id);
        }

        try
        {
            // The repository's EF span stays beneath kitchen.accept_order.
            await orders.AddAsync(new AcceptedOrder
            {
                Dish = dish.Id, DishName = dish.Name, AcceptedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
        catch (OrderStorageException exception)
        {
            activity?.SetTag("order.outcome", "error");
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception.InnerException ?? exception);
            logger.OrderStorageFailed(dish.Id, exception);
            return new(KitchenOutcome.StorageUnavailable);
        }

        logger.OrderSaved(dish.Id);
        return new(KitchenOutcome.Accepted, dish.Id);
    }
}
