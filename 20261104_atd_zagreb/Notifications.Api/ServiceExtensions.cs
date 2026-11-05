using Notifications.Api.Domain;
using Notifications.Api.Infrastructure;

namespace Notifications.Api;

public static class ServiceExtensions
{
    public static void AddNotificationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddSingleton<DemoNotificationDelivery>();
        builder.Services.AddSingleton<INotificationDelivery>(provider => provider.GetRequiredService<DemoNotificationDelivery>());
    }
}
