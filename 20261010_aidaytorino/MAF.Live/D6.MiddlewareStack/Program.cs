using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Torino.Live;

using var telemetry = new Telemetry("D6.MiddlewareStack");
DemoConsole.Title("D6 — Tre punti, lo stesso pattern: prima → next → dopo");
DemoConsole.Line("AGENTE → [MODELLO → TOOL → MODELLO] → AGENTE", ConsoleColor.Cyan);
using var model = Demo.CreateClient();
var agent = MiddlewareStack.CreateAgent(model);

while (Demo.ReadPrompt("Quando e dove si svolge AI Day Torino?") is { } prompt)
{
    using var turn = Telemetry.Source.StartActivity("demo.middleware_stack");
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    // Ogni prova è indipendente: il tool viene consultato di nuovo.
    var response = await agent.RunAsync(prompt, cancellationToken: timeout.Token);
    DemoConsole.Answer(response.Text);
}

public static class MiddlewareStack
{
    public static AIAgent CreateAgent(IChatClient model)
    {
        // 1. MODELLO: prima e dopo OGNI chiamata al modello, anche dopo un tool.
        var client = model.AsBuilder().Use(ChatMiddleware, null).Build();

        return new ChatClientAgent(client,
            name: "EventAssistant",
            instructions: "Rispondi in italiano, in una frase. Per data e luogo consulta sempre GetEventInfo.",
            tools: [AIFunctionFactory.Create(GetEventInfo)])
            .AsBuilder()
            .UseOpenTelemetry(Telemetry.AgentSource, o => o.EnableSensitiveData = true)
            .Use(AgentMiddleware, null) // 2. AGENTE: prima e dopo l'intera RunAsync.
            .Use(FunctionMiddleware)   // 3. TOOL: prima e dopo ogni invocazione di un tool.
            .Build();
    }

    private static async Task<AgentResponse> AgentMiddleware(IEnumerable<ChatMessage> messages,
        AgentSession? session, AgentRunOptions? options, AIAgent next, CancellationToken token)
    {
        using var span = Telemetry.Source.StartActivity("middleware.agent");
        DemoConsole.Line("[AGENTE →] inizio RunAsync", ConsoleColor.Cyan);
        var response = await next.RunAsync(messages, session, options, token);
        DemoConsole.Line("[← AGENTE] risposta finale", ConsoleColor.Cyan);
        return response;
    }

    private static async Task<ChatResponse> ChatMiddleware(IEnumerable<ChatMessage> messages,
        ChatOptions? options, IChatClient next, CancellationToken token)
    {
        using var span = Telemetry.Source.StartActivity("middleware.chat");
        DemoConsole.Line("    [MODELLO →] invio messaggi", ConsoleColor.Yellow);
        var response = await next.GetResponseAsync(messages, options, token);
        DemoConsole.Line("    [← MODELLO] testo oppure richiesta di tool", ConsoleColor.Yellow);
        return response;
    }

    private static async ValueTask<object?> FunctionMiddleware(AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken token)
    {
        using var span = Telemetry.Source.StartActivity("middleware.function");
        span?.SetTag("tool.name", context.Function.Name);
        DemoConsole.Line($"    [TOOL →] {context.Function.Name}", ConsoleColor.Magenta);
        var result = await next(context, token);
        DemoConsole.Line("    [← TOOL] risultato pronto", ConsoleColor.Magenta);
        return result;
    }

    [Description("Restituisce data e città dell'evento dimostrativo.")]
    private static string GetEventInfo() => "AI Day Torino — Torino, 10 ottobre 2026.";
}
