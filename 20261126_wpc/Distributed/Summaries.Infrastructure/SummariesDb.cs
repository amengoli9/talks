using Microsoft.EntityFrameworkCore;

namespace BabyLog.Distributed.Summaries.Infrastructure;

internal sealed class SummariesDb(DbContextOptions<SummariesDb> options) : DbContext(options)
{
    public DbSet<ProjectionRow> Activities => Set<ProjectionRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<ProjectionRow>().HasIndex(row => row.StartedAtTicks);
}

internal sealed class ProjectionRow
{
    public Guid Id { get; init; }
    public string? Kind { get; init; }
    public long? StartedAtTicks { get; init; }
    public long? EndedAtTicks { get; init; }
    public int? Milliliters { get; init; }
    public bool IsDeleted { get; init; }
}
