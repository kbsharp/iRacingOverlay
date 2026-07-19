#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and launches the overlay in live mode, detached from this shell.

    Unlike `dotnet run`, the app is started as an independent process, so
    closing this terminal window does NOT stop it. Use the tray icon to
    control or exit it. Waits for iRacing to start broadcasting telemetry.
#>

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

Initialize-Dotnet
Stop-RunningOverlay
Invoke-Dotnet build src/IRacingOverlay.App --nologo --verbosity quiet

$exe = Join-Path $RepoRoot 'src\IRacingOverlay.App\bin\Debug\net8.0-windows\IRacingOverlay.Dev.exe'
Start-Process -FilePath $exe

Write-Host "Overlay started in live mode. This terminal can be closed safely." -ForegroundColor Green
Write-Host "Look for the tray icon to show/hide widgets or exit." -ForegroundColor Green
