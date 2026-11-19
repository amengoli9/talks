using BabyLog.Distributed;
using BabyLog.Distributed.Activities.Application;
using BabyLog.Distributed.Activities.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.AddKeycloak("babylog-activities");
builder.Services.AddActivities(
    builder.Configuration.GetConnectionString("Activities") ?? "Data Source=activities.db",
    builder.Configuration["RabbitMq:Uri"] ?? "amqp://babylog:babylog@localhost:5672/");

var app = builder.Build();
await app.Services.InitializeActivitiesAsync();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/baby", () => new { name = "Sofia", birthDate = new DateOnly(2026, 10, 1) });
api.MapGet("/activities", async (DateOnly date, ActivityService service, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await service.ListAsync(date, ct));
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.Problem(statusCode: 400, title: "Data non valida", detail: ex.Message);
    }
});
api.MapGet("/activities/{id:guid}", async (Guid id, ActivityService service, CancellationToken ct) =>
    await service.GetAsync(id, ct) is { } activity ? Results.Ok(activity) : Results.NotFound());
api.MapPost("/activities", async (RecordActivity request, ActivityService service, CancellationToken ct) =>
{
    try
    {
        var activity = await service.RecordAsync(request, ct);
        return Results.Created($"/api/activities/{activity.Id}", activity);
    }
    catch (ArgumentException ex)
    {
        return Results.Problem(statusCode: 400, title: "Attività non valida", detail: ex.Message);
    }
});
api.MapDelete("/activities/{id:guid}", async (Guid id, ActivityService service, CancellationToken ct) =>
    await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

await app.RunAsync();
