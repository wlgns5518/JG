param([string]$KeyFile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'api-keys.txt'))
$ErrorActionPreference = 'Stop'
try { $settings = Get-Content -LiteralPath $KeyFile -Raw | ConvertFrom-Json } catch { throw 'API key file is missing or invalid JSON. Key contents were not logged.' }
if ([string]::IsNullOrWhiteSpace($settings.YouTube.ApiKey) -or [string]::IsNullOrWhiteSpace($settings.Gemini.ApiKey)) { throw 'YouTube.ApiKey and Gemini.ApiKey are required.' }
$dataRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MarketingAIAgent'
[IO.Directory]::CreateDirectory($dataRoot) | Out-Null
foreach ($entry in @(@{ Name = 'youtube'; Key = [string]$settings.YouTube.ApiKey }, @{ Name = 'gemini'; Key = [string]$settings.Gemini.ApiKey })) {
    $encrypted = [Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($entry.Key.Trim()), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    $target = Join-Path $dataRoot ($entry.Name + '.protected')
    [IO.File]::WriteAllBytes($target + '.tmp', $encrypted)
    [IO.File]::Move($target + '.tmp', $target, $true)
}
Write-Output 'YouTube and Gemini keys imported into Windows DPAPI CurrentUser storage. No key values were logged.'
if (-not [string]::IsNullOrWhiteSpace($settings.Gemini.Model)) { Write-Output ('Configured model in source file: ' + [string]$settings.Gemini.Model) }
$settings = $null
