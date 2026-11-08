# Tasks: TV-Sender: On-Air (Issue #348)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `Setup.TvSenderOnAir` (bool, Initializer `= true`) in `VideoWebPlayer/Data/Setup.cs` anlegen | Erledigt | `GetTvSenderOnAirAsync_DefaultsToTrue` |
| 2 | Datenmodell | EF-Migration `AddSetupTvSenderOnAir` erstellen (Spalte `defaultValue: true`, `ApplicationDbContextModelSnapshot` aktualisieren) | Erledigt | `GetTvSenderOnAirAsync_DefaultsToTrue` (Tests laufen gegen das migrierte Schema) |
| 3 | Backup | `Setups.TvSenderOnAir` in `OptionalRestoreColumns` eintragen (`VideoWebPlayerBackupData.cs`) | Erledigt | `ReadFromAsync_LegacyBackupWithoutTvSenderOnAirColumn_RestoresDefaultTrue` |
| 4 | Backup | `(Setups, TvSenderOnAir, true)` in `OptionalRestoreBoolDefaults` eintragen | Erledigt | `ReadFromAsync_LegacyBackupWithoutTvSenderOnAirColumn_RestoresDefaultTrue` |
| 5 | Logik | `ProgramSettingsService.GetTvSenderOnAirAsync` hinzufügen (Vorbild `GetTvSenderEnabledAsync`) | Erledigt | `GetTvSenderOnAirAsync_DefaultsToTrue` |
| 6 | Logik | `ProgramSettingsService.UpdateTvSenderOnAirAsync` hinzufügen (Vorbild `UpdateTvSenderEnabledAsync`) | Erledigt | `UpdateTvSenderOnAirAsync_Roundtrip` |
| 7 | Logik | `LiveStreamWorker.ExecuteAsync`: On-Air-Gate in der Gate-Verzweigung (Off-Air → idempotenter `ILiveStreamSegmenter`-Stopp) | Erledigt | `ExecuteAsync_OffAir_StopsSegmenterAndNeverStarts` |
| 8 | Logik | `LiveStreamController.GetFile`: On-Air-Gate nach der Benutzerfreigabe (`NotFound()` vor Datei- und Standby-Auslieferung) | Erledigt | `GetFile_OffAir_Returns404` |
| 9 | UI | Off-Air-Bild-Asset `wwwroot/images/live-tv-off-air.png` erzeugt (16:9-Testbild per ffmpeg) und committet; `Content Update`-Eintrag in `VideoWebPlayer.csproj` (`CopyToOutputDirectory=Always`) | Erledigt | `LiveTv_OffAir_ShowsOffAirImage_GuideStillRenders` (`naturalWidth > 0` beweist Auslieferung) |
| 10 | UI | `LiveTvErrorMessages.NotOnAir`-Konstante hinzufügen („Der Sender ist zurzeit nicht auf Sendung.", Verwendung als `alt`-Text) | Erledigt | `Page_OffAir_ShowsOffAirImage_ProgramGuideStillRenders` |
| 11 | UI | `LiveTv.razor`: `tvSenderOnAir`-Zustand in `OnInitializedAsync` lesen und in `PollLoopAsync` je Tick aktualisieren | Erledigt | `PollTick_OffAirSwitch_DetachesPlayerAndShowsOffAirImage` |
| 12 | UI | `LiveTv.razor`: Off-Air-Bild-Zweig `#live-tv-off-air` (`<img>` via `AssetVersions.Url("images/live-tv-off-air.png")`) im Player-Bereich (Vorrang vor `<video>`/Gap-Hinweis, Programmauskunft bleibt) | Erledigt | `Page_OffAir_ShowsOffAirImage_ProgramGuideStillRenders` |
| 13 | UI | `LiveTv.razor.css`: `.livetv-off-air`-Regel (16:9, `object-fit: cover`, wie `.livetv-video`) | Erledigt | `LiveTv_OffAir_ShowsOffAirImage_GuideStillRenders` (Sichtbarkeits-Assertion setzt Größe voraus) |
| 14 | UI | `LiveTv.razor`: Attach-Bedingung in `OnAfterRenderAsync` um `tvSenderOnAir` erweitern (Off-Air → `DetachPlayerAsync`, kein Re-Attach) | Erledigt | `PollTick_OffAirSwitch_DetachesPlayerAndShowsOffAirImage` |
| 15 | UI | `TVSenderAdmin.razor`: `tvSenderOnAir`-Zustand in `OnInitializedAsync` laden | Erledigt | `SettingsTab_OnAirToggle_PersistsAndNotifies` |
| 16 | UI | `TVSenderAdmin.razor`: Sub-Karte „Sendebetrieb" mit Sofort-Umschalter `#tvsender-on-air` im Tab „Einstellungen" | Erledigt | `SettingsTab_OnAirToggle_PersistsAndNotifies` |
| 17 | UI | `TVSenderAdmin.razor`: `ToggleOnAirAsync`-Handler (`UpdateTvSenderOnAirAsync` + `ScheduleSignal.NotifyScheduleChangedNow()` + Status/Fehler-Rollback) | Erledigt | `SettingsTab_OnAirToggle_PersistsAndNotifies`, `SettingsTab_OnAirToggle_SaveFails_RevertsCheckbox` |
| 18 | Tests | `ProgramSettingsServiceTests`: `GetTvSenderOnAirAsync_DefaultsToTrue` + `UpdateTvSenderOnAirAsync_Roundtrip` | Erledigt | grün |
| 19 | Tests | `VideoWebPlayerBackupDataTests_TvSenderOnAir` (neu): Alt-Backup ohne Spalte → Restore-Default `true` (Seed `false`, `LegacyBackupArchiveBuilder.RemoveColumnsAsync`) | Erledigt | grün |
| 20 | Tests | `LiveStreamWorkerTests`: `ExecuteAsync_OffAir_StopsSegmenterAndNeverStarts` | Erledigt | grün |
| 21 | Tests | `LiveStreamWorkerTests`: `ExecuteAsync_OffAirToOnAir_ResumesSegmentation` | Erledigt | grün |
| 22 | Tests | `LiveStreamControllerTests`: `GetFile_OffAir_Returns404` (Theory: `live.m3u8`, `standby_0_00.ts`, `seg_001.ts`) | Erledigt | grün |
| 23 | Tests | `LiveTvTests.CreateContext` um Parameter `tvSenderOnAir` (Default `true`) erweitern | Erledigt | `Page_OffAir_ShowsOffAirImage_ProgramGuideStillRenders` |
| 24 | Tests | `LiveTvTests`: `Page_OffAir_ShowsOffAirImage_ProgramGuideStillRenders` | Erledigt | grün |
| 25 | Tests | `LiveTvTests`: `PollTick_OffAirSwitch_DetachesPlayerAndShowsOffAirImage` | Erledigt | grün |
| 26 | Tests | `TVSenderAdminTests`: `SettingsTab_OnAirToggle_PersistsAndNotifies` | Erledigt | grün |
| 27 | Tests | `TVSenderAdminTests`: `SettingsTab_OnAirToggle_SaveFails_RevertsCheckbox` | Erledigt | grün |
| 28 | E2E-Tests | `Helpers/TVSenderAdminE2ETestBase`: Hilfsmethode `SetOnAirAsync(bool)` ergänzen | Erledigt | `Admin_TogglesBackOnAir_StreamAndPlayerResume` |
| 29 | E2E-Tests | `TvSenderOnAirE2ETests` (neu): Admin schaltet Off-Air → Endpoint 404 → `/live-tv` zeigt Off-Air-Bild (`img` sichtbar, `naturalWidth > 0`) | Erledigt | `Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` |
| 30 | E2E-Tests | `TvSenderOnAirE2ETests`: On-Air zurück → Stream/Standby wieder erreichbar, Player attacht | Erledigt | `Admin_TogglesBackOnAir_StreamAndPlayerResume` |
| 31 | E2E-Tests | `LiveStreamE2ETests`: Off-Air blockiert Playlist- und Standby-Auslieferung für berechtigte Zuschauer | Erledigt | `PairedDevice_OffAir_PlaylistAndStandby_Return404` |
| 32 | E2E-Tests | `LiveTvE2ETests`: Off-Air-Bild bei laufender Sendung mit sichtbarer Programmauskunft | Erledigt | `LiveTv_OffAir_ShowsOffAirImage_GuideStillRenders` |
| 33 | Dokumentation | `docs/help/tv-sender.md`: On-Air-Schalter dokumentieren (Unterschied zur Freischaltung, Off-Air-Bild) | Erledigt | Kein direkter Test |
| 34 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md`: Gate-Kette und Signal-Weg ergänzen | Erledigt | Kein direkter Test |
| 35 | Dokumentation | `README.md` und `docs/RELEASE_NOTES.md` aktualisieren | Erledigt | Kein direkter Test |
