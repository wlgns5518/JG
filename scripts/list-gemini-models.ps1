$ErrorActionPreference = 'Stop'
$root = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MarketingAIAgent'
$key = [Text.Encoding]::UTF8.GetString([Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes((Join-Path $root 'gemini.protected')), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser))
try {
    $response = Invoke-WebRequest -Uri 'https://generativelanguage.googleapis.com/v1beta/models?pageSize=1000' -Headers @{ 'x-goog-api-key' = $key } -SkipHttpErrorCheck -TimeoutSec 45
    Write-Output ('Gemini models endpoint HTTP ' + [int]$response.StatusCode)
    if ([int]$response.StatusCode -ne 200) { throw 'Unable to list Gemini models. No response body or keys were logged.' }
    $models = ($response.Content | ConvertFrom-Json).models | Where-Object { $_.supportedGenerationMethods -contains 'generateContent' } | Select-Object name,displayName
    $models | Format-Table -AutoSize
} finally { $key = $null }
