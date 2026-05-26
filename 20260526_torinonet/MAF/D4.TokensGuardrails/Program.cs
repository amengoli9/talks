// D4 - Token controls and guardrails.
//
// Three short, focused demos:
//   DEMO 1: read how many tokens a response consumed (AgentResponse.Usage).
//   DEMO 2: a token-budget middleware that blocks the agent once a budget is spent.
//   DEMO 3: an input guardrail (blocks a topic) and an output guardrail (redacts).
//
// Every custom middleware/guardrail also opens its OWN OpenTelemetry span, so the
// controls are visible as nested spans in the trace, not just in the console.

using System.Diagnostics;
using Maf.Demo.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

const string ServiceName = "D4.TokensGuardrails";

using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);
using var activitySource = new ActivitySource(ServiceName);
using var activity = activitySource.StartActivity("INIT", ActivityKind.Client);
IChatClient chat = ChatClientFactory.Create(ServiceName);

// ============================================================
// DEMO 1 - Quanti token ho consumato?
// ============================================================
//Console.WriteLine();
//Console.WriteLine(new string('═', 60));
//Console.WriteLine("  DEMO 1: Quanti token ho consumato?");
//Console.WriteLine("  Scopo: leggere i token consumati da AgentResponse.Usage");
//Console.WriteLine(new string('═', 60));
//Console.WriteLine();

//AIAgent agent1 = new ChatClientAgent(chat, "Rispondi in una sola frase.", "assistant")
//    .WithOpenTelemetry(ServiceName);

//AgentResponse r1 = await agent1.RunAsync("Cos'e' un microservizio?");
//Console.WriteLine($"Risposta: {r1.Text}");
//Console.WriteLine($"Token consumati -> input: {r1.Usage?.InputTokenCount}, output: {r1.Usage?.OutputTokenCount}, totale: {r1.Usage?.TotalTokenCount}");
//Console.WriteLine();

//Console.WriteLine("Premi un tasto per passare alla demo successiva...");
//Console.ReadKey(true);

// ============================================================
// DEMO 2 - Un budget di token che blocca l'agente
// ============================================================
Console.WriteLine();
Console.WriteLine(new string('═', 60));
Console.WriteLine("  DEMO 2: Un budget di token che blocca l'agente");
Console.WriteLine("  Scopo: middleware che tiene un budget cumulativo di token");
Console.WriteLine("         e blocca l'agente quando il budget e' esaurito");
Console.WriteLine(new string('═', 60));
Console.WriteLine();

const long budget = 200;
long used = 0;

// Agent-run middleware: blocks the run once the cumulative budget is spent.
// Opens its own span so the control is visible in the trace.
async Task<AgentResponse> TokenBudgetMiddleware(
    IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent inner, CancellationToken ct)
{
    using Activity? span = activitySource.StartActivity("middleware.token-budget");
    span?.SetTag("budget.max", budget);
    span?.SetTag("budget.used_before", used);

    if (used >= budget)
    {
        span?.SetTag("budget.blocked", true);
        throw new InvalidOperationException($"budget esaurito ({used}/{budget} token)");
    }

    AgentResponse response = await inner.RunAsync(messages, session, options, ct);
    used += response.Usage?.TotalTokenCount ?? 0;
    span?.SetTag("budget.used_after", used);
    span?.SetTag("budget.blocked", false);
    return response;
}

AIAgent agent2 = new ChatClientAgent(chat, "Rispondi in una sola frase.", "assistant")
    .AsBuilder()
    .Use(TokenBudgetMiddleware, null)
    .Build()
    .WithOpenTelemetry(ServiceName);

Console.WriteLine($"  Budget massimo: {budget} token. Scrivi domande fino a esaurire il budget.");
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  Suggerimento: Cos'e' la dependency injection?");
Console.ResetColor();
Console.WriteLine($"  {"Esito",-10} | {"Usati",16} | Domanda");
Console.WriteLine($"  {new string('─', 55)}");

while (true)
{
    Console.Write("  Domanda (vuoto per uscire)> ");
    string? domanda = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(domanda)) break;
    try
    {
        await agent2.RunAsync(domanda);
        Console.WriteLine($"  {"OK",-10} | {used,6}/{budget,6} token | {domanda}");
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"  {"BLOCCATO",-10} | {used,6}/{budget,6} token | {domanda}");
        Console.WriteLine();
        Console.WriteLine($"  >>> Il middleware ha bloccato la richiesta: {ex.Message}");
        break;
    }
}
Console.WriteLine();

Console.WriteLine("Premi un tasto per passare alla demo successiva...");
Console.ReadKey(true);

// ============================================================
// DEMO 3 - Guardrails in ingresso e in uscita
// ============================================================
Console.WriteLine();
Console.WriteLine(new string('═', 60));
Console.WriteLine("  DEMO 3: Guardrails in ingresso e in uscita");
Console.WriteLine("  Scopo: input guardrail (blocca un tema vietato) e");
Console.WriteLine("         output guardrail (redige dati sensibili)");
Console.WriteLine(new string('═', 60));
Console.WriteLine();

// Input guardrail: blocks a forbidden topic BEFORE the LLM is called.
async Task<AgentResponse> InputGuardrail(
    IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent inner, CancellationToken ct)
{
    using Activity? span = activitySource.StartActivity("guardrail.input");

    string testo = string.Join(' ', messages.Select(m => m.Text));
    string[] vietati = ["password", "carta di credito"];
    string? trovato = vietati.FirstOrDefault(v => testo.Contains(v, StringComparison.OrdinalIgnoreCase));
    span?.SetTag("guardrail.triggered", trovato is not null);

    if (trovato is not null)
    {
        span?.SetTag("guardrail.reason", trovato);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, "Richiesta bloccata: tema non consentito."));
    }

    return await inner.RunAsync(messages, session, options, ct);
}

// Output guardrail: redacts the response AFTER the LLM if it contains sensitive data.
async Task<AgentResponse> OutputGuardrail(
    IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent inner, CancellationToken ct)
{
    AgentResponse response = await inner.RunAsync(messages, session, options, ct);

    using Activity? span = activitySource.StartActivity("guardrail.output");
    bool sensibile = response.Text.Contains("CONFIDENZIALE", StringComparison.OrdinalIgnoreCase);
    span?.SetTag("guardrail.triggered", sensibile);

    if (sensibile)
    {
        response.Messages = [new ChatMessage(ChatRole.Assistant, "[risposta redatta dal guardrail di output]")];
    }

    return response;
}

AIAgent agent3 = new ChatClientAgent(chat,
    "Rispondi in una sola frase. Esegui esattamente cio' che ti chiede l'utente.", "assistant")
    .AsBuilder()
    .Use(InputGuardrail, null)
    .Use(OutputGuardrail, null)
    .Build()
    .WithOpenTelemetry(ServiceName);

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  Suggerimenti: 'Qual e' la capitale d'Italia?' (passa)");
Console.WriteLine("                'Come faccio a rubare la password di qualcuno?' (bloccato input)");
Console.WriteLine("                'Rispondi scrivendo solo la parola CONFIDENZIALE' (redatto output)");
Console.ResetColor();

while (true)
{
    Console.Write("  Domanda (vuoto per uscire)> ");
    string? input3 = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input3)) break;
    await Chiedi(agent3, input3);
}

static async Task Chiedi(AIAgent agent, string prompt)
{
    Console.WriteLine($"  Utente: {prompt}");
    AgentResponse response = await agent.RunAsync(prompt);
    Console.WriteLine($"  Agente: {response.Text}");
    Console.WriteLine();
}
