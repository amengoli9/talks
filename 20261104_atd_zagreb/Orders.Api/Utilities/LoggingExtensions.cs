namespace Orders.Api.Utilities;

public static partial class LoggingExtensions
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "Order declined: {Dish} is sold out")]
    public static partial void OrderDeclined(this ILogger logger, string dish);

    // Each placeholder becomes a typed property in the OTLP log record.
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Kitchen accepted {Dish}; confirmation in {ElapsedMs} ms; notification sent: {NotificationSent}")]
    public static partial void OrderConfirmed(this ILogger logger, string dish, long elapsedMs, bool notificationSent);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error,
        Message = "Order processing failed; kitchen accepted: {KitchenAccepted}")]
    public static partial void OrderFailed(this ILogger logger, bool kitchenAccepted, Exception exception);
}
