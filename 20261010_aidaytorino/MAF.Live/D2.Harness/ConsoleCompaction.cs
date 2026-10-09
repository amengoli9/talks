using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Torino.Live;

// Wrapper per interruttore e console; la riduzione è quella nativa di MAF.
public sealed class ConsoleCompaction() : CompactionStrategy(CompactionTriggers.Always)
{
    private readonly SlidingWindowCompactionStrategy strategy = new(CompactionTriggers.TurnsExceed(2));
    public bool Enabled { get; set; }

    protected override async ValueTask<bool> CompactCoreAsync(CompactionMessageIndex index,
        ILogger logger, CancellationToken cancellationToken)
    {
        if (!Enabled) return false;
        int beforeMessages = index.IncludedMessageCount;
        int beforeTokens = index.IncludedTokenCount;
        bool changed = await strategy.CompactAsync(index, logger, cancellationToken);
        if (changed)
            DemoConsole.Line($"[COMPACTION] messaggi {beforeMessages} → {index.IncludedMessageCount}; " +
                $"token stimati {beforeTokens} → {index.IncludedTokenCount}; ultimi 2 turni conservati.", ConsoleColor.Yellow);
        return changed;
    }

    public async Task CompactSessionAsync(AgentSession session)
    {
        using var span = Telemetry.Source.StartActivity("demo.compact_session");
        if (!session.TryGetInMemoryChatHistory(out var history) || history.Count == 0)
        {
            DemoConsole.Line("Compaction attiva. Scrivi almeno tre prompt per vedere la riduzione.", ConsoleColor.Yellow);
            return;
        }
        var result = (await CompactionProvider.CompactAsync(this, history)).ToList();
        session.SetInMemoryChatHistory(result);
        if (history.Count == result.Count)
            DemoConsole.Line("Nessuna riduzione necessaria: la storia è già entro gli ultimi 2 turni.");
    }

    public void ShowStatus(AgentSession session)
    {
        session.TryGetInMemoryChatHistory(out var history);
        history ??= [];
        DemoConsole.Line($"[CONTESTO] compaction {(Enabled ? "ON" : "OFF")} | " +
            $"turni: {history.Count(m => m.Role == ChatRole.User)} | messaggi: {history.Count}", ConsoleColor.Cyan);
    }
}
