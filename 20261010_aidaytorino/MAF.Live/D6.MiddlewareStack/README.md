# D6 — Tre punti, lo stesso pattern: prima → next → dopo

Un agente, una domanda, un tool che restituisce data e città. Apri [Program.cs](Program.cs): il punto della demo sono le tre registrazioni `.Use(...)` e i tre callback.

Dalla cartella `MAF.Live`:

```powershell
dotnet run --project D6.MiddlewareStack
```

Prompt: **Quando e dove si svolge AI Day Torino?**

## I tre punti di intervento

| Dove | Registrazione | Quando interviene |
|---|---|---|
| Agente | `.Use(AgentMiddleware, null)` sull'agent builder | Una volta attorno all'intera `RunAsync`: input e risposta finale |
| Modello | `.Use(ChatMiddleware, null)` sul chat client builder | A ogni chiamata al modello, inclusa quella con il risultato del tool |
| Tool | `.Use(FunctionMiddleware)` sull'agent builder | A ogni invocazione di una funzione: argomenti e risultato |

In tutti e tre i callback si ripete la stessa struttura:

```csharp
// Prima: posso leggere o modificare l'input.
var result = await next(...);
// Dopo: posso leggere o modificare il risultato.
return result;
```

Qui aggiungiamo solo un messaggio prima e dopo, più una span per vedere il punto di intervento in Aspire. `next` fa proseguire l'esecuzione; restituire una risposta senza chiamarlo permette invece di fermarla. La demo usa `RunAsync`; il secondo parametro `null` nelle due registrazioni omette il callback dedicato allo streaming.

## Cosa mostrare dal vivo

1. Le tre righe `.Use(...)` in `CreateAgent`.
2. I tre callback: stessa struttura, cambiano input e tipo di `next`.
3. La console, che per un giro modello → tool → modello mostra:

```text
[AGENTE →] inizio RunAsync
    [MODELLO →] invio messaggi
    [← MODELLO] testo oppure richiesta di tool
    [TOOL →] GetEventInfo
    [← TOOL] risultato pronto
    [MODELLO →] invio messaggi
    [← MODELLO] testo oppure richiesta di tool
[← AGENTE] risposta finale
```

Il middleware agente avvolge tutto il giro. Quello del modello entra due volte; quello del tool una. Il middleware delle funzioni si registra sull'agente, ma viene eseguito dentro il loop, quando il modello richiede un tool. Ogni prompt parte senza la cronologia del precedente.

Facoltativo: in Aspire, **Traces → D6.MiddlewareStack → demo.middleware_stack**, cerca `middleware.agent`, `middleware.chat` e `middleware.function`. Le span rendono visibili annidamento e durata degli stessi callback mostrati in console.

[Middleware in MAF: documentazione ufficiale](https://learn.microsoft.com/en-us/agent-framework/agents/middleware/?pivots=programming-language-csharp)
