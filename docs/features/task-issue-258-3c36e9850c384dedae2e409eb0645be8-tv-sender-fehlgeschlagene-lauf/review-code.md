# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### MediaDurationProbeWorkerTests.cs (MediaDurationProbeWorkerTests)

- **Testqualität** — Der Kommentar in `ExecuteAsync_FeatureNotReleased_LeavesItemsUnprobed` (Zeile 139) behauptet: „Laenger als ein Worker-Intervall-Tick warten: auch ein zweiter Zyklus darf nichts messen." Das stimmt nicht: Der Worker ist mit `IntervalSeconds = 60` konfiguriert (Zeile 41), die Wartezeit beträgt nur 3 Sekunden (Zeile 140) und `initialDelay: TimeSpan.Zero` lässt genau einen Durchlauf bei t≈0 zu — ein zweiter Zyklus läuft im Testfenster nie. Der Test prüft also nicht, was der Kommentar behauptet: dass das Feature-Flag je Iteration erneut ausgewertet wird (`MediaDurationProbeWorker.cs` Zeile 65 liest `GetTvSenderEnabledAsync` tatsächlich pro Durchlauf). Eine Regression, die das Flag nur einmal beim Start läse, würde der Test nicht erkennen.
  Empfehlung: Entweder `IntervalSeconds` in den Test-Options auf ~1 Sekunde setzen, damit innerhalb der 3-Sekunden-Wartezeit mindestens ein zweiter Zyklus mit Flag-Auswertung durchläuft, oder den Kommentar auf die tatsächliche Abdeckung (ein initialer Durchlauf mit ausgeschaltetem Flag) korrigieren.

## Geprüfte Dateien

- `VideoWebPlayer/Services/MediaDurationProbeProgress.cs`
- `VideoWebPlayer/Services/MediaDurationProbeService.cs`
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer.Tests/Helpers/MediaProbeTestDoubles.cs`
- `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`
- `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Progress.cs`
- `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Reset.cs`
- `VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`

## Hinweise

- Die Befunde aus Runde 2 sind behoben und verifiziert: exakte Zellen-Assertion statt Substring-Match in `TVSenderAdminE2ETests.cs` (Zeilen 166, 174) und `TVSenderAdminTests.cs` (Zeile 145); `<c>openFileStream</c>` statt `<paramref>` in `MediaProbeTestDoubles.cs` (Zeile 35).
- Serverseitige Berechtigungserzwingung (AGENTS.md §7) ist gegeben: `RequireAdminAsync` wird in `GetProgressAsync`, `ResetFailedProbeAsync` und `ResetFailedProbesAsync` vor jeder Datenbankoperation aufgerufen; die UI-Buttons sind zusätzlich hinter dem `isAdmin`-Gate der Seite. Abdeckung durch `*_NonAdmin_Throws`-Tests in beiden Reset- und der Progress-Testdatei.
- Die geteilte `FailedProbePredicate`-Definition wird von Zähler, Detail-Liste und beiden Reset-Methoden konsistent verwendet — inklusive `VideoExtensionPredicate`, sodass Nicht-Video-Items weder gezählt noch zurückgesetzt werden (abgesichert durch `ResetFailedProbeAsync_NonVideoItem_ReturnsFalseAndKeepsState`).
- Keine neuen NuGet-Hauptversionen, keine neuen Tabellen/Spalten — `ExecuteUpdateAsync` auf den vorhandenen Spalten `DurationProbedAt`/`DurationProbeAttempts`; DB-Tests laufen gegen echtes SQLite.
