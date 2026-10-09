using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Torino.Live;

using var telemetry = new Telemetry("D2.Harness");
DemoConsole.Title("D2 — Stesso tool, ora con HarnessAgent e una sessione");
using var client = Demo.CreateClient();
bool minimal = args.Contains("--minimal");
var compaction = new ConsoleCompaction { Enabled = args.Contains("--compact") };
DemoConsole.Line($"Todo e modalità plan/execute: {!minimal}. Compaction: {compaction.Enabled}.");
DemoConsole.Line("/compact (o --compact) = attiva e compatta · /compact off = disattiva · /status = contesto");
DemoConsole.Line("Compaction della demo: conserva gli ultimi 2 turni. I precedenti vengono rimossi, non riassunti.");


var agent = new HarnessAgent(client, new HarnessAgentOptions
{
    Name = "EventHarness",
    ChatOptions = new()
    {
        Instructions = "Rispondi in italiano. Consulta GetEventInfo per i fatti. " +
            "Se hai i tool todo, crea e completa una breve lista di attività.",
        Tools = [AIFunctionFactory.Create(GetEventInfo, nameof(GetEventInfo))]
    },
    DisableTodoProvider = minimal,
    DisableAgentModeProvider = minimal,
    DisableFileMemory = true,
    DisableAgentSkillsProvider = true,
    DisableWebSearch = true,
    MaximumIterationsPerRequest = 12,
    // Strategia nativa a turni: dimostrabile con tre prompt brevi, senza riempire migliaia di token.
    CompactionStrategy = compaction,
    ChatHistoryProvider = new InMemoryChatHistoryProvider(new()
    {
        ChatReducer = compaction.AsChatReducer(),
        // Riduce anche la storia salvata alla fine di un turno senza tool.
        ReducerTriggerEvent = InMemoryChatHistoryProviderOptions.ChatReducerTriggerEvent.AfterMessageAdded
    }),
    MaxOutputTokens = 2048,
    DisableOpenTelemetry = true // Sostituiamo il wrapper predefinito con quello configurato sotto.
}).AsBuilder()
    .UseOpenTelemetry(Telemetry.AgentSource, o => o.EnableSensitiveData = true)
    .Build();


var session = await agent.CreateSessionAsync();
while (Demo.ReadPrompt("Prepara un promemoria per AI Day Torino.", "Ora riducilo a due righe.") is { } prompt)
{
    if (prompt.Equals("/status", StringComparison.OrdinalIgnoreCase))
    {
        compaction.ShowStatus(session);
        continue;
    }
    if (prompt.Equals("/compact off", StringComparison.OrdinalIgnoreCase))
    {
        compaction.Enabled = false;
        compaction.ShowStatus(session);
        continue;
    }
    if (prompt.Equals("/compact", StringComparison.OrdinalIgnoreCase) ||
        prompt.Equals("--compact", StringComparison.OrdinalIgnoreCase))
    {
        compaction.Enabled = true;
        await compaction.CompactSessionAsync(session);
        compaction.ShowStatus(session);
        continue; // Il comando non viene inviato al modello.
    }
    using var turn = Telemetry.Source.StartActivity("demo.turn");
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var response = await agent.RunAsync(prompt, session, cancellationToken: timeout.Token);
    foreach (var call in response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>())
        DemoConsole.Line($"[HARNESS TOOL] {call.Name}", ConsoleColor.Magenta);
    DemoConsole.Answer(response.Text);
    compaction.ShowStatus(session);
}

[Description("Restituisce data e città dell'evento dimostrativo.")]
static string GetEventInfo()
{
    using var span = Telemetry.Source.StartActivity("tool.GetEventInfo");
    DemoConsole.Line("[TOOL] GetEventInfo", ConsoleColor.Magenta);
    return "AI Day Torino — Torino, 10 ottobre 2026.";
}
