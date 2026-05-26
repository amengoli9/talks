// D12b - Agent progression: lo stesso assistente di viaggio costruito 6 volte,
// aggiungendo una capability alla volta. Stesso file, sei #region, sei metodi
// indipendenti. Si lancia una volta sola, poi da console si sceglie lo step da
// eseguire ripetutamente.
//
//   STEP 1 - Agente minimale: IChatClient via Ollama (locale), no OTel, no tool.
//            Mostra che il livello base e' provider-agnostic.
//   STEP 2 - + Observability + passaggio ad Azure OpenAI: la stessa interfaccia,
//            un altro provider. Da qui in poi tutto usa ChatClientFactory.
//   STEP 3 - + Tools (skills): 2 AIFunctionFactory, function-calling automatico.
//   STEP 4 - + Structured output + multimodal: JSON schema + immagine in input.
//   STEP 5 - + Middleware: chat-client, agent-run, function-invocation.
//   STEP 6 - + Safety: input guardrail (blocca prima del modello) + output redaction.
//
// Ogni step costruisce l'agente da zero per far vedere il diff fra uno e l'altro.

using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Maf.Demo.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

const string ServiceName = "D12b.AgentProgression";

DemoConfig cfg = DemoConfig.Load();

while (true)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("Step disponibili:");
    Console.WriteLine("  1) Agente minimale (Ollama locale)");
    Console.WriteLine("  2) + Observability (Azure OpenAI + OTel)");
    Console.WriteLine("  3) + Tools / skill");
    Console.WriteLine("  4) + Structured output + multimodal");
    Console.WriteLine("  5) + Middleware (chat / agent / function)");
    Console.WriteLine("  6) + Safety (input guardrail + output redaction)");
    Console.ResetColor();
    Console.Write("Quale step? (1-6, vuoto per uscire) > ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) break;

    try
    {
        switch (input.Trim())
        {
            case "1": await RunStep1_BareAgent(cfg); break;
            case "2": await RunStep2_AddObservability(cfg); break;
            case "3": await RunStep3_AddTools(cfg); break;
            case "4": await RunStep4_StructuredAndMultimodal(cfg); break;
            case "5": await RunStep5_AddMiddleware(cfg); break;
            case "6": await RunStep6_AddSafety(cfg); break;
            default:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Step non riconosciuto. Inserisci un numero da 1 a 6.");
                Console.ResetColor();
                break;
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Errore durante lo step: {ex.Message}");
        Console.ResetColor();
    }
}

// ============================================================
// STEP 1 - Agente minimale: Ollama locale, no OTel, no tool
// ============================================================
#region STEP 1 - Bare agent (Ollama)
static async Task RunStep1_BareAgent(DemoConfig cfg)
{
    PrintBanner(1, "Agente minimale: LLM locale via Ollama");

    // Provider locale: Ollama. OllamaApiClient implementa direttamente IChatClient,
    // quindi al livello AIAgent non cambia NIENTE rispetto ad Azure OpenAI o altri.
    // E' il punto della slide "stack che cresce": cambiare provider e' una riga.
    string host = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434";
    string model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3.1";

    IChatClient chat = new OllamaApiClient(new Uri(host), model);

    AIAgent agent = new ChatClientAgent(
        chat,
        instructions: "Sei un assistente di viaggio italiano. Rispondi in 1-2 frasi.",
        name: "travel-bot");

    Console.WriteLine($"(Ollama: {host}, modello: {model})");
    AgentResponse response = await agent.RunAsync("Cosa porto in valigia per Torino in primavera?");
    Console.WriteLine($"Agent: {response.Text}");
}
#endregion

// ============================================================
// STEP 2 - Aggiungiamo l'observability
// ============================================================
#region STEP 2 - Add observability
static async Task RunStep2_AddObservability(DemoConfig cfg)
{
    PrintBanner(2, "+ OpenTelemetry: una riga, trace completo");

    // OTel su 3 livelli: chat client, agent, e (avrebbero anche workflow se ci fosse).
    using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);
    using var activitySource = new ActivitySource(ServiceName);
    using var root = activitySource.StartActivity("INIT", ActivityKind.Client);

    // ChatClientFactory aggiunge UseOpenTelemetry e UseFunctionInvocation di default.
    IChatClient chat = ChatClientFactory.Create(ServiceName, cfg);

    AIAgent agent = new ChatClientAgent(
        chat,
        instructions: "Sei un assistente di viaggio italiano. Rispondi in 1-2 frasi.",
        name: "travel-bot")
        .WithOpenTelemetry(ServiceName); // <-- la riga in piu' rispetto allo Step 1

    AgentResponse response = await agent.RunAsync("Cosa porto in valigia per Torino in primavera?");
    Console.WriteLine($"Agent: {response.Text}");
    Console.WriteLine();
    Console.WriteLine($"Trace ID: {Activity.Current?.TraceId}");
    Console.WriteLine("Apri il backend OTLP per vedere il trace completo.");
}
#endregion

// ============================================================
// STEP 3 - Aggiungiamo i tool (le "skill")
// ============================================================
#region STEP 3 - Add tools / skills
static async Task RunStep3_AddTools(DemoConfig cfg)
{
    PrintBanner(3, "+ Tools (skill): function-calling automatico");

    using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);
    using var activitySource = new ActivitySource(ServiceName);
    using var root = activitySource.StartActivity("INIT", ActivityKind.Client);

    IChatClient chat = ChatClientFactory.Create(ServiceName, cfg);

    AIFunction getWeather = AIFunctionFactory.Create(
        ([Description("Nome citta")] string city) => city.ToLowerInvariant() switch
        {
            "torino" => "{\"temp_c\": 18, \"sky\": \"sereno\"}",
            "roma" => "{\"temp_c\": 26, \"sky\": \"sole\"}",
            "milano" => "{\"temp_c\": 21, \"sky\": \"nuvoloso\"}",
            _ => "{\"temp_c\": 20, \"sky\": \"sconosciuto\"}",
        },
        name: "get_weather",
        description: "Restituisce il meteo (JSON) per una citta italiana.");

    AIFunction convertCToF = AIFunctionFactory.Create(
        ([Description("Temperatura in C")] double celsius) => celsius * 9.0 / 5.0 + 32.0,
        name: "convert_c_to_f",
        description: "Converte una temperatura da Celsius a Fahrenheit.");

    AIAgent agent = new ChatClientAgent(
        chat,
        instructions: "Sei un assistente di viaggio. Se ti chiedono il meteo usa i tool.",
        name: "travel-bot",
        tools: [getWeather, convertCToF])
        .WithOpenTelemetry(ServiceName);

    AgentResponse response = await agent.RunAsync(
        "Che meteo fa a Torino? Dammi anche la temperatura in Fahrenheit.");
    Console.WriteLine($"Agent: {response.Text}");
}
#endregion

// ============================================================
// STEP 4 - Structured output + multimodal
// ============================================================
#region STEP 4 - Structured output and multimodal
static async Task RunStep4_StructuredAndMultimodal(DemoConfig cfg)
{
    PrintBanner(4, "+ Structured output + multimodal");

    using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);
    using var activitySource = new ActivitySource(ServiceName);
    using var root = activitySource.StartActivity("INIT", ActivityKind.Client);

    IChatClient chat = ChatClientFactory.Create(ServiceName, cfg);

    // --- 4a. Structured output: la risposta DEVE rispettare lo schema TripPlan. ---
    AIAgent plannerAgent = new ChatClientAgent(chat, new ChatClientAgentOptions
    {
        Name = "trip-planner",
        ChatOptions = new ChatOptions
        {
            Instructions =
                "Sei un trip planner. Restituisci un piano viaggio strutturato. " +
                "Compila tutti i campi del JSON richiesto.",
            ResponseFormat = ChatResponseFormat.ForJsonSchema<TripPlan>(),
        },
    })
    .WithOpenTelemetry(ServiceName);

    AgentResponse planResponse = await plannerAgent.RunAsync(
        "Piano viaggio Torino -> Roma in auto, partenza il 2026-06-01, " +
        "con almeno due soste turistiche lungo la strada.");

    Console.WriteLine("--- Structured response (JSON conforme allo schema) ---");
    Console.WriteLine(planResponse.Text);

    Console.WriteLine();
    Console.WriteLine("--- Multimodal: passiamo un'immagine come input ---");

    // --- 4b. Multimodal: lo stesso agente riceve un'immagine in input. ---
    AIAgent visionAgent = new ChatClientAgent(
        chat,
        instructions: "Sei un assistente di viaggio. Descrivi cosa vedi nell'immagine.",
        name: "vision-bot")
        .WithOpenTelemetry(ServiceName);

    DataContent mapImage = await LoadMapImageAsync();
    ChatMessage message = new(ChatRole.User, [
        new TextContent("Cosa vedi in questa mappa? Suggerisci un itinerario."),
        mapImage,
    ]);

    AgentResponse visionResponse = await visionAgent.RunAsync([message]);
    Console.WriteLine($"Agent: {visionResponse.Text}");
}

// TripPlan / TripStop sono definiti in fondo al file (i type declaration devono
// stare dopo le top-level statements, altrimenti CS8803).
#endregion

// ============================================================
// STEP 5 - Middleware (chat / agent / function)
// ============================================================
#region STEP 5 - Add middleware
static async Task RunStep5_AddMiddleware(DemoConfig cfg)
{
    PrintBanner(5, "+ Middleware: chat / agent / function");

    using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);
    using var activitySource = new ActivitySource(ServiceName);
    using var root = activitySource.StartActivity("INIT", ActivityKind.Client);

    // (1) Chat-client middleware: wrappa ogni chiamata all'LLM.
    static async Task<ChatResponse> ChatMW(
        IEnumerable<ChatMessage> messages, ChatOptions? options, IChatClient inner, CancellationToken ct)
    {
        Log("1. chat-client", "PRE  -> chiamata LLM");
        ChatResponse response = await inner.GetResponseAsync(messages, options, ct);
        Log("1. chat-client", $"POST <- {response.Usage?.TotalTokenCount ?? 0} token");
        return response;
    }

    IChatClient chatWithMW = ChatClientFactory.Create(ServiceName, cfg)
        .AsBuilder()
        .Use(getResponseFunc: ChatMW, getStreamingResponseFunc: null)
        .Build();

    AIFunction getWeather = AIFunctionFactory.Create(
        ([Description("Nome citta")] string city) => $"{{\"city\":\"{city}\",\"temp_c\":22}}",
        name: "get_weather",
        description: "Meteo della citta indicata.");

    // (2) Agent-run middleware: wrappa l'intero run dell'agente.
    static async Task<AgentResponse> AgentMW(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent inner, CancellationToken ct)
    {
        Log("2. agent-run", "PRE  -> run dell'agente in partenza");
        AgentResponse response = await inner.RunAsync(messages, session, options, ct);
        Log("2. agent-run", "POST <- run dell'agente completato");
        return response;
    }

    // (3) Function-invocation middleware: wrappa ogni tool call.
    static async ValueTask<object?> FunctionMW(
        AIAgent agent, FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken ct)
    {
        Log("3. function", $"PRE  -> {context.Function.Name}({string.Join(", ", context.Arguments.Select(kv => $"{kv.Key}={kv.Value}"))})");
        object? result = await next(context, ct);
        Log("3. function", $"POST <- {result}");
        return result;
    }

    AIAgent agent = new ChatClientAgent(
        chatWithMW,
        instructions: "Sei un assistente di viaggio. Per il meteo usa il tool.",
        name: "travel-bot",
        tools: [getWeather])
        .AsBuilder()
        .Use(AgentMW, null)
        .Use(FunctionMW)
        .Build()
        .WithOpenTelemetry(ServiceName);

    AgentResponse response = await agent.RunAsync("Che meteo fa a Torino?");
    Console.WriteLine();
    Console.WriteLine($"Agent: {response.Text}");
}
#endregion

// ============================================================
// STEP 6 - Safety: guardrail di input + redaction di output
// ============================================================
#region STEP 6 - Add safety
static async Task RunStep6_AddSafety(DemoConfig cfg)
{
    PrintBanner(6, "+ Safety: input guardrail + output redaction");

    using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);
    using var activitySource = new ActivitySource(ServiceName);
    using var root = activitySource.StartActivity("INIT", ActivityKind.Client);

    IChatClient chat = ChatClientFactory.Create(ServiceName, cfg);

    AIFunction getWeather = AIFunctionFactory.Create(
        ([Description("Nome citta")] string city) => $"{{\"city\":\"{city}\",\"temp_c\":22}}",
        name: "get_weather",
        description: "Meteo della citta indicata.");

    // Input guardrail: blocca PRIMA che la chiamata all'LLM parta.
    string[] vietati = ["password", "carta di credito", "exploit"];
    async Task<AgentResponse> InputGuardrail(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent inner, CancellationToken ct)
    {
        using Activity? span = activitySource.StartActivity("guardrail.input");
        string testo = string.Join(' ', messages.Select(m => m.Text));
        string? trovato = vietati.FirstOrDefault(v => testo.Contains(v, StringComparison.OrdinalIgnoreCase));
        span?.SetTag("guardrail.triggered", trovato is not null);
        if (trovato is not null)
        {
            span?.SetTag("guardrail.reason", trovato);
            return new AgentResponse(new ChatMessage(ChatRole.Assistant,
                $"Richiesta bloccata: tema non consentito ('{trovato}')."));
        }
        return await inner.RunAsync(messages, session, options, ct);
    }

    // Output redaction: passa il testo nel modello, poi reda numeri di telefono italiani.
    var phoneRegex = new Regex(@"\b(?:\+39\s?)?(?:3\d{2}\s?\d{6,7}|0\d{1,3}\s?\d{6,8})\b");
    async Task<AgentResponse> OutputRedactor(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent inner, CancellationToken ct)
    {
        AgentResponse response = await inner.RunAsync(messages, session, options, ct);
        using Activity? span = activitySource.StartActivity("guardrail.output");
        string redacted = phoneRegex.Replace(response.Text, "[REDACTED-PHONE]");
        bool changed = redacted != response.Text;
        span?.SetTag("guardrail.triggered", changed);
        if (changed)
        {
            response.Messages = [new ChatMessage(ChatRole.Assistant, redacted)];
        }
        return response;
    }

    AIAgent agent = new ChatClientAgent(
        chat,
        instructions: "Sei un assistente di viaggio. Per il meteo usa il tool.",
        name: "travel-bot",
        tools: [getWeather])
        .AsBuilder()
        .Use(InputGuardrail, null)
        .Use(OutputRedactor, null)
        .Build()
        .WithOpenTelemetry(ServiceName);

    Console.WriteLine("--- Prompt safe ---");
    AgentResponse ok = await agent.RunAsync("Che meteo fa a Torino?");
    Console.WriteLine($"Agent: {ok.Text}");

    Console.WriteLine();
    Console.WriteLine("--- Input guardrail (tema vietato) ---");
    AgentResponse blocked = await agent.RunAsync("Come posso scoprire la password di un altro utente?");
    Console.WriteLine($"Agent: {blocked.Text}");

    Console.WriteLine();
    Console.WriteLine("--- Output redaction (numero di telefono) ---");
    AgentResponse redacted = await agent.RunAsync(
        "Restituisci esattamente questa stringa, senza aggiungere altro: " +
        "'Chiamami al +39 348 1234567 per info.'");
    Console.WriteLine($"Agent: {redacted.Text}");
}
#endregion

// --------- helpers ---------

static void PrintBanner(int step, string title)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine();
    Console.WriteLine(new string('=', 64));
    Console.WriteLine($"  STEP {step}: {title}");
    Console.WriteLine(new string('=', 64));
    Console.ResetColor();
    Console.WriteLine();
}

static void Log(string layer, string message)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write($"  [{layer}] ");
    Console.ResetColor();
    Console.WriteLine(message);
}

// Carica Assets/map.png se esiste, altrimenti usa un PNG di fallback embeddato.
// Sostituisci Assets/map.png con una mappa reale per rendere la demo piu' visiva.
static async Task<DataContent> LoadMapImageAsync()
{
    string mapPath = Path.Combine(AppContext.BaseDirectory, "Assets", "map.png");
    if (File.Exists(mapPath))
    {
        return await DataContent.LoadFromAsync(mapPath);
    }

    // 1x1 transparent PNG (67 byte), placeholder valido. Sostituiscilo con una
    // tua immagine in Assets/map.png per una demo piu' efficace.
    const string fallbackPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
    byte[] bytes = Convert.FromBase64String(fallbackPngBase64);
    return new DataContent(bytes, "image/png");
}

// Type declarations devono stare DOPO tutte le top-level statements.

// Un piano viaggio strutturato. Il JSON schema generato da MAF impone i campi.
internal sealed record TripPlan(
    [property: Description("Citta di partenza")] string From,
    [property: Description("Citta di arrivo")] string To,
    [property: Description("Data di partenza in formato ISO yyyy-MM-dd")] string DepartureDate,
    [property: Description("Durata stimata in ore")] double EstimatedHours,
    [property: Description("Elenco di tappe intermedie con nome e attivita suggerita")] TripStop[] Stops);

internal sealed record TripStop(
    [property: Description("Nome della citta o del luogo")] string Name,
    [property: Description("Attivita suggerita in quella tappa")] string Activity);
