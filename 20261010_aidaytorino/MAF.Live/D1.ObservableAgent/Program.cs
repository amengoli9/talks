using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Torino.Live;

using var telemetry = new Telemetry("D1.ObservableAgent");
DemoConsole.Title("D1 — Modello → tool → modello, tutto in una trace");
using var client = Demo.CreateClient();

AIAgent agent = new ChatClientAgent(client,
    name: "EventAssistant",
    instructions: "Rispondi in italiano. Per data e luogo consulta sempre GetEventInfo.",
    tools: [AIFunctionFactory.Create(GetEventInfo, nameof(GetEventInfo))])
    .AsBuilder()
    .UseOpenTelemetry(Telemetry.AgentSource, o => o.EnableSensitiveData = true)
    .Build();

var session = await agent.CreateSessionAsync();
while (Demo.ReadPrompt("Quando e dove si svolge AI Day Torino?", "Prepara un promemoria per AI Day Torino.") is { } prompt)
{
    using var turn = Telemetry.Source.StartActivity("demo.turn");
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var response = await agent.RunAsync(prompt, session, cancellationToken: timeout.Token);
    DemoConsole.Answer(response.Text);
}

[Description("Restituisce data e città dell'evento dimostrativo.")]
static string GetEventInfo()
{
    using var span = Telemetry.Source.StartActivity("tool.GetEventInfo");
    DemoConsole.Line("[TOOL] GetEventInfo", ConsoleColor.Magenta);
    return "AI Day Torino — Torino, 10 ottobre 2026.";
}
