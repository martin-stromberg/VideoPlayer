# Testergebnisse (Schritt 10)

## Finaler Testlauf

- Zeitpunkt: 2026-10-21 (MEZ), nach Abschluss der Implementierung
- Branch: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-3-sendeplan-erzeugen`
- Befehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (volle Suite, Vordergrund, kein `--no-build`; vorausgegangener `dotnet build VideoPlayer.sln` erfolgreich, 0 Fehler)

### Ergebnis

| Gesamt | Bestanden | Fehlgeschlagen | Übersprungen | Dauer |
|--------|-----------|----------------|--------------|-------|
| 1852 | 1851 | 0 | 1 | ~5 m 01 s |

Übersprungen: `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — der seit Schritt 1 bekannte Alt-Skip, keine Regressions.

Gegenüber der Baseline (1825 bestanden / 1 übersprungen / 0 Fehler) kamen **26 neue Tests** hinzu, alle bestanden:

- `GermanHolidayCalendarTests` (3)
- `ProgramScheduleServiceTests_Generation` (10: täglich/wöchentlich/einmalig/Feiertag, Serien-Cursor + Wrap-around, SlotDuration, lückenlos, verwaistes Material, laufende Ausstrahlung, Extend-only)
- `ProgramScheduleServiceTests_Conflicts` (2: Priorität, Gleichstand-Tiebreak)
- `ProgramScheduleServiceTests_Filler` (2: nur Füller, Füller-Fit/Restlücke)
- `ProgramScheduleServiceTests_Access` (4: Laufzeit, nicht vollständig freigegeben, für alle freigeschaltet, nur Admins)
- `ProgramScheduleWorkerTests` (1: Worker erzeugt Sendeplätze)
- `ProgramSettingsServiceScheduleTests` (2: Defaults, Persistenz + Validierung)
- `VideoWebPlayerBackupDataTests.ReadFromAsync_LegacyBackupWithoutProgramScheduleTablesAndSettings_RestoresSuccessfully` (1)
- E2E-Erweiterung in `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (zählt als bestehender Test, kein neuer)

## Zwischenläufe während der Implementierung

Zielgerichtete neue Tests (`--filter`, 26 Tests) zeigten zunächst Fehler, die alle behoben wurden:

| Iteration | Fehler | Ursache / Behebung |
|-----------|--------|--------------------|
| 1 | 19 × FK-Verletzung beim Seeding | `MediaCollection.Id`/`TVShow.Id` wurden vor dem Save referenziert → Fixture speichert Principal zuerst |
| 1 | 1 × `TimeSpan`-Overflow | Regel ohne `EpisodeCount`/`SlotDuration` nahm unbegrenzt Items → Fallback „1 Item" in `MaterializeOccurrence` |
| 2 | Duplikat-Slot beim zweiten Lauf | Lookback-Ausstrahlung persistierte historische Slots → Slots mit `End ≤ coverageStart` entfallen |
| 2 | Restlücken-Fail | 30/20-min-Füller teilen das 10-min-Restfenster am DST-Tag nicht → Testfiller auf 10 min; zudem Füller-Overshoot am Horizont-Ende erlaubt |
| 2 | Cursor-Assertions | Lookback-Ausstrahlungen konsumieren Folgen — Erwartungen dokumentiert und angepasst |
| 2 | Zeitstempel-Vergleiche | SQLite liefert `DateTimeKind.Unspecified` → `SpecifyKind`/`ConvertTimeFromUtc` in den Assertions |

Keine Flakes, keine Deaktivierungen.

## Build-Verifikation

- `dotnet build VideoPlayer.sln` (Debug): 0 Fehler, 141 Warnungen (Baseline-Stand, keine neuen Warnungen).
- `dotnet build VideoPlayer.sln -c Release`: 0 Fehler, 141 Warnungen.
