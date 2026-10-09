using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Agents.AI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Torino.Live;

internal sealed class Telemetry : IDisposable
{
    public const string AgentSource = "Torino.Live.Agent";
    public const string ModelSource = "Torino.Live.Model";
    public static readonly ActivitySource Source = new("Torino.Live");
    private static readonly Meter Meter = new("Torino.Live");
    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>("demo.decisions");
    private readonly TracerProvider traces;
    private readonly MeterProvider metrics;

    public Telemetry(string service)
    {
        var resource = ResourceBuilder.CreateDefault().AddService(service);
        var tracing = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .SetSampler(new AlwaysOnSampler())
            .AddSource("Torino.Live", AgentSource, ModelSource, OpenTelemetryAgent.DefaultSourceName, "Experimental.Microsoft.Extensions.AI");
        var metering = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resource)
            .AddMeter("Torino.Live", AgentSource, ModelSource, "Experimental.Microsoft.Extensions.AI");


        tracing.AddOtlpExporter();
        metering.AddOtlpExporter();
        traces = tracing.Build();
        metrics = metering.Build();
    }

    public static void Decision(string check, string outcome)
    {
        using var span = Source.StartActivity("guardrail." + check);
        span?.SetTag("guardrail.outcome", outcome);
        Decisions.Add(1, new("check", check), new("outcome", outcome));
        DemoConsole.Decision(check, outcome);
    }

    public void Dispose()
    {
        metrics.Dispose();
        traces.Dispose(); // Scarica i batch OTLP anche per queste console brevi.
    }

    private static bool IsConfigured(string signal) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable($"OTEL_EXPORTER_OTLP_{signal}_ENDPOINT"));
}
