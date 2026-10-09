using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Torino.Live;

using var telemetry = new Telemetry("D5.Evaluation");
DemoConsole.Title("D5 — Il tuo prompt → agente → controlli → report → gate");
string? prompt = Demo.ReadPrompt("Quando e dove si svolge AI Day Torino?",
    "Prepara un promemoria con luogo e data dell'evento.",
    "Un collega dice Milano nel 2025: verifica luogo e data di AI Day Torino.");
if (prompt is null) return;
string? judgeModel = args.Contains("--judge") ? Demo.GetModel("JudgeModel") : null;
using var client = Demo.CreateClient();
bool regression = args.Contains("--regression");
var agent = new ChatClientAgent(client, name: "EventAssistant",
    instructions: "Rispondi in italiano. Consulta sempre GetEventInfo. " +
        "Riporta sempre città e anno forniti dal tool, senza usare conoscenze esterne.",
    tools: [AIFunctionFactory.Create(GetEventInfo, nameof(GetEventInfo))])
    .AsBuilder()
    .UseOpenTelemetry(Telemetry.AgentSource, o => o.EnableSensitiveData = true)
    .Build();

using var batch = Telemetry.Source.StartActivity("evaluation.batch");
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));

// LocalEvaluator non chiama un giudice; l'agente usa comunque il modello reale.
var checks = new LocalEvaluator(
    EvalChecks.KeywordCheck("Torino", "2026"),
    EvalChecks.ToolCalledCheck("GetEventInfo"),
    FunctionEvaluator.Create("correct_tool_facts", (EvalItem item) =>
        item.Conversation.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Any(result => result.Result?.ToString()?.Contains("Torino, 10 ottobre 2026") == true)));

var results = await agent.EvaluateAsync([prompt], checks,
    numRepetitions: args.Contains("--repeat") ? 3 : 1,
    cancellationToken: timeout.Token);
string? model = client.GetService<ChatClientMetadata>()?.DefaultModelId;
EvaluationReport.Print(results, model, regression);
bool passed = results.AllPassed;
int passedItems = results.Passed, totalItems = results.Total;

// Valuta risposte GIA' prodotte: il modello dell'agente non viene richiamato.
// Stessa API utilizzabile in un worker su conversazioni campionate dalla produzione.
if (judgeModel is not null)
{
    var judged = await QualityJudge.EvaluateAsync(agent, results, judgeModel, timeout.Token);
    EvaluationReport.Print(judged, model, regression, judgeModel);
    passed &= judged.AllPassed;
    passedItems += judged.Passed;
    totalItems += judged.Total;
}

batch?.SetTag("evaluation.passed", passedItems);
batch?.SetTag("evaluation.total", totalItems);
batch?.SetTag("evaluation.gate", passed ? "pass" : "fail");
batch?.SetStatus(passed ? System.Diagnostics.ActivityStatusCode.Ok : System.Diagnostics.ActivityStatusCode.Error);
DemoConsole.Section("ESITO FINALE");
DemoConsole.Line(passed ? "GATE: PASS" : "GATE: FAIL (exit code 1)", passed ? ConsoleColor.Green : ConsoleColor.Red);
Environment.ExitCode = passed ? 0 : 1;

[Description("Restituisce data e città dell'evento dimostrativo.")]
string GetEventInfo()
{
    using var span = Telemetry.Source.StartActivity("tool.GetEventInfo");
    return regression ? "AI Day: Milano, 10 ottobre 2025." : "AI Day Torino: Torino, 10 ottobre 2026.";
}
