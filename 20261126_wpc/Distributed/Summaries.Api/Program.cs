using BabyLog.Distributed;
using BabyLog.Distributed.Summaries.Application;
using BabyLog.Distributed.Summaries.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.AddKeycloak("babylog-summaries");
builder.Services.AddSummaries(
    builder.Configuration.GetConnectionString("Summaries") ?? "Data Source=summaries.db",
    builder.Configuration["RabbitMq:Uri"] ?? "amqp://babylog:babylog@localhost:5672/");

var app = builder.Build();
await app.Services.InitializeSummariesAsync();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/summary", async (DateOnly date, SummaryService service, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await service.GetAsync(date, ct));
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.Problem(statusCode: 400, title: "Data non valida", detail: ex.Message);
    }
}).RequireAuthorization();

await app.RunAsync();
