#Requires -Version 7.0
<#
.SYNOPSIS
    Gemeinsamer Helfer fuer CI-Annotations der Test-Skripte (per Dot-Sourcing eingebunden).

.DESCRIPTION
    Kapselt die GitHub-Actions-Annotation-Syntax "::kind::message", damit der
    Workflow-Befehl nicht in jedem Skript gepflegt wird. Ausserhalb von GitHub
    Actions wird Klartext ausgegeben. Verwendung im aufrufenden Skript:

        . (Join-Path $PSScriptRoot "lib/Annotations.ps1")
#>

function Write-Annotation {
    # Schreibt "::Kind::Message" in GitHub Actions, sonst "[Kind] Message" lokal
    # (optional mit -ForegroundColor eingefaerbt). Die Env-Pruefung steht bewusst in
    # der Funktion, damit Aufrufer keinen eigenen $inGitHubActions-Schalter pflegen.
    param(
        [Parameter(Mandatory = $true)]
        [string]$Kind,
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [System.ConsoleColor]$ForegroundColor
    )
    if ($env:GITHUB_ACTIONS -eq "true") {
        Write-Host "::$Kind`::$Message"
    }
    elseif ($PSBoundParameters.ContainsKey("ForegroundColor")) {
        Write-Host "[$Kind] $Message" -ForegroundColor $ForegroundColor
    }
    else {
        Write-Host "[$Kind] $Message"
    }
}
