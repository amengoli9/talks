# D9 — Checkpoints

Mostra come **salvare e riprendere lo stato di un workflow** tramite i checkpoint.

## Cosa mostra

Un workflow "indovina il numero": `GuessExecutor` (ricerca binaria 1-100) e
`JudgeExecutor` (numero target 42) collegati in un loop.

Concetti chiave:

- **Super step** — il workflow avanza a stadi; ogni mossa è un super-step.
- **Checkpoint** — con un `CheckpointManager`, il framework salva automaticamente lo
  stato alla fine di **ogni super-step**.
- **Stato degli executor** — ogni executor persiste il proprio stato sovrascrivendo
  `OnCheckpointingAsync` (salva) e `OnCheckpointRestoredAsync` (ripristina). Qui
  `GuessExecutor` salva i suoi bound, `JudgeExecutor` salva il numero di tentativi.
- **Reidratazione** — si crea un'**istanza nuova di zero** del workflow e la si riprende
  da un checkpoint salvato con `InProcessExecution.ResumeStreamingAsync`.

Il programma esegue il workflow raccogliendo un checkpoint per super-step, poi reidrata
un workflow nuovo da un checkpoint **intermedio** e lo lascia finire: il risultato finale
è identico, a riprova che lo stato era davvero persistito e non solo tenuto in memoria.

## Come si esegue

```powershell
dotnet run --project MAF/D9.Checkpoints
```

Prerequisito: un collector OTLP su `http://localhost:4317`.

## Da provare

In `GuessWorkflow.cs` e `Program.cs`:

- cambia il numero target in `JudgeExecutor` (`new(target: 42)`) o il range in
  `GuessExecutor` (`new(low: 1, high: 100)`) — cambiano numero di super-step e checkpoint;
- cambia da quale checkpoint reidratare (`int index = checkpoints.Count / 2;`): provando
  un indice più basso il workflow ripreso esegue più passi, più alto meno passi;
- aggiungi stato a un executor e verifica che, senza aggiornarne
  `OnCheckpointingAsync`/`OnCheckpointRestoredAsync`, quello stato **non** sopravvive alla
  reidratazione.
