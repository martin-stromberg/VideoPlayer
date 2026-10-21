# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `VideoFileExtensions` (`VideoWebPlayer/Utils/VideoFileExtensions.cs`) — angelegt mit `All` + `IsVideoFileName`
- [x] Feld `MediaItem.Duration` (`TimeSpan?`) — vorhanden in `VideoWebPlayer/Data/MediaItem.cs`
- [x] Feld `MediaItem.DurationProbedAt` (`DateTime?`) — vorhanden
- [x] Index `IX_MediaItems_DurationProbedAt` in `MediaItemConfiguration` — vorhanden
- [x] Migration `20261021033436_AddMediaItemDuration` — Spalten `Duration`/`DurationProbedAt` (TEXT, nullable) + Index, Snapshot aktualisiert
- [x] `MediaProbeOptions` (`VideoWebPlayer/Configuration/`) — `FfprobePath`, `IntervalSeconds`, `BatchSize`, `TimeoutSeconds` + `Effective*` Normalisierung
- [x] `appsettings.json`-Abschnitt `MediaProbe` — vorhanden
- [x] `IFfprobeLocator`/`FfprobeLocator` (`VideoWebPlayer/Services/`) — Auflösung Config → `tools/ffmpeg/` + App-Dir → `PATH`; interner Test-Seam (baseDirectory, PATH-Func)
- [x] `IMediaDurationProbe`/`FfprobeMediaDurationProbe` — `ProbeFileAsync`/`ProbeStreamAsync`, `BuildArguments`, `TryParseDurationOutput`, Timeout/Kill, stdin-Piping, interner Delegate-Seam; `FfprobeInvocationResult`
- [x] `MediaDurationProbeService` — Pending-Abfrage (`DurationProbedAt == null` + Video-Endungen via OR-Expression), BatchSize, Local→`ProbeFileAsync`, SFTP→`OpenFileStream`→`ProbeStreamAsync`, Fehler→`Duration=null`+`DurationProbedAt`, Binary-missing→keine Markierung (statisches Log-Flag)
- [x] `MediaDurationProbeWorker` — Startup-Catch-up, Batch-Loop bis leer, Intervall, `IBackgroundProcessingGate`-Lease
- [x] `MediaSourceClassifier` — nutzt `VideoFileExtensions.All` (Zeile 40), kein Verhaltensunterschied
- [x] `MediaSourceScanner` — Dateiwechsel-Zweig setzt `Duration`/`DurationProbedAt` zurück (Zeilen 274–275)
- [x] `VideoWebPlayerBackupData.OptionalRestoreColumns` — `MediaItems.Duration`, `MediaItems.DurationProbedAt` registriert (Zeilen 76–77), kein Default → `NULL` = ungeprüft
- [x] DI-Registrierung in `ServiceCollectionExtensions` — Options, Locator, Probe, Service (scoped), `AddHostedService<MediaDurationProbeWorker>` (Zeilen 233–237)
- [x] `MediaDurationProbeServiceTests` — 10 Testmethoden (Erfolg, Fehlschlag+Kein-Retry, bereits geprüft, Nicht-Video, Local-Routing, SFTP-Stream, Null-Stream, Exception-Fortsetzung, fehlendes Binary, BatchSize)
- [x] `FfprobeMediaDurationProbeTests` — Parsing (2+8 Fälle), Exit-Code, Timeout, fehlendes Binary, stdin-Piping, Timeout-Übergabe, Exception, Cancellation
- [x] `FfprobeLocatorTests` — 5 Tests zur Auflösungsreihenfolge
- [x] `MediaSourceScannerLocalTests.ScanNextMediaCollection_ChangedFile_ResetsDurationFields` — Reset-Nachweis
- [x] `VideoWebPlayerBackupDataTests.ReadFromAsync_LegacyBackupWithoutDurationColumnsInMediaItems_RestoresSuccessfully` — Alt-Backup-Regression
- [x] `MediaDurationProbeWorkerTests.ExecuteAsync_ProcessesPendingItems` — Worker treibt Service an

## Offene Aufgaben

Keine.

## Hinweise

- Tests laufen gegen echtes SQLite (shared in-memory), nicht EF InMemory — wie gefordert.
- ffprobe-Lizenz-/Bereitstellungsfestlegung ist in `inventory/ffmpeg.md` dokumentiert; Lizenztext-Beilage entfällt, da kein Binary ausgeliefert wird.
