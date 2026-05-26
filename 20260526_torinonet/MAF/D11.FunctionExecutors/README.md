# D11 — Workflow di funzioni con un sub-workflow condizionale

Mostra che un workflow MAF **non è obbligato a usare agenti LLM**: gli executor possono
essere puro codice C# (`Executor<TIn, TOut>`). E mostra come incorporare **un workflow
condizionale come singolo executor** dentro un workflow più grande.

## Cosa mostra

Pipeline sequenziale di **executor C#**, dove il terzo passo è in realtà un **sub-workflow
condizionale**:

```
Parse  ->  Enrich  ->  [Router sub-workflow (switch su importo)]  ->  Finalize
                              │
                       ┌──────┼──────┐
                  small        standard       large
                  fast lane    standard lane  audit lane
```

Niente LLM, niente agenti: ogni passo è una classe che eredita da
`Executor<TIn, TOut>` e implementa `HandleAsync`. Le decisioni si prendono in C#.

Concetti API chiave:

- `Executor<TIn, TOut>` — il nodo del workflow è una funzione C# tipizzata.
- `new WorkflowBuilder(root).AddEdge(a, b).WithOutputFrom(x).Build()` — costruzione del
  workflow.
- `AddSwitch(source, sb => sb.AddCase<T>(predicate, target).WithDefault(fallback))` —
  routing condizionale: il `source` produce un messaggio che viene smistato al case che
  matcha (qui sull'importo dell'ordine).
- `subWorkflow.BindAsExecutor("Router")` — un workflow intero diventa un singolo
  `ExecutorBinding` usabile come nodo nel workflow esterno. È così che il router
  condizionale entra nella pipeline.

## Come si esegue

```powershell
dotnet run --project MAF/D11.FunctionExecutors
```

Il programma esegue 3 ordini di esempio (uno per ogni lane). Prerequisito: un collector
OTLP su `http://localhost:4317`.

## Da provare

In `Program.cs` cambia gli input per esplorare le lane (formato `id|cliente|importo`):

- `ord-x|Pippo|10`   → lane **fast** (< 100)
- `ord-x|Pippo|500`  → lane **standard** (100..999)
- `ord-x|Pippo|9999` → lane **audit** (>= 1000)

In `Executors.cs` cambia le soglie nello switch (`AddCase<Order>(o => o is { Amount: < 100m }, ...)`)
per vedere come il router cambia comportamento, oppure aggiungi un nuovo `LaneExecutor`
e un nuovo `AddCase`.
