[CmdletBinding()]
param([int]$TimeoutSeconds = 300, [switch]$Standalone, [switch]$UseLocalOsvSnapshot)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Test-LocalEndpoint([string]$Url) {
    try {
        (Invoke-WebRequest $Url -NoProxy -TimeoutSec 3).StatusCode -eq 200
    } catch { $false }
}

function Get-ReleaseFile([string]$Url, [string]$Path, [string]$Sha256) {
    if (!(Test-Path -LiteralPath $Path)) {
        Invoke-WebRequest $Url -OutFile $Path
    }
    if ((Get-FileHash -LiteralPath $Path).Hash -ne $Sha256) {
        throw "Hash SHA-256 inatteso per $Path."
    }
}

function Start-Standalone {
    $serviceDir = Join-Path $root '.artifacts/dependency-track-standalone'
    $baseUrl = 'http://127.0.0.1:8190'
    New-Item -ItemType Directory -Force $serviceDir | Out-Null

    if ($UseLocalOsvSnapshot -and !(Test-LocalEndpoint 'http://127.0.0.1:8192/NuGet/modified_id.csv')) {
        $snapshot = Join-Path $root '.artifacts/dependency-track/osv'
        if (!(Test-Path -LiteralPath (Join-Path $snapshot 'NuGet/all.zip'))) {
            throw 'Snapshot OSV assente. Esegui security/monitoring/refresh-osv.ps1.'
        }
        $python = (Get-Command python -CommandType Application | Select-Object -First 1).Source
        $serverArgs = '-m http.server 8192 --bind 127.0.0.1 --directory "{0}"' -f $snapshot
        Start-Process -FilePath $python -ArgumentList $serverArgs -WindowStyle Hidden -RedirectStandardOutput "$serviceDir/osv-stdout.log" -RedirectStandardError "$serviceDir/osv-stderr.log"
    }

    if ((Test-LocalEndpoint "$baseUrl/api/version") -and (Test-LocalEndpoint "$baseUrl/")) {
        Write-Host "Dependency-Track standalone disponibile su $baseUrl"
        return
    }

    $jar = Join-Path $serviceDir 'dependency-track-bundled.jar'
    Get-ReleaseFile 'https://github.com/DependencyTrack/dependency-track/releases/download/4.14.6/dependency-track-bundled.jar' $jar '421f4cb919f8e19babd13a0fe6380000fcf4938ae31009503eb34dcf65146753'
    $java = Join-Path $serviceDir 'java/jdk-25.0.5+8-jre/bin/java.exe'
    if (!(Test-Path -LiteralPath $java)) {
        $archive = Join-Path $serviceDir 'temurin25.zip'
        Get-ReleaseFile 'https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.5%2B8/OpenJDK25U-jre_x64_windows_hotspot_25.0.5_8.zip' $archive 'da78d54a2fcdbba1e92d5e3f07dd30236f96297891c4a21a781c3f38f5c0beae'
        Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $serviceDir 'java') -Force
    }
    New-Item -ItemType Directory -Force "$serviceDir/tmp" | Out-Null
    $pidPath = Join-Path $serviceDir 'pid.txt'
    $process = $null
    if (Test-Path -LiteralPath $pidPath) {
        $previousId = Get-Content -LiteralPath $pidPath
        $process = Get-Process -Id $previousId -ErrorAction SilentlyContinue | Where-Object Path -eq $java
    }
    if (!$process) {
        $javaArgs = '-Xms512m -Xmx4g "-Duser.home={0}" "-Djava.io.tmpdir={0}/tmp" -jar "{1}" -host 127.0.0.1 -port 8190' -f $serviceDir,$jar
        $process = Start-Process -FilePath $java -ArgumentList $javaArgs -WorkingDirectory $serviceDir -WindowStyle Hidden -PassThru -RedirectStandardOutput "$serviceDir/stdout.log" -RedirectStandardError "$serviceDir/stderr.log"
        $process.Id | Set-Content -LiteralPath $pidPath
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if ((Test-LocalEndpoint "$baseUrl/api/version") -and (Test-LocalEndpoint "$baseUrl/")) {
            Write-Host "Dependency-Track standalone disponibile su $baseUrl"
            return
        }
        if ($process.HasExited) { break }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Dependency-Track standalone non pronto. Controllare $serviceDir/stdout.log e stderr.log."
}

if ($Standalone) {
    Start-Standalone
    return
}

$secretDir = Join-Path $root '.secrets'
$envPath = Join-Path $secretDir 'dependency-track.env'
New-Item -ItemType Directory -Force $secretDir | Out-Null
if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    & chmod 700 $secretDir
    if ($LASTEXITCODE -ne 0) { throw 'Impossibile restringere i permessi della directory .secrets.' }
}
if (-not (Test-Path $envPath)) {
    $password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    "DTRACK_DB_PASSWORD=$password" | Set-Content $envPath
}
docker compose --env-file $envPath -f (Join-Path $root 'compose.security.yaml') up -d
if ($LASTEXITCODE -ne 0) { throw 'Avvio Dependency-Track fallito.' }
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    try {
        $version = Invoke-RestMethod 'http://localhost:8091/api/version' -TimeoutSec 5
        Write-Host "Dependency-Track $($version.version) disponibile: http://localhost:8090"
        return
    } catch { Start-Sleep -Seconds 3 }
} while ([DateTime]::UtcNow -lt $deadline)
throw 'Dependency-Track non pronto entro il timeout. Controllare docker compose logs dtrack-api.'
