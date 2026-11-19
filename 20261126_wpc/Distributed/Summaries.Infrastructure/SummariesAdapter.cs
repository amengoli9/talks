using BabyLog.Distributed.RabbitMq;
using BabyLog.Distributed.Summaries.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabyLog.Distributed.Summaries.Infrastructure;

public static class SummariesAdapter
{
    public static void AddSummaries(this IServiceCollection services, string database, string rabbitMqUri)
    {
        services.AddDbContext<SummariesDb>(options => options.UseSqlite(database));
        services.AddScoped<ISummaryProjection, SqliteSummaryProjection>();
        services.AddScoped<SummaryService>();
        services.AddSingleton(ActivityTopology.CreateFactory(rabbitMqUri, "babylog-summaries"));
        services.AddHostedService<ActivityConsumer>();
    }

    public static async Task InitializeSummariesAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SummariesDb>().Database.EnsureCreatedAsync();
    }
}
