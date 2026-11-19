# BabyLog — piano della sessione sulla supply chain .NET

Piano della sessione, preparato il 17 novembre 2026. Per avvio, verifica e risultati dell’implementazione, vedi [README](README.md).

Vincoli confermati: frontend e backend nello stesso host ASP.NET Core e nella stessa immagine applicativa, inizialmente; due varianti dell'immagine, standard e Chiseled. Tutto il percorso deve essere implementato e provato localmente prima di creare GitHub Actions, inclusi i template YAML. Account Git/GitHub da usare: `amengoli9`, non `AlessandroMengoliSacmi`.

Ipotesi iniziali: sessione di 60 minuti; GitHub Actions come futura CI, successiva alla validazione locale; esecuzione della demo e Dependency-Track sul computer del relatore; percorso principale senza servizi Azure a pagamento. Le alternative Microsoft cloud sono illustrate e predisposte come estensioni, non come prerequisiti.

1. **Risultato e storia della demo**

   BabyLog è un diario quotidiano per un solo neonato fittizio. Una pagina mostra nome, età, riepilogo della giornata e cronologia. Tre azioni: registrare una poppata (latte in ml), un cambio pannolino e un intervallo di sonno. Gli eventi si possono eliminare per correggere un inserimento. Il riepilogo contiene latte totale, numero di cambi e durata del sonno.

   Dati dimostrativi deterministici, etichette italiane, date e orari italiani. Nessun suggerimento medico. Funzioni volutamente limitate: un profilo, una timeline, tre form. Un'eventuale esportazione JSON è riservata al laboratorio sulla dipendenza vulnerabile se serve a rendere reale l'uso del pacchetto scelto.

   Storia: l'app funziona; scopriamo cosa distribuiamo; blocchiamo una dipendenza vulnerabile; confrontiamo le immagini; correggiamo e ricostruiamo; continuiamo a monitorare la versione già rilasciata.

2. **Architettura allineata al progetto di Zagabria**

   Riferimento verificato: `../20261104_atd_zagreb`, soprattutto Orders.Api e Kitchen.Api. Usa progetti host con cartelle Controllers, Domain, Infrastructure; servizi e porte nel Domain; adapter HTTP/EF in Infrastructure; composizione in ServiceExtensions.cs. Come Orders.Api nel riferimento, BabyLog ospita sia Blazor sia i controller API nello stesso processo ASP.NET Core. Un solo publish output, un solo servizio Compose e una sola immagine applicativa per variante.

   ```text
   BabyLog.slnx
   BabyLog/
     Components/                  # Blazor con interattività Server
     Contracts/                   # DTO HTTP nello stesso progetto
     Controllers/                 # adapter in ingresso HTTP
     Domain/                      # modelli, servizi e porte repository/gateway
     Infrastructure/
       Data/                      # EF Core e SQLite
       ActivityRepository.cs
       HttpBabyLogGateway.cs      # UI -> API dello stesso host, come nel riferimento
     ServiceExtensions.cs
     Program.cs
   BabyLog.Tests/
   containers/                    # Dockerfile e configurazioni comuni
   security/                      # policy, fixture e inventario strumenti
   scripts/                       # build, SBOM, scan, upload, smoke, confronto
   docs/                          # runbook e confronto dei tool
   compose.yaml
   compose.chiseled.yaml
   compose.security.yaml
   global.json
   Directory.Packages.props
   nuget.config
   requests.http
   rehearse.ps1
   ```

   Dipendenze: Controllers -> servizio di dominio -> porta repository <- adapter EF. UI -> porta gateway <- adapter HTTP -> API. Domain non dipende da EF Core, HttpClient o controller; Program resta il composition root. Manteniamo la separazione per cartelle del riferimento, senza moltiplicare i progetti per ogni layer.

   .NET 10, Blazor Server, ASP.NET Core Controllers, EF Core SQLite. Browser -> host BabyLog (Blazor + API) -> file SQLite persistente. UI e API condividono porta e origine; il gateway HTTP server-side usa l'indirizzo interno configurato dello stesso host. La porta pubblicata è associata al loopback per la demo locale e requests.http. Il backend resta l'autorità per validazione e riepiloghi. Nessun secondo processo o supervisore nel container.

   Contratti da congelare prima della delega: GET profilo, GET eventi per data, POST evento, DELETE evento, GET riepilogo, GET health. Identificativi stabili, errori ProblemDetails, valori positivi per il latte, intervallo di sonno valido. Memorizzazione UTC e riepilogo secondo Europe/Rome, verificando anche il cambio di giorno e gli intervalli che attraversano mezzanotte.

3. **Container standard e Chiseled**

   Due varianti della stessa immagine applicativa: BabyLog standard e BabyLog Chiseled, entrambe con backend e frontend. Build multistage: SDK nel builder; solo publish output nell'immagine finale. Stesso commit, stessi lockfile, stessa architettura linux/amd64 e stessa patch .NET per il confronto. Lo stack Dependency-Track resta separato dall'immagine applicativa.

   Basi proposte: `mcr.microsoft.com/dotnet/aspnet:10.0-noble` e `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra`. All'implementazione fissare patch e digest effettivi, incluso SDK, e registrare i riferimenti. La variante extra conserva ICU e tzdata per lingua italiana e fuso orario. È comunque Chiseled, senza shell e package manager. Fonti: [immagini .NET](https://learn.microsoft.com/en-us/dotnet/core/docker/container-images), [varianti ufficiali](https://github.com/dotnet/dotnet-docker/blob/main/documentation/image-variants.md).

   Entrambe le varianti eseguono come utente non root sulla porta 8080. Root filesystem in sola lettura dove verificato; volumi scrivibili per SQLite e chiavi Data Protection del frontend; directory temporanee esplicite. Drop delle capability e no-new-privileges. Probe HTTP eseguite dall'esterno: non installare curl o shell nella variante Chiseled.

   Validare SQLite nativo, permessi dei volumi, riavvio, SignalR, formattazione italiana e timezone in entrambe le varianti. Riutilizzare lo stesso output pubblicato di BabyLog per le due varianti dell'immagine.

   Report comparativo: digest, dimensione con metrica dichiarata, numero di componenti OS e applicativi, shell/package manager, utente, finding per severità. Usare stessa versione dello scanner e snapshot del database CVE. Controllare che lo scanner riconosca l'inventario Chiseled: zero pacchetti riconosciuti non significa immagine sicura. Non promettere zero CVE né un conteggio inferiore in ogni esecuzione.

4. **Opzioni SBOM Microsoft e GitHub**

   | Opzione | Ruolo e formato | Uso proposto |
   | --- | --- | --- |
   | Microsoft SBOM Tool | Generazione e validazione SPDX: 2.2 predefinito, 3.0 selezionabile | Prima opzione della demo Microsoft, sul publish output con contesto di build per progetto |
   | Microsoft.Sbom.DotNetTool / binario CLI | Modalità di distribuzione dello stesso motore | Tool manifest locale con versione fissata; alternativa binario verificato |
   | Microsoft.Sbom.Targets | Task e target MSBuild dello stesso motore | Variante documentata per integrare la generazione nel build/publish |
   | Microsoft Component Detection | Rilevamento delle dipendenze usato dall'ecosistema SBOM Tool | Spiegare il componente sottostante; non trattarlo come scanner CVE o SBOM di release completo |
   | GitHub Dependency Graph, export via UI/API | SBOM SPDX derivata dal grafo del repository | Confronto con SBOM della build; verificare dipendenze transitive e scope |
   | Defender for Cloud, agentless code scanning | Generazione inventario SBOM interrogabile; funzionalità documentata in Preview | Estensione cloud, con prerequisiti/licenza; verificare export e formato prima di qualsiasi collegamento a Dependency-Track |

   Fonti: [SBOM Tool](https://github.com/microsoft/sbom-tool), [MSBuild Targets](https://www.nuget.org/packages/Microsoft.Sbom.Targets/), [Component Detection](https://github.com/microsoft/component-detection), [export GitHub](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/establish-provenance-and-integrity/export-dependencies-as-sbom), [Defender agentless scanning](https://learn.microsoft.com/en-us/azure/defender-for-cloud/agentless-code-scanning).

   CLI, dotnet tool e Targets sono integrazioni dello stesso prodotto, non tre motori diversi. GitHub Actions e Azure Pipelines possono eseguirlo. Azure Artifacts/ACR conservano pacchetti e immagini; le attestazioni collegano evidenze agli artefatti: sono ruoli distinti dalla generazione SBOM. Il catalogo copre le opzioni pubbliche pertinenti a questa demo .NET/container; servizi firmware e strumenti interni Microsoft sono fuori perimetro.

5. **Alternative SBOM e scelta raccomandata**

   | Tool | Uso | Decisione |
   | --- | --- | --- |
   | CycloneDX .NET | BOM NuGet dirette/transitive per progetto e target ripristinato | Generatore CycloneDX per l'ingestione applicativa in Dependency-Track |
   | Trivy | Inventario/SBOM immagine e scansione vulnerabilità OS e dipendenze supportate | Percorso principale per le due varianti dell'immagine |
   | Syft + Grype | Syft genera SPDX/CycloneDX; Grype analizza immagini/SBOM | Alternativa riproducibile per confronto, fuori dal percorso live principale |

   Fonti: [CycloneDX .NET](https://github.com/CycloneDX/cyclonedx-dotnet), [Trivy](https://trivy.dev/docs/latest/target/container_image/), [Syft](https://github.com/anchore/syft), [Grype](https://github.com/anchore/grype).

   Produrre SPDX con Microsoft e CycloneDX nativo per Dependency-Track. Conservare distinti gli inventari di applicazione e container. Per app: un inventario BabyLog, prodotto nei due formati, con scope di runtime esplicito e separazione di test e tool di build. Per immagini: una BOM per variante dal filesystem finale, includendo OS, runtime e componenti riconosciuti. Verificare copertura con pacchetti noti e file .deps.json; gestire le lacune documentandole, senza fondere JSON alla cieca.

   Dependency-Track importa CycloneDX nel percorso documentato: l'output SPDX Microsoft non va caricato direttamente. Una conversione SPDX -> CycloneDX può essere un esercizio opzionale, verificando relazioni, purl e licenze, non il collegamento principale. Validare ogni BOM con schema compatibile con la versione Dependency-Track fissata.

6. **Vulnerabilità: opzioni Microsoft e percorso operativo**

   | Strumento | Copertura e collocazione |
   | --- | --- |
   | NuGet Audit | Audit dipendenze dirette/transitive durante restore; configurazione esplicita NuGetAuditMode=all e soglie CI |
   | dotnet package list --vulnerable --include-transitive | Report leggibile e JSON; il gate deve valutare il report, non presumere che il comando fallisca per la sola presenza di finding |
   | GitHub Dependabot e Dependency Review | Alert/aggiornamenti e controllo delle nuove dipendenze nelle PR; verificare disponibilità per repository/piano |
   | GitHub Advanced Security per Azure DevOps | Task di dependency scanning dopo restore/build, se si usa Azure Repos con licenza appropriata |
   | Defender for Cloud / Defender Vulnerability Management | Valutazione immagini in registri e ambienti supportati, come ACR; estensione cloud con prerequisiti e costi |
   | Trivy | Gate locale/CI sulle immagini finali, comprensivo di pacchetti OS e librerie riconosciute |

   Fonti: [NuGet Audit](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages), [Azure DevOps dependency scanning](https://learn.microsoft.com/en-us/azure/devops/repos/security/github-advanced-security-dependency-scanning), [Defender container assessment](https://learn.microsoft.com/en-us/azure/defender-for-cloud/enable-vulnerability-assessment).

   Policy iniziale: finding High/Critical bloccanti, inclusi quelli senza fix; eccezioni soltanto puntuali, motivate e con scadenza. Fallire separatamente per scanner non disponibile, feed irraggiungibile o report invalido. Conservare i report anche quando il gate fallisce. Non confondere SAST/CodeQL, scansione segreti e SCA: eventuali estensioni, non sostituti della scansione dipendenze.

7. **Pipeline locale prima delle GitHub Actions**

   Prima implementare script PowerShell ripetibili, invocabili singolarmente e tramite rehearse.ps1. Non creare workflow GitHub Actions o template YAML prima che la prova locale completa abbia dimostrato: app funzionante in entrambe le varianti, SBOM valide, scansioni con report, gate negativo sulla baseline vulnerabile, correzione verificata e import/analisi/persistenza in Dependency-Track. Conservare le evidenze locali. Le future Actions richiameranno gli stessi script già verificati.

   ```text
   sorgenti locali -> restore locked + audit -> build/test -> publish
       -> SPDX Microsoft + CycloneDX BabyLog
       -> build BabyLog standard/chiseled
       -> smoke test + SBOM immagini + scansione CVE
       -> report/policy
       -> artefatti locali + evidenze associate all'identità dell'immagine
       -> caricamento Dependency-Track e verifica completamento analisi
   ```

   Raccolta report sempre eseguita, anche in caso di errore. Un audit bloccante ferma la promozione della release; il percorso didattico separato può completare la raccolta evidenze per la baseline vulnerabile mantenendo l'esito di sicurezza negativo. Mai pubblicare quella baseline come release sicura. La pubblicazione in registry e i job CI saranno aggiunti soltanto dopo la validazione locale; localmente registrare image ID e hash degli artefatti, senza inventare un registry digest prima del push.

   Protezioni concrete: global.json, gestione centralizzata versioni, packages.lock.json e restore locked, fonti NuGet esplicite, tool/action fissati a versioni o commit verificati, basi container per digest, permessi minimi e segreti fuori dai file. PR esterne eseguono verifiche senza credenziali di pubblicazione. Aggiornamenti periodici di pacchetti e basi con nuova build e nuova scansione.

   Artifact naming: BabyLog, versione/commit, variante, identità immutabile dell'immagine; report con versioni di scanner e feed. Dopo pubblicazione, associare anche il registry digest. Nessun uso di latest come identità di una release. La futura scansione pianificata delle immagini già rilasciate usa i digest esistenti; l'aggiornamento dell'immagine base richiede una build distinta.

   Connettività iniziale: gli script locali producono gli artefatti e li caricano direttamente nel Dependency-Track locale. Solo nella fase CI successiva: un runner GitHub ospitato non raggiunge il localhost del relatore; scegliere import locale degli artefatti CI, runner dedicato nella stessa rete limitato ai workflow fidati, oppure endpoint Dependency-Track autenticato e raggiungibile. Non eseguire codice di PR esterne sul runner locale.

   Il repository Git è `D:/progetti/privale/talks`: nella fase successiva alla validazione locale, i workflow attivi andranno nella sua `.github/workflows`, con working-directory e filtri `20261126_wpc/**`. Un workflow sotto `20261126_wpc/.github/workflows` non viene scoperto. Il collocamento nella root richiederà accesso in scrittura fuori dal workspace attuale.

   Identità richiesta: `amengoli9` per Git/GitHub; non usare `AlessandroMengoliSacmi`. Prima di commit, push o operazioni GitHub verificare separatamente identità autore Git e account autenticato, incluse le credenziali usate dal trasporto Git. Usare nome/email verificati associati ad amengoli9, senza inventare l'email. Preferire configurazione mirata al repository o all'operazione, senza cambiare l'identità globale degli altri progetti. Nessun cambio di proprietario o remote implicito.

   Estensione supply chain consigliata se il tempo lo consente: attestazioni SBOM/provenienza e verifica del digest prima del deploy. Per l'alternativa Azure: [Notation con Artifact Signing e ACR](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-tutorial-sign-verify-notation-artifact-signing). Va mostrata anche la verifica, non solo la firma.

8. **Dependency-Track e monitoraggio continuo**

   Stack Compose dedicato: API server, frontend e PostgreSQL, con volumi persistenti, versioni fissate, healthcheck e porte locali distinte dall'app. La documentazione raccomanda 8 GB per il solo API server: verificare il budget Docker complessivo prima delle prove. Bootstrap ripetibile con password amministrativa cambiata e API key a privilegi minimi, senza stampare segreti. [Deploy ufficiale](https://docs.dependencytrack.org/getting-started/deploy-docker/).

   Tre progetti distinti: BabyLog/app, BabyLog/container-standard e BabyLog/container-chiseled. Le versioni applicative usano release+commit; quelle container riportano l'identità immutabile locale e, dopo pubblicazione, il registry digest completo nelle proprietà. Non sovrascrivere inventari applicativi e OS nello stesso progetto/versione. Evitare di sommare conteggi di finding duplicati tra queste viste.

   Upload CycloneDX con API, poll del token di elaborazione e verifica successiva della disponibilità dei risultati; un HTTP 200/201 non prova che analisi e policy siano terminate. Timeout esplicito e diagnostica. Dependency-Track rivaluta i componenti all'ingestione e automaticamente ogni giorno: la stessa release può acquisire nuovi finding senza rebuild. [Integrazione CI/CD](https://docs.dependencytrack.org/usage/cicd/).

   Configurare le fonti supportate dalla versione scelta, incluse NVD e advisory per NuGet; verificare chiavi, rate limit e sincronizzazione iniziale. Per OS e pacchetti distro mantenere Trivy e le sue fonti vendor come controllo dedicato; valutare l'analyzer Trivy di Dependency-Track soltanto dopo test di compatibilità. Non assumere identici conteggi tra scanner. [Analyzer ufficiali](https://docs.dependencytrack.org/analysis-types/known-vulnerabilities/).

   Configurare policy e notifica verso un ricevitore locale, poi mostrare triage e aggiornamento della release. Nessun invio reale a email o chat durante il setup. Prefetch dei feed prima della sessione; backup del DB e BOM/report già generati per il replay.

9. **Scenari e criteri di accettazione**

   - Funzionale: registro tutti e tre gli eventi, vedo riepilogo e timeline, correggo un evento, riavvio e ritrovo i dati. Stesso comportamento per entrambe le varianti.
   - Dipendenza vulnerabile: fixture/revisione esplicita con una versione realmente affetta da advisory verificato, usata da una funzione dimostrativa. Audit e scanner producono il finding atteso; upgrade alla versione corretta lo elimina. Le versioni saranno scelte e fissate durante l'implementazione, senza inventare CVE o forzare pacchetti inutilizzati nell'app principale.
   - Chiseled: confronto misurato delle due varianti dell'immagine; stessa vulnerabilità NuGet eventualmente presente in entrambe le basi; verifica di assenza shell e riduzione componenti.
   - Continuità: importo la baseline e la release corretta in Dependency-Track; vedo stato, policy e notifica. Per mostrare un advisory arrivato dopo la build, uso un replay dichiarato o advisory dimostrativo locale, mantenendo la BOM invariata. Non aspetto una nuova CVE reale sul palco.
   - Provenienza opzionale: verifica di attestazione valida e rifiuto di digest diverso.
   - Verifiche: test mirati su validazioni e riepiloghi, integrazione API/SQLite, smoke del container applicativo in entrambe le varianti e flusso UI, validazione BOM, finding noto, import asincrono e persistenza Dependency-Track. Tutte le prove sono eseguite localmente prima di creare le GitHub Actions.

10. **Scaletta indicativa da 60 minuti**

    | Minuti | Contenuto |
    | --- | --- |
    | 0–6 | App e confini della supply chain |
    | 6–13 | Architettura e container standard |
    | 13–25 | SBOM Microsoft, CycloneDX, differenza app/container |
    | 25–37 | Vulnerabilità, gate negativo e correzione |
    | 37–45 | Confronto standard/Chiseled |
    | 45–55 | Dependency-Track e rivalutazione di una release |
    | 55–60 | Takeaway e domande; firma come contenuto extra |

11. **Delega successiva: tutti i sottoagenti con modello gpt-6.1-sol**

    Prima dell'avvio, il coordinatore congela contratti HTTP, nomi progetti, porte, versioni, struttura output e policy. Ogni agente riceve questo piano e i file pertinenti come contesto; con l'override del modello si usa una sessione separata e un brief esplicito.

    | Pacchetto | Ownership | Dipendenze e consegna |
    | --- | --- | --- |
    | A — Backend | BabyLog/Controllers, Domain applicativo, adapter EF, test di dominio/API | Contratti congelati; API, SQLite, seed e test |
    | B — Frontend | BabyLog/Components, CSS, porta IBabyLogGateway e HttpBabyLogGateway | Contratti congelati; UI completa nello stesso host, stati errore/vuoto |
    | C — Container | containers, compose app, script build/smoke/confronto | Skeleton disponibile; una immagine applicativa in due varianti e verifica runtime locale |
    | D — SBOM e pipeline locale | security, script SBOM/scan | Output di C definito; Microsoft SPDX, CycloneDX, gate e report; nessun YAML GitHub Actions |
    | E — Dependency-Track | compose.security, bootstrap/upload/monitoring | Schema BOM e naming definiti; ingestione, policy e notifica locale |
    | Coordinatore | soluzione, BabyLog/Contracts, Program, ServiceExtensions, progetto e file comuni, docs, rehearse | Integrazione del singolo host, runbook e prova locale completa; Actions solo in fase successiva |

    Ordine: A+B+C in prima ondata; D+E dopo i rispettivi contratti di output, con il coordinatore sull'integrazione. Massimo tre sottoagenti attivi insieme. Nessuna modifica concorrente agli stessi file comuni: backend e frontend condividono il progetto, ma hanno ownership di file distinte. Verifica finale del coordinatore su entrambe le varianti e ripetizione completa della demo in locale. Solo dopo esito positivo, creare le Actions che richiamano gli script validati. Ogni brief deve riportare il vincolo dell'immagine unica, la precedenza delle prove locali e l'account amengoli9.

12. **Ambiente già verificato e punti da definire**

    Workspace inizialmente vuoto; SDK .NET 10.0.401 installato; motore Docker 28.3.2 raggiungibile. Non sono state create immagini, installati tool o avviati servizi. Tokensave non esposto tra i tool della sessione; database `.tokensave/tokensave.db` assente nel riferimento, perciò l'architettura è stata verificata con letture mirate.

    Durata ipotizzata: 60 minuti. GitHub Actions resta la CI futura, subordinata alla prova locale completa. Account richiesto amengoli9 registrato nel piano; configurazione Git e autenticazione non modificate in questa fase di pianificazione. Per il percorso cloud si dovranno identificare subscription, registry e licenze prima di provisionare risorse; il percorso locale resta autonomo.
