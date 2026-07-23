#Requires -Version 5.1
<#
.SYNOPSIS
    Renders widget windows offscreen to PNGs in out/, so a styling change can be
    reviewed by looking at it rather than guessed at.

.EXAMPLE
    .\scripts\render.ps1                    # every widget -> out/
    .\scripts\render.ps1 fuel relative      # just these two
    .\scripts\render.ps1 -OutDir img fuel
    .\scripts\render.ps1 -ColorBlind -OutDir out-cb   # colour-blind palette
    .\scripts\render.ps1 -Grips fuel                 # with the resize grip showing

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
    # Targets: standings, relative, fuel, fuel-pit-exit, radar, radar-danger,
    # radar-unresolved, delta, settings.
    # Omit to render all of them - it costs barely more than rendering one.
    [Parameter(Position = 0, ValueFromRemainingArguments)]
    [string[]] $Targets,

    [string] $OutDir = 'out',

    # Render in the colour-blind-friendly palette instead of the default. Pair with
    # -OutDir so the two palettes land in separate folders for comparison.
    [switch] $ColorBlind,

    # Force the corner resize grips visible. They only appear on hover, and nothing
    # hovers anything in a headless render, so this is the only way to review them.
    [switch] $Grips
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

Initialize-Dotnet

$arguments = @('run', '--project', 'tools/RenderWidget', '--verbosity', 'quiet', '--', '--out', $OutDir)
if ($ColorBlind) {
    $arguments += '--colorblind'
}
if ($Grips) {
    $arguments += '--grips'
}
if ($Targets) {
    $arguments += $Targets
}

Invoke-Dotnet @arguments
