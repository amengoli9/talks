#requires -Version 7.0
[CmdletBinding()]
param([int]$Port = 5080, [switch]$KeepContainers)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$run = [Guid]::NewGuid().ToString('N').Substring(0,10)
$reportDir = Join-Path $root ".artifacts/containers/smoke-$run"
New-Item -ItemType Directory -Force $reportDir | Out-Null
function Invoke-Docker {
    $dockerArgs = $args
    $output = & docker @dockerArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "docker $($dockerArgs -join ' ') failed: $output" }
    return $output
}
function Wait-Health {
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        try { $null = Invoke-RestMethod "$base/api/health" -TimeoutSec 2; return } catch { Start-Sleep -Seconds 1 }
    }
    throw 'Container health timeout'
}
Push-Location $root
try {
    foreach ($variant in @('standard','chiseled')) {
        $project = "babylog-smoke-$run-$variant"
        $volume = "$project-data"
        $container = "$project-app"
        $base = "http://127.0.0.1:$Port"
        try {
            Invoke-Docker @('volume','create',$volume) | Out-Null
            Invoke-Docker @('run','-d','--name',$container,'--platform','linux/amd64','--user','1654:1654','--read-only','--cap-drop','ALL','--security-opt','no-new-privileges:true','--tmpfs','/tmp:rw,noexec,nosuid,size=64m,mode=1777','-p',"127.0.0.1:${Port}:8080",'-v',"${volume}:/data",'-e','ASPNETCORE_ENVIRONMENT=Production','-e','ConnectionStrings__BabyLog=Data Source=/data/babylog.db','-e','DataProtection__Path=/data/keys','-e','BabyLog__BaseUrl=http://127.0.0.1:8080','-e','TZ=Europe/Rome',"babylog:$variant") | Out-Null
            Wait-Health
            $page = Invoke-WebRequest "$base/"
            if ($page.StatusCode -ne 200 -or $page.Content -notmatch 'blazor') { throw 'Blazor HTML missing' }
            $null = Invoke-RestMethod "$base/api/baby"
            foreach ($endpoint in @('activities','summary')) {
                foreach ($query in @('', '?date=0001-01-01', '?date=9999-12-31', '?date=invalid')) {
                    $invalidDate = Invoke-WebRequest "$base/api/$endpoint$query" -SkipHttpErrorCheck
                    if ($invalidDate.StatusCode -ne 400) { throw "Invalid date accepted by $endpoint$query" }
                }
            }
            $date = '2040-06-01'
            $created = @()
            foreach ($body in @(
                @{kind='milk';startedAt='2040-06-01T10:00:00+02:00';milliliters=120;notes='smoke'},
                @{kind='diaper';startedAt='2040-06-01T11:00:00+02:00'},
                @{kind='sleep';startedAt='2040-06-01T12:00:00+02:00';endedAt='2040-06-01T13:30:00+02:00'})) {
                $response = Invoke-WebRequest "$base/api/activities" -Method Post -ContentType application/json -Body ($body | ConvertTo-Json)
                if ($response.StatusCode -ne 201) { throw 'Create did not return 201' }
                $created += $response.Content | ConvertFrom-Json
            }
            $bad = Invoke-WebRequest "$base/api/activities" -Method Post -ContentType application/json -Body '{"kind":"milk","startedAt":"2040-06-01T10:00:00+02:00","milliliters":0}' -SkipHttpErrorCheck
            if ($bad.StatusCode -ne 400) { throw 'Invalid activity was accepted' }
            $summary = Invoke-RestMethod "$base/api/summary?date=$date"
            if ($summary.milkMilliliters -ne 120 -or $summary.diaperCount -ne 1 -or $summary.sleepMinutes -ne 90) { throw 'Unexpected daily summary' }
            $before = (Invoke-WebRequest "$base/api/activities?date=$date").Content | ConvertFrom-Json
            if (@($before).Count -ne 3) { throw 'Expected three stored activities' }
            Invoke-Docker @('cp',"${container}:/data/keys",(Join-Path $reportDir "$variant-keys-before")) | Out-Null
            Invoke-Docker @('restart',$container) | Out-Null
            Wait-Health
            $after = (Invoke-WebRequest "$base/api/activities?date=$date").Content | ConvertFrom-Json
            if ((($before.id | Sort-Object) -join ',') -cne (($after.id | Sort-Object) -join ',')) { throw 'Activity persistence failed' }
            Invoke-Docker @('cp',"${container}:/data/keys",(Join-Path $reportDir "$variant-keys-after")) | Out-Null
            $keysBefore = @(Get-ChildItem (Join-Path $reportDir "$variant-keys-before") -File | Get-FileHash | Select-Object -ExpandProperty Hash)
            $keysAfter = @(Get-ChildItem (Join-Path $reportDir "$variant-keys-after") -File | Get-FileHash | Select-Object -ExpandProperty Hash)
            if ($keysBefore.Count -eq 0 -or ($keysBefore -join ',') -cne ($keysAfter -join ',')) { throw 'Data Protection keys did not persist' }
            foreach ($entry in $created) {
                $deleted = Invoke-WebRequest "$base/api/activities/$($entry.id)" -Method Delete
                if ($deleted.StatusCode -ne 204) { throw 'Delete did not return 204' }
            }
            $missing = Invoke-WebRequest "$base/api/activities/$($created[0].id)" -Method Delete -SkipHttpErrorCheck
            if ($missing.StatusCode -ne 404) { throw 'Missing activity did not return 404' }
            $shellOutput = & docker exec $container /bin/sh -c 'id' 2>&1
            $shellExit = $LASTEXITCODE
            $shellOutput | Set-Content (Join-Path $reportDir "$variant-shell.txt")
            if ($variant -eq 'chiseled' -and $shellExit -eq 0) { throw 'Chiseled unexpectedly contains a shell' }
            if ($variant -eq 'standard' -and ($shellExit -ne 0 -or "$shellOutput" -notmatch 'uid=1654')) { throw 'Standard process is not running as expected user' }
            $inspection = Invoke-Docker @('inspect',$container)
            $inspection | Set-Content (Join-Path $reportDir "$variant-inspect.json")
            $runtime = ($inspection | ConvertFrom-Json)[0]
            if ($runtime.Config.User -ne '1654:1654' -or -not $runtime.HostConfig.ReadonlyRootfs -or $runtime.HostConfig.CapDrop -notcontains 'ALL' -or $runtime.HostConfig.SecurityOpt -notcontains 'no-new-privileges:true') { throw 'Runtime hardening configuration mismatch' }
            @{variant=$variant;passed=$true;checks=@('HTML','profile','CRUD','validation','summary','restart persistence','Data Protection persistence','shell','nonroot')} | ConvertTo-Json | Set-Content (Join-Path $reportDir "$variant-result.json")
        } finally {
            & docker logs $container 2>&1 | Set-Content (Join-Path $reportDir "$variant.log")
            if (-not $KeepContainers) {
                Invoke-Docker @('rm','-f',$container) | Out-Null
                Invoke-Docker @('volume','rm',$volume) | Out-Null
            }
        }
        if ($KeepContainers) { $Port++ }
    }
    Write-Host "Container smoke passed. Reports: $reportDir"
} finally { Pop-Location }

