# Interfaces

## `IMediaSourceReader`
Datei: `VideoWebPlayer/Services/IMediaSourceReader.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReadRootDirectory` | `MediaSource` | `IEnumerable<MediaEntry>` | Root-Collection der Quelle |
| `ReadDirectoryEntries` | `MediaCollection` | `IEnumerable<MediaEntry>` | Direkte Untereinträge (Collections + Items) |
| `FileExistsAsync` | `MediaCollection`, `string` | `Task<bool>` | Existenzprüfung |
| `ReadFileAsync` | `MediaCollection`, `string` | `Task<string?>` | Dateiinhalt als Text |
| `ReadFileStreamAsync` | `MediaCollection`, `string` | `Task<Stream?>` | Dateiinhalt als Stream (SFTP: komplett in `MemoryStream` geladen!) |
| `OpenFileStream` | `MediaCollection`, `string` | `Stream?` | **Streamender** Dateizugriff (SFTP: `SftpStreamWrapper` über `client.OpenRead`, lokal `FileStream`) — geeignet für ffprobe-stdin-Streaming |

Für die Probe zu beachten: `ReadFileStreamAsync` lädt bei SFTP die komplette Datei in den Speicher — für Videodateien ungeeignet; `OpenFileStream` ist der richtige Einstiegspunkt.

## `IBackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnterOperationAsync` | `string` (Operationsname), `CancellationToken` | `Task<IAsyncDisposable?>` | Lease für Hintergrundoperation (wartet, während Backup-Restore läuft) |

## `IPlaylistBackfillSignal`
Datei: `VideoWebPlayer/Services/PlaylistBackfillSignal.cs`

Wecksignal für den Playlist-Backfill (`WaitAsync`, `BeginScan`-Scope) — Muster, falls der Probe-Worker künftig scan-getriggert statt intervallbasiert laufen soll; für Schritt 1 nicht erforderlich.

## `IBackupDataSource` / `IBackupDataFactory` (msTools.Backup)
Backup-Abstraktion; `VideoWebPlayerBackupData` implementiert `IBackupData` (`WriteToAsync`/`ReadFromAsync`).

## `IMediaMetadataWriteCoordinator`
Datei: `VideoWebPlayer/Services/IMediaMetadataWriteCoordinator.cs`

Serialisierungs-Lease für Metadaten-Schreibzugriffe (`EnterAsync`); vom Classifier verwendet. Für den Probe-Worker nicht zwingend (eigener Schreibpfad, kurze Saves), aber nutzbar, falls Schreib-Konflikte mit der Klassifizierung vermieden werden sollen.
