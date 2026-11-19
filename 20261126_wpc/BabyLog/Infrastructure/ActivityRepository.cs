using BabyLog.Domain;
using BabyLog.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BabyLog.Infrastructure;

public sealed class ActivityRepository(BabyLogDbContext db) : IActivityRepository
{
    public async Task<IReadOnlyList<Activity>> FindAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var start = from.UtcTicks;
        var end = until.UtcTicks;
        var rows = await db.Activities.AsNoTracking().Where(x => x.StartedAtTicks < end && (x.StartedAtTicks >= start || (x.Kind == "sleep" && x.EndedAtTicks > start))).OrderByDescending(x => x.StartedAtTicks).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        return rows.Select(x => new Activity(x.Id, x.Kind, new(x.StartedAtTicks, TimeSpan.Zero), x.EndedAtTicks is { } ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : null, x.Milliliters, x.Notes)).ToArray();
    }
    public async Task AddAsync(Activity activity, CancellationToken cancellationToken)
    {
        db.Activities.Add(new() { Id = activity.Id, Kind = activity.Kind, StartedAtTicks = activity.StartedAt.UtcTicks, EndedAtTicks = activity.EndedAt?.UtcTicks, Milliliters = activity.Milliliters, Notes = activity.Notes });
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) => await db.Activities.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}
