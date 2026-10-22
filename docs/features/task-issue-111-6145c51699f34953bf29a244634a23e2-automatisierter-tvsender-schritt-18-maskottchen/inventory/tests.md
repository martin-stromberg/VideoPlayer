# Bestandsaufnahme: Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-24 (UTC+2, lokale Systemzeit)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-18-maskottchen`, `3b8d493` (Bearbeiter-Eintrag)
- Uncommittete Änderungen im getesteten Stand: nur Lifecycle-Artefakte unter `docs/features/…-schritt-18-maskottchen/` und leere Verzeichnisse unter `docs/projects/…/maskottchen/` (kein Produktcode)
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.402, Runtime 10.0.13, Windows 11, xUnit.net VSTest Adapter 3.1.5, Playwright-Chromium (E2E)
- Ermittelte Testsuiten und Quellen der Testbefehle: `VideoWebPlayer.Tests` (xUnit + bUnit + Playwright-E2E, `dotnet test`); Debug-Build vorab `dotnet build VideoPlayer.sln` → 0 Fehler, 144 Warnungen (Bestand)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "console;verbosity=normal"` | Repo-Stamm | 1 | 2110 | 1 | 1 | [baseline-dotnet-test.log](test-results/baseline-dotnet-test.log) |
| Retry | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter "FullyQualifiedName~PlaylistSkipLockedEntriesE2ETest"` | Repo-Stamm | 0 | 1 | 0 | 0 | [baseline-retry-skiplocked.log](test-results/baseline-retry-skiplocked.log) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` | `VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs` | `TimeoutException` (30 s) auf `Locator("#playlist-detail-selected-entry")` — bekannter Baseline-Flake, im Einzel-Retry grün (siehe Retry-Lauf); bereits in den Schritten 4/11/12 als Flake dokumentiert | [baseline-dotnet-test.log](test-results/baseline-dotnet-test.log), [baseline-retry-skiplocked.log](test-results/baseline-retry-skiplocked.log) |

### Testlücken und Ausführungsprobleme

- 1 übersprungener Test (bekannter Alt-Skip `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`).
- Keine Build-, Setup- oder Infrastrukturfehler; `VideoWebPlayer.exe` lief nicht (kein bin/Debug-Lock).

## Testklassen (relevant für diese Anforderung)

### `ProgramScheduleTestDb` (Hilfsklasse, `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync`, `CreateService`, `CreateMovieAsync`, `CreateShowAsync`, `CreateMediaItemAsync`,
  `CreateGenreAsync`, `LinkMovieGenreAsync`, `CreateRuleAsync`, `CreateSlotAsync`,
  `EnsureSourceAsync`, `GrantSourceAccessAsync`, `AddUserAsync`, `SlotsAsync`, `AssertGapless`,
  `SaveDiagAsync` — echtes In-Memory-SQLite mit FKs.

### `ProgramScheduleServiceTests_Filler` / `_Generation` / `_Conflicts` / `_Reconcile` / `_Access` / `_Diagnosis` / `_Genres` / `_Trailer`
- Füller-Logik (`FillGaps`, Prioritäten, Gapless-Abdeckung), Reconcile, Zugriffsfilter,
  Diagnose-Zähler, Trailer-Vorlauf — Muster für die neue Clip-Material-Tests.

### `ProgramRuleServiceTests_Validation` / `_Crud` / `_GenreMaterial` / `_Authorization`
- `GetAllowedMaterialTypes`, `ValidateRuleInputAsync`-Existenz-/Zulässigkeitsfälle,
  Admin-Erzwingung.

### `ProgramGuideServiceTests` (`VideoWebPlayer.Tests/ProgramGuideServiceTests.cs`)
- Service-Tests gegen `ProgramScheduleTestDb`; Muster für Titel-Fallback-Test.

### `TVSenderAdminTests` (`VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`, bUnit)
- Material-Picker-Verhalten (Tabs, Editor-Card, Genre-/Quell-Checkboxen) — Muster für
  Clip-Quellen-Dropdown-Test.

### `LocalMediaSourceClassifierTests` (`VideoWebPlayer.Tests/LocalMediaSourceClassifierTests.cs`)
- Klassifizierung über echtes lokales Verzeichnis + NFO; Beleg, dass Videos ohne `.nfo`
  keinen `Movie`-Satz erzeugen (kein Marker nötig).

### `TVSenderAdminE2ETests` (`VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`, Playwright)
- `Admin_CreateEditDeactivateDeleteRule` — konsolidierter E2E über Regel-CRUD; Erweiterung
  nur bei Bedarf (E2E-Minimierung).
