# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-21 ~13:05 UTC
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-9-genre-auswahl` @ `c84fa19` (plus uncommittete Lifecycle-Artefakte `docs/features/.../requirement.md`, `inventory.md`, `inventory/`)
- Uncommittete Änderungen im getesteten Stand: nur Doku-Artefakte dieser Anforderung; kein Produktivcode
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.x, net10.0, Windows, SQLite-InMemory-Tests + Playwright-E2E
- Ermittelte Testsuiten und Quellen der Testbefehle: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (einzige Suite; Definition of Done laut AGENTS.md §7)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build VideoPlayer.sln -c Debug` | Repo-Root | 0 | — | — | — | 0 Fehler, 142 Alt-Warnungen |
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger trx` | Repo-Root | 0 | 1975 | 0 | 1 | [baseline-schritt9.trx](test-results/baseline-schritt9.trx) |

### Nachgewiesene bestehende Testfehler

Keine — 0 Fehlschläge im Ausgangslauf.

### Testlücken und Ausführungsprobleme

- 1 bekannter Alt-Skip: `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` (bereits in den Vorgänger-Schritten dokumentiert).
- Keine weiteren Einschränkungen.

## Testklassen (für diese Anforderung relevant)

### `ProgramScheduleServiceTests_Generation`
- Genre-Expansion über `MovieGenre`/`TVShowGenre`; Sortierung; Dauer-/Freigabefilter.

### `ProgramScheduleServiceTests_Access`
- Freigabe-Filter (`AccessContext`): Quellfreigabe vs. Einzel-Unlock.

### `ProgramScheduleServiceTests_Reconcile`
- Cursor-Rewind, Verdrängung — relevant für Cursor-Semantik über der Genre-Union.

### `ProgramScheduleServiceTests_Filler` / `_Conflicts`
- Füller-Lückenlogik, Prioritäten — Genre-Regeln kommen auch als Füller vor.

### `ProgramRuleServiceTests_Validation` / `_Crud` / `_Channel` / `_Authorization`
- Validierung (Materialart ↔ Regelart, Existenz `MaterialId`), CRUD, Admin-Erzwingung — werden durch `ProgramRuleInput`-Erweiterung und neue Join-Tabellen betroffen.

### `TVSenderAdminE2ETests`
- Playwright: Regel-CRUD inkl. Genre-Auswahl über `rule-material-genre` — wird zur Mehrfachauswahl angepasst/erweitert.

### `VideoWebPlayerBackupDataTests_DevicePairing` (Muster) + `LegacyBackupArchiveBuilder`
- Alt-Backup-Regressionsmuster: Archiv ohne neue Tabelle restaurierbar (`RemoveTablesAsync`).

### `MediaMetadataEditorServiceTests` (Services/)
- Tests zu `GetGenreOptionsAsync` — relevant falls sich der Options-Endpunkt ändert (bei eigener Optionsquelle: unverändert).

## Hilfsmethoden

### `ProgramScheduleTestDb` (VideoWebPlayer.Tests/Helpers/)
- `CreateAsync` (SQLite shared-memory, `EnsureCreated`, FKs), `CreateService`, `CreateGuideService`, `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync`, `CreateMediaItemAsync`, `CreateMovieAsync`, `CreateShowAsync`, `CreateRuleAsync`, `CreateSlotAsync`, `GetChannelAsync`, `SlotsAsync`, `AssertGapless`, `SaveDiagAsync`.

### `LegacyBackupArchiveBuilder` (VideoWebPlayer.Tests/Services/Backups/)
- `CreateSeededBackupAsync`, `RemoveTablesAsync`, `ReadTableColumnsAsync` — Alt-Backup-Simulation.
