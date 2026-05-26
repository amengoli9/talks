// D1 - Sequential orchestrator shown in DevUI.
//
// Three trivial agents (brainstormer -> writer -> editor) are composed into a
// sequential workflow with AgentWorkflowBuilder.BuildSequential and exposed,
// together with the individual agents, through the DevUI web interface.
//
// Chat client and every agent are instrumented with OpenTelemetry (-> OTLP).

using System.Diagnostics;
using Maf.Demo.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

const string ServiceName = "D1.SequentialDevUI";

var builder = WebApplication.CreateBuilder(args);

DemoConfig cfg = DemoConfig.Load();

// Shared Azure OpenAI chat client (always instrumented), consumed by the hosted agents.
builder.Services.AddChatClient(ChatClientFactory.Create(ServiceName, cfg));

// OpenTelemetry -> OTLP collector. Captures the Agent Framework and ASP.NET activity.
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("Microsoft.Agents.AI*")
        .AddSource("Microsoft.Extensions.AI*")
        .AddSource(ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(cfg.OtlpEndpoint)))
    .WithMetrics(m => m
        .AddMeter("Microsoft.Agents.AI*")
        .AddMeter("Microsoft.Extensions.AI*")
        .AddOtlpExporter(o => o.Endpoint = new Uri(cfg.OtlpEndpoint)));

// Builds one instrumented agent on top of the shared chat client.
static AIAgent BuildAgent(IServiceProvider sp, string name, string instructions) =>
    new ChatClientAgent(sp.GetRequiredService<IChatClient>(), instructions, name)
        .WithOpenTelemetry(ServiceName);


// Three simple agents forming a writing pipeline.
IHostedAgentBuilder brainstormer = builder.AddAIAgent("brainstormer", (sp, name) => BuildAgent(sp, name,
    "Sei un generatore di idee. Data una richiesta, produci 3 spunti brevi e concreti. Non scrivere il testo finale, solo gli spunti."));

IHostedAgentBuilder writer = builder.AddAIAgent("writer", (sp, name) => BuildAgent(sp, name,
    "Sei un copywriter. Prendi gli spunti che ricevi e scrivi una prima bozza di testo, chiara e scorrevole."));

IHostedAgentBuilder editor = builder.AddAIAgent("editor", (sp, name) => BuildAgent(sp, name,
    "Sei un editor. Rifinisci la bozza che ricevi: correggi, accorcia dove serve e restituisci la versione finale pronta da pubblicare."));

// Sequential workflow: brainstormer -> writer -> editor, exposed as a single agent in DevUI.
builder.AddWorkflow("writing-pipeline", (sp, key) =>
{
    IEnumerable<AIAgent> agents = new[] { brainstormer, writer, editor }
        .Select(b => sp.GetRequiredKeyedService<AIAgent>(b.Name));
    return AgentWorkflowBuilder.BuildSequential(workflowName: key, agents: agents);
}).AddAsAIAgent();

// Required by DevUI.
builder.AddDevUI();
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

var app = builder.Build();

// OpenTelemetry providers are wired into DI here; start the INIT activity AFTER
// build so it's captured by the tracer provider.
using var activitySource = new ActivitySource(ServiceName);
using var initActivity = activitySource.StartActivity("INIT", ActivityKind.Client);

app.MapOpenAIResponses();
app.MapOpenAIConversations();

if (builder.Environment.IsDevelopment())
{
    app.MapDevUI();
}

Console.WriteLine("DevUI:  https://localhost:50516/devui");
Console.WriteLine("Premi Ctrl+C per fermare.");

app.Run();
