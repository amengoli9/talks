namespace BabyLog.Domain;

public interface IActivityRepository
{
    Task<IReadOnlyList<Activity>> FindAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken);
    Task AddAsync(Activity activity, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
