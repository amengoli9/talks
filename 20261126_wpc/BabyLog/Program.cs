using BabyLog;
using BabyLog.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Services.AddBabyLogBackend(builder.Configuration);
builder.AddBabyLogPresentation();
var app = builder.Build();
await app.InitializeBabyLogAsync();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

public partial class Program;
