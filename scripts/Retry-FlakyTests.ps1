#Requires -Version 7.0
<#
.SYNOPSIS
    Wertet das Primaer-TRX eines Testlaufs aus und wiederholt fehlgeschlagene, als
    "Flaky" markierte Tests genau einmal gefiltert.

.DESCRIPTION
    Ablauf:
      1. Primaer-TRX parsen: Namen aller "Failed"-Ergebnisse einsammeln (Theory-
         Datenzeilen werden auf den Methoden-FQN normalisiert).
      2. Flaky-Menge des Projekts via "dotnet test --no-build --list-tests
         --filter Category=Flaky" ermitteln.
      3. Schnittmenge leer  -> "keine Flaky-Fehlschlaege", Exit 0 (kein Retry).
         Sonst genau ein Re-Run: --filter "(Category=Flaky)&(FullyQualifiedName~...)".
      4. Retry-TRX auswerten: recovered (fehlgeschlagen -> bestanden) vs. persistent.
         Persistente Fehlschlaege -> Exit 1 (ehrliches Signal; in der CI bleibt der
         Job durch continue-on-error trotzdem nicht-blockierend).

    Fehlendes oder unauswertbares Primaer-TRX gilt als "keine Flaky-Fehlschlaege"
    (Exit 0), damit ein als if: failure() laufender CI-Schritt bei Build-Fehlschlag
    oder fehlendem TRX nichts verschleiert.

.PARAMETER TestProject
    Pfad zum Testprojekt (.csproj), dessen Primaerlauf ausgewertet wird.

.PARAMETER PrimaryTrx
    Pfad zur TRX-Datei des Primaerlaufs.

.PARAMETER Configuration
    Build-Konfiguration (Standard: Debug).

.PARAMETER RetryTrxFileName
    Dateiname des Retry-TRX im selben TestResults-Verzeichnis.
    Standard: <primaername>-flaky-retry.trx

.EXAMPLE
    ./scripts/Retry-FlakyTests.ps1 -TestProject VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -PrimaryTrx VideoWebPlayer.Tests/TestResults/test-results-os-interface.trx -Configuration Release
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TestProject,
    [Parameter(Mandatory = $true)]
    [string]$PrimaryTrx,
    [string]$Configuration = "Debug",
    [string]$RetryTrxFileName = ""
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "lib/Trx.ps1")
. (Join-Path $PSScriptRoot "lib/TestDiscovery.ps1")
. (Join-Path $PSScriptRoot "lib/Annotations.ps1")

# 1. Primaer-TRX auswerten
$primaryResults = Get-TrxTestResults -TrxPath $PrimaryTrx
if ($null -eq $primaryResults) {
    Write-Host "Keine Flaky-Fehlschlaege: Primaer-TRX '$PrimaryTrx' fehlt oder ist unauswertbar."
    exit 0
}

$failedMethods = @($primaryResults.GetEnumerator() |
    Where-Object { $_.Value -eq "Failed" } |
    ForEach-Object { ConvertTo-MethodName $_.Key } |
    Sort-Object -Unique)
if ($failedMethods.Count -eq 0) {
    Write-Host "Keine Flaky-Fehlschlaege: Der Primaerlauf hat keine fehlgeschlagenen Tests."
    exit 0
}

# 2. Flaky-Menge des Projekts ermitteln (Helper aus lib/TestDiscovery.ps1)
$flakyMethods = Get-FlakyTestNames -ProjectPath $TestProject -Configuration $Configuration

$retryMethods = @($failedMethods | Where-Object { $flakyMethods.Contains($_) })
if ($retryMethods.Count -eq 0) {
    Write-Host "Keine Flaky-Fehlschlaege: $($failedMethods.Count) Fehlschlag/Fehlschlaege im Primaerlauf, davon keiner Flaky-markiert."
    exit 0
}

Write-Host "==> $($retryMethods.Count) Flaky-Fehlschlag/Fehlschlaege werden genau einmal wiederholt:" -ForegroundColor Cyan
$retryMethods | ForEach-Object { Write-Host "    $_" }

# 3. Genau ein gefilterter Re-Run
$resultsDirectory = Split-Path -Parent $PrimaryTrx
if ([string]::IsNullOrEmpty($resultsDirectory)) {
    # Reiner Dateiname ohne Verzeichnisanteil (lokaler Aufruf aus dem TRX-Verzeichnis):
    # "" wuerde --results-directory "" und ein unbrauchbares Join-Path erzeugen.
    $resultsDirectory = "."
}
if ([string]::IsNullOrWhiteSpace($RetryTrxFileName)) {
    $RetryTrxFileName = [IO.Path]::GetFileNameWithoutExtension($PrimaryTrx) + "-flaky-retry.trx"
}
$nameFilter = ($retryMethods | ForEach-Object { "FullyQualifiedName~$_" }) -join "|"
$retryOutput = & dotnet test $TestProject -c $Configuration --no-build `
    --filter "(Category=Flaky)&($nameFilter)" `
    --logger "trx;LogFileName=$RetryTrxFileName" `
    --results-directory $resultsDirectory `
    --logger "console;verbosity=normal" 2>&1
$retryOutput | ForEach-Object { Write-Host $_ }

# 4. Retry-TRX auswerten: Nur "Passed" gilt als recovered — andere Outcomes (z. B.
# NotExecuted durch ein Skip-Flag oder Discovery-Ausfall im gefilterten Re-Run) sind
# kein Bestandsnachweis und werden als eigene, nicht-recovered Kategorie gemeldet.
$retryResults = Get-TrxTestResults -TrxPath (Join-Path $resultsDirectory $RetryTrxFileName)
$persistent = [System.Collections.Generic.List[string]]::new()
$indeterminate = [System.Collections.Generic.List[string]]::new()
foreach ($method in $retryMethods) {
    $retryOutcomes = @()
    if ($null -ne $retryResults) {
        $retryOutcomes = @($retryResults.GetEnumerator() |
            Where-Object { (ConvertTo-MethodName $_.Key) -eq $method } |
            ForEach-Object { $_.Value })
    }
    if ($retryOutcomes.Count -gt 0 -and @($retryOutcomes | Where-Object { $_ -ne "Passed" }).Count -eq 0) {
        Write-Annotation -Kind "notice" -Message "Flaky-Retry recovered: $method (fehlgeschlagen im Primaerlauf, bestanden im Retry)"
    }
    elseif ($retryOutcomes.Count -eq 0 -or @($retryOutcomes | Where-Object { $_ -eq "Failed" }).Count -gt 0) {
        $persistent.Add($method)
    }
    else {
        $indeterminate.Add("$method (Retry-Outcome: $(($retryOutcomes | Sort-Object -Unique) -join ', '))")
    }
}

foreach ($method in $persistent) {
    Write-Annotation -Kind "warning" -Message "Flaky-Retry persistent: $method (fehlgeschlagen im Primaerlauf UND im Retry)"
}
foreach ($method in $indeterminate) {
    Write-Annotation -Kind "warning" -Message "Flaky-Retry nicht recovered: $method (kein Passed-Ergebnis im Retry)"
}

if ($persistent.Count -gt 0 -or $indeterminate.Count -gt 0) {
    Write-Host "==> $($persistent.Count) Flaky-Test(s) auch im Retry fehlgeschlagen (persistent), $($indeterminate.Count) ohne Passed-Ergebnis (nicht recovered)." -ForegroundColor Red
    exit 1
}
Write-Host "==> Alle Flaky-Fehlschlaege im Retry bestanden (recovered)." -ForegroundColor Green
exit 0
