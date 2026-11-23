# Datenmodell (Medien-Entitäten)

Vererbung: `MediaEntry` (Datei-/Quell-Seite: `MediaSource`, `MediaCollection`,
`MediaItem`) und `MediaBaseEntry` (klassifizierte Titel: `Movie`, `TVShow`,
`TVShowSeason`, `TVShowEpisode`, `MovieCollection`).

## `MediaEntry` (abstract)

Datei: `VideoWebPlayer/Data/MediaEntry.cs`

| Eigenschaft | Typ |
|-------------|-----|
| `Id`, `Name`, `Path`, `CreatedAt` | `long`, `string`, `string`, `DateTime` |
| `ClassifiedAt`, `Changed` | `DateTime?`, `bool` |

## `MediaSource` : `MediaEntry`

`VideoWebPlayer/Data/MediaSource.cs` — siehe [media-sources.md](media-sources.md).

## `MediaCollection` : `MediaEntry`

Datei: `VideoWebPlayer/Data/MediaCollection.cs`

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `MediaSourceId` | `long` | Quelle |
| `ParentMediaCollectionId` | `long?` | Baumstruktur |
| `LastScannedAt`, `ScanDueAt` | `DateTime?` | Scan-Scheduling (`ScanDueAt` steuert `ScanNextMediaCollection`) |
| `Classifyable` | `bool` | Freigabe für Klassifizierung (alle Kinder classifyable) |
| `Skip` | `bool` | Ausblend-Flag |
| `ChildCollections`, `MediaItems` | Collections | Navigationen |

## `MediaItem` : `MediaEntry`

Datei: `VideoWebPlayer/Data/MediaItem.cs`

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `MediaCollectionId` | `long` | Eltern-Collection |
| `Duration`, `DurationProbedAt`, `DurationProbeAttempts` | `TimeSpan?`, `DateTime?`, `int` | ffprobe-Laufzeitprobe |

## `MediaBaseEntry` (abstract)

Datei: `VideoWebPlayer/Data/MediaBaseEntry.cs`

| Eigenschaft | Typ |
|-------------|-----|
| `Name`, `ReleaseDate`, `PremieredAt`, `EndedAt` | `string`, `DateTime?` |
| `MediaSourceId`, `CollectionId` | `long` — `CollectionId` zeigt auf `MediaCollection.Id` |
| `CreatedAt`, `ClassifiedAt`, `Changed`, `IsManuallyEdited` | `DateTime`, `DateTime?`, `bool`, `bool` |
| `PosterPictureId`, `BannerPictureId`, `FanartPictureId` + Navigationen | `long?`, `Picture?` |

## `Movie` : `MediaBaseEntry`

Datei: `VideoWebPlayer/Data/Movie.cs` — zusätzlich `OriginalTitle`, `Language`,
`Year`, `Country`, `Studios`, `Director`, `Credits`, `Plot`, `AgeRating*`,
`GenreNames`, `MovieCollectionId`; Navigationen `MovieMediaItems`
(Link-Tabelle `MovieMediaItem`, `IsTrailer`-Flag), `MovieGenres`, `MovieActors`.

## `TVShow` : `MediaBaseEntry`

Datei: `VideoWebPlayer/Data/TVShow.cs` — `OriginalName`, `Language`, `Plot`,
`Status`, `Studio`, `GenreNames`, `AgeRating*`; `Seasons`, `TVShowGenres`.

## `TVShowSeason` : `MediaBaseEntry`

Datei: `VideoWebPlayer/Data/TVShowSeason.cs` — `TVShowId`, `Episodes`;
`CollectionId` ist die **Episode-Collection** (Unterordner).

## `TVShowEpisode` : `MediaBaseEntry`

Datei: `VideoWebPlayer/Data/TVShowEpisode.cs` — `Number`, `TVShowSeasonId`,
`Plot`, `GeneratedBackgroundPictureId`, `BackgroundImageRequiresUpdate`,
`BackgroundImageGeneratedAt`; Links `TVShowEpisodeMediaItem`, `TVShowEpisodeActor`.

## `Picture`

Datei: `VideoWebPlayer/Data/Picture.cs`

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `Id`, `Type`, `Description` | `long`, `string`, `string?` | `Type` = `poster`/`banner`/`fanart`/`thumb`/`background`/`folder` |
| `MediaItemId`, `MediaItem` | `long?`, `MediaItem?` | Quelldatei |
| `Data`, `ContentType` | `byte[]`, `string` | **Bildbytes in der DB** |
| `Width`, `Height` | `int?` | optional |
| `IsGeneratedBackground`, `EpisodeId`, `PlaylistId` | `bool`, `long?` | generierte Hintergründe |

## `MediaSourceUser`

Datei: `VideoWebPlayer/Data/MediaSourceUser.cs` — Join `MediaSourceId` + `UserId`;
steuert Quell-Freigabe (serverseitig geprüft, siehe Controller-Zugriffschecks).

## `Setup`

Datei: `VideoWebPlayer/Data/Setup.cs` — enthält u. a. `ScanProcessIntervalMinutes`
(Default 60) und `TvSenderEnabled`; über `ProgramSettingsService` gelesen.
