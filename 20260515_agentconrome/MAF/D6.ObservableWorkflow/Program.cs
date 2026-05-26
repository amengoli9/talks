// D6 - A fully observable workflow.
//
// A simple 4-agent sequential pipeline (researcher -> analyst -> writer -> reviewer)
// where OpenTelemetry is enabled at EVERY level:
//   - chat client   (ChatClientFactory.Create -> UseOpenTelemetry)
//   - each agent     (.WithOpenTelemetry)
//   - the workflow   (captured via the "Microsoft.Agents.AI*" activity source)
//
// The whole run is wrapped in a root span. In the OTLP backend you can see the full
// flow: what each agent passes to the next, which tools are called, and every turn
// change. The console mirrors the same story so the demo is readable live.

using System.ComponentModel;
using System.Diagnostics;
using Maf.Demo.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

const string ServiceName = "D6.ObservableWorkflow";

using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);
using var activitySource = new ActivitySource(ServiceName);

IChatClient chat = ChatClientFactory.Create(ServiceName);

// A fake "search" tool so tool calls are visible in the traces.
[Description("Cerca fatti su un argomento e restituisce un breve elenco.")]
static string SearchFacts([Description("L'argomento da cercare")] string topic) =>
    $"Fatti su '{topic}': (1) e' un tema molto discusso nel 2026; " +
    $"(2) ha visto una forte crescita di adozione tra le aziende; " +
    $"(3) restano sfide aperte su costi, sicurezza e governance.";

// Four trivial agents, each individually instrumented.
AIAgent researcher = new ChatClientAgent(chat,
    "Sei un ricercatore. Usa lo strumento search_facts per raccogliere fatti sull'argomento, poi elenca i 3 fatti piu' rilevanti.",
    "researcher", "Raccoglie fatti",
    tools: [AIFunctionFactory.Create(SearchFacts, name: "search_facts")])
    .WithOpenTelemetry(ServiceName);

AIAgent analyst = new ChatClientAgent(chat,
    "Sei un analista. Dai fatti che ricevi, estrai 2 implicazioni o pattern interessanti. Sii sintetico.",
    "analyst", "Analizza i fatti")
    .WithOpenTelemetry(ServiceName);

AIAgent writer = new ChatClientAgent(chat,
    "Sei un divulgatore. Scrivi un paragrafo breve e chiaro basato sull'analisi che ricevi.",
    "writer", "Scrive il paragrafo")
    .WithOpenTelemetry(ServiceName);

AIAgent reviewer = new ChatClientAgent(chat,
    "Sei un revisore. Controlla il paragrafo che ricevi, correggilo se serve e restituisci la versione finale.",
    "reviewer", "Rivede e finalizza")
    .WithOpenTelemetry(ServiceName);

Workflow workflow = AgentWorkflowBuilder.BuildSequential(researcher, analyst, writer, reviewer);

// Root span: everything below hangs under this trace.
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("Suggerimento: gli agenti AI nello sviluppo software");
Console.ResetColor();
Console.Write("Argomento> ");
string? inputTopic = Console.ReadLine();
string topic = string.IsNullOrWhiteSpace(inputTopic) ? "gli agenti AI nello sviluppo software" : inputTopic;
using Activity? rootActivity = activitySource.StartActivity("INIT", ActivityKind.Client);
rootActivity?.SetTag("workflow.topic", topic);

Console.WriteLine($"Argomento: {topic}");
Console.WriteLine($"Trace ID : {Activity.Current?.TraceId}");
Console.WriteLine(new string('=', 64));

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow, new List<ChatMessage> { new(ChatRole.User, topic) });
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
                Console.WriteLine($">> turno: {update.ExecutorId}");
                Console.ResetColor();
            }

            // Show tool calls explicitly so the "behind the scenes" is visible.
            foreach (AIContent content in update.Update.Contents)
            {
                if (content is FunctionCallContent call)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"   [tool] chiamata: {call.Name}({string.Join(", ", call.Arguments?.Values ?? [])})");
                    Console.ResetColor();
                }
                else if (content is FunctionResultContent result)
                {
                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    Console.WriteLine($"   [tool] risultato: {result.Result}");
                    Console.ResetColor();
                }
            }

            Console.Write(update.Update.Text);
            break;

        case WorkflowOutputEvent:
            Console.WriteLine();
            Console.WriteLine(new string('=', 64));
            Console.WriteLine("Workflow completato. Apri il tuo backend OTLP per vedere il trace completo.");
            return;

        case WorkflowErrorEvent error:
            Console.Error.WriteLine(error.Exception);
            return;

        case ExecutorFailedEvent failed:
            Console.Error.WriteLine($"Executor '{failed.ExecutorId}' fallito: {failed.Data}");
            return;
    }
}
