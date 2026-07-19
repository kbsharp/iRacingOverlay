#Requires -Version 5.1
<#
.SYNOPSIS
    Shared helpers for the scripts in this folder. Dot-source it, don't run it.

    The point of this file is that the "where is dotnet?" question is answered
    in exactly one place. The .NET 8 SDK is often installed per-user at
    %LOCALAPPDATA%\Microsoft\dotnet, which is not on the system PATH — every
    script and doc used to carry its own copy of the fix-up incantation.
#>

$script:RepoRoot = Split-Path -Parent $PSScriptRoot

function Initialize-Dotnet {
    <#
    .SYNOPSIS
        Makes `dotnet` resolvable for the rest of this process, or throws.
    #>
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        return
    }

    $userInstall = "$env:LOCALAPPDATA\Microsoft\dotnet"
    if (Test-Path (Join-Path $userInstall 'dotnet.exe')) {
        $env:PATH = "$userInstall;$env:PATH"
        return
    }

    throw "The .NET 8 SDK was not found on PATH or at $userInstall. Install it with: winget install Microsoft.DotNet.SDK.8"
}

function Invoke-Dotnet {
    <#
    .SYNOPSIS
        Runs a dotnet command from the repo root and throws on a non-zero exit,
        so a failure stops the script instead of scrolling past.

    .NOTES
        Pass dotnet's LONG-form flags only (`--verbosity quiet`, not `-v q`).
        PowerShell binds a single-dash argument to any common parameter it
        prefix-matches before ValueFromRemainingArguments sees it — `-v` is
        swallowed as `-Verbose`, and the value after it is passed to dotnet on
        its own, which fails with a baffling `MSB1008: Only one project can be
        specified`.
    #>
    param([Parameter(Mandatory, ValueFromRemainingArguments)][string[]] $Arguments)

    Push-Location $script:RepoRoot
    try {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet $($Arguments -join ' ') failed (exit code $LASTEXITCODE)."
        }
    }
    finally {
        Pop-Location
    }
}

function Stop-RunningOverlay {
    <#
    .SYNOPSIS
        Kills any live overlay process so the build can overwrite its DLLs.

        Without this, a build after a detached launch fails with
        "MSB3026: ... The file is locked by IRacingOverlay (pid)".
    #>
    $running = Get-Process IRacingOverlay -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Stopping $($running.Count) running overlay process(es) so the build can write." -ForegroundColor Yellow
        $running | Stop-Process -Force
        Start-Sleep -Milliseconds 300
    }
}
