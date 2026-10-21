# Bestandsaufnahme — Datenmodell

## `MediaSource`
Datei: `VideoWebPlayer/Data/MediaSource.cs` (Achtung: Datei ist **CP1252**-kodiert, nicht UTF-8 — bei Edits Encoding beachten, wird für diese Anforderung nicht geändert)

Erbt von `MediaEntry` (`Id`, `Name`, `Path`, `CreatedAt`, `ClassifiedAt`, `Changed`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Host` | `string` | Hostname des SFTP-Servers |
| `Port` | `int` | Port (üblich 22; <= 0 möglich) |
| `Username` | `string?` | SFTP-Benutzername, kann `null` sein |
| `Password` | `string?` | SFTP-Passwort im Klartext in der DB, kann `null` sein |
| `SourceType` | `MediaSourceType` | `Sftp` (0, Default) / `LocalDirectory` (1) |
| `Path` (aus `MediaEntry`) | `string` | Root-Pfad der Quelle auf dem Server |
| `IconPictureId`, `LastScannedAt`, `MediaCollections`, `MediaSourceUsers` | – | nicht relevant |

## `MediaCollection`
Datei: `VideoWebPlayer/Data/MediaCollection.cs` (CP1252)

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Path` (aus `MediaEntry`) | `string` | Voller SFTP-Verzeichnispfad bzw. lokaler Pfad |
| `MediaSource` | `MediaSource` | Navigation; in `ProbePendingAsync` per `Include` geladen |

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Path` (aus `MediaEntry`) | `string` | Bei SFTP: **voller Remote-Pfad** (`ISftpFile.FullName` aus `client.ListDirectory(collection.Path)` = `collection.Path + "/" + entry.Name`); bei lokal: voller Dateisystempfad |
| `Name` (aus `MediaEntry`) | `string` | Dateiname (`entry.Name`) |
| `Duration` | `TimeSpan?` | Ermittelte Laufzeit; `null` = ausstehend/fehlgeschlagen |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt des letzten Probe-Versuchs |
| `DurationProbeAttempts` | `int` | Zähler fehlgeschlagener Versuche (Migration aus Schritt 8) |

**Wichtig für die URL-Pfadbildung:** Da `MediaItem.Path` bereits den vollen SFTP-Pfad trägt, sind `item.Path` und `CombineSftpPath(collection.Path, item.Name)` für gescannte SFTP-Items äquivalent. `OpenFileStream` verwendet Letzteres.
