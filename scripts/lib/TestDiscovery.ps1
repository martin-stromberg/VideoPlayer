#Requires -Version 7.0
<#
.SYNOPSIS
    Gemeinsame Test-Discovery-Helfer fuer die Test-Skripte (per Dot-Sourcing eingebunden).

.DESCRIPTION
    Kapselt die Auswertung von "dotnet test --list-tests"-Ausgaben, damit Aufruf und
    Zeilen-Parsing nicht in jedem Skript gepflegt werden. Verwendung im aufrufenden Skript:

        . (Join-Path $PSScriptRoot "lib/TestDiscovery.ps1")
#>

function Get-FlakyTestNames {
    # Liefert die Methoden-FQNs aller Tests mit Category=Flaky des Projekts als
    # HashSet[string] (leere Menge bei leerem/fehlgeschlagenem List-Lauf). Der Aufruf
    # nutzt --no-build und setzt daher einen zuvor erfolgten Build voraus.
    # xUnit-Theory-Datenzeilen ("Ns.Class.Method(arg: 1)") werden durch das
    # Praefix-Muster automatisch auf den Methoden-FQN reduziert, weil die
    # Klammer-Argumente nicht zum Match gehoeren.
    # Rueckgabe per ","-Operator: HashSet implementiert IEnumerable und wuerde sonst
    # beim Funktions-Output in die Einzelnamen unrollt werden.
    param([string]$ProjectPath, [string]$Configuration = "Debug")
    $listOutput = & dotnet test $ProjectPath -c $Configuration --no-build --list-tests --filter "Category=Flaky" 2>&1
    $names = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($line in $listOutput) {
        $trimmed = "$line".Trim()
        if ($trimmed -match '^([A-Za-z0-9_]+(\.[A-Za-z0-9_]+)+)') {
            [void]$names.Add($matches[1])
        }
    }
    return ,$names
}

function ConvertTo-MethodName {
    # Theory-Datenzeilen ("Ns.Class.Method(arg: 1)") auf den Methoden-FQN normalisieren,
    # damit TRX-Ergebnisnamen gegen die Methoden-FQNs aus Get-FlakyTestNames bzw.
    # --filter-Ausdruecke vergleichbar sind. Facts ohne Klammer-Argumente bleiben unveraendert.
    param([string]$TestName)
    if ($TestName -match '^([A-Za-z0-9_]+(\.[A-Za-z0-9_]+)+)\(.*\)$') { return $matches[1] }
    return $TestName
}
