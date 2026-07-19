#Requires -Version 5.1
<#
.SYNOPSIS
    Renders widget windows offscreen to PNGs in out/, so a styling change can be
    reviewed by looking at it rather than guessed at.

.EXAMPLE
    .\scripts\render.ps1                    # every widget -> out/
    .\scripts\render.ps1 fuel relative      # just these two
    .\scripts\render.ps1 -OutDir img fuel

.NOTES
    tools/RenderWidget is deliberately NOT in IRacingOverlay.sln, so the solution
    build and CI don't carry it. This script builds it on demand.

    PositionalBinding is off deliberately. With it on, PowerShell auto-assigns
    positions in declaration order, so `render.ps1 fuel radar-danger` bound
    "radar-danger" to -OutDir instead of collecting both into -Targets.

    Note also that `dotnet run` does not accept --nologo; passing it gets it
    forwarded to the app as an argument rather than rejected.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    # Targets: standings, relative, fuel, radar, radar-danger, settings.
    # Omit to render all of them - it costs barely more than rendering one.
    [Parameter(Position = 0, ValueFromRemainingArguments)]
    [string[]] $Targets,

    [string] $OutDir = 'out'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

Initialize-Dotnet

$arguments = @('run', '--project', 'tools/RenderWidget', '--verbosity', 'quiet', '--', '--out', $OutDir)
if ($Targets) {
    $arguments += $Targets
}

Invoke-Dotnet @arguments
