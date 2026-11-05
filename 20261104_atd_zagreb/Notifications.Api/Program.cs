using Notifications.Api;
using Notifications.Api.Controllers;
using RestaurantDemo;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoTelemetry("Zagreb.Notifications");
builder.AddNotificationServices();
builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapDemoEndpoints();
app.Run();
