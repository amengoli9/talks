using BabyLog.Distributed.Summaries.Application;
using BabyLog.Distributed.Summaries.Domain;
using Microsoft.EntityFrameworkCore;

namespace BabyLog.Distributed.Summaries.Infrastructure;

internal sealed class SqliteSummaryProjection(SummariesDb db) : ISummaryProjection
{
    public async Task ApplyRecordedAsync(SummaryActivity activity, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Activities (Id, Kind, StartedAtTicks, EndedAtTicks, Milliliters, IsDeleted)
            VALUES ({activity.Id}, {activity.Kind}, {activity.StartedAt.UtcTicks}, {activity.EndedAt?.UtcTicks}, {activity.Milliliters}, 0)
            ON CONFLICT (Id) DO NOTHING;
            """, ct);
    }

    public async Task ApplyDeletedAsync(Guid id, CancellationToken ct)
    {
        // Keep the ID so a redelivered or delayed recorded event cannot restore a deleted activity.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Activities (Id, IsDeleted) VALUES ({id}, 1)
            ON CONFLICT (Id) DO UPDATE SET
                Kind = NULL, StartedAtTicks = NULL, EndedAtTicks = NULL, Milliliters = NULL, IsDeleted = 1;
            """, ct);
    }

    public async Task<IReadOnlyList<SummaryActivity>> FindAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var rows = await db.Activities.AsNoTracking().Where(row =>
            !row.IsDeleted && row.StartedAtTicks < end.UtcTicks &&
            (row.StartedAtTicks >= start.UtcTicks || (row.Kind == "sleep" && row.EndedAtTicks > start.UtcTicks)))
            .ToListAsync(ct);

        return rows.Select(row => new SummaryActivity(row.Id, row.Kind!,
            new DateTimeOffset(row.StartedAtTicks!.Value, TimeSpan.Zero),
            row.EndedAtTicks is { } ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : null, row.Milliliters)).ToArray();
    }
}
