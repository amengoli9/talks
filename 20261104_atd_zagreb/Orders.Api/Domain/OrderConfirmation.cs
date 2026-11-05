namespace Orders.Api.Domain;

public enum OrderOutcome { Accepted, Rejected, Unavailable, Failed, TimedOut }

public sealed record OrderConfirmation(
    OrderOutcome Outcome, string Message, string? TraceId,
    string? Dish = null, bool KitchenAccepted = false, bool NotificationSent = false);
