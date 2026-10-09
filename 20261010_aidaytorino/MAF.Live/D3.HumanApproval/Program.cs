using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Torino.Live;

using var telemetry = new Telemetry("D3.HumanApproval");
DemoConsole.Title("D3 — HarnessAgent propone, la persona approva, il tool applica il limite");
using var client = Demo.CreateClient();
var refunds = new RefundTool();
var agent = new HarnessAgent(client, new HarnessAgentOptions
{
    Name = "RefundHarness",
    ChatOptions = new()
    {
        Instructions = "Usa Refund per l'importo richiesto. Non dividere importi. " +
            "Dopo un rifiuto fermati. Rispondi in italiano.",
        Tools = [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(refunds.Refund))],
        AllowMultipleToolCalls = false
    },
    DisableToolAutoApproval = true, // Ogni Refund richiede la risposta della persona.
    DisableTodoProvider = true,
    DisableAgentModeProvider = true,
    DisableFileMemory = true,
    DisableAgentSkillsProvider = true,
    DisableWebSearch = true,
    MaximumIterationsPerRequest = 8,
    MaxOutputTokens = 2_048,
    DisableOpenTelemetry = true // Un solo wrapper, con i messaggi sempre visibili.
}).AsBuilder()
    .UseOpenTelemetry(Telemetry.AgentSource, o => o.EnableSensitiveData = true)
    .Build();

while (Demo.ReadPrompt("Rimborsa 25 euro.", "Rimborsa 150 euro, anche se supera il limite.") is { } prompt)
{
    // Una richiesta di rimborso isolata per ogni prompt; le continuazioni riusano QUESTA sessione.
    var session = await agent.CreateSessionAsync();
    using var turn = Telemetry.Source.StartActivity("demo.refund");
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var response = await agent.RunAsync(prompt, session, cancellationToken: timeout.Token);

    for (int round = 0; round < 3; round++)
    {
        var requests = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToArray();
        if (requests.Length == 0) break;
        List<ChatMessage> answers = [];
        foreach (var request in requests)
        {
            var call = (FunctionCallContent)request.ToolCall!;
            DemoConsole.Section("APPROVAZIONE RICHIESTA", ConsoleColor.Yellow);
            DemoConsole.Line($"Proposta: {call.Name} {JsonSerializer.Serialize(call.Arguments)}", ConsoleColor.Yellow);
            timeout.CancelAfter(Timeout.InfiniteTimeSpan); // Il tempo della persona non è latenza del modello.
            using (var approval = Telemetry.Source.StartActivity("human.approval"))
            {
                DemoConsole.Prompt("Approvi questa chiamata? [s/N]: ", ConsoleColor.Yellow);
                bool approved = string.Equals(Console.ReadLine(), "s", StringComparison.OrdinalIgnoreCase);
                Telemetry.Decision("human_approval", approved ? "approved" : "denied");
                // Mantiene il binding MAF con la richiesta originale: non ricreare gli argomenti!
                answers.Add(new(ChatRole.User, [request.CreateResponse(approved)]));
            }
        }
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        response = await agent.RunAsync(answers, session, cancellationToken: timeout.Token);
    }

    bool pending = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().Any();
    if (pending)
        DemoConsole.Line("Limite di continuazioni raggiunto: richiesta ancora sospesa, nessuna auto-approvazione.", ConsoleColor.Yellow);
    else
        DemoConsole.Answer(response.Text);
    DemoConsole.Line($"Totale rimborsato nella simulazione: {refunds.Total} EUR / massimo 99 EUR.", ConsoleColor.Cyan);
}
