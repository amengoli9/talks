#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._+-]*$')][string]$Version = '1.0.0',
    [switch]$Images,
    [switch]$Upload,
    [string]$TrivyPath,
    [string]$DependencyTrackUrl = 'http://localhost:8091',
    [string]$CredentialsPath
)

. "$PSScriptRoot/../security/common.ps1"
if ($TrivyPath) { $TrivyPath = (Resolve-Path -LiteralPath $TrivyPath).Path }
$output = Join-Path $SecurityRoot '.artifacts/distributed-supply-chain'
$inventory = [System.Collections.Generic.List[object]]::new()
$errors = [System.Collections.Generic.List[string]]::new()

function Scan-Publish([string]$PublishPath, [string]$ReportPath) {
    Invoke-Checked $TrivyPath @('fs', '--cache-dir', (Join-Path $SecurityRoot '.artifacts/trivy-cache'),
        '--skip-db-update', '--scanners', 'vuln', '--format', 'json', '--output', $ReportPath, $PublishPath)
}

function Add-ApplicationEvidence([string]$Service) {
    $slug = $Service.ToLowerInvariant()
    $name = "BabyLog.Distributed/$slug"
    $project = "Distributed/$Service.Api/$Service.Api.csproj"
    $target = Join-Path $output $slug
    $publish = Join-Path $SecurityRoot ".artifacts/distributed-publish/$slug"
    $spdx = Join-Path $target 'spdx'
    $validation = Join-Path $target 'spdx-validation.json'
    $cdx = Join-Path $target "$slug.cdx.json"
    New-Item -ItemType Directory -Force $spdx | Out-Null

    Invoke-Checked dotnet @('publish', $project, '-c', 'Release', '--no-restore', '-o', $publish, '-p:UseAppHost=false')
    $componentPath = Join-Path $SecurityRoot (Split-Path -Parent $project)
    Invoke-Checked dotnet @('tool', 'run', 'sbom-tool', '--', 'generate', '-b', $publish, '-bc', $componentPath,
        '-m', $spdx, '-pn', $name, '-pv', $Version, '-ps', 'Organization: BabyLog demo',
        '-nsb', 'https://babylog.example/sbom/', '-mi', 'SPDX:2.2', '-D', 'true')
    if (Test-Path -LiteralPath $validation) { Remove-Item -LiteralPath $validation }
    Invoke-Checked dotnet @('tool', 'run', 'sbom-tool', '--', 'validate', '-b', $publish,
        '-m', (Join-Path $spdx '_manifest'), '-o', $validation, '-mi', 'SPDX:2.2', '-n', 'true')
    if ((Get-Content -LiteralPath $validation -Raw | ConvertFrom-Json).Result -ne 'Success') {
        throw "Validazione SPDX fallita per $Service."
    }

    New-BabyLogApplicationBom $project $Version $cdx $name
    $bom = Get-Content -LiteralPath $cdx -Raw | ConvertFrom-Json
    foreach ($package in 'RabbitMQ.Client', 'Microsoft.AspNetCore.Authentication.JwtBearer') {
        if (!($bom.components | Where-Object name -eq $package)) { throw "$name non contiene $package nella SBOM." }
    }
    $item = [ordered]@{
        Name = $name; Version = $Version; Scope = 'NuGet application'
        Bom = $cdx; BomSha256 = (Get-FileHash -LiteralPath $cdx).Hash
        Components = @($bom.components).Count; SpdxValidation = $validation
        NuGetAudit = 'non verificato'; Trivy = 'non richiesto'
    }
    $inventory.Add($item)

    try {
        $auditPath = Join-Path $target 'nuget-audit.json'
        $stderr = Join-Path $target 'nuget-errors.log'
        $raw = & dotnet package list --project $project --no-restore --vulnerable --include-transitive --format json 2> $stderr
        if ($LASTEXITCODE -ne 0) {
            throw "NuGet Audit fallito. Dettagli in $stderr"
        }
        $audit = ($raw -join [Environment]::NewLine) | ConvertFrom-Json
        $raw | Set-Content -LiteralPath $auditPath
        $auditErrors = @($audit.logs | Where-Object code -in 'NU1900', 'NU1905', 'NU1301')
        if ($auditErrors.Count) { throw "Feed advisory NuGet non verificato. Dettagli in $auditPath" }
        $packages = $audit.projects.frameworks | ForEach-Object { $_.topLevelPackages; $_.transitivePackages }
        $item.NuGetAudit = @($packages | ForEach-Object { $_.vulnerabilities } | Where-Object { $null -ne $_ }).Count
    } catch {
        $errors.Add("$($item.Name) - $($_.Exception.Message)")
    }

    if ($TrivyPath) {
        try {
            $item.Trivy = 'non verificato'
            $report = Join-Path $target 'trivy-publish.json'
            Scan-Publish $publish $report
            $item.Trivy = @(Read-TrivyFindings $report).Count
        } catch {
            $errors.Add("$($item.Name) - $($_.Exception.Message)")
        }
    }
}

function Add-ImageEvidence {
    $compose = (& docker compose -f Distributed/compose.yaml config --format json) | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Configurazione Compose non leggibile.' }
    Invoke-Trivy @('image', '--download-db-only')
    foreach ($service in 'activities', 'summaries', 'rabbitmq', 'keycloak') {
        $image = $compose.services.$service.image
        $target = ".artifacts/distributed-supply-chain/$service"
        New-Item -ItemType Directory -Force $target | Out-Null
        try {
            $imageId = & docker image inspect $image --format '{{.Id}}'
            if ($LASTEXITCODE -ne 0) { throw "Immagine locale mancante: $image" }
            $cdx = "$target/$service-image.cdx.json"
            $report = "$target/trivy-image.json"
            Invoke-Trivy @('image', '--image-src', 'docker', '--format', 'cyclonedx', '--output', "/work/$cdx", $imageId)
            Invoke-Trivy @('image', '--image-src', 'docker', '--skip-db-update', '--scanners', 'vuln',
                '--format', 'json', '--output', "/work/$report", $imageId)
            $findings = @(Read-TrivyFindings $report)
            $bom = Get-Content -LiteralPath $cdx -Raw | ConvertFrom-Json
            $inventory.Add([ordered]@{
                Name = "BabyLog.Distributed/$service-image"; Version = $Version; Scope = 'Container image'
                Image = $image; ImageId = $imageId; Bom = (Resolve-Path $cdx).Path; BomSha256 = (Get-FileHash $cdx).Hash
                Components = @($bom.components).Count; Trivy = $findings.Count
                HighCritical = @($findings | Where-Object Severity -in 'HIGH', 'CRITICAL').Count
            })
        } catch {
            $errors.Add("$service - $($_.Exception.Message)")
        }
    }
}

Push-Location $SecurityRoot
try {
    Invoke-Checked dotnet @('tool', 'restore')
    Invoke-Checked dotnet @('restore', 'Distributed/BabyLog.Distributed.slnx', '--locked-mode')
    foreach ($service in 'Activities', 'Summaries') { Add-ApplicationEvidence $service }
    if ($Images) {
        try { Add-ImageEvidence } catch { $errors.Add($_.Exception.Message) }
    }
    if (($TrivyPath -or $Images) -and (Test-Path -LiteralPath '.artifacts/trivy-cache/db/metadata.json')) {
        Copy-Item -LiteralPath '.artifacts/trivy-cache/db/metadata.json' -Destination (Join-Path $output 'trivy-db-metadata.json')
    }
    [ordered]@{ GeneratedAt = [DateTimeOffset]::UtcNow; ImagesRequested = [bool]$Images; Artifacts = $inventory; Errors = $errors } |
        ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $output 'manifest.json')

    if ($Upload) {
        foreach ($item in $inventory) {
            $parameters = @{ BomPath = $item.Bom; ProjectName = $item.Name; Version = $item.Version; ApiUrl = $DependencyTrackUrl }
            if ($CredentialsPath) { $parameters.CredentialsPath = $CredentialsPath }
            & "$SecurityRoot/scripts/upload-bom.ps1" @parameters
        }
    }
    if ($errors.Count) { throw "Report salvati in $output. $($errors -join ' | ')" }
} finally {
    Pop-Location
}
