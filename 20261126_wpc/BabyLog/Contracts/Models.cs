namespace BabyLog.Contracts;

public sealed record BabyProfile(string Name, DateOnly BirthDate);
public sealed record CreateActivityRequest(string Kind, DateTimeOffset StartedAt, DateTimeOffset? EndedAt = null, int? Milliliters = null, string? Notes = null);
public sealed record ActivityEntry(Guid Id, string Kind, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, int? Milliliters, string? Notes);
public sealed record DailySummary(DateOnly Date, int MilkMilliliters, int DiaperCount, int SleepMinutes);
