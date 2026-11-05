using Kitchen.Api;
using Kitchen.Api.Controllers;
using Kitchen.Api.Infrastructure.Data;
using RestaurantDemo;

var builder = WebApplication.CreateBuilder(args);
builder.AddDemoTelemetry("Zagreb.Kitchen");
builder.AddKitchenServices();
builder.Services.AddControllers();
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<KitchenDbContext>();
    await database.EnsureSchemaAsync();
}
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapDemoEndpoints();
app.Run();
