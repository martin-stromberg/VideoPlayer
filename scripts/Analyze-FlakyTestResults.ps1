#Requires -Version 7.0
<#
.SYNOPSIS
    Wertet TRX-Dateien auf Flake-Ereignisse aus und meldet Quarantaene-Kandidaten.

.DESCRIPTION
    Ein "Lauf" wird ueber die Dateinamens-Konvention gruppiert: <name>.trx ist der
    Primaerlauf, <name>-flaky-retry.trx im selben Verzeichnis der zugehoerige Retry
    (erzeugt von scripts/Retry-FlakyTests.ps1 bzw. dem CI-Schritt "Retry flaky tests once").

    Als Flake-Ereignis zaehlt:
      - Fehlschlag im Primaer-TRX + gruen im Retry-TRX desselben Laufs, oder
      - Fehlschlag im Primaer-TRX von Lauf N gefolgt von gruen im Primaer-TRX des
        naechsten Laufs, der den Test enthaelt (sortiert nach dem Lauf-Zeitstempel
        aus dem TRX <Times start>-Attribut, LastWriteTimeUtc als Fallback).

    Primaer-Outcomes ausserhalb Passed/Failed (z. B. NotExecuted durch CI-Skip-Flags
    wie SOFTWARESCHMIEDE_SKIP_*) gelten als fehlende Beobachtung: Sie erhoehen weder
    den Ausfuehrungszaehler noch die Fehlrate und unterbrechen die Lauf-Sequenz
    nicht (kein Phantom-"gruen im Folgelauf" nach einem Skip, kein verlorenes
    fail->pass ueber einen Skip hinweg).

    Klassifikation pro Testname (Fenster: letzte -MaxRuns Primaer-Laeufe):
      - Quarantaene-Kandidat: >= QuarantineMinFlakeEvents Flake-Ereignisse ODER
        Fehlrate >= QuarantineMinFailureRate bei >= QuarantineMinExecutions Ausfuehrungen.
      - beobachten: >= 1 Flake-Ereignis ODER Fehlrate >= WatchMinFailureRate.
      - stabil: sonst.

    Reiner Report — der Exit-Code ist immer 0. Fuer Quarantaene-Kandidaten wird eine
    Empfehlung ausgegeben (Quarantaene-Issue anlegen + Fehlerbild in
    docs/help/stabilitaet/os-interface-tests.md dokumentieren); in GitHub Actions
    zusaetzlich als ::warning::-Annotation.

.PARAMETER Path
    Ein oder mehrere Verzeichnisse, die rekursiv nach TRX-Dateien durchsucht werden —
    z. B. lokale Laeufe (test-results/<zeitstempel>/trx, src/*/TestResults) oder per
    "gh run download <run-id> -n test-results-os-interface-<pr|staging>" geholte
    CI-Artefakte.

.PARAMETER MaxRuns
    Auswertungsfenster: nur die letzten N Primaer-Laeufe (nach dem Lauf-Zeitstempel
    im TRX, LastWriteTimeUtc als Fallback) zaehlen. Standard: 20.

.PARAMETER QuarantineMinFlakeEvents
    Ab so vielen Flake-Ereignissen im Fenster gilt ein Test als Quarantaene-Kandidat.
    Standard: 2 (Startwert, kalibrierbar).

.PARAMETER QuarantineMinFailureRate
    Fehlraten-Schwelle fuer die Quarantaene (zusammen mit QuarantineMinExecutions).
    Standard: 0.2 (Startwert, kalibrierbar).

.PARAMETER QuarantineMinExecutions
    Mindestanzahl Primaer-Ausfuehrungen, bevor die Fehlraten-Quarantaene greift.
    Standard: 5 (Startwert, kalibrierbar).

.PARAMETER WatchMinFailureRate
    Fehlraten-Schwelle fuer die Beobachtungsstufe. Standard: 0.1 (Startwert, kalibrierbar).

.PARAMETER ReEntryMinConsecutivePasses
    Re-Entry-Kriterium: so viele aufeinanderfolgende Primaer-Durchlaeufe ohne
    Fehlschlag (in Laeufen, die den Test enthalten) muss ein Flaky-markierter
    Test gesammelt haben, damit er als Re-Entry-Kandidat gemeldet wird.
    Standard: 10 (halbes Beobachtungsfenster, kalibrierbar).

.PARAMETER TestProject
    Pfad zum Testprojekt (.csproj), ueber das die Menge der Flaky-markierten
    Tests fuer die Re-Entry-Auswertung per --list-tests ermittelt wird.
    Standard: VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj (relativ zum
    Repository-Stamm bzw. Arbeitsverzeichnis). Nicht auffindbar/unparsbar ->
    die Re-Entry-Auswertung wird uebersprungen (Exit bleibt 0).

.PARAMETER Configuration
    Build-Konfiguration fuer den --list-tests-Aufruf (Standard: Release, wie
    die CI-Lanes; --no-build setzt einen zuvor erfolgten Build voraus).

.EXAMPLE
    ./scripts/Analyze-FlakyTestResults.ps1 -Path test-results/2026-11-20_10-00-00/trx

.EXAMPLE
    gh run download 123456789 -n test-results-os-interface-pr -D artifacts/os-pr
    ./scripts/Analyze-FlakyTestResults.ps1 -Path artifacts/os-pr
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path,
    [int]$MaxRuns = 20,
    [int]$QuarantineMinFlakeEvents = 2,
    [double]$QuarantineMinFailureRate = 0.2,
    [int]$QuarantineMinExecutions = 5,
    [double]$WatchMinFailureRate = 0.1,
    [int]$ReEntryMinConsecutivePasses = 10,
    [string]$TestProject = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$inGitHubActions = $env:GITHUB_ACTIONS -eq "true"

. (Join-Path $PSScriptRoot "lib/Trx.ps1")
. (Join-Path $PSScriptRoot "lib/TestDiscovery.ps1")

function ConvertTo-MethodName {
    # Theory-Datenzeilen ("Ns.Class.Method(arg: 1)") auf den Methoden-FQN normalisieren.
    param([string]$TestName)
    if ($TestName -match '^([A-Za-z0-9_]+(\.[A-Za-z0-9_]+)+)\(.*\)$') { return $matches[1] }
    return $TestName
}

# 1. TRX-Dateien einsammeln und in Primaer/Retry aufteilen
$trxFiles = @()
foreach ($dir in $Path) {
    if (Test-Path $dir) {
        $trxFiles += @(Get-ChildItem -Path $dir -Filter "*.trx" -Recurse -File)
    }
}

$primaries = [System.Collections.Generic.List[object]]::new()
$retries = [System.Collections.Generic.List[object]]::new()
foreach ($file in $trxFiles) {
    if ($file.BaseName -like "*-flaky-retry") {
        $retries.Add($file)
    }
    else {
        $primaries.Add($file)
    }
}
# Lauf-Reihenfolge primaer ueber den Lauf-Zeitstempel im TRX (<Times start="...">):
# LastWriteTimeUtc spiegelt bei per "gh run download" geholten Artefakten nur den
# Download-Zeitpunkt wider und ist daher kein zuverlaessiges Ordnungskriterium.
$primaries = @($primaries | Sort-Object {
    $runStart = Get-TrxRunStartTime -TrxPath $_.FullName
    if ($null -ne $runStart) { $runStart } else { $_.LastWriteTimeUtc }
} | Select-Object -Last $MaxRuns)

Write-Host "==> $($primaries.Count) Primaer-Lauf/Laeufe und $($retries.Count) Retry-TRX im Fenster (MaxRuns=$MaxRuns)."

if ($primaries.Count -eq 0) {
    Write-Host "==> Keine TRX-Dateien gefunden — leerer Report."
    exit 0
}

# 2. Laeufe parsen (Primaer + gepaarter Retry im selben Verzeichnis)
$runs = [System.Collections.Generic.List[object]]::new()
foreach ($primary in $primaries) {
    $primaryResults = Get-TrxTestResults -TrxPath $primary.FullName
    if ($null -eq $primaryResults) { continue }
    $retryPath = Join-Path $primary.DirectoryName "$($primary.BaseName)-flaky-retry.trx"
    $retryResults = $null
    if (Test-Path $retryPath) {
        $retryResults = Get-TrxTestResults -TrxPath $retryPath
    }
    $runs.Add([PSCustomObject]@{
        Name    = $primary.BaseName
        Primary = $primaryResults
        Retry   = $retryResults
    })
}

# 3. Pro Testname: Ausfuehrungen, Fehlschlaege, Flake-Ereignisse
$stats = @{}
# Re-Entry-Zaehlung pro Methoden-FQN (Theory-Datenzeilen normalisiert): Zahl der
# aufeinanderfolgenden fehlerfreien Primaer-Beobachtungen.
$passStreaks = @{}
foreach ($run in $runs) {
    foreach ($entry in $run.Primary.GetEnumerator()) {
        $name = $entry.Key
        # Nicht-Passed/Failed-Primaer-Outcomes (z. B. NotExecuted durch CI-Skip-Flags
        # wie SOFTWARESCHMIEDE_SKIP_* oder Discovery-Ausfaelle) zaehlen als fehlende
        # Beobachtung: keine Ausfuehrung, keine Sequenz-Transition, kein
        # LastOutcome-Update. Sonst erzeugt fail->skip ein Phantom-"gruen im
        # Folgelauf", verwaessert Executions/Fehlrate und loescht ein gemerktes
        # LastOutcome=Failed (echte fail->skip->pass-Sequenz ginge verloren).
        if ($entry.Value -ne "Passed" -and $entry.Value -ne "Failed") { continue }
        $failed = $entry.Value -eq "Failed"
        if (-not $stats.ContainsKey($name)) {
            $stats[$name] = [PSCustomObject]@{
                Executions               = 0
                Failures                 = 0
                FlakeEvents              = 0
                LastOutcome              = $null
                ConsecutivePrimaryPasses = 0
            }
        }
        $stat = $stats[$name]
        $stat.Executions++
        if ($failed) { $stat.Failures++ }
        $retried = $null -ne $run.Retry -and $run.Retry.Contains($name)
        $retryOutcome = if ($retried) { $run.Retry[$name] } else { $null }
        # Nur "Passed" gilt als gruen — andere Retry-Outcomes (z. B. NotExecuted durch ein
        # SOFTWARESCHMIEDE_SKIP_*-Flag oder einen Discovery-Ausfall im gefilterten Re-Run)
        # sind kein Bestandsnachweis und duerfen kein Flake-Ereignis erzeugen (analog
        # Retry-FlakyTests.ps1).
        $recovered = $failed -and $retried -and $retryOutcome -eq "Passed"
        if ($recovered) {
            # Fehlschlag im Primaer + gruen im Retry desselben Laufs.
            $stat.FlakeEvents++
        }
        # Fuer die Primaer-zu-Primaer-Sequenz zaehlt der effektive Lauf-Ausgang: Ein durch
        # den Retry geretteter Lauf gilt als gruen, damit ein Flake-Ereignis nicht doppelt
        # (Retry-gruen + Folgelauf-gruen) gezaehlt wird. Nur Passed/Failed-Retry-Outcomes
        # werden uebernommen — ein nicht ausgefuehrter Re-Run veraendert den Primaer-Ausgang
        # nicht.
        $effectiveOutcome = if ($retryOutcome -eq "Passed" -or $retryOutcome -eq "Failed") { $retryOutcome } else { $entry.Value }
        if ($effectiveOutcome -ne "Failed" -and $stat.LastOutcome -eq "Failed") {
            # Fehlschlag im Primaer von Lauf N, gruen im Primaer des naechsten Laufs.
            $stat.FlakeEvents++
        }
        $stat.LastOutcome = $effectiveOutcome
        # Re-Entry-Kriterium: Nur ein Primaer-"Passed" zaehlt als fehlerfreier
        # Durchlauf. Jeder Primaer-Fehlschlag setzt die Serie zurueck — auch ein
        # vom Retry geretteter (recovered), siehe Quarantaene-/Re-Entry-Regel in
        # docs/help/stabilitaet/os-interface-tests.md.
        $methodName = ConvertTo-MethodName $name
        if ($entry.Value -eq "Passed") {
            $passStreaks[$methodName] = ($passStreaks[$methodName] ?? 0) + 1
        }
        else {
            $passStreaks[$methodName] = 0
        }
    }
}

# 4. Klassifikation und Report
$report = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $stats.GetEnumerator()) {
    $stat = $entry.Value
    $failureRate = if ($stat.Executions -gt 0) { $stat.Failures / $stat.Executions } else { 0 }
    $classification = "stabil"
    if ($stat.FlakeEvents -ge $QuarantineMinFlakeEvents -or
        ($stat.Executions -ge $QuarantineMinExecutions -and $failureRate -ge $QuarantineMinFailureRate)) {
        $classification = "Quarantaene-Kandidat"
    }
    elseif ($stat.FlakeEvents -ge 1 -or $failureRate -ge $WatchMinFailureRate) {
        $classification = "beobachten"
    }
    $report.Add([PSCustomObject]@{
        Test           = $entry.Key
        Executions     = $stat.Executions
        Failures       = $stat.Failures
        FlakeEvents    = $stat.FlakeEvents
        FailureRate    = "{0:P0}" -f $failureRate
        Classification = $classification
    })
}

$severity = @{ "Quarantaene-Kandidat" = 0; "beobachten" = 1; "stabil" = 2 }
$sorted = @($report | Sort-Object { $severity[$_.Classification] }, Test)

Write-Host ""
Write-Host "| Test | Ausfuehrungen | Fehlschlaege | Flake-Ereignisse | Fehlrate | Klassifikation |"
Write-Host "|---|---|---|---|---|---|"
foreach ($row in $sorted) {
    Write-Host "| $($row.Test) | $($row.Executions) | $($row.Failures) | $($row.FlakeEvents) | $($row.FailureRate) | $($row.Classification) |"
}
Write-Host ""

$candidates = @($report | Where-Object { $_.Classification -eq "Quarantaene-Kandidat" })
$watched = @($report | Where-Object { $_.Classification -eq "beobachten" })
Write-Host "==> $($report.Count) Tests ausgewertet: $($candidates.Count) Quarantaene-Kandidat(en), $($watched.Count) beobachten, $($report.Count - $candidates.Count - $watched.Count) stabil."

foreach ($candidate in $candidates) {
    $message = "Quarantaene-Kandidat: $($candidate.Test) ($($candidate.FlakeEvents) Flake-Ereignisse, Fehlrate $($candidate.FailureRate)) — Empfehlung: Quarantaene-Issue anlegen + Fehlerbild in docs/help/stabilitaet/os-interface-tests.md dokumentieren."
    if ($inGitHubActions) { Write-Host "::warning::$message" }
    else { Write-Host "[warning] $message" -ForegroundColor Yellow }
}

# 5. Re-Entry-Kandidaten: Flaky-markierte Tests mit >= ReEntryMinConsecutivePasses
# aufeinanderfolgenden fehlerfreien Primaer-Durchlaeufen.
if ([string]::IsNullOrWhiteSpace($TestProject)) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $TestProject = Join-Path $repoRoot "VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj"
}
if (Test-Path $TestProject) {
    $flakyMethods = Get-FlakyTestNames -ProjectPath $TestProject -Configuration $Configuration
    $reEntryCandidates = @()
    foreach ($method in $flakyMethods) {
        $streak = $passStreaks[$method] ?? 0
        if ($streak -ge $ReEntryMinConsecutivePasses) {
            $reEntryCandidates += [PSCustomObject]@{ Test = $method; Passes = $streak }
        }
    }
    foreach ($candidate in $reEntryCandidates) {
        $message = "Re-Entry-Kandidat: $($candidate.Test) ($($candidate.Passes) aufeinanderfolgende fehlerfreie Primaer-Durchlaeufe) — Empfehlung: bei dokumentiertem Fix [Flaky]-Trait per PR entfernen und Quarantaene-Issue schliessen (Konvention: docs/help/stabilitaet/os-interface-tests.md)."
        if ($inGitHubActions) { Write-Host "::notice::$message" }
        else { Write-Host "[notice] $message" -ForegroundColor Cyan }
    }
    if ($flakyMethods.Count -gt 0) {
        Write-Host "==> $($flakyMethods.Count) Flaky-markierte(r) Test(s), davon $($reEntryCandidates.Count) Re-Entry-Kandidat(en) (Kriterium: >= $ReEntryMinConsecutivePasses aufeinanderfolgende fehlerfreie Primaer-Durchlaeufe)."
    }
}
else {
    Write-Host "==> Re-Entry-Auswertung uebersprungen: Testprojekt '$TestProject' nicht gefunden."
}

exit 0
