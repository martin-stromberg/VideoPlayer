# Datenmodell

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs` (Encoding: Windows-1252)

Erbt von `MediaEntry`. Repräsentiert eine einzelne Datei in einer `MediaCollection`. Kein Laufzeit-Feld vorhanden.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaCollectionId` | `long` | FK auf die enthaltende `MediaCollection` |
| `MediaCollection` | `MediaCollection` | Navigation zur Collection (verpflichtend, `null!`) |

Geerbt von `MediaEntry` (`VideoWebPlayer/Data/MediaEntry.cs`):

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` | Dateiname |
| `Path` | `string` | Vollständiger Pfad bzw. Verbindungsinfo (bei lokalen Quellen absoluter Dateisystempfad) |
| `CreatedAt` | `DateTime` | Änderungszeitpunkt der Datei (File.GetLastWriteTimeUtc bzw. SFTP-Attribut); Grundlage der Dateiwechsel-Erkennung im Scanner |
| `ClassifiedAt` | `DateTime?` | Zeitpunkt der letzten Klassifizierung |
| `Changed` | `bool` | Marker „geändert, Klassifizierung nötig" |

## `MediaCollection`
Datei: `VideoWebPlayer/Data/MediaCollection.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceId` | `long` | FK `MediaSource` |
| `ParentMediaCollectionId` | `long?` | FK Eltern-Collection |
| `LastScannedAt` | `DateTime?` | Zeitpunkt des letzten Scans |
| `ScanDueAt` | `DateTime?` | Nächster fälliger Scan |
| `Classifyable` | `bool` | Vollständig gescannt → klassifizierbar |
| `MediaSource` | `MediaSource` | Navigation (für Reader-Dispatch erforderlich, eager zu laden) |
| `MediaItems` | `ICollection<MediaItem>` | Enthaltene Dateien |
| `Skip` | `bool` | Skip-Flag für Scans |

## `MediaSource`
Datei: `VideoWebPlayer/Data/MediaSource.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Host` | `string` | SFTP-Host |
| `Port` | `int` | SFTP-Port |
| `Username` / `Password` | `string?` | SFTP-Credentials |
| `SourceType` | `MediaSourceType` | `Sftp` (Default) oder `LocalDirectory` — steuert den `MediaSourceReaderDispatcher` |
| `IconPictureId` | `long?` | Optionaler Icon-FK |
| `LastScannedAt` | `DateTime?` | Letzter Root-Scan |
| `MediaCollections` | `ICollection<MediaCollection>` | Collections der Quelle |
| `MediaSourceUsers` | `ICollection<MediaSourceUser>` | Zugriffszuordnung |

## `Movie` / `TVShowEpisode`
Dateien: `VideoWebPlayer/Data/Movie.cs`, `VideoWebPlayer/Data/TVShowEpisode.cs`

Klassifizierte Entitäten, jeweils über Join-Entities `MovieMediaItem` (`MovieId`+`MediaItemId`) bzw. `TVShowEpisodeMediaItem` (`TVShowEpisodeId`+`MediaItemId`) 1:n mit `MediaItem` verknüpft. **Kein** Laufzeit-Feld; `LoadFromXml` liest aus NFO u. a. keine Dauer (Movie-NFO hätte optional `runtime`, wird nicht gelesen und ist laut Anforderung keine verlässliche Quelle).

## `ContinueWatchingEntry`
Datei: `VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs`

Referenzbeleg für `TimeSpan`-Speicherung: `Position` (`TimeSpan`, Required) und `Duration` (`TimeSpan?`) werden per EF auf SQLite als `TEXT` gemappt (siehe Migration `UpdateMediaModels25`) und laufen fehlerfrei durch Backup/Restore (`ToDbValue` deserialisiert über `JsonSerializer`-Fallback).

## `Setup`
Datei: `VideoWebPlayer/Data/Setup.cs`

Einzelne Einstellungszeile (`ProgramSettingsService.GetOrCreateSetupAsync`): `ScanProcessIntervalMinutes` (Default 60), `MediaCollectionScanIntervalDays` (Default 7), `DataVersion` (für `DataUpgradeManager`, aktuell 8) u. a.

## `ApplicationDbContext`
Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`

DbSets u. a. `MediaSources`, `MediaCollections`, `MediaItems`, `Movies`, `TVShowEpisodes`, `MovieMediaItems`, `TVShowEpisodeMediaItems`. Konfigurationen unter `VideoWebPlayer/Data/Configurations/` (`MediaItemConfiguration` registriert nur die Collection-Relation). Migrations liegen in `VideoWebPlayer/Migrations/` (aktuellster Stand siehe `ApplicationDbContextModelSnapshot.cs`).
