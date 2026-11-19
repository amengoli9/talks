namespace BabyLog.Distributed.Events;

public sealed record ActivityRecordedV1(
    Guid ActivityId, string Kind, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, int? Milliliters)
{
    public const string RoutingKey = "activity.recorded.v1";
}

public sealed record ActivityDeletedV1(Guid ActivityId)
{
    public const string RoutingKey = "activity.deleted.v1";
}
