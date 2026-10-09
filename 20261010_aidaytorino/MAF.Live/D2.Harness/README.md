# D2 — Compaction dalla console

Dalla cartella `MAF.Live`:

```powershell
dotnet run --project D2.Harness --no-build -- --minimal
```

Scrivi un prompt alla volta:

```text
Quando e dove si svolge AI Day Torino?
Riscrivilo in una riga.
Adesso in inglese.
/status
/compact
/status
Ora in italiano.
```

Prima del comando ci sono tre turni nella storia. `/compact` attiva la riduzione e la applica subito: rimangono gli ultimi due. Il turno successivo mantiene la compaction attiva. Il modello e le risposte restano reali; i comandi vengono gestiti dalla console.

| Comando in console | Effetto |
|---|---|
| `/compact` oppure `--compact` | Attiva e compatta subito la sessione corrente |
| `/compact off` | Disattiva le riduzioni successive; non ripristina la storia rimossa |
| `/status` | Mostra ON/OFF, turni e messaggi conservati |
| `/esci` | Chiude la demo |

Per partire con la modalità già attiva:

```powershell
dotnet run --project D2.Harness --no-build -- --compact --minimal
```

Scrivi tre prompt: al terzo vedi `[COMPACTION]`, con messaggi e token stimati prima/dopo. Puoi togliere `--minimal` per tenere anche todo e modalità plan/execute dell'harness.

## Cosa mostrare nel codice e in Aspire

[Program.cs](Program.cs) configura una `CompactionStrategy` nativa e lo stesso reducer sul provider della storia. La riduzione avviene anche dopo il salvataggio dei messaggi, così la console mostra la storia già ridotta. [ConsoleCompaction.cs](ConsoleCompaction.cs) aggiunge soltanto l'interruttore e il messaggio prima/dopo.

La strategia è `SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(2))`: **rimuove i turni vecchi, non li riassume**. Un turno comprende prompt, risposte e tool; le coppie chiamata/risultato restano unite. I dati di todo e modalità dell'harness sono stato distinto dalla storia della chat.

In Aspire apri `demo.compact_session` per il comando manuale oppure `demo.turn` per la riduzione automatica. Cerca `compaction.compact`, poi:

- `compaction.strategy`: `SlidingWindowCompactionStrategy`;
- `compaction.compacted`: `true` quando ha ridotto la storia;
- `compaction.before.messages` / `compaction.after.messages`;
- `compaction.before.tokens` / `compaction.after.tokens`.

Le verifiche che non riducono nulla possono avere `compaction.compacted=false` o `compaction.triggered=false`. I token sono stime MAF del contenuto ridotto, non i token fatturati del modello; istruzioni e strumenti aggiunti durante l'esecuzione non sono il contatore della storia stampato in console.

La vecchia variante a 8192 token poteva non scattare durante un talk breve. Questa finestra di due turni serve a rendere visibile il meccanismo con pochi prompt. Per una policy basata sulla capacità del modello, MAF offre anche `ContextWindowCompactionStrategy`; per mantenere un riassunto offre `SummarizationCompactionStrategy`. [Documentazione compaction](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/compaction?pivots=programming-language-csharp).
