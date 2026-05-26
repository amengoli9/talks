# D7 — Human in the loop

Mostra l'**human-in-the-loop** come funzionalità di prima classe dell'agente:
un tool sensibile non può essere eseguito senza l'approvazione esplicita di un umano.

## Cosa mostra

Un assistente bancario con due tool:

| Tool | Tipo | Comportamento |
|------|------|---------------|
| `get_account_balance` | normale | viene eseguito automaticamente |
| `transfer_money` | `ApprovalRequiredAIFunction` | il run **si ferma** e chiede approvazione |

Quando l'agente vuole chiamare `transfer_money`, la `RunAsync` non esegue il tool ma
restituisce un `ToolApprovalRequestContent`. Il programma mostra all'umano cosa l'agente
sta per fare, chiede `Y/N` sulla console, e rimanda la decisione all'agente con
`request.CreateResponse(approved)`:

- **Y** → il tool viene eseguito, l'agente conferma l'operazione.
- **N** → il tool non viene eseguito, l'agente reagisce di conseguenza.

## Come si esegue

```powershell
dotnet run --project MAF/D7.HumanInTheLoop
```

Il programma esegue tre richieste; per le due che attivano il bonifico, **digiti tu**
`Y` o `N` sulla console.

Prerequisito: un collector OTLP su `http://localhost:4317`.

## Da provare

Modifica i prompt passati a `Ask(...)` in `Program.cs`, oppure durante l'esecuzione
rispondi diversamente alle richieste di approvazione:

- `Quanto ho sul conto?` → nessuna approvazione (tool sicuro)
- `Trasferisci 500 euro a Mario Rossi` → approva con `Y` → bonifico eseguito
- `Trasferisci 1000 euro a Luca Bianchi` → rifiuta con `N` → bonifico annullato

Prova anche a chiedere due bonifici in un'unica frase: l'agente genera due richieste di
approvazione e le approvi una per una.
