using Menu.Utilities;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Runtime.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

#region 0-SimpleTrace
//builder.Services.AddOpenTelemetry()
//   .WithTracing(trace => trace
//      .AddAspNetCoreInstrumentation()
//      .AddHttpClientInstrumentation()
//      .AddConsoleExporter()
//      .AddOtlpExporter()
//   )
//   ;

#endregion

#region 1-TraceWithResource
//builder.Services.AddOpenTelemetry()
//   .ConfigureResource(resource => resource
//      .AddService(serviceName: "MenuApp", serviceVersion: "1.0.0.0"))
//   .WithTracing(metrics => metrics
//         .AddAspNetCoreInstrumentation()
//         .AddHttpClientInstrumentation()
//         .AddConsoleExporter()
//         .AddOtlpExporter()
//      );
#endregion


#region 2-SimpleTraceWithMultipleExporters

//builder.Services.AddOpenTelemetry()
//   .WithTracing(trace =>
//      trace
//      .ConfigureResource(resource => resource
//         .AddService(serviceName: "MenuApp", serviceVersion: "1.2.3"))
//      .AddAspNetCoreInstrumentation()
//      .AddHttpClientInstrumentation()
//      .AddConsoleExporter()
//      .AddOtlpExporter()
//      .AddOtlpExporter(cfg => cfg.Endpoint = new Uri("http://localhost:4318"))
//      .AddOtlpExporter(cf =>
//      {
//         cf.Endpoint = new Uri("http://localhost:5341/ingest/otlp/v1/traces");
//         cf.Protocol = OtlpExportProtocol.HttpProtobuf;
//      })
//   );

#endregion




#region 3-MetricsWithResource
builder.Services.AddOpenTelemetry()
      .ConfigureResource(resource => resource
      .AddService(serviceName: "MenuApp", serviceVersion: "1.0.0"))
      .WithMetrics(metrics => metrics
         .AddAspNetCoreInstrumentation()
         .AddMeter("Microsoft.AspNetCore.Hosting")
         .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
         //.AddMeter(ApplicationDiagnostics.MeterName)
         .AddConsoleExporter()
         .AddOtlpExporter()
      )
      ;

#endregion

#region Extra-BatchExportAndFiltering
//builder.Services.AddOpenTelemetry().ConfigureResource(resource => resource
//   .AddService(serviceName: "MenuApp", serviceVersion: "1.0.0.0"))
//      .WithMetrics(metrics => metrics
//         .AddAspNetCoreInstrumentation()
//         .AddMeter("Microsoft.AspNetCore.Hosting")
//         .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
//         .AddConsoleExporter()
//         .AddOtlpExporter(o =>
//         {

//            o.ExportProcessorType = ExportProcessorType.Batch;
//            o.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity> //AVAILABLE ONLY IN with ExportType Batch
//            {
//               MaxQueueSize = 2048,
//               ScheduledDelayMilliseconds = 500,
//               ExporterTimeoutMilliseconds = 30000,
//               MaxExportBatchSize = 512
//            };
//         })
//         .AddOtlpExporter((exporterOptions, metricReaderOptions) =>
//         {
//            metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10;
//         })
//         );
#endregion

#region 5-ILoggingBuilderOTELProvider
builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(otel =>
{
   otel.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: "MenuApp", serviceVersion: "1.0.0.0"));
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
   app.UseSwagger();
   app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
