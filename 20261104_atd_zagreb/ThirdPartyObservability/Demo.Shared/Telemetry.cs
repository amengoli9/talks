using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ThirdPartyDemo;

public static class Telemetry
{
    public const string Name = "Zagreb.ThirdParty";
    public static readonly ActivitySource Source = new(Name);
    public static readonly Meter Meter = new(Name);
    public static readonly Counter<long> Orders = Meter.CreateCounter<long>("restaurant.orders", "{order}");
    public static readonly Counter<long> Cache = Meter.CreateCounter<long>("restaurant.cache.lookups", "{lookup}");
    public static readonly Histogram<double> Processing = Meter.CreateHistogram<double>("restaurant.processing.duration", "s");
    public static readonly Histogram<double> QueueAge = Meter.CreateHistogram<double>("restaurant.message.age", "s");
    public static readonly Counter<long> Retries = Meter.CreateCounter<long>("restaurant.http.retries", "{retry}");
    public static readonly Counter<long> Notifications = Meter.CreateCounter<long>("restaurant.notifications", "{notification}");
    public static readonly Counter<long> Outbox = Meter.CreateCounter<long>("restaurant.outbox.dispatched", "{message}");

    public static void AddDemoTelemetry(this IHostApplicationBuilder builder, string service)
    {
        void Export(OtlpExporterOptions options)
        {
            options.Endpoint = new Uri(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4327");
            options.Protocol = OtlpExportProtocol.Grpc;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(service, serviceVersion: "1.0.0"))
            .WithTracing(t => t.SetSampler(new AlwaysOnSampler())
                .AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
                .AddSource(Name, "Npgsql", "Experimental.System.Net.Http.Connections", "Experimental.System.Net.NameResolution")
                .AddOtlpExporter(Export))
            .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddMeter(Name, "Npgsql", "System.Net.Http", "System.Runtime")
                .AddView("restaurant.processing.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [0.01, 0.05, 0.1, 0.5, 1, 3, 5, 10]
                })
                .AddOtlpExporter((options, reader) =>
                {
                    Export(options);
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                }));

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();
        builder.Logging.AddOpenTelemetry(l =>
        {
            l.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(service));
            l.IncludeFormattedMessage = true;
            l.IncludeScopes = true;
            l.AddOtlpExporter(Export);
        });
    }

    public static void Fail(Activity? activity, Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.AddException(exception);
    }
}
