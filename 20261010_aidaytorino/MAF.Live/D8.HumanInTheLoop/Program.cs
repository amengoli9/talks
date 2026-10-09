using System.ComponentModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Torino.Live;

using var telemetry = new Telemetry("D8.HumanInTheLoop");
DemoConsole.Title("D8 — HarnessAgent: approva, ricorda, applica una regola");
bool auto = args.Contains("--auto");
DemoConsole.Line("Pubblicazioni simulate: solo console. /reset = nuova sessione, senza permessi ricordati.");
DemoConsole.Line($"AutoApprovalRules: {(auto ? "ON — canale bozze" : "OFF — avvia con --auto per provarle")}.");
using var client = Demo.CreateClient();
var demo = new AnnouncementDemo();
var agent = demo.CreateAgent(client, auto);
var session = await agent.CreateSessionAsync(); // Le approvazioni ricordate vivono qui, anche tra turni.

while (Demo.ReadPrompt("Pubblica sul canale evento il testo esatto: Benvenuti ad AI Day Torino!",
    "Pubblica sul canale bozze il testo esatto: Prova microfono.") is { } prompt)
{
    if (prompt.Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        session = await agent.CreateSessionAsync();
        DemoConsole.Line("[SESSIONE] Nuova: storia e approvazioni ricordate azzerate. Configurazione --auto invariata.");
        continue;
    }
    int before = demo.PublicationCount;
    using var turn = Telemetry.Source.StartActivity("demo.hitl");
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var response = await agent.RunAsync(prompt, session, cancellationToken: timeout.Token);

    for (int round = 0; round < 3; round++)
    {
        var requests = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToArray();
        if (requests.Length == 0) break;
        timeout.CancelAfter(Timeout.InfiniteTimeSpan); // La persona può prendersi il suo tempo.
        List<ChatMessage> answers = [];
        foreach (var request in requests)
        {
            // La persona vede la chiamata effettiva, con i suoi argomenti originali.
            var call = (FunctionCallContent)request.ToolCall!;
            DemoConsole.Section("APPROVAZIONE RICHIESTA", ConsoleColor.Yellow);
            DemoConsole.Line($"Proposta: {call.Name} {JsonSerializer.Serialize(call.Arguments)}", ConsoleColor.Yellow);
            using var approval = Telemetry.Source.StartActivity("human.approval");
            DemoConsole.Line("s = una volta · a = ricorda stessi argomenti · t = ricorda tool con QUALSIASI argomento", ConsoleColor.Yellow);
            DemoConsole.Prompt("Scelta [s/a/t/N]: ", ConsoleColor.Yellow);
            string? choice = Console.ReadLine()?.Trim().ToLowerInvariant();
            // Il wrapper dell'harness registra le scelte a/t nella sessione.
            AIContent answer = choice switch
            {
                "s" => request.CreateResponse(true),
                "a" => request.CreateAlwaysApproveToolWithArgumentsResponse(),
                "t" => request.CreateAlwaysApproveToolResponse(),
                _ => request.CreateResponse(false)
            };
            Telemetry.Decision("human_approval", choice switch
            {
                "s" => "approved", "a" => "approved_same_arguments", "t" => "approved_tool", _ => "denied"
            });
            answers.Add(new(ChatRole.User, [answer]));
        }

        // Riprendi la STESSA sessione: MAF gestisce esecuzione, rifiuto e permessi ricordati.
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        response = await agent.RunAsync(answers, session, cancellationToken: timeout.Token);
    }

    if (response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().Any())
        DemoConsole.Line("Richiesta ancora sospesa dopo 3 passaggi: la chiamata restante non viene eseguita.", ConsoleColor.Yellow);
    else
        DemoConsole.Answer(response.Text);
    DemoConsole.Line($"Pubblicazioni simulate in questo turno: {demo.PublicationCount - before}.", ConsoleColor.Cyan);
}

// Configurazione, regola e tool nello stesso file per mostrarli sul palco.
public sealed class AnnouncementDemo
{
    public int PublicationCount { get; private set; }

    public AIAgent CreateAgent(IChatClient client, bool autoApproveDrafts = false) =>
        new HarnessAgent(client, new HarnessAgentOptions
        {
            Name = "AnnouncementHarness",
            ChatOptions = new()
            {
                Instructions = "Rispondi in italiano, brevemente. Ogni richiesta di pubblicazione è una nuova " +
                    "chiamata a PublishAnnouncement, anche se identica alla precedente. Copia il testo esatto in message. " +
                    "Usa il canale richiesto, oppure evento se non specificato. Dopo un rifiuto fermati senza riprovare.",
                Tools = [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(PublishAnnouncement))],
                AllowMultipleToolCalls = false
            },
            DisableToolAutoApproval = false, // Abilita memoria delle approvazioni e regole; NON approva tutto.
            ToolApprovalAgentOptions = new()
            {
                AutoApprovalRules = autoApproveDrafts ? [ApproveDrafts] : null,
                MaxAutoApprovalIterations = 3
            },
            DisableTodoProvider = true, DisableAgentModeProvider = true,
            DisableFileMemory = true, DisableAgentSkillsProvider = true, DisableWebSearch = true,
            MaximumIterationsPerRequest = 8, MaxOutputTokens = 2_048, DisableOpenTelemetry = true
        }).AsBuilder()
            .UseOpenTelemetry(Telemetry.AgentSource, o => o.EnableSensitiveData = true).Build();

    private static ValueTask<bool> ApproveDrafts(ToolAutoApprovalRuleContext context)
    {
        var call = context.FunctionCallContent;
        bool match = call.Name == nameof(PublishAnnouncement) &&
            call.Arguments?.TryGetValue("channel", out var channel) == true && channel?.ToString() == "bozze";
        DemoConsole.Line($"[REGOLA] SoloBozze = {match}: " +
            (match ? "approvazione automatica." : "nessuna approvazione da questa regola; non è un divieto."), ConsoleColor.Yellow);
        Telemetry.Decision("auto_approval", match ? "approved" : "not_matched");
        return ValueTask.FromResult(match); // false: prossima regola; se nessuna approva, chiedi alla persona.
    }

    [Description("Pubblica un annuncio sul canale indicato (simulazione in console).")]
    private string PublishAnnouncement(
        [Description("Canale: bozze oppure evento.")] string channel,
        [Description("Testo completo da pubblicare, copiato esattamente dalla richiesta.")] string message)
    {
        using var span = Telemetry.Source.StartActivity("tool.PublishAnnouncement");
        PublicationCount++;
        DemoConsole.Line($"[TOOL ESEGUITO] Pubblicazione simulata su {channel}: {message}", ConsoleColor.Magenta);
        return "Annuncio pubblicato nella simulazione.";
    }
}
