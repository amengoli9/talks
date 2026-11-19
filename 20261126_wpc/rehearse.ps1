#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Version = 'local-20261126',
    [int]$SmokePort = 5082,
    [switch]$UseHostTrust,
    [switch]$UseLocalOsvSnapshot,
    [switch]$SkipBuild,
    [switch]$SkipMonitoring
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & dotnet restore BabyLog.slnx --locked-mode '-p:WarningsAsErrors=NU1900%3BNU1905'
    if ($LASTEXITCODE -ne 0) { throw 'Ripristino NuGet fallito.' }
    & dotnet test BabyLog.slnx -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Test .NET falliti.' }

    if (!$SkipBuild) { & ./scripts/build-images.ps1 -UseHostTrust:$UseHostTrust }
    & ./scripts/smoke-containers.ps1 -Port $SmokePort
    & ./scripts/install-security-tools.ps1
    & ./scripts/generate-sbom.ps1 -Version $Version

    $gateFailure = $null
    try { & ./scripts/scan-security.ps1 }
    catch { $gateFailure = $_; Write-Warning $_.Exception.Message }
    if (!$SkipMonitoring) {
        if ($UseLocalOsvSnapshot) { & ./security/monitoring/refresh-osv.ps1 }
        & ./scripts/start-dependency-track.ps1
        & ./scripts/bootstrap-dependency-track.ps1 -UseLocalOsvSnapshot:$UseLocalOsvSnapshot
    }

    & ./scripts/demo-vulnerability.ps1 -CurrentBomPath .artifacts/sbom/babylog-app.cdx.json -Upload:(!$SkipMonitoring)

    if (!$SkipMonitoring) {
        $inventory = Get-Content .artifacts/sbom/manifest.json -Raw | ConvertFrom-Json
        foreach ($entry in ($inventory | Where-Object Name -ne 'BabyLog/app')) {
            & ./scripts/upload-bom.ps1 -BomPath (Join-Path .artifacts/sbom $entry.Bom) -ProjectName $entry.Name -Version $entry.Version
        }
    }

    if ($gateFailure) { throw $gateFailure }
    Write-Host 'Prove completate. Evidenze in .artifacts.'
} finally { Pop-Location }
