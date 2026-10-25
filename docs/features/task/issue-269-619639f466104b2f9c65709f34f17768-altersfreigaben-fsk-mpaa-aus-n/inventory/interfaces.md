# Interfaces — Bestandsaufnahme

**Befund:** Es existiert kein Interface für eine Altersfreigabe- oder Metadaten-Online-Quelle (kein `ITmdb*`, `IAgeRating*` o. ä.).

## `IMediaSourceReader`
Datei: `VideoWebPlayer/Services/IMediaSourceReader.cs`

Abstraktion des Dateizugriffs auf eine Medienquelle (lokal via `LocalMediaSourceReader`, SFTP via `SftpMediaSourceReader`, Vermittlung via `MediaSourceReaderDispatcher`). Über dieses Interface liest der `MediaSourceClassifier` die NFO-Dateien.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReadRootDirectory` | `MediaSource` | `IEnumerable<MediaEntry>` | Root-Collection der Quelle |
| `ReadDirectoryEntries` | `MediaCollection` | `IEnumerable<MediaEntry>` | Direkte Untereinträge einer Collection |
| `FileExistsAsync` | `MediaCollection`, `string fileName` | `Task<bool>` | Existenzprüfung (NFO-Checks im Scan) |
| `ReadFileAsync` | `MediaCollection`, `string fileName` | `Task<string?>` | Dateiinhalt (NFO-Lesung im Scan und Backfill) |
| `ReadFileStreamAsync` | `MediaCollection`, `string fileName` | `Task<Stream?>` | Dateistream (Bilder) |
| `OpenFileStream` | `MediaCollection`, `string fileName` | `Stream?` | Stream für Datei der Quelle |

## `IMediaMetadataWriteCoordinator`
Datei: `VideoWebPlayer/Services/IMediaMetadataWriteCoordinator.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnterAsync` | `CancellationToken` | `Task<IAsyncDisposable>` | Exklusive Metadaten-Schreibsektion; serialisiert Scan- und Editor-Schreibzugriffe (Lease in `MediaSourceClassifier` und `MediaMetadataEditorService`) |

## `IUnlockedMediaService`
Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs`

Freischaltungs-/Zugriffsabfrage für Medieneinträge (benutzerbezogene Einzelfreischaltungen; `GetUnlockedMovieCollectionIdsForUserAsync`, `GetUnlockedTVShowIdsForUserAsync`, `IsUnlockedAsync(DtoMediaEntry)`). Relevant im Hinblick auf perspektivische Freigaberegeln pro Benutzer (Anforderung: „später filterbar … Jugendschutz-/Freigaberegeln") — heutiger Mechanismus für eintragsweise Sichtbarkeit.

## `IBackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups` (Implementierung `BackgroundProcessingGate`)

Gate zur Koordination von Hintergrundoperationen (`EnterOperationAsync(name, ct)`); genutzt von `MediaSourceScanService`, `ActorBackfillWorker` und `MediaMetadataEditorService`, damit Scan/Editor/Backup nicht gleichzeitig schreiben.

## `IBackupData` / `IBackupDataSource` / `IBackupDataFactory`
Paket `msTools.Backup`; implementiert durch `VideoWebPlayerBackupData` bzw. `VideoWebPlayerBackupDataSource`/`VideoWebPlayerBackupDataFactory` — Rahmen für den Alt-Backup-Regressionstest der neuen Spalten.
