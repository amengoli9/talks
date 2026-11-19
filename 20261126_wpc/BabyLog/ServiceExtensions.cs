using BabyLog.Domain;
using BabyLog.Infrastructure;
using Microsoft.AspNetCore.DataProtection;

namespace BabyLog;

public static class ServiceExtensions
{
    public static void AddBabyLogPresentation(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddHttpClient<IBabyLogGateway, HttpBabyLogGateway>(client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["BabyLog:BaseUrl"] ?? "http://127.0.0.1:5080");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        var keysPath = builder.Configuration["DataProtection:Path"] ?? "data/keys";
        Directory.CreateDirectory(keysPath);
        builder.Services.AddDataProtection().SetApplicationName("BabyLog")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    }
}
