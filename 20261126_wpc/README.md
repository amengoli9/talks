# BabyLog · Supply chain di un’app .NET

BabyLog registra poppate, cambi e sonno di una neonata fittizia. Lo stesso host ASP.NET Core .NET 10 serve Blazor Server e l’API usata dai componenti. I controller chiamano il servizio di dominio attraverso una porta, il repository EF Core scrive su SQLite e il frontend usa un adapter HTTP. L’architettura riprende quella della demo di Zagabria.

Per avviare l’app servono PowerShell 7, SDK .NET 10.0.401 e Docker in modalità container Linux. Dalla cartella della sessione:

```powershell
dotnet restore BabyLog.slnx --locked-mode
dotnet run --project BabyLog
```

Apri [BabyLog](http://127.0.0.1:5080). Per vedere le richieste HTTP usa `requests.http`. Per la demo in container, arresta l’app avviata con `dotnet run` per liberare la porta 5080.

```powershell
./scripts/build-images.ps1
docker compose up -d --no-build
# Passa alla variante Chiseled sullo stesso volume SQLite:
docker compose -f compose.yaml -f compose.chiseled.yaml up -d --no-build
```

`babylog:standard` e `babylog:chiseled` condividono un solo publish. Chiseled Extra include ICU e tzdata per l’italiano e il fuso di Roma. Le immagini girano come UID 1654. SQLite e le chiavi Blazor persistono in `/data`. Se NuGet segnala `UntrustedRoot` nel builder, usa `./scripts/build-images.ps1 -UseHostTrust` su Windows oppure `-TrustedCaBundle <file.pem>`. BuildKit usa il bundle durante il restore tramite secret. [Dettagli dei container](containers/README.md)

## Prova locale

```powershell
./rehearse.ps1
# Su questa postazione, se il proxy non è riconosciuto dai container:
./rehearse.ps1 -UseHostTrust -UseLocalOsvSnapshot
```

Lo script esegue restore con lockfile, test, build delle due immagini, smoke test, generazione e validazione SPDX, generazione CycloneDX, rilevamento delle vulnerabilità e importazione in Dependency-Track. Usa `-SkipBuild` per riutilizzare le immagini prodotte dal build e `-SkipMonitoring` per ripetere la parte di CI. Le evidenze finiscono in `.artifacts/`. Per ripetere il solo confronto applicativo con Dependency-Track già avviato e configurato, esegui `./scripts/demo-vulnerability.ps1 -Upload`.

| Evidenza | File |
| --- | --- |
| Immagini, UID e dimensioni Docker | `.artifacts/containers/images.json` |
| SPDX Microsoft e verifica hash | `.artifacts/sbom/microsoft/`, `.artifacts/sbom/microsoft-validation.json` |
| CycloneDX applicazione e container | `.artifacts/sbom/manifest.json` e file `.cdx.json` |
| NuGet Audit, Trivy e gate High/Critical | `.artifacts/security/` |
| Confronto BabyLog 1.0.0 e 1.1.0 | `.artifacts/vulnerability/result.json` |
| Importazioni, finding e notifica | `.artifacts/dependency-track/` |

Il 18 novembre 2026 i 20 test .NET, i due smoke test e le interazioni Blazor su desktop e mobile sono passati. Trivy ha rilevato 97 pacchetti OS nella standard e 10 nella Chiseled, con 37 e 7 finding OS rispettivamente. Il gate High/Critical è passato per entrambe. Docker ha misurato 270,5 e 202,6 MB non compressi. I conteggi cambiano con le basi e il database delle vulnerabilità.

Il workflow [BabyLog supply chain](../.github/workflows/babylog-supply-chain.yml) richiama la stessa prova con `-SkipMonitoring` e conserva SBOM, report e risultati degli smoke test anche se il gate fallisce. La configurazione è stata validata in locale con actionlint. L’importazione in Dependency-Track usa le SBOM scaricate dagli artefatti del workflow.

## Sequenza sul palco

1. Registra una poppata e un riposo. Mostra che il riepilogo resta disponibile dopo il cambio della variante del container.
2. Apri la SBOM SPDX del publish e confronta l’inventario CycloneDX applicativo con i componenti delle due immagini. Confronta dimensioni, pacchetti OS e finding nella stessa sessione di Trivy.
3. Apri [Dependency-Track](http://localhost:8090) e confronta le due versioni di `BabyLog/app`. La 1.0.0 usa EF Core SQLite 10.0.10 e include `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, a cui è associato un finding High. La 1.1.0 usa EF Core SQLite 10.0.15 e risolve SQLitePCLRaw 2.1.13. [Advisory GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)

Per mostrare anche il monitoraggio a SBOM invariata, esegui `./scripts/demo-monitoring.ps1`. Il replay assegna un advisory interno sintetico dopo l’importazione. Lo script confronta gli hash della SBOM e verifica il nuovo finding e la notifica locale. [Integrazione CI/CD di Dependency-Track](https://docs.dependencytrack.org/usage/cicd/)

[Runbook Dependency-Track](docs/dependency-track.md) · [Strumenti SBOM e vulnerability scanning](docs/security-tools.md) · [Piano della sessione](PLAN.md)

## Demo dopo la sessione

[BabyLog distribuito](Distributed/README.md) usa una solution separata con due servizi esagonali, RabbitMQ e autenticazione Keycloak. Lo script demo mostra il ritardo del riepilogo durante un’interruzione del broker e il recupero al riavvio.
