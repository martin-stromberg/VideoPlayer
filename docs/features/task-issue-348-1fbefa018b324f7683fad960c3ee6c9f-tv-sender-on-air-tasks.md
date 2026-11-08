# Tasks: TV-Sender: On-Air (Issue #348)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `Setup.TvSenderOnAir` (bool, Initializer `= true`) in `VideoWebPlayer/Data/Setup.cs` anlegen | Fertig | — |
| 2 | Datenmodell | EF-Migration `AddSetupTvSenderOnAir` erstellen (Spalte `defaultValue: true`, `ApplicationDbContextModelSnapshot` aktualisieren) | Fertig | — |
| 3 | Backup | `Setups.TvSenderOnAir` in `OptionalRestoreColumns` eintragen (`VideoWebPlayerBackupData.cs`) | Fertig | — |
| 4 | Backup | `(Setups, TvSenderOnAir, true)` in `OptionalRestoreBoolDefaults` eintragen | Fertig | — |
| 5 | Logik | `ProgramSettingsService.GetTvSenderOnAirAsync` hinzufügen (Vorbild `GetTvSenderEnabledAsync`) | Fertig | `GetTvSenderOnAirAsync_DefaultsToTrue` |
| 6 | Logik | `ProgramSettingsService.UpdateTvSenderOnAirAsync` hinzufügen (Vorbild `UpdateTvSenderEnabledAsync`) | Fertig | `UpdateTvSenderOnAirAsync_Roundtrip` |
| 7 | Logik | `LiveStreamWorker.ExecuteAsync`: On-Air-Gate in der Gate-Verzweigung (Off-Air → idempotenter `ILiveStreamSegmenter`-Stopp) | Fertig | `ExecuteAsync_OffAir_StopsSegmenterAndNeverStarts` |
| 8 | Logik | `LiveStreamController.GetFile`: On-Air-Gate nach der Benutzerfreigabe (`NotFound()` vor Datei- und Standby-Auslieferung) | Fertig | `GetFile_OffAir_Returns404` |
| 9 | UI | Off-Air-Bild-Asset `wwwroot/images/live-tv-off-air.png` erzeugt (16:9-Testbild per ffmpeg) und committet; `Content Update`-Eintrag in `VideoWebPlayer.csproj` (`CopyToOutputDirectory=Always`) | Fertig | — |
| 10 | UI | `LiveTvErrorMessages.NotOnAir`-Konstante hinzufügen („Der Sender ist zurzeit nicht auf Sendung.", Verwendung als `alt`-Text) | Fertig | `Page_OffAir_ShowsOffAirImage_ProgramGuideStillRenders` |
| 11 | UI | `LiveTv.razor`: `tvSenderOnAir`-Zustand in `OnInitializedAsync` lesen und in `PollLoopAsync` je Tick aktualisieren | Fertig | `PollTick_OffAirSwitch_DetachesPlayerAndShowsOffAirImage` |
| 12 | UI | `LiveTv.razor`: Off-Air-Bild-Zweig `#live-tv-off-air` (`<img>` via `AssetVersions.Url("images/live-tv-off-air.png")`) im Player-Bereich (Vorrang vor `<video>`/Gap-Hinweis, Programmauskunft bleibt) | Fertig | `Page_OffAir_ShowsOffAirImage_ProgramGuideStillRenders` |
| 13 | UI | `LiveTv.razor.css`: `.livetv-off-air`-Regel (16:9, `object-fit: cover`, wie `.livetv-video`) | Fertig | — |
| 14 | UI | `LiveTv.razor`: Attach-Bedingung in `OnAfterRenderAsync` um `tvSenderOnAir` erweitern (Off-Air → `DetachPlayerAsync`, kein Re-Attach) | Fertig | `PollTick_OffAirSwitch_DetachesPlayerAndShowsOffAirImage` |
| 15 | UI | `TVSenderAdmin.razor`: `tvSenderOnAir`-Zustand in `OnInitializedAsync` laden | Fertig | `SettingsTab_OnAirToggle_PersistsAndNotifies` |
| 16 | UI | `TVSenderAdmin.razor`: Sub-Karte „Sendebetrieb" mit Sofort-Umschalter `#tvsender-on-air` im Tab „Einstellungen" | Fertig | `SettingsTab_OnAirToggle_PersistsAndNotifies` |
| 17 | UI | `TVSenderAdmin.razor`: `ToggleOnAirAsync`-Handler (`UpdateTvSenderOnAirAsync` + `ScheduleSignal.NotifyScheduleChangedNow()` + Status/Fehler-Rollback) | Fertig | `SettingsTab_OnAirToggle_PersistsAndNotifies`, `SettingsTab_OnAirToggle_SaveFails_RevertsCheckbox` |
| 18 | Tests | `ProgramSettingsServiceTests`: `GetTvSenderOnAirAsync_DefaultsToTrue` + `UpdateTvSenderOnAirAsync_Roundtrip` | Fertig | grün |
| 19 | Tests | `VideoWebPlayerBackupDataTests_TvSenderOnAir` (neu): Alt-Backup ohne Spalte → Restore-Default `true` (Seed `false`, `LegacyBackupArchiveBuilder.RemoveColumnsAsync`) | Fertig | grün |
| 20 | Tests | `LiveStreamWorkerTests`: `ExecuteAsync_OffAir_StopsSegmenterAndNeverStarts` | Fertig | grün |
| 21 | Tests | `LiveStreamWorkerTests`: `ExecuteAsync_OffAirToOnAir_ResumesSegmentation` | Fertig | grün |
| 22 | Tests | `LiveStreamControllerTests`: `GetFile_OffAir_Returns404` (Theory: `live.m3u8`, `standby_0_00.ts`, `seg_001.ts`) | Fertig | grün |
| 23 | Tests | `LiveTvTests.CreateContext` um Parameter `tvSenderOnAir` (Default `true`) erweitern | Fertig | — |
| 24 | Tests | `LiveTvTests`: `Page_OffAir_ShowsOffAirImage_ProgramGuideStillRenders` | Fertig | grün |
| 25 | Tests | `LiveTvTests`: `PollTick_OffAirSwitch_DetachesPlayerAndShowsOffAirImage` | Fertig | grün |
| 26 | Tests | `TVSenderAdminTests`: `SettingsTab_OnAirToggle_PersistsAndNotifies` | Fertig | grün |
| 27 | Tests | `TVSenderAdminTests`: `SettingsTab_OnAirToggle_SaveFails_RevertsCheckbox` | Fertig | grün |
| 28 | E2E-Tests | `Helpers/TVSenderAdminE2ETestBase`: Hilfsmethode `SetOnAirAsync(bool)` ergänzen | Fertig | — |
| 29 | E2E-Tests | `TvSenderOnAirE2ETests` (neu): Admin schaltet Off-Air → Endpoint 404 → `/live-tv` zeigt Off-Air-Bild (`img` sichtbar, `naturalWidth > 0`) | Fertig | `Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` |
| 30 | E2E-Tests | `TvSenderOnAirE2ETests`: On-Air zurück → Stream/Standby wieder erreichbar, Player attacht | Fertig | `Admin_TogglesBackOnAir_StreamAndPlayerResume` |
| 31 | E2E-Tests | `LiveStreamE2ETests`: Off-Air blockiert Playlist- und Standby-Auslieferung für berechtigte Zuschauer | Fertig | `PairedDevice_OffAir_PlaylistAndStandby_Return404` |
| 32 | E2E-Tests | `LiveTvE2ETests`: Off-Air-Bild bei laufender Sendung mit sichtbarer Programmauskunft | Fertig | `LiveTv_OffAir_ShowsOffAirImage_GuideStillRenders` |
| 33 | Dokumentation | `docs/help/tv-sender.md`: On-Air-Schalter dokumentieren (Unterschied zur Freischaltung, Off-Air-Bild) | Fertig | — |
| 34 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md`: Gate-Kette und Signal-Weg ergänzen | Fertig | — |
| 35 | Dokumentation | `README.md` und `docs/RELEASE_NOTES.md` aktualisieren | Fertig | — |
