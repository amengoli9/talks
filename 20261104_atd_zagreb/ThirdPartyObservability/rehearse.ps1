param([string]$BaseUrl = 'http://localhost:5280', [switch]$IncludeInfrastructure)
$ErrorActionPreference = 'Stop'

function Submit-Order([string]$Scenario) {
    $response = Invoke-WebRequest "$BaseUrl/orders" -Method Post -ContentType 'application/json' `
        -Body (@{ dish = 'margherita'; scenario = $Scenario } | ConvertTo-Json)
    if ($response.StatusCode -ne 202) { throw "Expected 202, got $($response.StatusCode)" }
    $order = $response.Content | ConvertFrom-Json
    if ($order.traceId -notmatch '^[a-f0-9]{32}$') { throw 'Missing trace context' }
    return $order
}

function Wait-Order($Order, [string]$Expected) {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $response = Invoke-WebRequest "$BaseUrl/orders/$($Order.id)" -SkipHttpErrorCheck
        if ($response.StatusCode -eq 200) {
            $status = $response.Content | ConvertFrom-Json
            if ($status.status -ne $Expected) { throw "Expected $Expected, got $($status.status)" }
            return $status
        }
        if ($response.StatusCode -ne 404) { throw "Unexpected lookup status $($response.StatusCode)" }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Order $($Order.id) was not processed within 20 seconds"
}

$normal = Submit-Order 'normal'
$null = Wait-Order $normal 'accepted'
# A second lookup exercises the Redis hit path.
$cached = Invoke-RestMethod "$BaseUrl/orders/$($normal.id)"
if ($cached.status -ne 'accepted') { throw 'Cached result is incorrect' }
Write-Host "Normal + cached lookup passed; trace $($normal.traceId)"

$watch = [Diagnostics.Stopwatch]::StartNew()
$slow = Submit-Order 'slow-db'
$null = Wait-Order $slow 'accepted'
if ($watch.Elapsed.TotalSeconds -lt 2.8) { throw 'Slow database scenario did not wait for PostgreSQL' }
Write-Host "Slow PostgreSQL passed; trace $($slow.traceId)"

$failed = Submit-Order 'fail'
$null = Wait-Order $failed 'failed'
Write-Host "Failed processing passed; trace $($failed.traceId)"

$invalid = Invoke-WebRequest "$BaseUrl/orders" -Method Post -ContentType 'application/json' `
    -Body '{"dish":"unknown"}' -SkipHttpErrorCheck
if ($invalid.StatusCode -ne 400) { throw 'Invalid dish was not rejected' }
function Wait-Notification($Order, [string]$Expected = 'delivered') {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $result = Invoke-RestMethod "$BaseUrl/orders/$($Order.id)/notification"
        if ($result.notification -eq $Expected) { return }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Notification for $($Order.id) did not reach $Expected"
}

$evidence = @(
    @{ scenario = 'normal'; id = $normal.id; traceId = $normal.traceId },
    @{ scenario = 'slow-db'; id = $slow.id; traceId = $slow.traceId },
    @{ scenario = 'fail'; id = $failed.id; traceId = $failed.traceId }
)
Wait-Notification $normal
foreach ($case in @(
    @{ scenario = 'slow-supplier'; status = 'accepted' },
    @{ scenario = 'supplier-retry'; status = 'accepted' },
    @{ scenario = 'supplier-down'; status = 'failed' },
    @{ scenario = 'sold-out'; status = 'unavailable' },
    @{ scenario = 'payment-declined'; status = 'declined' },
    @{ scenario = 'payment-timeout'; status = 'failed' },
    @{ scenario = 'notification-fail'; status = 'accepted' }
)) {
    $order = Submit-Order $case.scenario
    $null = Wait-Order $order $case.status
    if ($case.scenario -eq 'notification-fail') { Wait-Notification $order 'dispatched' }
    else { Wait-Notification $order }
    $evidence += @{ scenario = $case.scenario; id = $order.id; traceId = $order.traceId }
    Write-Host "$($case.scenario) -> $($case.status) passed; trace $($order.traceId)"
}

$unknownScenario = Invoke-WebRequest "$BaseUrl/orders" -Method Post -ContentType 'application/json' `
    -Body '{"dish":"margherita","scenario":"unknown"}' -SkipHttpErrorCheck
if ($unknownScenario.StatusCode -ne 400) { throw 'Invalid scenario was not rejected' }
$null = Invoke-RestMethod "$BaseUrl/health/ready"

if ($IncludeInfrastructure) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $targets = (Invoke-RestMethod 'http://localhost:19090/api/v1/targets').data.activeTargets
        $unhealthy = @($targets | Where-Object health -ne 'up')
        if ($targets.Count -eq 5 -and $unhealthy.Count -eq 0) { break }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($targets.Count -ne 5 -or $unhealthy.Count -ne 0) { throw 'Prometheus targets are not all healthy' }
    foreach ($query in @('restaurant_orders_total', 'restaurant_http_retries_total', 'redis_up', 'pg_up', 'rabbitmq_queue_messages_ready')) {
        $encoded = [uri]::EscapeDataString($query)
        $metric = Invoke-RestMethod "http://localhost:19090/api/v1/query?query=$encoded"
        if ($metric.data.result.Count -eq 0) { throw "Missing Prometheus metric $query" }
    }
    $credentials = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('demo:demo'))
    # Replay an already delivered notification to exercise consumer deduplication.
    $duplicateQuery = [uri]::EscapeDataString('sum(restaurant_notifications_total{outcome="duplicate"})')
    $before = Invoke-RestMethod "http://localhost:19090/api/v1/query?query=$duplicateQuery"
    $duplicateCount = if ($before.data.result.Count) { [double]$before.data.result[0].value[1] } else { 0 }
    $payload = @{ Id = $normal.id; Dish = 'margherita'; Status = 'accepted'; Scenario = 'normal' } | ConvertTo-Json -Compress
    $publication = @{
        properties = @{ content_type = 'application/json'; delivery_mode = 2 }
        routing_key = 'restaurant.notifications'
        payload = $payload
        payload_encoding = 'string'
    } | ConvertTo-Json -Depth 5
    $published = Invoke-RestMethod 'http://localhost:15673/api/exchanges/%2F/amq.default/publish' `
        -Method Post -ContentType application/json -Body $publication -Headers @{ Authorization = "Basic $credentials" }
    if (!$published.routed) { throw 'Duplicate notification was not routed' }
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $after = Invoke-RestMethod "http://localhost:19090/api/v1/query?query=$duplicateQuery"
        if ($after.data.result.Count -and [double]$after.data.result[0].value[1] -gt $duplicateCount) { break }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)
    if (!$after.data.result.Count -or [double]$after.data.result[0].value[1] -le $duplicateCount) {
        throw 'Notification consumer did not report deduplication'
    }
    foreach ($queue in @('restaurant.orders.failed', 'restaurant.notifications.failed')) {
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        do {
            $state = Invoke-RestMethod "http://localhost:15673/api/queues/%2F/$queue" -Headers @{ Authorization = "Basic $credentials" }
            if ($state.messages_ready -gt 0) { break }
            Start-Sleep -Milliseconds 500
        } while ([DateTime]::UtcNow -lt $deadline)
        if ($state.messages_ready -lt 1) { throw "Expected a dead-lettered message in $queue" }
    }
    $dashboard = Invoke-RestMethod 'http://localhost:13000/api/dashboards/uid/restaurant-full'
    if ($dashboard.dashboard.panels.Count -lt 10) { throw 'Grafana dashboard was not provisioned' }
    Write-Host 'Prometheus targets, application/server metrics, dead-letter queues and Grafana provisioning passed.'
}
$output = Join-Path $PSScriptRoot '.artifacts'
New-Item -ItemType Directory -Force $output | Out-Null
$evidence | ConvertTo-Json | Set-Content (Join-Path $output 'rehearsal.json')
Write-Host 'All checks passed. Order IDs and trace IDs saved to .artifacts/rehearsal.json.'
