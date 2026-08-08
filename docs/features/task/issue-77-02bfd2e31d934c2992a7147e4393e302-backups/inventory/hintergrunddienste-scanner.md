# Hintergrunddienste und Scanner

## Registrierte Hintergrundprozesse

In `ServiceCollectionExtensions.AddVideoWebPlayerServices` werden registriert:

- `AddHostedService<MediaSourceScanService>()`
- `AddHostedService<ContinueWatchingWorker>()`

Zusaetzlich startet `Program.cs` manuell einen `UdpDiscoveryListener` ueber `udpListener.Start()`.

## MediaSourceScanService

`MediaSourceScanService` erbt von `BackgroundService`.

Aufgaben:

- Startet optional `DataUpgradeManager.EnsureUpToDateAsync`.
- Wartet initial 10 Sekunden.
- Holt Scanintervalle aus `ProgramSettingsService`.
- Fuehrt `MediaSourceScanner.ScanAllSourcesAsync` aus.
- Fuehrt bis zu 64 `ScanNextMediaCollection`-Schritte pro Lauf aus.
- Stoesst `MediaSourceClassifier` fuer MediaItems, MediaCollections und Genre-Reload an.
- Sendet SignalR-Updates ueber `MediaUpdateNotificationService`.

Der Dienst nutzt den Host-`stoppingToken`. Es gibt keinen separaten Pause-Mechanismus fuer Restore.

## MediaSourceScanner

`MediaSourceScanner` ist scoped und schreibt direkt in `ApplicationDbContext`.

Wichtige Schreiboperationen:

- Aktualisiert `MediaSource.LastScannedAt`.
- Legt `MediaCollections` und `MediaItems` an.
- Setzt `ScanDueAt`, `LastScannedAt`, `Classifyable`, `Changed`, `ClassifiedAt`.
- Speichert mehrfach innerhalb eines Scans.

Der Scanner kann manuell aus der Admin-UI gestartet werden:

- `MediaSourceAdmin.razor` Button `Komplettscan aller Quellen`.
- `MediaSourceExplorer.razor` Button `Neu erfassen`.

Auch diese manuellen Pfade muessen fuer Restore blockiert oder koordiniert werden.

## MediaSourceClassifier

`MediaSourceClassifier` ist scoped und schreibt intensiv in den DbContext.

Wichtige Schreibbereiche:

- `Movies`, `MovieCollections`, `MovieMediaItems`
- `TVShows`, `TVShowSeasons`, `TVShowEpisodes`, `TVShowEpisodeMediaItems`
- `Pictures`
- `Genres`, `MovieGenres`, `TVShowGenres`
- Statusfelder auf `MediaItems` und `MediaCollections`

Es gibt einen statischen In-Process-Schutz gegen parallele Klassifizierungen (`_classificationRunning`) und eine Queue fuer Collection-Tree-Klassifizierungen. Dieser Schutz pausiert aber keine bereits laufende Klassifizierung fuer Restore.

## ContinueWatchingWorker

`ContinueWatchingWorker` erbt von `BackgroundService`.

Aufgaben:

- Liest Eintraege aus `ContinueWatchingBuffer`.
- Erstellt DI-Scope.
- Ruft `ContinueWatchingService.ProcessBufferedEntryAsync(...)` auf.
- Schreibt dadurch Continue-Watching-Daten in die Datenbank.

Auch dieser Worker kann waehrend Restore Daten veraendern.

## UdpDiscoveryListener

Der UDP-Listener beantwortet Discovery-Anfragen und schreibt nicht in die Datenbank. Fuer Restore muss er vermutlich nicht pausiert werden, ist aber aktuell nicht als Hosted Service registriert und wird nicht automatisch sauber ueber DI gesteuert.

## Integrationsbedarf fuer Restore

Fuer die Anforderung "Hintergrundprozesse beendet oder angehalten" fehlt eine Infrastruktur. Sinnvolle Erweiterung:

- Ein Singleton-Koordinator, z. B. `IBackgroundProcessingGate`.
- Hosted Services und manuelle Admin-Scan-Aktionen pruefen den Gate-Status vor Start und an sinnvollen Punkten.
- Restore setzt den Gate auf pausiert, wartet auf laufende Operationen oder bricht sie kooperativ ab.
- Danach fuehrt Restore Datenloeschung und Import aus.
- Im `finally` wird der Gate wieder freigegeben.

Ohne diese Koordination besteht ein reales Risiko, dass Scanner/Klassifizierung/ContinueWatching parallel zum Restore speichern.
