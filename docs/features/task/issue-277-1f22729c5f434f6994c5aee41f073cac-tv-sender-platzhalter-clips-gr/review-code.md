# Code-Review

Basisbranch: `staging` (`git merge-base HEAD staging` = `3bbbd490`). Dritte Review-Runde — Runde 1 (`review-code.1.md`, 14 Befunde) und Runde 2 (`review-code.2.md`, 4 Befunde) nur als Kontext. Geprüft wurden alle im Branch geänderten und neuen Quelldateien inklusive der uncommitteten/untracked Dateien; Dokumentation (docs/, README.md, RELEASE_NOTES) wurde ausgenommen. `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` läuft fehlerfrei (0 Warnungen, 0 Fehler). Alle Befunde der Vor-Runden sind im aktuellen Stand adressiert (Except-Validierung, Grace-Ablehnung ohne SlotDuration, `FillAiringSegments` mit 5 Parametern, `TVSenderAdminE2ETestBase`, idempotenter Grant, Grace-Default aus `ProgramRule.DefaultPlaceholderGrace`, Assert-Ergänzungen, Discards, ausgelagerte Razor-Komponente).

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### VideoWebPlayer/Services/ProgramScheduleService.cs (ProgramScheduleService)

- **Doppelter Code / fehlende Kapselung** — Die Karenzzeit-Ableitung `state.ClipGroups.Count > 0 && rule.SlotDuration is not null ? rule.PlaceholderGrace : (TimeSpan?)null` steht identisch an drei Stellen: `FlatAiringTailReproducible` (Zeilen 598–600), `InterleavedAiringTailReproducible` (Zeilen 675–677) und `SelectItems` (Zeilen 1453–1455). Sie kodiert dieselbe Fachregel („Karenzzeit wirkt nur bei zugeordneten Clip-Gruppen und gesetzter Sendeplatz-Länge") an drei Stellen — eine Änderung der Bedingung müsste konsistent an allen drei Stellen gepflegt werden.

  Empfehlung: Die Ableitung an einer Stelle kapseln, z. B. als Property `RuleState.ClipGrace` (berechnet aus `ClipGroups`, `Rule.SlotDuration`, `Rule.PlaceholderGrace`) oder als kleine private statische Methode `ClipGraceFor(RuleState state)`, die alle drei Aufrufer nutzen.

### VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs (TVSenderAdminClipGroupsE2ETests)

- **Doppelter Code** — Der Block „erste MediaSource holen oder `E2E`-Quelle anlegen" (`db.MediaSources.FirstOrDefaultAsync(ct) ?? (await db.MediaSources.AddAsync(new MediaSource { Name = "E2E", ... }, ct)).Entity` + `SaveChangesAsync`) steht wortgleich dreimal in der Datei: `SeedClipItemsAsync` (Zeilen 76–78), `SeedMovieWithItemAsync` (Zeilen 106–108) und inline im Test `GeneratedSchedule_ContainsVisibleClipSlots` (Zeilen 302–304). Die Basisklasse enthält mit `EnsureMediaSourceIdAsync` bereits genau diese Logik (`PlaylistsE2ETestBase.cs:951`), sie ist dort aber `private`.

  Empfehlung: Einmalige private Hilfsmethode in der Testklasse (z. B. `EnsureSourceAsync(ApplicationDbContext db)`) oder `EnsureMediaSourceIdAsync` in `PlaylistsE2ETestBase` auf `protected` heben und alle drei Stellen darauf umstellen.

## Geprüfte Dateien

Geändert:
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/Data/ApplicationDbContext.cs`
- `VideoWebPlayer/Data/Configurations/ProgramRuleConfiguration.cs`
- `VideoWebPlayer/Data/ProgramRule.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Services/ProgramRuleInput.cs`
- `VideoWebPlayer/Services/ProgramRuleService.cs`
- `VideoWebPlayer/Services/ProgramScheduleService.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`
- `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`
- `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`
- `VideoWebPlayer.Tests/ProgramRuleCursorNullStrandKeyMigrationTests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests_FfmpegDownload.cs`

Neu:
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdminClipGroups.razor`
- `VideoWebPlayer/Data/ProgramClipGroup.cs`
- `VideoWebPlayer/Data/ProgramClipGroupItem.cs`
- `VideoWebPlayer/Data/ProgramRuleClipGroup.cs`
- `VideoWebPlayer/Data/Configurations/ProgramClipGroupConfiguration.cs`
- `VideoWebPlayer/Data/Configurations/ProgramClipGroupItemConfiguration.cs`
- `VideoWebPlayer/Data/Configurations/ProgramRuleClipGroupConfiguration.cs`
- `VideoWebPlayer/Migrations/20261025112747_AddProgramClipGroups.cs`
- `VideoWebPlayer/Migrations/20261025112747_AddProgramClipGroups.Designer.cs`
- `VideoWebPlayer/Services/IProgramClipGroupService.cs`
- `VideoWebPlayer/Services/ProgramClipGroupService.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/Helpers/TVSenderAdminE2ETestBase.cs`
- `VideoWebPlayer.Tests/ProgramClipGroupServiceTests.cs`
- `VideoWebPlayer.Tests/ProgramGuideServiceTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/ProgramRuleServiceTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs`
