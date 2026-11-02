#requires -Version 7.0
param([switch]$Stop, [switch]$SkipInfrastructure)
$ErrorActionPreference = 'Stop'
$demoRoot = $PSScriptRoot
$artifacts = Join-Path $demoRoot '.artifacts'
$stateFile = Join-Path $artifacts 'processes.json'
$projects = @('Supplier.Api', 'Inventory.Api', 'Payments.Api', 'Notifications.Worker', 'Kitchen.Worker', 'Orders.Api')

function Stop-DemoProcesses {
    if (!(Test-Path -LiteralPath $stateFile)) { return }
    foreach ($entry in @(Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json)) {
        $process = Get-Process -Id $entry.id -ErrorAction SilentlyContinue
        if ($process -and $process.StartTime.ToUniversalTime().Ticks.ToString() -eq $entry.startTicks) {
            # Validate both executable and process start time before stopping a recorded process.
            $expected = Join-Path $demoRoot "$($entry.project)/bin/Debug/net10.0/$($entry.project)"
            if ($IsWindows) { $expected += '.exe' }
            if ($process.Path -eq $expected) { Stop-Process -Id $process.Id }
        }
    }
    Remove-Item -LiteralPath $stateFile
}

if ($Stop) {
    Stop-DemoProcesses
    Write-Host 'Local demo applications stopped. Infrastructure remains running; use docker compose down to stop it.'
    return
}
if (Test-Path -LiteralPath $stateFile) { throw 'Demo process state already exists. Run ./run-local.ps1 -Stop first.' }
New-Item -ItemType Directory -Force $artifacts | Out-Null
if (!$SkipInfrastructure) {
    & docker compose -f (Join-Path $demoRoot 'compose.yaml') up -d --wait dashboard collector rabbitmq redis postgres prometheus grafana redis-exporter postgres-exporter
    if ($LASTEXITCODE -ne 0) { throw 'Infrastructure startup failed.' }
}
& dotnet build (Join-Path $demoRoot 'ThirdPartyObservability.slnx')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$started = @()
try {
    foreach ($project in $projects) {
        $executable = Join-Path $demoRoot "$project/bin/Debug/net10.0/$project"
        if ($IsWindows) { $executable += '.exe' }
        $startOptions = @{
            FilePath = $executable
            WorkingDirectory = Join-Path $demoRoot $project
            RedirectStandardOutput = Join-Path $artifacts "$project.log"
            RedirectStandardError = Join-Path $artifacts "$project.err"
            PassThru = $true
        }
        if ($IsWindows) { $startOptions.WindowStyle = 'Hidden' }
        $process = Start-Process @startOptions
        $started += @{ project = $project; id = $process.Id; startTicks = $process.StartTime.ToUniversalTime().Ticks.ToString() }
        ConvertTo-Json -InputObject @($started) | Set-Content -LiteralPath $stateFile
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(40)
    foreach ($port in @(5282, 5281, 5283, 5280)) {
        do {
            foreach ($entry in $started) {
                if (!(Get-Process -Id $entry.id -ErrorAction SilentlyContinue)) { throw "$($entry.project) exited. See $artifacts." }
            }
            try { $null = Invoke-RestMethod "http://localhost:$port/health" -TimeoutSec 2; break }
            catch { if ([DateTime]::UtcNow -gt $deadline) { throw }; Start-Sleep -Milliseconds 250 }
        } while ($true)
    }
    $null = Invoke-RestMethod 'http://localhost:5280/health/ready' -TimeoutSec 10
    Write-Host 'Six applications started. Run ./rehearse.ps1 -IncludeInfrastructure.'
    Write-Host 'Traces/logs: http://localhost:18898 | Grafana: http://localhost:13000/d/restaurant-full | Prometheus: http://localhost:19090'
} catch {
    Stop-DemoProcesses
    throw
}
