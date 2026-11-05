namespace Notifications.Api.Domain;

public interface INotificationService
{
    Task<bool> NotifyAsync(string? dish, CancellationToken cancellationToken);
}
