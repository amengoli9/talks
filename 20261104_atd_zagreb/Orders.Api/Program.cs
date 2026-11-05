using Orders.Api;
using Orders.Api.Components;
using Orders.Api.Controllers;
using RestaurantDemo;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoTelemetry("Zagreb.Orders");



builder.AddOrdersServices();
builder.Services.AddControllers();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapDemoEndpoints();
app.Run();
