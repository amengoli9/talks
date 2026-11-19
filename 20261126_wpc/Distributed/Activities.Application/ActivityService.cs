using BabyLog.Domain;

namespace BabyLog.Distributed.Activities.Application;

public sealed record RecordActivity(
    string Kind, DateTimeOffset StartedAt, DateTimeOffset? EndedAt = null,
    int? Milliliters = null, string? Notes = null);

public sealed class ActivityService(IActivityStore store)
{
    public async Task<Activity> RecordAsync(RecordActivity command, CancellationToken ct)
    {
        var activity = Activity.Create(command.Kind, command.StartedAt, command.EndedAt, command.Milliliters, command.Notes);
        await store.AddAsync(activity, ct);
        return activity;
    }

    public Task<IReadOnlyList<Activity>> ListAsync(DateOnly date, CancellationToken ct)
    {
        var (start, end) = RomeDay.Bounds(date);
        return store.FindAsync(start, end, ct);
    }

    public Task<Activity?> GetAsync(Guid id, CancellationToken ct) => store.GetAsync(id, ct);

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => store.DeleteAsync(id, ct);
}
