#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the whole solution. TreatWarningsAsErrors is on, so a warning fails.

.EXAMPLE
    .\scripts\build.ps1
    .\scripts\build.ps1 -Configuration Release
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

Initialize-Dotnet
Stop-RunningOverlay
Invoke-Dotnet build --configuration $Configuration --nologo

Write-Host "Build succeeded ($Configuration)." -ForegroundColor Green
