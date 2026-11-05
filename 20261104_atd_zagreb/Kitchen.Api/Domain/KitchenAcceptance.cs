namespace Kitchen.Api.Domain;

public enum KitchenOutcome { Accepted, Rejected, Unavailable, ManagementUnavailable, StorageUnavailable }

public sealed record KitchenAcceptance(KitchenOutcome Outcome, string? Dish = null);
