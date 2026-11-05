using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace RestaurantDemo;

public static class Telemetry
{
    public const string ServiceName = "Zagreb.Restaurant";
    public static readonly ActivitySource Source = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);
    public static readonly Counter<long> Orders = Meter.CreateCounter<long>(
        "restaurant.orders", unit: "{order}", description: "Order confirmations by outcome");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "restaurant.order.duration", unit: "s", description: "Time waiting for order confirmation");

    public static void AddDemoTelemetry(this WebApplicationBuilder builder, string serviceName = ServiceName)
    {
        #region config otlp
        var endpoint = new Uri(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? "http://localhost:4317");
        void ExportToDashboard(OtlpExporterOptions options)
        {
            options.Endpoint = endpoint;
            options.Protocol = OtlpExportProtocol.Grpc;
        }
        #endregion








        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: "1.0.0"))


        #region fullObservability
        .WithTracing(tracing => tracing
            .SetSampler(new DemoSampler()) // Capture every request, excluding render acknowledgements.
            .AddAspNetCoreInstrumentation(options => options.RecordException = true)
            .AddHttpClientInstrumentation(options => options.RecordException = true)
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource(Source.Name) // Subscribe to our business spans.
            .AddProcessor(new ErrorEventProcessor())
            .AddOtlpExporter(ExportToDashboard))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddMeter(Meter.Name)
            .AddView("restaurant.order.duration", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = [0.05, 0.1, 0.25, 0.5, 1, 5, 10, 15]
            })
            .AddOtlpExporter((options, reader) =>
            {
                ExportToDashboard(options);
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
            }));
        #endregion

        #region loggingObservability
        // Keep demo logging portable: console + OTLP, with no Windows Event Log dependency.
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(serviceName, serviceVersion: "1.0.0"));
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter(ExportToDashboard);
        });
        #endregion
    }

    private sealed class DemoSampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            // SignalR sets DisplayName after sampling; match its initial RPC tags instead.
            string? service = null;
            string? method = null;
            if (samplingParameters.Tags is { } tags)
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "rpc.service") service = tag.Value as string;
                    if (tag.Key == "rpc.method") method = tag.Value as string;
                }
            }

            return new(service == "Microsoft.AspNetCore.Components.Server.ComponentHub"
                && method == "OnRenderCompleted"
                    ? SamplingDecision.Drop
                    : SamplingDecision.RecordAndSample);
        }
    }

    // Some instrumentations set Error without an event (for example, a failed SQL
    // command or an HTTP 5xx response). Add the event before the exporter sees the span.
    private sealed class ErrorEventProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            if (activity.Status != ActivityStatusCode.Error || activity.Events.Any(e => e.Name == "error"))
                return;

            var statusCode = activity.GetTagItem("http.response.status_code");
            var message = !string.IsNullOrWhiteSpace(activity.StatusDescription)
                ? activity.StatusDescription
                : statusCode is not null ? $"HTTP request failed with status {statusCode}." : "Operation failed.";
            var tags = new ActivityTagsCollection { { "error.message", message } };
            if (activity.GetTagItem("error.type") is { } errorType)
                tags.Add("error.type", errorType);
            if (statusCode is not null)
                tags.Add("http.response.status_code", statusCode);

            activity.AddEvent(new ActivityEvent("error", activity.StartTimeUtc + activity.Duration, tags));
        }
    }
}
