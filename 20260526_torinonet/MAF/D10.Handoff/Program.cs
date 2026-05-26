// D10 - Handoff orchestration, esposta in DevUI.
//
// A triage agent decides which SPECIALIST should answer each user message and hands
// off the conversation to it. Each specialist hands back to the triage so the next
// turn can be routed somewhere else.
//
// Built with AgentWorkflowBuilder.CreateHandoffBuilderWith / WithHandoffs and
// registered via the hosting packages so it appears in DevUI together with the
// individual agents.
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

const string ServiceName = "D10.Handoff";

var builder = WebApplication.CreateBuilder(args);

DemoConfig cfg = DemoConfig.Load();

// Shared instrumented chat client used by every hosted agent.
builder.Services.AddChatClient(ChatClientFactory.Create(ServiceName, cfg));

// OpenTelemetry -> OTLP collector.
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
static AIAgent BuildAgent(IServiceProvider sp, string name, string instructions, string description) =>
    new ChatClientAgent(sp.GetRequiredService<IChatClient>(), instructions, name, description)
        .WithOpenTelemetry(ServiceName);

// --- The four agents ---
IHostedAgentBuilder triage = builder.AddAIAgent("triage", (sp, name) => BuildAgent(sp, name,
    """
        Sei un assistente di smistamento per il supporto clienti.
        Devi SEMPRE fare handoff a un altro agente (non rispondere mai direttamente).
        Scegli in base all'argomento della richiesta dell'utente:
        - problemi tecnici (errori, configurazione, bug, prodotto) -> tech
        - fatturazione, pagamenti, abbonamenti -> billing
        - informazioni generali (orari, sedi, contatti) -> general
        """,
    "Smistatore: instrada la richiesta al giusto specialista"));

IHostedAgentBuilder tech = builder.AddAIAgent("tech", (sp, name) => BuildAgent(sp, name,
    "Sei lo specialista TECNICO. Aiuta l'utente con errori, configurazioni e problemi di prodotto. Rispondi in modo conciso.",
    "Specialista in supporto tecnico"));

IHostedAgentBuilder billing = builder.AddAIAgent("billing", (sp, name) => BuildAgent(sp, name,
    "Sei lo specialista della FATTURAZIONE. Aiuta con pagamenti, fatture, abbonamenti. Rispondi in modo conciso.",
    "Specialista in fatturazione e pagamenti"));

IHostedAgentBuilder general = builder.AddAIAgent("general", (sp, name) => BuildAgent(sp, name,
    "Sei lo specialista per INFORMAZIONI GENERALI. Rispondi a orari, sedi e contatti. Rispondi in modo conciso.",
    "Specialista per informazioni generali"));

// --- The handoff workflow, exposed as an agent in DevUI ---
// Note: HandoffWorkflowBuilder doesn't expose WithName() and Workflow.Name is
// internal-init, so AddWorkflow("handoff", ...).AddAsAIAgent() fails with a
// name-mismatch error. We register the AIAgent directly: we build the workflow
// inside the agent factory and convert it with workflow.AsAIAgent(name: ...).
builder.AddAIAgent("handoff", (sp, name) =>
{
    AIAgent triageAgent = sp.GetRequiredKeyedService<AIAgent>(triage.Name);
    AIAgent[] specialists =
    [
        sp.GetRequiredKeyedService<AIAgent>(tech.Name),
        sp.GetRequiredKeyedService<AIAgent>(billing.Name),
        sp.GetRequiredKeyedService<AIAgent>(general.Name),
    ];

    Workflow handoffWorkflow = AgentWorkflowBuilder
        .CreateHandoffBuilderWith(triageAgent)
        .WithHandoffs(triageAgent, specialists)
        .WithHandoffs(specialists, triageAgent)
        .Build();

    return handoffWorkflow.AsAIAgent(
        name: name,
        description: "Handoff orchestration: il triage instrada a tech, billing o general.");
});

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

Console.WriteLine("DevUI:  https://localhost:50517/devui");
Console.WriteLine("Premi Ctrl+C per fermare.");

app.Run();
