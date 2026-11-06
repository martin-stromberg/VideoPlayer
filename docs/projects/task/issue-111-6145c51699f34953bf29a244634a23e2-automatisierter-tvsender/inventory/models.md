# Datenmodell — Bestandsaufnahme

Alle Entities liegen unter `VideoWebPlayer/Data/` (teilweise `Data/Entities/`, `Data/Configurations/` für EF-Konfigurationen). Der `ApplicationDbContext` (`VideoWebPlayer/Data/ApplicationDbContext.cs`) registriert die DbSets; `OnModelCreating` wendet alle `IEntityTypeConfiguration<T>` der Assembly an (`ApplyConfigurationsFromAssembly`, Zeile ~718) und registriert die DbFunction `AppDbFunctions.LowerInvariant`.

## Explizit nicht vorhanden (verifiziert)

Per Volltextsuche über `VideoWebPlayer/` und `VideoWebPlayer.Client/` geprüft — **keine** der folgenden, von der Anforderung geforderten Entities existiert:

- `TvChannel` / Sender-Entity
- `ScheduleRule` (Programmregel)
- `ScheduleSlot` (Sende-Slot mit `StartAt`/`EndAt`/`Status`)
- `ScheduleProgress` (Fortsetzungs-Merker pro Serie/Regel)
- **Kein Dauer-/Laufzeit-Feld** an `Movie`, `TVShowEpisode`, `MediaItem`, `MediaBaseEntry`, `MediaEntry` — kein `Duration`, `Runtime`, `Length` o. ä. Der Scheduler kann `EndAt` und Offsets heute aus keinem Datenfeld ableiten.

## `MediaBaseEntry`

Datei: `VideoWebPlayer/Data/MediaBaseEntry.cs`

Basisklasse für `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` | Anzeigename |
| `ReleaseDate` | `DateTime?` | Erscheinungsdatum (u. a. Sortierung `PlaylistSortMode.ByReleaseDate`) |
| `PremieredAt` | `DateTime?` | Premierendatum |
| `EndedAt` | `DateTime?` | Enddatum |
| `MediaSourceId` | `long` | FK zur Medienquelle |
| `CollectionId` | `long` | FK zur Medienkollektion |
| `CreatedAt` | `DateTime` | Anlagezeitpunkt |
| `ClassifiedAt` | `DateTime?` | Zeitpunkt der letzten Klassifizierung |
| `Changed` | `bool` | Änderungsflag |
| `IsManuallyEdited` | `bool` | Schutz manueller Metadaten vor Scans |
| `PosterPictureId`/`BannerPictureId`/`FanartPictureId` | `long?` | Bild-FKs |
| `PosterPicture`/`BannerPicture`/`FanartPicture` | `Picture?` | Bild-Navigationen |

**Kein Dauer-Feld.**

## `Movie`

Datei: `VideoWebPlayer/Data/Movie.cs` — erbt `MediaBaseEntry`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MovieCollectionId` | `long?` | FK zur Filmsammlung |
| `MovieCollection` | `MovieCollection?` | Navigation |
| `OriginalTitle`, `Language`, `Country`, `Studios`, `Director`, `Credits`, `Plot` | `string?` | NFO-Metadaten |
| `Year` | `int?` | Erscheinungsjahr |
| `ActorsClassifiedAt` | `DateTime?` | Stand der Schauspieler-Klassifizierung |
| `MovieMediaItems` | `ICollection<MovieMediaItem>` | Verknüpfung zu Dateien (`MediaItem`) |
| `GenreNames` | `string?` | Kommagetrennte Genre-Namen aus NFO |
| `MovieGenres` | `ICollection<MovieGenre>` | Genre-Verknüpfungen (`Genre`-Entity) |
| `MovieActors` | `ICollection<MovieActor>` | Schauspieler-Verknüpfungen |
| `MediaItems` | `IEnumerable<MediaItem>` | Komfort-Projection über `MovieMediaItems` |
| `LoadFromXml(XElement)` | Methode | Liest NFO-Felder — **liest kein `runtime`-/`duration`-Element** |

## `TVShowEpisode`

Datei: `VideoWebPlayer/Data/TVShowEpisode.cs` — erbt `MediaBaseEntry`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Number` | `int` | Episodennummer (für Fortsetzungsreihenfolge relevant) |
| `TVShowSeasonId` | `long` | FK zur Staffel |
| `TVShowSeason` | `TVShowSeason` | Navigation (→ `TVShowSeason.TVShowId` → `TVShow`) |
| `Plot` | `string?` | Handlung |
| `GeneratedBackgroundPictureId`, `BackgroundImageRequiresUpdate`, `BackgroundImageGeneratedAt`, `GeneratedBackgroundPicture` | — | Generiertes Hintergrundbild |
| `ActorsClassifiedAt` | `DateTime?` | Stand der Schauspieler-Klassifizierung |
| `TVShowEpisodeMediaItems` | `ICollection<TVShowEpisodeMediaItem>` | Verknüpfung zu Dateien |
| `TVShowEpisodeActors` | `ICollection<TVShowEpisodeActor>` | Schauspieler-Verknüpfungen |
| `MediaItems` | `IEnumerable<MediaItem>` | Komfort-Projection |
| `LoadFromXml(XElement)` | Methode | Liest `episode`, `aired`, `premiered`, `plot` — **kein Laufzeit-Element** |

## `TVShow` / `TVShowSeason` / `MovieCollection`

Dateien: `VideoWebPlayer/Data/TVShow.cs`, `TVShowSeason.cs`, `MovieCollection.cs` — alle erben `MediaBaseEntry`.

- `TVShow`: `Seasons` (ICollection), `OriginalName`, `Language`, `Plot`, `Status`, `Studio`, `GenreNames`, `TVShowGenres` — Serien-Stammsatz; Referenzziel für `ScheduleRule.TVShowId` und für `UnlockedMediaEntry.TVShowId`.
- `TVShowSeason`: `TVShowId` (FK), `TVShow`, `Episodes` (ICollection) — Staffel-Knoten der Serienhierarchie.
- `MovieCollection`: `Movies` (ICollection) — Filmsammlung; Referenzziel für `ScheduleRule.MovieCollectionId` und `UnlockedMediaEntry.MovieCollectionId`.

## `MediaEntry` / `MediaItem`

Dateien: `VideoWebPlayer/Data/MediaEntry.cs` (Basisklasse), `MediaItem.cs` (beide Dateien in Windows-1252 kodiert, nicht UTF-8).

`MediaEntry` (abstrakt, Basisklasse für `MediaSource`, `MediaCollection`, `MediaItem`): `Id` (long), `Name` (string), `Path` (string), `CreatedAt` (DateTime), `ClassifiedAt` (DateTime?), `Changed` (bool).

`MediaItem` erbt `MediaEntry`: `MediaCollectionId` (long, FK), `MediaCollection` (Navigation). Repräsentiert eine einzelne Datei (u. a. Videodatei); `ItemsController` löst `Movie`/`TVShowEpisode` über `MovieMediaItem`/`TVShowEpisodeMediaItem` auf `MediaItem` auf und streamt `MediaItem.Path`. **Kein Dauer-Feld.**

## `MediaSource` / `MediaCollection`

Dateien: `VideoWebPlayer/Data/MediaSource.cs`, `MediaCollection.cs` — beide erben `MediaEntry`.

- `MediaSource`: `Host`, `Port` (int), `Username`, `Password`, `SourceType` (`MediaSourceType`: `Sftp`/`LocalDirectory`), `IconPictureId`, `LastScannedAt`, `MediaCollections`, `MediaSourceUsers`.
- `MediaCollection`: `MediaSourceId` (FK), `ParentMediaCollectionId` (long?, Verzeichnisbaum), `LastScannedAt`, `ScanDueAt` (nächster fälliger Scan — vorhandenes Feld für zeitbasierte Planung), `Classifyable`, `Skip`, `MediaSource`, `ParentMediaCollection`, `ChildCollections`, `MediaItems`.

## `Genre` / `GenreName`

Datei: `VideoWebPlayer/Data/Genre.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK — Referenzziel für `ScheduleRule.GenreId` |
| `Name` | `string` | Genre-Name |
| `MediaSourceId` | `long` | FK (Genres sind quellenbezogen!) |
| `StartDate`/`EndDate` | `DateTime?` | Sichtbarkeitsfenster (saisonale Genres — Vorbild für Feiertags-Zeitfenster) |
| `AlternateNames` | `ICollection<GenreName>` | Aliasnamen |
| `MovieGenres`/`TVShowGenres` | ICollection | Verknüpfungen |
| `IsHidden` | `bool` | Ausblendflag |

## `Playlist` (bestehend — Namenskonflikt!)

Datei: `VideoWebPlayer/Data/Playlist.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK (`[Key]`) |
| `UserId` | `string` | FK `ApplicationUser`, `[Required]` — benutzereigene Playlist |
| `Name` | `string` | `[Required]`, `NameMaxLength = 255` |
| `Description` | `string?` | `DescriptionMaxLength = 2000` |
| `SortMode` | `PlaylistSortMode` | `ByReleaseDate` (Default) / `Manual` |
| `CreatedAt`/`UpdatedAt` | `DateTime` | Zeitstempel |
| `GenresManuallyOverridden` | `bool` | Manuelle Genre-Zuordnung |
| `IsPublic` | `bool` | Öffentlich-Flag (nur Admin+Owner setzbar) |
| `CoverPictureId`, `CoverPictureIsUserUploaded`, `CoverPicture` | `long?`/`bool`/`Picture?` | Cover |
| `PlaylistEntries` | `ICollection<PlaylistEntry>` | Einträge |
| `MaxDisplayedGenres` | `const int = 5` | Anzeige-Kappe |

## `PlaylistEntry` (polymorphe Referenz — Vorbild für `ScheduleSlot`)

Datei: `VideoWebPlayer/Data/PlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `PlaylistId`/`Playlist` | `long`/`Playlist` | FK, Kaskade (`DeleteBehavior.Cascade`) |
| `MediaType` | `string` | `[Required]` — Diskriminator aus `MediaTypeValues` (`Movie`, `TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection`) — **String-Diskriminator + `MediaId` statt typisierter FKs** |
| `MediaId` | `long` | ID des referenzierten Mediums |
| `ParentMediaType`/`ParentMediaId` | `string?`/`long?` | Sammlungskontext (durch was kaskadiert hinzugefügt) |
| `AddedAt` | `DateTime` | Hinzufügezeitpunkt |
| `SortOrder` | `long?` | Manuelle Reihenfolge |

EF-Konfiguration `PlaylistEntryConfiguration`: Unique-Index `(PlaylistId, MediaType, MediaId)`, Zusatzindizes `(MediaType, MediaId)` und `(PlaylistId, SortOrder)`.

## `PlaylistBackfillMarker` (Vorbild für „Plan-Neuaufbau"-Merker)

Datei: `VideoWebPlayer/Data/PlaylistBackfillMarker.cs`; DbSet in `ApplicationDbContext.PlaylistBackfillMarking.cs` (partial class).

`Id` (long), `MediaType` (string), `MediaId` (long), `MarkedAt` (DateTime), `Version` (long, Upsert-Zähler). Wird von einem `SaveChanges`-Hook geschrieben, wenn neue `TVShowEpisode`/`TVShowSeason`/`Movie` entstehen — der „ein einziger Punkt, durch den alles geht"-Mechanismus.

## Zugriffs-/Auth-Entities

- **`MediaSourceUser`** (`Data/MediaSourceUser.cs`, Composite-Key `MediaSourceId`+`UserId`): Quellenfreigabe pro Benutzer; Grundlage der serverseitigen Zugriffsprüfung (`ItemsController.EnsureAccessAsync`).
- **`UnlockedMediaEntry`** (`Data/UnlockedMediaEntry.cs`): `Id`, `UserId`, `MovieCollectionId?`, `TVShowId?`, `CreatedAt` — Einzelfreischaltung unabhängig von der Quellenfreigabe. Kein `MovieId`-Feld (Filme laufen über ihre `MovieCollection`).
- **`PairedDevice`** (`Data/PairedDevice.cs`): `Id` (int), `Name`, `TokenHash`, `IssuedAtUtc`, `LastUsedAtUtc`, `RevokedAtUtc`, `CreatedByUserId` — gekoppelte Geräte (Device-Token-Auth).
- **`PairingCode`** (`Data/PairingCode.cs`): `Id`, `Kind` (`PairingCodeKind`), `CodeHash`, `TicketHash`, `CreatedAtUtc`, `ExpiresAtUtc`, `ConsumedAtUtc`, `CreatedByUserId`.
- **`RefreshToken`** (`Data/RefreshToken.cs`): `Id`, `TokenHash`, `UserId`, `DeviceId`, `CreatedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc`, `ReplacedByHash` (Rotation).

## `ContinueWatchingEntry` — einzige `Duration` im Modell

Datei: `VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs`

`Id`, `UserId`, `MovieId?`, `TVShowEpisodeId?`, `PlaylistId?`, `Position` (TimeSpan), **`Duration` (`TimeSpan?`)** — vom Client während der Wiedergabe gemeldete Gesamtlaufzeit (`ContinueWatchingController` `DurationSeconds`), `UpdatedAt`, `ListOrder`, Navigationen. **Nicht** vom Scan befüllt und benutzerbezogen — für den Scheduler ungeeignet, aber Beleg, dass die Client-Player die Laufzeit kennen.

## `Setup` (Admin-editierbare Anwendungseinstellungen)

Datei: `VideoWebPlayer/Data/Setup.cs` — Singleton-Satz über `ProgramSettingsService` (`GetOrCreateSetupAsync`).

`Id`, `DataVersion`, `GenresChanged`, `ApplicationTitle`, `ScanProcessIntervalMinutes` (60), `MediaCollectionScanIntervalDays` (7), `ContinueWatchingEndThresholdSeconds` (30), `ActorCollectionThresholdPercent` (50), `MdnsAdvertisementEnabled`, `DiscoveryPublicBaseUrl`, `PlaylistBackfillLastSweepAt` (persistierter Zeitpunkt des letzten Safety-Sweeps — Vorbild für „letzte Planerstellung").

## `ApplicationDbContext` — DbSets

`MediaSources`, `MediaSourceUsers`, `MediaCollections`, `MediaItems`, `MovieCollections`, `Movies`, `TVShows`, `TVShowSeasons`, `TVShowEpisodes`, `MovieMediaItems`, `TVShowEpisodeMediaItems`, `Pictures`, `MediaSourceIcons`, `Setups`, `RecentEntries`, `FavoriteEntries`, `Playlists`, `PlaylistEntries`, `PlaylistEntryExclusions`, `PlaylistGenres`, `UnlockedMediaEntries`, `Genres`, `GenreNames`, `MovieGenres`, `TVShowGenres`, `Actors`, `MovieActors`, `TVShowEpisodeActors`, `ContinueWatchingEntries`, `WatchedEntries`, `BlockedLoginIps`, `BackupSettings`, `BackupOperationHistories`, `UpdateSettings`, `PairedDevices`, `PairingCodes`, `RefreshTokens`, `PlaylistBackfillMarkers` (partial). **Keine DbSets für Sender/Regeln/Slots.**

## Backup-Registrierung (`VideoWebPlayerBackupData`)

Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `OptionalRestoreTables` (Zeile 23–39): Tabellen, die in Alt-Backups fehlen dürfen — u. a. alle Playlist-, Pairing- und Unlock-Tabellen. **Neue Sender-Tabellen müssen hier eingetragen werden.**
- `OptionalRestoreColumns` (Zeile 41–76): Spalten, die in Alt-Backups fehlen dürfen — Muster `Tabelle.Spalte`. **Ein neues Dauer-Feld an `Movies`/`TVShowEpisodes` muss hier eingetragen werden.**
- `IgnoredRestoreColumns`, `OptionalRestoreBoolDefaults`, `OptionalRestoreLongDefaults`, `OptionalRestoreIntDefaults`: Standardwerte beim Wiederherstellen fehlender Spalten.
- `CurrentSchemaVersion = 1`; Restore löscht alle Tabellen und füllt sie transaktional neu (`ReadFromAsync`), für SQLite mit `PRAGMA foreign_keys = OFF` + anschließender FK-Prüfung.
- Regressionstests für Alt-Backups: `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs` + `LegacyBackupArchiveBuilder.cs` (Hilfsklasse erzeugt Alt-Archive).
