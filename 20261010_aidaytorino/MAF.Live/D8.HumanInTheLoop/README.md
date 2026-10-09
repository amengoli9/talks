# D8 — HITL con HarnessAgent

Apri **[HITL.html](HITL.html)** nel browser: guida offline con simulatore, `AutoApprovalRules`, consensi ricordati, planning e workflow. La pagina è didattica; la console usa il modello reale.

Tutto il flusso è in [Program.cs](Program.cs): un `HarnessAgent`, un tool simulato e una sessione condivisa tra i turni. MAF **1.24.0**. Riusa `../appsettings.local.json` oppure `AI_ENDPOINT`, `AI_MODEL`, `AI_API_KEY`.

## Due minuti sul palco

Dalla cartella `MAF.Live`, dopo restore e build:

```powershell
dotnet run --project D8.HumanInTheLoop --no-build
```

1. Incolla **Pubblica sul canale evento il testo esatto: Benvenuti ad AI Day Torino!**
2. Mostra tool e argomenti ancora in attesa. Rispondi **n**: zero pubblicazioni.
3. Ripeti il prompt, rispondi **a**: esegue e ricorda gli argomenti.
4. Ripeti lo stesso prompt: esegue senza chiedere.
5. Cambia il testo in **A domani!**: chiede di nuovo. Rispondi **n**.

Il contatore misura gli effetti del tool nel turno. Le pubblicazioni scrivono solo in console. La ripetizione dipende dagli argomenti effettivamente proposti dal modello: se cambia anche un solo argomento, è corretto chiedere ancora.

| Scelta | API | Portata |
|---|---|---|
| `s` | `CreateResponse(true)` | Una chiamata |
| `n`, Invio, altro input o fine input | `CreateResponse(false)` | Rifiuta questa chiamata |
| `a` | `CreateAlwaysApproveToolWithArgumentsResponse()` | Stesso tool con tutti gli argomenti identici, nella sessione |
| `t` | `CreateAlwaysApproveToolResponse()` | Stesso tool con qualsiasi argomento, nella sessione |

`/reset` apre una nuova sessione, cancellando storia e consensi ricordati. `/esci` chiude. Il timeout del modello è sospeso durante la decisione umana. L'app gestisce fino a tre passaggi manuali.

## Un minuto in più: AutoApprovalRules

```powershell
dotnet run --project D8.HumanInTheLoop --no-build -- --auto
```

1. **Pubblica sul canale bozze il testo esatto: Prova microfono.** `ApproveDrafts` restituisce `true`: il tool esegue senza domanda.
2. **Pubblica sul canale evento il testo esatto: Prova microfono.** Restituisce `false`: decide la persona. Rispondi **s** per mostrare che `false` non era un divieto.

Per un tool che richiede approvazione, l'harness:

1. Cerca un consenso ricordato nella sessione.
2. Se manca, valuta `AutoApprovalRules` in ordine; il primo `true` autorizza.
3. Se nessuna regola approva, chiede alla persona.

La callback esamina **nome del tool e canale**. `false` significa “questa regola non autorizza”: non vieta e non crea una regola negativa. I limiti obbligatori vanno applicati nel tool o nel servizio prima dell'effetto.

`DisableToolAutoApproval = false` abilita il middleware: senza `--auto` non ci sono callback automatiche, ma `a` e `t` funzionano. Con `true`, il requisito di `ApprovalRequiredAIFunction` rimane, mentre consensi ricordati e callback dell'harness sono disattivati. `MaxAutoApprovalIterations = 3` limita le continuazioni automatiche.

`/reset` non disabilita `--auto`: i consensi sono stato della sessione, la callback è configurazione dell'agente. La scelta `t` autorizza prima delle callback, anche con un canale diverso.

## Verifica

```powershell
dotnet test MAF.Live.slnx --no-build --no-restore
./rehearse.ps1 -HitlOnly
```

I test usano agente, callback e tool reali di D8, sostituendo solo il modello. Verificano decisioni singole, corrispondenza degli argomenti, isolamento tra sessioni e ritorno alla persona dopo `false`. La prova generale usa il modello configurato e salva log in `.artifacts/rehearsal/`.

In Aspire: `demo.hitl`, `human.approval`, `guardrail.auto_approval`, `tool.PublishAnnouncement`. La memoria può autorizzare senza valutare la callback.

Riferimenti: [approvazione dei tool](https://learn.microsoft.com/en-us/agent-framework/agents/tools/tool-approval) · [AutoApprovalRules 1.24.0](https://github.com/microsoft/agent-framework/blob/dotnet-1.24.0/dotnet/src/Microsoft.Agents.AI/Harness/ToolApproval/ToolApprovalAgentOptions.cs).
