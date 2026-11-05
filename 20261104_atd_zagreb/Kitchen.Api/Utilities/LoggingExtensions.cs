namespace Kitchen.Api.Utilities;

public static partial class LoggingExtensions
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information,
        Message = "Cannot accept {Dish}: sold out")]
    public static partial void DishSoldOut(this ILogger logger, string dish);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error,
        Message = "Could not save the comanda for {Dish}")]
    public static partial void OrderStorageFailed(this ILogger logger, string dish, Exception exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
        Message = "Comanda saved for {Dish}; cooking happens after confirmation")]
    public static partial void OrderSaved(this ILogger logger, string dish);

    [LoggerMessage(EventId = 2100, Level = LogLevel.Warning,
        Message = "Availability cache expired; this order must wait for the management system")]
    public static partial void AvailabilityCacheExpired(this ILogger logger);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information,
        Message = "Management availability check: {Behavior}, delay {DelayMs} ms")]
    public static partial void ManagementAvailabilityChecked(this ILogger logger, string behavior, int delayMs);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Error,
        Message = "Management system temporarily unavailable; order cannot be confirmed")]
    public static partial void ManagementUnavailable(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Information,
        Message = "Reloading availability from management system; trigger {Trigger}, delay {DelayMs} ms")]
    public static partial void AvailabilityReloading(this ILogger logger, string trigger, int delayMs);

    [LoggerMessage(EventId = 2104, Level = LogLevel.Information,
        Message = "Availability refreshed; trigger {Trigger}, valid for {LifetimeSeconds} seconds")]
    public static partial void AvailabilityRefreshed(this ILogger logger, string trigger, double lifetimeSeconds);

    [LoggerMessage(EventId = 2105, Level = LogLevel.Information,
        Message = "Presenter selected demo scenario {Scenario}")]
    public static partial void DemoScenarioSelected(this ILogger logger, string scenario);
}
