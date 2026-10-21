# Tasks: Live-Stream ausliefern (Schritt 6)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `Setup`-Spalten `LiveStreamSegmentLengthSeconds`, `LiveStreamSegmentDirectory`, `LiveStreamSegmentRetentionMinutes` anlegen | Offen | — |
| 2 | Datenmodell | EF-Migration `AddSetupLiveStreamSettings` erzeugen | Offen | — |
| 3 | Datenmodell | `OptionalRestoreColumns` + `OptionalRestoreIntDefaults` für die drei Spalten registrieren | Offen | — |
| 4 | Konfiguration | `MediaProbeOptions.FfmpegPath` hinzufügen | Offen | — |
| 5 | Logik | `IFfmpegLocator` + `FfmpegLocator` (Suchreihenfolge konfigurierbarer Pfad → `tools/ffmpeg/` + App-Dir → PATH) | Offen | — |
| 6 | Logik | `LiveStreamInput`/`LiveStreamJob`-Records und `ILiveStreamSegmenter`/`FfmpegLiveStreamSegmenter` (Argumentbau `-re -ss -t -f hls`, stdin-Pump, Kill-Baum, `ISegmenterProcess`-Testseam) | Offen | — |
| 7 | Logik | `LocalMediaSourceReader.ResolveLocalFilePath` (public Wrapper um `ResolveFilePath`) | Offen | — |
| 8 | Logik | `LiveStreamMediaResolver` (MediaItemId → Pfad oder Stream-Fabrik) | Offen | — |
| 9 | Logik | `LiveStreamSegmentStore` (Verzeichnisauflösung, Whitelist/Containment, `SweepExpiredSegmentsAsync`, `ClearAsync`) | Offen | — |
| 10 | Logik | `LiveStreamCoordinator` + `LiveStreamSyncResult` (Start/Stop/Neustart/Drift/Lücke, WakeAtUtc) | Offen | — |
| 11 | Logik | `LiveStreamWorker` (Initialdelay, Startup-Clear, Gate-Lease, Poll-Cap, `IProgramScheduleSignal`-Weckung) | Offen | — |
| 12 | API | `LiveStreamController` (`api/live-stream/{fileName}`, BearerTokenCheck, `.m3u8`-Rewrite mit `access_token`, `.ts`-Auslieferung, Pfadsicherheit) | Offen | — |
| 13 | Logik | `ProgramSettingsService`: `GetLiveStreamSegmentLengthAsync`, `GetLiveStreamSegmentDirectoryAsync`, `GetLiveStreamSegmentRetentionAsync`, `UpdateLiveStreamSettingsAsync` mit Validierung | Offen | — |
| 14 | UI | `TVSenderAdmin`: drei Stream-Einstellungsfelder + Load/Save | Offen | — |
| 15 | DI | Registrierungen in `ServiceCollectionExtensions` inkl. Testing-Guard für `LiveStreamWorker` | Offen | — |
| 16 | Tests | `FfmpegLocatorTests` | Offen | — |
| 17 | Tests | `FfmpegLiveStreamSegmenterTests` (Fake-Prozess, Argumente, Pipe-Pump, Start/Stop) | Offen | — |
| 18 | Tests | `LiveStreamCoordinatorTests` (SQLite + Fakes: Start/Wechsel/Lücke/Drift/fehlendes ffmpeg/fehlendes Medium/WakeAtUtc) | Offen | — |
| 19 | Tests | `LiveStreamMediaResolverTests` | Offen | — |
| 20 | Tests | `LiveStreamSegmentStoreTests` | Offen | — |
| 21 | Tests | `LiveStreamControllerTests` (m3u8-Rewrite, Content-Type, 404/Whitelist) | Offen | — |
| 22 | Tests | `ProgramSettingsService`-Stream-Settings-Tests | Offen | — |
| 23 | Tests | `LiveStreamWorkerTests` (Startup-Clear/Tick/Poll) | Offen | — |
| 24 | Tests | Alt-Backup-Regressionstest für die drei Spalten | Offen | — |
| 25 | E2E-Tests | `LiveStreamE2ETests`: 401 anonym, Playlist+Token-Rewrite via `access_token`, Segment 200, 404/Traversal | Offen | — |
| 26 | E2E-Tests | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` um Stream-Felder erweitern | Offen | — |
| 27 | Dokumentation | `docs/API.md` Live-Stream-Endpunkt, `docs/help/tv-sender.md`, ffmpeg-Lizenz/Bereitstellung | Offen | — |
| 28 | Dokumentation | `docs/RELEASE_NOTES.md`, `changes.log` | Offen | — |
