# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Neue Klassen / Dateien

- [x] `MediaSourceType` (Enum, `VideoWebPlayer/Data/MediaSourceType.cs`) — angelegt mit `Sftp = 0`, `LocalDirectory = 1`
- [x] `IMediaSourceReader` (Interface, `VideoWebPlayer/Services/IMediaSourceReader.cs`) — angelegt mit allen sechs Methoden (`ReadRootDirectory`, `ReadDirectoryEntries`, `FileExistsAsync`, `ReadFileAsync`, `ReadFileStreamAsync`, `OpenFileStream` → `Stream?`)
- [x] `LocalMediaSourceReader` (Klasse, `VideoWebPlayer/Services/LocalMediaSourceReader.cs`) — angelegt, implementiert `IMediaSourceReader` per `System.IO`
- [x] `MediaSourceReaderDispatcher` (Klasse, `VideoWebPlayer/Services/MediaSourceReaderDispatcher.cs`) — angelegt, implementiert `IMediaSourceReader`, Konstruktor mit `SftpMediaSourceReader` + `LocalMediaSourceReader`, Routing anhand `SourceType` mit SFTP-Default bei `null`-Navigation
- [x] `AddMediaSourceSourceType` (EF-Core-Migration, `VideoWebPlayer/Migrations/20260922180232_AddMediaSourceSourceType.cs` + Designer) — Spalte `MediaSources.SourceType` (`int`, non-nullable, Default `0`); `ApplicationDbContextModelSnapshot` enthält `SourceType` (Zeile 737)

### Geänderte Klassen (Produktivcode)

- [x] Feld `SourceType` (`MediaSourceType`, Default `Sftp`) in `MediaSource` (`VideoWebPlayer/Data/MediaSource.cs`) — vorhanden
- [x] `MediaSourceExtensions.Update` kopiert `SourceType` (`VideoWebPlayer/Data/MediaSourceExtensions.cs`) — vorhanden
- [x] `SftpMediaSourceReader : IMediaSourceReader`; `GetSftpFileStream` → `OpenFileStream` mit Rückgabe `Stream?` (Rückgabewert weiterhin `SftpStreamWrapper`); `ReadSubtree` bleibt öffentlich, kein Interface-Member (`VideoWebPlayer/Services/SftpMediaSourceReader.cs`) — vorhanden
- [x] `MediaSourceScanner`: Konstruktor-Injection `IMediaSourceReader` (Feld `_reader`); `catch`-Filter in `ScanMediaCollectionInternalAsync` (Zeile 214) um `DirectoryNotFoundException`, `UnauthorizedAccessException`, `IOException` erweitert; `using Renci.SshNet.Common` erhalten — vorhanden
- [x] `MediaSourceClassifier`: Konstruktor-Injection `IMediaSourceReader` (Feld `_reader`), alle Aufrufstellen fachlich unverändert — vorhanden
- [x] `ItemsController`: Konstruktor-Injection `IMediaSourceReader` (Feld `_reader`); `StreamMediaItem` ruft `OpenFileStream(mediaCollection, fileName)` mit `fileName = Path.GetFileName(mediaItem.Path)` (Zeile 441–442) — vorhanden
- [x] `ServiceCollectionExtensions`: `SftpMediaSourceReader` scoped beibehalten, `LocalMediaSourceReader` scoped neu, `IMediaSourceReader` → `MediaSourceReaderDispatcher` scoped neu (Zeilen 232–234) — vorhanden
- [x] `VideoWebPlayerBackupData`: `MediaSources.SourceType` in `OptionalRestoreColumns` (Zeile 55) und `OptionalRestoreIntDefaults` mit Default `(int)MediaSourceType.Sftp` (Zeile 86) — vorhanden

### `LocalMediaSourceReader`-Details

- [x] `ReadRootDirectory`: Root-`MediaCollection` mit `Path = source.Path`, `Name` = letztes Pfadsegment via `DirectoryInfo` (Fallback `source.Path`), `CreatedAt = Directory.GetLastWriteTimeUtc` — vorhanden
- [x] `ReadDirectoryEntries`: `Directory.EnumerateFileSystemEntries`, `.`-Einträge ignoriert (`IsIgnoredEntry`), `ReparsePoint`s übersprungen, volle lokale Pfade, `CreatedAt` aus `GetLastWriteTimeUtc` — vorhanden
- [x] Traversal-Schutz: `Path.GetFullPath(Path.Combine(collection.Path, fileName))` + Präfixprüfung gegen normalisierten `MediaSource.Path`-Root, segmentweiser `OrdinalIgnoreCase`-Abgleich per Verzeichnis-Enumeration, `ReparsePoint`-Ablehnung (`ResolveFilePath`, Zeilen 141–225) — vorhanden
- [x] `OpenFileStream` liefert `FileStream` (`FileMode.Open`, `FileAccess.Read`, `FileShare.Read`), `null` bei fehlender Datei/Verlassen des Roots — vorhanden

### UI

- [x] `MediaSourceAdminDetails.razor`: `InputSelect` „Quelltyp" (id `source-type`, Optionen „SFTP-Server"/„Lokales Verzeichnis", `disabled` bei `!IsNew`); `Host`/`Port`/`Username`/`Password` nur bei `SourceType == Sftp` gerendert; `Path`-Label typabhängig („Pfad"/„Lokales Verzeichnis"); `OnInitializedAsync` kopiert `SourceType` in `editSource`; `Save()` validiert bei `LocalDirectory` (nicht leer, `Path.IsPathRooted`, `Directory.Exists(Path.GetFullPath(...))`) → `errorMessage` + Abbruch; SFTP-Felder werden neutralisiert (`Host = string.Empty`, `Port = 0`, `Username`/`Password = null`) — vorhanden
- [x] `MediaSourceAdmin.razor`: neue Spalte „Typ" („SFTP"/„Lokal"); bei lokalen Quellen `—` in `Host`/`Port`/`Benutzername` — vorhanden
- [x] `NavMenu.razor`: `OnInitializedAsync` ruft `EnsureAuthorizationTokenAsync`/`RequestSourcesAsync` nur bei `state.User.Identity?.IsAuthenticated == true` — vorhanden (preexisting-Defekt behoben)

### Tests

- [x] `MediaSourceScannerTests`: Registrierung auf `IMediaSourceReader` umgestellt (Zeile 33)
- [x] `MediaSourceScanServiceTests`: alle 5 Registrierungsstellen auf `IMediaSourceReader` (Zeilen 37, 106, 187–188, 284, 387)
- [x] `MediaSourceClassifierBackgroundImageTests`: `BuildServiceProvider` registriert `IMediaSourceReader` (Zeile 251)
- [x] `MediaSourceClassifierActorBackfillTests`: keine DI-Registrierung vorhanden — Fakes (`BackfillSftpMediaSourceReader`, `MissingPathSftpMediaSourceReader`) werden direkt an den `IMediaSourceReader`-Konstruktorparameter übergeben und kompilieren über die Vererbung von `SftpMediaSourceReader` unverändert; Datei musste nicht geändert werden
- [x] `LocalMediaSourceReaderTests` (neu): Root-Verzeichnis, Enumeration inkl. `.`-Ignorierung und `ReparsePoint`-Skip, `OrdinalIgnoreCase`-Abgleich, `ReadFileAsync`/`ReadFileStreamAsync`/`OpenFileStream`, Traversal-Fälle (`..\..`, absoluter Pfad außerhalb Root, Junction) — vorhanden
- [x] `MediaSourceReaderDispatcherTests` (neu): Routing `LocalDirectory`/`Sftp`/`null`-Navigation → SFTP-Default — vorhanden
- [x] `MediaSourceScannerLocalTests` (neu): Root-Collection-Anlage, Befüllung lokaler Verzeichnisse, fehlendes Verzeichnis → überspringen + `ScanDueAt` neu terminiert — vorhanden
- [x] `LocalMediaSourceClassifierTests` (neu): `tvshow.nfo`-Struktur → `TVShow`/`TVShowSeasons`/`TVShowEpisodes` — vorhanden
- [x] `ItemsControllerLocalSourceTests` (neu): `FileStreamResult` mit `FileStream`, Content-Type, Download, 401 ohne Freigabe — vorhanden
- [x] `ApplicationDbContextTests.Update_CopiesSourceType` + `MediaSource_DefaultsToSftpSourceType` — vorhanden
- [x] `VideoWebPlayerBackupDataTests.ReadFromAsync_LegacyBackupWithoutMediaSourceSourceType_RestoresWithSftpDefault` — vorhanden
- [x] `MediaSourceLocalDirectoryE2ETests` (neu, Playwright): Anlage lokaler Quelle, Typ-Kennzeichnung, ausgeblendete SFTP-Felder, Fehlermeldung bei nicht existierendem Pfad, deaktivierter Typ-Select im Edit-Modus — vorhanden
- [x] `LocalMediaSourcePipelineE2ETests` (neu, `WebApplicationFactory`): lokale Quelle wird im Host gescannt + klassifiziert, TVShow über API abrufbar — vorhanden
- [x] `LocalMediaSourceStreamingE2ETests` (neu, `WebApplicationFactory` + `access_token`): `stream`/`download` liefern 200 + Dateibytes, 401 ohne `MediaSourceUsers`-Freigabe — vorhanden

## Offene Aufgaben

Keine.

## Hinweise

- Die Implementierung liegt als uncommittete Änderungen plus untracked Dateien im Working Tree vor; die Prüfung erfolgte am tatsächlichen Dateiinhalt.
- `dotnet build VideoPlayer.sln` läuft fehlerfrei durch (0 Warnungen, 0 Fehler). Die Tests wurden im Rahmen des Reviews nicht ausgeführt — geprüft wurde die vorhandene Implementierung, nicht der Testlauf.
- `MediaSourceClassifierActorBackfillTests` enthält entgegen der Planformulierung keine DI-Registrierung, sondern direkte Konstruktoraufrufe — das ist gleichwertig erfüllt, da die Fakes über `SftpMediaSourceReader` das Interface erben und die geänderte Signatur akzeptieren.
