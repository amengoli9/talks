[CmdletBinding()]
param()
. "$PSScriptRoot/../security/common.ps1"
Push-Location $SecurityRoot
try {
    Invoke-Checked dotnet @('tool','restore')
    Invoke-Checked docker @('pull',$SyftImage)
    Invoke-Checked docker @('pull',$GrypeImage)
    New-Item -ItemType Directory -Force '.artifacts/security' | Out-Null
    $tools = [ordered]@{ MicrosoftSbom='4.1.5'; CycloneDx='6.3.1'; Syft=$SyftImage; Grype=$GrypeImage; VerifiedAt=(Get-Date).ToUniversalTime().ToString('o') }
    $tools | ConvertTo-Json | Set-Content '.artifacts/security/tools.json'
    Invoke-Checked dotnet @('tool','run','sbom-tool','--','--version')
    Invoke-Checked dotnet @('tool','run','dotnet-CycloneDX','--','--version')

} finally { Pop-Location }
