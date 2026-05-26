// D7 - Human in the loop.
//
// A bank assistant agent with two tools:
//   - get_account_balance : safe, runs automatically.
//   - transfer_money      : sensitive, wrapped in ApprovalRequiredAIFunction.
//
// When the agent wants to call the sensitive tool, the run PAUSES and returns a
// ToolApprovalRequestContent. A human approves or rejects on the console, the
// decision is fed back, and the agent continues. This is human-in-the-loop as a
// first-class agent feature.

using System.ComponentModel;
using System.Diagnostics;
using Maf.Demo.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

const string ServiceName = "D7.HumanInTheLoop";

using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);

var activitySource = new ActivitySource(ServiceName);
using var activity = activitySource.StartActivity("INIT", ActivityKind.Client);

IChatClient chat = ChatClientFactory.Create(ServiceName);

[Description("Restituisce il saldo del conto corrente.")]
static string GetAccountBalance() => "Il saldo attuale e' 4.250,00 EUR.";

[Description("Esegue un bonifico bancario verso un destinatario.")]
static string TransferMoney(
    [Description("Importo in euro")] decimal amount,
    [Description("Nome del destinatario")] string recipient)
    => $"Bonifico di {amount:N2} EUR a {recipient} eseguito con successo.";

// The safe tool runs automatically. The sensitive tool is wrapped in
// ApprovalRequiredAIFunction: the agent cannot invoke it without explicit human approval.
AIAgent agent = new ChatClientAgent(chat,
    "Sei l'assistente di una banca. Usa gli strumenti a disposizione per rispondere alle richieste dell'utente.",
    "bank-assistant",
    tools:
    [
        AIFunctionFactory.Create(GetAccountBalance, name: "get_account_balance"),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(TransferMoney, name: "transfer_money")),
    ])
    .WithOpenTelemetry(ServiceName);

AgentSession session = await agent.CreateSessionAsync();

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("Suggerimenti: 'Quanto ho sul conto?' (tool sicuro, niente approvazione)");
Console.WriteLine("              'Trasferisci 500 euro a Mario Rossi' (tool sensibile, richiede approvazione)");
Console.ResetColor();

while (true)
{
    Console.Write("Domanda (vuoto per uscire)> ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) break;
    await Ask(input);
}

async Task Ask(string prompt)
{
    Console.WriteLine(new string('=', 64));
    Console.WriteLine($"Utente: {prompt}");

    AgentResponse response = await agent.RunAsync(prompt, session);

    // The run pauses whenever the agent wants to call an approval-required tool.
    List<ToolApprovalRequestContent> pending = ExtractApprovals(response);
    while (pending.Count > 0)
    {
        var replies = new List<ChatMessage>();
        foreach (ToolApprovalRequestContent request in pending)
        {
            var call = (FunctionCallContent)request.ToolCall;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [HITL] L'agente vuole eseguire: {call.Name}({string.Join(", ", call.Arguments?.Values ?? [])})");
            Console.Write("  Approvi? (Y/N): ");
            Console.ResetColor();

            bool approved = Console.ReadLine()?.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase) ?? false;
            Console.WriteLine($"  -> {(approved ? "APPROVATO" : "RIFIUTATO")}");

            replies.Add(new ChatMessage(ChatRole.User, [request.CreateResponse(approved)]));
        }

        response = await agent.RunAsync(replies, session);
        pending = ExtractApprovals(response);
    }

    Console.WriteLine($"Agente: {response.Text}\n");
}

static List<ToolApprovalRequestContent> ExtractApprovals(AgentResponse response) =>
    response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
