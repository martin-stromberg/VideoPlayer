# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-26, Läufe gestartet ca. 06:32–06:40 +0100 (Mitteleuropäische Zeit, UTC+1)
- Branch und Commit-ID: `task/issue-284-347c7c10893b42569ac97a9687503bce-medienquellen-merkmal-im-menue`, Commit `6a89228edf77a756563abbc7c211b8abe634533f` (2026-10-26 04:12:34 +0100)
- Uncommittete Änderungen im getesteten Stand: nur untracked `docs/features/task-issue-284-347c7c10893b42569ac97a9687503bce-medienquellen-merkmal-im-menue/` (requirement.md, todo.md, inventory/) — kein Produktivcode, keine Tests geändert.
- Testumgebung und Runtime-/SDK-Versionen: Windows (DESKTOP-CM8OBSG), .NET SDK 10.0.402, Testziel `net10.0`, xunit.v3 3.2.2, Microsoft.Playwright 1.62.0 (Chromium lokal installiert), Release-Build (`dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release`, 0 Fehler, 142 Warnungen).
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - AGENTS.md: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (volle Suite).
  - CI `.github/workflows/staging-ci.yml` bzw. `pr-staging-ci.yml`: Release-Build des Testprojekts, dann drei Läufe `--filter "Category!=E2E"`, `--filter "Category=Integration"`, `--filter "Category=E2E"` (jeweils `--no-build -c Release`). **Hinweis:** Im Code existiert kein `Trait("Category", "Integration")` — ein Integration-Lauf würde 0 Tests ausführen und wurde deshalb nicht separat gefahren; die Integration-Tests laufen unmarkiert im `Category!=E2E`-Lauf mit.
  - Die beiden ausgeführten Läufe decken zusammen die komplette Suite ab (`!=E2E` ∪ `E2E` = alle Tests).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 (Nicht-E2E) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=test-results-non-e2e.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 2192 | 0 | 1 | `test-results/test-results-non-e2e.trx`, `test-results/console-non-e2e.txt` (Gesamtzeit ~1,84 min) |
| 2 (E2E) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E" --logger "trx;LogFileName=test-results-e2e.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 230 | 0 | 1 | `test-results/test-results-e2e.trx`, `test-results/console-e2e.txt` (Gesamtzeit ~4,65 min) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| — | — | — | — |

Im Ausgangslauf ist **kein Test fehlgeschlagen**. Es gibt demnach keinen dokumentierten „preexisting failure"; jeder spätere Fehlschlag muss als neu untersucht werden (Playwright-Tests können laut AGENTS.md unter Last flackern — in diesen Läufen trat kein Flackern auf).

### Testlücken und Ausführungsprobleme

| Test / Umfang | Grund | Nachweis |
|---------------|-------|----------|
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | Plattformbedingt übersprungen: „Unix-Dateirechte lassen sich nur unter Unix prüfen." (Lauf auf Windows) | Lauf 1, `test-results-non-e2e.trx` (outcome `NotExecuted`) |
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | `[Fact(Skip = ...)]` — dokumentierter bekannter Defekt: `api/continue-watching` antwortet mit 500, wenn ein Film keiner Filmsammlung gehört (`ContinueWatchingService.Create` ohne Null-Prüfung). Bewusst deaktiviert, unabhängig von dieser Anforderung. | Lauf 2, `test-results-e2e.trx`; Quelle `VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs` Zeile 76 |
| `Category=Integration`-Suite | In der CI als eigener Lauf vorgesehen, im Code existiert jedoch kein entsprechendes `Trait` — eine Ausführung würde 0 Tests umfassen. | `grep -r 'Trait("Category", "Integration")'` → keine Treffer |

Keine Build-, Setup- oder Infrastrukturfehler aufgetreten. Keine Tests abgebrochen.

## Testklassen (bereichsrelevant)

### `SourceVisibilityControllerTests`
Datei: `VideoWebPlayer.Tests/Controllers/SourceVisibilityControllerTests.cs`

- `GetSources_Includes_Sources_With_Unlocked_Items` — `SourcesController.GetSources` liefert Quellen über `MediaSourceUsers` **und** Einzelfreischaltungen (`UnlockedMediaService`); nicht freigegebene Quellen fehlen. Direkter Ansatzpunkt für DTO-/Listenverhalten.
- `Get_Items_For_Unlocked_Source_Lists_Only_Unlocked_Entries` — `ItemsController.Get` listet nur freigeschaltete Einträge einer Quelle.
- Private Hilfsklasse `FakeAuthService` (Zeile 138) — eigene Kopie; eine gemeinsame liegt zusätzlich unter `Helpers/FakeAuthService.cs`.

### `ApplicationDbContextTests`
Datei: `VideoWebPlayer.Tests/ApplicationDbContextTests.cs` (echtes SQLite, In-Memory)

- `Update_CopiesSourceType` (Zeile 598) — `MediaSourceExtensions.Update` kopiert `SourceType`; Muster für einen `HiddenInMenu`-Kopierte-Test.
- `MediaSource_DefaultsToSftpSourceType` (Zeile 617) — Default-Wert der Quelle.
- `DeleteMediaSourceAsync_*` (9 Tests, Zeilen 14–594) — Löschverhalten inkl. Event-Publikation.
- Hilfsmethoden `SeedSourceOnlyAsync` (Zeile 365) / `SeedFullSourceAsync` (Zeile 176) — bauen SQLite-In-Memory-Kontext mit Quelle (+Collections/Items/User) inkl. `EventManager`.

### `VideoWebPlayerBackupDataTests_TvSenderEnabled`
Datei: `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_TvSenderEnabled.cs`

- `ReadFromAsync_LegacyBackupWithoutTvSenderEnabledColumn_RestoresDefaultFalse` — **direktes Vorbild** für den `HiddenInMenu`-Regressionstest: Alt-Backup ohne Spalte via `LegacyBackupArchiveBuilder.RemoveColumnsAsync`, Seed bewusst mit `true`, Restore muss `false` liefern.

Geschwisterdateien mit gleichem Muster: `VideoWebPlayerBackupDataTests_ClipGroups.cs`, `_AgeRatings.cs`, `_Trailer.cs`, `_ProgramRuleLinks.cs`, `_StrandKey.cs`, `_DevicePairing.cs`, `_TvSenderUserAccess.cs`.

### `NavMenuLiveTvTests` (bUnit)
Datei: `VideoWebPlayer.Tests/Components/NavMenuLiveTvTests.cs`

- `NavMenu_ContainsExactlyOneLiveTvEntry_ForAuthorizedUser` — `NavMenu` rendert `/live-tv`-Link.
- `NavMenu_HidesLiveTvEntry_WhenFeatureNotReleased` / `_WhenUserNotReleased` — Flag-/Freigabe-gesteuerte Menüausblendung (Muster für `HiddenInMenu`-Filtertests).
- `NavMenu_ShowsLiveTvEntry_ForAdminWithoutAccessRow` — Admin-Sonderfall.
- Aufbau: `BunitContext` + `AddAuthorization().SetAuthorized(...)` + InMemory-`ApplicationDbContext` + `ProgramSettingsService` + `NoOpVideoWebPlayerClient` + `Mock<ILoginIpBlockService>`.

### `NavMenuPlaylistsTests` (bUnit)
Datei: `VideoWebPlayer.Tests/Components/NavMenuPlaylistsTests.cs`

- `NavMenu_ContainsExactlyOnePlaylistEntry_AndNoSeparatePublicPlaylistsEntry` — gleicher bUnit-Aufbau; prüft `a.nav-link`-Markup.

### `MediaSourceAdminDetailsValidationTests`
Datei: `VideoWebPlayer.Tests/MediaSourceAdminDetailsValidationTests.cs`

- 5 Tests um `MediaSourceAdminDetails.TryValidateLocalDirectory` (internal static, direkt ohne Rendering aufrufbar) — kein Formular-Rendering-Test für die Detailseite vorhanden.

### `ApiDocumentationContractTests` / `ApiDocumentationContractTests_Runtime`
Dateien: `VideoWebPlayer.Tests/ApiDocumentationContractTests.cs`, `..._Runtime.cs`

- Prüfen, dass `docs/API.md` die erwarteten Routen (`GET /api/Sources` u. a.) als `### <Verb> <Pfad>`-Überschriften enthält. Ein DTO-Feldvergleich findet **nicht** statt — das `DtoMediaSource`-Beispiel in `docs/API.md` (Zeilen 268–279) wird nicht automatisch vertraglich geprüft.

### `EnumContractTests`
Datei: `VideoWebPlayer.Tests/EnumContractTests.cs`

- Pinnt Enum-Member von Client-/Server-Verträgen (`RecentEntryType`, `ManualBackupJobStatus`); `MediaSourceType`/`ProgramRuleMaterialType` werden nicht gepinnt.

### E2E-Tests (Playwright, `Trait("Category", "E2E")`)

- `MediaSourceSwitchE2ETests.User_Can_Switch_Source_From_Menu_And_Sees_Only_Selected_Source_Titles` — Quellenliste im `NavMenu` + Quellenwechsel (direkt betroffen).
- `UnlockedSourceE2ETests.Regular_User_Can_Open_Unlock_Source_And_Only_Unlocked_Entries_Are_Shown` — Sichtbarkeit über Einzelfreischaltung.
- `MediaSourceLocalDirectoryE2ETests` (4 Tests) — Admin-Detailseite (`/admin/mediasources/...`) mit LocalDirectory-Quelle.
- `MediaSourceDeleteE2ETests.Admin_Can_Delete_MediaSource_And_It_Disappears`.
- `TVSenderAdminE2ETests`, `TVSenderAdminClipGroupsE2ETests` — Clip-Quellen-/Clip-Gruppen-Verwaltung.

## Hilfsmethoden

### `LegacyBackupArchiveBuilder`
Datei: `VideoWebPlayer.Tests/Services/Backups/LegacyBackupArchiveBuilder.cs` (internal static)

- `CreateSeededBackupAsync(SqliteConnection, CancellationToken)` — aktuelles Schema + Admin-User + Setup-Row + `VideoWebPlayerBackupData`-Instanz.
- `RemoveTablesAsync(backup, tableNames, ct)` — baut Alt-Backup ohne gegebene Tabellen.
- `RemoveColumnsAsync(backup|stream, tableName, columnNames, ct)` — baut Alt-Backup ohne gegebene Spalten (Index + Zeilen).
- `AppendRowsAsync(...)` — hängt Zeilen an eine Tabellen-Payload an.
- `ReadTableColumnsAsync(stream, tableName, ct)` — liest die Spaltenliste aus `index.json` (Verifikation, dass die Spalte wirklich fehlt).

### `NoOpVideoWebPlayerClient`
Datei: `VideoWebPlayer.Tests/Helpers/NoOpVideoWebPlayerClient.cs`

- Überschreibt `HttpGetAsync<T>` und liefert typabhängig leere Resultate — für bUnit-Tests, die `VideoWebPlayerClient` injizieren, aber keine HTTP-Aufrufe brauchen. Liefert aktuell leeres `List<MediaEntryDto>` für unbekannte `T` — bei einem NavMenu-Filtertest mit Quellen müsste ein eigener Client-Double mit `DtoMediaSource[]`-Rückgabe her (oder `HttpGetAsync` im Test überschreiben).

### `TestWebHostEnvironment`
Datei: `VideoWebPlayer.Tests/Helpers/TestWebHostEnvironment.cs`

- Minimal-`IWebHostEnvironment` für `VideoWebPlayerBackupData`-Tests.

### `FakeAuthService` (Helper)
Datei: `VideoWebPlayer.Tests/Helpers/FakeAuthService.cs`

- `IAuthService`-Double mit setzbarem `CurrentUser`.

### `TestHelpers`
Datei: `VideoWebPlayer.Tests/Helpers/TestHelpers.cs`

- `DenyReadAccess`/`ResetAccessControl` u. a. für Dateisystem-Tests.

### `ProgramScheduleTestDb`, `TVSenderAdminE2ETestBase`, `TestableMediaSourceScanService`, `FakeSftpMediaSourceReader` u. a.
Dateien: `VideoWebPlayer.Tests/Helpers/`

- DB-/Service-Fixtures für TV-Sender- und Scanner-Tests (Kontext Clip-Quellen, Verifikationspunkte).
