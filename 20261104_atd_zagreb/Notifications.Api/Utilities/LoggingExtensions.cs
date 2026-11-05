namespace Notifications.Api.Utilities;

public static partial class LoggingExtensions
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information,
        Message = "Order notification received for {Dish}")]
    public static partial void OrderNotificationReceived(this ILogger logger, string dish);
}
