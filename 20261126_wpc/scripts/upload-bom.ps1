[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BomPath,
    [Parameter(Mandatory)][string]$ProjectName,
    [Parameter(Mandatory)][string]$Version,
    [int]$TimeoutSeconds=300,
    [string]$ApiUrl='http://localhost:8091',
    [bool]$IsLatest=$true,
    [string]$CredentialsPath
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if (!$CredentialsPath) { $CredentialsPath = Join-Path $root '.secrets/dependency-track.json' }
$credentials=Get-Content -LiteralPath $CredentialsPath -Raw | ConvertFrom-Json
if (!$credentials.uploadApiKey) { throw "Chiave di upload assente in $CredentialsPath." }
$headers=@{'X-Api-Key'=$credentials.uploadApiKey}
$ApiUrl=$ApiUrl.TrimEnd('/')
$resolved=(Resolve-Path $BomPath).Path
$bom=Get-Content $resolved -Raw | ConvertFrom-Json
if ($bom.bomFormat -ne 'CycloneDX') { throw 'È richiesta una BOM CycloneDX JSON valida, non SPDX.' }
$hash=(Get-FileHash $resolved -Algorithm SHA256).Hash
$reportDir=Join-Path $root '.artifacts/dependency-track'
New-Item -ItemType Directory -Force $reportDir | Out-Null
$reportName=($ProjectName+'-'+$Version) -replace '[^a-zA-Z0-9._-]','-'
function Api([string]$Method,[string]$Path,$Body,[ref]$ResponseHeaders) {
    $requestArgs=@{Uri="$ApiUrl/api/v1/$Path";Method=$Method;Headers=$headers;ContentType='application/json';TimeoutSec=30}
    if ($null -ne $Body) { $requestArgs.Body=ConvertTo-Json -InputObject $Body -Depth 30 -Compress }
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            $response = Invoke-RestMethod @requestArgs -ResponseHeadersVariable receivedHeaders
            if ($ResponseHeaders) { $ResponseHeaders.Value = $receivedHeaders }
            return $response
        }
        catch [System.Net.Http.HttpRequestException] {
            if ($attempt -eq 5) { throw }
            Start-Sleep -Seconds (2 * $attempt)
        }
    }
}
function Wait-Token([string]$Token) {
    if (-not $Token) { throw 'Dependency-Track non ha restituito un token.' }
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $status=Api Get "event/token/$Token"
        if (-not $status.processing) { return }
        Start-Sleep -Seconds 2
    } while([DateTime]::UtcNow -lt $deadline)
    throw "Elaborazione asincrona oltre timeout ($TimeoutSeconds secondi). Token: $Token"
}
function Parse-DTrackDate($Value) {
    $epoch=0L
    if([long]::TryParse([string]$Value,[ref]$epoch)) { return [DateTimeOffset]::FromUnixTimeMilliseconds($epoch) }
    return [DateTimeOffset]::Parse($Value)
}
$uploadStarted=[DateTimeOffset]::UtcNow
$result=Api Put 'bom' @{projectName=$ProjectName;projectVersion=$Version;autoCreate=$true;isLatest=$IsLatest;bom=[Convert]::ToBase64String([IO.File]::ReadAllBytes($resolved))}
Wait-Token $result.token
$query='project/lookup?name='+[Uri]::EscapeDataString($ProjectName)+'&version='+[Uri]::EscapeDataString($Version)
$project=Api Get $query
$componentHeaders=$null
$componentResponse=Api Get "component/project/$($project.uuid)" $null ([ref]$componentHeaders)
$components=@($componentResponse | Where-Object { $null -ne $_ })
$componentCount=$components.Count
$total=0
if ([int]::TryParse([string]($componentHeaders['X-Total-Count'] | Select-Object -First 1), [ref]$total)) {
    $componentCount=$total
}
if (-not $project.lastBomImport -or $components.Count -eq 0 -or $componentCount -eq 0) { throw 'Token completato ma import/componenti non verificabili.' }
# An independent analysis token verifies the task beyond mere HTTP upload acceptance.
$analysis=Api Post "finding/project/$($project.uuid)/analyze" @{}
Wait-Token $analysis.token
$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    $project=Api Get $query
    if ($project.lastVulnerabilityAnalysis -and (Parse-DTrackDate $project.lastVulnerabilityAnalysis) -ge $uploadStarted.AddSeconds(-2)) { break }
    Start-Sleep -Seconds 2
} while ([DateTime]::UtcNow -lt $deadline)
if (-not $project.lastVulnerabilityAnalysis -or (Parse-DTrackDate $project.lastVulnerabilityAnalysis) -lt $uploadStarted.AddSeconds(-2)) { throw 'Analisi non confermata dal timestamp del progetto entro il timeout.' }
$findings=Api Get "finding/project/$($project.uuid)"
$metrics=Api Get "metrics/project/$($project.uuid)/current"
$report=@{projectName=$ProjectName;version=$Version;projectUuid=$project.uuid;bomPath=$resolved;bomSha256=$hash;componentCount=$componentCount;uploadToken=$result.token;analysisToken=$analysis.token;lastBomImport=$project.lastBomImport;lastVulnerabilityAnalysis=$project.lastVulnerabilityAnalysis;checkedAt=[DateTime]::UtcNow.ToString('o');metrics=$metrics;findings=$findings;feedCaveat='Completed tasks confirm import and analysis. Check the configured sources and mirror logs for advisory coverage.'}
$report | ConvertTo-Json -Depth 50 | Set-Content (Join-Path $reportDir "$reportName.json")
Write-Host "Import e analisi verificati: $ProjectName / $Version ($componentCount componenti)."
$report | Select-Object projectName,version,projectUuid,bomSha256,componentCount,lastBomImport,lastVulnerabilityAnalysis

