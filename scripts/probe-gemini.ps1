param([string]$Model = 'gemini-2.5-flash')
$ErrorActionPreference = 'Stop'
$root = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MarketingAIAgent'
$key = [Text.Encoding]::UTF8.GetString([Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes((Join-Path $root 'gemini.protected')), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser))
try {
    $body = @{ contents = @(@{ parts = @(@{ text = 'Reply with OK only.' }) }); generationConfig = @{ maxOutputTokens = 32 } } | ConvertTo-Json -Depth 10
    $response = Invoke-WebRequest -Method Post -Uri ('https://generativelanguage.googleapis.com/v1beta/models/' + [Uri]::EscapeDataString($Model) + ':generateContent') -Headers @{ 'x-goog-api-key' = $key } -ContentType 'application/json' -Body $body -SkipHttpErrorCheck -TimeoutSec 60
    Write-Output ('Gemini generateContent HTTP ' + [int]$response.StatusCode + ', model=' + $Model)
    if ([int]$response.StatusCode -ne 200) {
        $errorData = $response.Content | ConvertFrom-Json
        Write-Output ([string]$errorData.error.message).Replace($key, '[REDACTED]')
    } else { Write-Output 'Model generation endpoint returned a successful response.' }
} finally { $key = $null }
