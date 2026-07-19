#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the Core unit tests (the only project with tests, by design — see
    docs/DEVELOPMENT.md § Testing conventions).

.EXAMPLE
    .\scripts\test.ps1
    .\scripts\test.ps1 -Filter FuelCalculator
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    # Passed through to `dotnet test --filter`, e.g. -Filter FuelCalculator.
    [string] $Filter
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

Initialize-Dotnet
Stop-RunningOverlay

$arguments = @('test', '--configuration', $Configuration, '--nologo')
if ($Filter) {
    $arguments += @('--filter', $Filter)
}

Invoke-Dotnet @arguments
