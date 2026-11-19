using BabyLog.Domain;
using Microsoft.EntityFrameworkCore;

namespace BabyLog.Distributed.Activities.Infrastructure;

internal sealed class ActivitiesDb(DbContextOptions<ActivitiesDb> options) : DbContext(options)
{
    public DbSet<ActivityRow> Activities => Set<ActivityRow>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityRow>().HasIndex(row => row.StartedAtTicks);
        modelBuilder.Entity<OutboxMessage>().HasKey(message => message.Sequence);
    }
}

internal sealed class ActivityRow
{
    public Guid Id { get; init; }
    public required string Kind { get; init; }
    public long StartedAtTicks { get; init; }
    public long? EndedAtTicks { get; init; }
    public int? Milliliters { get; init; }
    public string? Notes { get; init; }

    public static ActivityRow From(Activity activity) => new()
    {
        Id = activity.Id,
        Kind = activity.Kind,
        StartedAtTicks = activity.StartedAt.UtcTicks,
        EndedAtTicks = activity.EndedAt?.UtcTicks,
        Milliliters = activity.Milliliters,
        Notes = activity.Notes
    };

    public Activity ToActivity() =>
        new(Id, Kind, new DateTimeOffset(StartedAtTicks, TimeSpan.Zero),
            EndedAtTicks is { } ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : null, Milliliters, Notes);
}

internal sealed class OutboxMessage
{
    public long Sequence { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public required string RoutingKey { get; init; }
    public required byte[] Body { get; init; }
}
