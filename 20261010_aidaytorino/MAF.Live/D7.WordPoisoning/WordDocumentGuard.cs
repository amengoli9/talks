using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Torino.Live;

// Policy conservativa per questo esempio: rifiuta il documento, non prova a ripulirlo.
public sealed class WordDocumentGuard(string visible, string full)
{
    public bool Blocked { get; private set; }

    public async Task<AgentResponse> InvokeAsync(IEnumerable<ChatMessage> messages, AgentSession? session,
        AgentRunOptions? options, AIAgent next, CancellationToken token)
    {
        using var span = Telemetry.Source.StartActivity("middleware.document_guard");
        bool hiddenText = !string.Equals(visible, full, StringComparison.Ordinal);
        bool fakeRole = full.Contains("[SYSTEM]", StringComparison.OrdinalIgnoreCase) ||
            full.Contains("[/SYSTEM]", StringComparison.OrdinalIgnoreCase);
        Blocked = hiddenText || fakeRole;
        span?.SetTag("document.hidden_text", hiddenText);
        span?.SetTag("document.role_marker", fakeRole);
        Telemetry.Decision("word_document", Blocked ? "blocked" : "allowed");
        if (Blocked)
            return new AgentResponse(new ChatMessage(ChatRole.Assistant,
                "Il Word contiene testo non visibile o marcatori di ruolo. Richiedere una versione verificata del documento."));

        return await next.RunAsync(messages, session, options, token);
    }
}
