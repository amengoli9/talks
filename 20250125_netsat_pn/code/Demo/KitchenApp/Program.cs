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


#region OTELTraces
//builder.Services.AddOpenTelemetry().ConfigureResource(resource => resource
//            .AddService(serviceName: "KitchenApp", serviceVersion: Assembly.GetCallingAssembly().GetName().Version?.ToString()))
//      .WithTracing(tracing => tracing
//         .AddHttpClientInstrumentation()
//         .AddAspNetCoreInstrumentation()
//        //.AddAspNetCoreInstrumentation(options =>
//        //{
//        //   options.Filter = context => !context.Request.Path.ToString().Contains("/health", StringComparison.OrdinalIgnoreCase); //esclude dal tracing le chiamate che contengono /health e fa passare tutto il resto (vedi il !)
//        //})

//         .AddEntityFrameworkCoreInstrumentation(opt => { opt.SetDbStatementForText = true;
//            opt.SetDbStatementForText = true;
//            opt.EnrichWithIDbCommand = (activity, command) => {
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
builder.Services.AddOpenTelemetry().ConfigureResource(resource => resource
            .AddService(serviceName: "KitchenApp", serviceVersion: Assembly.GetCallingAssembly().GetName().Version?.ToString()))
      .WithTracing(tracing => tracing
         //.AddHttpClientInstrumentation()
         .AddSource(ApplicationDiagnostics.DataAccessSourceName)
         .AddAspNetCoreInstrumentation()
           //.AddEntityFrameworkCoreInstrumentation(opt => {
           //   opt.SetDbStatementForText = true;
           //   //opt.SetDbStatementForText = true;
           //   //opt.EnrichWithIDbCommand = (activity, command) => {
           //   //   activity.SetParentId(activity.ParentId);
           //   //};

           //})
           .AddSqlClientInstrumentation(options =>
           {
              options.SetDbStatementForText = true;
              options.RecordException = true;

           })
           .AddEntityFrameworkCoreInstrumentation(opt =>
           {
              opt.SetDbStatementForText = true;
           })
         .AddConsoleExporter()
         .AddOtlpExporter()
         .AddOtlpExporter(cfg => cfg.Endpoint = new Uri("http://localhost:4318"))
         );
#endregion 



#region OTELLogging
builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(otel => {
   otel.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: "KitchenApp", serviceVersion: Assembly.GetCallingAssembly().GetName().Version?.ToString()));
   otel.IncludeScopes = true;
   otel.IncludeFormattedMessage = true;
   otel.AddConsoleExporter();
   otel.AddOtlpExporter(); 
   otel.AddOtlpExporter(exporter => {
      exporter.Endpoint = new Uri("http://localhost:5341/ingest/otlp/v1/logs");
      exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
   });
}
);
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