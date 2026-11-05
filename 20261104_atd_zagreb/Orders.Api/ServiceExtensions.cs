using Orders.Api.Domain;
using Orders.Api.Infrastructure;

namespace Orders.Api;

public static class ServiceExtensions
{
    public static void AddOrdersServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IOrderService, OrderService>();
        // Named client shared with the presenter controls; typed adapter used by the domain.
        builder.Services.AddHttpClient("kitchen", client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["Kitchen:BaseUrl"] ?? "http://localhost:5271");
            client.Timeout = TimeSpan.FromSeconds(20);
        }).AddTypedClient<IKitchenGateway, HttpKitchenGateway>();
        builder.Services.AddHttpClient<INotificationGateway, HttpNotificationGateway>(client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["Notifications:BaseUrl"] ?? "http://localhost:5272");
            client.Timeout = TimeSpan.FromSeconds(2);
        });
        // Blazor calls the same public HTTP API as requests.http.
        builder.Services.AddHttpClient("orders", client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["Orders:BaseUrl"] ?? "http://localhost:5270");
            client.Timeout = TimeSpan.FromSeconds(25);
        });
    }
}
