# D12 - CodeAct via Hyperlight

Mostra il pattern **CodeAct**: invece di chiamare N tool uno alla volta, l'agente
**scrive un programma Python** e lo esegue in un **sandbox Hyperlight**
(micro-VM con isolation hardware). Provider-owned tools sono raggiunti dal
sandbox con `call_tool(...)` in un singolo `execute_code`.

Pacchetto: `Microsoft.Agents.AI.Hyperlight` (preview), guest Python via
`Hyperlight.HyperlightSandbox.Guest.Python`.

## Cosa fanno le due demo

1. **Pure interpreter** (DEMO 1): provider esposto senza tool. Domanda
   quantitativa di viaggio -> l'agente scrive Python, lo manda al sandbox,
   stampa il risultato.
2. **Provider-owned tools** (DEMO 2): registra `get_weather`,
   `convert_c_to_f` e `book_trip`. Quest'ultimo e' wrappato in
   `ApprovalRequiredAIFunction`, quindi qualsiasi `execute_code` che potrebbe
   raggiungerlo richiede approvazione dell'umano.

Un middleware di function-invocation stampa **il codice Python generato** e lo
**stdout** restituito, cosi' che dal vivo si vede esattamente cosa fa CodeAct.

## Comando

```powershell
dotnet run --project MAF/D12.CodeAct
```

## Prerequisiti specifici di D12

- **Hardware virtualization**: KVM su Linux o **Windows Hypervisor Platform**
  (WHP) su Windows. Senza, la creazione del sandbox fallisce.
- Nessuna variabile d'ambiente richiesta: il guest Python e' risolto in
  automatico da `PythonGuestModule.GetModulePath()` del package
  `Hyperlight.HyperlightSandbox.Guest.Python`.

## Caveat onesto (per il talk)

`Microsoft.Agents.AI.Hyperlight` e' **preview**. La build qui passa con le
versioni verificate su nuget.org al momento della scrittura
(`Microsoft.Agents.AI.Hyperlight 1.6.2-preview.260521.1` + 
`Hyperlight.HyperlightSandbox.Guest.Python 0.4.0`). Al RUN serve **hardware
virtualization** (WHP / KVM): se il backend non e' disponibile sulla macchina
del palco, `execute_code` fallisce alla creazione del sandbox. Tieni come
backup video o usa il sample Python in `MAF/python/codeact/` come piano B.

## Cose da chiedere durante la demo

- *Pure interpreter*:
  - `Calcola il tempo di percorrenza Torino-Roma...` (gia' nel programma)
  - `Quanto pesa l'acqua in 12 bottiglie da 0.5 L?` (calcolo banale ma toglie l'illusione del modello)
- *Provider-owned tools*:
  - `Confronta meteo di Torino e Roma in C e F` (un'unica execute_code)
  - `Quale citta tra Torino, Roma, Milano e' la piu' calda? Restituisci una tabella` (orchestrazione di N call_tool in un colpo solo)
  - `Prenota il viaggio da Torino a Roma per il 2026-06-01` (scatta l'approvazione)

## Riferimenti

- [Hyperlight CodeAct - Microsoft Learn](https://learn.microsoft.com/en-us/agent-framework/integrations/hyperlight?pivots=programming-language-csharp)
- [Pattern CodeAct](https://learn.microsoft.com/en-us/agent-framework/agents/code_act)
