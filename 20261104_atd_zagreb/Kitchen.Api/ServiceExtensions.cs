using Kitchen.Api.Domain;
using Kitchen.Api.Infrastructure;
using Kitchen.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kitchen.Api;

public static class ServiceExtensions
{
    public static void AddKitchenServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IKitchenService, KitchenService>();
        builder.Services.AddScoped<IAcceptedOrderRepository, AcceptedOrderRepository>();
        builder.Services.AddDbContext<KitchenDbContext>(options => options.UseSqlite(
            builder.Configuration.GetConnectionString("Kitchen")
            ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "kitchen.db")}"));
        // Requests, presenter controls and background refresh share one cache instance.
        builder.Services.AddSingleton<AvailabilityCache>();
        builder.Services.AddSingleton<IAvailability>(provider => provider.GetRequiredService<AvailabilityCache>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<AvailabilityCache>());
    }
}
