# Lokale Medienquelle (`MediaSourceType.LocalDirectory`)

Implementierungsstand: **vollständig umgesetzt und released** (Commit `b592bce`,
Release-Notes-Eintrag „Medienquellen unterstützen jetzt lokale Verzeichnisse").
Für die Demoinstallation ist das die fachliche Grundlage — kein neuer Quelltyp
nötig.

## `MediaSourceType` (Enum)

Datei: `VideoWebPlayer/Data/MediaSourceType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Sftp = 0` | Entfernter SFTP-Server; **Default für Bestandsdaten und neue `MediaSource`-Instanzen** |
| `LocalDirectory = 1` | Lokales Verzeichnis auf dem Server (inkl. UNC-Pfade) |

Persistenz/Backup: Spalte `SourceType` ist in
`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs:81` als
`OptionalRestoreColumns`-Eintrag und in Zeile 166 mit Default `Sftp` für
Alt-Backups hinterlegt; Migration `20260922180232_AddMediaSourceSourceType`.

## `MediaSource` (relevante Felder)

Datei: `VideoWebPlayer/Data/MediaSource.cs` (nicht-UTF-8/cp1252-Kodierung —
beim Bearbeiten Encoding beachten)

| Eigenschaft | Typ | Anmerkung |
|-------------|-----|-----------|
| `Host`, `Port`, `Username`, `Password` | `string`/`int`/`string?` | Nur für SFTP relevant; Admin-UI leert sie bei `LocalDirectory` (`MediaSourceAdminDetails.razor:306-309`) |
| `Path` | `string` | Bei `LocalDirectory`: **absoluter Serverpfad** (siehe Validierung) |
| `SourceType` | `MediaSourceType` | Default `Sftp` |
| `IconPictureId`, `IconPicture` | `long?`/`MediaSourceIcon?` | Hochgeladenes Quellen-Icon (2 MB Limit, `MediaSourceIcons`-Tabelle) |
| `LastScannedAt`, `HiddenInMenu` | `DateTime?`/`bool` | Scan-Zeitstempel; `HiddenInMenu` ist rein kosmetisch |
| `MediaSourceUsers` | `ICollection<MediaSourceUser>` | Freigabe pro Benutzer |

## Reader-Pipeline (`IMediaSourceReader`)

Datei: `VideoWebPlayer/Services/IMediaSourceReader.cs` — Vertrag:
`ReadRootDirectory`, `ReadDirectoryEntries`, `FileExistsAsync`, `ReadFileAsync`,
`ReadFileStreamAsync`, `OpenFileStream`.

`MediaSourceReaderDispatcher` (`VideoWebPlayer/Services/MediaSourceReaderDispatcher.cs`)
wählt pro Aufruf anhand `MediaSource.SourceType` den Reader (Zeile 63–64:
`LocalDirectory ? _localReader : _sftpReader`); er verlangt, dass
`MediaCollection.MediaSource` eager geladen ist (Zeilen 52–61).

## `LocalMediaSourceReader`

Datei: `VideoWebPlayer/Services/LocalMediaSourceReader.cs`

| Methode | Zeilen | Beschreibung |
|---------|--------|--------------|
| `ReadRootDirectory` / `CreateRootCollection` | 32–73 | Liefert genau eine `MediaCollection` mit `Path = source.Path`, `Name` = Verzeichnisname; ungültiger Pfad → Warnung + leere Folge (kein Abbruch des Root-Scans) |
| `ReadDirectoryEntries` | 80–124 | `Directory.EnumerateFileSystemEntries`; überspringt `.`-Einträge (`MediaEntryFilter.IsIgnoredEntry`, `MediaEntryFilter.cs:13-16`) und ReparsePoints; Verzeichnisse → `MediaCollection`, Dateien → `MediaItem` |
| `FileExistsAsync` / `ReadFileAsync` / `ReadFileStreamAsync` | 132–164 | Lesen über `ResolveFilePath`; NFO- und Bildzugriff des Classifiers |
| `OpenFileStream` | 172–179 | `FileStream` (Read/ReadShare) — Wiedergabe-Pfad |
| `ResolveLocalFilePath` | 190–191 | Gibt den physischen Pfad für externe Tools (Live-Stream-ffmpeg) |
| `ResolveFilePath` / `ResolveSegmentsCaseInsensitive` | 200–299 | Root-Confinement: Pfade außerhalb der Quelle, ReparsePoints und Verzeichnis-Endpunkte liefern `null`; Segmente werden case-insensitiv aufgelöst |

## Admin-UI

- `MediaSourceAdmin.razor`: Übersicht mit Typ-Kennzeichnung „Lokal"/„SFTP"
  (Zeilen 89–90); Button „Komplettscan" ruft
  `MediaSourceScanCoordinator.RunFullScanAsync("Manueller Komplettscan", null)` (Zeile 221).
- `MediaSourceAdminDetails.razor`: Quelltyp-Auswahl (Zeile 79), Pfadfeld mit
  Label „Lokales Verzeichnis" bei `LocalDirectory` (Zeilen 101–103); beim
  Speichern `TryValidateLocalDirectory` (Zeilen 297–310) — **absoluter Pfad
  erforderlich** (`Path.IsPathRooted`), Verzeichnis muss existieren und
  lesbar sein (Zeilen 383–420); bei `LocalDirectory` werden Host/Port/
  Credentials geleert. Quelltyp-Wechsel bestehender Quellen ist blockiert
  (E2E-getestet). Löschen über `Client.DeleteSourceAsync` (Zeile 435) mit
  `MediaSourceDeletedEvent`.
- `MediaSourceExplorer.razor`: „Rescan" je Collection via
  `RescanCollectionTreeAsync` (Zeile 251).

## Streaming / Laufzeitermittlung für lokale Quellen

- Wiedergabe: `ItemsController.OpenMediaFileAsync` (`ItemsController.cs` ~Zeile 630)
  öffnet die Datei über denselben `IMediaSourceReader.OpenFileStream`-Vertrag
  → bei `LocalDirectory` `FileStream`; `MediaFileContentTypes.ForFileName`.
  Interner Stream für ffprobe/Segmenter: `InternalMediaStreamController.cs:88`.
- Dauerprobe: `MediaDurationProbeService.cs:277` — bei `LocalDirectory` wird
  `ProbeFileAsync(item.Path)` direkt auf den Dateipfad gerufen (kein
  Token-/HTTP-Umweg nötig).
- Live-Stream: `LiveStreamMediaResolver.cs:116` nutzt `ResolveLocalFilePath`.

## Events

`ApplicationDbContext.AddMediaSourceAsync` (`ApplicationDbContext.cs:251-256`)
publiziert `MediaSourceCreatedEvent` (`Events/MediaSourceCreatedEvent.cs`).
**Kein Subscriber im Code** — Suche über `Subscribe<`/`SubscribeDisposable` findet
nur `BackgroundProcessingStatusEvent` (`StatusTicker.razor:18`). Das Anlegen
einer Quelle (auch per Demodaten) triggert also **keinen** Scan.
