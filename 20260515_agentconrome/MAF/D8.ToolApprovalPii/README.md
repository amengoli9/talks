# D8 — Tool approval + PII redaction

Combina **due controlli di sicurezza** su un singolo agente di supporto clienti:
l'approvazione umana dei tool sensibili e la rimozione dei dati personali (PII).

## Cosa mostra

### 1. PII redaction
Un agent-run middleware ripulisce email, numeri di telefono e nomi+cognomi:

- **in ingresso** — prima che i messaggi raggiungano l'LLM (e quindi anche prima che
  finiscano nei trace OpenTelemetry, dove `EnableSensitiveData` è attivo);
- **in uscita** — dai messaggi che l'agente restituisce.

Il middleware preserva i contenuti non testuali (tool call, richieste di approvazione):
reda solo il testo.

### 2. Tool approval
Il tool `send_email` è avvolto in `ApprovalRequiredAIFunction`: l'agente non può inviare
nulla senza un `Y/N` esplicito dell'umano (stesso meccanismo della demo D7).

Le due cose lavorano insieme: quando l'agente vuole mandare la mail, il run si ferma per
l'approvazione **e** ogni dato personale resta comunque redatto in tutto il flusso.

## Come si esegue

```powershell
dotnet run --project MAF/D8.ToolApprovalPii
```

Il programma esegue due richieste; per quella che attiva l'invio email **digiti tu** `Y` o `N`.

Prerequisito: un collector OTLP su `http://localhost:4317`.

## Da provare

Modifica i prompt passati a `Ask(...)` in `Program.cs`:

- `Ciao, sono Mario Rossi, il mio numero e' 333-1234567. Che orari avete?`
  → PII redatta in/out, nessun tool
- `Ho un problema con l'ordine 4815. Scrivi un riassunto e invialo al supporto.`
  → PII redatta + richiesta di approvazione per `send_email`
- Prova un prompt con una email dentro (es. `scrivimi a nome.cognome@test.com`) e
  osserva che l'indirizzo viene sostituito con `[PII RIMOSSA]` prima dell'LLM.

Confronta anche il trace nel backend OTLP: i dati personali non compaiono perché vengono
redatti **prima** che l'LLM (e quindi la telemetria) li veda.
