namespace Notifications.Api.Domain;

public interface INotificationDelivery
{
    Task DeliverAsync(string dish, CancellationToken cancellationToken);
}
