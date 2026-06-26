using Microsoft.EntityFrameworkCore;
using HexagonalPiadineria.Domain;

namespace HexagonalPiadineria.Infrastructure;

public sealed class PiadineriaDbContext : DbContext
{
    public PiadineriaDbContext(DbContextOptions<PiadineriaDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var order = model.Entity<Order>();
        order.HasKey(o => o.Table);
        order.Ignore(o => o.Subtotal);
        order.Ignore(o => o.Discount);
        order.Ignore(o => o.Total);

        order.OwnsMany(o => o.Lines, line =>
        {
            line.Ignore(l => l.Total);
            line.Property(l => l.UnitPrice)
                .HasConversion(euro => euro.Amount, amount => new Euro(amount));
        });
    }
}
