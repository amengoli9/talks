using Microsoft.EntityFrameworkCore;
using Piadineria.Analyzers.Sample.Domain;

namespace Piadineria.Analyzers.Sample.Infrastructure;

/// <summary>
/// Il bersaglio della regola ARCH001: erede di EF Core DbContext.
/// Vive nell'Infrastructure — qui può stare, è il suo posto.
/// </summary>
public sealed class PiadineriaDbContext : DbContext
{
    public PiadineriaDbContext(DbContextOptions<PiadineriaDbContext> options) : base(options) { }

    public DbSet<Piada> Piade => Set<Piada>();
}
