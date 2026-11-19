using System.Text.Json;
using BabyLog.Distributed.Activities.Application;
using BabyLog.Distributed.Events;
using BabyLog.Domain;
using Microsoft.EntityFrameworkCore;

namespace BabyLog.Distributed.Activities.Infrastructure;

internal sealed class SqliteActivityStore(ActivitiesDb db) : IActivityStore
{
    public async Task AddAsync(Activity activity, CancellationToken ct)
    {
        db.Activities.Add(ActivityRow.From(activity));
        db.Outbox.Add(new()
        {
            RoutingKey = ActivityRecordedV1.RoutingKey,
            Body = JsonSerializer.SerializeToUtf8Bytes(new ActivityRecordedV1(
                activity.Id, activity.Kind, activity.StartedAt, activity.EndedAt, activity.Milliliters))
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Activity>> FindAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var rows = await db.Activities.AsNoTracking()
            .Where(row => row.StartedAtTicks < end.UtcTicks &&
                (row.StartedAtTicks >= start.UtcTicks || (row.Kind == "sleep" && row.EndedAtTicks > start.UtcTicks)))
            .OrderByDescending(row => row.StartedAtTicks).ThenBy(row => row.Id)
            .ToListAsync(ct);
        return rows.Select(row => row.ToActivity()).ToArray();
    }

    public async Task<Activity?> GetAsync(Guid id, CancellationToken ct) =>
        (await db.Activities.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, ct))?.ToActivity();

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var deleted = await db.Activities.Where(row => row.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0)
            return false;

        db.Outbox.Add(new()
        {
            RoutingKey = ActivityDeletedV1.RoutingKey,
            Body = JsonSerializer.SerializeToUtf8Bytes(new ActivityDeletedV1(id))
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
