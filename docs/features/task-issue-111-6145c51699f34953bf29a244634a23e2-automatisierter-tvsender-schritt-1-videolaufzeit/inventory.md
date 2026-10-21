# Bestandsaufnahme: Videolaufzeit erfassen

Analyse des Medien-Scan/Metadaten-Bereichs, der Datenhaltung/Backup-Schicht und der Konfigurations-/Worker-Infrastruktur bezogen auf die Anforderung „Videolaufzeit per ffprobe erfassen" (Schritt 1 des Projekts Automatisierter TV-Sender).

## Zusammenfassung

- Es existiert **kein** Laufzeit-/Duration-Feld an `MediaItem`, `Movie` oder `TVShowEpisode`. Einzig `ContinueWatchingEntry` hat `TimeSpan Position` / `TimeSpan? Duration` (Wiedergabeposition des Benutzers, nicht Datei-Laufzeit).
- `MediaItem` ist die Datei-Entität (1 Zeile pro Datei) und wird über `MovieMediaItem`/`TVShowEpisodeMediaItem` mit den klassifizierten `Movie`/`TVShowEpisode`-Entitäten verknüpft — die Laufzeit gehört fachlich zur Datei, also zu `MediaItem`.
- Der Scan läuft über `MediaSourceScanner` (legt `MediaItem`s an, erkennt Dateiwechsel über `CreatedAt`) und `MediaSourceClassifier` (ordnet Dateien mit Video-Endungen `.mp4`/`.avi`/`.mkv`/`.mpeg` + NFO als `Movie`/`TVShowEpisode` zu). Angetrieben wird das vom `MediaSourceScanService` (BackgroundService, Intervall aus `ProgramSettingsService.GetScanIntervalsAsync`).
- Es gibt **keine** ffmpeg/ffprobe-Anbindung und kein eingebettetes Medien-Toolkit im Repo.
- Dateizugriff ist über `IMediaSourceReader` abstrahiert: `LocalMediaSourceReader` (System.IO) und `SftpMediaSourceReader` (SSH.NET, `OpenFileStream` liefert streamenden `SftpStreamWrapper`). Für Remote-Dateien steht ein Stream-Zugriff zur Verfügung (ffprobe kann via `pipe:0` aus stdin lesen).
- Backfill-Muster existiert: `ActorBackfillWorker` (BackgroundService, einmalig beim Start, `IBackgroundProcessingGate`-Lease) und `PlaylistBackfillWorker` (Signal + Sicherheitslauf). Marker-Konvention: `*ClassifiedAt`-Zeitstempelspalten.
- Backup-Mechanismus: `VideoWebPlayerBackupData` mit `OptionalRestoreColumns` (fehlende Spalten in Alt-Backups toleriert, ohne Default-Registrierung → `null`) und `OptionalRestoreTables`; Regressionstests per `LegacyBackupArchiveBuilder.RemoveColumnsAsync` sind etabliert.
- Konfiguration: `services.Configure<T>` + `IOptions` für appsettings-Abschnitte (`Playlists`, `Mdns`, `Discovery`, `EpisodeBackgroundImage`); DB-persistente Einstellungen über `Setup`-Entität + `ProgramSettingsService`.
- Tests laufen gegen echtes SQLite (`SqliteConnection` shared in-memory); E2E per Playwright + `WebApplicationFactory`.

Test-Ausgangszustand: siehe [Tests](inventory/tests.md) — Basislauf `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` dokumentiert dort.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Konfiguration und Infrastruktur](inventory/config.md)
- [Externe Abhängigkeit ffprobe/ffmpeg](inventory/ffmpeg.md)
- [Tests](inventory/tests.md)
