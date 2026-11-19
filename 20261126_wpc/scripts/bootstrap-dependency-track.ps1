[CmdletBinding()]
param([string]$ApiUrl = 'http://localhost:8091', [switch]$UseLocalOsvSnapshot, [switch]$Standalone)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$secretDir = Join-Path $root '.secrets'
$credentialsName = if ($Standalone) { 'dependency-track-standalone' } else { 'dependency-track' }
$credentialsPath = Join-Path $secretDir "$credentialsName.json"
$reportDir = Join-Path $root ".artifacts/$credentialsName"
if ($Standalone -and !$PSBoundParameters.ContainsKey('ApiUrl')) { $ApiUrl = 'http://127.0.0.1:8190' }
New-Item -ItemType Directory -Force $secretDir,$reportDir | Out-Null
$ApiUrl = $ApiUrl.TrimEnd('/')
Invoke-WebRequest "$ApiUrl/api/openapi.json" -OutFile (Join-Path $reportDir 'openapi.json')
if (Test-Path $credentialsPath) { $credentials = Get-Content $credentialsPath -Raw | ConvertFrom-Json }
else {
    $credentials = [pscustomobject]@{ apiUrl=$ApiUrl; username='admin'; password=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)); uploadApiKey=$null; uploadTeamUuid=$null }
    # Persist the intended password BEFORE changing it, so interrupted bootstrap is recoverable.
    $credentials | ConvertTo-Json | Set-Content $credentialsPath
}
$login = @{username=$credentials.username;password=$credentials.password}
try { $token = Invoke-RestMethod "$ApiUrl/api/v1/user/login" -Method Post -ContentType 'application/x-www-form-urlencoded' -Body $login }
catch {
    try {
        $null = Invoke-RestMethod "$ApiUrl/api/v1/user/forceChangePassword" -Method Post -ContentType 'application/x-www-form-urlencoded' -Body @{username='admin';password='admin';newPassword=$credentials.password;confirmPassword=$credentials.password}
        $token = Invoke-RestMethod "$ApiUrl/api/v1/user/login" -Method Post -ContentType 'application/x-www-form-urlencoded' -Body $login
    }
    catch { throw "Login/bootstrap fallito. Le credenziali locali sono in $credentialsPath." }
}
$headers = @{Authorization="Bearer $token"}
function Api([string]$Method,[string]$Path,$Body) {
    $requestArgs = @{Uri="$ApiUrl/api/v1/$Path";Method=$Method;Headers=$headers;ContentType='application/json'}
    if ($null -ne $Body) { $requestArgs.ContentType='application/json'; $requestArgs.Body=ConvertTo-Json -InputObject $Body -Depth 25 -Compress }
    try { Invoke-RestMethod @requestArgs } catch { throw "API $Method $Path failed: $($_.Exception.Message)" }
}
$teams = Api Get 'team'
$team = $teams | Where-Object name -eq 'BabyLog BOM uploader' | Select-Object -First 1
if (-not $team) { $team = Api Put 'team' @{name='BabyLog BOM uploader'} }
# No admin, vulnerability management or system configuration privileges on this key.
foreach ($permission in @('BOM_UPLOAD','PROJECT_CREATION_UPLOAD','VIEW_PORTFOLIO','VIEW_VULNERABILITY')) {
    if ($team.permissions.name -notcontains $permission) { $team=Api Post "permission/$permission/team/$($team.uuid)" }
}
if (-not $credentials.uploadApiKey) {
    $key=Api Put "team/$($team.uuid)/key"
    $credentials.uploadApiKey=$key.key
    $credentials.uploadTeamUuid=$team.uuid
    $credentials | ConvertTo-Json | Set-Content $credentialsPath
}
$policies = Api Get 'policy'
$policy = $policies | Where-Object name -eq 'BabyLog High Critical' | Select-Object -First 1
if (-not $policy) { $policy=Api Put 'policy' @{name='BabyLog High Critical';operator='ANY';violationState='FAIL';global=$true;includeChildren=$true;onlyLatestProjectVersion=$false} }
foreach($severity in @('HIGH','CRITICAL')) {
    if (-not ($policy.policyConditions | Where-Object { $_.subject -eq 'SEVERITY' -and $_.value -eq $severity })) {
        $null=Api Put "policy/$($policy.uuid)/condition" @{subject='SEVERITY';operator='IS';value=$severity}
    }
}
$rule = $null
if (!$Standalone) {
    $publishers=Api Get 'notification/publisher'
    $publisher=$publishers | Where-Object { $_.publisherClass -match 'WebhookPublisher$' } | Select-Object -First 1
    if (-not $publisher) { throw 'Publisher Webhook non trovato nella versione installata.' }
    $rules=Api Get 'notification/rule'
    $rule=$rules | Where-Object name -eq 'BabyLog local monitoring' | Select-Object -First 1
    $ruleBody=@{name='BabyLog local monitoring';enabled=$true;scope='PORTFOLIO';notificationLevel='INFORMATIONAL';notifyOn=@('NEW_VULNERABILITY','NEW_VULNERABLE_DEPENDENCY','POLICY_VIOLATION','BOM_PROCESSED');publisher=@{uuid=$publisher.uuid};publisherConfig=(@{destination='http://notification-receiver:8080/notifications'}|ConvertTo-Json -Compress);logSuccessfulPublish=$true}
    if ($rule) { $ruleBody.uuid=$rule.uuid; $rule=Api Post 'notification/rule' $ruleBody } else { $rule=Api Put 'notification/rule' $ruleBody }
}
$baseUrl = 'https://osv-vulnerabilities.storage.googleapis.com/'
if ($UseLocalOsvSnapshot) {
    $baseUrl = if ($Standalone) { 'http://127.0.0.1:8192/' } else { 'http://notification-receiver:8080/osv/' }
}
$null=Api Post 'configProperty' @{groupName='vuln-source';propertyName='google.osv.base.url';propertyValue=$baseUrl;propertyType='URL'}
$config=Api Get 'configProperty'
$osv=$config | Where-Object { $_.groupName -eq 'vuln-source' -and $_.propertyName -eq 'google.osv.enabled' }
if ($osv.propertyValue -ne 'NuGet') {
    $null=Api Post 'configProperty' @{groupName='vuln-source';propertyName='google.osv.enabled';propertyValue='NuGet';propertyType='STRING'}
    Write-Host 'Mirror OSV NuGet configurato. Riavviare il server per avviare la sincronizzazione.'
    $config=Api Get 'configProperty'
}
$config | Where-Object { $_.propertyName -match 'password|token|key|secret' } | ForEach-Object { $_ | Add-Member -NotePropertyName propertyValue -NotePropertyValue '[REDACTED]' -Force }
$config | Where-Object { $_.groupName -match 'vuln|analyzer|ossindex|osv|nvd|github' } | Select-Object groupName,propertyName,propertyValue,propertyType | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $reportDir 'feed-configuration.json')
@{apiUrl=$ApiUrl;bootstrappedAt=[DateTime]::UtcNow.ToString('o');uploadTeamUuid=$team.uuid;uploadPermissions=@($team.permissions.name);policyUuid=$policy.uuid;notificationRuleUuid=$rule.uuid;credentialsFile=".secrets/$credentialsName.json";feedStatus='Configuration recorded. Mirror startup/analysis readiness must be checked; absence of findings is not a clean result.'} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $reportDir 'bootstrap.json')
Write-Host "Bootstrap completato. Credenziali salvate in .secrets/$credentialsName.json."






