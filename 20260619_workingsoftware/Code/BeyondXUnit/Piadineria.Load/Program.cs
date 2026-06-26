using System.Text;
using HexagonalPiadineria.WebApp;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using NBomber.CSharp;
using NBomber.Http.CSharp;

// ─────────────────────────────────────────────────────────────────────────────
// FITNESS FUNCTION di PERFORMANCE (§5e) — NBomber + WebApplicationFactory.
//
// Mettiamo sotto carico l'endpoint REALE POST /orders della WebApp esagonale,
// avviata IN-MEMORY (niente server da lanciare a mano, niente porte). Poi
// verifichiamo una SLO: 95° percentile sotto soglia E zero errori.
//
// Verde di default. Scommenta la riga 🔴 DEMO in OrderService.PlaceOrder
// (await Task.Delay) → la p95 sfora la soglia → questa fitness function diventa
// ROSSA e il processo esce con exit code 1 (in CI la build non passa).
// ─────────────────────────────────────────────────────────────────────────────

const int sloP95Ms = 100;   // SLO: p90 < 200 ms

// Ogni ordine è identificato dal tavolo (chiave). Usiamo un tavolo diverso a ogni
// richiesta — altrimenti l'inserimento collide sulla chiave già presente.
var nextTable = 0;

// Avvia la WebApp in-memory. WebAppMarker individua l'assembly della WebApp
// senza scontrarsi col Program (top-level) di questo runner.
using var factory = new PiadineriaWebApp();
using var client = factory.CreateClient();

var scenario = Scenario.Create("place_order", async _ =>
{
    var table = Interlocked.Increment(ref nextTable);
    var orderJson =
        $$"""{"table":{{table}},"lines":[{"piada":"squacquerone e rucola","price":5.0,"quantity":3}]}""";

    var request = Http.CreateRequest("POST", "/orders")
        .WithHeader("Accept", "application/json")
        .WithBody(new StringContent(orderJson, Encoding.UTF8, "application/json"));

    return await Http.Send(client, request);
})
.WithWarmUpDuration(TimeSpan.FromSeconds(3))               // lascia "scaldare" JIT + EF
.WithLoadSimulations(
    Simulation.Inject(rate: 100,                           // 100 ordini/sec...
                      interval: TimeSpan.FromSeconds(1),
                      during: TimeSpan.FromSeconds(8)));    // ...per 8 secondi

var stats = NBomberRunner
    .RegisterScenarios(scenario)
    .WithReportFolder("reports")
    .Run();

// ── La fitness function vera e propria: valuta la SLO sulle statistiche ──
var sc = stats.ScenarioStats[0];
var p95 = sc.Ok.Latency.Percent95;     // millisecondi
var failures = sc.Fail.Request.Count;

Console.WriteLine();
Console.WriteLine($"SLO performance:  p95 < {sloP95Ms} ms  e  0 errori");
Console.WriteLine($"Misurato:         p95 = {p95:F1} ms,  errori = {failures}");

if (p95 <= sloP95Ms && failures == 0)
{
    Console.WriteLine("✅ VERDE: la piadineria regge il carico entro la SLO.");
    return 0;
}

Console.WriteLine("🔴 ROSSO: SLO violata — performance degradata. In CI questa build NON passa.");
return 1;

// Fissa esplicitamente la content root: l'endpoint POST /orders non usa file statici,
// così evitiamo l'euristica di WebApplicationFactory (che cercherebbe la WebApp in un
// percorso sbagliato, non avendo i sorgenti nella stessa cartella del runner).
file sealed class PiadineriaWebApp : WebApplicationFactory<WebAppMarker>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        return base.CreateHost(builder);
    }
}
