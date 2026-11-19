[CmdletBinding()]
param([string]$Version='local-20261126',[string]$PublishPath='.artifacts/publish',[switch]$SkipImages)
. "$PSScriptRoot/../security/common.ps1"
Push-Location $SecurityRoot
try {
    $applicationVersion = ([xml](Get-Content -LiteralPath 'BabyLog/BabyLog.csproj' -Raw)).Project.PropertyGroup.Version
    if (!(Test-Path "$PublishPath/BabyLog.deps.json")) { throw "Publish output missing: $PublishPath/BabyLog.deps.json" }
    $output = Join-Path $SecurityRoot '.artifacts/sbom'
    New-Item -ItemType Directory -Force $output | Out-Null
    New-Item -ItemType Directory -Force (Join-Path $output 'microsoft') | Out-Null
    Invoke-Checked dotnet @('tool','run','sbom-tool','--','generate','-b',(Join-Path $SecurityRoot $PublishPath),'-bc',(Join-Path $SecurityRoot 'BabyLog'),'-m',(Join-Path $output 'microsoft'),'-pn','BabyLog','-pv',$Version,'-ps','Organization: BabyLog demo','-nsb','https://babylog.example/sbom/','-mi','SPDX:2.2','-D','true')
    $validationPath = Join-Path $output 'microsoft-validation.json'
    if (Test-Path -LiteralPath $validationPath) { Remove-Item -LiteralPath $validationPath }
    Invoke-Checked dotnet @('tool','run','sbom-tool','--','validate','-b',(Join-Path $SecurityRoot $PublishPath),'-m',(Join-Path $output 'microsoft/_manifest'),'-o',$validationPath,'-mi','SPDX:2.2','-n','true')
    if (-not (Test-Path -LiteralPath $validationPath)) { throw 'Microsoft SBOM Tool did not produce a validation report.' }
    if ((Get-Content -LiteralPath $validationPath -Raw | ConvertFrom-Json).Result -ne 'Success') { throw 'SPDX validation failed.' }
    New-BabyLogApplicationBom 'BabyLog/BabyLog.csproj' $applicationVersion (Join-Path $output 'babylog-app.cdx.json')
    $manifest = @([ordered]@{Name='BabyLog/app'; Version=$applicationVersion; Bom='babylog-app.cdx.json'; Scope='NuGet runtime dependency graph; excludes tests and dev; framework/runtime separately inventoried in image'})
    if (!$SkipImages) {
        foreach ($variant in 'standard','chiseled') {
            $image = "babylog:$variant"
            $id = & docker image inspect $image --format '{{.Id}}'
            if ($LASTEXITCODE -ne 0) { throw "Image missing: $image" }
            Invoke-Checked docker @('run','--rm','-v','/var/run/docker.sock:/var/run/docker.sock','-v',"${output}:/reports",$SyftImage,"docker:$image",'-o',"cyclonedx-json=/reports/babylog-container-$variant.cdx.json")
            $manifest += [ordered]@{Name="BabyLog/container-$variant"; Version="$Version-$variant"; ImageId=$id; Bom="babylog-container-$variant.cdx.json"; Scope='Final image OS, .NET runtime and recognized application packages'}
        }
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content "$output/manifest.json"
    Get-ChildItem $output -Recurse -File | Where-Object Name -ne 'hashes.json' | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash | ConvertTo-Json | Set-Content "$output/hashes.json"
} finally { Pop-Location }
