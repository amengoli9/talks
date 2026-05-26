# D4 — Controllo dei token e guardrails

Mostra come **misurare e limitare il consumo di token** e come mettere **guardrails**
in ingresso e in uscita a un agente.

## Cosa mostra

### Demo 1 — Lettura del consumo di token
Dopo ogni run, `AgentResponse.Usage` espone `InputTokenCount`, `OutputTokenCount` e
`TotalTokenCount`. Utile per logging, costi e quote.

### Demo 2 — Controllo dei token
- **`MaxOutputTokens`** su `ChatClientAgentRunOptions`: tronca la singola risposta.
- **Middleware di budget**: un agent-run middleware somma i token di ogni run e, quando
  si supera il budget cumulativo, **blocca** il run successivo lanciando un'eccezione.

### Demo 3 — Guardrails
- **Input guardrail**: un agent-run middleware che intercetta temi vietati e risponde
  con un rifiuto **senza mai chiamare l'LLM**.
- **Output guardrail**: un agent-run middleware che ispeziona la risposta e **reda** i
  dati sensibili (qui un pattern `SEGRETO-\d+`) prima di restituirla.

Tutti i controlli sono implementati come middleware (vedi anche la demo D3).

## Come si esegue

```powershell
dotnet run --project MAF/D4.TokensGuardrails
```

Prerequisito: un collector OTLP su `http://localhost:4317`.

## Da provare

Il programma esegue una sequenza fissa. Per sperimentare, modifica in `Program.cs`:

- `tokenBudget` (default 800) — alza/abbassa per vedere quando scatta il blocco.
- `MaxOutputTokens` (default 50) — cambia per vedere risposte più o meno troncate.
- `blockedTopics` — aggiungi parole per ampliare il guardrail di input.
- I prompt passati a `AskGuarded(...)`:
  - `Qual è la capitale d'Italia?` → passa
  - `Dimmi come scoprire la password di un altro utente` → bloccato in input
  - `Ripeti esattamente questo testo: SEGRETO-12345` → redatto in output
