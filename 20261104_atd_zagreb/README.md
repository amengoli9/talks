# Perché la conferma del mio ordine è così lenta?

Una seconda soluzione indipendente, con **sei servizi .NET, RabbitMQ, Redis e PostgreSQL reali**, è disponibile in [ThirdPartyObservability](ThirdPartyObservability/README.md). Include integrazioni HTTP, worker asincroni, outbox transazionale, Collector OpenTelemetry, Aspire Dashboard, Prometheus, Grafana e dieci scenari; può essere eseguita insieme a questa demo.

Una demo .NET 10 e OpenTelemetry ambientata in un ristorante. Il cliente ordina dal menu, la cucina controlla le disponibilità e conferma la comanda. Quando la cache scade, il gestionale lento fa aspettare il cliente dieci secondi, anche se la risposta HTTP è 200.

Il browser è la parte visibile della storia: nove piatti, un pulsante **Order this dish** per ciascuno, attesa visibile e popup soltanto dopo la risposta. Nessun carrello, login o passaggio di checkout. La conferma significa che la cucina ha accettato l'ordine, non che il piatto è pronto.

## Avvio

Servono .NET 10, Visual Studio e la dashboard Aspire standalone già in esecuzione. Non servono AppHost, server database o account cloud. La cucina crea automaticamente un database SQLite locale `Kitchen.Api/kitchen.db` per salvare le comande accettate tramite Entity Framework Core.

1. Apri `OpenTelemetryDemo.slnx`.
2. Seleziona il profilo condiviso **Restaurant: Orders + Kitchen + Notifications** e premi F5. Se non compare, imposta `Kitchen.Api`, `Orders.Api` e `Notifications.Api` come progetti di avvio multipli.
3. Il browser apre **<http://localhost:5270/?demo=1>**, con menu e controlli del relatore nella stessa pagina.
4. Apri la dashboard a <http://localhost:18888>. I servizi sono `Zagreb.Orders`, `Zagreb.Kitchen` e `Zagreb.Notifications`.

La vista cliente, senza controlli e dati tecnici, è <http://localhost:5270/>. Il frontend è **Blazor con interattività Server**, ospitato da `Orders.Api`: componenti Razor, eventi C# e CSS locale, senza dipendenze frontend, font remoti o compilazione Node. I testi dell’interfaccia, le descrizioni, le conferme e i controlli demo sono in inglese; i nomi dei piatti restano italiani. Il menu comprende bruschetta, caprese, pizza Margherita, carbonara, lasagne, risotto ai funghi, parmigiana, tiramisù e cannoli, con layout a tre colonne su desktop, due su tablet e una su telefono.

La UI invoca via `HttpClient` gli stessi endpoint `/orders` e `/demo/*` usati da `requests.http`; il browser mantiene una connessione SignalR con Blazor.

Da tre terminali puoi usare `dotnet run --project Kitchen.Api`, `dotnet run --project Notifications.Api` e `dotnet run --project Orders.Api`. La cucina ascolta sulla porta **5271**, gli ordini e il menu sulla **5270**, le notifiche sulla **5272**. Gli endpoint di controllo esistono solo in `Development`, come nei profili di avvio. La query `demo=1` rende visibile il pannello; non è un meccanismo di autorizzazione.

I tre profili inviano OTLP/gRPC a `http://localhost:4317`. Questa è la porta di ricezione della telemetria, diversa dalla porta web 18888. Se la tua dashboard usa un'altra porta OTLP, aggiorna i tre `launchSettings.json` e riavvia. L'indirizzo della cucina è modificabile con `Kitchen__BaseUrl` sul processo ordini. Se cambi la porta dell'API ordini, configura anche `Orders__BaseUrl` (default `http://localhost:5270`), usato dal frontend Blazor per chiamarla.

Se non hai una dashboard avviata, `docker compose up -d` usa il `compose.yaml` incluso. Non avviarlo se 18888 o 4317 sono già occupate. La dashboard standalone visualizza telemetria, ma non avvia le API.

## La storia da raccontare

### Un errore reale del database, sempre sullo stesso piatto

Ogni span con stato `Error` esporta anche un evento `error`, con `error.message` e, se disponibili, `error.type` e `http.response.status_code`. Questo include gli span automatici SQL e HTTP. Le eccezioni catturate hanno inoltre eventi `exception` con tipo, messaggio e stack trace; i guasti di trasporto HTTP vengono registrati dalla strumentazione. In dashboard seleziona lo span e apri **Events**. Le risposte 400/409 per piatti sconosciuti o esauriti non sono guasti applicativi.

Con **1. Normal service**, ordina **Cannoli siciliani con ricotta e pistacchio**. Il nome supera i 32 caratteri ammessi dal vincolo SQLite `CK_AcceptedOrders_DishName_Length`: è il database a rifiutare l'INSERT. Gli altri piatti disponibili continuano a funzionare.

La cucina risponde **503** e Orders restituisce **502** al frontend. La comanda non viene salvata e Notifications non viene chiamata. Nella trace apri lo span EF Core `main` sotto `kitchen.accept_order`: il comando SQL fallisce con `SqliteException` (CHECK constraint failed); lo span della cucina registra anche la `DbUpdateException`. I log contengono l'eccezione interna e lo stack trace. In modalità intermittente un errore del gestionale può precedere il salvataggio: usa il servizio normale per mostrare direttamente il vincolo DB.

All'avvio, i database esistenti ricevono la colonna `DishName` e il vincolo senza cancellare le comande. Le righe precedenti hanno nome vuoto; per i nuovi ordini viene salvato il nome dal menu. Per SQLite il limite è un `CHECK` esplicito, perché la sola lunghezza dichiarata di una stringa non lo impone.

### Nuova chiamata dopo la cucina

`POST /orders` attende `POST /commands`, poi chiama `POST /notifications` su `Notifications.Api` (porta 5272). La notifica demo viene registrata nei log, senza inviare email o SMS. L'indirizzo si configura con `Notifications__BaseUrl`; il timeout è di 2 secondi. Un ordine rifiutato dalla cucina non genera notifiche.

Per provocare un errore reale, invia `POST http://localhost:5272/demo/error`, poi ordina dal menu: Notifications lancia `InvalidOperationException`, lo span `notification.accept_order` registra l'eccezione e lo stato Error, e Orders risponde **502**. Anche gli span `order.notify` e `order.confirm` registrano l'errore e l'eccezione. Con `/demo/slow` la notifica supera il timeout e Orders risponde **504**. Ripristina con `/demo/normal`. Questi controlli esistono solo in Development.

La cucina ha già salvato la comanda: la risposta di errore contiene `kitchenAccepted: true` e il frontend invita a contattare il cameriere senza ripetere l'ordine. Una risposta riuscita include `notificationSent: true`. Non ci sono retry automatici.

> «Sono al tavolo. Ordino una margherita. Non mi aspetto che sia già cotta: mi aspetto che il ristorante mi dica subito che ha ricevuto l'ordine.»

### 1. Un ordine normale

Premi **1. Normal service**, poi **Order this dish** sotto Margherita. Il popup conferma la ricezione. Chiudilo con **Got it** o Esc. Anche il risotto viene confermato rapidamente: il tempo di cottura non entra nella richiesta.

Apri una trace per orientare il pubblico: il browser chiama gli ordini, gli ordini chiamano la cucina. Non spiegare ancora tutte le righe di codice.

### 2. Il cliente aspetta, ma non ci sono errori HTTP

Premi **2. Simulate slowdown**, poi ordina ancora la stessa margherita. Il menu resta visibile e il contatore di attesa sale fino a circa dieci secondi. Solo dopo compare il popup. La risposta è ancora **200**.

> «I camerieri dicono che il sistema si blocca quando inviano una comanda. Guardando solo le risposte riuscite sembra tutto normale. Quanto stanno aspettando i clienti?»

In **Metrics**, servizio `Zagreb.Orders`, mostra `restaurant.order.duration` e `restaurant.orders`. Il contatore distingue gli esiti; la distribuzione della durata mostra le attese. `http.server.request.duration` fornisce anche la misura automatica HTTP. Poche richieste illustrano la distribuzione, non un percentile statisticamente rappresentativo. Puoi ripetere il pulsante di rallentamento e un ordine per aggiungere campioni lenti.

### 3. Segui proprio quell'ordine

Il pannello relatore mostra HTTP, durata e `traceId` dell'ultima richiesta. Usalo per aprire la trace in **Traces**:

```text
POST /orders                         Zagreb.Orders, automatico
  order.confirm
    POST                             HttpClient, automatico
      POST /commands                 Zagreb.Kitchen, automatico
        kitchen.accept_order
          kitchen.check_availability cache.hit=false
            availability.reload      circa 10 s, refresh.trigger=request
          main                       EF Core, SQLite: INSERT della comanda
    order.notify
      POST                             HttpClient, automatico
        POST /notifications            Zagreb.Notifications, automatico
          notification.accept_order
```

Con il frontend Blazor, sopra `POST /orders` compare anche lo span HTTP client della UI, nello stesso servizio `Zagreb.Orders`. Con `requests.http` la trace inizia direttamente dal server. Non usare le richieste `/_blazor` per cercare il singolo ordine: il pannello espone il trace ID dell'endpoint `/orders`.

Ogni comanda accettata genera uno span database automatico `main` sotto `kitchen.accept_order`, anche quando la disponibilità è già in cache. Aprilo per vedere `db.system=sqlite`, il comando SQL parametrizzato in `db.statement` e la durata della scrittura. I valori dei parametri non vengono esportati. I piatti esauriti e le richieste non valide non vengono salvati. Il pacchetto di strumentazione EF Core è attualmente beta, allineato alla serie OpenTelemetry 1.19 usata dalla demo ([documentazione](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.EntityFrameworkCore)).

Il database persiste tra i riavvii ed è escluso da Git. Per ripartire da zero, arresta la cucina e rimuovi `Kitchen.Api/kitchen.db` e gli eventuali file `kitchen.db-shm` e `kitchen.db-wal`. Puoi cambiare destinazione con `ConnectionStrings__Kitchen` (per esempio `Data Source=D:/demo/kitchen.db`). La demo usa `EnsureCreatedAsync` per lo schema iniziale.

> «L'API degli ordini sta aspettando la cucina. La cucina sta aspettando di ricaricare le disponibilità.»

### 4. I log spiegano la causa

Apri i log associati alla trace: la cache è scaduta, quindi quella richiesta ha dovuto ricaricare le disponibilità dal gestionale. Il log di reload riporta `Trigger=request` e `DelayMs=10000`. Non devi cercare timestamp simili in due console: la chiamata HTTP propaga il contesto e i log portano trace e span ID.

Un secondo ordine immediato sarà veloce: il primo ha riempito la cache. È un comportamento intenzionale, utile per raccontare un problema intermittente. **Simulate slowdown** fa scadere nuovamente la cache per ripetere la dimostrazione.

### 5. Correggi e verifica

Dopo l'ordine lento, premi **3. Apply fix** e ordina subito. Il gestionale continua a impiegare **dieci secondi**, ma un `BackgroundService` aggiorna la cache prima della scadenza. Gli ordini leggono lo snapshot ancora valido durante il caricamento.

Per rendere il confronto immediato, il controllo relatore porta la scadenza dello snapshot corrente a 20 secondi: il worker parte entro circa un secondo. Non carica dati finti al posto del risultato precedente e non azzera la lentezza del gestionale. Se la cache è già scaduta durante la spiegazione, il pulsante esegue prima un caricamento reale di dieci secondi (`refresh.trigger=warmup`), poi abilita il refresh: nessun click aggiuntivo.

In dashboard compare una trace separata `availability.reload` con `refresh.trigger=background`, sempre lunga circa dieci secondi. Gli ordini restano rapidi durante il refresh **e oltre i 20 secondi della vecchia scadenza**. Le nuove durate migliorano; i campioni lenti precedenti rimangono nella finestra delle metriche.

### 6. Un rifiuto non è un guasto

Ordina il **Tiramisù**: appare **Dish sold out**, HTTP **409**, metrica `outcome=unavailable` e log informativo. La cucina funziona, ma non può accettare quel piatto. Nessuna eccezione o stato di errore sugli span applicativi. Lo span HTTP client può segnare 409 come errore secondo la convenzione HTTP: distinguilo dall'esito di business e dagli span server.

## Servizio intermittente

Premi **4. Intermittent service** una sola volta, poi continua a ordinare Margherita o Risotto. Ogni richiesta sceglie indipendentemente un comportamento del gestionale:

| Probabilità | Comportamento | Risposta ordine |
| --- | --- | --- |
| 50% | Rapido, circa 50 ms | 200 e popup di conferma |
| 25% | Lento, ritardo casuale tra 2 e 4,5 secondi | 200 e popup dopo l'attesa |
| 25% | Gestionale temporaneamente non disponibile | Cucina 503, ordini 502, popup «Order not confirmed» |

Il ritardo simulato lascia margine sotto i cinque secondi per HTTP e interfaccia. In questa modalità ogni ordine verifica direttamente il gestionale simulato: la cache e il semaforo di refresh non nascondono gli esiti e non sommano le attese degli ordini concorrenti. Le percentuali sono probabilità, non una sequenza garantita. Il tiramisù resta esaurito quando la verifica riesce.

La trace contiene `availability.lookup` con `management.behavior` e `management.delay_ms`. Gli errori hanno span falliti, log con eccezione e metrica `outcome=error`. Puoi tornare agli altri scenari con un solo pulsante. L'equivalente HTTP è `POST /demo/intermittent`.

## Cosa è reale e cosa è simulato

- Reali: due API, chiamata HTTP, propagazione del contesto, cache con scadenza di 60 secondi, aggiornamento concorrente in background, salvataggio delle comande accettate con EF Core e SQLite, segnali OpenTelemetry e UI che aspetta la risposta effettiva.
- Simulati: lettura dal gestionale (`Task.Delay` cancellabile) e disponibilità fisse. Nessun ordine viene cucinato e le scorte non vengono decrementate. Il menu contiene nove piatti; il tiramisù è sempre esaurito.
- La correzione anticipa il refresh di 30 secondi e funziona per il ritardo simulato di 10 secondi. Se un caricamento supera la validità residua, le richieste tornano ad attendere: non serviamo uno snapshot già scaduto. Questa cache di disponibilità non sostituisce una prenotazione atomica delle scorte in un sistema reale.
- I controlli alterano lo stato condiviso della demo. Una sola persona deve pilotare gli scenari; i normali ordini concorrenti condividono il caricamento grazie a un semaforo.

## Codice e prove

| File | Cosa mostrare |
| --- | --- |
| `Orders.Api/Program.cs` | Conferma, chiamata HTTP alla cucina, metriche di esito e durata |
| `Kitchen.Api/Program.cs` | Accettazione della comanda e piatto esaurito |
| `Kitchen.Api/KitchenDbContext.cs` | Comande accettate salvate in SQLite e span EF Core automatici |
| `Kitchen.Api/AvailabilityCache.cs` | Cache miss, caricamento lento, correzione in background |
| `Common/Telemetry.cs` | ActivitySource, Meter, ILogger e tre pipeline OTLP |
| `Common/Menu.cs` | Menu condiviso e contratti minimi |
| `Orders.Api/Components/Pages/Home.razor` | Menu Blazor, chiamate HTTP, attesa e popup in C# |
| `Orders.Api/wwwroot/style.css` | Stile compatto e responsive |
| `requests.http` | Sequenza equivalente senza browser |
| `rehearse.ps1` | Verifica HTTP, trace ID, tempi, concorrenza e refresh oltre la vecchia scadenza |

```powershell
dotnet restore OpenTelemetryDemo.slnx
dotnet build OpenTelemetryDemo.slnx --no-restore
# Con entrambe le API già avviate:
./rehearse.ps1
```

La prova dura circa 45 secondi e ripristina lo scenario normale alla fine. Usa PowerShell 7. Le metriche custom sono emesse solo da Orders e hanno cinque esiti limitati: `accepted`, `unavailable`, `rejected`, `error`, `cancelled`. Nessun trace ID come dimensione. Le richieste JSON malformate vengono rifiutate da ASP.NET Core prima del contatore applicativo. Tutte le trace sono campionate, le metriche esportate ogni secondo, trace e log in batch: attendi qualche secondo prima di cercarli in dashboard.
