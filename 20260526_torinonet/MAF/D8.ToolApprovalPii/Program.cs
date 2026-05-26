// D8 - Tool approval + PII redaction on a single agent.
//
// Two safety features combined on a customer-support agent:
//   1. PII redaction  - an agent-run middleware scrubs emails, phone numbers and
//      full names from BOTH the messages sent to the LLM and the messages it returns,
//      so personal data never reaches the model, the traces or the logs.
//   2. Tool approval  - the 'send_email' tool is wrapped in ApprovalRequiredAIFunction:
//      the agent cannot send anything without explicit human approval.

using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Maf.Demo.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

const string ServiceName = "D8.ToolApprovalPii";

using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);

var activitySource = new ActivitySource(ServiceName);
using var activity = activitySource.StartActivity("INIT", ActivityKind.Client);

IChatClient chat = ChatClientFactory.Create(ServiceName);

// --- The sensitive tool: sending email always requires human approval ---
[Description("Invia una email.")]
static string SendEmail(
    [Description("Indirizzo del destinatario")] string recipient,
    [Description("Corpo del messaggio")] string body)
    => $"Email inviata a {recipient}.";

// --- PII redaction ---
Regex[] piiPatterns =
[
    new(@"\b\d{3}[-\s]?\d{6,7}\b"),          // numero di telefono
    new(@"\b[\w.\-]+@[\w.\-]+\.\w+\b"),      // email
    new(@"\b[A-Z][a-z]+\s[A-Z][a-z]+\b"),    // nome e cognome
];

string RedactPii(string text)
{
    foreach (Regex pattern in piiPatterns)
    {
        text = pattern.Replace(text, "[PII RIMOSSA]");
    }
    return text;
}

// Redacts text content while preserving non-text content (tool calls, approval requests).
ChatMessage RedactMessage(ChatMessage message)
{
    List<AIContent> contents = message.Contents
        .Select<AIContent, AIContent>(c => c is TextContent t ? new TextContent(RedactPii(t.Text)) : c)
        .ToList();
    return new ChatMessage(message.Role, contents) { AuthorName = message.AuthorName };
}

// Agent-run middleware: redact PII on the way in AND on the way out.
// Opens its own OpenTelemetry span so PII redaction is visible in the trace.
async Task<AgentResponse> PiiRedactionMiddleware(
    IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent inner, CancellationToken ct)
{
    using Activity? span = activitySource.StartActivity("middleware.pii-redaction");

    string originalInput = string.Join(" | ", messages.Select(m => m.Text));
    List<ChatMessage> redactedInput = messages.Select(RedactMessage).ToList();
    string redactedInputText = string.Join(" | ", redactedInput.Select(m => m.Text));
    span?.SetTag("pii.input.original", originalInput);
    span?.SetTag("pii.input.redacted", redactedInputText);
    Console.WriteLine("  [pii] input ripulito dai dati personali prima dell'LLM");

    AgentResponse response = await inner.RunAsync(redactedInput, session, options, ct);

    string originalOutput = string.Join(" | ", response.Messages.Select(m => m.Text));
    List<ChatMessage> redactedOutput = response.Messages.Select(RedactMessage).ToList();
    string redactedOutputText = string.Join(" | ", redactedOutput.Select(m => m.Text));
    response.Messages = redactedOutput;
    span?.SetTag("pii.output.original", originalOutput);
    span?.SetTag("pii.output.redacted", redactedOutputText);
    Console.WriteLine("  [pii] output ripulito dai dati personali");
    return response;
}

AIAgent agent = new ChatClientAgent(chat,
    "Sei un assistente del supporto clienti. Quando devi inviare una comunicazione, usa lo strumento send_email verso l'indirizzo del supporto interno: supporto@azienda.it.",
    "support-assistant",
    tools: [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(SendEmail, name: "send_email"))])
    .AsBuilder()
    .Use(PiiRedactionMiddleware, null)
    .Build()
    .WithOpenTelemetry(ServiceName);

AgentSession session = await agent.CreateSessionAsync();

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("Suggerimenti:");
Console.WriteLine("  'Ciao, sono Mario Rossi, il mio numero e' 333-1234567. Che orari avete?' (PII redaction)");
Console.WriteLine("  'Ho un problema con l'ordine 4815. Scrivi un riassunto del reclamo e invialo al supporto.' (PII + tool approval)");
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

    // Approval loop: pause whenever the agent wants to call the approval-required tool.
    List<ToolApprovalRequestContent> pending = ExtractApprovals(response);
    while (pending.Count > 0)
    {
        var replies = new List<ChatMessage>();
        foreach (ToolApprovalRequestContent request in pending)
        {
            var call = (FunctionCallContent)request.ToolCall;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [HITL] L'agente vuole eseguire: {call.Name}({string.Join(", ", call.Arguments?.Keys ?? [])})");
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
