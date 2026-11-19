[CmdletBinding()]
param(
    [string]$ApiUrl='http://localhost:8091',
    [ValidatePattern('^[A-Za-z0-9._-]+$')][string]$Version=('replay-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')),
    [int]$TimeoutSeconds=120
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $root '.artifacts/dependency-track'
New-Item -ItemType Directory -Force $dir | Out-Null
$bomPath=Join-Path $dir "monitoring-synthetic-$Version.cdx.json"
if(-not (Test-Path $bomPath)) {
    @{bomFormat='CycloneDX';specVersion='1.6';serialNumber=('urn:uuid:'+[guid]::NewGuid());version=1;metadata=@{component=@{type='application';name='BabyLog monitoring SYNTHETIC';version=$Version}};components=@(@{type='library';name='babylog-demo-component-SYNTHETIC';version='1.0.0';purl='pkg:generic/babylog-demo-component-synthetic@1.0.0';'bom-ref'='demo-component'})} | ConvertTo-Json -Depth 10 | Set-Content $bomPath
}
$beforeHash=(Get-FileHash $bomPath -Algorithm SHA256).Hash
& (Join-Path $PSScriptRoot 'upload-bom.ps1') -BomPath $bomPath -ProjectName 'BabyLog/monitoring-synthetic' -Version $Version -ApiUrl $ApiUrl -TimeoutSeconds $TimeoutSeconds | Out-Null
$c=Get-Content (Join-Path $root '.secrets/dependency-track.json') -Raw|ConvertFrom-Json
$token=Invoke-RestMethod "$ApiUrl/api/v1/user/login" -Method Post -ContentType 'application/x-www-form-urlencoded' -Body @{username=$c.username;password=$c.password}
$headers=@{Authorization="Bearer $token"}
function Api([string]$Method,[string]$Path,$Body) {
    $r=@{Uri="$ApiUrl/api/v1/$Path";Method=$Method;Headers=$headers;ContentType='application/json'}
    if($null -ne $Body) { $r.Body=ConvertTo-Json -InputObject $Body -Depth 25 -Compress }
    Invoke-RestMethod @r
}
$project=Api Get ('project/lookup?name=BabyLog%2Fmonitoring-synthetic&version='+[Uri]::EscapeDataString($Version))
$component=@(Api Get "component/project/$($project.uuid)")[0]
$id='BABYLOG-SYNTHETIC-LATE-001'
$vuln=$null
try { $vuln=Api Get "vulnerability/source/INTERNAL/vuln/$id" } catch { if($_.Exception.Response.StatusCode.value__ -ne 404){throw} }
if(-not $vuln) {
    $vuln=Api Put 'vulnerability' @{vulnId=$id;source='INTERNAL';title='SYNTHETIC REPLAY - advisory scoperto dopo la build';description='SIMULAZIONE DIDATTICA. Questo advisory locale non e una CVE reale e non descrive un difetto del software. Viene assegnato dopo import per dimostrare rivalutazione della stessa BOM.';recommendation='Solo demo: esaminare il finding e registrare il triage. Non distribuire questo advisory come intelligence reale.';severity='HIGH';created=[DateTime]::UtcNow.ToString('o');published=[DateTime]::UtcNow.ToString('o')}
}
function Analyze-Project {
    $task=Api Post "finding/project/$($project.uuid)/analyze" @{}
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $state=Api Get "event/token/$($task.token)"
        if(-not $state.processing){return $task}
        Start-Sleep -Seconds 2
    } while([DateTime]::UtcNow -lt $deadline)
    throw 'Timeout rivalutazione advisory sintetico.'
}
# Make reruns reproduce the transition without changing or reuploading the BOM.
try { $null=Api Delete "vulnerability/$($vuln.uuid)/component/$($component.uuid)" } catch { if($_.Exception.Response.StatusCode.value__ -ne 404){throw} }
$null=Analyze-Project
$transitionStarted=[DateTimeOffset]::UtcNow
$beforeFindings=Api Get "finding/project/$($project.uuid)"
$null=Api Post "vulnerability/$($vuln.uuid)/component/$($component.uuid)" @{}
$analysis=Analyze-Project
$findings=Api Get "finding/project/$($project.uuid)"
$synthetic=@($findings | Where-Object { $_.vulnerability.vulnId -eq $id })
if($synthetic.Count -eq 0){throw 'Finding sintetico non presente dopo assegnazione e rivalutazione.'}
# Verify a real notification from the assignment/policy event, not the publisher test.
$deadline=[DateTime]::UtcNow.AddSeconds(30)
do {
    $raw=(Invoke-WebRequest 'http://localhost:8092/notifications').Content
    if ($raw -is [byte[]]) { $raw = [Text.Encoding]::UTF8.GetString($raw) }
    $lines=@(($raw -split "`n") | Where-Object {$_ -match $project.uuid} | ForEach-Object { $_ | ConvertFrom-Json -DateKind String } | Where-Object { $_.body.notification.group -eq 'POLICY_VIOLATION' -and [DateTimeOffset]::Parse($_.receivedAt) -ge $transitionStarted })
    if($lines.Count -gt 0){break}
    Start-Sleep -Seconds 2
} while([DateTime]::UtcNow -lt $deadline)
$afterHash=(Get-FileHash $bomPath -Algorithm SHA256).Hash
if($beforeHash -ne $afterHash){throw 'La BOM e cambiata durante il replay.'}
$violations=Api Get "violation/project/$($project.uuid)"
@{label='SYNTHETIC REPLAY - not a real CVE';projectUuid=$project.uuid;version=$Version;vulnerabilityUuid=$vuln.uuid;advisoryId=$id;bomSha256Before=$beforeHash;bomSha256After=$afterHash;bomUnchanged=($beforeHash -eq $afterHash);analysisToken=$analysis.token;findingsBefore=@($beforeFindings);findingsAfter=@($findings);policyViolations=@($violations);matchingNotifications=$lines.Count;performedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 50|Set-Content (Join-Path $dir 'synthetic-monitoring.json')
$raw | Set-Content (Join-Path $dir 'notifications.jsonl')
if($lines.Count -eq 0){throw 'Finding verificato ma notifica reale non ricevuta. Consultare log e synthetic-monitoring.json.'}
Write-Host 'SYNTHETIC REPLAY verificato: stessa BOM, nuovo advisory INTERNAL HIGH, finding e notifica locale.'


