# D1 — Sequential orchestrator in DevUI

Mostra un **orchestratore sequenziale** visualizzato nella **DevUI**, l'interfaccia web
del Microsoft Agent Framework per testare agenti e workflow.

## Cosa mostra

Tre agenti banali composti in pipeline con `AgentWorkflowBuilder.BuildSequential`:

```
brainstormer  ->  writer  ->  editor
(genera spunti)   (scrive bozza)  (rifinisce)
```

Nella DevUI trovi sia i 3 agenti singoli sia il workflow `writing-pipeline` che li
esegue in sequenza, passando l'output di uno come input del successivo.

## Come si esegue

```powershell
dotnet run --project MAF/D1.SequentialDevUI
```

Poi apri **https://localhost:50516/devui**.

Prerequisito: un collector OTLP in ascolto su `http://localhost:4317` (traces e metrics
vengono esportati lì).

## Da provare nella chat della DevUI

Seleziona il workflow **`writing-pipeline`** e prova:

- `Scrivi un post LinkedIn che annuncia il mio talk su AI agents ad AgentCon Roma`
- `Genera il testo per la slide di apertura di una demo su .NET 10`
- `Scrivi una mail breve per invitare un collega a un workshop interno sull'AI`
- `Inventa lo slogan per una conferenza sugli agenti intelligenti`

Osserva come cambia la risposta: il brainstormer produce gli spunti, il writer la bozza,
l'editor la versione finale. Selezionando un singolo agente vedi invece solo il suo step.
