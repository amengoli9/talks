// D13 Server - travel assistant esposto via AG-UI (HTTP + SSE).
//
// L'agente ha:
//   - get_weather(city)         -> tool backend, eseguito qui
//   - get_attractions(city)     -> tool backend, eseguito qui
//   - book_trip(from,to,date)   -> ApprovalRequiredAIFunction -> HITL
//
// L'agente e' wrappato in ServerFunctionApprovalAgent che traduce le
// approvazioni in/dal protocollo AG-UI. Esposto su /ag-ui via MapAGUI.

using System.ComponentModel;
using System.Diagnostics;
using D13.AgUiBlazor.Server;
using Maf.Demo.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

const string ServiceName = "D13.AgUiBlazor.Server";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// OTel: stesso pattern delle altre demo (chat client + agente strumentati).
DemoConfig cfg = DemoConfig.Load();
var telemetryBundle = Telemetry.ConfigureConsole(ServiceName, cfg);

builder.Services.AddSingleton(telemetryBundle);
builder.Services.AddHttpClient();
builder.Services.AddAGUI();
builder.Services.ConfigureHttpJsonOptions(opt =>
    opt.SerializerOptions.TypeInfoResolverChain.Add(ApprovalJsonContext.Default));

WebApplication app = builder.Build();

// CORS aperto in dev cosi' il Blazor client su altra porta non rompe.
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
    ctx.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
    ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    if (HttpMethods.IsOptions(ctx.Request.Method)) { ctx.Response.StatusCode = 200; return; }
    await next();
});

// --- TOOL ---
[Description("Restituisce il meteo per una citta italiana, come stringa.")]
static string GetWeather([Description("Nome citta italiana")] string city) => city.ToLowerInvariant() switch
{
    "torino" => "Torino: 18 C, sereno.",
    "roma" => "Roma: 26 C, sole.",
    "milano" => "Milano: 21 C, nuvoloso.",
    "firenze" => "Firenze: 24 C, poche nubi.",
    "bologna" => "Bologna: 23 C, sereno.",
    _ => $"{city}: 20 C, sconosciuto.",
};

[Description("Elenca 3 attrazioni turistiche per la citta indicata.")]
static string GetAttractions([Description("Nome citta italiana")] string city) => city.ToLowerInvariant() switch
{
    "torino" => "Mole Antonelliana, Piazza Castello, Museo Egizio",
    "roma" => "Colosseo, Pantheon, Fontana di Trevi",
    "milano" => "Duomo, Galleria Vittorio Emanuele, Castello Sforzesco",
    "firenze" => "Duomo, Ponte Vecchio, Uffizi",
    "bologna" => "Due Torri, Piazza Maggiore, Portici",
    _ => $"Attrazioni di {city}: piazza centrale, museo locale, parco.",
};

[Description("Prenota un viaggio tra due citta in una data specifica.")]
static string BookTrip(
    [Description("Citta di partenza")] string from,
    [Description("Citta di arrivo")] string to,
    [Description("Data in formato ISO yyyy-MM-dd")] string date)
    => $"Prenotazione confermata: {from} -> {to} il {date}. Codice: TRP-{Random.Shared.Next(1000, 9999)}.";

#pragma warning disable MEAI001 // ApprovalRequiredAIFunction e' experimental
AITool[] tools = [
    AIFunctionFactory.Create(GetWeather, name: "get_weather"),
    AIFunctionFactory.Create(GetAttractions, name: "get_attractions"),
    new ApprovalRequiredAIFunction(AIFunctionFactory.Create(BookTrip, name: "book_trip")),
];
#pragma warning restore MEAI001

// Chat client strumentato OTel via Maf.Demo.Shared.
IChatClient chat = ChatClientFactory.Create(ServiceName, cfg);

AIAgent baseAgent = new ChatClientAgent(chat,
    instructions:
        "Sei un assistente di viaggio italiano. Per il meteo usa get_weather, " +
        "per le attrazioni usa get_attractions. Quando vuoi prenotare un viaggio " +
        "usa book_trip — l'utente verra' chiamato ad approvare prima dell'esecuzione.",
    name: "travel-agent",
    tools: tools)
    .WithOpenTelemetry(ServiceName);

// Wrappato per HITL su protocollo AG-UI.
var jsonOptions = app.Services.GetRequiredService<IOptions<JsonOptions>>().Value;
AIAgent agent = new ServerFunctionApprovalAgent(baseAgent, jsonOptions.SerializerOptions);

// Lo span radice copre tutta la run: tool, HITL, LLM, tutto sotto un trace.
using var activitySource = new ActivitySource(ServiceName);
app.Lifetime.ApplicationStopped.Register(() => telemetryBundle.Dispose());

// L'endpoint AG-UI. Da qui in poi e' tutto SSE.
app.MapAGUI("/ag-ui", agent);

app.MapGet("/", () => "D13 AG-UI Server attivo. POST su /ag-ui per parlare con l'agente.");

await app.RunAsync();
