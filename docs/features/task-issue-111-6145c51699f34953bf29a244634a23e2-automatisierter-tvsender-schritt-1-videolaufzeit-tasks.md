# Tasks: Videolaufzeit erfassen (Schritt 1 – Automatisierter TV-Sender)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `VideoFileExtensions` (geteilte Video-Endungsliste) anlegen | Offen | — |
| 2 | Datenmodell | `MediaItem.Duration` (`TimeSpan?`) anlegen | Offen | — |
| 3 | Datenmodell | `MediaItem.DurationProbedAt` (`DateTime?`) anlegen | Offen | — |
| 4 | Datenmodell | Index auf `DurationProbedAt` in `MediaItemConfiguration` | Offen | — |
| 5 | Datenmodell | EF-Migration `AddMediaItemDuration` erstellen | Offen | — |
| 6 | Konfiguration | `MediaProbeOptions` anlegen (`FfprobePath`, `IntervalSeconds`, `BatchSize`, `TimeoutSeconds`) | Offen | — |
| 7 | Konfiguration | `appsettings.json`-Abschnitt `MediaProbe` ergänzen | Offen | — |
| 8 | Logik | `IFfprobeLocator`/`FfprobeLocator` implementieren (Config → Konventionsverzeichnis → PATH) | Offen | — |
| 9 | Logik | `IMediaDurationProbe`/`FfprobeMediaDurationProbe` implementieren (Datei- und Stream-Probe, Timeout, Parsing) | Offen | — |
| 10 | Logik | `MediaDurationProbeService` implementieren (Batch, Quelltyp-Routing, Fehlermarkierung) | Offen | — |
| 11 | Logik | `MediaDurationProbeWorker` implementieren (Startup + Intervall, Gate-Lease) | Offen | — |
| 12 | Logik | `MediaSourceClassifier` auf `VideoFileExtensions` umstellen | Offen | — |
| 13 | Logik | `MediaSourceScanner`: `Duration`/`DurationProbedAt` bei Dateiwechsel zurücksetzen | Offen | — |
| 14 | DI | Services + Options + HostedService in `ServiceCollectionExtensions` registrieren | Offen | — |
| 15 | Backup | `OptionalRestoreColumns` um `MediaItems.Duration`/`DurationProbedAt` erweitern | Offen | — |
| 16 | Tests | `MediaDurationProbeServiceTests` (Erfolg, Fehlschlag, Skip, Routing, BatchSize, kein Binary, Exception) | Offen | — |
| 17 | Tests | `FfprobeMediaDurationProbeTests` (Parsing, Exit-Code, Timeout, stdin-Piping) | Offen | — |
| 18 | Tests | `FfprobeLocatorTests` (Auflösungsreihenfolge) | Offen | — |
| 19 | Tests | Scanner-Test: Dateiwechsel setzt Duration-Felder zurück | Offen | — |
| 20 | Tests | Backup-Regressionstest: Alt-Backup ohne neue Spalten wiederherstellbar | Offen | — |
| 21 | Tests | `MediaDurationProbeWorkerTests` (Worker ruft Service auf) | Offen | — |
| 22 | Dokumentation | `docs/help/` (ffprobe-Einrichtung, Laufzeit-Verhalten) | Offen | — |
| 23 | Dokumentation | `README.md` bei Bedarf ergänzen | Offen | — |
| 24 | Dokumentation | `docs/RELEASE_NOTES.md` ergänzen | Offen | — |
