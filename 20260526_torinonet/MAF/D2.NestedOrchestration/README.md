# D2 — Orchestrazione custom annidata

Mostra come **comporre orchestrazioni dentro altre orchestrazioni**: un workflow può
essere usato come tassello di un workflow più grande.

## Cosa mostra

Due livelli di orchestrazione:

```
ORCHESTRAZIONE ESTERNA  (sequential)
  triage  ->  debate-room  ->  summarizer
                  |
                  +-- ORCHESTRAZIONE INTERNA (group chat round-robin)
                        optimist  <->  skeptic   (4 turni)
```

- `triage` riformula la richiesta dell'utente come una proposta neutra.
- `debate-room` è **un workflow group-chat** trasformato in un singolo agente con
  `workflow.AsAIAgent()`: al suo interno `optimist` e `skeptic` si alternano a turni.
- `summarizer` riceve il dibattito e produce una raccomandazione finale.

L'API chiave è `workflow.AsAIAgent(...)`: un'orchestrazione diventa un `AIAgent`, quindi
può essere passata a `AgentWorkflowBuilder.BuildSequential` come una qualsiasi altra.

## Come si esegue

```powershell
dotnet run --project MAF/D2.NestedOrchestration
# oppure con una domanda tua:
dotnet run --project MAF/D2.NestedOrchestration -- "Conviene adottare il pair programming a tempo pieno?"
```

Prerequisito: un collector OTLP su `http://localhost:4317`.

## Da provare

Passa la domanda come argomento dopo `--`:

- `Dovremmo riscrivere il monolite in microservizi?` (default)
- `Conviene migrare tutta l'infrastruttura su Kubernetes?`
- `Ha senso introdurre la settimana lavorativa di 4 giorni nel team?`
- `Dovremmo rendere obbligatorio il code review su ogni commit?`

Nell'output noti i tre `stage:` dell'orchestrazione esterna; dentro lo stage
`debate-room` vedi i turni alternati dei due agenti interni.
