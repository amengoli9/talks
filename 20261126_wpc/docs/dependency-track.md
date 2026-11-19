# Dependency-Track in locale

```powershell
./scripts/start-dependency-track.ps1
./scripts/bootstrap-dependency-track.ps1
```

La UI è su [localhost:8090](http://localhost:8090), l’API su `localhost:8091` e le notifiche su `localhost:8092/notifications`. PostgreSQL e i dati di Dependency-Track usano volumi Compose persistenti. Il bootstrap cambia la password iniziale e salva le credenziali amministrative e la chiave del team di upload in `.secrets/dependency-track.json`. Leggi da lì la password per accedere come `admin`. La chiave separata ha i permessi di upload, creazione durante l’upload e lettura dei risultati.

```powershell
dotnet tool restore
./scripts/demo-vulnerability.ps1 -Upload
```

Il confronto usa due SBOM dello stesso progetto `BabyLog/app`. Lo script ripristina la 1.0.0 in `.artifacts/restore/babylog-1.0.0` con il suo lockfile e la usa per l’audit e l’inventario. La 1.1.0 è la versione corrente di `BabyLog/BabyLog.csproj`. Lo script verifica che l’advisory relativo a SQLite sia presente per la 1.0.0 e assente per la 1.1.0, sia in NuGet Audit sia in Dependency-Track.

La prova completa importa anche `BabyLog/container-standard` e `BabyLog/container-chiseled`. La policy High/Critical emette notifiche al ricevitore del laboratorio. Il replay opzionale `./scripts/demo-monitoring.ps1` verifica la notifica quando arriva un advisory interno a SBOM invariata.

Il feed OSV NuGet reale alimenta il monitoraggio applicativo. Su questa postazione il client Java ha incontrato un errore TLS verso OSV. PowerShell scarica lo snapshot originale con verifica TLS attiva, poi il ricevitore locale lo serve sulla rete Compose. Lo script registra orario e checksum. Per la prova dal vivo aggiorna lo snapshot prima della sessione. Se usi questo percorso per il monitoraggio quotidiano, pianifica lo stesso comando di refresh. L’analisi dei pacchetti Ubuntu resta in Trivy.

```powershell
./security/monitoring/refresh-osv.ps1
./scripts/bootstrap-dependency-track.ps1 -UseLocalOsvSnapshot
./security/monitoring/refresh-osv.ps1 -RestartApi
```

Per verificare la sincronizzazione, cerca nei log `OsvDownloadTask` e confronta i finding SQLite di `BabyLog/app` nelle due versioni. Il completamento del token dimostra l’elaborazione della SBOM. Lo stato aggiornato del feed va verificato separatamente. Il runner GitHub ospitato produce gli stessi artefatti, che puoi importare in questa istanza con `upload-bom.ps1`.

## Avvio standalone su Windows

Se Docker Desktop è indisponibile, puoi avviare Dependency-Track con il JAR bundled e un runtime Java portatile. La UI e l’API sono su [127.0.0.1:8190](http://127.0.0.1:8190/). I dati di questa istanza sono in `.artifacts/dependency-track-standalone/.dependency-track` e i log in `.artifacts/dependency-track-standalone/stdout.log`. [Distribuzioni Dependency-Track](https://docs.dependencytrack.org/getting-started/distributions/)

```powershell
./scripts/start-dependency-track.ps1 -Standalone
./scripts/bootstrap-dependency-track.ps1 -Standalone
(Get-Content .secrets/dependency-track-standalone.json -Raw | ConvertFrom-Json).password
```

Accedi come `admin` con la password mostrata dall’ultimo comando. Per usare lo snapshot locale NuGet, aggiungi `-UseLocalOsvSnapshot` ai due script. Lo script di avvio serve lo snapshot esistente sulla porta 8192 con Python. Dopo la prima configurazione del feed, riavvia Dependency-Track e controlla `OsvDownloadTask` nei log.
