using Microsoft.EntityFrameworkCore;
using Kitchen.Api.Domain;

namespace Kitchen.Api.Infrastructure.Data;

public sealed class KitchenDbContext(DbContextOptions<KitchenDbContext> options) : DbContext(options)
{
    public DbSet<AcceptedOrder> AcceptedOrders => Set<AcceptedOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AcceptedOrder>().ToTable("AcceptedOrders", table =>
            table.HasCheckConstraint("CK_AcceptedOrders_DishName_Length", "length(\"DishName\") <= 32"));
    }

    public async Task EnsureSchemaAsync()
    {
        await Database.EnsureCreatedAsync();
        // EnsureCreated does not upgrade existing demo databases. Add the column and
        // its database constraint without rebuilding the table or deleting orders.
        await Database.OpenConnectionAsync();
        try
        {
            using var command = Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info(\"AcceptedOrders\")";
            var hasDishName = false;
            await using (var columns = await command.ExecuteReaderAsync())
            {
                while (await columns.ReadAsync())
                    hasDishName |= columns.GetString(1) == nameof(AcceptedOrder.DishName);
            }
            if (!hasDishName)
                await Database.ExecuteSqlRawAsync("""
                    ALTER TABLE "AcceptedOrders" ADD COLUMN "DishName" TEXT NOT NULL DEFAULT ''
                    CONSTRAINT "CK_AcceptedOrders_DishName_Length" CHECK (length("DishName") <= 32)
                    """);
        }
        finally { await Database.CloseConnectionAsync(); }
    }
}

