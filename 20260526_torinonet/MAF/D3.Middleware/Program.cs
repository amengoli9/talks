// D3 - The three middleware types of the Agent Framework, on one simple agent.
//
//   1. Chat client middleware      -> wraps every call to the LLM
//   2. Agent run middleware        -> wraps a whole agent run
//   3. Function invocation middleware -> wraps each tool call
//
// Each middleware prints a PRE/POST line so you can see the pipeline nest:
// agent-run is the outermost, then chat-client, then function (only when a tool runs).

using System.ComponentModel;
using System.Diagnostics;
using Maf.Demo.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

const string ServiceName = "D3.Middleware";
using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);

var activitySource = new ActivitySource(ServiceName);
using var activity = activitySource.StartActivity("INIT", ActivityKind.Client);

// A trivial tool
[Description("Restituisce l'ora corrente.")]
static string GetCurrentTime() => DateTimeOffset.Now.ToString("HH:mm:ss");

// --- (1) CHAT CLIENT MIDDLEWARE: intercepts calls to the LLM ---
async Task<ChatResponse> ChatClientMiddleware(
    IEnumerable<ChatMessage> messages, ChatOptions? options, IChatClient inner, CancellationToken ct)
{
    Log("1. chat-client", "PRE  -> sto per chiamare l'LLM");
    ChatResponse response = await inner.GetResponseAsync(messages, options, ct);
    Log("1. chat-client", "POST <- risposta ricevuta dall'LLM");
    return response;
}

// ChatClientFactory.Create already adds function-invocation handling and OpenTelemetry;
// here we add the demo chat-client middleware on top.
IChatClient chatClient = ChatClientFactory.Create(ServiceName)
    .AsBuilder()
    .Use(getResponseFunc: ChatClientMiddleware, getStreamingResponseFunc: null)
    .Build();

AIAgent baseAgent = new ChatClientAgent(
    chatClient,
    "Sei un assistente conciso. Se ti chiedono l'ora, usa lo strumento a disposizione.",
    "assistant",
    tools: [AIFunctionFactory.Create(GetCurrentTime, name: "get_current_time")]);

// --- (2) AGENT RUN MIDDLEWARE: wraps an entire agent run ---
async Task<AgentResponse> AgentRunMiddleware(
    IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent inner, CancellationToken ct)
{
    Log("2. agent-run", "PRE  -> run dell'agente in partenza");
    AgentResponse response = await inner.RunAsync(messages, session, options, ct);
    Log("2. agent-run", "POST <- run dell'agente completato");
    return response;
}

// --- (3) FUNCTION INVOCATION MIDDLEWARE: wraps each tool call ---
async ValueTask<object?> FunctionInvocationMiddleware(
    AIAgent agent, FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken ct)
{
    Log("3. function", $"PRE  -> sto per invocare il tool '{context.Function.Name}'");
    object? result = await next(context, ct);
    Log("3. function", $"POST <- il tool '{context.Function.Name}' ha restituito: {result}");
    return result;
}

AIAgent agent = baseAgent
    .AsBuilder()
    .Use(AgentRunMiddleware, null)
    .Use(FunctionInvocationMiddleware)
    .Build()
    .WithOpenTelemetry(ServiceName);

// Interactive loop
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("Suggerimenti: 'Che ore sono?' (attiva il tool) oppure 'Dimmi una curiosita sul C#' (senza tool)");
Console.ResetColor();

while (true)
{
    Console.Write("Domanda (vuoto per uscire)> ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) break;
    await Ask(agent, input);
}

static async Task Ask(AIAgent agent, string prompt)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 64));
    Console.WriteLine($"Utente: {prompt}");
    Console.WriteLine(new string('-', 64));
    AgentResponse response = await agent.RunAsync(prompt);
    Console.WriteLine(new string('-', 64));
    Console.WriteLine($"Agente: {response.Text}");
}

static void Log(string layer, string message)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write($"  [{layer}] ");
    Console.ResetColor();
    Console.WriteLine(message);
}
