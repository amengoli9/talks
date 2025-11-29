using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using KitchenApp.Domain;
using KitchenApp.Domain.Models;
using KitchenApp.Infrastructure;
using KitchenApp.Infrastructure.Data;
using System.Reflection;
using KitchenApp.Utilities;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddOpenApi();


var connectionString = builder.Configuration.GetConnectionString("MainConnection");

builder.Services.Configure<ConnectionStringsOptions>(
    builder.Configuration.GetSection("ConnectionStrings"));

builder.Services.AddDbContext<KitchenContext>(options =>
    options.UseSqlServer(connectionString));


#region OTELStep1
builder.Services.AddOpenTelemetry()
      .ConfigureResource(resource => resource.AddService(serviceName: "KitchenApp", serviceVersion: Assembly.GetCallingAssembly().GetName().Version?.ToString()))
      .WithTracing(tracing => tracing
         .AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddSource(ApplicationDiagnostics.DataAccessSourceName)
         //.AddEntityFrameworkCoreInstrumentation(opt =>
         //{
         //    opt.EnrichWithIDbCommand = (activity, command) =>
         //    {
         //        activity.SetParentId(activity.ParentId);
         //    };

         //})
         .AddSqlClientInstrumentation(opt => {
             opt.EnrichWithSqlCommand = (activity, command) =>
                 {
                     activity.SetParentId(activity.ParentId);
                 };
             opt.RecordException = true;
         })
         .AddConsoleExporter()
         .AddOtlpExporter()
         )
      .WithMetrics(metrics => metrics
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddOtlpExporter()
            .AddConsoleExporter());
#endregion

builder.Services.AddScoped<IDishService, DishService>();
builder.Services.AddScoped<IDishRepository, DishRepository>();
builder.Services.AddScoped<IDrinkService, DrinkService>();
builder.Services.AddScoped<IDrinkRepository, DrinkRepository>();

var app = builder.Build();

SeedData.Initialize(app);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); 
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Kitchen API");
    });
}


app.UseAuthorization();
app.MapControllers();
app.Run();
