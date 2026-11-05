using System.Diagnostics;
using RestaurantDemo;
using Orders.Api.Utilities;

namespace Orders.Api.Domain;

public sealed class OrderService(
    IKitchenGateway kitchen, INotificationGateway notifications, ILogger<OrderService> logger) : IOrderService
{
    public async Task<OrderConfirmation> ConfirmAsync(string? dishId, CancellationToken cancellationToken)
    {
        #region examples
        var traceId = Activity.Current?.TraceId.ToString();
        var timer = Stopwatch.StartNew();

        #endregion
        using var activity = Telemetry.Source.StartActivity("order.confirm");

        
        var outcome = "accepted";
        var kitchenAccepted = false;
        try
        {
            var dish = Menu.Find(dishId);
            if (dish is null)
            {
                outcome = "rejected";
                return new(OrderOutcome.Rejected, "Please choose a dish from the menu.", traceId);
            }

            activity?.SetTag("order.dish", dish.Id);
            if (!await kitchen.AcceptAsync(dish.Id, cancellationToken))
            {
                outcome = "unavailable";
                logger.OrderDeclined(dish.Id);
                return new(OrderOutcome.Unavailable, $"Sorry, {dish.Name} is sold out. Please choose another dish.", traceId);
            }
            kitchenAccepted = true;

            // Notify only after the kitchen has accepted and saved the order. Never retry an accepted order.
            using (var notificationActivity = Telemetry.Source.StartActivity("order.notify"))
            {
                try
                {
                    await notifications.NotifyAsync(dish.Id, cancellationToken);
                }
                catch (GatewayUnavailableException exception)
                {
                    notificationActivity?.SetStatus(ActivityStatusCode.Error, "Order notification failed.");
                    notificationActivity?.AddException(exception.InnerException ?? exception);
                    throw;
                }
                notificationActivity?.SetTag("notification.sent", true);
            }

            logger.OrderConfirmed(dish.Id, timer.ElapsedMilliseconds, true);
            return new(OrderOutcome.Accepted, $"The kitchen has received your order: {dish.Name}.", traceId,
                dish.Name, KitchenAccepted: true, NotificationSent: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        catch (GatewayUnavailableException exception)
        {
            outcome = "error";
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception.InnerException ?? exception);
            logger.OrderFailed(kitchenAccepted, exception);
            var message = kitchenAccepted
                ? "The kitchen accepted your order, but the notification failed. Please ask your waiter; do not place the order again."
                : "We couldn't confirm your order. Please ask your waiter before trying again.";
            return new(exception.TimedOut ? OrderOutcome.TimedOut : OrderOutcome.Failed,
                message, traceId, KitchenAccepted: kitchenAccepted);
        }
        finally
        {
            activity?.SetTag("order.outcome", outcome);
            Telemetry.Orders.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
            Telemetry.Duration.Record(timer.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("outcome", outcome));
        }
    }
}
