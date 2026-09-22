# Übersetzte Anforderung: Lokales Verzeichnis als Medienquelle

## Ausgangs-Kundenanforderung

> Aktuell wird bei der Einrichtung einer Quelle erwartet, dass es sich um einen FTP-Server handelt. Es soll nun auch möglich sein, dass ein lokales Verzeichnis als Quelle angegeben werden kann.

## Fachliche Zusammenfassung

Medienquellen (`MediaSource`) sind heute faktisch auf entfernte SFTP-Server festgelegt: Das Datenmodell enthält nur verbindungsbezogene Felder (`Host`, `Port`, `Username`, `Password`), das Admin-Formular fragt ausschließlich diese ab, und sämtliche Dateizugriffe laufen über `SftpMediaSourceReader` per `SftpClient` (Renci.SshNet). Hinweis: Die Anforderung spricht von „FTP“, die tatsächlich implementierte Fernquelle ist SFTP; die Dokumentation (`README.md`, `docs/help/einrichtung.md`) bewirbt lokale Quellen bereits, ohne dass sie implementiert sind.

Die Anforderung erweitert `MediaSource` um einen Quelltyp, sodass beim Anlegen/Bearbeiten einer Quelle alternativ ein lokales Verzeichnis (Dateisystempfad auf dem Server, auf dem der VideoWebPlayer läuft) angegeben werden kann. Alle nachgelagerten Prozesse — Root-/Collection-Scan (`MediaSourceScanner`), Klassifizierung inkl. NFO-/Bild-/Schauspielerdateien (`MediaSourceClassifier`) sowie Video-Streaming und Download (`ItemsController`) — müssen den Quelltyp berücksichtigen und für lokale Quellen über `System.IO` lesen statt über SFTP. Die Auswahl des Quelltyps in der Admin-Oberfläche (`MediaSourceAdminDetails.razor`) ist ein eigener Fachpunkt: Der Anwender wählt explizit zwischen „SFTP-Server" und „Lokales Verzeichnis" und bekommt je nach Typ nur die relevanten Felder angezeigt.

## Betroffene Klassen und Komponenten

### Datenmodell

- `VideoWebPlayer/Data/MediaSource.cs` — neue Eigenschaft zur Unterscheidung des Quelltyps, z. B. `SourceType` (Enum) oder gleichwertige Diskriminator-Eigenschaft. `Path` (geerbt von `MediaEntry`) wird bei lokalen Quellen als lokaler Root-Pfad verwendet; `Host`, `Port`, `Username`, `Password` sind dann irrelevant.
- Neues Enum, z. B. `MediaSourceType` mit den Werten `Sftp` und `LocalDirectory` (Name als Vorschlag; Default `Sftp`, damit bestehende Datensätze unverändert funktionieren).
- Neue EF-Core-Migration unter `VideoWebPlayer/Migrations/` für die zusätzliche Spalte.
- `VideoWebPlayer/Data/MediaSourceExtensions.cs` — `Update(this MediaSource, MediaSource)` muss die neue Typ-Eigenschaft mitkopieren.
- `VideoWebPlayer.Client/Models/DtoSource.cs` (`DtoMediaSource`) — ggf. um den Quelltyp erweitern, falls API-Clients ihn benötigen (z. B. zur Anzeige).

### Logikklassen / Services

- `VideoWebPlayer/Services/SftpMediaSourceReader.cs` — ist heute der einzige Dateizugriff auf Quellen und wird von allen Konsumenten direkt als konkrete Klasse injiziert. Er bietet die virtuellen Methoden `ReadRootDirectory`, `ReadDirectoryEntries`, `ReadSubtree`, `FileExistsAsync`, `ReadFileAsync`, `ReadFileStreamAsync` sowie die nicht-virtuelle Methode `GetSftpFileStream` (Rückgabetyp `SftpStreamWrapper`).
- Neu: `LocalMediaSourceReader` (Name als Vorschlag) — liest Verzeichnisse/Dateien per `Directory.EnumerateFileSystemEntries`, `File.Exists`, `File.ReadAllText`/`FileStream` und bildet sie analog auf `MediaCollection`/`MediaItem` ab (inkl. `CreatedAt` aus `File.GetLastWriteTimeUtc` o. ä. und dem Ignorieren von Einträgen mit führendem `.`, analog `IsIgnoredEntry`).
- `VideoWebPlayer/Services/MediaSourceScanner.cs` — ruft `_sftpReader.ReadRootDirectory(source)` (Zeile 68) und `_sftpReader.ReadDirectoryEntries(next)` (Zeile 211) auf und fängt nur `SftpPathNotFoundException`/`SftpPermissionDeniedException` (Zeile 213). Muss den Reader quelltypabhängig auflösen und lokale Fehler (`DirectoryNotFoundException`, `UnauthorizedAccessException`, `IOException`) entsprechend behandeln.
- `VideoWebPlayer/Services/MediaSourceClassifier.cs` — nutzt `_sftpReader` an ca. 16 Stellen (`FileExistsAsync`, `ReadFileAsync`, `ReadFileStreamAsync`) für `tvshow.nfo`, Episoden-/Film-NFOs, Poster/Banner/Fanart/Thumbs und Schauspielerbilder (Zeilen 444, 451, 578, 583, 775, 780, 994, 1017, 1099, 1174, 1243, 1500, 1704, 1706).
- `VideoWebPlayer/Controllers/ItemsController.cs` — `StreamMediaItem` (Zeile 442) und damit indirekt `Download` (Zeile 485) holen den Videostream über `_sftpReader.GetSftpFileStream(...)`. Für lokale Quellen ist ein `FileStream` auf den kombinierten Pfad (`collection.Path` + Dateiname) zu liefern; `enableRangeProcessing: true` bleibt bestehen.
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` — Registrierung in Zeile 232; neue Reader-Implementierung und ggf. eine Reader-Auswahl (Factory/Dispatcher) registrieren.

### Interfaces

- Es existiert aktuell kein Interface für den Quellenzugriff — `SftpMediaSourceReader` wird überall konkret injiziert und per `virtual`/Ableitung in Tests gefakt. Für die typspezifische Auswahl ist eine Abstraktion sinnvoll, z. B. ein Interface `IMediaSourceReader` (mit den o. g. Methoden, Stream-Rückgabe verallgemeinert auf `Stream`) plus einer Auswahl-Komponente (z. B. `IMediaSourceReaderFactory`/`MediaSourceReaderResolver`), die anhand von `MediaSource.SourceType` den passenden Reader liefert. Alternative mit geringerem Eingriff: `SftpMediaSourceReader` behalten und pro Aufruf den Quelltyp prüfen — erscheint aber ungünstig, da die Klasse SFTP-spezifische Verbindungslogik kapselt.

### Enums

- Neu: Quelltyp-Enum (siehe Datenmodell).

### UI-Komponenten

- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor` — Auswahl des Quelltyps (z. B. `InputSelect`) beim Anlegen/Bearbeiten; bei „Lokales Verzeichnis" nur `Name`, `Path` (lokaler Pfad), Icon und Benutzerfreigaben anzeigen; `Host`, `Port`, `Username`, `Password` ausblenden bzw. nicht befüllen. Serverseitige Validierung des Pfads (nicht leer, existierendes Verzeichnis) beim Speichern.
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor` — Übersichtstabelle zeigt aktuell die Spalten `Host`, `Port`, `Pfad`, `Benutzername`; für lokale Quellen Typ-Kennzeichnung ergänzen bzw. Spalten typabhängig darstellen.
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor` — arbeitet nur auf bereits gescannten Datenbankeinträgen; voraussichtlich keine Änderung nötig.
- Benutzerseitige Anzeige (`MediaSourceDetails.razor`, `MediaSourceDetailsViewModel`) — voraussichtlich unverändert; nur relevant, falls `DtoMediaSource` um den Typ erweitert wird.

### Tests (`VideoWebPlayer.Tests`)

- Bestehende Test-Helpers (`Helpers/FakeSftpMediaSourceReader.cs`, `ThrowingSftpMediaSourceReader.cs`, `SeriesSftpMediaSourceReader.cs`, `BackfillSftpMediaSourceReader.cs`) leiten von `SftpMediaSourceReader` ab — bei Einführung eines Interfaces sind diese anzupassen.
- Neue Tests für den lokalen Reader mit temporären Verzeichnissen (Scan, NFO-Erkennung, Streaming-Pfadauflösung) sowie für die Reader-Auswahl nach `SourceType`.
- Bestehende Suites, die den Reader nutzen: `MediaSourceScannerTests`, `MediaSourceScanServiceTests`, `MediaSourceClassifierBackgroundImageTests`, `MediaSourceClassifierActorBackfillTests`, `ItemsControllerMetadataTests`, `Controllers/ItemsControllerAccessTests`, `Controllers/SourceVisibilityControllerTests`.

## Implementierungsansatz

1. **Datenmodell:** `MediaSource` um `SourceType` (Enum, Default `Sftp`) erweitern; Migration erzeugen; `MediaSourceExtensions.Update` anpassen. Für Bestandsdaten ergibt der Default automatisch das bisherige Verhalten.
2. **Abstraktion des Quellenzugriffs:** Gemeinsame Schnittstelle (z. B. `IMediaSourceReader`) aus den heute genutzten Methoden von `SftpMediaSourceReader` extrahieren und die Streaming-Methode auf `Stream` verallgemeinern (lokal: `FileStream`, SFTP: weiterhin `SftpStreamWrapper`). `SftpMediaSourceReader` implementiert das Interface; ein neuer `LocalMediaSourceReader` implementiert es mit `System.IO`-Zugriffen. Die Pfadkombination erfolgt lokal über `Path.Combine`/`Path.GetFullPath` statt des SFTP-spezifischen `CombineSftpPath` (`/`-Separator).
3. **Reader-Auswahl:** Eine kleine Dispatch-Komponente (Factory/Resolver) wählt anhand von `MediaSource.SourceType` den Reader; `MediaSourceScanner`, `MediaSourceClassifier` und `ItemsController` werden darauf umgestellt. Registrierung in `ServiceCollectionExtensions`.
4. **Fehlerbehandlung:** Die SFTP-spezifische Exception-Filterung in `MediaSourceScanner.ScanMediaCollectionInternalAsync` (Zeile 213) um lokale Äquivalente erweitern bzw. auf ein gemeinsames Fehlerkonzept abbilden.
5. **Admin-UI:** Typauswahl in `MediaSourceAdminDetails.razor` ergänzen (eigener Fachpunkt: explizite Auswahl, keine implizite Erkennung über leere Felder), Felder typabhängig ein-/ausblenden, Pfad validieren (`Directory.Exists`), ggf. Typ in der Übersicht (`MediaSourceAdmin.razor`) anzeigen.
6. **Sicherheit bei lokalen Pfaden:** Dateizugriffe über relative Angaben (z. B. NFO-`<thumb>`-Pfade in `MediaSourceClassifier`, Zeile 1500) müssen normalisiert und auf das Quell-Rootverzeichnis beschränkt werden (Schutz vor Pfad-Traversal wie `..\..`), da andernfalls beliebige Dateien des Serverprozesses lesbar wären. Präzedenzfall für solche Prüfungen existiert in `VideoWebPlayerBackupFacade`/`VideoWebPlayerBackupData` (`IsSafeRelativePath`, Pfad-Präfix-Prüfung).
7. **Events/Hooks:** `AddMediaSourceAsync`/`UpdateMediaSourceAsync` publizieren `MediaSourceCreatedEvent`/`MediaSourceUpdatedEvent`; es gibt keine Typ-abhängigen Subscriber, die Anpassung beschränkt sich auf die neuen Felder. `MediaSourceScanService` (Background-Service) benötigt keine strukturelle Änderung, da er `MediaSourceScanner`/`MediaSourceClassifier` nutzt.

## Konfiguration

Der Quelltyp ist eine Eigenschaft pro Datensatz (`MediaSource`), keine globale Anwendungseinstellung. Mögliche Ergänzung (zu klären, siehe Offene Fragen): eine optionale serverseitige Einschränkung erlaubter Basisverzeichnisse für lokale Quellen in `appsettings` (z. B. `MediaSources:LocalRootPaths`), um den Dateisystemzugriff des Serverprozesses einzugrenzen.

## Offene Fragen

1. Die Anforderung nennt „FTP-Server“, implementiert ist jedoch SFTP (`SftpClient`, SSH.NET, Standard-Port 22). Bestätigung, dass „Quelle" hier die bestehende SFTP-Quelle meint — ein zusätzlicher echter FTP-Quelltyp ist nicht Teil dieser Anforderung (Annahme).
2. Soll der Quelltyp einer bestehenden Quelle nachträglich änderbar sein? Falls ja: sollen vorhandene `MediaCollections`/`MediaItems` dabei zurückgesetzt/neu gescannt werden (Annahme: Typwechsel erfordert Scan-Reset, da `Path`-Semantik wechselt)?
3. Sind unter Windows auch UNC-Pfade/Netzlaufwerke (`\\server\share`) als „lokales Verzeichnis" zulässig, oder nur lokale Laufwerkspfade?
4. Soll der Zugriff auf beliebige Serverpfade uneingeschränkt erlaubt sein, oder soll eine Whitelist/Basisverzeichnis-Konfiguration den lesbaren Bereich eingrenzen (siehe Konfiguration)?
5. Soll der Quelltyp über die API exponiert werden (`DtoMediaSource`), damit externe Clients (z. B. MAUI) ihn darstellen können?
6. Backup/Restore: Neue `SourceType`-Spalte sollte in `VideoWebPlayerBackupDataSource` automatisch mitserialisiert werden — zu verifizieren, dass ältere Backups ohne die Spalte weiterhin restorierbar sind.
