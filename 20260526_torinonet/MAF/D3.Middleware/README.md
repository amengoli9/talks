# D3 — I tre tipi di middleware

Mostra i **3 tipi di middleware** del Microsoft Agent Framework applicati a un singolo
agente con un solo tool. Ogni middleware stampa una riga PRE/POST, così si vede come la
pipeline si annida.

## I tre middleware

| # | Tipo | Dove si aggancia | Cosa intercetta |
|---|------|------------------|-----------------|
| 1 | **Chat client** | `IChatClient.AsBuilder().Use(...)` | Ogni chiamata verso l'LLM |
| 2 | **Agent run** | `AIAgent.AsBuilder().Use(runFunc, ...)` | Un intero run dell'agente |
| 3 | **Function invocation** | `AIAgent.AsBuilder().Use(funcFunc)` | Ogni invocazione di un tool |

Ordine di annidamento osservabile nell'output:

```
[2. agent-run]   PRE
  [1. chat-client]  PRE / POST        (1ª chiamata: l'LLM decide di usare il tool)
  [3. function]     PRE / POST        (esecuzione del tool)
  [1. chat-client]  PRE / POST        (2ª chiamata: l'LLM formula la risposta finale)
[2. agent-run]   POST
```

Se il prompt **non** richiede il tool, lo strato 3 non scatta e l'LLM viene chiamato
una sola volta.

## Come si esegue

```powershell
dotnet run --project MAF/D3.Middleware
```

Prerequisito: un collector OTLP su `http://localhost:4317`.

## Da provare

Il programma esegue già due prompt (uno che usa il tool, uno che non lo usa). Per
sperimentare modifica le chiamate `Ask(agent, "...")` in `Program.cs`:

- `Che ore sono?` → attiva tutti e 3 i middleware
- `Dimmi una curiosità su C#` → attiva solo chat-client e agent-run
- `Che ore sono adesso e raccontami una barzelletta?` → il tool scatta, poi l'LLM continua
