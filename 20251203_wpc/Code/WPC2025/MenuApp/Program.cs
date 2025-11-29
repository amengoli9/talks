using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpClient();



builder.Services.AddOpenTelemetry()
      .ConfigureResource(res => res.AddService(serviceName: "MenuApp", serviceVersion: "1.0.0"))
      .WithTracing(cfg =>
         cfg
         .AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddConsoleExporter()
         .AddOtlpExporter()
      //.AddOtlpExporter((otlpOptions =>
      //{
      //   otlpOptions.Endpoint = new Uri("http://localhost:5080/api/default/v1/traces");
      //   otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
      //   otlpOptions.Headers = "Authorization=Basic " +
      //       Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin@example.com:Complexpass#123"));
      //}))

      )
   ;


#region 3-MetricsWithResource
builder.Services.AddOpenTelemetry()
      .ConfigureResource(resource => resource
      .AddService(serviceName: "MenuApp", serviceVersion: "1.0.0"))
      .WithMetrics(metrics => metrics
         .AddAspNetCoreInstrumentation()
         .AddMeter("Microsoft.AspNetCore.Hosting")
         .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
         //.AddMeter(ApplicationDiagnostics.MeterName)
         .AddOtlpExporter()
      )
      ;

#endregion


#region 5-ILoggingBuilderOTELProvider
builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(otel =>
{
    otel.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: "MenuApp", serviceVersion: "1.0.0"));
    otel.IncludeScopes = true;
    otel.IncludeFormattedMessage = true;
    otel.AddConsoleExporter();
    otel.AddOtlpExporter();
    otel.AddOtlpExporter((otlpOptions =>
    {
        otlpOptions.Endpoint = new Uri("http://localhost:5080/api/default/v1/logs");
        otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;

        // Autenticazione Basic per OpenObserve
        otlpOptions.Headers = "Authorization=Basic " +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin@example.com:Complexpass#123"));
    }));
}
);
#endregion
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Menu API");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();
