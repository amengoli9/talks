using Microsoft.EntityFrameworkCore;

namespace BabyLog.Infrastructure.Data;

public sealed class BabyLogDbContext(DbContextOptions<BabyLogDbContext> options) : DbContext(options)
{
    public DbSet<ActivityRow> Activities => Set<ActivityRow>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ActivityRow>();
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Kind).HasMaxLength(10);
        entity.Property(x => x.Notes).HasMaxLength(200);
        entity.HasIndex(x => x.StartedAtTicks);
    }
}

public sealed class ActivityRow
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public long StartedAtTicks { get; set; }
    public long? EndedAtTicks { get; set; }
    public int? Milliliters { get; set; }
    public string? Notes { get; set; }
}
