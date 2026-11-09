# Tasks: Live-Stream ausliefern (Schritt 6)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `Setup`-Spalten `LiveStreamSegmentLengthSeconds`, `LiveStreamSegmentDirectory`, `LiveStreamSegmentRetentionMinutes` anlegen | Erledigt | `ProgramSettingsServiceLiveStreamTests` (Roundtrip) |
| 2 | Datenmodell | EF-Migration `AddSetupLiveStreamSettings` erzeugen | Erledigt | Migrations-Build + Testsuite |
| 3 | Datenmodell | `OptionalRestoreColumns` + `OptionalRestoreIntDefaults` für die drei Spalten registrieren | Erledigt | `ReadFromAsync_LegacyBackupWithoutLiveStreamSettings_RestoresSuccessfully` |
| 4 | Konfiguration | `MediaProbeOptions.FfmpegPath` hinzufügen | Erledigt | `FfmpegLocatorTests` |
| 5 | Logik | `IFfmpegLocator` + `FfmpegLocator` (Suchreihenfolge konfigurierbarer Pfad → `tools/ffmpeg/` + App-Dir → PATH) | Erledigt | `FfmpegLocatorTests` |
| 6 | Logik | `LiveStreamInput`/`LiveStreamJob`-Records und `ILiveStreamSegmenter`/`FfmpegLiveStreamSegmenter` (Argumentbau `-re -ss -t -f hls`, stdin-Pump, Kill-Baum, `ISegmenterProcess`-Testseam) | Erledigt | `FfmpegLiveStreamSegmenterTests` |
| 7 | Logik | `LocalMediaSourceReader.ResolveLocalFilePath` (public Wrapper um `ResolveFilePath`) | Erledigt | `LiveStreamMediaResolverTests.ResolveAsync_LocalSource_ReturnsFilePath` |
| 8 | Logik | `LiveStreamMediaResolver` (MediaItemId → Pfad oder Stream-Fabrik) | Erledigt | `LiveStreamMediaResolverTests` |
| 9 | Logik | `LiveStreamSegmentStore` (Verzeichnisauflösung, Whitelist/Containment, `SweepExpiredSegmentsAsync`, `ClearAsync`) | Erledigt | `LiveStreamSegmentStoreTests` |
| 10 | Logik | `LiveStreamCoordinator` + `LiveStreamSyncResult` (Start/Stop/Neustart/Drift/Lücke, WakeAtUtc) | Erledigt | `LiveStreamCoordinatorTests` |
| 11 | Logik | `LiveStreamWorker` (Initialdelay, Startup-Clear, Gate-Lease, Poll-Cap, `IProgramScheduleSignal`-Weckung) | Erledigt | `LiveStreamWorkerTests.ExecuteAsync_Startup_ClearsSegmentDirectoryAndTicks` |
| 12 | API | `LiveStreamController` (`api/live-stream/{fileName}`, BearerTokenCheck, `.m3u8`-Rewrite mit `access_token`, `.ts`-Auslieferung, Pfadsicherheit) | Erledigt | `LiveStreamControllerTests`, `LiveStreamE2ETests` |
| 13 | Logik | `ProgramSettingsService`: 3 Getter + `UpdateLiveStreamSettingsAsync` mit Validierung | Erledigt | `ProgramSettingsServiceLiveStreamTests` |
| 14 | UI | `TVSenderAdmin`: drei Stream-Einstellungsfelder + Load/Save | Erledigt | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (erweitert) |
| 15 | DI | Registrierungen in `ServiceCollectionExtensions` inkl. Testing-Guard für `LiveStreamWorker` | Erledigt | `LiveStreamE2ETests` (Testing-Host ohne Worker) |
| 16 | Tests | `FfmpegLocatorTests` | Erledigt | — |
| 17 | Tests | `FfmpegLiveStreamSegmenterTests` (Fake-Prozess, Argumente, Pipe-Pump, Start/Stop) | Erledigt | — |
| 18 | Tests | `LiveStreamCoordinatorTests` (SQLite + Fakes: Start/Wechsel/Lücke/Drift/fehlendes ffmpeg/fehlendes Medium/WakeAtUtc) | Erledigt | — |
| 19 | Tests | `LiveStreamMediaResolverTests` | Erledigt | — |
| 20 | Tests | `LiveStreamSegmentStoreTests` | Erledigt | — |
| 21 | Tests | `LiveStreamControllerTests` (m3u8-Rewrite, Content-Type, 404/Whitelist) | Erledigt | — |
| 22 | Tests | `ProgramSettingsService`-Stream-Settings-Tests | Erledigt | — |
| 23 | Tests | `LiveStreamWorkerTests` (Startup-Clear/Tick) | Erledigt | — |
| 24 | Tests | Alt-Backup-Regressionstest für die drei Spalten | Erledigt | — |
| 25 | E2E-Tests | `LiveStreamE2ETests`: 401 anonym, Playlist+Token-Rewrite via `access_token`, Segment 200, 404/Traversal | Erledigt | — |
| 26 | E2E-Tests | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` um Stream-Felder erweitern | Erledigt | — |
| 27 | Dokumentation | `docs/API.md` Live-Stream-Endpunkt, `docs/help/tv-sender.md`, ffmpeg-Lizenz/Bereitstellung | Erledigt | — |
| 28 | Dokumentation | `docs/RELEASE_NOTES.md` (What's-New-Eintrag) | Erledigt | — |
