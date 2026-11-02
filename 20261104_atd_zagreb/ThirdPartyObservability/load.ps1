#requires -Version 7.0
param([int]$Count = 30, [int]$Concurrency = 4, [string]$BaseUrl = 'http://localhost:5280')
$ErrorActionPreference = 'Stop'
if ($Count -lt 1 -or $Concurrency -lt 1) { throw 'Count and Concurrency must be positive.' }
$results = 1..$Count | ForEach-Object -Parallel {
    $scenarios = @('normal', 'supplier-retry', 'slow-supplier', 'payment-declined', 'normal')
    $scenario = $scenarios[($_ - 1) % $scenarios.Count]
    $order = Invoke-RestMethod "$using:BaseUrl/orders" -Method Post -ContentType application/json `
        -Body (@{ dish = 'margherita'; scenario = $scenario } | ConvertTo-Json) -ErrorAction Stop
    [pscustomobject]@{ id = $order.id; traceId = $order.traceId; scenario = $scenario }
} -ThrottleLimit $Concurrency
$output = Join-Path $PSScriptRoot '.artifacts'
New-Item -ItemType Directory -Force $output | Out-Null
$results | ConvertTo-Json | Set-Content (Join-Path $output 'load.json')
Write-Host "Queued $(@($results).Count) orders. Processing continues asynchronously; watch queue depth and message age."
