$ErrorActionPreference = 'Stop'
$SecurityRoot = Split-Path $PSScriptRoot -Parent
$TrivyImage = 'aquasec/trivy:0.76.2@sha256:8f6bb947dbc528902e11f2f5be52f7b519f6a39d5d148f41166f2040499768fb'
function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE" }
}
function New-BabyLogApplicationBom([string]$ProjectPath, [string]$Version, [string]$BomPath, [string]$Name = 'BabyLog/app') {
    $output = Split-Path -Parent $BomPath
    New-Item -ItemType Directory -Force $output | Out-Null
    Invoke-Checked dotnet @('tool','run','dotnet-CycloneDX','--',$ProjectPath,'--disable-package-restore','--exclude-dev','--exclude-test-projects','--output-format','Json','--spec-version','1.6','--set-name',$Name,'--set-version',$Version,'-o',$output,'-fn',(Split-Path -Leaf $BomPath))
    $bom = Get-Content -LiteralPath $BomPath -Raw | ConvertFrom-Json
    if ($bom.bomFormat -ne 'CycloneDX' -or !($bom.components | Where-Object name -eq 'Microsoft.EntityFrameworkCore.Sqlite')) {
        throw 'Application BOM does not contain EF SQLite.'
    }
}
function Invoke-Trivy([string[]]$Arguments) {
    $cache = Join-Path $SecurityRoot '.artifacts/trivy-cache'
    New-Item -ItemType Directory -Force $cache | Out-Null
    $trust = $env:BABYLOG_TRUSTED_CA
    if (!$trust -and (Test-Path "$SecurityRoot/.artifacts/containers/host-trust.pem")) { $trust = "$SecurityRoot/.artifacts/containers/host-trust.pem" }
    $artifactRoot = Join-Path $SecurityRoot '.artifacts'
    $dockerArgs = @('run','--rm','-v',"${artifactRoot}:/work/.artifacts",'-v',"${cache}:/root/.cache/trivy",'-v','/var/run/docker.sock:/var/run/docker.sock')
    if($trust){ $dockerArgs += @('-v',"${trust}:/run/host-trust.pem:ro",'-e','SSL_CERT_FILE=/run/host-trust.pem') }
    Invoke-Checked docker ($dockerArgs + @($TrivyImage) + $Arguments)
}
function Read-TrivyFindings([string]$Path) {
    $report = Get-Content -Raw $Path | ConvertFrom-Json
    if ($report.SchemaVersion -ne 2) { throw "Invalid Trivy report: $Path" }
    @($report.Results | ForEach-Object { $class = $_.Class; $_.Vulnerabilities | Where-Object { $null -ne $_ } | ForEach-Object { [pscustomobject]@{ Class=$class; Id=$_.VulnerabilityID; Package=$_.PkgName; Installed=$_.InstalledVersion; Fixed=$_.FixedVersion; Severity=$_.Severity } } })
}
