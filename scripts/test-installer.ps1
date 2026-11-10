param([string]$Version = '1.0.1')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))
$installTarget = [IO.Path]::GetFullPath((Join-Path $artifacts ('install-smoke-' + $Version)))
if (-not $installTarget.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test path escaped artifacts directory' }
if (Test-Path -LiteralPath $installTarget) { throw 'Installation smoke-test directory already exists. Review it before running again.' }
$setup = Join-Path $artifacts ('installer/MarketingAIAgent-' + $Version + '-win-x64-Setup.exe')
$installLog = Join-Path $artifacts 'install-smoke.log'
$uninstallLog = Join-Path $artifacts 'uninstall-smoke.log'
$process = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', ('/DIR="' + $installTarget + '"'), ('/LOG="' + $installLog + '"')) -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode)" }
$source = Join-Path $artifacts 'publish/win-x64'
$files = @(Get-ChildItem -LiteralPath $source -Recurse -File -Force)
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
    $installed = Join-Path $installTarget $relative
    if (-not (Test-Path -LiteralPath $installed)) { throw "Missing installed file: $relative" }
    if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash) { throw "Hash mismatch: $relative" }
}
Write-Output "INSTALL PASS: $($files.Count) files matched SHA256, including Playwright driver and .NET runtime."
$uninstaller = Join-Path $installTarget 'unins000.exe'
if (-not (Test-Path -LiteralPath $uninstaller)) { throw 'Uninstaller missing' }
$process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + $uninstallLog + '"')) -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Uninstaller failed: $($process.ExitCode)" }
if (Test-Path -LiteralPath (Join-Path $installTarget 'MarketingAIAgent.exe')) { throw 'Application executable remains after uninstall' }
Write-Output 'UNINSTALL PASS: exit code 0 and application executable removed. App launch and UI were not tested.'
