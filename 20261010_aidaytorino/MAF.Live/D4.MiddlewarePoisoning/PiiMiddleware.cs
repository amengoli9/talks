using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Torino.Live;

// Demo testuale: email e codici DEMO a cinque cifre. Non è un classificatore PII generale.
public sealed class PiiMiddleware(bool block = false)
{
    private static readonly Regex Pattern = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}|\bDEMO-\d{5}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public async Task<AgentResponse> InvokeAsync(IEnumerable<ChatMessage> messages, AgentSession? session,
        AgentRunOptions? options, AIAgent next, CancellationToken token)
    {
        var input = messages.ToList();
        bool found = input.SelectMany(m => m.Contents).OfType<TextContent>().Any(c => Pattern.IsMatch(c.Text));
        if (block && found)
        {
            Telemetry.Decision("pii.input", "blocked");
            return new AgentResponse(new ChatMessage(ChatRole.Assistant, "Input bloccato: contiene PII della demo."));
        }

        // Prima di next: il modello e la storia ricevono il prompt oscurato.
        var response = await next.RunAsync(Redact(input, "input"), session, options, token);
        // Dopo next: oscuriamo il testo pubblicato. I tool sono già stati eseguiti!
        response.Messages = Redact(response.Messages, "output");
        response.RawRepresentation = null;
        return response;
    }

    private static List<ChatMessage> Redact(IEnumerable<ChatMessage> messages, string point)
    {
        int count = 0;
        var copies = messages.Select(m => m.Clone()).ToList();
        foreach (var message in copies)
        {
            message.RawRepresentation = null;
            message.Contents = message.Contents.Select(content =>
            {
                if (content is not TextContent text) return content;
                count += Pattern.Matches(text.Text).Count;
                return new TextContent(Pattern.Replace(text.Text, "[REDACTED]"));
            }).ToList();
        }
        Telemetry.Decision("pii." + point, count == 0 ? "clean" : "redacted");
        DemoConsole.Line($"[PII] {point}: {count} rilevazioni", count == 0 ? ConsoleColor.Gray : ConsoleColor.Yellow);
        return copies;
    }
}
