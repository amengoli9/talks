# D12b - Agent progression

Lo stesso "assistente di viaggio" costruito **sei volte** in un unico
`Program.cs`. Ogni step aggiunge una capability alla precedente. L'agente
viene **ricostruito da zero ogni step** apposta, cosi' dal vivo il diff
tra step N e step N+1 e' una manciata di righe e si vede a colpo d'occhio.

## I sei step

| Step | Aggiunge | API MAF chiave |
|---|---|---|
| 1 | Agente minimale via **Ollama locale** | `new OllamaApiClient(uri, model)` (implementa `IChatClient`) |
| 2 | Observability + switch ad Azure OpenAI | `ChatClientFactory.Create` (OTel built-in) + `.WithOpenTelemetry()` |
| 3 | Tools / skill | `AIFunctionFactory.Create(...)` + `tools:` su ChatClientAgent |
| 4 | Structured output + multimodal | `ChatResponseFormat.ForJsonSchema<TripPlan>()` + `DataContent` |
| 5 | Middleware | `.Use(ChatMW, null)` + `.Use(AgentMW, null)` + `.Use(FunctionMW)` |
| 6 | Safety | input guardrail + output redaction (regex telefoni) |

## Comando

```powershell
dotnet run --project MAF/D12b.AgentProgression
```

Si lancia una volta sola e poi il programma chiede da console quale step
eseguire (1-6). Si puo' rieseguire piu' step di fila senza riavviare;
vuoto/Invio per uscire.

## Multimodal: l'immagine

Lo step 4 carica `Assets/map.png` se presente, altrimenti usa un PNG
trasparente 1x1 embeddato come fallback (solo per non far esplodere la
demo). **Prima del talk droppa una mappa reale** (es. screenshot Google
Maps di un percorso Torino-Roma) in `Assets/map.png` e la demo diventa
visibile a tutta la sala.

## Prerequisiti per lo Step 1

Lo step 1 gira su **Ollama** in locale. Prima del talk:

```powershell
# installa Ollama da https://ollama.com
ollama pull llama3.2     # o un altro modello small/locale
ollama serve             # parte come servizio se non e' gia' attivo
```

Override (opzionale) via env var:
- `OLLAMA_HOST` (default `http://localhost:11434`)
- `OLLAMA_MODEL` (default `llama3.2`)

Dallo step 2 in poi si torna ad Azure OpenAI via `ChatClientFactory` — il
punto narrativo: **al livello `AIAgent` non e' cambiato niente**, ho solo
swappato l'`IChatClient`.

## Cosa fare vedere step per step

- **STEP 1** -> "Sotto sotto e' una chiamata LLM con un'identita'. E gira in
  **locale, via Ollama**: l'agente non sa nulla del provider, vede solo un
  `IChatClient`. Cambio provider = cambio una riga."
- **STEP 2** -> Mostra il trace nel backend OTLP: il prompt completo, la
  risposta, i token, tutto. "Una riga, ho aperto la scatola nera."
- **STEP 3** -> Apri il trace dello step 3: dentro l'attivita' dell'agente
  ci sono tool call con argomenti e risultato. "Function-calling automatico
  + tracing automatico."
- **STEP 4a** -> Mostra il JSON: non e' "JSON con un po' di fortuna", e' un
  JSON schema forzato. **Apri il trace** e cerca il parametro
  `response_format` nella chiamata LLM: si vede lo schema. **STEP 4b**: il
  prompt contiene un'immagine, il modello la descrive.
- **STEP 5** -> Apri il `Program.cs` e mostra l'annidamento: agent-run e'
  l'outer cerchio, chat-client e function-invocation sono dentro.
- **STEP 6** -> Mostra le 3 demo:
  - prompt safe -> passa
  - tema vietato -> bloccato **prima** della chiamata LLM (zero token spesi)
  - output con un numero di telefono -> il middleware reda dopo l'LLM

## Cose da dire ad alta voce

> "L'agente cresce un livello alla volta. Niente di magico in nessun passaggio
> singolo: una riga aggiunge l'observability, una riga aggiunge un tool, una
> riga aggiunge un middleware. La differenza fra uno script qualunque e un
> agente di produzione e' la somma di queste righe."
