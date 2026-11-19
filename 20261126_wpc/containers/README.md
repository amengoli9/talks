# Immagini BabyLog

`pwsh scripts/build-images.ps1` costruisce `babylog:standard` e `babylog:chiseled`
per linux/amd64 dallo stesso stage `publish`, condiviso in un unico grafo BuildKit.
Esporta gli stessi file in `.artifacts/publish`; conservare questa directory per
gli inventari applicativi. I log e gli image ID locali sono in
`.artifacts/containers`. `Size` in `images.json` misura i byte non compressi
riportati da Docker, non la dimensione del download o lo spazio incrementale.

Le basi sono fissate nel Dockerfile con digest manifest verificati il 17 novembre
2026. SDK `10.0.401-noble` disponibile; runtime standard e chiseled-extra entrambi
`10.0.15`. Il digest dell'indice è fissato e la piattaforma viene selezionata
esplicitamente dal comando di build. Nessun registry digest applicativo viene
inventato: gli ID locali sono riportati distintamente.

Se il proxy aziendale richiede una CA aggiuntiva, passare
`-TrustedCaBundle percorso.pem`, oppure su Windows `-UseHostTrust` per esportare
i certificati pubblici già attendibili dall'host. Il bundle è montato come secret
BuildKit nel solo restore; non viene copiato nelle immagini runtime. La verifica
TLS resta attiva. L'esportazione locale è in `.artifacts`, esclusa dal contesto.
Prova locale del 18 novembre 2026, circa 09:47 Europe/Rome: il comando normale ha riprodotto
`NU1301 / UntrustedRoot` due volte dopo un precedente restore riuscito senza
override. La build finale usa quindi `-UseHostTrust`; il comando predefinito
rimane senza CA aggiuntive. Il contesto Docker usa una allowlist di sorgenti
BabyLog, configurazione NuGet/SDK e definizioni container, escludendo fixture
security, strumenti locali, report e segreti.

Avvio standard: `docker compose up -d`. Avvio Chiseled:
`docker compose -f compose.yaml -f compose.chiseled.yaml up -d`.
Entrambi pubblicano solo `http://127.0.0.1:5080`, conservano SQLite e chiavi
Data Protection nel volume `babylog_babylog-data` e usano UID/GID 1654.
La directory iniziale del volume appartiene a questo utente nell'immagine:
nessun init container privilegiato è necessario per un volume nuovo.
Se si riutilizza un volume creato da un'altra applicazione, verificare prima
i permessi. `docker compose down` conserva i dati.

`pwsh scripts/smoke-containers.ps1` verifica entrambe le immagini su porta 5080
(configurabile con `-Port`), usando container e volumi con identificatori unici;
la pulizia riguarda solo quelle risorse. Richiede la porta libera.
`-KeepContainers` conserva le risorse per diagnostica e usa la porta successiva
per la seconda variante. Le evidenze sono salvate anche quando una prova fallisce.
Le chiavi salvate nelle evidenze appartengono esclusivamente ai volumi effimeri
di test e non devono essere riutilizzate come chiavi di produzione.

Il filesystem root è read-only; `/data` è persistente e `/tmp` è tmpfs.
Le capability sono rimosse e `no-new-privileges` è attivo.
Le probe HTTP vengono eseguite dall'host. Chiseled-extra mantiene ICU e tzdata
per italiano e Europe/Rome senza aggiungere shell o gestori pacchetti.
