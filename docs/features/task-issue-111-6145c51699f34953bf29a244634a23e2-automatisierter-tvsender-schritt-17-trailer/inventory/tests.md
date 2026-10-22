# Bestandsaufnahme: Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-24, Nachmittag (UTC+2, lokale Systemzeit)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-17-trailer`, `d0f215023dda62b8e72df6534ac046241b87fc39`
- Uncommittete Änderungen im getesteten Stand: nur Lifecycle-Artefakte unter `docs/features/…-schritt-17-trailer/` (kein Produktcode)
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.402, Runtime 10.0.13, Windows 11, xUnit.net VSTest Adapter 3.1.5
- Ermittelte Testsuiten und Quellen der Testbefehle: `VideoWebPlayer.Tests` (xUnit, `dotnet test`), Build vorab: `dotnet build VideoPlayer.sln` (Debug, 0 Fehler, 142 bestehende Warnungen)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --logger "console;verbosity=normal"` | Repo-Stamm | 0 | 2098 | 0 | 1 | [baseline-dotnet-test.log](test-results/baseline-dotnet-test.log) |

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf ist vollständig grün (1 bekannter Alt-Skip:
`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`).

### Testlücken und Ausführungsprobleme

- 1 übersprungener Test (bekannter Alt-Skip, siehe oben).
- Keine Build-, Setup- oder Infrastrukturfehler.

## Testklassen (relevant für diese Anforderung)

### `ProgramScheduleTestDb` (Hilfsklasse, `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync` — echtes In-Memory-SQLite mit FKs.
- `CreateService` — `ProgramScheduleService` mit `ProgramRuleService` + `ProgramSettingsService` (NullLogger).
- `CreateMovieAsync`, `CreateShowAsync`, `CreateMediaItemAsync`, `CreateGenreAsync`, `LinkMovieGenreAsync`,
  `CreateRuleAsync`, `CreateSlotAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync`, `AddUserAsync`,
  `SlotsAsync`, `AssertGapless`, `SaveDiagAsync`.

### `LocalMediaSourceClassifierTests` (`VideoWebPlayer.Tests/LocalMediaSourceClassifierTests.cs`)
- `ClassifyAllAsync_ClassifiesTvShow_FromLocalSource` — legt ein echtes Verzeichnis mit NFO + Videodatei
  an und klassifiziert via `LocalMediaSourceReader`; gleiches Muster für Trailer-Konventionen nutzbar.
- `CreateContextAndServiceAsync` — baut ServiceProvider mit `MediaSourceClassifier`.

### `ProgramScheduleServiceTests_Filler` / `_Generation` / `_Conflicts` / `_Reconcile` / `_Access` / `_Diagnosis` / `_Genres`
- Füller-Logik (`FillGaps`, Prioritäten, Gapless-Abdeckung), Reconcile-Verhalten, Zugriffssteuerung,
  Diagnose-Zähler.

### `ProgramRuleServiceTests_Validation` / `_Crud` / `_GenreMaterial` / `_Authorization`
- Validierung der Materialarten (`GetAllowedMaterialTypes`, `ValidateRuleInputAsync`), Admin-Erfordernis.

### `VideoWebPlayerBackupDataTests*` (`VideoWebPlayer.Tests/Services/Backups/`)
- Alt-Backup-Regressionen via `LegacyBackupArchiveBuilder` (`RemoveTablesAsync`, `RemoveColumnsAsync`,
  `CreateSeededBackupAsync`) — Vorlage für `MovieMediaItems.IsTrailer`-Regressionstest.

### `TVSenderAdminTests` (bUnit) / `TVSenderAdminE2ETests`
- Regel-Editor (bUnit), E2E-Abdeckung der Admin-Seite.
