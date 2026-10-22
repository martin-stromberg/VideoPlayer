# Testergebnisse (Schritt 23 — N19–N21)

## Zusammenfassung

- Debug-Build der Solution: **durch laufende Benutzer-App gesperrt** (`VideoWebPlayer` PID
  58256 + Visual Studio sperren `bin\Debug`) → Verifikation über
  `-p:OutDir=<repo>/artifacts/schritt23-bin` (Debug) — 0 Fehler, Warnungen auf Bestandsniveau
  (~142).
- Release-Build `dotnet build VideoPlayer.sln -c Release`: **0 Fehler**, 144 Warnungen
  (Bestandsniveau, keine neuen).
- Volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj
  -p:OutDir=artifacts/schritt23-bin`: **2167 gesamt — 2165 bestanden, 1 fehlgeschlagen,
  1 übersprungen** (Gesamtzeit ~4,9 min).

## Der eine Fehlschlag

`BackupUploadE2ETests.BackupUpload_NavigatingAwayDuringUpload_AbortsWithoutRedirectBack`
(Playwright-Navigations-Timing, kein Bezug zu N19–N21). Bei Einzelwiederholung: **grün**
(`--filter` Einzellauf, 9 s). Dokumentierte Flake-Kategorie laut AGENTS.md („Einzelne
Playwright-Tests flackern gelegentlich unter Last"). Damit faktisch: 2166/2167 grün + 1
bekannter Alt-Skip.

## Neue/angepasste Tests

| Test | Ergebnis |
|------|----------|
| `Interleaving.EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays` | grün |
| `Interleaving.EnsureCoverage_GenreRule_Interleaving_EpisodeOrderPerShow` | grün |
| `Interleaving.EnsureCoverage_GenreRule_SecondRun_ContinuesEachShowCursor` | grün |
| `Interleaving.Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled` | grün |
| `Generation.EnsureCoverage_WeekdayRule_OnlyMondayToFriday` | grün |
| `Generation.EnsureCoverage_WeekendRule_OnlySaturdaySunday` | grün |
| `Validation.CreateRule_Weekday/WeekendWithoutDayOfWeekOrDate_IsAllowed` (2) | grün |
| `Backups.VideoWebPlayerBackupDataTests_StrandKey...` | grün |
| `Components.TVSenderAdminTests.RuleEditor_RendersAsOverlayDialog` (umbenannt/umgebaut) | grün |
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (Overlay-Assertion) | grün |

Baseline-Vergleich: 2158 → 2167 Tests (+9: vier Interleaving, zwei Recurrence-Generation,
zwei Recurrence-Validierung, ein Backup-StrandKey — der Editor-bUnit-Test wurde umbenannt,
nicht addiert).
