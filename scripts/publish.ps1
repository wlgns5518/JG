param([switch]$Installer)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    dotnet restore MarketingAIAgent.slnx
    if ($LASTEXITCODE) { throw 'Restore failed' }
    dotnet build MarketingAIAgent.slnx -c Release --no-restore
    if ($LASTEXITCODE) { throw 'Build failed' }
    dotnet test MarketingAIAgent.slnx -c Release --no-build --no-restore --logger trx --results-directory artifacts/test-results
    if ($LASTEXITCODE) { throw 'Tests failed' }
    dotnet publish src/MarketingAIAgent.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/publish/win-x64
    if ($LASTEXITCODE) { throw 'Publish failed' }
    if ($Installer) {
        $compiler = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $compiler) { throw 'Install Inno Setup 6, then rerun with -Installer.' }
        & $compiler scripts/installer.iss
        if ($LASTEXITCODE) { throw 'Installer build failed' }
    }
} finally { Pop-Location }
