# D6 — Workflow completamente osservabile

Mostra un workflow semplice di **4 agenti** in cui l'observability OpenTelemetry fa
vedere **tutto quello che succede dietro le quinte**.

## Cosa mostra

Pipeline sequenziale:

```
researcher  ->  analyst  ->  writer  ->  reviewer
(usa un tool)   (analizza)   (scrive)    (rivede)
```

OpenTelemetry è attivo a **ogni livello**:

| Livello | Come |
|---------|------|
| Chat client | `ChatClientFactory.Create` → `UseOpenTelemetry` |
| Ogni agente | `.WithOpenTelemetry(...)` |
| Workflow | catturato dalla source `Microsoft.Agents.AI*` |

L'intero run è avvolto in uno **span radice** (`D6 workflow run`): nel backend OTLP
trovi un unico trace con dentro, in gerarchia:

- ogni **turno** (cambio di agente)
- il **messaggio passato** da un agente al successivo (prompt e risposta come attributi)
- le **chiamate ai tool** (`search_facts`) con argomenti e risultato
- timing e token di ogni chiamata all'LLM

La console rispecchia la stessa storia (`>> turno:`, `[tool] chiamata/risultato`) così la
demo è leggibile dal vivo, ma il valore è nel trace completo sul backend.

## Come si esegue

```powershell
dotnet run --project MAF/D6.ObservableWorkflow
# con un argomento tuo:
dotnet run --project MAF/D6.ObservableWorkflow -- "il caching distribuito"
```

Prerequisito: un collector OTLP su `http://localhost:4317`. All'avvio il programma
stampa il **Trace ID**: cercalo nel tuo backend per vedere l'albero completo.

## Da provare

Passa l'argomento dopo `--`:

- `gli agenti AI nello sviluppo software` (default)
- `il pattern CQRS`
- `l'osservabilità nei sistemi distribuiti`
- `il green software`

Per ognuno, confronta la console con il trace nel backend: nel trace vedi anche prompt,
risposte complete, token e durate che in console non stampiamo.
