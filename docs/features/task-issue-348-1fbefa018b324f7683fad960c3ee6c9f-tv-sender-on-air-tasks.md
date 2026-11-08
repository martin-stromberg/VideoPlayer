# Tasks: TV-Sender: On-Air (Issue #348)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `Setup.TvSenderOnAir` (bool, Initializer `= true`) in `VideoWebPlayer/Data/Setup.cs` anlegen | Offen | — |
| 2 | Datenmodell | EF-Migration `AddSetupTvSenderOnAir` erstellen (Spalte `defaultValue: true`, `ApplicationDbContextModelSnapshot` aktualisieren) | Offen | — |
| 3 | Backup | `Setups.TvSenderOnAir` in `OptionalRestoreColumns` eintragen (`VideoWebPlayerBackupData.cs`) | Offen | — |
| 4 | Backup | `(Setups, TvSenderOnAir, true)` in `OptionalRestoreBoolDefaults` eintragen | Offen | — |
| 5 | Logik | `ProgramSettingsService.GetTvSenderOnAirAsync` hinzufügen (Vorbild `GetTvSenderEnabledAsync`) | Offen | — |
| 6 | Logik | `ProgramSettingsService.UpdateTvSenderOnAirAsync` hinzufügen (Vorbild `UpdateTvSenderEnabledAsync`) | Offen | — |
| 7 | Logik | `LiveStreamWorker.ExecuteAsync`: On-Air-Gate in der Gate-Verzweigung (Off-Air → idempotenter `ILiveStreamSegmenter`-Stopp) | Offen | — |
| 8 | Logik | `LiveStreamController.GetFile`: On-Air-Gate nach der Benutzerfreigabe (`NotFound()` vor Datei- und Standby-Auslieferung) | Offen | — |
| 9 | UI | Off-Air-Bild-Asset `wwwroot/images/live-tv-off-air.png` erzeugen (16:9-Testbild per ffmpeg) und committen; `Content Update`-Eintrag in `VideoWebPlayer.csproj` (`CopyToOutputDirectory=Always`) | Offen | — |
| 10 | UI | `LiveTvErrorMessages.NotOnAir`-Konstante hinzufügen („Der Sender ist zurzeit nicht auf Sendung.", Verwendung als `alt`-Text) | Offen | — |
| 11 | UI | `LiveTv.razor`: `tvSenderOnAir`-Zustand in `OnInitializedAsync` lesen und in `PollLoopAsync` je Tick aktualisieren | Offen | — |
| 12 | UI | `LiveTv.razor`: Off-Air-Bild-Zweig `#live-tv-off-air` (`<img>` via `AssetVersions.Url("images/live-tv-off-air.png")`) im Player-Bereich (Vorrang vor `<video>`/Gap-Hinweis, Programmauskunft bleibt) | Offen | — |
| 13 | UI | `LiveTv.razor.css`: `.livetv-off-air`-Regel (16:9, `object-fit: cover`, wie `.livetv-video`) | Offen | — |
| 14 | UI | `LiveTv.razor`: Attach-Bedingung in `OnAfterRenderAsync` um `tvSenderOnAir` erweitern (Off-Air → `DetachPlayerAsync`, kein Re-Attach) | Offen | — |
| 15 | UI | `TVSenderAdmin.razor`: `tvSenderOnAir`-Zustand in `OnInitializedAsync` laden | Offen | — |
| 16 | UI | `TVSenderAdmin.razor`: Sub-Karte „Sendebetrieb" mit Sofort-Umschalter `#tvsender-on-air` im Tab „Einstellungen" | Offen | — |
| 17 | UI | `TVSenderAdmin.razor`: `ToggleOnAirAsync`-Handler (`UpdateTvSenderOnAirAsync` + `ScheduleSignal.NotifyScheduleChangedNow()` + Status/Fehler-Rollback) | Offen | — |
| 18 | Tests | `ProgramSettingsServiceTests`: `GetTvSenderOnAirAsync_DefaultsToTrue` + `UpdateTvSenderOnAirAsync_Roundtrip` | Offen | — |
| 19 | Tests | `VideoWebPlayerBackupDataTests_TvSenderOnAir` (neu): Alt-Backup ohne Spalte → Restore-Default `true` (Seed `false`, `LegacyBackupArchiveBuilder.RemoveColumnsAsync`) | Offen | — |
| 20 | Tests | `LiveStreamWorkerTests`: `ExecuteAsync_OffAir_StopsSegmenterAndNeverStarts` | Offen | — |
| 21 | Tests | `LiveStreamWorkerTests`: `ExecuteAsync_OffAirToOnAir_ResumesSegmentation` | Offen | — |
| 22 | Tests | `LiveStreamControllerTests`: `GetFile_OffAir_Returns404` (Theory: `live.m3u8`, `standby_0_00.ts`, `seg_001.ts`) | Offen | — |
| 23 | Tests | `LiveTvTests.CreateContext` um Parameter `tvSenderOnAir` (Default `true`) erweitern | Offen | — |
| 24 | Tests | `LiveTvTests`: `Page_OffAir_ShowsOffAirImage_ProgramGuideStillRenders` | Offen | — |
| 25 | Tests | `LiveTvTests`: `PollTick_OffAirSwitch_DetachesPlayerAndShowsOffAirImage` | Offen | — |
| 26 | Tests | `TVSenderAdminTests`: `SettingsTab_OnAirToggle_PersistsAndNotifies` | Offen | — |
| 27 | Tests | `TVSenderAdminTests`: `SettingsTab_OnAirToggle_SaveFails_RevertsCheckbox` | Offen | — |
| 28 | E2E-Tests | `Helpers/TVSenderAdminE2ETestBase`: Hilfsmethode `SetOnAirAsync(bool)` ergänzen | Offen | — |
| 29 | E2E-Tests | `TvSenderOnAirE2ETests` (neu): Admin schaltet Off-Air → Endpoint 404 → `/live-tv` zeigt Off-Air-Bild (`img` sichtbar, `naturalWidth > 0`) | Offen | — |
| 30 | E2E-Tests | `TvSenderOnAirE2ETests`: On-Air zurück → Stream/Standby wieder erreichbar, Player attacht | Offen | — |
| 31 | E2E-Tests | `LiveStreamE2ETests`: Off-Air blockiert Playlist- und Standby-Auslieferung für berechtigte Zuschauer | Offen | — |
| 32 | E2E-Tests | `LiveTvE2ETests`: Off-Air-Bild bei laufender Sendung mit sichtbarer Programmauskunft | Offen | — |
| 33 | Dokumentation | `docs/help/tv-sender.md`: On-Air-Schalter dokumentieren (Unterschied zur Freischaltung, Off-Air-Bild) | Offen | — |
| 34 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md`: Gate-Kette und Signal-Weg ergänzen | Offen | — |
| 35 | Dokumentation | `README.md` und `docs/RELEASE_NOTES.md` aktualisieren | Offen | — |
