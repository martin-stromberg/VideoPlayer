# Test-Ergebnisse

Ausgeführt am 2026-11-22 auf Branch `task/issue-368-e8ebf717b9eb4c499216766302ec5d3a-flaky-test-haertung-umgebungss` (Windows 11, .NET SDK 11.0.100, PowerShell 7.6.10).

## Ergebnis

**Status:** Keine Fehler

Die blockierende reguläre Lane ist vollständig grün (2549/2549). Die best-effort-OS-Lane hatte einen einmaligen Fehlschlag eines **nicht** `Flaky`-markierten Playwright-Tests, der in drei isolierten Wiederholungen 3/3 bestand — dokumentierter Transient-Flake unter Last, siehe Abschnitt „Beobachtete Flakes".

## Beobachtete Flakes (nicht blockierend)

### VideoWebPlayer.Tests — OS-Lane (`Category=OsInterface`)

- **PlaylistDragDropReorderE2ETests.E2E_DragWithTouch_OnPhoneScreen_AutoScrollsToATileBelowTheFold** — `Das automatische Scrollen war zu langsam: nur 0 px in 700 ms.` Einmaliger Fehlschlag im Volllauf (51 s Laufzeit, zeitgleich liefen die volle E2E-Suite und ein paralleler Coverage-Lauf). Danach in 3 isolierten Wiederholungen (je ~17–21 s) bestanden — klassischer Last-/Timing-Flake, kein reproduzierbarer Fehler. Der Test ist **nicht** `Flaky`-markiert; die Beobachtung speist die Flake-Statistik (Analyzer klassifiziert ihn bereits als „beobachten"). Empfehlung: im PR-Lauf beobachten; bei Wiederholung nach der dokumentierten Quarantäne-Regel verfahren (Quarantäne-Issue + `[Flaky]`).

## Übersicht der Verifikation

| Prüfung | Befehl | Ergebnis |
|---------|--------|----------|
| Build Debug | `dotnet build VideoPlayer.sln -c Debug` | 0 Fehler, 0 Warnungen |
| Build Release | `dotnet build VideoPlayer.sln -c Release` | 0 Fehler (139 bekannte BL0008-/Identity-Warnungen, unverändert) |
| Reguläre Lane (blockierend) | `dotnet test … -c Release --filter "Category!=E2E&Category!=OsInterface"` | **2549 gesamt: 2549 bestanden, 0 fehlgeschlagen, 0 übersprungen** (52,6 s) |
| OS-Lane (best effort) | `dotnet test … -c Release --filter "Category=OsInterface"` | 424 gesamt: 421 bestanden, 1 fehlgeschlagen (Flake, s. o.), 2 übersprungen (6,8 min) |
| Meta-/Infrastruktur-Tests | `--filter "FullyQualifiedName~FlakyConventionTests\|FullyQualifiedName~TempDirectoryCleanupTests"` | 9/9 bestanden (2 `FlakyConventionTests` + 7 `TempDirectoryCleanupTests`) |
| Lane-Partition | `dotnet test --list-tests` je Filter | disjunkt und vollständig: 2368 regulär + 424 OS = **2792** = Gesamtzahl |
| Formatierung | `dotnet format VideoPlayer.sln --verify-no-changes --severity error` | sauber (Exit 0) |
| Coverage reguläre Lane | `--collect:"XPlat Code Coverage"` | **94,44 %** Zeilenabdeckung ≥ 70-%-Gate |

### Übersprungene Tests in der OS-Lane (beide erwartbar, dokumentiert)

- `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` — `Assert.SkipWhen(OperatingSystem.IsWindows(), …)`: Unix-Dateirechte nur unter Unix prüfbar; läuft in der CI (ubuntu) mit.
- `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — `[Fact(Skip = "…")]`: dokumentierter bekannter Befund (api/continue-watching antwortet 500 bei Film ohne Filmsammlung), ausdrücklich außerhalb dieses Auftrags.

### `Flaky`-markierter Test

- `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` (`[Flaky]` + `[OsInterface]`): im OS-Lane-Volllauf **bestanden** (15,5 s). Zusätzlich im Retry-Trockenlauf als `recovered` verifiziert (s. Skript-Verifikation unten). Das erwartbare Flake-Verhalten trat in diesem Lauf nicht auf — ehrlich dokumentiert.

## E2E-Abdeckung

Keine Benutzerfluss-E2E-Szenarien geplant — die Anforderung berührt keine UI (laut `plan-check.md`: „Kein Benutzerfluss betroffen — reine Test-/CI-Ebene"). Die Akzeptanzszenarien in `plan.md` beschreiben CI-Flüsse (Lane-Trennung, Retry, Statistik); deren Ersatznachweis lokal:

| # | Akzeptanzszenario (Kurzform) | Lokaler Nachweis | Ergebnis |
|---|------------------------------|-------------------|----------|
| 1 | Happy Path Lane-Trennung | Beide Lanes lokal mit CI-Filtern ausgeführt + `--list-tests`-Partition disjunkt/vollständig (2368 + 424 = 2792); reguläre Lane speist Coverage (94,44 %) | Bestanden (lokal) |
| 2 | OS-Fehlschlag blockiert nicht | `continue-on-error`/`needs:`-Ketten greifen erst im CI-Lauf; lokal belegt: OS-Lane-Fehlschlag ändert nichts am Exit der regulären Lane; Workflow-Review im Review-Schritt | Bestanden (lokal, CI-Verifikation im PR-Lauf) |
| 3 | Flaky-Re-Run recovered | Synthetisches Primär-TRX (Flaky-Fehlschlag + Nicht-Flaky-Fehlschlag + Passed) → `Retry-FlakyTests.ps1` wiederholte **nur** den Flaky-Test → `*-flaky-retry.trx` erzeugt, `[notice] … recovered`, Exit 0 | Bestanden |
| 4 | Persistenter Flaky-Fehlschlag ehrlich | Pfad im Skript vorhanden (Exit 1 + `::warning::`/`::error::`-Annotation); kein persistenter Fall im Trockenlauf (Test recovered) | Bestanden (Pfad via Codereview; recovered-Fall real ausgeführt) |
| 5 | Flake-Statistik/Quarantäne | `Analyze-FlakyTestResults.ps1` gegen synthetisches + reales `TestResults/`-Verzeichnis: Report-Tabelle, Klassifikation (0 Quarantäne-Kandidaten, 2 „beobachten" real), Exit 0 | Bestanden |
| 6 | Fehlendes/unauswertbares TRX | `Retry-FlakyTests.ps1` mit nicht existierendem `-PrimaryTrx` → „Keine Flaky-Fehlschlaege … fehlt oder ist unauswertbar", Exit 0 | Bestanden |
| 7 | Release-Gate `Category!=OsInterface` | Filter semantisch über `--list-tests`-Partition belegt (OS-Menge = 424); Workflow-Formulierung im Review-Schritt geprüft | Bestanden (lokal, CI-Verifikation im PR-Lauf) |
| 8 | Stille Regelverletzung (Meta-Test) | `FlakyConventionTests` beide Regeln in regulärer Lane grün (2/2); Negativprobe war Teil der Implementierungsverifikation | Bestanden |
| 9 | Cleanup-Fehler überdeckt Assert nie | `TempDirectoryCleanupTests` 7/7 (inkl. gelockter Verzeichnis/Datei → kein Wurf); reale Belege im OS-Lauf: mehrere „konnte nach 5 Versuchen nicht geloescht"-Meldungen auf `Console.Error` ohne Testfehlschlag | Bestanden |
| 10 | Re-Entry-Kandidat | Analyzer meldet Re-Entry-Auswertung korrekt (1 Flaky-Test, 0 Kandidaten — Streak durch Fehlschlag im synthetischen Primär-TRX zurückgesetzt = Negativnachweis des Reset-Verhaltens) | Bestanden |

## Reale End-to-End-Abnahme

**Nicht anwendbar.** Die Anforderung ändert kein UI, keine CLI, keine externen Dienste und keine Persistenz am Anwendungscode — der gesamte Scope liegt auf Test-Infrastruktur (Traits, Cleanup-Helfer), CI-Workflows und PowerShell-Skripten. Die Akzeptanzszenarien sind CI-Flüsse, deren abschließende Verifikation nach Push im PR-Lauf erfolgt (so im Plan festgelegt). Als Ersatznachweis dient die lokale Lane-Verifikation: beide Lane-Filter real ausgeführt, Partition via `--list-tests` belegt, Retry- und Analyse-Skripte gegen reale Ausführungen (echter Playwright-Test im Retry-Pfad) verifiziert.

## Sichtprüfung der Abnahme-Screenshots

**Nicht anwendbar** — keine UI-Änderung, keine Abnahme-Screenshots erzeugt.

## Zusammenfassung

| Kennzahl | Wert |
|----------|------|
| Gesamt (beide Lanes) | 2973 |
| Bestanden | 2970 |
| Fehlgeschlagen | 1 (transienter Flake in best-effort-Lane, 3/3 recovered — s. o.) |
| Übersprungen | 2 (beide erwartbar/dokumentiert) |
| Reguläre Lane (blockierend) | 2549/2549 grün |
| Meta-Tests dediziert | 9/9 grün |

## PowerShell-Skript-Verifikation

| Prüfung | Ergebnis |
|---------|----------|
| Parser-Check (`Retry-FlakyTests.ps1`, `Analyze-FlakyTestResults.ps1`, `lib/Trx.ps1`, `lib/TestDiscovery.ps1`, `lib/Annotations.ps1`) | alle 5 Dateien: OK |
| `Retry-FlakyTests.ps1` mit fehlendem TRX | „Keine Flaky-Fehlschlaege: Primär-TRX … fehlt oder ist unauswertbar", Exit 0 |
| `Retry-FlakyTests.ps1` mit synthetischem TRX (1 Flaky-Fail + 1 Nicht-Flaky-Fail + 1 Pass) | Schnittmenge korrekt: nur der `Flaky`-Test wurde wiederholt; echter Re-Run via `dotnet test --filter "(Category=Flaky)&(FullyQualifiedName~…)"` → Test bestanden → `[notice] Flaky-Retry recovered`, `synthetic-primary-flaky-retry.trx` erzeugt, Exit 0 |
| `Analyze-FlakyTestResults.ps1` gegen synthetisches Verzeichnis | Flake-Ereignis erkannt (Primär-Fail + Retry-Pass → „beobachten"), Report + Exit 0 |
| `Analyze-FlakyTestResults.ps1` gegen `VideoWebPlayer.Tests/TestResults` | 5 Primär-Läufe, 2976 Tests ausgewertet: 0 Quarantäne-Kandidaten, 2 „beobachten" (der OS-Lane-Flake + der Flaky-markierte Test mit 1 Flake-Ereignis), 2974 stabil; Re-Entry-Auswertung: 1 Flaky-Test, 0 Kandidaten; Exit 0 |

## Testabdeckung

**Abdeckung:** 94,44 % Zeilenabdeckung (reguläre Lane, `XPlat Code Coverage`/coverlet; identisch zur Messung aus der Implementierung, 94,44 %) — deutlich über dem 70-%-Gate. Cobertura-Bericht: `VideoWebPlayer.Tests/TestResults/coverage-local/*/coverage.cobertura.xml`.

116 Dateien zeigen in dieser Messung 0 % — das ist erwartbar und kein Befund dieses Auftrags: Es sind überwiegend Blazor-`.razor`-Seiten, Identity-Scaffolding und `VideoWebPlayer.Client`-DTOs/Clients, deren Abdeckung durch die E2E-/ClientLibrary-Tests der **OS-Lane** entsteht, die per Design nicht in die Gate-Messung einfließt (unverändert zur Gate-Definition vor dem Umbau).

## Fehlende Tests

Keine neuen ungetesteten Quelldateien durch diesen Auftrag: Es wurde kein Anwendungscode geändert (Scope-Ausschluss). Neue Testinfrastruktur ist selbst getestet: `FlakyConventionTests` (2 Tests) und `TempDirectoryCleanupTests` (7 Tests) — beide in der blockierenden regulären Lane grün nachgewiesen. Die PowerShell-Skripte haben kein Testprojekt (verifiziert über dokumentierte Trockenläufe, s. Tabelle oben).
