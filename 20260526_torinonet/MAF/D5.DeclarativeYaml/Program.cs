// D5 - Declarative agents and workflows from YAML.
//
// PART A: a declarative AGENT loaded from agent.yaml via ChatClientPromptAgentFactory.
//         Works with the shared Azure OpenAI chat client (API key).
//
// PART B: a declarative WORKFLOW loaded from workflow.yaml via DeclarativeWorkflowBuilder.
//         This path needs Azure AI Foundry: the workflow references agents by name,
//         which the program creates in the Foundry project at startup. Auth is
//         DefaultAzureCredential -> run 'az login' before launching.

using System.ComponentModel;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Maf.Demo.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Agents.AI.Workflows.Declarative.Events;
using Microsoft.Extensions.AI;
using System.Diagnostics;

const string ServiceName = "D5.DeclarativeYaml";

using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);

var activitySource = new ActivitySource(ServiceName);
using var rootActivity = activitySource.StartActivity("INIT", ActivityKind.Client);

DemoConfig cfg = DemoConfig.Load();

// ============================================================
// PART A - Declarative AGENT from YAML
// ============================================================
Console.WriteLine("=== PARTE A: agente dichiarativo da agent.yaml ===\n");

[Description("Restituisce il meteo per una citta'.")]
static string GetWeather([Description("La citta'")] string location)
    => $"A {location} e' sereno, 21 gradi.";

IChatClient chat = ChatClientFactory.Create(ServiceName, cfg);

string agentYaml = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "agent.yaml"));

// The factory binds the YAML 'get_weather' tool to the real C# method.
var agentFactory = new ChatClientPromptAgentFactory(
    chat,
    functions: [AIFunctionFactory.Create(GetWeather, name: "get_weather")]);

AIAgent yamlAgent = (await agentFactory.CreateFromYamlAsync(agentYaml))!;
yamlAgent = yamlAgent.WithOpenTelemetry(ServiceName);

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("Suggerimento: Che tempo fa a Bologna?");
Console.ResetColor();
Console.Write("Domanda> ");
string? inputA = Console.ReadLine();
string questionA = string.IsNullOrWhiteSpace(inputA) ? "Che tempo fa a Bologna?" : inputA;

AgentResponse agentResponse = await yamlAgent.RunAsync(questionA);
Console.WriteLine($"Concierge (da agent.yaml): {agentResponse.Text}\n");

// ============================================================
// PART B - Declarative WORKFLOW from YAML (Foundry)
// ============================================================
Console.WriteLine("=== PARTE B: workflow dichiarativo da workflow.yaml (Foundry) ===\n");

if (string.IsNullOrWhiteSpace(cfg.FoundryEndpoint))
{
    Console.WriteLine("Foundry:Endpoint non configurato in appsettings.json: salto la parte B.");
    return;
}

var foundryEndpoint = new Uri(cfg.FoundryEndpoint);
var credential = new DefaultAzureCredential();
var projectClient = new AIProjectClient(foundryEndpoint, credential);

try
{
    // The workflow YAML references PlannerAgent and WriterAgent by name:
    // they must exist in the Foundry project, so create them first.
    Console.WriteLine("Creo gli agenti nel progetto Foundry...");
    await CreateFoundryAgentAsync(projectClient, "PlannerAgent", cfg.FoundryModel!,
        "Scomponi la richiesta dell'utente in 3 punti chiave da trattare. Rispondi solo con l'elenco puntato.");
    await CreateFoundryAgentAsync(projectClient, "WriterAgent", cfg.FoundryModel!,
        "Hai ricevuto un elenco di punti chiave. Scrivi una risposta breve e chiara che li affronta tutti.");

    // Build the workflow from workflow.yaml using the Foundry agent provider.
    var agentProvider = new AzureAgentProvider(foundryEndpoint, credential);
    var options = new DeclarativeWorkflowOptions(agentProvider);
    string workflowPath = Path.Combine(AppContext.BaseDirectory, "workflow.yaml");
    Workflow workflow = DeclarativeWorkflowBuilder.Build<string>(workflowPath, options);

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("Suggerimento: Spiega perche' l'observability e' importante per gli agenti AI.");
    Console.ResetColor();
    Console.Write("Domanda> ");
    string? inputB = Console.ReadLine();
    string input = string.IsNullOrWhiteSpace(inputB)
        ? "Spiega perche' l'observability e' importante per gli agenti AI."
        : inputB;
    Console.WriteLine($"Input: {input}");
    Console.WriteLine(new string('-', 64));

    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input);
    string? lastMessageId = null;
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case DeclarativeActionInvokedEvent action:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n[azione YAML] {action.ActionId} ({action.ActionType})");
                Console.ResetColor();
                break;

            case AgentResponseUpdateEvent update:
                if (update.Update.MessageId != lastMessageId)
                {
                    lastMessageId = update.Update.MessageId;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"\n{update.Update.AuthorName ?? "agent"}: ");
                    Console.ResetColor();
                }
                Console.Write(update.Update.Text);
                break;

            case MessageActivityEvent activity:
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n{activity.Message}");
                Console.ResetColor();
                break;

            case WorkflowErrorEvent error:
                Console.Error.WriteLine(error.Exception);
                break;

            case ExecutorFailedEvent failed:
                Console.Error.WriteLine($"Executor '{failed.ExecutorId}' fallito: {failed.Data}");
                break;
        }
    }

    Console.WriteLine();
    Console.WriteLine(new string('-', 64));
    Console.WriteLine("Workflow dichiarativo completato.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Parte B non eseguita: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine("Verifica di aver fatto 'az login' e di avere accesso RBAC al progetto Foundry.");
}
finally
{
    // Cleanup: remove the demo agents from the Foundry project (best-effort).
    Console.WriteLine("\nPulizia: rimuovo gli agenti dal progetto Foundry...");
    await DeleteFoundryAgentAsync(projectClient, "PlannerAgent");
    await DeleteFoundryAgentAsync(projectClient, "WriterAgent");
}

static async Task CreateFoundryAgentAsync(AIProjectClient client, string name, string model, string instructions)
{
    await client.AgentAdministrationClient.CreateAgentVersionAsync(
        name,
        new ProjectsAgentVersionCreationOptions(
            new DeclarativeAgentDefinition(model: model) { Instructions = instructions }));
    Console.WriteLine($"  + {name}");
}

static async Task DeleteFoundryAgentAsync(AIProjectClient client, string name)
{
    try
    {
        await client.AgentAdministrationClient.DeleteAgentAsync(name);
        Console.WriteLine($"  - {name}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  (impossibile rimuovere {name}: {ex.Message})");
    }
}
