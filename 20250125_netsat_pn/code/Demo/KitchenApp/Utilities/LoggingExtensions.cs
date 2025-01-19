namespace KitchenApp.Utilities;

public static partial class LoggingExtensions
{

   [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Exception caught: {Message}")]
   public static partial void LogAppError(this ILogger logger, string message, Exception? exception = null);

}
