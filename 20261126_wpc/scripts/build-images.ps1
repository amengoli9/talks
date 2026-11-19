#requires -Version 7.0
[CmdletBinding()]
param([string]$TrustedCaBundle, [switch]$UseHostTrust)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    New-Item -ItemType Directory -Force .artifacts/containers | Out-Null
    # One BuildKit graph shares one publish across both final images and export.
    if ($UseHostTrust) {
        $TrustedCaBundle = Join-Path $root '.artifacts/containers/host-trust.pem'
        $certificates = Get-ChildItem Cert:/LocalMachine/Root,Cert:/CurrentUser/Root | Sort-Object Thumbprint -Unique
        ($certificates | ForEach-Object { $_.ExportCertificatePem() }) -join "`n" | Set-Content $TrustedCaBundle
    }
    $buildArgs = @('buildx','bake','--file','containers/docker-bake.hcl')
    $previousTrust = $env:TRUSTED_CA
    if ($TrustedCaBundle) { $env:TRUSTED_CA = (Resolve-Path $TrustedCaBundle).Path }
    & docker @buildArgs 2>&1 | Tee-Object .artifacts/containers/build.log
    $buildExit = $LASTEXITCODE
    $env:TRUSTED_CA = $previousTrust
    if ($buildExit -ne 0) { throw 'Docker image build failed' }
    $report = foreach ($target in @('standard','chiseled')) {
        $result = & docker image inspect "babylog:$target"
        if ($LASTEXITCODE -ne 0) { throw "Image inspection failed: $target" }
        $result | ConvertFrom-Json | Select-Object Id,RepoTags,Size,Architecture,Os,@{n='User';e={$_.Config.User}}
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content .artifacts/containers/images.json
    Get-ChildItem .artifacts/publish -File -Recurse | Get-FileHash -Algorithm SHA256 |
        Select-Object @{n='Path';e={[IO.Path]::GetRelativePath((Join-Path $root '.artifacts/publish'),$_.Path)}},Hash |
        ConvertTo-Json -Depth 3 | Set-Content .artifacts/containers/publish-sha256.json
} finally { Pop-Location }
