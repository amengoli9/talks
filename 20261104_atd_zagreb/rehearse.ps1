#Requires -Version 7.0
param(
    [string] $BaseUrl = 'http://localhost:5270',
    [string] $KitchenUrl = 'http://localhost:5271'
)

$ErrorActionPreference = 'Stop'

function Set-Scenario([string] $Name) {
    Invoke-RestMethod "$BaseUrl/demo/$Name" -Method Post -TimeoutSec 30 | Out-Null
}

function Send-Order([string] $Name, [string] $Dish, [int] $Status = 200, [double] $MinSeconds = 0, [double] $MaxSeconds = 2) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-WebRequest "$BaseUrl/orders" -Method Post -ContentType 'application/json' `
        -Body (@{ dish = $Dish } | ConvertTo-Json -Compress) -SkipHttpErrorCheck -TimeoutSec 30
    $timer.Stop()
    $content = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { $response.Content }
    $json = $content | ConvertFrom-Json
    if ([int]$response.StatusCode -ne $Status) { throw "${Name}: expected HTTP $Status, got $($response.StatusCode)." }
    if ($json.traceId -notmatch '^[0-9a-f]{32}$' -or $json.traceId -eq ('0' * 32)) { throw "${Name}: invalid trace ID." }
    if ($timer.Elapsed.TotalSeconds -lt $MinSeconds -or $timer.Elapsed.TotalSeconds -gt $MaxSeconds) {
        throw "${Name}: $($timer.Elapsed.TotalSeconds) seconds, expected $MinSeconds-$MaxSeconds."
    }
    '{0,-26} HTTP {1}  {2,7:N0} ms  traceId={3}' -f $Name, $Status, $timer.Elapsed.TotalMilliseconds, $json.traceId
}

try {
    Set-Scenario normal
    Send-Order 'Warm-up' margherita -MaxSeconds 10
    Send-Order 'Normal confirmation' margherita
    Send-Order 'Risotto is also fast' risotto
    Send-Order 'Sold out' tiramisu -Status 409
    Send-Order 'Unknown dish' unknown -Status 400
    Send-Order 'DB constraint: cannoli' cannoli -Status 502
    Send-Order 'Recovery after DB error' margherita

    Set-Scenario slow
    $before = Invoke-RestMethod "$KitchenUrl/demo/state"
    # Both orders must share one reload, with correlated but distinct trace IDs.
    $client = [Net.Http.HttpClient]::new()
    $firstBody = [Net.Http.StringContent]::new('{"dish":"margherita"}', [Text.Encoding]::UTF8, 'application/json')
    $secondBody = [Net.Http.StringContent]::new('{"dish":"risotto"}', [Text.Encoding]::UTF8, 'application/json')
    try {
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $first = $client.PostAsync("$BaseUrl/orders", $firstBody)
        $second = $client.PostAsync("$BaseUrl/orders", $secondBody)
        $traceIds = @()
        foreach ($task in @($first, $second)) {
            $reply = $task.GetAwaiter().GetResult()
            try {
                if ([int]$reply.StatusCode -ne 200) { throw 'Concurrent order failed.' }
                $json = $reply.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
                if ($json.traceId -notmatch '^[0-9a-f]{32}$') { throw 'Missing concurrent trace ID.' }
                $traceIds += $json.traceId
            } finally { $reply.Dispose() }
        }
        $timer.Stop()
        if ($timer.Elapsed.TotalSeconds -lt 10 -or $timer.Elapsed.TotalSeconds -gt 15) { throw 'Concurrent reload should take 10-15 seconds.' }
        if ($traceIds[0] -eq $traceIds[1]) { throw 'Independent requests should have different trace IDs.' }
        $after = Invoke-RestMethod "$KitchenUrl/demo/state"
        if ($after.refreshCount -ne $before.refreshCount + 1) { throw 'Concurrent orders did not share one reload.' }
        'Two concurrent orders      HTTP 200  {0,7:N0} ms  one shared reload' -f $timer.Elapsed.TotalMilliseconds
    } finally { $firstBody.Dispose(); $secondBody.Dispose(); $client.Dispose() }

    Send-Order 'Cache now populated' margherita
    Set-Scenario proactive
    $beforeFix = Invoke-RestMethod "$KitchenUrl/demo/state"
    Send-Order 'After enabling fix' margherita
    Start-Sleep -Seconds 2
    Send-Order 'During slow refresh' risotto
    Write-Host 'Waiting past the original 20-second cache expiry...'
    Start-Sleep -Seconds 20
    Send-Order 'Beyond original expiry' margherita
    $afterFix = Invoke-RestMethod "$KitchenUrl/demo/state"
    if (!$afterFix.proactive -or $afterFix.managementDelayMs -ne 10000) { throw 'The fix must keep the management system slow.' }
    if ($afterFix.refreshCount -le $beforeFix.refreshCount) { throw 'No background refresh completed.' }
    if ([DateTimeOffset]$afterFix.expiresAt -le [DateTimeOffset]$beforeFix.expiresAt) { throw 'Background refresh did not renew the snapshot.' }
    Send-Order 'Sold out after fix' tiramisu -Status 409
    # The presenter may spend longer than the cache lifetime explaining traces.
    # Applying the fix after expiry must warm up automatically, with no extra order.
    Set-Scenario slow
    $warmupTimer = [Diagnostics.Stopwatch]::StartNew()
    Set-Scenario proactive
    $warmupTimer.Stop()
    if ($warmupTimer.Elapsed.TotalSeconds -lt 10 -or $warmupTimer.Elapsed.TotalSeconds -gt 15) { throw 'Expired-cache fix must perform the slow warm-up.' }
    Send-Order 'Fix after a long pause' margherita
    Write-Host 'All scenarios passed. Inspect traces, logs and metrics in the dashboard.'
} finally { Set-Scenario normal }
