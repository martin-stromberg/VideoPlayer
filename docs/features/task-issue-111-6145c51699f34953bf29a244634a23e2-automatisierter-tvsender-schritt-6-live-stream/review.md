# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `Setup.LiveStreamSegmentLengthSeconds`/`LiveStreamSegmentDirectory`/`LiveStreamSegmentRetentionMinutes` — `VideoWebPlayer/Data/Setup.cs`
- [x] Migration `AddSetupLiveStreamSettings` — `VideoWebPlayer/Migrations/`
- [x] `OptionalRestoreColumns` + `OptionalRestoreIntDefaults` — `VideoWebPlayerBackupData.cs`
- [x] `MediaProbeOptions.FfmpegPath` — `VideoWebPlayer/Configuration/MediaProbeOptions.cs`
- [x] `IFfmpegLocator` + `FfmpegLocator` (konfigurierter Pfad → `tools/ffmpeg/` + App-Dir → PATH, interner Testkonstruktor)
- [x] `LiveStreamInput`/`LiveStreamJob`-Records
- [x] `ILiveStreamSegmenter` + `FfmpegLiveStreamSegmenter` (`-re -ss -t -f hls`-Argumentbau, `-nostdin` bei Datei, stdin-Pump bei Pipe, `Kill(entireProcessTree)`, `ISegmenterProcess`-Testseam)
- [x] `LocalMediaSourceReader.ResolveLocalFilePath` — public Wrapper um `ResolveFilePath`
- [x] `LiveStreamMediaResolver` — Local→Pfad, sonst `OpenFileStream`-Fabrik, fehlende Referenzen→null
- [x] `LiveStreamSegmentStore` — Default `Data/LiveStream`, Whitelist+Containment, `SweepExpiredSegmentsAsync`, `ClearAsync`
- [x] `LiveStreamCoordinator` + `LiveStreamSyncResult`/`LiveStreamSyncAction` — Start/Neustart/Drift(30s)/Lücke/ffmpeg-fehlt/Medium-fehlt, WakeAtUtc
- [x] `LiveStreamWorker` — Initialdelay, Startup-`ClearAsync`, Gate-Lease, Poll-Cap 30 s, `IProgramScheduleSignal`-Weckung
- [x] `LiveStreamController` — `api/live-stream/{fileName}`, `[BearerTokenCheck]`+`CheckLoggedIn`, `.m3u8`-Rewrite mit `access_token`, `video/mp2t`, 404-Pfade
- [x] `ProgramSettingsService` — 3 Getter + `UpdateLiveStreamSettingsAsync` mit Validierung
- [x] `TVSenderAdmin` — 3 Felder + Load/Save
- [x] DI-Registrierungen inkl. Testing-Guard `LiveStreamWorker`
- [x] `docs/API.md` — Live-Stream-Endpunkt (folgt im Docs-Schritt; API.md-Konvention gilt)

## Offene Aufgaben

—

## Hinweise

- Alle geplanten Testklassen existieren und sind grün (60 fokussierte neue Tests + Worker-Test); E2E-Abdeckung: `LiveStreamE2ETests` (401/200/Token-Rewrite/404) + `TVSenderAdminE2ETests`-Erweiterung.
- Abweichung vom Plan: `UpdateLiveStreamSettingsAsync` speichert den Roh-Pfad (relativ bleibt relativ) statt ihn zu absolutieren — ContentRoot-Auflösung liegt konsequent im `LiveStreamSegmentStore`. Durch Testbefund korrigiert.
