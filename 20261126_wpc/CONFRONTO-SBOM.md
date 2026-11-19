# Come leggere gli inventari di BabyLog

Apri **Actions → BabyLog supply chain → Summary**. Il riepilogo confronta i pacchetti NuGet per nome e versione. Scarica l’artefatto **babylog-supply-chain** per consultare i documenti originali e la matrice completa.

| Strumento | Input effettivo | Che cosa trovi nel report |
|---|---|---|
| GitHub Dependency Graph | Grafo del branch predefinito di tutto `talks`, al momento dell’esportazione | Uno SPDX con le dipendenze che GitHub riconosce, comprese quelle di altri progetti. Durante una PR il grafo può riferirsi a una revisione diversa dal checkout. |
| Microsoft SBOM Tool | Directory del publish e directory sorgente `BabyLog` per il rilevamento dei componenti | Un manifest SPDX 2.2 con informazioni su file distribuiti, hash e componenti rilevati. Il workflow valida il manifest rispetto al publish. |
| CycloneDX .NET | Progetto `BabyLog.csproj` dopo il restore | Grafo dei pacchetti NuGet risolti, incluse le dipendenze transitive. La configurazione esclude progetti di test e dipendenze di sviluppo. |
| Syft | Filesystem finale delle immagini Docker `standard` e `chiseled` costruite dallo stesso publish | Un CycloneDX per variante con i componenti riconoscibili dai catalogatori, inclusi pacchetti di sistema e .NET. La disponibilità di metadati nel filesystem influenza ciò che viene identificato. |
| Grype | Ciascuno dei due SBOM prodotti da Syft | Advisory associati ai componenti inventariati. Il risultato dipende dalla versione del database delle vulnerabilità. |
| NuGet Audit | Dipendenze dirette e transitive del progetto | Advisory NuGet applicabili alle versioni risolte. |
| CodeQL | Sorgenti supportati in `20261126_wpc` | Risultati di analisi statica consultabili in GitHub Code scanning. L’analisi gira in un workflow separato. |

## Come confrontare i pacchetti

Apri `sbom/package-matrix.csv`. Ogni riga rappresenta una Package URL con versione. Un **1** indica che lo strumento ha identificato il pacchetto, uno **0** che non lo ha elencato con quella chiave. Il confronto normalizza maiuscole e minuscole degli identificatori NuGet e trascura qualificatori e sottopercorsi delle Package URL. Due build per architetture differenti possono quindi condividere la stessa riga. I record privi di un’identità confrontabile sono disponibili in `sbom/unmatched-identifiers.csv`.

Per confrontare Microsoft e CycloneDX filtra le righe `pkg:nuget/`. Per confrontare le immagini filtra le colonne Syft e cerca le righe presenti in una sola variante. Un pacchetto presente soltanto nel grafo GitHub potrebbe appartenere a un altro talk. Una differenza richiede di verificare input e metadati nei documenti originali.

## Come leggere le vulnerabilità

Per ogni immagine consulta `security/grype-standard.json` o `security/grype-chiseled.json`. Per il progetto consulta `security/nuget-audit.json`. Il gate distingue una scansione completata che ha trovato vulnerabilità High/Critical da un errore nell’esecuzione degli strumenti. Una CVE pubblicata dopo la build può comparire riesaminando il medesimo SBOM con un database aggiornato.
