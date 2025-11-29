using KitchenApp.Domain.Models;
using Microsoft.Extensions.Logging;

namespace KitchenApp.Utilities;

public static partial class LoggingExtensions
{

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Exception caught: {Message}")]
    public static partial void LogAppError(this ILogger logger, string message, Exception? exception = null);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Get dish: {DishName}")]
    public static partial void LogDishNameInfo(this ILogger logger, string message, string dishname);

    //[LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Get dish")]
    //public static partial void LogDishInfo(this ILogger logger, string message, [LogPropertiesAttribute] Dish dish);

}


