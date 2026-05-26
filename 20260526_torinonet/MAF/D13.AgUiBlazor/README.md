# D13 — AG-UI + Blazor

Demo: un **travel agent** esposto via il protocollo **AG-UI** (HTTP + Server-Sent Events) e consumato da una UI **Blazor Server**. Mostra come MAF disaccoppi il runtime dell'agente dal frontend usando un protocollo standard, e come l'**human-in-the-loop** (HITL) viene tunnellato sopra a quel protocollo.

## Struttura

```
D13.AgUiBlazor/
├── Server/                              <- runtime dell'agente (ASP.NET Core)
│   ├── Program.cs                       <- chat client, tools, MapAGUI("/ag-ui", agent)
│   ├── ApprovalModels.cs                <- payload "request_approval" condiviso col client
│   └── ServerFunctionApprovalAgent.cs   <- delegating agent per HITL
└── Client/                              <- Blazor Server UI
    ├── Program.cs                       <- AGUIChatClient -> AIAgent -> approvalClientAgent
    ├── ServerFunctionApprovalClientAgent.cs
    └── Components/Pages/Chat.razor      <- chat + activity log + modal di approvazione
```

I due `ServerFunctionApprovalAgent[ClientAgent]` sono **plumbing**: AG-UI nativamente non ha eventi per "richiesta di approvazione tool", quindi MAF tunnellizza il `ToolApprovalRequestContent` come una pseudo-tool-call `request_approval`. Codice copiato (e commentato in italiano) dai sample ufficiali `Step04_HumanInLoop`.

## Tools dell'agente

| Tool | Tipo | Cosa fa |
|------|------|---------|
| `get_weather(city)` | normale | finto meteo, restituito subito |
| `get_attractions(city)` | normale | finta lista di attrazioni |
| `book_trip(from, to, date)` | **ApprovalRequiredAIFunction** | richiede conferma utente prima di eseguire |

Quando l'LLM prova a chiamare `book_trip`, MAF emette un `ToolApprovalRequestContent`. Il delegating agent lo trasforma in una tool-call `request_approval` che viaggia su AG-UI. Il client la riceve, la rigenera come `ToolApprovalRequestContent`, e la UI mostra un modal "Approva / Rifiuta".

## Come si lancia

Due terminali:

```powershell
# terminale 1 — Server AG-UI
dotnet run --project MAF/D13.AgUiBlazor/Server
# -> http://localhost:5100/ag-ui

# terminale 2 — UI Blazor
dotnet run --project MAF/D13.AgUiBlazor/Client
# -> http://localhost:5101
```

Apri http://localhost:5101 nel browser. Servono `appsettings.json` con `AzureOpenAI` configurato in `MAF/` (come per tutti gli altri progetti).

## Prompt di esempio

- `Che meteo fa a Torino?` → tool semplice
- `Cosa visito a Roma?` → un altro tool semplice
- `Prenota un viaggio Torino → Roma per il 2026-06-15` → tool **sensibile**: appare il modal di approvazione. Approva → l'agente prosegue con il risultato. Rifiuta → l'agente reagisce alla negazione.
- `Che meteo a Milano e che attrazioni? Poi prenotami Milano-Venezia per il 2026-07-01` → due tool sicuri in fila e poi il modal.

## Cosa guardare durante la demo

1. **Streaming** della risposta dell'agente — testo che appare carattere per carattere via SSE.
2. **Activity log** sulla destra: ogni evento AG-UI (chunk, tool-call, tool-result, approval-req, approval-yes/no) arriva nel momento esatto in cui viene emesso.
3. **HITL modal**: al `book_trip` la UI si ferma, l'utente decide, e la stessa sessione AG-UI riparte da dove si era interrotta — senza ricreare l'agente.
4. **Reset session**: il pulsante "New chat" crea un nuovo `AgentSession` lato client.

## Punto del talk

Il backend agente **non sa nulla di Blazor**. Il client Blazor **non sa nulla del runtime dell'agente**: parla solo AG-UI. Domani il frontend può diventare React, Vue, Python o un'altra UI desktop, e il server resta identico. È lo stesso disaccoppiamento che si è visto storicamente con OpenAPI/REST, ma applicato a turni conversazionali + tool + HITL.
