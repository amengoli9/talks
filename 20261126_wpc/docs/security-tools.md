# Strumenti per la demo

| Strumento | Scope | Output o ruolo |
| --- | --- | --- |
| [Microsoft SBOM Tool](https://github.com/microsoft/sbom-tool) | File del publish e dipendenze della build | SPDX 2.2 validato. Il tool supporta anche SPDX 3.0 |
| [Microsoft.Sbom.DotNetTool](https://www.nuget.org/packages/Microsoft.Sbom.DotNetTool) e [Microsoft.Sbom.Targets](https://www.nuget.org/packages/Microsoft.Sbom.Targets) | Integrazione del motore Microsoft | CLI da manifest locale oppure task MSBuild |
| [Microsoft Component Detection](https://github.com/microsoft/component-detection) | Rilevamento dipendenze | Motore di rilevamento usato da SBOM Tool |
| [GitHub Dependency Graph](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/establish-provenance-and-integrity/export-dependencies-as-sbom) | Grafo del repository | Export SPDX da UI o API |
| [Microsoft Defender for Cloud](https://learn.microsoft.com/en-us/azure/defender-for-cloud/agentless-code-scanning) | Repository connessi | Inventario SBOM interrogabile tramite agentless scanning |
| [CycloneDX .NET](https://github.com/CycloneDX/cyclonedx-dotnet) | Pacchetti NuGet risolti | CycloneDX 1.6 con PURL, importato in Dependency-Track |
| [Trivy](https://trivy.dev/docs/latest/target/container_image/) | Immagini finali e SBOM | CycloneDX per l’inventario, JSON per i finding |
| [Syft](https://github.com/anchore/syft) e [Grype](https://github.com/anchore/grype) | Immagini, filesystem e inventari | Alternativa con generatore SBOM e scanner distinti |

NuGet Audit controlla pacchetti diretti e transitivi. Il gate analizza le severità nel report JSON. Trivy applica la stessa soglia High/Critical alle immagini e salva i report anche quando il gate fallisce. GitHub Dependency Review controlla le modifiche al grafo nelle PR. Azure DevOps offre dependency scanning tramite GitHub Advanced Security. Defender per Container valuta immagini in registry supportati. [NuGet Audit](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages) · [Dependency Review](https://docs.github.com/en/code-security/concepts/supply-chain-security/dependency-review) · [Azure DevOps](https://learn.microsoft.com/en-us/azure/devops/repos/security/github-advanced-security-dependency-scanning) · [Defender per Container](https://learn.microsoft.com/en-us/azure/defender-for-cloud/enable-vulnerability-assessment)

La pipeline usa Microsoft SBOM Tool 4.1.5, CycloneDX .NET 6.3.1 e Trivy 0.76.2. I primi due sono fissati nel manifest .NET, Trivy nel riferimento OCI con digest. Il database Trivy viene aggiornato prima della scansione e riutilizzato per entrambe le immagini. La versione dell’immagine locale è associata al suo image ID. Un eventuale push in un registry produrrà un digest di distribuzione da registrare a parte.
