using BabyLog.Domain;
using BabyLog.Infrastructure;
using BabyLog.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BabyLog;

public static class BackendServiceExtensions
{
    public static IServiceCollection AddBabyLogBackend(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BabyLog") ?? "Data Source=data/babylog.db";
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (builder.DataSource != ":memory:" && !string.IsNullOrEmpty(builder.DataSource))
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(builder.DataSource))!);
        services.AddDbContext<BabyLogDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IBabyLogService, BabyLogService>();
        return services;
    }
    public static async Task InitializeBabyLogAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BabyLogDbContext>();
        var created = await db.Database.EnsureCreatedAsync();
        // Seed only a new diary. Deleting its last activity must not reseed it on restart.
        if (!created || !app.Configuration.GetValue("BabyLog:SeedDemoData", false)) return;
        var repository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        var (today, _) = BabyLogService.Bounds(new DateOnly(2026, 11, 26));
        await repository.AddAsync(Activity.Create("milk", today.AddHours(8), null, 120, "Poppata del mattino"), default);
        await repository.AddAsync(Activity.Create("diaper", today.AddHours(9), null, null, null), default);
        await repository.AddAsync(Activity.Create("sleep", today.AddHours(10), today.AddHours(11), null, "Riposino"), default);
    }
}
