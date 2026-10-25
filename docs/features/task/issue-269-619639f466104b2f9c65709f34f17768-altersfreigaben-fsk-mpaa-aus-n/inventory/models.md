# Datenmodell — Bestandsaufnahme

Bezug: `requirement.md` — persistierte Altersfreigabe für `Movie` und `TVShow` (normalisierter Wert plus Rohwert), Erweiterungspunkt `LoadFromXml(XElement)`.

**Befund:** Es existiert **kein** Feld für Altersfreigabe im gesamten Datenmodell (Suche nach `mpaa`, `certification`, `fsk`, `agerating`, `altersfreigabe` findet außerhalb der Aufgaben-Dokumente keine Treffer im Code).

## `MediaBaseEntry`
Datei: `VideoWebPlayer/Data/MediaBaseEntry.cs`

Abstrakte Basisklasse für `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode` und `MovieCollection`. Möglicher Ort für eine gemeinsame Freigabe-Eigenschaft (Offene Frage 1 der Anforderung).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` | Anzeigename |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum (Film/Episode) |
| `PremieredAt` | `DateTime?` | Premierendatum (Serie/Staffel) |
| `EndedAt` | `DateTime?` | Enddatum (vom Scan aus Release/Premiered aggregiert) |
| `MediaSourceId` | `long` | Zugehörige Medienquelle |
| `CollectionId` | `long` | Stabile Herkunfts-`MediaCollection` |
| `CreatedAt` | `DateTime` | Anlagezeitpunkt |
| `ClassifiedAt` | `DateTime?` | Zeitpunkt der letzten Klassifizierung |
| `Changed` | `bool` | Änderungsflag — steuert Re-Klassifizierung beim Scan |
| `IsManuallyEdited` | `bool` | **Eintragsweiter Schutz**: Scan überschreibt keine Metadaten, wenn gesetzt (Anforderung „kein Überschreiben manuell gepflegter Werte") |
| `PosterPictureId` / `BannerPictureId` / `FanartPictureId` | `long?` | Bild-Verweise |
| `PosterPicture` / `BannerPicture` / `FanartPicture` | `Picture?` | Navigationen |

## `Movie`
Datei: `VideoWebPlayer/Data/Movie.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MovieCollectionId` / `MovieCollection` | `long?` / `MovieCollection?` | Zugehörige Filmsammlung |
| `OriginalTitle` | `string?` | NFO `<originaltitle>` |
| `Language` | `string?` | NFO `<language>` |
| `Year` | `int?` | NFO `<year>` |
| `Country` | `string?` | NFO `<country>` |
| `Studios` | `string?` | NFO `<studio>` (kommagetrennt) |
| `Director` | `string?` | NFO `<director>` |
| `Credits` | `string?` | NFO `<credits>` (kommagetrennt) |
| `Plot` | `string?` | NFO `<plot>` |
| `ActorsClassifiedAt` | `DateTime?` | Markierung für Schauspieler-Nacherfassung (Vorbild für einen evtl. Rating-Backfill) |
| `GenreNames` | `string?` | NFO `<genre>` (kommagetrennt, kanonisiert) |
| `MovieMediaItems`, `MovieGenres`, `MovieActors` | `ICollection<...>` | Verknüpfungstabellen |
| `MediaItems` | `IEnumerable<MediaItem>` | Komfort-Property, von EF ignoriert (`MovieConfiguration`) |

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadFromXml(XElement)` | `public` | Liest `originaltitle`, `language`, `year`, `releasedate`, `premiered`, `country`, `genre`, `studio`, `director`, `credits`, `plot` — **liest kein `<mpaa>`- oder `<certification>`-Element**. Aufgerufen aus `MediaSourceClassifier.ProcessCollectionAsMovieAsync` (Zeilen 848, 861). |

## `TVShow`
Datei: `VideoWebPlayer/Data/TVShow.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Seasons` | `ICollection<TVShowSeason>` | Staffeln |
| `OriginalName` | `string?` | tvshow.nfo `<title>` |
| `Language` | `string?` | tvshow.nfo `<language>` |
| `Plot` | `string?` | tvshow.nfo `<plot>` |
| `Status` | `string?` | tvshow.nfo `<status>` |
| `Studio` | `string?` | tvshow.nfo `<studio>` |
| `GenreNames` | `string?` | tvshow.nfo `<genre>` (kommagetrennt) |
| `TVShowGenres` | `ICollection<TVShowGenre>` | Genre-Verknüpfungen |

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadFromXml(XElement)` | `public` | Liest `title`, `language`, `plot`, `status`, `studio`, `genre`, `premiered` via `GetElementValue` — **liest kein `<mpaa>`- oder `<certification>`-Element**. Aufgerufen aus `MediaSourceClassifier.CreateOrUpdateTVShow` (Zeilen 503, 515). |
| `GetElementValue(XElement, string)` | `private` | Hilfsmethode: Elementwert oder `string.Empty` |

## `TVShowEpisode`
Datei: `VideoWebPlayer/Data/TVShowEpisode.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Number` | `int` | Episodennummer |
| `TVShowSeasonId` / `TVShowSeason` | `long` / `TVShowSeason` | Zugehörige Staffel |
| `Plot` | `string?` | Episoden-Handlung |
| `GeneratedBackgroundPictureId` + `BackgroundImage*` | `long?`/`bool`/`DateTime?` | Generiertes Hintergrundbild |
| `ActorsClassifiedAt` | `DateTime?` | Markierung Schauspieler-Nacherfassung |
| `TVShowEpisodeMediaItems`, `TVShowEpisodeActors` | `ICollection<...>` | Verknüpfungen |

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadFromXml(XElement)` | `public` | Liest `episode`, `aired`, `premiered`, `plot`. **Wird derzeit von keiner Stelle aufgerufen** (kein `LoadFromXml`-Aufruf für Episoden im `MediaSourceClassifier` — die Felder werden dort inline in `ProcessEpisodesForTVShowAsync`, Zeilen 654–684, gesetzt). Kein `<mpaa>`/`<certification>`. |

## `TVShowSeason`
Datei: `VideoWebPlayer/Data/TVShowSeason.cs`

Nur `TVShowId`, `TVShow`, `Episodes` plus geerbte `MediaBaseEntry`-Eigenschaften. Kein `LoadFromXml`.

## `MovieCollection`
Datei: `VideoWebPlayer/Data/MovieCollection.cs`

Nur `Movies` (`ICollection<Movie>`) plus geerbte `MediaBaseEntry`-Eigenschaften. Kein `LoadFromXml`.

## `MediaItem` / `MediaEntry`
Dateien: `VideoWebPlayer/Data/MediaItem.cs`, `VideoWebPlayer/Data/MediaEntry.cs`

`MediaItem` (die physische Datei, Basisklasse `MediaEntry`) besitzt `MediaCollectionId`, `Duration`, `DurationProbedAt`, `DurationProbeAttempts`. `MediaEntry` trägt `Id`, `Name`, `Path`, `CreatedAt`, `ClassifiedAt`, `Changed`. **Keine Freigabe-Felder** — das Issue nennt `MediaItem` nur als alternative Orientierung; die übersetzte Anforderung sieht die Felder bei `Movie`/`TVShow` vor.

## `MediaCollection`
Datei: `VideoWebPlayer/Data/MediaCollection.cs`

Verzeichnis-Ebene beim Scan: `MediaSourceId`, `ParentMediaCollectionId`/`ParentMediaCollection`, `ChildCollections`, `MediaItems`, `LastScannedAt`, `ScanDueAt`, `Classifyable`, `Skip` (intern). Keine NFO-Felder.

## `Setup`
Datei: `VideoWebPlayer/Data/Setup.cs`

Globaler Ein-Satz-Admin-Einstellungsspeicher (`DbSet<Setup> Setups`, Single-Row-Index via Migration `AddSetupsSingleRowIndex`). Relevant für Offene Frage 4 (Ablage eines TMDB-API-Keys zur Laufzeit): `Id`, `DataVersion`, `GenresChanged`, `ApplicationTitle`, `ScanProcessIntervalMinutes`, `MediaCollectionScanIntervalDays`, `ContinueWatchingEndThresholdSeconds`, `ActorCollectionThresholdPercent`, `MdnsAdvertisementEnabled`, `DiscoveryPublicBaseUrl`, `PlaylistBackfillLastSweepAt`, `TvSenderEnabled`, `HolidayFederalState`, `ProgramScheduleHorizonHours`, `ProgramScheduleIntervalMinutes`, `LiveStreamSegmentLengthSeconds`, `LiveStreamSegmentDirectory`, `LiveStreamSegmentRetentionMinutes`. Kein API-Key-/Extern-Dienst-Feld vorhanden.

## `ApplicationDbContext`
Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`

Relevante DbSets: `Movies`, `TVShows`, `TVShowSeasons`, `TVShowEpisodes`, `MovieCollections`, `MovieMediaItems`, `TVShowEpisodeMediaItems`, `Genres`, `MovieGenres`, `TVShowGenres`, `Actors`, `MovieActors`, `TVShowEpisodeActors`, `Pictures`, `Setups`. EF-Konfiguration je Entity unter `VideoWebPlayer/Data/Configurations/` (`MovieConfiguration` ignoriert `MediaItems`, FK `MovieCollectionId`).

## EF-Migrationen
Verzeichnis: `VideoWebPlayer/Migrations/` — letzte Migration `20261025112747_AddProgramClipGroups` plus `ApplicationDbContextModelSnapshot.cs`. Migrationen werden beim Start über `app.MigrateDatabase()` (`VideoWebPlayer/Extensions/WebApplicationExtensions.cs:23`, aufgerufen in `VideoWebPlayer/Program.cs:47`) angewendet.

## Backup-Restauration (`VideoWebPlayerBackupData`)
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `OptionalRestoreTables` (Zeilen 23–49): Tabellen, die in Alt-Backups fehlen dürfen.
- `OptionalRestoreColumns` (Zeilen 51–100): `Tabelle.Spalte`-Einträge (case-insensitiv), die in Alt-Backups fehlen dürfen — u. a. `Movies.IsManuallyEdited`, `Movies.ActorsClassifiedAt`, `TVShowEpisodes.ActorsClassifiedAt`, `MovieMediaItems.IsTrailer`. **Hier müssen neue Freigabe-Spalten eingetragen werden** (`IsOptionalRestoreColumn`, Zeile 1107).
- `OptionalRestoreBoolDefaults` (ab Zeile 110): Default-Werte für fehlende bool-Spalten.
- Validierung in `ReadFromAsync`/Index-Prüfung (Zeilen 556–594): fehlende nicht-optionale Tabellen/Spalten erzeugen Fehler.

## Konfiguration (Options-Klassen)
Verzeichnis: `VideoWebPlayer/Configuration/` — vorhandene Optionsmuster: `MediaProbeOptions`, `PlaylistSettings`, `EpisodeBackgroundImageOptions` (mit `IValidateOptions`-Validator), `MdnsOptions`, `DiscoveryOptions`, `LiveStreamOptions`, `ProgramScheduleOptions`. Registrierung in `ServiceCollectionExtensions.AddVideoWebPlayerServices` (`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`, u. a. `services.Configure<MediaProbeOptions>(configuration.GetSection("MediaProbe"))` Zeile 240, `AddOptions<...>().Bind(...).ValidateOnStart()` Zeilen 303–314). Abschnitte in `VideoWebPlayer/appsettings.json`: `Playlists`, `Backups`, `EpisodeBackgroundImage`, `Mdns`, `Discovery`, `MediaProbe`, `LiveStream`, `ProgramSchedule`, `AutoUpdate.ProtectedFiles`-`JsonKeys`-Liste (Zeilen 147–199, dort müssen ggf. neue update-geschützte Keys eingetragen werden). **Keine TMDB-/IMDb- oder API-Key-Optionsklasse vorhanden.** Secrets-Leitlinie: `docs/SECRETS_MANAGEMENT.md` (keine Schlüssel im Repository; `appsettings.Local.json` als update-sichere Ablage).
