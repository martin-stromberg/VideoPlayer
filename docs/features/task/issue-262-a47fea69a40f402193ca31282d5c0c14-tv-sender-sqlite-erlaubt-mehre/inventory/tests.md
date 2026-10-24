# Bestandsaufnahme: Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-24, Lauf ca. 08:58–09:02 MESZ (UTC+0200); Wiederholungsläufe unmittelbar danach
- Branch und Commit-ID: `task/issue-262-a47fea69a40f402193ca31282d5c0c14-tv-sender-sqlite-erlaubt-mehre` @ `975dc7a2a1aeff228782dc3b83ab8a81a74c2b62` („TV-Sender: ProgramSlot-Zeitwerte als UTC materialisieren (Issue #260) (#272)")
- Uncommittete Änderungen im getesteten Stand: nur das untracked Feature-Verzeichnis `docs/features/task/issue-262-a47fea69a40f402193ca31282d5c0c14-tv-sender-sqlite-erlaubt-mehre/` (`requirement.md`, `todo.md`, ab Laufzeitpunkt zusätzlich die hier erzeugten `inventory/`-Artefakte). Keine Änderungen an Produktiv- oder Testcode.
- Testumgebung und Runtime-/SDK-Versionen: Windows, Git Bash; .NET SDK 10.0.402, .NET Runtime 10.0.13; Ziel `net10.0`, Debug-Konfiguration (Default von `dotnet test`); xunit.v3 3.2.2, `xunit.runner.visualstudio` 3.1.5; Microsoft.Playwright 1.62.0 mit installierten Browsern (`~/AppData/Local/ms-playwright`: chromium-1248 u. a.).
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - `AGENTS.md` (Definition of Done): volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`.
  - `.github/workflows/pr-staging-ci.yml`: Release-Build des Testprojekts, dann drei getrennte Läufe `--filter "Category!=E2E"`, `--filter "Category=Integration"`, `--filter "Category=E2E"` (71 `[Trait("Category", "E2E")]`-Tests; `Category=Integration` hat kein einziges Vorkommen im Testcode — die Filterlauf-Gruppe läuft in der CI leer bzw. deckt nur nicht-E2E-abhängige Fälle ab).
  - Hier ausgeführt: die volle Suite in einem Lauf (Debug), was alle CI-Filtergruppen abdeckt.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 0 (Abbruch, kein Test ausgeführt) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "trx;LogFileName=baseline-all.trx;LogFilePrefix=baseline" --logger "console;verbosity=normal" --results-directory <test-results>` | Repo-Root | 1 | 0 (kein Test ausgeführt) | – | – | Konsolenlog durch `>`-Umleitung von Lauf 1 überschrieben — nicht erhalten; dokumentierter Fehler: Logger-Init schlug fehl („Die Parameter „LogFileName" und „LogFilePrefix" können nicht zusammen verwendet werden"), der Debug-Build selbst war erfolgreich |
| 1 (Baseline, volle Suite) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --logger "trx;LogFileName=baseline-all.trx" --logger "console;verbosity=normal" --results-directory <test-results>` — `--no-build` nutzt das in Lauf 0 erzeugte Debug-Build | Repo-Root | 1 | 2225 | 1 | 1 | [TRX](test-results/baseline-all.trx), [Log](test-results/baseline-all-console.log) |
| 2 (Wiederholung Fehlschlag) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --filter "FullyQualifiedName=VideoWebPlayer.Tests.PlaylistPlaybackE2ETests.PlaylistSkipCollectionEntriesE2ETest"` | Repo-Root | 0 | 1 | 0 | 0 | [TRX](test-results/retry1-playlist-skip-collection.trx) |
| 3 (Wiederholung Fehlschlag) | wie Lauf 2, `LogFileName=retry2-playlist-skip-collection.trx` | Repo-Root | 0 | 1 | 0 | 0 | [TRX](test-results/retry2-playlist-skip-collection.trx) |

Lauf 1 dauerte 5,06 Minuten; Gesamtzahl 2227 Tests.

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `VideoWebPlayer.Tests.PlaylistPlaybackE2ETests.PlaylistSkipCollectionEntriesE2ETest` | `VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs:259` | `System.TimeoutException: Timeout 30000ms exceeded` — Playwright wartet auf `Locator("#playlist-detail-selected-entry")` in `Helpers/PlaylistsE2ETestBase.SelectEntryAsync` (Z. 285) | Lauf 1, [TRX](test-results/baseline-all.trx); **in Lauf 2 und 3 einzeln bestanden** → flackernder E2E-Test unter Last (in `AGENTS.md` als bekanntes Phänomen genannt), nicht reproduzierbar |

Der einzige Fehlschlag betrifft einen Playwright-E2E-Test im Playlist-Bereich — ohne Bezug zu
`ProgramRuleCursors`. Beide Einzel-Wiederholungen (Läufe 2, 3) waren erfolgreich; der Fehlschlag
gilt als Flake, bleibt aber als dokumentierter Ausgangsbefund bestehen.

### Testlücken und Ausführungsprobleme

- **Übersprungen:** `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — `[Fact(Skip = "…")]`, `DevicePlaylistE2ETests_Playback.cs:76`. Dokumentierter, bekannter Produktivdefekt: `GET api/continue-watching` antwortet mit HTTP 500, wenn ein Film keiner Filmsammlung angehört (`ContinueWatchingService.GetListAsync` → `Create<DtoMovieCollection>(null)` → `NullReferenceException`, `VideoWebPlayer/Services/ContinueWatchingService.cs:122/78`). Laut Skip-Begründung bewusst nicht in jenem Auftrag behoben; nicht Teil dieser Anforderung.
- **Infrastrukturfehler:** Lauf 0 endete ohne Testausführung am TRX-Logger (Parameterkombination `LogFileName`+`LogFilePrefix` unzulässig) — korrigiert in Lauf 1; keine Auswirkung auf den getesteten Stand.
- **`Category=Integration`:** In der CI als eigener Lauf vorgesehen, im Testcode existiert jedoch kein einziger `[Trait("Category", "Integration")]` — diese CI-Gruppe enthält faktisch keine Tests.
- **Migrations-Testbarkeit:** Kein Test im Projekt ruft `Migrate()`/`MigrateAsync()` auf; alle Test-Datenbanken entstehen per `EnsureCreatedAsync` (Schema aus dem aktuellen EF-Modell, nicht aus der Migrationshistorie). Eine künftige Datenmigration (Duplikat-Bereinigung) ist damit mit dem vorhandenen Testmuster nicht direkt ausführbar — Index-Erzwingung und Lesepfade dagegen schon.
- **Kein Test zur Unique-Index-Erzwingung auf `ProgramRuleCursors`:** Es existiert kein Test, der zwei Zeilen mit derselben `ProgramRuleId` und `StrandKey = NULL` einfügt oder die Index-Erzwingung prüft. `SingleAsync`-Assertions (s. unten) würden Duplikate zwar sichtbar machen, erzeugen aber keine.
- **Kein Test für deterministische `FirstOrDefault`-Wahl bei Cursor-Duplikaten.**

## Testklassen

### `ProgramScheduleServiceTests_Interleaving`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Interleaving.cs` — echte SQLite-DB via
`ProgramScheduleTestDb`; direkt relevant für Strang-Cursor.

- `EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays` — Interleaving: stabile Slot-Positionen je Serie über Tage, keine Überlappungen
- `EnsureCoverage_GenreRule_Interleaving_EpisodeOrderPerShow` — Episoden je Serie in Reihenfolge; assertiert genau 2 `ProgramRuleCursors`-Zeilen, alle `StrandKey` beginnt mit `show:` (Z. 96–98)
- `EnsureCoverage_GenreRule_SecondRun_ContinuesEachShowCursor` — zweiter Lauf setzt je Strang am persistierten Cursor fort, kein Neustart
- `StrandOrderKey_IsDeterministic_AndNotAlphabetic` — reproduzierbarer Hash-Mix `ProgramScheduleService.StrandOrderKey`
- `EnsureCoverage_GenreRule_LeadStrand_FollowsHashOrder_NotAlphabetical` — Führungsserie folgt der Hash-Permutation
- `Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled` — Reconcile ohne Änderungen verdrängt nichts

### `ProgramScheduleServiceTests_Generation`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Generation.cs` — u. a.:

- `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder` — assertiert `ProgramRuleCursors.SingleAsync` (Z. 177): genau eine Cursor-Zeile, `LastContentType = TVShowEpisode`; zweiter Lauf setzt fort
- `EnsureCoverage_SeriesExhausted_WrapsAround`, `EnsureCoverage_SecondRun_ExtendsNotDuplicates` — Ring-Wrap bzw. Extend-only-Verhalten (beide berühren den Cursor-Pfad)

### `ProgramScheduleServiceTests_Reconcile`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Reconcile.cs` — 13 Tests zu Verdrängung/
Re-Materialisierung; decken indirekt `RewindCursor` ab, u. a. `Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip`,
`Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip`,
`Reconcile_PartiallyAiredOccurrence_KeepsTailScheduled`. Kein Test erzeugt Cursor-Duplikate.

### `ProgramScheduleServiceTests_Boundary`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Boundary.cs` — u. a.:

- `ResetSchedule_RemovesAllSlotsAndCursors` — `ResetScheduleAsync` leert `ProgramRuleCursors` komplett (Z. 337–344)

### `ProgramScheduleServiceTests_ClipSource`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipSource.cs` — u. a.:

- `EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns` — `SingleAsync` auf die Cursor-Zeile einer Clip-Quellen-Regel (`StrandKey = NULL`-Fall), Fortsetzung am Ring über Läufe (Z. 135–160)

### `ProgramScheduleServiceTests_Conflicts`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Conflicts.cs` — u. a.:

- `EnsureCoverage_HigherPriorityWins_Overlap` — verlierende Regel schreibt **keine** Cursor-Zeile (Z. 43)

### `ProgramScheduleServiceTests_Access`, `_Diagnosis`, `_Filler`, `_Genres`, `_Trailer`, `ProgramScheduleWorkerTests`, `ProgramScheduleSignalTests`

Weitere Schedule-Suiten (Zugriffsfreigaben, Regel-Diagnose, Füller, Genre-Auflösung, Trailer,
Worker-Trigger, `ProgramScheduleSignal`) — laufen alle über `ProgramScheduleTestDb` und damit
über die Cursor-Pfade, ohne sie gezielt zu prüfen.

### `VideoWebPlayerBackupDataTests_StrandKey`

Datei: `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_StrandKey.cs`

- `ReadFromAsync_LegacyBackupWithoutStrandKeyColumn_RestoresSuccessfully` — Alt-Backup ohne `StrandKey`-Spalte (per `LegacyBackupArchiveBuilder.RemoveColumnsAsync` simuliert) stellt die Cursor-Zeile mit `StrandKey = NULL` wieder her (Z. 60–62: `SingleAsync`, `Assert.Null(cursor.StrandKey)`)

### `VideoWebPlayerBackupDataTests`

Datei: `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs` (27 Fakten) —
Alt-Backup-Kompatibilität je Tabelle/Spalte; für diese Anforderung relevant:

- `ReadFromAsync_LegacyBackupWithoutProgramScheduleTablesAndSettings_RestoresSuccessfully` — Backup ohne `ProgramSlots`-/`ProgramRuleCursors`-Tabellen restore-fähig (`OptionalRestoreTables`)
- `ReadFromAsync_LegacyBackupWithoutProgramSlotStatus_RestoresWithScheduled` — Muster für Spalten-Defaults

### `ApplicationDbContextTests`

Datei: `VideoWebPlayer.Tests/ApplicationDbContextTests.cs` — echte SQLite-In-Memory-DB
(`SqliteConnection` + `EnsureCreatedAsync`, `DeleteTestContext` Z. 160–174); prüft
Cascade-Löschverhalten von `MediaSource`, nicht `ProgramRuleCursors`.

## Hilfsmethoden

### `ProgramScheduleTestDb`

Datei: `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` — **echtes SQLite**
(`Data Source=file:{dbName}?mode=memory&cache=shared`), Schema per `EnsureCreatedAsync`
(aus dem EF-Modell — Unique-Indizes werden also von SQLite durchgesetzt, nicht von EF InMemory).

- `CreateAsync(dbName, ct)` — Verbindung + `ApplicationDbContext` + `EnsureCreatedAsync`, seedet `Setup { TvSenderEnabled = true }`
- `CreateService(scheduleOptions)` / `CreateGuideService()` — `ProgramScheduleService`/`ProgramGuideService` über derselben DB
- `CreateSlotAsync` — Sendeplatz direkt (ohne Generierung), u. a. mit `ProgramSlotStatus`
- `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync` — Benutzer/Quellen/Freigaben
- `CreateMediaItemAsync`, `SeedProbeItemAsync`, `CreateMovieAsync`, `CreateShowAsync`, `CreateGenreAsync`, `LinkMovieGenreAsync`, `LinkShowGenreAsync` — Material-Seeds mit probter Laufzeit
- `CreateRuleAsync` — `ProgramRule` direkt in der DB (Service-Validierung umgangen), inkl. Genre-/Source-Joins
- `GetChannelAsync`, `SlotsAsync`, `AssertGapless`, `SaveDiagAsync` (FK-Diagnose im Fehlertext)

### `LegacyBackupArchiveBuilder`

Datei: `VideoWebPlayer.Tests/Services/Backups/LegacyBackupArchiveBuilder.cs`

- `CreateSeededBackupAsync(connection, ct)` — `EnsureCreatedAsync`-DB mit Admin-User + `Setup`, liefert `(Db, VideoWebPlayerBackupData, UserId)`
- `RemoveTablesAsync(backup, tableNames, ct)` — aktuelles Backup-Archiv ohne die genannten Tabellen (Index-Eintrag + Payload entfernt)
- `RemoveColumnsAsync(backup, tableName, columnNames, ct)` — Archiv ohne die genannten Spalten (Index-Liste und alle Zeilen)
- `ReadTableColumnsAsync(archiveStream, tableName, ct)` — liest die Spaltenliste aus `index.json`
