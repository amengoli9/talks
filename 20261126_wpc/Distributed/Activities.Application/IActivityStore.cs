using BabyLog.Domain;

namespace BabyLog.Distributed.Activities.Application;

public interface IActivityStore
{
    Task AddAsync(Activity activity, CancellationToken ct);
    Task<IReadOnlyList<Activity>> FindAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<Activity?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}
