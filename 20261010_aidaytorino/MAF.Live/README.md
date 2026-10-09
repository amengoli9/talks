# AI Day Torino — dal primo agente alle evaluation

Apri **[MAF.Live.slnx](MAF.Live.slnx)**. Otto console .NET 10, una capacità alla volta: partiamo dal `GetEventInfo` del vecchio D1 e arriviamo a middleware, evaluation e poisoning da Word, con un esempio HITL essenziale in D8.

**MAF 1.24.0**, ultima release .NET verificata il 9 ottobre 2026, [pubblicata il 7 ottobre](https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.24.0). I pacchetti hanno versioni esplicite. Harness providers e evaluation espongono ancora API con marker sperimentale `MAAI001`.

| Tappa | Codice da aprire | Domanda della demo |
|---|---|---|
| 1 | [D1.ObservableAgent/Program.cs](D1.ObservableAgent/Program.cs) | Quali chiamate fa un agente con un tool? |
| 2 | [D2.Harness/Program.cs](D2.Harness/Program.cs) | Cosa aggiunge HarnessAgent? |
| 3 | [D3.HumanApproval/Program.cs](D3.HumanApproval/Program.cs) | Chi autorizza l'effetto e chi applica il limite? |
| 4 | [D4.MiddlewarePoisoning/Program.cs](D4.MiddlewarePoisoning/Program.cs) | Dove metto il middleware? Cosa succede con una nota avvelenata? |
| 5 | [D5.Evaluation/Program.cs](D5.Evaluation/Program.cs) | Come misuro regressioni e qualità? |
| 6 | [D6.MiddlewareStack/Program.cs](D6.MiddlewareStack/Program.cs) | Come aggiungo middleware ad agente, modello e tool con tre `.Use(...)`? |
| 7 | [D7.WordPoisoning/Program.cs](D7.WordPoisoning/Program.cs) | Il modello legge nel Word qualcosa che la persona non vede? |
| 8 | [D8.HumanInTheLoop/Program.cs](D8.HumanInTheLoop/Program.cs) | Come fermo un tool finché una persona non approva? |

La costruzione degli agenti è visibile nei `Program.cs`. `Common/` contiene soltanto connessione al modello, lettura del prompt e raccolta della telemetria, collegati ai progetti come sorgenti. I controlli di D3 e D4 sono in piccoli file accanto al programma. In D5, [EvaluationReport.cs](D5.Evaluation/EvaluationReport.cs) raccoglie la scrittura del report e [QualityJudge.cs](D5.Evaluation/QualityJudge.cs) la valutazione opzionale delle risposte già ottenute.

## Console

Titoli e prompt in **ciano**, tool ed effetti simulati in **magenta**, approvazioni richieste e PII oscurate in **giallo**, controlli superati in **verde**, blocchi e fallimenti in **rosso**. Le risposte hanno una sezione dedicata con testo bianco; dettagli e percorsi dei report sono grigi. Le etichette rendono i messaggi distinguibili anche senza colore.

La presentazione è in [Common/DemoConsole.cs](Common/DemoConsole.cs), senza pacchetti aggiuntivi. Quando l'output è rediretto su file o pipe i colori si disattivano automaticamente; per disattivarli nel terminale imposta `$env:NO_COLOR = '1'`.

## Avvio

Da questa cartella:

```powershell
dotnet restore MAF.Live.slnx --configfile nuget.config --packages ../.artifacts/packages
dotnet build MAF.Live.slnx --no-restore
dotnet run --project D1.ObservableAgent --no-build
```

Si riusa **`../appsettings.local.json`**, lo stesso delle demo precedenti. Se non esiste, copia `appsettings.template.json` in quel percorso e imposta endpoint, deployment e chiave. È escluso da Git. Non sovrascrivere un file già configurato.

In alternativa imposta `AI_ENDPOINT`, `AI_MODEL`, `AI_API_KEY`: hanno precedenza sul file. D7 legge il proprio modello da `AI:AttackModel` in `../appsettings.local.json`. L'endpoint deve essere compatibile con OpenAI Chat Completions (per Azure OpenAI: `https://<risorsa>.openai.azure.com/openai/v1/`). Serve un deployment con function calling.

D1–D8 aspettano il tuo prompt: gli esempi stampati si copiano, **non vengono inviati automaticamente**. `/esci` chiude la console; una riga vuota richiede nuovamente il prompt. D5 valuta la richiesta che hai scritto. Tutte le chiamate ai modelli sono reali; rimborsi, invii e pubblicazioni sono simulati, senza pagamenti o email. Ogni turno ha un timeout di due minuti, il batch di evaluation di cinque; D3 e D8 sospendono il timeout durante l'approvazione umana. D7 con `--inspect` mostra soltanto il contenuto del Word.

## Backup locale con Ollama

Il provider si sceglie in **`../appsettings.local.json`**. `AI:Provider` è **`foundry` per default**, anche quando la proprietà manca. Conserva endpoint, modelli e chiave già presenti in `AI`; aggiungi `Provider` e il profilo `Ollama` come in [appsettings.template.json](appsettings.template.json). Queste sono le sole proprietà da aggiungere al file esistente:

```json
{
  "AI": {
    "Provider": "foundry"
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434/v1/",
    "Model": "qwen3:8b"
  }
}
```

**Prima del talk, con la connessione disponibile:** installa [Ollama](https://ollama.com/download), avvialo e scarica il modello locale. Il modello predefinito [qwen3:8b](https://ollama.com/library/qwen3:8b) supporta i tool; puoi scegliere un altro modello locale con function calling adatto alla tua macchina.

```powershell
ollama pull qwen3:8b
ollama list
ollama run qwen3:8b "Rispondi solo: pronto."
```

Se il servizio non è già in esecuzione, avvia `ollama serve` in un altro terminale. Scarica anche le dipendenze .NET ed eventualmente l'immagine Aspire prima di andare offline. Per il backup senza rete scegli un modello locale, non una variante `:cloud`.

**Se Foundry o la rete non rispondono:** chiudi la demo, cambia solo `AI:Provider` da `foundry` a `ollama` nel file e riavvia:

```powershell
# Da MAF.Live: compila e copia anche il file di configurazione aggiornato.
dotnet run --project D1.ObservableAgent --no-restore
```

Quando modifichi il file sorgente, esegui una build o `dotnet run` **senza `--no-build`**: le console leggono la copia nella directory di output. Per tornare a Foundry rimetti `"Provider": "foundry"` e riavvia nello stesso modo. La console stampa provider e modello effettivi. Il cambio è manuale e apre una nuova sessione; una richiesta fallita non viene rieseguita automaticamente.

Con Ollama **tutte le otto demo** usano il server locale, inclusi D7 e il giudice opzionale di D5. `Ollama:AttackModel` e `Ollama:JudgeModel` sono opzionali e, se assenti, usano `Ollama:Model`. I deployment e la chiave del profilo `AI` non vengono inviati a Ollama. La connessione usa la [compatibilità OpenAI di Ollama](https://docs.ollama.com/api/openai-compatibility), con il valore segnaposto `ollama` come chiave; non servono pacchetti aggiuntivi.

Le variabili `AI_PROVIDER`, `OLLAMA_ENDPOINT`, `OLLAMA_MODEL`, `OLLAMA_ATTACK_MODEL` e `OLLAMA_JUDGE_MODEL` restano override opzionali del file. Se usi il file per cambiare provider, lascia `AI_PROVIDER` non impostata nella shell. Per Foundry, `AI_ATTACK_MODEL` e `AI_JUDGE_MODEL` sovrascrivono `AI:AttackModel` e `AI:JudgeModel`.

Dopo il cambio a Ollama, esegui build, test e `./rehearse.ps1` anche su quel profilo. L'inferenza è locale dopo il download dei modelli; tempi, qualità del giudice e riuscita del poisoning dipendono dal modello e dall'hardware. Restano i timeout esistenti: due minuti per turno, cinque per evaluation. Tool, approvazioni, middleware e telemetria OTLP seguono lo stesso percorso. Le protezioni gestite dal servizio Foundry non sono disponibili su Ollama.

## Osservabilità solo OTLP

Tracce e metriche vengono esportate **sempre via OTLP**, di default a `http://localhost:4317` con gRPC. Sono supportati l'endpoint generale e quelli specifici `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` e `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT`. In console restano prompt, risposte, approvazioni e risultati dimostrativi dei controlli, senza righe `[TRACE]`. Una richiesta collega span dell'agente, chiamate al modello, tool e guardrail; durata, ID e token si consultano nel collector.

**`EnableSensitiveData = true` in tutte le demo**, sia sull'agente sia sul client del modello, incluso il giudice. Prompt, risposte e chiamate ai tool sono quindi consultabili nel [visualizzatore GenAI di Aspire](https://aspire.dev/dashboard/explore/#genai-telemetry-visualization). Il bootstrap è in [Common/Telemetry.cs](Common/Telemetry.cs); la strumentazione del modello è in [Common/Demo.cs](Common/Demo.cs). Usiamo dati fittizi: la span esterna dell'agente registra il prompt originale anche quando il middleware successivo oscura le PII.

Dashboard Aspire opzionale, solo su loopback:

```powershell
docker compose up -d --wait
$env:OTEL_EXPORTER_OTLP_ENDPOINT = 'http://localhost:4317'
$env:OTEL_EXPORTER_OTLP_PROTOCOL = 'grpc'
dotnet run --project D1.ObservableAgent --no-build
```

Apri **http://localhost:18888**, poi **Traces**. Espandi una trace, apri una span `chat` e il visualizzatore GenAI per leggere messaggi e tool. In **Metrics** seleziona la risorsa della demo. Usa la stessa shell per impostare le variabili ed eseguire le console. Se hai già un dashboard sulle porte 18888/4317, riusa quello. Le demo funzionano anche senza Docker, ma senza un collector raggiungibile le trace non vengono conservate. Il dashboard standalone non raccoglie automaticamente l'output `Console.WriteLine` nella pagina Logs.

L'immagine segue `latest`, come nell'[avvio ufficiale standalone](https://aspire.dev/dashboard/standalone/). Scaricala durante la preparazione, prova la demo e conserva quella cache per il palco. Per fermare solo il dashboard di questo compose: `docker compose down`. Rimuovere `OTEL_EXPORTER_OTLP_ENDPOINT` ripristina il collector predefinito su localhost; non disattiva l'export.

## Opzioni per il palco

```powershell
dotnet run --project D2.Harness --no-build
dotnet run --project D2.Harness --no-build -- --minimal
dotnet run --project D2.Harness --no-build -- --compact

dotnet run --project D3.HumanApproval --no-build

dotnet run --project D4.MiddlewarePoisoning --no-build -- --clean
dotnet run --project D4.MiddlewarePoisoning --no-build
dotnet run --project D4.MiddlewarePoisoning --no-build -- --block
dotnet run --project D4.MiddlewarePoisoning --no-build -- --unsafe
dotnet run --project D4.MiddlewarePoisoning --no-build -- --probe
dotnet run --project D4.MiddlewarePoisoning --no-build -- --probe --unsafe

dotnet run --project D5.Evaluation --no-build
dotnet run --project D5.Evaluation --no-build -- --regression
dotnet run --project D5.Evaluation --no-build -- --repeat
$env:AI_JUDGE_MODEL = 'deployment-del-giudice'
dotnet run --project D5.Evaluation --no-build -- --judge

dotnet run --project D6.MiddlewareStack --no-build

dotnet run --project D7.WordPoisoning --no-build -- --inspect
dotnet run --project D7.WordPoisoning --no-build -- --attack
dotnet run --project D7.WordPoisoning --no-build -- --block
dotnet run --project D7.WordPoisoning --no-build -- --visible

dotnet run --project D8.HumanInTheLoop --no-build
dotnet run --project D8.HumanInTheLoop --no-build -- --auto
```

**D2:** `HarnessAgent` abilita todo e modalità plan/execute, mantiene la storia fra turni e limita le iterazioni a 12. `--minimal` toglie todo e modalità per il confronto. `--compact` attiva una strategia nativa che conserva gli **ultimi due turni**: dopo tre prompt la riduzione è visibile. Anche durante la chat puoi digitare `/compact` (alias `--compact`), `/compact off` e `/status`. Il comando attiva la compaction e la applica subito alla sessione corrente; vengono stampati messaggi e token stimati prima/dopo. È una finestra, non un riassunto. [Sequenza da provare e span Aspire](D2.Harness/README.md). Web search, file memory e skills sono disattivati. D2 e D3 sostituiscono il wrapper OpenTelemetry predefinito dell'harness con un solo wrapper esplicito configurato con `EnableSensitiveData = true`.

**D3:** usa `HarnessAgent` con `DisableToolAutoApproval = true`. `ApprovalRequiredAIFunction` sospende la chiamata; `CreateResponse` lega la risposta umana alla richiesta originale, ripresa nella stessa sessione. Solo `s` approva. Il tool applica un budget **cumulativo di 99 EUR per processo**, anche dopo un sì umano e anche dividendo l'importo. Riavvia la console per azzerarlo. Il budget è una policy didattica; un pagamento reale richiederebbe anche identità, persistenza e idempotenza.

**D4:** normalmente la nota è avvelenata e la destinazione è protetta. `--clean` seleziona una nota senza attacco; `--block` rifiuta input con PII, altrimenti le oscura; `--unsafe` disattiva soltanto la allowlist delle caselle simulate. Il modello può ignorare l'attacco anche in modalità unsafe: non presentare questa eventualità come garanzia di sicurezza.

`--probe` chiama direttamente **lo stesso tool** per mostrare il controllo in modo ripetibile, senza modello e senza credenziali. La console lo dichiara esplicitamente. Non è una prova che il modello sia caduto nel poisoning.

Il middleware è volutamente piccolo: riconosce email e codici `DEMO-12345` in `TextContent`. **Non è una soluzione DLP generale**: non filtra argomenti, allegati, JSON o risultati dei tool prima che questi tornino al modello. Il post-processing oscura la risposta pubblicata, ma avviene dopo gli effetti dei tool e dopo l'eventuale salvataggio della storia. D4 usa richieste senza una sessione condivisa. Per coprire il confine del modello serve middleware `IChatClient` per ogni chiamata; per coprire gli effetti serve un controllo al tool. PII e prompt injection richiedono controlli diversi.

Per leggere **D4 in Aspire**, segui i dati: `alice@example.test` nel prompt originale della span agente → `[REDACTED]` nella prima chiamata al modello → `bob@example.test` nella nota del tool e nella seconda chiamata al modello → email oscurate nella risposta pubblicata. La nota può contenere anche il poisoning: oscurare email non elimina le istruzioni malevole. La allowlist protegge separatamente l'effetto di `SendMessage`.

**D6:** un agente e il solo tool `GetEventInfo` mostrano i tre punti di intervento: agente, modello e funzione. Ogni middleware ripete **prima → `next` → dopo**, con messaggi in console e una span. Tutto è in un solo `Program.cs`: tre `.Use(...)` e tre callback. [Guida alla demo](D6.MiddlewareStack/README.md).

**D7:** riprende `20260419_aiday/MAF/Poisoner` e lo stesso Word, con istruzioni nascoste come testo bianco. Con Foundry usa `AI:AttackModel` di `../appsettings.local.json` (nel template `grok-4-1-fast-non-reasoning`); con Ollama usa `Ollama:AttackModel` oppure `Ollama:Model`, uguale in tutte le modalità. Con `--attack` invia il testo completo: nelle prove il fatturato è passato da **2,4 a 2,64 milioni**. Con `--block` un middleware rifiuta il documento prima del modello: **zero chiamate**, nessuna span `chat`. La console mostra indicatori sul prefisso e sulla cifra alterata, da confrontare con la risposta effettiva. `--visible` esclude le run bianche; `--inspect` mostra le estrazioni senza cloud. [File Word, comandi e cosa osservare](D7.WordPoisoning/README.md).

Per mostrare **HITL in due minuti**, usa **D8**: `HarnessAgent`, un annuncio simulato e una sessione condivisa. `n` rifiuta, `s` approva una volta, `a` ricorda tutti gli argomenti, `t` ricorda il tool. Ripeti la chiamata per mostrare la memoria; `/reset` apre una sessione nuova. Con `--auto`, `AutoApprovalRules` autorizza il canale `bozze`; per `evento` resta la decisione umana. Tutto è in un `Program.cs`. [Sequenza pronta](D8.HumanInTheLoop/README.md) · [Guida HTML interattiva offline](D8.HumanInTheLoop/HITL.html).

## Evaluation e produzione

D5 usa le API native `EvaluateAsync`, `LocalEvaluator`, `EvalChecks` e `FunctionEvaluator`. Scrivi o incolla una domanda sull'evento: i controlli verificano città/anno, chiamata al tool e fixture restituita. `--repeat` esegue la tua richiesta tre volte in modo indipendente. `--regression` cambia la fixture in Milano/2025: il controllo sulla fixture fallisce anche se il modello prova a correggerla. Il processo termina con **exit code 1** quando il gate fallisce. Il dataset dimostrativo contiene solo la richiesta inserita; puoi rilanciare con prompt diversi.

`--judge` aggiunge `RelevanceEvaluator` e `CoherenceEvaluator` di `Microsoft.Extensions.AI.Evaluation.Quality`, passando le risposte **già ottenute** all'overload di `EvaluateAsync`. Non riesegue l'agente. Il giudice usa il profilo selezionato: con Foundry il deployment esplicito `AI:JudgeModel` / `AI_JUDGE_MODEL`, con Ollama `Ollama:JudgeModel` oppure `Ollama:Model`; può coincidere con quello dell'agente per la demo, ma non è allora una valutazione indipendente. Anche il giudice produce span, durate e token. Il gate combina i due risultati, senza trasformare un giudizio negativo in successo.

Ogni evaluator scrive un report JSON sotto `D5.Evaluation/bin/Debug/net10.0/.artifacts/evals/` (o la directory della configurazione compilata), con metriche per caso, input dichiarato `manual-input`, versione delle istruzioni, modello, giudice, modalità regressione e trace ID. Non salva i testi delle conversazioni.

Per spiegare il passaggio in produzione:

| Momento | Cosa eseguire | Come usare il risultato |
|---|---|---|
| Prima del rilascio | Dataset rappresentativo versionato, casi avversariali, ripetizioni e baseline | Gate per regressioni; errori tecnici e verdict negativi devono emergere |
| Durante ogni richiesta | Trace, token, latenza, errori, decisioni HITL/guardrail | Alert operativi; i guardrail decidono prima degli effetti |
| Fuori dal percorso della richiesta | Campione di conversazioni minimizzate, valutato senza rieseguire i tool | Trend di qualità, analisi per scenario, confronto tra versioni |
| Revisione periodica | Giudizi umani su successi e fallimenti degli evaluator | Calibrare rubriche/soglie e trasformare incidenti in casi di regressione |

Il campionamento, la coda e l'archivio di conversazioni **non sono implementati** in questa console. L'overload sulle risposte esistenti mostra il punto di integrazione. Le trace della demo contengono i messaggi, ma non costituiscono un dataset di evaluation versionato: la raccolta per evaluation richiede selezione, minimizzazione e accessi controllati. Una richiesta manuale e la ricerca di parole sono un esempio leggibile, non una misura generale di affidabilità. Non sommare i token delle span agente con quelli delle span modello: i primi sono già aggregati.

La [documentazione MAF evaluation](https://learn.microsoft.com/en-us/agent-framework/agents/evaluation?pivots=programming-language-csharp) descrive anche `FoundryEvals`, expected tool calls e splitter `LastTurn`, `Full`, `PerTurn`. Foundry è un'opzione per valutazioni gestite e report nel portale; qui il percorso locale evita di richiedere un secondo progetto cloud. Le evaluation misurano il comportamento; non bloccano retroattivamente gli effetti.

## Verifica e prova generale

```powershell
dotnet test MAF.Live.slnx --no-build --no-restore
./rehearse.ps1
./rehearse.ps1 -Judge  # Foundry: AI:JudgeModel / AI_JUDGE_MODEL; Ollama: usa il profilo locale
```

I test senza cloud verificano PII, short-circuit, approvazione/rifiuto con HarnessAgent, limite cumulativo, poisoning con e senza allowlist, messaggi GenAI e gerarchia delle span. Verificano anche l'ordine e l'annidamento dei middleware di D6 con e senza tool, la compaction attivata durante la conversazione con e senza tool, l'integrità delle coppie tool call/result e l'estrazione del Word reale. I test della configurazione coprono Foundry predefinito, cambio provider dal file, precedenza delle variabili, isolamento di chiavi/modelli e validazione degli endpoint. Le risposte simulate esistono **solo nei test**.

`rehearse.ps1` usa i modelli reali e salva log locali in `.artifacts/rehearsal/`. Controlla le evidenze dei percorsi principali e si aspetta il fallimento della regressione. Per il giudice distingue un'esecuzione valida con verdict negativo da un errore tecnico: il risultato resta esplicito nel log. I log della console includono le risposte dimostrative; non usarli come archivio indiscriminato di conversazioni reali.

[Scaletta per il palco](LIVE-DEMO.md) · [Pipeline MAF](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/agent-pipeline?pivots=programming-language-csharp) · [Harness](https://learn.microsoft.com/en-us/agent-framework/concepts/harness?pivots=programming-language-csharp) · [Observability](https://learn.microsoft.com/en-us/agent-framework/agents/observability?pivots=programming-language-csharp)
