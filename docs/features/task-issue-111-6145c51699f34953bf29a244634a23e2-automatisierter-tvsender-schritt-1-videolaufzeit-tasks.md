# Tasks: Videolaufzeit erfassen (Schritt 1 – Automatisierter TV-Sender)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `VideoFileExtensions` (geteilte Video-Endungsliste) anlegen | Erledigt | `MediaDurationProbeServiceTests.ProbePendingAsync_SkipsNonVideoItems` |
| 2 | Datenmodell | `MediaItem.Duration` (`TimeSpan?`) anlegen | Erledigt | `MediaDurationProbeServiceTests.ProbePendingAsync_ProbesPendingVideoItems_WritesDuration` |
| 3 | Datenmodell | `MediaItem.DurationProbedAt` (`DateTime?`) anlegen | Erledigt | `MediaDurationProbeServiceTests.ProbePendingAsync_FailedProbe_MarksProbedWithoutDuration` |
| 4 | Datenmodell | Index auf `DurationProbedAt` in `MediaItemConfiguration` | Erledigt | Migration `AddMediaItemDuration` (`IX_MediaItems_DurationProbedAt`) |
| 5 | Datenmodell | EF-Migration `AddMediaItemDuration` erstellen | Erledigt | Build + voller Testlauf |
| 6 | Konfiguration | `MediaProbeOptions` anlegen | Erledigt | `FfprobeMediaDurationProbeTests.ProbeFileAsync_PassesConfiguredTimeout` |
| 7 | Konfiguration | `appsettings.json`-Abschnitt `MediaProbe` ergänzen | Erledigt | Kein direkter Test |
| 8 | Logik | `IFfprobeLocator`/`FfprobeLocator` implementieren | Erledigt | `FfprobeLocatorTests` (5 Tests) |
| 9 | Logik | `IMediaDurationProbe`/`FfprobeMediaDurationProbe` implementieren | Erledigt | `FfprobeMediaDurationProbeTests` (13 Tests) |
| 10 | Logik | `MediaDurationProbeService` implementieren | Erledigt | `MediaDurationProbeServiceTests` (10 Tests) |
| 11 | Logik | `MediaDurationProbeWorker` implementieren | Erledigt | `MediaDurationProbeWorkerTests.ExecuteAsync_ProcessesPendingItems` |
| 12 | Logik | `MediaSourceClassifier` auf `VideoFileExtensions` umstellen | Erledigt | bestehende Classifier-Tests (unverändert grün) |
| 13 | Logik | `MediaSourceScanner`: Duration-Felder bei Dateiwechsel zurücksetzen | Erledigt | `MediaSourceScannerLocalTests.ScanNextMediaCollection_ChangedFile_ResetsDurationFields` |
| 14 | DI | Services + Options + HostedService registrieren | Erledigt | Worker-Test + App-Start in E2E-Tests |
| 15 | Backup | `OptionalRestoreColumns` um neue Spalten erweitern | Erledigt | `VideoWebPlayerBackupDataTests.ReadFromAsync_LegacyBackupWithoutDurationColumnsInMediaItems_RestoresSuccessfully` |
| 16–21 | Tests | Testklassen gemäß Plan | Erledigt | 35 neue Tests, alle grün |
| 22 | Dokumentation | `docs/help/videolaufzeit.md` | Erledigt | Kein direkter Test |
| 23 | Dokumentation | `README.md` ergänzt | Erledigt | Kein direkter Test |
| 24 | Dokumentation | `docs/RELEASE_NOTES.md` ergänzt | Erledigt | Kein direkter Test |
