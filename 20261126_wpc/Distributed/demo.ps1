#Requires -Version 7.0
[CmdletBinding()]
param([switch]$BrokerOutage)

$ErrorActionPreference = 'Stop'
$activities = 'http://127.0.0.1:5180'
$summaries = 'http://127.0.0.1:5181'
$issuer = 'http://localhost:5182/realms/babylog'
$date = '2026-11-26'
$summaryUrl = "$summaries/api/summary?date=$date"

function Invoke-Compose([string[]]$ComposeArguments) {
    docker compose -f (Join-Path $PSScriptRoot 'compose.yaml') @ComposeArguments
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose ha restituito il codice $LASTEXITCODE." }
}

function Wait-For([string]$Description, [scriptblock]$Condition, [int]$Seconds = 60) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Seconds)
    do {
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Tempo scaduto: $Description."
}

function Test-Endpoint([string]$Url) {
    try {
        (Invoke-WebRequest $Url -TimeoutSec 3 -SkipHttpErrorCheck).StatusCode -eq 200
    } catch {
        $false
    }
}

function Get-Token([string]$ClientId, [string]$Secret) {
    $form = @{ grant_type = 'client_credentials'; client_id = $ClientId; client_secret = $Secret }
    (Invoke-RestMethod "$issuer/protocol/openid-connect/token" -Method Post -ContentType 'application/x-www-form-urlencoded' -TimeoutSec 10 -Body $form).access_token
}

function Assert-Status([string]$Url, [int]$Expected, [hashtable]$Headers = @{}, [string]$Method = 'Get') {
    $response = Invoke-WebRequest $Url -Method $Method -Headers $Headers -TimeoutSec 10 -SkipHttpErrorCheck
    if ($response.StatusCode -ne $Expected) {
        throw "$Method $Url ha restituito $($response.StatusCode), atteso $Expected. $($response.Content)"
    }
}

function Get-Summary {
    Invoke-RestMethod $summaryUrl -Headers $summaryHeaders -TimeoutSec 10
}

function Test-Summary([int]$Milk, [int]$Diapers, [int]$SleepMinutes) {
    $summary = Get-Summary
    $summary.milkMilliliters -eq $Milk -and $summary.diaperCount -eq $Diapers -and $summary.sleepMinutes -eq $SleepMinutes
}

function Add-Activity([hashtable]$Activity) {
    $request = @{
        Uri = "$activities/api/activities"
        Method = 'Post'
        Headers = $activityHeaders
        ContentType = 'application/json'
        Body = ($Activity | ConvertTo-Json -Compress)
        TimeoutSec = 10
        SkipHttpErrorCheck = $true
    }
    $response = Invoke-WebRequest @request
    if ($response.StatusCode -ne 201) { throw "Atteso 201 sulla POST. $($response.StatusCode) $($response.Content)" }
    ($response.Content | ConvertFrom-Json).id
}

Wait-For 'Activities API' { Test-Endpoint "$activities/health" }
Wait-For 'Summaries API' { Test-Endpoint "$summaries/health" }
Wait-For 'realm Keycloak' { Test-Endpoint "$issuer/.well-known/openid-configuration" } 120

$activityHeaders = @{ Authorization = "Bearer $(Get-Token 'babylog-activities-cli' 'babylog-activities-demo')" }
$summaryHeaders = @{ Authorization = "Bearer $(Get-Token 'babylog-summaries-cli' 'babylog-summaries-demo')" }
Assert-Status "$activities/api/baby" 401
Assert-Status $summaryUrl 401
Assert-Status "$activities/api/baby" 401 $summaryHeaders
Assert-Status $summaryUrl 401 $activityHeaders
Assert-Status "$activities/api/baby" 200 $activityHeaders
$before = Get-Summary
Write-Host 'Entrambe le API accettano il token con la propria audience e rifiutano gli altri.'

$brokerStopped = $false
$created = [System.Collections.Generic.List[string]]::new()
try {
    if ($BrokerOutage) {
        Invoke-Compose @('stop', '--timeout', '5', 'rabbitmq')
        $brokerStopped = $true
    }

    $milkId = Add-Activity @{ kind = 'milk'; startedAt = "$($date)T08:00:00+01:00"; milliliters = 120 }
    $created.Add($milkId)
    $created.Add((Add-Activity @{ kind = 'diaper'; startedAt = "$($date)T09:00:00+01:00" }))
    $created.Add((Add-Activity @{
        kind = 'sleep'; startedAt = '2026-11-25T23:30:00+01:00'; endedAt = "$($date)T00:30:00+01:00"
    }))
    Assert-Status "$activities/api/activities/$milkId" 200 $activityHeaders

    if ($BrokerOutage) {
        if (!(Test-Summary $before.milkMilliliters $before.diaperCount $before.sleepMinutes)) {
            throw 'Il riepilogo è cambiato durante la pausa del broker.'
        }
        Write-Host 'RabbitMQ spento. L''attività è già leggibile e il riepilogo è ancora al valore iniziale.'
        Invoke-Compose @('start', 'rabbitmq')
        $brokerStopped = $false
    }

    Wait-For 'proiezione delle tre attività' {
        Test-Summary ($before.milkMilliliters + 120) ($before.diaperCount + 1) ($before.sleepMinutes + 30)
    }
    Write-Host 'Riepilogo aggiornato. +120 ml, +1 cambio, +30 minuti di sonno dopo mezzanotte.'

    foreach ($id in $created) {
        Assert-Status "$activities/api/activities/$id" 204 $activityHeaders 'Delete'
    }
    Assert-Status "$activities/api/activities/$milkId" 404 $activityHeaders
    Assert-Status "$activities/api/activities/$milkId" 404 $activityHeaders 'Delete'
    Wait-For 'proiezione delle cancellazioni' {
        Test-Summary $before.milkMilliliters $before.diaperCount $before.sleepMinutes
    }
    $created.Clear()
    Write-Host 'Cancellazioni proiettate. Il riepilogo è tornato al valore iniziale.'
} finally {
    if ($brokerStopped) {
        Invoke-Compose @('start', 'rabbitmq')
    }
    foreach ($id in $created) {
        try {
            Invoke-WebRequest "$activities/api/activities/$id" -Method Delete -Headers $activityHeaders -SkipHttpErrorCheck -TimeoutSec 10 | Out-Null
        } catch {
            Write-Warning "Pulizia non riuscita per l’attività $id."
        }
    }
}
