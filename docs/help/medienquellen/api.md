← [Zurück zur Übersicht](index.md)

# Medienquellen — API

## Übersicht

Das Feature exponiert keine neuen HTTP-Endpunkte und erweitert das Client-DTO `DtoMediaSource` bewusst nicht (kein Consumer benötigt den Quelltyp). Die relevante Schnittstelle ist die interne Code-Abstraktion `IMediaSourceReader`, über die sämtlicher Dateizugriff auf Medienquellen läuft.

## Authentifizierung

Nicht relevant — interne Schnittstelle ohne HTTP-Exposure. Die bestehenden Endpunkte (`ItemsController.StreamMediaItem`, `Download`) behalten ihre Authentifizierung über `access_token` bzw. Authorization-Header unverändert bei.

## Rate Limiting

Nicht relevant.

## Endpunkte / Methoden

### `IMediaSourceReader` (`VideoWebPlayer/Services/IMediaSourceReader.cs`)

**Beschreibung:** Abstraktion des Dateizugriffs auf eine Medienquelle. Implementiert von `SftpMediaSourceReader`, `LocalMediaSourceReader` und `MediaSourceReaderDispatcher` (letzterer wählt anhand `MediaSource.SourceType` und ist als `IMediaSourceReader` im DI-Container registriert).

**Methoden:**

| Name | Signatur | Beschreibung |
|------|----------|--------------|
| `ReadRootDirectory` | `IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source)` | Liefert die Root-`MediaCollection` der Quelle |
| `ReadDirectoryEntries` | `IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection)` | Direkte Unterverzeichnisse (`MediaCollection`) und Dateien (`MediaItem`) einer Collection |
| `FileExistsAsync` | `Task<bool> FileExistsAsync(MediaCollection collection, string fileName)` | Existenzprüfung, Dateiname case-insensitiv (`OrdinalIgnoreCase`) |
| `ReadFileAsync` | `Task<string?> ReadFileAsync(MediaCollection collection, string fileName)` | Dateiinhalt als String (UTF-8) oder `null` |
| `ReadFileStreamAsync` | `Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName)` | Dateiinhalt als `Stream` oder `null` |
| `OpenFileStream` | `Stream? OpenFileStream(MediaCollection collection, string fileName)` | Seekbarer Dateistream für Streaming/Download (`FileStream` bzw. `SftpStreamWrapper`) oder `null` |

**Rückgabe-Konvention:** Alle Methoden behandeln „Datei existiert nicht", „Pfad außerhalb des Quell-Roots" und „Reparse Point" einheitlich als `false`/`null` — keine Exception an den Aufrufer.

**Fehler:**

| Fall | Verhalten |
|------|-----------|
| Datei/Verzeichnis nicht vorhanden | `null`/`false` bzw. leere Ergebnismenge |
| Pfad-Traversal außerhalb des Quell-Roots (nur `LocalMediaSourceReader`) | `null`/`false` |
| Reparse Point / Verzeichnis als Dateiziel (nur `LocalMediaSourceReader`) | `null`/`false` |
| `DirectoryNotFoundException`, `UnauthorizedAccessException`, `IOException` bei `ReadDirectoryEntries` | Exception an den Aufrufer — `MediaSourceScanner` fängt sie pro Collection ab und terminiert `ScanDueAt` neu |

## Betroffene bestehende Endpunkte

### `ItemsController.StreamMediaItem` / `ItemsController.Download`

Unveränderte Routen und Authentifizierung. Interner Unterschied: Der Videostream wird über `_reader.OpenFileStream(mediaCollection, fileName)` geholt — bei lokalen Quellen ein `FileStream`, bei SFTP weiterhin `SftpStreamWrapper`. `enableRangeProcessing: true` bleibt bestehen; `null` führt zu `NotFound`.
