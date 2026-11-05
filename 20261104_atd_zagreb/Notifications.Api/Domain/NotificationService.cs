using System.Diagnostics;
using RestaurantDemo;

namespace Notifications.Api.Domain;

public sealed class NotificationService(INotificationDelivery delivery) : INotificationService
{
    public async Task<bool> NotifyAsync(string? dishId, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Source.StartActivity("notification.accept_order");
        var dish = Menu.Find(dishId);
        if (dish is null)
            return false;

        activity?.SetTag("order.dish", dish.Id);
        try
        {
            await delivery.DeliverAsync(dish.Id, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            throw;
        }
    }
}
