[CmdletBinding()]
param([switch]$SkipImages)
. "$PSScriptRoot/../security/common.ps1"
Push-Location $SecurityRoot
try {
    $output = Join-Path $SecurityRoot '.artifacts/security'
    New-Item -ItemType Directory -Force $output | Out-Null
    $errors = @(); $findings = @()
    try {
        $audit = & dotnet package list --project BabyLog/BabyLog.csproj --vulnerable --include-transitive --format json 2> "$output/nuget-errors.log"
        if ($LASTEXITCODE -ne 0) { throw 'NuGet audit command failed' }
        $audit | Set-Content "$output/nuget-audit.json"
        $auditObject = $audit | ConvertFrom-Json
        if (@($auditObject.projects | ForEach-Object { $_.frameworks } | ForEach-Object { $_.topLevelPackages; $_.transitivePackages } | ForEach-Object { $_.vulnerabilities } | Where-Object severity -in 'High','Critical').Count) { $findings += 'NuGet High/Critical' }
    } catch { $errors += $_.Exception.Message }
    $totals = @()
    if (!$SkipImages) {
        try { Invoke-Trivy @('image','--download-db-only') } catch { $errors += $_.Exception.Message }
        foreach ($variant in 'standard','chiseled') {
            try {
                Invoke-Trivy @('image','--skip-db-update','--scanners','vuln','--format','json','--output',"/work/.artifacts/security/trivy-$variant.json","babylog:$variant")
                $items = @(Read-TrivyFindings "$output/trivy-$variant.json")
                $high = @($items | Where-Object Severity -in 'HIGH','CRITICAL')
                $totals += [ordered]@{Variant=$variant; Total=$items.Count; OS=@($items | Where-Object Class -eq 'os-pkgs').Count; Application=@($items | Where-Object Class -ne 'os-pkgs').Count; HighCritical=$high.Count}
                if ($high.Count) { $findings += "$variant High/Critical=$($high.Count)" }
            } catch { $errors += "$variant`: $($_.Exception.Message)" }
        }
        if (Test-Path '.artifacts/trivy-cache/db/metadata.json') { Copy-Item '.artifacts/trivy-cache/db/metadata.json' "$output/trivy-db-metadata.json" }
    }
    [ordered]@{ Gate= $(if($errors.Count){'error'}elseif($findings.Count){'rejected'}else{'passed'}); Errors=$errors; Findings=$findings; Images=$totals; Time=(Get-Date).ToUniversalTime().ToString('o') } | ConvertTo-Json -Depth 8 | Set-Content "$output/gate.json"
    if ($errors.Count) { throw "Security tooling error (reports preserved): $($errors -join '; ')" }
    if ($findings.Count) { throw "Security gate rejected High/Critical findings (reports preserved): $($findings -join '; ')" }
} finally { Pop-Location }
