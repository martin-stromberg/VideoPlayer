# Logik – Bestandsaufnahme

Alle Dateizugriffe auf Medienquellen laufen ausschließlich über die konkrete Klasse `SftpMediaSourceReader` (Renci.SshNet). Es existiert kein lokaler Reader und keine Abstraktion/Auswahl nach Quelltyp.

## `SftpMediaSourceReader`
Datei: `VideoWebPlayer/Services/SftpMediaSourceReader.cs`

Einziger Dateizugriff auf Quellen; wird von `MediaSourceScanner`, `MediaSourceClassifier` und `ItemsController` als konkrete Klasse injiziert. Registrierung: `ServiceCollectionExtensions.cs` Zeile 232 (`services.AddScoped<SftpMediaSourceReader>()`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ReadRootDirectory(MediaSource)` | `public virtual` | Liefert nur die Root-`MediaCollection` aus `source.Path` (kein echter Verzeichniszugriff) |
| `ReadDirectoryEntries(MediaCollection)` | `public virtual` | Listet direkte Untereinträge via `SftpClient.ListDirectory`; ignoriert Einträge mit führendem `.` (`IsIgnoredEntry`, Zeile 293) |
| `ReadSubtree(MediaCollection)` | `public` (nicht virtuell) | Rekursives Auslesen via `ReadDirectoryInternal` |
| `FileExistsAsync(MediaCollection, string)` | `public virtual` | Dateiexistenz via `ListDirectory` + Namensvergleich (OrdinalIgnoreCase); fängt `SftpPathNotFoundException` |
| `ReadFileAsync(MediaCollection, string)` | `public virtual` | Liest Dateiinhalt als UTF-8-String, `null` wenn nicht vorhanden |
| `ReadFileStreamAsync(MediaCollection, string)` | `public virtual` | Lädt Datei komplett in `MemoryStream` (`client.DownloadFile`), `null` wenn nicht vorhanden |
| `GetSftpFileStream(MediaCollection, string)` | `public` (nicht virtuell) | Liefert `SftpStreamWrapper` mit offenem `SftpClient` (Streaming ohne Voll-Download) — einzige Methode für Video-Streaming |
| `IsIgnoredEntry(string)` | `private static` | `name.StartsWith('.')` |
| `CombineSftpPath(string, string)` | `private static` | Pfadverknüpfung mit `/`-Separator (SFTP-spezifisch) |

Publizierte/abonnierte Events: keine.

## `SftpStreamWrapper`
Datei: `VideoWebPlayer/Services/SftpStreamWrapper.cs`

`Stream`-Ableitung, die beim `Dispose` den gekapselten `SftpClient` mit entsorgt. Wird ausschließlich von `SftpMediaSourceReader.GetSftpFileStream` erzeugt und in `ItemsController.StreamMediaItem` (Zeile 442) konsumiert.

## `MediaSourceScanner`
Datei: `VideoWebPlayer/Services/MediaSourceScanner.cs`

Konstruktor-Injektion von `SftpMediaSourceReader` (Zeile 35). Verwendet `Renci.SshNet.Common` (Zeile 9) für die Exception-Filterung.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ScanAllSourcesAsync(CancellationToken)` | `public` | Root-Scan aller Quellen; ruft `_sftpReader.ReadRootDirectory(source)` (Zeile 68) |
| `ScanNextMediaCollection(CancellationToken)` | `public` | Scannt die älteste fällige Collection |
| `ScanMediaCollectionAsync(long, CancellationToken)` | `public` | Scannt eine bestimmte Collection |
| `ScanCollectionTreeAsync(long, CancellationToken)` | `public` | Scannt einen Teilbaum (Queue/BFS); von `MediaSourceExplorer.razor` (Zeile 260) aufgerufen |
| `ScanMediaCollectionInternalAsync(...)` | `private` | Kernlogik; `_sftpReader.ReadDirectoryEntries(next)` (Zeile 211); catch-Filter nur `SftpPathNotFoundException`/`SftpPermissionDeniedException` (Zeile 213) — lokale IO-Fehler werden nicht abgefangen |
| `UpdateParentClassifyableAsync(long?, CancellationToken)` | `private` | Propagiert `Classifyable` nach oben |

Events: keine direkt (DB-Schreibzugriffe indirekt über `ApplicationDbContext`).

## `MediaSourceClassifier`
Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs` (1744 Zeilen)

Konstruktor-Injektion von `SftpMediaSourceReader` (Zeile 61, Feld Zeile 26). Reader-Aufrufe: `FileExistsAsync` (Zeilen 444, 578, 775, 1017, 1099, 1174, 1243, 1704), `ReadFileAsync` (Zeilen 451, 583, 780, 1706), `ReadFileStreamAsync` (Zeilen 994, 1500).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ClassifyAllAsync(CancellationToken)` | `public` | Komplette Klassifizierung (Items + Collections) |
| `ClassifyMediaItemsAsync(CancellationToken)` | `public` | Nur Item-Klassifizierung |
| `ClassifyMediaCollectionsAsync(CancellationToken)` | `public` | Nur Collection-Klassifizierung |
| `ClassifyCollectionTreeAsync(long, CancellationToken)` | `public` | Klassifiziert Teilbaum; von `MediaSourceExplorer.razor` (Zeile 264) aufgerufen |
| `ReloadGenres(CancellationToken)` | `public` | Lädt Genres neu |
| `BackfillMissingActorsAsync(CancellationToken)` | `public` | Nacherfassung von Schauspielern aus NFOs |
| `CheckReloadGenres(CancellationToken)` | `internal` | Flag-gesteuertes Genre-Reload |
| `ProcessCollectionAsTVShowAsync` | `private` | `tvshow.nfo`-Erkennung (Zeilen 444/451) |
| `ProcessEpisodesForTVShowAsync` | `private` | Episoden-NFOs (Zeilen 578/583) |
| `ProcessCollectionAsMovieAsync` | `private` | Film-NFOs (Zeilen 775/780) |
| `GetOrCreatePicture` | `private` | Bilddaten via `ReadFileStreamAsync` (Zeile 994) |
| `AssignPicturesTo*` | `private` | Poster/Banner/Fanart/Thumbs via `FileExistsAsync` |
| `DownloadActorPictureAsync` | `private` | Schauspielerbilder; `thumb` kann HTTP-URL **oder relativer Pfad** sein (Zeile 1500 `ReadFileStreamAsync(collection, thumb)`) — relevant für Pfad-Traversal-Schutz bei lokalen Quellen |
| `TryReadActorNfoAsync` | `private` | Kandidaten-NFOs entlang der Collection-Kette (Zeilen 1704/1706) |

Statische Klassifizierungs-Queue: `_queuedCollectionTreeClassificationIds` etc. (Zeilen 35–38).

## `ItemsController`
Datei: `VideoWebPlayer/Controllers/ItemsController.cs`

Konstruktor-Injektion von `SftpMediaSourceReader` (Zeile 37, Feld Zeile 18).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StreamMediaItem(string, long)` | `public` (Route `GET api/items/{type}/{id}/stream`) | `_sftpReader.GetSftpFileStream(mediaCollection, fileName)` (Zeile 442); `File(stream, contentType, enableRangeProcessing: true)` |
| `Download(string, long)` | `public` (Route `GET .../download`) | Nutzt intern `StreamMediaItem` (Zeile 485) |
| `Get`, `GetRecent`, `GetGenres`, `UpdateMetadata` | `public` | Kein Reader-Zugriff |
| `FindEntry`/`EnsureAccessAsync`/`FindMediaItemAsync`/`IsUnlockedAsync` | `private` | DB-/Berechtigungslogik, kein Reader |

## `MediaSourceScanService`
Datei: `VideoWebPlayer/Services/MediaSourceScanService.cs`

`BackgroundService`; löst `MediaSourceScanner` und `MediaSourceClassifier` pro Scope auf (Zeilen 88–89). Kein direkter Reader-Zugriff — strukturell unverändert nutzbar.

## `ApplicationDbContext` (MediaSource-Methoden)
Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddMediaSourceAsync(MediaSource)` | `public` | Speichert Quelle, publiziert `MediaSourceCreatedEvent` (Zeile 165) |
| `UpdateMediaSourceAsync(MediaSource)` | `public` | Kopiert via `MediaSourceExtensions.Update`, publiziert `MediaSourceUpdatedEvent` (Zeile 179) |
| `DeleteMediaSourceAsync(...)` | `public` | Rekursives Löschen, publiziert `MediaSourceDeletedEvent` (Zeile 337) |

Subscriber: **keine** produktiven Subscriber für die `MediaSource*`-Events (nur `ApplicationDbContextTests` abonniert `MediaSourceDeletedEvent`, Zeile 444).

## `MediaSourceExtensions`
Datei: `VideoWebPlayer/Data/MediaSourceExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Update(this MediaSource, MediaSource)` | `public static` | Kopiert `Name`, `Path`, `Host`, `Port`, `Username`, `Password`, `IconPictureId`, `LastScannedAt`; **kein** Typfeld vorhanden |

## `ServiceCollectionExtensions`
Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`

Registrierung Zeile 232: `services.AddScoped<SftpMediaSourceReader>()` — einzige Reader-Registrierung; `MediaSourceScanner`/`MediaSourceClassifier` scoped (Zeilen 225–226).

## `VideoWebPlayerBackupData` (Präzedenzfall Pfad-Sicherheit)
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsSafeRelativePath(string)` | `private static` (Zeile 970) | Lehnt leere/rooted/`:`-haltige Pfade sowie `.`/`..`-Segmente ab — Vorlage für Traversal-Schutz bei lokalen Quellpfaden |

## `FileSystemDemoDataSetService`
Datei: `VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs`

Legt Demo-`MediaSource`-Datensätze an (`AddMediaSourceAsync`, Zeile 116) mit `Host`/`Port`/`Path`/`Username`/`Password` aus der Demo-Definition.
