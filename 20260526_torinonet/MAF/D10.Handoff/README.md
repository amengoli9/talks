# D10 — Handoff orchestration in DevUI

Mostra l'orchestrazione a **handoff** esposta nella **DevUI**: un agente di smistamento
passa la conversazione allo specialista giusto in base all'argomento.

## Cosa mostra

Quattro agenti, registrati nell'host insieme al workflow:

```
        triage  (smistatore: decide a chi passare)
          |
   +------+------+
   |      |      |
  tech  billing general   (gli specialisti rispondono davvero)
```

L'API chiave è `AgentWorkflowBuilder.CreateHandoffBuilderWith(triage)` +
`.WithHandoffs(triage, [tech, billing, general])` (handoff dal triage agli specialisti) +
`.WithHandoffs([tech, billing, general], triage)` (gli specialisti tornano al triage per
i turni successivi).

Nella DevUI trovi:
- i **4 agenti singoli** (`triage`, `tech`, `billing`, `general`) che puoi testare uno per uno
- il **workflow `handoff`** che li orchestra: scegli quello, scrivi una domanda, e vedi
  triage che decide e poi lo specialista che risponde

Tutto è instrumentato con OpenTelemetry: nel trace vedi chi parla a chi.

## Come si esegue

```powershell
dotnet run --project MAF/D10.Handoff
```

Poi apri **https://localhost:50517/devui**.

Prerequisito: un collector OTLP su `http://localhost:4317`.

## Da provare nella chat della DevUI

Seleziona il workflow **`handoff`** e prova:

- `Ho un errore 500 quando avvio l'app`       → triage → tech
- `Quando viene addebitata la prossima rata?` → triage → billing
- `Che orari fa il vostro ufficio di Roma?`   → triage → general
- `Ho un problema con la fattura del mese scorso e l'app va in errore` — osserva come il
  triage sceglie quando ci sono più temi

Selezionando un singolo agente (es. `tech`) lo interroghi direttamente, senza passare
dal triage: utile per mostrare la differenza tra chiamata diretta e flusso a handoff.
