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

#region Configurations
var connectionString = builder.Configuration.GetConnectionString("MainConnection");

builder.Services.Configure<ConnectionStringsOptions>(
    builder.Configuration.GetSection("ConnectionStrings"));

#endregion

builder.Services.AddDbContext<KitchenContext>(options =>
    options.UseSqlServer(connectionString));


#region OTELStep1
builder.Services.AddOpenTelemetry()
      .ConfigureResource(resource => resource.AddService(serviceName: "KitchenApp", serviceVersion: Assembly.GetCallingAssembly().GetName().Version?.ToString()))
      .WithTracing(tracing => tracing
         .AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddSource(ApplicationDiagnostics.DataAccessSourceName)

         .AddEntityFrameworkCoreInstrumentation(opt =>
         {
            opt.SetDbStatementForText = true;
            opt.SetDbStatementForText = true;
            opt.EnrichWithIDbCommand = (activity, command) =>
            {
               activity.SetParentId(activity.ParentId);
            };

         })
         .AddConsoleExporter()
         .AddOtlpExporter()

         )
      .WithMetrics(metrics => metrics
            .AddMeter(ApplicationDiagnostics.MeterName)
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddOtlpExporter()
            .AddConsoleExporter()
            .AddOtlpExporter((otlpOptions =>
            {
               otlpOptions.Endpoint = new Uri("http://localhost:5080/api/default/v1/metrics");
               otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;

               // Autenticazione Basic per OpenObserve
               otlpOptions.Headers = "Authorization=Basic " +
                   Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin@example.com:Complexpass#123"));
            }))
            );
#endregion

#region sqlclient_instr
//.AddSqlClientInstrumentation(options =>
//           {
//              options.SetDbStatementForText = true;
//              options.RecordException = true;

//           })
//         .AddEntityFrameworkCoreInstrumentation(opt =>
//         {
//            opt.SetDbStatementForText = true;
//            opt.SetDbStatementForText = true;
//            opt.EnrichWithIDbCommand = (activity, command) =>
//            {
//               activity.SetParentId(activity.ParentId);
//            };

//         })
#endregion
#region EXTRA - OTELTraces with filter
//builder.Services.AddOpenTelemetry().ConfigureResource(resource => resource
//            .AddService(serviceName: "KitchenApp", serviceVersion: Assembly.GetCallingAssembly().GetName().Version?.ToString()))
//      .WithTracing(tracing => tracing
//         .AddHttpClientInstrumentation()
//         .AddAspNetCoreInstrumentation()
//         //.AddAspNetCoreInstrumentation(options =>
//         //{
//         //   options.Filter = context => !context.Request.Path.ToString().Contains("/health", StringComparison.OrdinalIgnoreCase); //esclude dal tracing le chiamate che contengono /health e fa passare tutto il resto (vedi il !)
//         //})

//         .AddEntityFrameworkCoreInstrumentation(opt =>
//         {
//            opt.SetDbStatementForText = true;
//            opt.SetDbStatementForText = true;
//            opt.EnrichWithIDbCommand = (activity, command) =>
//            {
//               activity.SetParentId(activity.ParentId);
//            };

//         })
//           .AddSqlClientInstrumentation(options =>
//           {
//              options.SetDbStatementForText = true;
//              options.RecordException = true;
//              options.EnableConnectionLevelAttributes = true;

//           })
//         .AddConsoleExporter()
//         .AddOtlpExporter()
//         .AddOtlpExporter(cfg => cfg.Endpoint = new Uri("http://localhost:4318"))
//         );
#endregion

#region OTELTracesWithCustomSpan

//         .AddSource(ApplicationDiagnostics.DataAccessSourceName)
//builder.Services.AddOpenTelemetry().ConfigureResource(resource => resource
//            .AddService(serviceName: "KitchenApp", serviceVersion: Assembly.GetCallingAssembly().GetName().Version?.ToString()))
//      .WithTracing(tracing => tracing
//         .AddSource(ApplicationDiagnostics.DataAccessSourceName)
//         .AddAspNetCoreInstrumentation()
//           .AddSqlClientInstrumentation(options =>
//           {
//              options.SetDbStatementForText = true;
//              options.RecordException = true;
//           })
//           .AddEntityFrameworkCoreInstrumentation(opt =>
//           {
//              opt.SetDbStatementForText = true;
//           })
//         .AddConsoleExporter()
//         .AddOtlpExporter()

//         )
//      .WithMetrics(metrics => metrics
//            .AddMeter(ApplicationDiagnostics.MeterName)
//            .AddOtlpExporter()
//            .AddConsoleExporter()
//            );
#endregion

#region customExporter
//.AddOtlpExporter(cfg => cfg.Endpoint = new Uri("http://localhost:4318"))
//.AddOtlpExporter(cf => {
//   cf.Endpoint = new Uri("http://localhost:5341/ingest/otlp/v1/traces");
//   cf.Protocol = OtlpExportProtocol.HttpProtobuf;
//})
//.AddOtlpExporter((otlpOptions =>
// {
//    otlpOptions.Endpoint = new Uri("http://localhost:5080/api/default/v1/traces");
//    otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;

//    // Autenticazione Basic per OpenObserve
//    otlpOptions.Headers = "Authorization=Basic " +
//        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin@example.com:Complexpass#123"));
// }))

#endregion

#region OTELLogging
builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(otel =>
{
   otel.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: "KitchenApp", serviceVersion: Assembly.GetCallingAssembly().GetName().Version?.ToString()));
   otel.IncludeScopes = true;
   otel.IncludeFormattedMessage = true;
   otel.AddConsoleExporter();

   otel.AddOtlpExporter();
}
);
#endregion

#region customLogging
//otel.AddOtlpExporter((otlpOptions =>
//{
//   otlpOptions.Endpoint = new Uri("http://localhost:5080/api/default/v1/logs");
//   otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;

//   // Autenticazione Basic per OpenObserve
//   otlpOptions.Headers = "Authorization=Basic " +
//       Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin@example.com:Complexpass#123"));
//}));
#endregion
#region azuremonitor
//builder.Services.AddOpenTelemetry()
//    .UseAzureMonitor(options => {
//       options.ConnectionString = "my-connection-string";
//    });
#endregion

builder.Services.AddScoped<IDishService, DishService>();
builder.Services.AddScoped<IDishRepository, DishRepository>();
builder.Services.AddScoped<IDrinkService, DrinkService>();
builder.Services.AddScoped<IDrinkRepository, DrinkRepository>();


var app = builder.Build();

SeedData(app);


if (app.Environment.IsDevelopment())
{
   app.MapOpenApi();
   app.UseSwaggerUI(options =>
   {
      options.SwaggerEndpoint("/openapi/v1.json", "Dummy API");
   });
}


app.UseAuthorization();
app.MapControllers();
app.Run();

static void SeedData(WebApplication app)
{
   using (var scope = app.Services.CreateScope())
   {
      var db = scope.ServiceProvider.GetRequiredService<KitchenContext>();
      db.Database.Migrate();

      if (!db.Dishes.Any())
      {
         db.Dishes.AddRange(
             new Dish { Name = "Piadina vuota" },
             new Dish { Name = "Piadina crudo e squaquerone" },
             new Dish { Name = "Tagliatelle al ragù" },
             new Dish { Name = "Cappelletti al ragù" },
             new Dish { Name = "Cappelletti in brodo" }
         );
         db.Drinks.AddRange(
             new Drink { Name = "Birra artigianale IPA" },
             new Drink { Name = "Birra lager" }
         );
         db.SaveChanges();
      }
   }
}