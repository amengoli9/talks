namespace Orders.Api.Domain;

public interface INotificationGateway
{
    Task NotifyAsync(string dish, CancellationToken cancellationToken);
}
