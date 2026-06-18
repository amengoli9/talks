# Come prevenire il degrado architetturale

> **Traccia + materiale di studio** — talk tecnico, 60 minuti
> **Spina dorsale:** *il degrado è erosione di caratteristiche specifiche (le -ility). Prevenirlo = proteggere quelle caratteristiche. Una -ility resa misurabile ed eseguibile è una fitness function.*

---

## Agenda & timing (60')

| # | Sezione | Tempo | Durata | Obiettivo |
|---|---------|-------|--------|-----------|
| 1 | Un problema che ho avuto | 00:00 → 00:04 | 4' | Aprire con una scena vera, far provare il dolore |
| 2 | Bit rot | 00:04 → 00:10 | 6' | Spiegare *perché* degrada (drift vs erosion) |
| 3 | Il ponte | 00:10 → 00:12 | 2' | Il bit rot erode *qualità*, non "il codice" |
| 4 | Le -ility | 00:12 → 00:18 | 6' | Cosa proteggere, trade-off, "misurabile o è un desiderio" |
| 5 | Cornice: l'architettura evolve | 00:18 → 00:19 | 1' | Evolutionary Architecture in una frase |
| 6 | Fitness function + i 5 assi | 00:19 → 00:32 | 13' | Lo strumento centrale |
| 7a | Demo .NET — ArchUnitNet | 00:32 → 00:42 | 10' | Mostrarlo nel codice |
| 7b | Oltre xUnit | 00:42 → 00:48 | 6' | Non sono solo test |
| 7c | Call to action | 00:48 → 00:52 | 4' | Cosa fare lunedì |

Restano ~8' per intro, transizioni e Q&A.

**Filo conduttore (una frase):** *il software degrada perché cambia il mondo intorno; non possiamo congelarlo, ma possiamo decidere quali qualità proteggere e renderle misurabili, così che le regressioni siano impossibili da ignorare — ad ogni commit.*

---

## 1. Un problema che ho avuto (4')

**// SUL PALCO** — apri con una **storia vera**, in prima persona. Una scena concreta vale più di mille definizioni. Adatta il template qui sotto al tuo aneddoto reale (rendilo riconoscibile: tutti ci sono passati).

### Template della war story

> *"Su un progetto X, una modifica che doveva durare due giorni ne ha richiesti dieci. Non per il dominio: per l'architettura. Toccare una cosa ne rompeva tre. C'era un modulo che nessuno voleva aprire. E quando finalmente abbiamo guardato dentro, abbiamo trovato un `Controller` che parlava direttamente al database — esattamente l'opposto di ciò che il diagramma sul wiki diceva. Nessuno l'aveva deciso. Era successo e basta, un commit alla volta."*

### L'artefatto da mostrare a video

```csharp
// ❌ Il "controller che fa tutto": violazione di layer + business logic + SQL injection in omaggio
public class OrdersController : ControllerBase
{
    [HttpPost]
    public IActionResult Create(OrderDto dto)
    {
        using var conn = new SqlConnection("Server=prod-db;Database=Sales;User Id=sa;Password=...");
        conn.Open();

        // 1) business logic annegata nel controller
        if (dto.Total > 1000)
            dto.Discount = dto.Total * 0.1m;

        // 2) il presentation layer parla direttamente al DB...
        // 3) ...con SQL costruito per concatenazione (injection)
        var cmd = new SqlCommand(
            $"INSERT INTO Orders (Customer, Total) VALUES ('{dto.Customer}', {dto.Total})", conn);
        cmd.ExecuteNonQuery();

        return Ok();
    }
}
```

**// SUL PALCO** — "Questo passa la code review quando si ha fretta. Compila, è verde, funziona. Eppure ha già rotto tre principi. Il punto non è il singolo errore: è che **nessuno se n'è accorto**, e non era nei piani di nessuno. Così degrada un'architettura."

**Takeaway §1:** il degrado non è una decisione, è un'**accumulazione**. Invisibile finché non diventa costoso.

---

## 2. Bit rot — perché il software degrada (6')

### Definizione

**Bit rot** (anche *software rot*, *code rot*, *software erosion*, *software entropy*, *software decay*): il **deterioramento graduale della qualità del software nel tempo**, che lo rende progressivamente più fragile e più costoso da modificare.

**Frase da dire ad alta voce:** *i bit non marciscono fisicamente.* Il codice fermo su disco è identico a ieri. **A cambiare è il contesto** — requisiti, dipendenze, runtime, sistema operativo, team, conoscenza condivisa — e ogni modifica aggiunge entropia. Il software non degrada in assoluto: degrada **relativamente** a un mondo che si muove.

### Le radici (cita 1–2, veloce)

- **Leggi di Lehman** sull'evoluzione del software:
  - *Continuing Change*: un sistema in uso deve essere **continuamente adattato**, o diventa progressivamente meno soddisfacente.
  - *Increasing Complexity*: man mano che evolve, la sua **complessità cresce**, a meno di lavorare attivamente per ridurla.
- **Broken Windows** (*The Pragmatic Programmer*): una "finestra rotta" (un hack lasciato lì) segnala che è accettabile romperne altre. Il degrado accelera.
- **Big Ball of Mud** (Foote & Yoder, 1997): l'architettura *de facto* più diffusa al mondo. È la **gravità** verso cui ogni sistema cade senza una forza contraria.
- **Technical debt** (Ward Cunningham): il debito è ok se *consapevole* e *ripagato*; è letale se *invisibile* e *capitalizzato*.

### Drift vs erosion (CONCETTO CHIAVE — tienilo)

Perry & Wolf (1992) distinguono due modi in cui l'architettura *intended* si stacca da quella reale:

- **Architectural drift** (deriva): decisioni **non previste** dall'architettura, che però **non la violano**. Erosione di chiarezza e coerenza (es.: tre modi diversi di fare la stessa cosa). Subdola: tecnicamente non stai violando nulla.
- **Architectural erosion** (erosione): decisioni che **violano** l'architettura prevista (es.: il `Controller` che chiama il DB del §1).

**// SUL PALCO** — "Il drift è come un giardino: non serve un vandalo, basta non fare niente. L'erosione invece è una violazione netta — e quella, vedremo, possiamo **catturarla automaticamente**."

**Takeaway §2:** il degrado è **inevitabile per natura** (il mondo cambia) ma **governabile**. Resta una domanda: *cosa, esattamente, stiamo lasciando degradare?*

---

## 3. Il ponte (2')

**// SUL PALCO** — questa è la **cucitura del talk**. È breve ma è il cardine: trasforma il problema (bit rot) nella soluzione (fitness function) passando per le -ility. Dillo lentamente.

### Il bit rot non erode "il codice". Erode *qualità specifiche*.

Quando diciamo "l'architettura è degradata" non intendiamo qualcosa di vago: intendiamo che **qualità precise si sono sgretolate**. Ogni sintomo di dolore del §1 è una **caratteristica architetturale** che si erode:

| Sintomo (§1) | Caratteristica che si sta erodendo |
|---|---|
| "non toccare quel modulo", shotgun surgery | **modularità** |
| paura del deploy, rilasci rari | **deployability** |
| "ogni fix ne rompe due" | **testability** |
| il `Controller` che chiama il DB | **modularità / sicurezza** |
| stime che esplodono nel tempo | **manutenibilità / evolvibilità** |

### Il pivot

Se **degrado = erosione di caratteristiche**, allora **prevenire = proteggere quelle caratteristiche.**

E quali sono? **Esattamente quelle per cui abbiamo scelto l'architettura.** Nessuno sceglie un'architettura a caso: la scegliamo perché promette certe qualità. Quelle qualità hanno un nome — le **-ility** — e diventano *le cose da proteggere*.

**// SUL PALCO** — "La domanda 'cosa vogliamo proteggere?' si risponde da sola: le qualità per cui abbiamo scelto questa architettura. Vediamo cosa sono."

---

## 4. Le -ility (6')

### Cosa sono

Le **caratteristiche architetturali** (*architectural characteristics* — il termine di Ford; sinonimi: *quality attributes*, *requisiti non funzionali / NFR*) descrivono il **come** e **sotto quali vincoli** il sistema fa le cose — **non il cosa** (quello è il dominio, la logica di business).

> Il dominio dice *cosa* fa il software. Le -ility dicono *quanto bene* lo fa e *a quali condizioni*.

Si chiamano "-ility" perché molte finiscono così, ma **non tutte**: anche *performance* e *security* sono caratteristiche architetturali.

Esempi: **modularity, testability, deployability, scalability, performance, security, reliability/availability, observability, maintainability, evolvability, portability, interoperability, fault tolerance**…

### Regola d'oro: non puoi proteggerle tutte

Le -ility sono in **trade-off** tra loro. Massimizzarne una ne sacrifica un'altra:

- **sicurezza ↔ performance** (ogni controllo costa latenza)
- **consistenza ↔ disponibilità** (è il cuore del teorema CAP)
- **flessibilità ↔ semplicità** (l'astrazione "per ogni evenienza" costa comprensibilità)
- **performance ↔ modularità** (i confini netti a volte costano hop di rete)

Quindi: **scegli le 3–7 che contano per *questo* sistema.** Sette è già tanto. Un e-commerce e un pacemaker proteggono cose diverse.

**// SUL PALCO** — "Questo non è solo buon senso architetturale: è anche ciò che rende il resto **gestibile**. Se proteggi 5 caratteristiche, non scrivi 200 controlli: ne scrivi una manciata."

### La frase-killer (e l'handoff)

> **Una caratteristica che non sai misurare è solo un desiderio.**

"Scalabile", "robusto", "manutenibile" non vogliono dire niente finché restano aggettivi. Vanno resi **operativi**:

| Desiderio vago | Caratteristica resa misurabile |
|---|---|
| "deve essere scalabile" | p99 < 200ms a 10.000 req/s |
| "deve essere modulare" | il `Domain` ha **zero** dipendenze verso `Infrastructure` |
| "deve essere deployabile" | lead time commit → produzione < 15 min |
| "deve essere sicuro" | zero pacchetti con CVE note in produzione |

**Nel momento in cui rendi una -ility oggettiva ed eseguibile, hai costruito una fitness function.** È esattamente lì che andiamo.

**Takeaway §4:** scegli **poche** caratteristiche ad alto valore, e **rendile numeri**. Gli aggettivi non si possono difendere; le soglie sì.

---

## 5. Cornice: l'architettura evolve (60")

**// SUL PALCO** — una sola slide, un minuto. Serve a dare un nome alla cosa e a spiegare *perché controllare in continuo*. Non farne una sezione teorica.

Tutto questo ha un nome: **Evolutionary Architecture** (Neal Ford, Rebecca Parsons, Patrick Kua — *Building Evolutionary Architectures*, O'Reilly, 2ª ed. 2022). La definizione:

> *"An evolutionary architecture supports **guided**, **incremental** change across **multiple dimensions**."*

Tre parole, due delle quali ci servono subito:

- **Incrementale** = *come* cambi: a piccoli passi, e li sai **rilasciare** (serve la CI/CD — non è un optional).
- **Guidato** = *verso cosa / entro quali limiti* cambi: le **fitness function** ti dicono se stai ancora rispettando le caratteristiche scelte.

Il punto della cornice: **l'architettura non si decide una volta sola — evolve.** Ed è normale. Per evolvere *senza degradare* servono due cose: **sapere cosa proteggere** (le -ility, §4) e **guidare il cambiamento** (le fitness function, §6). Ecco perché il controllo dev'essere **continuo**, non un audit una tantum.

> Da qui viene anche il termine "fitness function": è preso in prestito dal libro, non è inventato.

---

## 6. Fitness function + i 5 assi (13')

### Origine del termine

Preso dal **calcolo evolutivo / algoritmi genetici**: una *fitness function* è una funzione obiettivo che misura **quanto una soluzione candidata è vicina all'obiettivo**. Non decide quali mutazioni avvengono: **valuta** i candidati e **guida** la popolazione verso i più "fit". Stessa logica qui.

### Definizione architetturale (impararla a memoria)

> **"An architectural fitness function provides an objective integrity assessment of some architectural characteristic(s)."**
> *Una fitness function fornisce una valutazione **oggettiva** dell'**integrità** di una o più **caratteristiche architetturali**.*

Tre parole chiave: **oggettiva** (un numero, un pass/fail, non un'opinione) · **integrità** (è ancora dentro i limiti che volevamo?) · **caratteristica architetturale** (la -ility del §4).

**Il legame col §4 — dillo esplicitamente:** *le fitness function sono le -ility rese oggettive ed eseguibili.* Non sono un concetto nuovo calato dall'alto: sono il passo successivo naturale del "rendile numeri".

### I 5 assi (2ª edizione del libro)

Nella 2ª edizione le categorie sono organizzate come **assi** con un nome. Sono questi 5 — più una 6ª considerazione trasversale (*Coverage*).

#### Asse 1 — Scope (Ambito): **Atomic ↔ Holistic**
- **Atomic**: contesto singolo, **un solo aspetto**. *Es.: uno unit test che verifica l'assenza di dipendenze cicliche.*
- **Holistic**: contesto condiviso, **combinazione** di aspetti. *Es.: l'effetto del caching su sicurezza E scalabilità insieme; "nessun dato personale finisce nei log".*

#### Asse 2 — Cadence (Cadenza): **Triggered ↔ Continual ↔ Temporal**
- **Triggered**: scatta su un **evento** (uno unit test, una build in pipeline).
- **Continual**: gira **di continuo**, spesso **in produzione** (monitoring, transazioni sintetiche, alert).
- **Temporal**: legata al **tempo** (es.: "break upon upgrade", un controllo che fallisce di proposito al cambio versione di una libreria; o un check di freschezza delle dipendenze).

#### Asse 3 — Result (Risultato): **Static ↔ Dynamic**
- **Static**: esito fisso **pass/fail** contro una soglia. *Es.: copertura ≥ 80%.*
- **Dynamic**: l'esito **dipende dal contesto**. *Es.: la latenza accettabile si rilassa all'aumentare del carico.*

#### Asse 4 — Invocation (Invocazione): **Automated ↔ Manual**
- **Automated**: in CI/CD. **È il default desiderato.**
- **Manual**: richiede un umano. **Non è un fallimento**: revisione legale, QA esplorativa, approvazione manuale di un cambiamento irreversibile.

#### Asse 5 — Proactivity (Proattività): **Intentional ↔ Emergent**
- **Intentional**: definita **all'inizio**, quando elicitiamo le caratteristiche.
- **Emergent**: **emerge durante** l'evoluzione (gli *unknown unknowns*). Il libro: *intentional over emergent*, ma resta pronto ad aggiungerne.

#### (6ª) — Coverage (Copertura): **Domain-specific**
Considerazione trasversale: alcuni domini hanno requisiti specifici (regolamentari, di sicurezza). *Es.: una banca che fa stress test di sicurezza continui ispirati alla Simian Army di Netflix.*

### Tabella riassuntiva degli assi

| Asse | Dicotomia | Domanda | Esempio .NET |
|------|-----------|---------|--------------|
| **Scope** | Atomic ↔ Holistic | Un aspetto o una combinazione? | ArchUnitNet (atomic) vs "no PII nei log" (holistic) |
| **Cadence** | Triggered ↔ Continual ↔ Temporal | Quando viene valutata? | `dotnet test` (triggered) vs monitor p99 (continual) |
| **Result** | Static ↔ Dynamic | Soglia fissa o contestuale? | coverage ≥ 80% (static) vs latency budget per carico (dynamic) |
| **Invocation** | Automated ↔ Manual | Umano nel loop? | gate in CI (automated) vs ADR review (manual) |
| **Proactivity** | Intentional ↔ Emergent | Pianificata o scoperta? | regola di layering (intentional) vs limite scoperto sotto carico (emergent) |

### Esempi mappati su Scope × Cadence (dal libro)

| | **Triggered** | **Continual** |
|--|--------------|---------------|
| **Atomic** | unit test: dipendenze cicliche, complessità ciclomatica, coverage | monitor: "ogni endpoint REST supporta i verbi richiesti"; *Conformity Monkey* |
| **Holistic** | integration test: effetto di più sicurezza sulla scalabilità | *performance monitoring*, *failover*, **Chaos Monkey** |

### Come si trovano (in pratica)

1. Parti dalle **3–7 caratteristiche** prioritarie (§4).
2. Per ognuna: **"come la misuro oggettivamente?"** → quella misura *è* la fitness function.
3. Mettila nella **pipeline** (deve far fallire la build, non vivere in un README).
4. **Identificale presto** e **rivedile periodicamente**: la suite è viva.

**Takeaway §6:** una fitness function è una **misura oggettiva di una -ility**, classificabile su 5 assi. La maggior parte saranno *atomic + triggered + static + automated* — cioè test in pipeline — ma è solo **un angolo** dello spazio (§7b).

---

## 7a. Demo .NET — ArchUnitNet (10')

**// SUL PALCO** — la parte tangibile. Se fai live coding tieni 2–3 regole pronte; altrimenti mostra gli snippet e *racconta* l'output di fallimento.

### Cos'è

**ArchUnitNet** è il port .NET di **ArchUnit** (Java). Permette di scrivere **regole architetturali come unit test** con una **fluent API**, eseguite dal runner che già usi. Repository: `TNG/ArchUnitNET`.

→ Sono fitness function **atomic + triggered + static + automated + intentional**. Girano dentro `dotnet test`, quindi **fanno fallire la CI** e bloccano la PR che erode l'architettura. *Sostituiscono i controlli manuali in code review con controlli automatici e ripetibili.*

### Setup

```bash
dotnet add package TngTech.ArchUnitNET.xUnit
# in alternativa: TngTech.ArchUnitNET.NUnit / .MSTestV2
```

> Verifica l'ultima versione su nuget.org. La fluent API è stabile, ma piccoli nomi di metodo possono variare: doc su `archunitnet.readthedocs.io`.

### Caricare l'architettura (una volta sola — è costoso)

```csharp
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;        // estensione: .Check() lancia FailedArchRuleException
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

public class ArchitectureTests
{
    private static readonly Architecture Architecture =
        new ArchLoader()
            .LoadAssemblies(
                typeof(MyApp.Domain.Order).Assembly,
                typeof(MyApp.Application.PlaceOrderHandler).Assembly,
                typeof(MyApp.Infrastructure.OrderRepository).Assembly)
            .Build();

    private readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInNamespace("MyApp.Domain", true).As("Domain Layer");
    private readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInNamespace("MyApp.Application", true).As("Application Layer");
    private readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInNamespace("MyApp.Infrastructure", true).As("Infrastructure Layer");
    // NB: il secondo parametro `true` di ResideInNamespace = "usa regex" (matcha i sotto-namespace)
}
```

### Regola 1 — Il dominio non dipende da nessun altro layer (la regola "anti-§1")

```csharp
[Fact]
public void Domain_should_not_depend_on_other_layers()
{
    IArchRule rule = Types().That().Are(DomainLayer)
        .Should().NotDependOnAny(ApplicationLayer)
        .AndShould().NotDependOnAny(InfrastructureLayer)
        .Because("il dominio deve restare puro: niente EF, niente ASP.NET, niente dettagli tecnici");

    rule.Check(Architecture);
}
```

Output tipico di fallimento (questo è ciò che *racconti*):
```
ArchUnitNET.xUnit.FailedArchRuleException :
"Types that are Domain Layer should not depend on Types that are Infrastructure Layer
 because il dominio deve restare puro..." failed:
   MyApp.Domain.Order does depend on MyApp.Infrastructure.SalesDbContext
```

### Regola 2 — Naming + posizione

```csharp
[Fact]
public void Concrete_repositories_live_in_infrastructure()
{
    IArchRule rule = Classes().That().HaveNameEndingWith("Repository")
        .Should().Be(InfrastructureLayer)
        .Because("le implementazioni concrete dei repository sono dettagli infrastrutturali");

    rule.Check(Architecture);
}

[Fact]
public void Interfaces_start_with_I()
{
    Interfaces().Should().HaveNameStartingWith("I").Check(Architecture);
}
```

### Regola 3 — I controller non parlano col database

```csharp
[Fact]
public void Controllers_should_not_depend_on_DbContext()
{
    IArchRule rule = Classes().That().HaveNameEndingWith("Controller")
        .Should().NotDependOnAny(typeof(Microsoft.EntityFrameworkCore.DbContext))
        .Because("i controller non accedono mai direttamente al database (vedi §1)");

    rule.Check(Architecture);
}
```

### Regola 4 — Nessuna dipendenza ciclica tra moduli (slices)

```csharp
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;

[Fact]
public void Feature_modules_should_be_free_of_cycles()
{
    Slices().Matching("MyApp.Modules.(*)")
        .Should().BeFreeOfCycles()
        .Check(Architecture);
}
```

### Confronto rapido: NetArchTest (alternativa più snella)

```csharp
using NetArchTest.Rules;

var result = Types.InAssembly(typeof(MyApp.Domain.Order).Assembly)
    .That().ResideInNamespace("MyApp.Domain")
    .ShouldNot().HaveDependencyOn("MyApp.Infrastructure")
    .GetResult();

Assert.True(result.IsSuccessful);
```

**// SUL PALCO** — chiudi la demo così: "Tre righe e l'erosione del §1 **non passa più**. Ma attenzione: questo è *uno* dei cinque assi. Una fitness function **non è** sinonimo di test xUnit."

---

## 7b. Oltre xUnit — non sono solo test (6')

**// SUL PALCO** — il *plot twist*. Smonta l'equazione "fitness function = test xUnit". Scorri veloce, fermati su 2–3 esempi.

### Esempio in evidenza (non-test): un **Roslyn analyzer**

La fitness function più "a sinistra" possibile: gira **nel compilatore**, mentre scrivi. Il feedback è una **squiggle rossa nell'IDE** e una **build rotta** — prima ancora della CI. Stessa regola del §1, ma a tempo di compilazione:

```csharp
using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ControllerDbAccessAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH001";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Un Controller non deve dipendere da DbContext",
        messageFormat: "Il controller '{0}' espone un DbContext: viola il layering",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,   // <-- ERROR = fa fallire la build
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.Field, SymbolKind.Property);
    }

    private static void Analyze(SymbolAnalysisContext ctx)
    {
        ITypeSymbol? memberType = ctx.Symbol switch
        {
            IFieldSymbol f    => f.Type,
            IPropertySymbol p => p.Type,
            _                 => null
        };
        if (memberType is null) return;

        var owner = ctx.Symbol.ContainingType;
        if (owner is null || !owner.Name.EndsWith("Controller", StringComparison.Ordinal)) return;

        if (InheritsFromDbContext(memberType))
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Symbol.Locations[0], owner.Name));
    }

    private static bool InheritsFromDbContext(ITypeSymbol type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (t.ToDisplayString() == "Microsoft.EntityFrameworkCore.DbContext")
                return true;
        return false;
    }
}
```

Perché è una fitness function diversa da quelle xUnit: **invocata dalla compilazione** (non da un test runner), feedback **istantaneo nell'IDE**, e si distribuisce come **pacchetto di analyzer** condiviso da tutto il team. Sugli assi: *atomic · triggered (a compile-time, percepita come continua mentre scrivi) · static · automated · intentional*.

### Tassonomia rapida (mappa il resto del paesaggio)

- **Static analysis / governance**: Roslyn analyzers + `.editorconfig severity=error`, `dotnet format --verify-no-changes`, StyleCop, SonarQube, NDepend (query CQLinq).
- **Mutation testing** *(meta-fitness function: misura la qualità dei **test**)*:
  ```bash
  dotnet stryker --break-at 60   # fallisce se il mutation score < 60%
  ```
- **Security**:
  ```bash
  dotnet list package --vulnerable --include-transitive   # CVE nelle dipendenze
  ```
  + SAST (CodeQL), dependency scanning (Dependabot, Snyk).
- **Performance** *(spesso dynamic)*: NBomber/k6, latency budget, performance budget su dimensione artefatto.
- **Contract testing** *(holistic, confini dei servizi)*: PactNet, compatibilità di schema per eventi/messaggi.
- **Chaos engineering** *(holistic + continual)*: Polly v8 / Simmy (`AddChaosLatency`, `AddChaosFault`), ispirato a Netflix Chaos Monkey.
- **Observability / SLO** *(holistic, continual, **in produzione**)*: una alerting rule "p99 < 200ms" che gira H24 **è** una fitness function. Synthetic monitoring, canary analysis.
- **Policy as code / infrastruttura** *(triggered)*: OPA/Conftest, Azure Policy, `terraform validate`.
- **Manuali** *(pienamente legittime)*: ADR review, architecture review board, QA esplorativa, approvazione di migrazioni distruttive.

### Mappa: esempio → assi

| Esempio | Scope | Cadence | Result | Invocation |
|---------|-------|---------|--------|------------|
| Roslyn analyzer | Atomic | Triggered (compile) | Static | Automated |
| ArchUnitNet (layering) | Atomic | Triggered | Static | Automated |
| `dotnet list package --vulnerable` | Atomic | Triggered/Temporal | Static | Automated |
| NBomber latency budget | Atomic→Holistic | Triggered | Dynamic | Automated |
| SLO p99 in prod | Holistic | Continual | Dynamic | Automated |
| Chaos (Simmy) | Holistic | Continual | Dynamic | Automated |
| ADR / review board | Holistic | Triggered | — | **Manual** |

### Tutto insieme in pipeline

```yaml
# GitHub Actions — i "quality gates" come fitness function triggered
- name: Quality & architecture gates
  run: |
    dotnet format --verify-no-changes
    dotnet test --filter "Category=Architecture"     # ArchUnitNet / NetArchTest
    dotnet test --filter "Category=Contract"          # PactNet
    dotnet list package --vulnerable --include-transitive
    dotnet stryker --break-at 60
```

**Takeaway §7b:** la fitness function è un **concetto**, non una libreria. *Compilatore, test, metriche, monitoring, policy, persino revisioni umane*: qualunque cosa dia una **valutazione oggettiva** di una caratteristica architetturale, su uno qualsiasi dei 5 assi.

---

## 7c. Call to action (4')

**// SUL PALCO** — chiusura concreta. "Cosa fate lunedì mattina."

### Cosa fare lunedì (in ordine)

1. **Scrivi le tue 3–7 caratteristiche** — le -ility che contano *davvero*. Renderle esplicite è già metà del lavoro (bonus: un **ADR**).
2. **Rendile numeri** — trasforma ogni aggettivo in una soglia ("modulare" → "Domain ha zero dipendenze su Infrastructure").
3. **Aggiungi UNA fitness function questa settimana** — la più dolorosa-eppure-facile. Tipicamente: la regola di layering in ArchUnitNet che cattura l'erosione del §1.
4. **Mettila nel quality gate** — deve **far fallire la CI**, non vivere in un README. Una FF che non blocca nessuno è teatro.
5. **Baseline + ratcheting sul legacy** — non parti da zero: congeli il numero di violazioni attuali e impedisci che **aumenti**. Ogni PR può solo migliorare.
6. **Rivedile periodicamente** — la suite è viva: aggiungi le *emergent*, togli le obsolete.

### Anti-pattern da evitare (dillo!)

- **Fitness function theater**: regole che esistono ma non bloccano nulla.
- **Troppo rigide, troppo presto**: 200 regole il primo giorno → tutti le disabilitano. Parti da poche.
- **Solo codice**: ricordati delle altre dimensioni (dati, sicurezza, operazioni).
- **Eroismo solitario**: è una pratica di **team**. Senza buy-in muore.

### Messaggio finale (la frase da lasciare)

> *"Non puoi impedire al mondo di cambiare, quindi non puoi impedire all'architettura di voler degradare. Ma puoi scegliere quali qualità proteggere, renderle **misurabili**, e rendere le regressioni **impossibili da ignorare**. Una fitness function alla volta — a partire da domani."*

---

## Appendice A — Glossario rapido

- **Bit rot / software rot**: degrado graduale della qualità del software nel tempo.
- **Architectural drift**: decisioni non previste ma non in violazione → perdita di coerenza.
- **Architectural erosion**: decisioni in **violazione** dell'architettura prevista.
- **Big Ball of Mud**: architettura *de facto* senza struttura percepibile.
- **Technical debt**: costo futuro accumulato da scelte rapide/non ottimali.
- **Caratteristica architetturale / -ility**: requisito non funzionale (modularità, performance, sicurezza, deployability, evolvability…).
- **Fitness function**: valutazione **oggettiva** dell'integrità di una caratteristica architetturale.
- **Guided change**: cambiamento *guidato* dalle fitness function (direzione/limiti).
- **Incremental change**: cambiamento a piccoli passi + capacità di rilasciarli (CI/CD).
- **Ratcheting**: bloccare la baseline di violazioni e impedirne l'aumento.

## Appendice B — Q&A: domande probabili

- **"Sul legacy da cui parto?"** → baseline + ratcheting. Non sistemi tutto: impedisci che peggiori.
- **"Non rallenta la CI?"** → le architetturali sono rapidissime (analisi statica). Le costose (load, mutation) vanno in pipeline notturna o pre-merge selettivo.
- **"Non basta una buona code review?"** → la review è *manuale, non ripetibile, a campione*; le fitness function automatiche sono *deterministiche e instancabili*. Complementari.
- **"Quante ne servono?"** → poche e ad alto valore. Meglio 5 che bloccano davvero che 50 ignorate.
- **"Microservizi obbligatori?"** → no. Vale anche per il monolite (un *modular monolith* con regole di layering è il caso d'uso perfetto per ArchUnitNet).
- **"Differenza tra fitness function e unit test?"** → l'unit test verifica il *comportamento*; la fitness function verifica una *caratteristica architetturale*. Spesso stesso strumento (xUnit), intento diverso — e spesso **non** è nemmeno xUnit (vedi Roslyn analyzer, monitoring, policy).

## Appendice C — Bibliografia & risorse

- Neal Ford, Rebecca Parsons, Patrick Kua — *Building Evolutionary Architectures* (O'Reilly, 2ª ed. 2022).
- Perry & Wolf — *Foundations for the Study of Software Architecture* (1992) — drift vs erosion.
- Foote & Yoder — *Big Ball of Mud* (1997).
- Hunt & Thomas — *The Pragmatic Programmer* — Broken Windows.
- Lehman — *Laws of Software Evolution*.
- ArchUnitNet (`TNG/ArchUnitNET`, `archunitnet.readthedocs.io`) · NetArchTest (`BenMorris/NetArchTest`).
- Roslyn analyzers (`learn.microsoft.com` → "Source generators and analyzers") · Stryker.NET · NBomber · PactNet · Polly/Simmy.

## Appendice D — Checklist pre-talk

- [ ] War story del §1 provata ad alta voce (storia *vera*, riconoscibile).
- [ ] Snippet del controller leggibile a video (font grande).
- [ ] Il **ponte** (§3) memorizzato: "il bit rot erode qualità specifiche".
- [ ] Tabella sintomo → -ility pronta.
- [ ] La frase-killer: "una caratteristica che non sai misurare è un desiderio".
- [ ] Cornice EA in 60" (una slide).
- [ ] I 5 assi recitabili senza slide.
- [ ] Demo ArchUnitNet: 2–3 regole + output di fallimento.
- [ ] Esempio non-xUnit scelto (Roslyn analyzer) + 1–2 di rincalzo (SLO, chaos).
- [ ] Call to action su una slide singola.
- [ ] Timer mentale: §6 è il più lungo — non sforare.
