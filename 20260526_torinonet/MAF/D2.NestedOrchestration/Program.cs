// D2 - Custom orchestration with nested levels.
//
// Outer orchestration: a SEQUENTIAL pipeline  triage -> debate-room -> summarizer.
// The middle stage "debate-room" is itself an orchestration: a round-robin GROUP CHAT
// between two agents, wrapped as a single AIAgent with workflow.AsAIAgent().
//
// This shows how workflows compose: an orchestration can be a building block inside
// another orchestration.

using Maf.Demo.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Diagnostics;

const string ServiceName = "D2.NestedOrchestration";

using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);
IChatClient chat = ChatClientFactory.Create(ServiceName);


var activitySource = new ActivitySource(ServiceName);
using var activity = activitySource.StartActivity("INIT", ActivityKind.Client);

// --- Inner orchestration: a 2-agent round-robin group chat ("debate room") ---
AIAgent optimist = new ChatClientAgent(chat,
    "Sei l'avvocato del SI. Difendi la proposta con argomenti concreti. Massimo 3 frasi per turno.",
    "optimist", "Sostiene la proposta").WithOpenTelemetry(ServiceName);
AIAgent skeptic = new ChatClientAgent(chat,
    "Sei l'avvocato del NO. Critica la proposta con argomenti concreti. Massimo 3 frasi per turno.",
    "skeptic", "Critica la proposta").WithOpenTelemetry(ServiceName);

Workflow debateWorkflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(agents => new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 4 })
    .AddParticipants(optimist, skeptic)
    .WithName("debate-room")
    .WithDescription("Due agenti dibattono pro e contro una proposta, a turni.")
    .Build();

// The inner workflow becomes a single agent, usable as one stage of the outer pipeline.
AIAgent debateRoom = debateWorkflow.AsAIAgent(
    name: "debate-room",
    description: "Fa dibattere due agenti su una proposta e restituisce la discussione.")
    .WithOpenTelemetry(ServiceName);

// --- Outer orchestration: a sequential pipeline whose middle stage is the nested workflow ---
AIAgent triage = new ChatClientAgent(chat,
    "Sei un agente di smistamento. Riformula la richiesta dell'utente come una proposta chiara e neutra da mettere in discussione. Rispondi con una sola frase.",
    "triage", "Riformula la richiesta come proposta").WithOpenTelemetry(ServiceName);
AIAgent summarizer = new ChatClientAgent(chat,
    "Hai ricevuto un dibattito tra due posizioni opposte. Riassumi i punti chiave di entrambe e chiudi con una raccomandazione finale in massimo 3 righe.",
    "summarizer", "Sintetizza il dibattito e raccomanda").WithOpenTelemetry(ServiceName);

Workflow outerWorkflow = AgentWorkflowBuilder.BuildSequential(triage, debateRoom, summarizer);

// --- Run ---
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("Suggerimento: Dovremmo riscrivere il nostro monolite in microservizi?");
Console.ResetColor();
Console.Write("Domanda> ");
string? input = Console.ReadLine();
string question = string.IsNullOrWhiteSpace(input)
    ? "Dovremmo riscrivere il nostro monolite in microservizi?"
    : input;

Console.WriteLine($"Domanda: {question}");
Console.WriteLine(new string('=', 64));

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    outerWorkflow, new List<ChatMessage> { new(ChatRole.User, question) });
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

string? lastExecutor = null;
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case AgentResponseUpdateEvent update:
            if (update.ExecutorId != lastExecutor)
            {
                lastExecutor = update.ExecutorId;
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($">> stage: {update.ExecutorId}");
                Console.ResetColor();
            }
            Console.Write(update.Update.Text);
            break;

        case WorkflowOutputEvent:
            Console.WriteLine();
            Console.WriteLine(new string('=', 64));
            Console.WriteLine("Workflow completato.");
            return;

        case WorkflowErrorEvent error:
            Console.Error.WriteLine(error.Exception);
            return;

        case ExecutorFailedEvent failed:
            Console.Error.WriteLine($"Executor '{failed.ExecutorId}' fallito: {failed.Data}");
            return;
    }
}
