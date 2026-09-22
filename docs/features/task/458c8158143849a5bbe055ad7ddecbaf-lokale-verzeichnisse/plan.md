# Umsetzungsplan: Lokales Verzeichnis als Medienquelle

## Übersicht

`MediaSource` wird um einen Quelltyp (`MediaSourceType`: `Sftp`/`LocalDirectory`) erweitert. Der Dateizugriff wird hinter ein Interface `IMediaSourceReader` abstrahiert; neben `SftpMediaSourceReader` kommt ein `LocalMediaSourceReader` (System.IO) hinzu, ein `MediaSourceReaderDispatcher` wählt pro Aufruf anhand von `SourceType`. `MediaSourceScanner`, `MediaSourceClassifier` und `ItemsController` werden auf die Abstraktion umgestellt; die Admin-UI erhält eine explizite Typauswahl mit typabhängigen Feldern und Pfadvalidierung. Zusätzlich wird die Backup-Restore-Kompatibilität für die neue Spalte sichergestellt.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Quellenzugriff-Abstraktion | Interface `IMediaSourceReader` (Services) mit den heute konsumierten Methoden plus `OpenFileStream` (Rückgabe `Stream?`); `SftpMediaSourceReader` und `LocalMediaSourceReader` implementieren es | Requirement empfiehlt Abstraktion; Konsumenten bleiben von Verbindungsdetails frei; entspricht Gateway-/Strategy-Muster |
| Reader-Auswahl | `MediaSourceReaderDispatcher` implementiert selbst `IMediaSourceReader` und leitet jeden Aufruf anhand von `collection.MediaSource.SourceType` (bzw. `source.SourceType`) an den passenden konkreten Reader weiter; DI: `IMediaSourceReader` → Dispatcher | Alle bestehenden Aufrufstellen übergeben bereits `MediaSource`/`MediaCollection` (inkl. `MediaSource`-Navigation) — kein zusätzlicher Resolve-Schritt an ~18 Aufrufstellen; Tests können einen Fake direkt als `IMediaSourceReader` registrieren und den Dispatcher umgehen; Alternative „Factory pro Aufruf" hätte jede Aufrufstelle geändert |
| Streaming-Methode | `SftpMediaSourceReader.GetSftpFileStream` wird zu `OpenFileStream(MediaCollection, string)` mit Rückgabetyp `Stream?` umbenannt/verallgemeinert (Interface-Member); `SftpStreamWrapper` bleibt intern | Einziger Aufrufer ist `ItemsController.StreamMediaItem`; `FileStream` (lokal) und `SftpStreamWrapper` (SFTP) sind beide `Stream`; `enableRangeProcessing: true` bleibt unverändert |
| `ReadSubtree` | Bleibt öffentlich auf `SftpMediaSourceReader`, wird **nicht** ins Interface übernommen | Kein einziger Aufrufer im Repo (nur Definition gefunden) — keine lokale Implementierung nötig |
| Pfadauflösung & Traversal-Schutz (lokal) | `LocalMediaSourceReader` löst jede Dateiangabe per `Path.GetFullPath(Path.Combine(collection.Path, name))` auf und prüft, dass das Ergebnis innerhalb des normalisierten Quell-Roots (`MediaSource.Path`) liegt; Verzeichnis/ReparsePoint-Einträge (`FileAttributes.ReparsePoint`) werden beim Enumerieren und Öffnen übersprungen | NFO-`<thumb>`-Pfade (Classifier, Zeile 1500) und DB-Pfade können relative Segmente enthalten; Schutz analog `VideoWebPlayerBackupData.IsSafeRelativePath` (Zeile 970); Symlinks/Junctions könnten sonst die Root-Prüfung umgehen |
| Dateinamen-Abgleich (lokal) | Existenz-/Lesezugriffe enumerieren das Verzeichnis und vergleichen `OrdinalIgnoreCase` | Gleiche Semantik wie `SftpMediaSourceReader.FileExistsAsync`/`ReadFileAsync` (OrdinalIgnoreCase); `File.Exists` wäre auf Linux case-sensitiv |
| Quelltyp im Edit-Dialog | `SourceType` ist nur beim Anlegen wählbar; beim Bearbeiten ist die Auswahl deaktiviert | Ein Typwechsel ändert die `Path`-Semantik aller bereits gescannten `MediaCollections`/`MediaItems`; der Scanner löscht verwaiste Einträge nicht — Sperrung verhindert inkonsistente Bestände. Ein Typwechsel bestehender Quellen ist bewusst nicht Teil dieses Features (ggf. separates Feature) |
| `DtoMediaSource` | **Kein** `SourceType` im DTO (entschieden) | Kein Consumer (`NavMenu`, `MediaSourceDetailsViewModel`, `SourcesController`) benötigt den Typ; das reflektionsbasierte `ApiBaseController.Create<T>` (Zeile 75) kopiert nur typgleiche Properties — ein Server-Enum ließe sich nicht auf ein Client-Enum mappen |
| Zulässige lokale Pfade | UNC-Pfade/Netzlaufwerke (`\\server\share`) sind als lokale Quelle zulässig; keine gesonderte Einschränkung | Aus Sicht des Serverprozesses sind UNC-Pfade lokale Verzeichnisse; `Path.GetFullPath`/`Directory.Exists` unterstützen UNC nativ. Eine Whitelist erlaubter Basisverzeichnisse (`MediaSources:LocalRootPaths`) wird nicht umgesetzt — der Root-Präfix-Schutz im `LocalMediaSourceReader` begrenzt Zugriffe bereits auf die konfigurierte Quelle |
| Enum-Persistenz | `SourceType` als `int`-Spalte (EF-Standardmapping), Default `0` = `Sftp` | Bestandsdaten erhalten automatisch SFTP-Verhalten; `int` passt zum bestehenden `OptionalRestoreIntDefaults`-Mechanismus der Backup-Restore-Validierung |

## Programmabläufe

### Lokale Quelle über die Admin-UI anlegen

1. Admin öffnet `/admin/mediasources/new` → `MediaSourceAdminDetails.razor` initialisiert `editSource = new MediaSource { Port = 22, SourceType = MediaSourceType.Sftp }`.
2. Der Admin wählt im neuen Typ-Select (`InputSelect` auf `editSource.SourceType`, Muster wie `InputSelect` in `Register.razor` Zeile 50) „Lokales Verzeichnis".
3. Das Formular zeigt typabhängig: bei `LocalDirectory` nur `Name`, `Icon`, `Path` (Label „Lokaler Pfad/Verzeichnis"), `Freigegebene Benutzer`; `Host`, `Port`, `Username`, `Password` werden nicht gerendert. Bei `Sftp` bleibt das bisherige Layout.
4. `Save()` validiert bei `LocalDirectory`: `Path` nicht leer, absoluter Pfad, `Directory.Exists(Path.GetFullPath(...))` == true — bei Fehler `errorMessage` setzen und abbrechen (bestehendes `.alert-danger`-Muster).
5. Bei `LocalDirectory` werden vor dem Speichern `Host = string.Empty`, `Port = 0`, `Username = null`, `Password = null` gesetzt (DB-Spalte `Host` ist non-nullable).
6. `DbContext.AddMediaSourceAsync` persistiert inkl. `SourceType` und publiziert `MediaSourceCreatedEvent`; `MediaSourceUsers`-Zuordnung wie bisher.

Beteiligte Klassen/Komponenten: `MediaSourceAdminDetails.razor`, `ApplicationDbContext.AddMediaSourceAsync`, `MediaSource`, `MediaSourceType`.

### Quelle bearbeiten

1. `OnInitializedAsync` lädt die Quelle und kopiert jetzt auch `SourceType` in `editSource`.
2. Typ-Select ist deaktiviert (`disabled`), Felder rendern passend zum gespeicherten Typ.
3. Bei `LocalDirectory` gilt dieselbe Pfadvalidierung wie beim Anlegen; bei `Sftp` unverändertes Verhalten.
4. `DbContext.UpdateMediaSourceAsync` → `MediaSourceExtensions.Update` kopiert neu auch `SourceType` (sicherheitsrelevant konsistent, obwohl UI den Typ nicht änderbar macht).

Beteiligte Klassen/Komponenten: `MediaSourceAdminDetails.razor`, `ApplicationDbContext.UpdateMediaSourceAsync`, `MediaSourceExtensions.Update`.

### Scan einer Quelle (Root- und Collection-Scan)

1. `MediaSourceScanner.ScanAllSourcesAsync` ruft `_reader.ReadRootDirectory(source)` auf — der injizierte `MediaSourceReaderDispatcher` wählt anhand `source.SourceType` den konkreten Reader.
2. `LocalMediaSourceReader.ReadRootDirectory` liefert eine Root-`MediaCollection` mit `Path = source.Path`, `Name` = letztes Pfadsegment (`new DirectoryInfo(...)`, Fallback: `source.Path`), `CreatedAt` = `Directory.GetLastWriteTimeUtc` (nicht `DateTime.UtcNow`, damit die Änderungserkennung `existing.CreatedAt != rootEntry.CreatedAt` real funktioniert; fehlendes Verzeichnis liefert den stabilen Sentinel 1601-01-01).
3. `ScanMediaCollectionInternalAsync` ruft `_reader.ReadDirectoryEntries(next)`; `LocalMediaSourceReader` enumeriert per `Directory.EnumerateFileSystemEntries`, ignoriert `.`-Einträge und `ReparsePoint`s, bildet `MediaCollection`/`MediaItem` mit vollem lokalem Pfad und `CreatedAt` aus `GetLastWriteTimeUtc`.
4. Der `catch`-Filter wird um `DirectoryNotFoundException`, `UnauthorizedAccessException` und `IOException` erweitert → gleiches „überspringen + ScanDueAt neu terminieren"-Verhalten wie bei SFTP-Fehlern.

Beteiligte Klassen/Komponenten: `MediaSourceScanner`, `MediaSourceReaderDispatcher`, `LocalMediaSourceReader`, `SftpMediaSourceReader`.

### Klassifizierung einer lokalen Quelle

1. `MediaSourceClassifier` injiziert `IMediaSourceReader` statt `SftpMediaSourceReader`; alle ~16 Aufrufstellen (`FileExistsAsync`, `ReadFileAsync`, `ReadFileStreamAsync` für `tvshow.nfo`, Episoden-/Film-NFOs, Poster/Banner/Fanart/Thumbs, Schauspieler-NFOs) bleiben unverändert.
2. `LocalMediaSourceReader` löst Dateinamen relativ zu `collection.Path` auf (Enumerations-Vergleich `OrdinalIgnoreCase`), inkl. relativer Unterpfade wie `.actors/Name.jpg` aus NFO-`<thumb>`-Elementen — beschränkt auf das Quell-Root (`Path.GetFullPath`-Präfixprüfung, `ReparsePoint`-Ablehnung).

Beteiligte Klassen/Komponenten: `MediaSourceClassifier`, `MediaSourceReaderDispatcher`, `LocalMediaSourceReader`.

### Streaming und Download aus einer lokalen Quelle

1. `ItemsController.StreamMediaItem` lädt `mediaCollection` inkl. `MediaSource` (vorhanden, Zeile 437–439), ermittelt `fileName = Path.GetFileName(mediaItem.Path)` (verarbeitet `/` und `\` unter Windows) und ruft `_reader.OpenFileStream(mediaCollection, fileName)`.
2. Der Dispatcher wählt `LocalMediaSourceReader`, der `Path.GetFullPath(Path.Combine(collection.Path, fileName))` gegen den Quell-Root prüft und einen `FileStream` (`FileMode.Open`, `FileAccess.Read`, `FileShare.Read`) liefert; `null` bei fehlender Datei oder Verlassen des Roots.
3. `File(stream, contentType, enableRangeProcessing: true)` und `Download` bleiben unverändert.

Beteiligte Klassen/Komponenten: `ItemsController`, `MediaSourceReaderDispatcher`, `LocalMediaSourceReader`.

### Restore eines Alt-Backups (ohne `SourceType`-Spalte)

1. `VideoWebPlayerBackupData` serialisiert `MediaSources` automatisch über `_db.Model.GetEntityTypes()` — die neue Spalte ist im Export automatisch enthalten.
2. Damit ältere Backups ohne `SourceType` weiter restorierbar sind, wird `MediaSources.SourceType` in `OptionalRestoreColumns` eingetragen und in `OptionalRestoreIntDefaults` mit Default `0` (`Sftp`) versehen — bestehender Mechanismus (Zeilen 33–85).

Beteiligte Klassen/Komponenten: `VideoWebPlayerBackupData`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `MediaSourceType` (`VideoWebPlayer/Data/MediaSourceType.cs`, neue Datei — `MediaSource.cs` ist Windows-1252-kodiert) | Enum | `Sftp = 0` (Default für Bestandsdaten), `LocalDirectory = 1` |
| `IMediaSourceReader` (`VideoWebPlayer/Services/IMediaSourceReader.cs`) | Interface | `ReadRootDirectory(MediaSource)`, `ReadDirectoryEntries(MediaCollection)`, `FileExistsAsync(MediaCollection, string)`, `ReadFileAsync(MediaCollection, string)`, `ReadFileStreamAsync(MediaCollection, string)`, `OpenFileStream(MediaCollection, string)` → `Stream?` |
| `LocalMediaSourceReader` (`VideoWebPlayer/Services/LocalMediaSourceReader.cs`) | Klasse | Implementiert `IMediaSourceReader` per `System.IO` (Enumeration, `OrdinalIgnoreCase`-Namensabgleich, `FileStream`-Streaming); Root-Präfixprüfung + `ReparsePoint`-Ablehnung als Traversal-Schutz |
| `MediaSourceReaderDispatcher` (`VideoWebPlayer/Services/MediaSourceReaderDispatcher.cs`) | Klasse | Implementiert `IMediaSourceReader`; Konstruktor erhält `SftpMediaSourceReader` + `LocalMediaSourceReader`, wählt pro Aufruf anhand `MediaSource.SourceType` (Default bei `null`-Navigation: SFTP) |
| `AddMediaSourceSourceType` (`VideoWebPlayer/Migrations/`) | EF-Core-Migration | Neue Spalte `MediaSources.SourceType` (`int`, non-nullable, Default `0`) |

## Änderungen an bestehenden Klassen

### `MediaSource` (Datenmodellklasse)

- **Neue Eigenschaften:** `SourceType` (`MediaSourceType`, Default `MediaSourceType.Sftp`) — Diskriminator für die Reader-Auswahl
- Hinweis: Datei ist Windows-1252-kodiert — Kodierung beim Editieren beibehalten; neue Enum-Definition in eigener UTF-8-Datei ablegen.

### `MediaSourceExtensions` (statische Klasse)

- **Geänderte Methoden:** `Update(this MediaSource, MediaSource)` — kopiert zusätzlich `SourceType` (Windows-1252-Datei).

### `SftpMediaSourceReader` (Klasse)

- **Geänderte Deklaration:** implementiert `IMediaSourceReader` (Signaturen der fünf virtuellen Methoden sind bereits kompatibel).
- **Geänderte Methoden:** `GetSftpFileStream` wird zu `OpenFileStream` umbenannt; Rückgabetyp `SftpStreamWrapper?` → `Stream?` (Rückgabewert bleibt `SftpStreamWrapper`).
- `ReadSubtree` bleibt unverändert (kein Interface-Member).

### `MediaSourceScanner` (Klasse)

- **Geänderte Konstruktor-Injection:** `SftpMediaSourceReader` → `IMediaSourceReader` (Feld `_sftpReader` → `_reader`).
- **Geänderte Methoden:** `ScanMediaCollectionInternalAsync` — `catch`-Filter (Zeile 213) erweitert um `DirectoryNotFoundException`, `UnauthorizedAccessException`, `IOException`; Aufrufe `ReadRootDirectory`/`ReadDirectoryEntries` gehen an `_reader`.
- `using Renci.SshNet.Common` bleibt für die SFTP-Exceptions erhalten.

### `MediaSourceClassifier` (Klasse)

- **Geänderte Konstruktor-Injection:** `SftpMediaSourceReader` → `IMediaSourceReader` (Feld `_sftpReader` → `_reader`); alle ~16 Aufrufstellen bleiben fachlich unverändert.

### `ItemsController` (Klasse)

- **Geänderte Konstruktor-Injection:** `SftpMediaSourceReader` → `IMediaSourceReader` (Feld `_sftpReader` → `_reader`).
- **Geänderte Methoden:** `StreamMediaItem` — Aufruf `GetSftpFileStream` → `OpenFileStream` (Zeile 442); Rest unverändert. `Download` profitiert indirekt.

### `ServiceCollectionExtensions` (Klasse)

- **Registrierung (Zeile 232 ff.):** `SftpMediaSourceReader` bleibt scoped registriert (wird vom Dispatcher benötigt); neu: `services.AddScoped<LocalMediaSourceReader>()` und `services.AddScoped<IMediaSourceReader, MediaSourceReaderDispatcher>()`.

### `MediaSourceAdminDetails.razor` (Blazor-Komponente, Windows-1252)

- **Neues Formularfeld:** `InputSelect` „Quelltyp" (id `source-type`, Optionen „SFTP-Server"/„Lokales Verzeichnis", gebunden an `editSource.SourceType`, `disabled` wenn `!IsNew`).
- **Bedingtes Rendering:** `Host`/`Port`/`Username`/`Password`-Blöcke nur bei `SourceType == Sftp`; `Path`-Label typabhängig („Pfad" / „Lokales Verzeichnis").
- **Geänderte Methoden:** `OnInitializedAsync` kopiert `SourceType` in `editSource`; `Save()` validiert lokale Pfade (siehe Validierungsregeln) und neutralisiert SFTP-Felder bei `LocalDirectory`.

### `MediaSourceAdmin.razor` (Blazor-Komponente)

- **Übersichtstabelle:** neue Spalte „Typ" („SFTP"/„Lokal"); bei lokalen Quellen zeigen `Host`, `Port`, `Benutzername` `—` statt leerer/irrelevanter Werte.

### `VideoWebPlayerBackupData` (Klasse)

- **Geänderte statische Listen:** `OptionalRestoreColumns` += `MediaSources.SourceType`; `OptionalRestoreIntDefaults` += `(MediaSources, SourceType, 0)`.

### `NavMenu.razor` (Blazor-Komponente) — Behebung eines preexisting Defekts

- **Geänderte Methoden:** `OnInitializedAsync` ruft `Client.EnsureAuthorizationTokenAsync(state.User)` und `Client.RequestSourcesAsync()` nur noch bei authentifiziertem Benutzer (`state.User.Identity?.IsAuthenticated == true`) bzw. fängt `UnauthorizedAccessException` ab.
- **Begründung:** Seit Commit `9d8868d` wirft `InternalVideoWebPlayerClient.ImpersonateAsync` (Zeile 131) für anonyme SSR-Requests `UnauthorizedAccessException`; `NavMenu` rendert über `MainLayout.razor` auf jeder Seite inkl. `/Account/Login` → Redirect `Found` statt gerenderter Seite. Dies ist die dokumentierte Ursache aller 37 preexisting E2E-Fehlschläge und blockiert jeden browserbasierten E2E-Test (Login nicht erreichbar). Die Behebung erfolgt in diesem Feature-Branch (Schritt 1); sie ist fachlich unabhängig korrekt und Voraussetzung für den E2E-Nachweis der Admin-UI.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddMediaSourceSourceType` | `MediaSources.SourceType` | Neue `int`-Spalte, non-nullable, Default `0` (= `Sftp`); `ApplicationDbContextModelSnapshot` aktualisieren. Erstellung per `dotnet ef migrations add` (EF Core Tools/Design sind im Projekt vorhanden); Anwendung erfolgt zur Laufzeit über `app.MigrateDatabase()` (`Program.cs` Zeile 45). Tests nutzen `EnsureCreated` — modellbasiert, keine Migration nötig. |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `editSource.Path` bei `SourceType == LocalDirectory` | Nicht leer; absoluter/rooted Pfad (UNC-Pfade `\\server\share` sind zulässig); `Directory.Exists(Path.GetFullPath(Path))` muss `true` sein | `errorMessage` „Verzeichnis existiert nicht"/„ungültiger Pfad", `Save()` bricht ab, nichts wird persistiert |
| `fileName`/relative Pfadangaben in `LocalMediaSourceReader` | Aufgelöster Voll-pfad muss innerhalb des normalisierten `MediaSource.Path`-Roots liegen; `ReparsePoint`-Einträge werden nicht gelesen | Datei gilt als nicht vorhanden (`null`/`false` bzw. Eintrag wird bei der Enumeration übersprungen) — kein Exception-Durchstich |
| `editSource.SourceType` | Nur `MediaSourceType`-Werte; im Edit-Modus nicht änderbar (UI-deaktiviert) | — |
| SFTP-Felder bei `LocalDirectory` | Werden beim Speichern neutralisiert (`Host = string.Empty`, `Port = 0`, `Username`/`Password = null`) | — |

## Konfigurationsänderungen

Keine. Die optional diskutierte Pfad-Whitelist `MediaSources:LocalRootPaths` wird nicht umgesetzt — der Root-Präfix-Schutz im `LocalMediaSourceReader` begrenzt den Dateisystemzugriff bereits auf die konfigurierte Quelle.

## Seiteneffekte und Risiken

- **Preexisting E2E-Blocker (`NavMenu.razor`):** Die geplante Behebung des anonymen-Benutzer-Fehlers aus Commit `9d8868d` betrifft Code außerhalb des Feature-Kerns. Positiver Nebeneffekt: vermutlich werden alle 37 preexisting E2E-Fehlschläge mitbehoben. Risiko gering (Guard um eine Zeile).
- **Konstruktor-Signaturänderungen** an `MediaSourceScanner`, `MediaSourceClassifier`, `ItemsController` (`SftpMediaSourceReader` → `IMediaSourceReader`): Alle DI-Registrierungen und direkten Konstruktoraufrufe in Tests müssen angepasst werden (siehe Tests). Direkte Übergabe von `new SftpMediaSourceReader()` kompiliert weiterhin (implementiert das Interface).
- **Reader-Fakes:** `Fake/Throwing/Series/BackfillSftpMediaSourceReader` + `MissingPathSftpMediaSourceReader` erben weiterhin von `SftpMediaSourceReader` — keine Codeänderung nötig, nur andere DI-Registrierung (`IMediaSourceReader`).
- **`Path`-Semantik bei lokalen Quellen:** `MediaCollection.Path`/`MediaItem.Path` enthalten OS-Pfade (`C:\...\file.mp4`). `ItemsController` nutzt `Path.GetFileName` — unter Windows korrekt für `\` und `/`. Bei einem Linux-Deployment wäre `\` kein Separator; das Projekt ist Windows-fokussiert (`web.config`, IIS) — akzeptables Risiko.
- **`StreamMediaItem`/`Download` bei lokalen Quellen:** gibt `FileStream` zurück — `enableRangeProcessing` verlangt seekbaren Stream, `FileStream` ist seekbar. Kein Risiko.
- **Backup/Restore:** `SourceType` wird durch die Entity-Iteration automatisch mitserialisiert; ohne den `OptionalRestoreColumns`-Eintrag würden Alt-Backups an der Schema-Validierung scheitern — ist eingeplant.
- **`FileSystemDemoDataSetService`:** legt Quellen mit `Host`/`Port` an → `SourceType` defaultet auf `Sftp`, keine Änderung nötig.
- **`MediaSourceExplorer`/`MediaSourceDetailsViewModel`/`NavMenu`-Quellenliste:** arbeiten auf DB-Daten bzw. DTO — unverändert.
- **Unerreichbare lokale Quelle (z. B. nicht gemountetes Laufwerk):** erweiterter `catch`-Filter sorgt für „überspringen + neu terminieren" statt dauerhaftem Fehlschlag des Scan-Prozesses.

## Umsetzungsreihenfolge

1. **Preexisting `NavMenu`-Defekt beheben (anonyme Benutzer)**
   - Voraussetzungen: Keine.
   - Beschreibung: In `NavMenu.razor` `OnInitializedAsync` die Aufrufe `EnsureAuthorizationTokenAsync`/`RequestSourcesAsync` auf authentifizierte Benutzer beschränken (oder `UnauthorizedAccessException` abfangen). Voraussetzung für jeden Browser-E2E-Test; behebt die dokumentierte Ursache der 37 Baseline-Fehlschläge. Der Fix erfolgt in diesem Feature-Branch.

2. **`MediaSourceType`-Enum anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Neue Datei `VideoWebPlayer/Data/MediaSourceType.cs` mit `Sftp = 0`, `LocalDirectory = 1`.

3. **`MediaSource.SourceType` + `MediaSourceExtensions.Update` + Migration**
   - Voraussetzungen: Schritt 2 (Enum); `Microsoft.EntityFrameworkCore.Design`/Tools sind im Projekt vorhanden.
   - Beschreibung: Eigenschaft `SourceType` (Default `Sftp`) an `MediaSource` ergänzen (Windows-1252-Datei!); `Update` kopiert `SourceType`; `dotnet ef migrations add AddMediaSourceSourceType` erzeugen; Snapshot aktualisiert sich automatisch.

4. **`IMediaSourceReader` extrahieren und `SftpMediaSourceReader` anpassen**
   - Voraussetzungen: Keine (unabhängig von Schritt 2/3).
   - Beschreibung: Interface mit den sechs Methoden anlegen; `SftpMediaSourceReader : IMediaSourceReader`; `GetSftpFileStream` → `OpenFileStream` (Rückgabe `Stream?`).

5. **`LocalMediaSourceReader` implementieren**
   - Voraussetzungen: Schritt 2 + 4 (Enum für `SourceType`-Kontext, Interface).
   - Beschreibung: System.IO-basierte Implementierung inkl. `OrdinalIgnoreCase`-Namensabgleich, `.`-Ignorierung, `GetLastWriteTimeUtc` für `CreatedAt`, Root-Präfixprüfung (`Path.GetFullPath`) und `ReparsePoint`-Ablehnung.

6. **`MediaSourceReaderDispatcher` + DI-Registrierung**
   - Voraussetzungen: Schritte 4 + 5.
   - Beschreibung: Dispatcher-Klasse anlegen; `ServiceCollectionExtensions` um `LocalMediaSourceReader` (scoped) und `IMediaSourceReader` → `MediaSourceReaderDispatcher` (scoped) erweitern.

7. **Konsumenten umstellen**
   - Voraussetzungen: Schritt 6.
   - Beschreibung: `MediaSourceScanner` (Injection + erweiterter `catch`-Filter), `MediaSourceClassifier` (Injection), `ItemsController` (Injection + `OpenFileStream`-Aufruf) auf `IMediaSourceReader` umstellen.

8. **Backup-Restore-Kompatibilität**
   - Voraussetzungen: Schritt 3 (`SourceType` im Modell).
   - Beschreibung: `OptionalRestoreColumns` und `OptionalRestoreIntDefaults` in `VideoWebPlayerBackupData` ergänzen.

9. **Admin-UI: Typauswahl, bedingte Felder, Pfadvalidierung**
   - Voraussetzungen: Schritt 3 (`SourceType` am Modell); UI-Muster: `InputSelect` aus `Register.razor` (Zeile 50) wiederverwenden; Windows-1252-Kodierung der Razor-Datei beachten.
   - Beschreibung: Typ-Select (nur bei Neuanlage aktiv), bedingtes Rendering der SFTP-Felder, Pfad-Label, `SourceType`-Kopie beim Laden, Validierung + Feld-Neutralisierung in `Save()`.

10. **Admin-Übersicht: Typ-Spalte**
    - Voraussetzungen: Schritt 3.
    - Beschreibung: `MediaSourceAdmin.razor` um „Typ"-Spalte und `—`-Darstellung für SFTP-Felder bei lokalen Quellen erweitern.

11. **Bestehende Tests anpassen**
    - Voraussetzungen: Schritte 4–7 (geänderte Signaturen).
    - Beschreibung: DI-Registrierungen in `MediaSourceScannerTests`, `MediaSourceScanServiceTests`, `MediaSourceClassifierBackgroundImageTests`, `MediaSourceClassifierActorBackfillTests` von `SftpMediaSourceReader` auf `IMediaSourceReader` umstellen.

12. **Neue Unit-/Integrationstests**
    - Voraussetzungen: Schritte 5–7.
    - Beschreibung: `LocalMediaSourceReaderTests` (temporäre Verzeichnisse), Dispatcher-Auswahltest, lokaler Scan-/Klassifizierungs-/Streaming-Test, Backup-Restore-Kompatibilitätstest.

13. **Neue E2E-Tests**
    - Voraussetzungen: Schritt 1 (für Browser-Tests), Schritte 3–10; Playwright-Browser sind lokal installiert (laut `inventory/tests.md`).
    - Beschreibung: Siehe Abschnitt „E2E-Tests". HTTP-basierte E2E-Tests (Streaming, Pipeline) sind von Schritt 1 unabhängig ausführbar.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ReadRootDirectory_ReturnsLocalRoot` | `LocalMediaSourceReaderTests` (neu, `VideoWebPlayer.Tests/Services/`) | Root-`MediaCollection` mit `Path` = Quellpfad, `Name` = Verzeichnisname, `CreatedAt` = `GetLastWriteTimeUtc` |
| `ReadDirectoryEntries_ListsFoldersAndFiles_IgnoresDotEntries` | `LocalMediaSourceReaderTests` | Temp-Verzeichnis mit Unterordnern/Dateien/`.hidden`; Ergebnis enthält `MediaCollection`+`MediaItem` mit vollen Pfaden, keine `.`-Einträge |
| `ReadDirectoryEntries_SkipsReparsePoints` | `LocalMediaSourceReaderTests` | Junction/Symlink im Quellverzeichnis wird nicht als Eintrag geliefert |
| `FileExistsAsync_MatchesCaseInsensitive` | `LocalMediaSourceReaderTests` | `tvshow.nfo` wird bei abweichender Groß-/Kleinschreibung gefunden |
| `ReadFileAsync_ReturnsContent_AndNullWhenMissing` | `LocalMediaSourceReaderTests` | UTF-8-Dateiinhalt; `null` bei fehlender Datei |
| `*_RejectsTraversalOutsideRoot` (mehrere Fälle: `..\..`, absoluter Pfad außerhalb, ReparsePoint) | `LocalMediaSourceReaderTests` | `FileExistsAsync`/`ReadFileAsync`/`ReadFileStreamAsync`/`OpenFileStream` liefern `false`/`null` für Pfade außerhalb des Quell-Roots |
| `OpenFileStream_ReturnsReadableFileStream` | `LocalMediaSourceReaderTests` | Seekbarer Stream mit Dateiinhalt; `null` bei fehlender Datei |
| `Dispatcher_RoutesBySourceType` | `MediaSourceReaderDispatcherTests` (neu) | `SourceType.LocalDirectory` → `LocalMediaSourceReader`-Aufruf; `Sftp`/Default → `SftpMediaSourceReader`; `MediaSource`-Navigation `null` → SFTP |
| `ScanAllSourcesAsync_CreatesRootCollection_ForLocalSource` | `MediaSourceScannerLocalTests` (neu o. Erweiterung `MediaSourceScannerTests`) | Echter `LocalMediaSourceReader` + Temp-Verzeichnis; Root-Collection wird angelegt |
| `ScanNextMediaCollection_PopulatesLocalDirectory` | `MediaSourceScannerLocalTests` | Temp-Baum → `MediaCollections`/`MediaItems` in DB |
| `ScanNextMediaCollection_SkipsCollection_WhenLocalDirectoryMissing` | `MediaSourceScannerLocalTests` | `DirectoryNotFoundException` → Collection übersprungen, `ScanDueAt` neu terminiert (Parallele zum bestehenden SFTP-Test) |
| `ClassifyAllAsync_ClassifiesTvShow_FromLocalSource` | `LocalMediaSourceClassifierTests` (neu) | Temp-Verzeichnis mit `tvshow.nfo`/Episoden-Struktur → `TVShow`/`TVShowEpisodes` werden klassifiziert |
| `StreamMediaItem_LocalSource_ReturnsFileStreamResult` | `ItemsControllerLocalSourceTests` (neu, Aufbau wie `ItemsControllerAccessTests`) | `FileStreamResult` mit lokalem `FileStream`, korrekter Content-Type |
| `Update_CopiesSourceType` | `ApplicationDbContextTests` (Erweiterung) | `UpdateMediaSourceAsync` übernimmt `SourceType` |
| `Restore_OldBackupWithoutSourceType_Succeeds` | `VideoWebPlayerBackupDataTests` (Erweiterung, `VideoWebPlayer.Tests/Services/Backups/`) | Backup-Payload ohne `SourceType`-Spalte validiert und stellt `Sftp` (0) wieder her |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `MediaSourceScannerTests` | `services.AddSingleton<SftpMediaSourceReader>(...)` → `IMediaSourceReader`-Registrierung (Scanner-Konstruktor ändert sich) |
| `MediaSourceScanServiceTests` (5 Tests) | Gleiche Registrierungsumstellung (Zeilen 37, 106, 188, 284, 387) |
| `MediaSourceClassifierBackgroundImageTests` (4 Tests) | `BuildServiceProvider`-Registrierung `SftpMediaSourceReader` → `IMediaSourceReader` (Zeile 251) |
| `MediaSourceClassifierActorBackfillTests` (5 Tests) | Registrierung der `BackfillSftpMediaSourceReader`/`MissingPathSftpMediaSourceReader`-Fakes als `IMediaSourceReader` |
| `ItemsControllerAccessTests`, `ItemsControllerMetadataTests`, `SourceVisibilityControllerTests` | Keine Änderung erwartet — direkte `new SftpMediaSourceReader()`-Übergabe kompiliert weiter (Interface); nur verifizieren |
| `MediaSourceDeleteE2ETests`, `MediaSourceSwitchE2ETests` | Keine Codeänderung (seeding nutzt Default `Sftp`); aktuell preexisting-fehlgeschlagen durch `NavMenu`-Defekt — nach Schritt 1 erneut prüfen |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin legt Quelle vom Typ „Lokales Verzeichnis" an: Typauswahl wählen, `Name`+`Path` (echtes Temp-Verzeichnis) ausfüllen, speichern → Quelle erscheint in der Übersicht mit Typ-Kennzeichnung; SFTP-Felder sind bei lokalem Typ nicht sichtbar | `MediaSourceLocalDirectoryE2ETests` (neu, `VideoWebPlayer.Tests/`, Playwright-Muster wie `MediaSourceDeleteE2ETests`) | Admin kann lokale Quelle einrichten; Typauswahl steuert Feldsichtbarkeit | Zentraler neuer Benutzerfluss (Formular-Interaktion, bedingtes Rendering, Persistenz) nur über UI nachweisbar. Voraussetzung: Schritt 1 (`NavMenu`-Fix), da sonst Login/Admin-Seiten nicht rendern |
| Pflicht | Ungültiger lokaler Pfad (nicht existierendes Verzeichnis) → `Save()` zeigt Fehlermeldung, Quelle wird nicht angelegt | `MediaSourceLocalDirectoryE2ETests` | Serverseitige Pfadvalidierung ist für den Anwender sichtbar | Fehlerfall direkt über die UI auslösbar |
| Soll | Bearbeiten einer lokalen Quelle: Typ-Select deaktiviert, lokale Felder sichtbar, Speichern möglich | `MediaSourceLocalDirectoryE2ETests` | Typwechsel-Sperrung und Edit-Rendering | Sichtbarkeits-/Sperrregel im UI |
| Pflicht | Lokale Quelle wird gescannt und klassifiziert: Temp-Verzeichnis mit `tvshow.nfo` + Episoden-Datei; `MediaSourceScanner`/`MediaSourceClassifier` über `_factory.Services`-Scope ausführen; `GET /api/sources` bzw. `GET /api/items/tvshow/{id}?access_token=` liefert die klassifizierten Inhalte | `LocalMediaSourcePipelineE2ETests` (neu, HTTP-Ebene via `WebApplicationFactory` wie `EpisodesBackgroundImageAccessTokenE2ETests`) | Lokale Quelle durchläuft Scan+Klassifizierung im echten Host | Nachweis der Gesamtkette in der laufenden Anwendung; browserunabhängig und damit vom `NavMenu`-Defekt unberührt — bleibt auch ohne Schritt 1 aussagekräftig |
| Pflicht | `GET /api/items/movie/{id}/stream?access_token=` auf lokale Quelle liefert HTTP 200, `video/*`-Content-Type und die Dateibytes; `GET .../download` ebenfalls | `LocalMediaSourceStreamingE2ETests` (neu, HTTP-Ebene) | Streaming/Download aus lokalem Verzeichnis funktioniert | Kernfunktion (Range-fähiges Streaming) über den echten HTTP-Endpunkt; browserunabhängig |
| Pflicht | `GET /api/items/movie/{id}/stream` ohne `MediaSourceUsers`-Freigabe → HTTP 401 | `LocalMediaSourceStreamingE2ETests` | Zugriffsschutz gilt auch für lokale Quellen | Berechtigungsregel am echten Endpunkt |

Welche bestehenden E2E-Tests müssen angepasst werden? Keine inhaltlich — `MediaSourceDeleteE2ETests`/`MediaSourceSwitchE2ETests` seeden `MediaSource` ohne `SourceType` (Default `Sftp`) und bleiben kompilier- und lauffähig. Sie sind im Baseline-Lauf durch den `NavMenu`-Defekt fehlgeschlagen; nach Schritt 1 sind sie als Regression mitzuverwenden. Neue E2E-Tests werden in eigenen Dateien mit sprechenden Assert-Meldungen (Console-/Body-Kontext wie in `MediaSourceDeleteE2ETests`) angelegt, damit ihre Ergebnisse von den preexisting Fehlschlägen klar unterscheidbar bleiben.

## Offene Punkte

Keine.
