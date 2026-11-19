[CmdletBinding()]
param([string]$ApiUrl='http://localhost:8091',[switch]$RestartApi)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$directory=Join-Path $root '.artifacts/dependency-track/osv/NuGet'
New-Item -ItemType Directory -Force $directory | Out-Null
$upstream='https://osv-vulnerabilities.storage.googleapis.com/NuGet/'
foreach($name in @('all.zip','modified_id.csv')) {
    $temp=Join-Path $directory ($name+'.tmp')
    Invoke-WebRequest ($upstream+$name) -OutFile $temp
    Move-Item -LiteralPath $temp -Destination (Join-Path $directory $name) -Force
}
@{upstream=$upstream;downloadedAt=[DateTime]::UtcNow.ToString('o');files=@(Get-ChildItem $directory -File | ForEach-Object {@{name=$_.Name;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash;bytes=$_.Length}});purpose='Unmodified complete upstream OSV NuGet snapshot. Local transport workaround; not synthetic data. Refresh before each demo or daily.'} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path (Split-Path $directory -Parent) 'provenance.json')
if($RestartApi) {
    docker compose --env-file (Join-Path $root '.secrets/dependency-track.env') -f (Join-Path $root 'compose.security.yaml') restart dtrack-api
    if($LASTEXITCODE -ne 0) {throw 'Riavvio Dependency-Track fallito.'}
}
Write-Host 'Snapshot OSV originale aggiornato. Provenienza e SHA256 in .artifacts/dependency-track/osv/provenance.json.'
