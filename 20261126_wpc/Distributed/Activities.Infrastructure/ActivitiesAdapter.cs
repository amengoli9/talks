using BabyLog.Distributed.Activities.Application;
using BabyLog.Distributed.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabyLog.Distributed.Activities.Infrastructure;

public static class ActivitiesAdapter
{
    public static void AddActivities(this IServiceCollection services, string database, string rabbitMqUri)
    {
        services.AddDbContext<ActivitiesDb>(options => options.UseSqlite(database));
        services.AddScoped<IActivityStore, SqliteActivityStore>();
        services.AddScoped<ActivityService>();
        services.AddSingleton(ActivityTopology.CreateFactory(rabbitMqUri, "babylog-activities"));
        services.AddHostedService<OutboxPublisher>();
    }

    public static async Task InitializeActivitiesAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ActivitiesDb>().Database.EnsureCreatedAsync();
    }
}
