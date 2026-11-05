using Notifications.Api.Domain;
using Notifications.Api.Utilities;

namespace Notifications.Api.Infrastructure;

// Local outbound adapter: logs delivery instead of sending email or SMS.
public sealed class DemoNotificationDelivery(ILogger<DemoNotificationDelivery> logger) : INotificationDelivery
{
    private int failureMode;

    public async Task DeliverAsync(string dish, CancellationToken cancellationToken)
    {
        var mode = Volatile.Read(ref failureMode);
        if (mode == 1)
            throw new InvalidOperationException("Demo notification delivery failed.");
        if (mode == 2)
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        logger.OrderNotificationReceived(dish);
    }

    public void SetScenario(string scenario)
        => Interlocked.Exchange(ref failureMode, scenario switch { "error" => 1, "slow" => 2, _ => 0 });
}
