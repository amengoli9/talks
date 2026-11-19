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
        $bomDir = Join-Path $SecurityRoot '.artifacts/sbom'
        foreach ($variant in 'standard','chiseled') {
            try {
                Invoke-Checked docker @('run','--rm','-v',"${bomDir}:/sbom:ro",'-v',"${output}:/reports",'-v','babylog-grype-db:/root/.cache/grype',$GrypeImage,"sbom:/sbom/babylog-container-$variant.cdx.json",'-o','json','--file',"/reports/grype-$variant.json")
                $items = (Get-Content -Raw "$output/grype-$variant.json" | ConvertFrom-Json).matches
                $high = @($items | Where-Object { $_.vulnerability.severity -in 'High','Critical' })
                $totals += [ordered]@{Variant=$variant; Total=$items.Count; HighCritical=$high.Count}
                if ($high.Count) { $findings += "$variant High/Critical=$($high.Count)" }
            } catch { $errors += $_.Exception.Message }
        }
    }
    [ordered]@{ Gate= $(if($errors.Count){'error'}elseif($findings.Count){'rejected'}else{'passed'}); Errors=$errors; Findings=$findings; Images=$totals; Time=(Get-Date).ToUniversalTime().ToString('o') } | ConvertTo-Json -Depth 8 | Set-Content "$output/gate.json"
    if ($errors.Count) { throw "Security tooling error (reports preserved): $($errors -join '; ')" }
    if ($findings.Count) { throw "Security gate rejected High/Critical findings (reports preserved): $($findings -join '; ')" }
} finally { Pop-Location }
