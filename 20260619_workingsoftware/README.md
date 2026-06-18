# 🥙 Piadineria — demo per "Come prevenire il degrado architetturale"

Una sola solution, **`Piadineria.slnx`** (.NET 10), con tutte le demo del talk.
Tema: una **piadineria romagnola**. Dominio in romagnolo (Piada, Farcitura, Cucina, Sala, sconto sagra),
codice tecnico in inglese, messaggi in italiano.

---

## 0. L'idea in una frase (il "perché" di tutto)

Il software **degrada** perché cambia il mondo intorno. Degradare vuol dire **erodere caratteristiche
precise** (le *-ility*: modularità, testability, sicurezza…). Una *-ility* resa **misurabile ed eseguibile**
è una **fitness function**: una valutazione **oggettiva** (numero o pass/fail) dell'integrità di quella
caratteristica, che gira **a ogni commit** e fa **fallire la build** quando qualcuno la viola.

Queste demo rispondono a una domanda: *"come faccio a impedire, in automatico, che l'architettura si
sgretoli un commit alla volta?"*

> **Regola d'oro delle demo:** tutto è **verde** di default. Le violazioni sono righe **commentate**
> marcate con `🔴 DEMO`. Dal vivo le **scommenti** → il controllo diventa **rosso** → poi **ricommenti**.
> Il rosso è il momento clou: fai vedere che la regressione **non passa**.

---

## 1. Cosa c'è dentro (3 demo)

```
Piadineria.slnx
├─ HexagonalPiadineria/    §7a — architettura ESAGONALE (Port & Adapter), 3 progetti + test
├─ DomainPiadineria/       §7a — ISOLAMENTO tra domini (2 cartelle) + test
└─ BeyondXUnit/            §7b — "oltre xUnit": analyzer, mutation testing, CVE, chaos, load test
```

| Demo | Sezione | La -ility che protegge | Lo strumento |
|------|---------|------------------------|--------------|
| HexagonalPiadineria | §7a | **modularità** (il dominio resta puro) | ArchUnitNet |
| DomainPiadineria | §7a | **modularità** (bounded context isolati) | ArchUnitNet |
| BeyondXUnit | §7b | sicurezza, qualità dei test, resilienza, performance | Roslyn / Stryker / NuGet audit / Polly / NBomber + Locust |

---

## 2. Prerequisiti & avvio rapido

```bash
# dalla cartella di questo README
dotnet build Piadineria.slnx          # 0 errori (solo 1 warning NU1903 VOLUTO, vedi §5c)
dotnet test  Piadineria.slnx          # 12 test verdi (5 + 5 + 2)
dotnet tool restore                   # installa Stryker (serve solo per la demo §5b)
```

Serve **.NET 10 SDK**. Niente database o Docker: l'API usa Entity Framework **InMemory**.

---

## 3. Demo §7a/1 — Architettura esagonale (HexagonalPiadineria)

### Cos'è
**Port & Adapter** su 3 progetti. Il **dominio è il centro** dell'esagono e non dipende da nulla;
gli **adapter** stanno fuori e comunicano col centro **solo attraverso porte** (interfacce).

```
        [ WebApp: OrdersController ]      (driving adapter)
                   │  usa OrderService (use case del core)
                   ▼
        [ Domain: modello + porte + use case ]   ← il CORE, non dipende da nessuno
                   ▲
                   │  implementa IOrderRepository (porta)
        [ Infrastructure: OrderRepository + DbContext ]   (driven adapter)
```

- `HexagonalPiadineria.Domain` — `Order` (+ invariante "ordine non vuoto"), `Euro`, le porte `IOrderRepository` e `IKitchenNotifier`, lo use case `OrderService` (valida → salva → manda la comanda al forno).
- `HexagonalPiadineria.Infrastructure` — `PiadineriaDbContext`, `OrderRepository : IOrderRepository`, `ConsoleKitchenNotifier : IKitchenNotifier`.
- `HexagonalPiadineria.WebApp` — `OrdersController` + composition root (`Program.cs`) + OpenAPI/Scalar UI.
- `HexagonalPiadineria.ArchitectureTests` — le 5 regole (fitness function).

### Le 5 regole
| Test | Protegge |
|------|----------|
| `Core_should_not_depend_on_adapters` | il dominio non conosce EF/ASP.NET/adapter |
| `Controller_should_not_depend_on_persistence` | il controller passa dalla porta, non dal DB (è l'erosione del §1) |
| `Concrete_repositories_live_in_infrastructure` | i repository concreti stanno nell'adapter |
| `Ports_should_be_interfaces` | le porte sono contratti: nessuna classe concreta nel namespace `Ports` |
| `Core_should_be_persistence_ignorant` | il core non dipende da `Microsoft.EntityFrameworkCore` |

### Come farla vedere

**(facoltativo) Mostra che l'app funziona davvero:**
```bash
dotnet run --project HexagonalPiadineria/src/HexagonalPiadineria.WebApp --no-launch-profile --urls http://localhost:5099
# UI interattiva (Scalar, sopra Microsoft.AspNetCore.OpenApi):
#   http://localhost:5099/scalar/v1        (doc OpenAPI grezzo: /openapi/v1.json)
# oppure via curl:
curl -X POST http://localhost:5099/orders -H "Content-Type: application/json" \
  -d '{"table":7,"lines":[{"piada":"squacquerone e rucola","price":5.0,"quantity":12}]}'
# → {"table":7,"total":54.000}   (60€, sconto sagra 10% sopra i 50€ → 54€)
curl http://localhost:5099/orders
# → [{"table":7,"total":54.000}]   (elenco ordini, via use case + porta)
```
Esempi pronti anche in `HexagonalPiadineria.WebApp/Piadineria.WebApp.http` (port 5145, profilo di launch).

**La demo vera (verde → rosso → verde):**

1. Mostra i test verdi: `dotnet test Piadineria.slnx`
   Il controller espone già due endpoint **puliti** (passano dallo use case): `POST /orders` e
   `GET /orders` (elenco). Il blocco `🔴 DEMO` aggiunge il dettaglio per tavolo "scavalcando" l'esagono.
2. Apri `HexagonalPiadineria/src/HexagonalPiadineria.WebApp/Controllers/OrdersController.cs`
   e **scommenta** i due blocchi `🔴 DEMO` (l'`using Microsoft.EntityFrameworkCore;` in testa **e**
   il metodo `GetByTable`): è lo scenario realistico *"il GET /orders dà solo la lista, mi serviva il
   dettaglio di un tavolo e l'ho preso al volo dal DbContext"* (`[FromServices]`), invece di esporlo dallo use case.
   ```csharp
   [HttpGet("{table:int}")]
   public async Task<IActionResult> GetByTable(
       int table, [FromServices] HexagonalPiadineria.Infrastructure.PiadineriaDbContext db, CancellationToken ct)
   {
       var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Table == table, ct);
       return order is null ? NotFound() : Ok(new { order.Table, total = order.Total.Amount });
   }
   ```
3. `dotnet test Piadineria.slnx` → **ROSSO**:
   ```
   Non superato  Controller_should_not_depend_on_persistence
   "...should not depend on ... DbContext ... because un controller non accede
    mai direttamente al database..." failed
   ```
   **Da dire:** *"La build compila, è verde, e in Scalar l'endpoint funziona davvero. Ma il
   controller ora parla col database: ho 'bucato' l'esagono. La fitness function lo cattura — e la
   motivazione è scritta nell'errore. In CI questa PR non passa."*
4. **Ricommenta** i due blocchi → torna verde.

---

## 4. Demo §7a/2 — Isolamento tra domini (DomainPiadineria)

### Cos'è
Un **modular monolith**: un solo progetto di dominio diviso in **due cartelle** (bounded context)
che **non si devono conoscere**.

```
DomainPiadineria.Domain
├─ Cucina/   → Piada (Farcitura, prezzo, Vegetariana), Comanda (stato InCoda→InCottura→Pronta)
└─ Sala/     → Order, OrderLine (snapshot della piada)   — conti, tavoli, sconto sagra

Cucina ⟂ Sala : niente dipendenze dirette, in nessuna direzione
```

### Le 5 regole
| Test | Protegge |
|------|----------|
| `Cucina_should_not_depend_on_Sala` | la Cucina non sa nulla di tavoli e conti |
| `Sala_should_not_depend_on_Cucina` | la Sala usa uno *snapshot* della piada, non l'entità della Cucina |
| `Domains_should_be_free_of_cycles` | nessun ciclo fra i domini |
| `Domains_should_be_persistence_ignorant` | i domini sono puri: nessuna dipendenza da `Microsoft.EntityFrameworkCore` |
| `Domain_aggregates_should_be_sealed` | gli aggregati non sono pensati per l'ereditarietà |

### Come farla vedere

1. Test verdi: `dotnet test Piadineria.slnx`
2. Apri `DomainPiadineria/src/DomainPiadineria.Domain/Sala/Order.cs` e **scommenta** il `using` in testa
   **e** il metodo `AddFromKitchen` (entrambi marcati `🔴 DEMO`):
   ```csharp
   using DomainPiadineria.Domain.Cucina;
   // ...
   public void AddFromKitchen(Piada piada, int quantity) =>
       Add(new OrderLine(piada.Farcitura.ToString(), piada.Prezzo, quantity));
   ```
3. `dotnet test Piadineria.slnx` → **ROSSO** su `Sala_should_not_depend_on_Cucina`.
   **Da dire:** *"Ho fatto una cosa che sembra comoda: invece di mappare la piada in uno snapshot, la Sala
   costruisce la riga prendendo direttamente l'entità `Piada` della Cucina. Così la Sala dipende dalla
   Cucina e i due domini non sono più isolati. Questa regola tiene separati i moduli senza microservizi."*
4. **Ricommenta** → verde.

---

## 5. Demo §7b — Oltre xUnit (BeyondXUnit)

**Il messaggio:** una fitness function è un **concetto**, non una libreria. Non è per forza un test xUnit.
Cinque esempi eseguibili su strumenti diversi.

### 5a. Roslyn analyzer — la fitness function nel COMPILATORE 🔴 (l'esempio in evidenza)

`Piadineria.Analyzers` è un analyzer (`ARCH001`) che applica la stessa regola del §1, ma a **tempo di
compilazione**: feedback **istantaneo nell'IDE** (squiggle rossa) e **build rotta**, prima ancora della CI.
`Piadineria.Analyzers.Sample` è la cavia.

```bash
# apri Piadineria.Analyzers.Sample/SampleController.cs e scommenta:
#     private readonly MenuDbContext _db = null!;
dotnet build BeyondXUnit/Piadineria.Analyzers.Sample
# → error ARCH001: Il controller 'SampleController' espone un DbContext: viola il layering dell'esagono
```
**Da dire:** *"Stessa regola di prima, ma non serve nemmeno far girare i test: l'errore esce dal compilatore,
e nell'IDE lo vedo sottolineato mentre scrivo. Lo distribuisco come pacchetto NuGet a tutto il team."*
(poi ricommenta)

### 5b. Mutation testing (Stryker) — "verde ≠ coperto"

`Piadineria.Pricing` ha la logica dello sconto sagra; `Piadineria.Pricing.Tests` ha test **verdi** ma con
un **oracolo debole** (verifica che *ci sia* uno sconto, non *quanto*). Stryker muta il codice e verifica
se i test se ne accorgono.

```bash
dotnet tool restore
cd BeyondXUnit/Piadineria.Pricing.Tests
dotnet stryker --project Piadineria.Pricing.csproj
# → mutation score ~65-75%: alcuni mutanti SOPRAVVIVONO
```
**Da dire:** *"I test sono verdi, ma Stryker cambia un `>` in `>=` o lo 0.10 in 0.15 e nessun test fallisce:
quei mutanti sopravvivono. Verde non vuol dire coperto. È una fitness function che misura la qualità... dei test."*

### 5c. Sicurezza — CVE nelle dipendenze

`Piadineria.Vulnerable` referenzia di proposito `Newtonsoft.Json 12.0.3`, con una CVE nota.

```bash
dotnet list BeyondXUnit/Piadineria.Vulnerable package --vulnerable
# → Newtonsoft.Json   12.0.3   High   GHSA-5crp-9r3c-p9vr
```
**Da dire:** *"Una riga in pipeline e nessuna dipendenza con CVE nota entra in produzione. È il warning
`NU1903` che vedete anche durante la build."*

### 5d. Chaos engineering (Polly v8 + Simmy)

`Piadineria.Chaos` inietta guasti nel "forno" nel 50% dei casi; un retry deve recuperare.

```bash
dotnet run --project BeyondXUnit/Piadineria.Chaos
# → il forno "fa i capricci", il retry recupera, tutte le piade escono
```
**Da dire:** *"Qui la fitness function gira in continuo e in modo dinamico: rompo apposta per verificare
che la resilienza regga. È lo spirito del Chaos Monkey di Netflix."*

### 5e. Performance come fitness function (NBomber .NET + Locust Python)

La *-ility* protetta qui è la **performance/scalabilità**. Mettiamo sotto **carico** l'endpoint reale
`POST /orders` dell'esagono e verifichiamo una **SLO**: *95° percentile sotto i 100 ms e zero errori*.

Due strumenti, **un solo punto di degrado**: la riga `🔴 DEMO` in
`HexagonalPiadineria/src/HexagonalPiadineria.Domain/OrderService.cs` (`await Task.Delay(300, ct);`).
Scommentandola la p95 sfora la soglia ed **entrambe** le fitness function diventano rosse → *.NET e Python
catturano la stessa regressione*.

**NBomber (.NET) — auto-contenuto, niente server da lanciare** (`Piadineria.Load`): avvia la WebApp
**in-memory** con `WebApplicationFactory` e la martella via `NBomber.Http`.
```bash
dotnet run --project BeyondXUnit/Piadineria.Load
# → tabella NBomber + report HTML in BeyondXUnit/Piadineria.Load/reports
#   ✅ VERDE: p95 ben sotto i 100 ms (exit code 0)
```

**Locust (Python) — contro la WebApp in esecuzione** (`Piadineria.Load.Locust`):
```bash
# 1) avvia la WebApp
dotnet run --project HexagonalPiadineria/src/HexagonalPiadineria.WebApp --no-launch-profile --urls http://localhost:5099
# 2) in un altro terminale
pip install -r BeyondXUnit/Piadineria.Load.Locust/requirements.txt
locust -f BeyondXUnit/Piadineria.Load.Locust/locustfile.py --headless -u 50 -r 50 -t 10s --host http://localhost:5099
# → ✅ VERDE / 🔴 ROSSO con exit code 0/1 (hook events.quitting sulla stessa SLO)
```

**La demo (verde → rosso → verde):** lancia uno dei due → **verde**. Scommenta la riga `🔴 DEMO` in
`OrderService.PlaceOrder` (per Locust riavvia la WebApp) → **rosso**, p95 oltre la soglia, exit code 1.
Ricommenta → verde.
**Da dire:** *"La build compila e i test unitari sono verdi: nessuno se ne accorgerebbe. Ma sotto carico la
p95 è triplicata. Questa fitness function gira in pipeline e fa fallire la PR sulla performance — e la stessa
SLO la verifico in .NET con NBomber e in Python con Locust."*

---

## 6. Mappa demo → i 5 assi (dal libro *Building Evolutionary Architectures*)

| Demo | Scope | Cadence | Result | Invocation | Proactivity |
|------|-------|---------|--------|------------|-------------|
| ArchUnitNet (esagono) | Atomic | Triggered | Static | Automated | Intentional |
| ArchUnitNet (domini) | Atomic | Triggered | Static | Automated | Intentional |
| Roslyn analyzer | Atomic | Triggered (compile) | Static | Automated | Intentional |
| Stryker | Atomic | Triggered | Static | Automated | Intentional |
| `--vulnerable` | Atomic | Triggered/Temporal | Static | Automated | Intentional |
| Chaos (Simmy) | Holistic | Continual | Dynamic | Automated | Emergent |
| NBomber / Locust (load) | Holistic | Triggered | Dynamic | Automated | Intentional |

Altri esempi del §7b restano **concettuali** (SLO/monitoring in produzione, contract testing, policy-as-code,
review manuali): non serve un progetto per spiegarli.

---

## 7. Scaletta rapida (cheat-sheet da palco)

1. `dotnet test Piadineria.slnx` → **12 verdi**. *"Tutto a posto. Ora rompo l'architettura."*
2. **Esagono:** scommenta `using` EF + il blocco dei due `[HttpGet]` nel controller → `dotnet test` → **rosso** → ricommenta.
3. **Domini:** scommenta `using` + `AddFromKitchen` in `Sala/Order.cs` → `dotnet test` → **rosso** → ricommenta.
4. **Analyzer:** scommenta `_db` nel `SampleController` → `dotnet build …Analyzers.Sample` → **ARCH001** → ricommenta.
5. **Stryker:** `dotnet stryker` → mutanti sopravvissuti.
6. **CVE:** `dotnet list … --vulnerable` → Newtonsoft High.
7. **Chaos:** `dotnet run --project …Chaos` → il forno regge.
8. **Performance:** `dotnet run --project …Piadineria.Load` (NBomber) → verde; scommenta `Task.Delay` in `OrderService` → **rosso** (p95 oltre SLO) → ricommenta. *(Variante Python: Locust contro la WebApp viva.)*
9. Chiudi: *"Una fitness function alla volta, a partire da domani."*

---

## 8. Versioni (risolte dall'SDK .NET 10)

ArchUnitNET.xUnit `0.13.3` · EF Core `10.0.9` · Microsoft.AspNetCore.OpenApi `10.0.4` ·
Scalar.AspNetCore `2.16.4` · Microsoft.CodeAnalysis.CSharp `5.3.0` ·
Polly.Core `8.7.0` · Newtonsoft.Json `12.0.3` (volutamente vulnerabile) · Stryker `4.14.2` ·
NBomber `6.4.1` · NBomber.Http `6.2.0` · Microsoft.AspNetCore.Mvc.Testing `10.0.9` · Locust `≥2.32` (Python).
