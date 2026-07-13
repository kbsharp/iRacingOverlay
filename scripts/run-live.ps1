#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and launches the overlay in live mode, detached from this shell.

    Unlike `dotnet run`, the app is started as an independent process, so
    closing this terminal window does NOT stop it. Use the tray icon to
    control or exit it. Waits for iRacing to start broadcasting telemetry.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnetUserInstall = "$env:LOCALAPPDATA\Microsoft\dotnet"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue) -and (Test-Path $dotnetUserInstall)) {
    $env:PATH = "$dotnetUserInstall;$env:PATH"
}

Push-Location $repoRoot
try {
    dotnet build src/IRacingOverlay.App --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed (exit code $LASTEXITCODE)."
    }

    $exe = Join-Path $repoRoot 'src\IRacingOverlay.App\bin\Debug\net8.0-windows\IRacingOverlay.exe'
    Start-Process -FilePath $exe

    Write-Host "Overlay started in live mode. This terminal can be closed safely." -ForegroundColor Green
    Write-Host "Look for the tray icon to show/hide widgets or exit." -ForegroundColor Green
}
finally {
    Pop-Location
}
