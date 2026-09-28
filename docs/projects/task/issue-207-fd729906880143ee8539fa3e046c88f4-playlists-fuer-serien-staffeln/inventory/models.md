# Verwandte Datenmodelle

## `ApplicationUser`
**Datei:** `VideoWebPlayer/Data/ApplicationUser.cs`

Basisklasse: `IdentityUser`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Sources` | string | Serialisierte Liste von Zugriffquellen |
| `IsAdmin` | bool | Kennzeichen für Admin-Benutzer |

**Status:** Muss um `Playlists`-Navigation erweitert werden.

---

## `ContinueWatchingEntry`
**Datei:** `VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | long | Primärschlüssel |
| `UserId` | string | Benutzerkennzeichen (RQ, FK auf ApplicationUser) |
| `MovieId` | long? | Fremdschlüssel auf Movie (optional) |
| `TVShowEpisodeId` | long? | Fremdschlüssel auf TVShowEpisode (optional) |
| `Position` | TimeSpan | Aktuelle Wiedergabeposition (RQ) |
| `Duration` | TimeSpan? | Gesamtdauer des Media (optional) |
| `UpdatedAt` | DateTime | Zeitstempel der letzten Aktualisierung |
| `ListOrder` | long | Sortierreihenfolge in der Benutzer-Liste |
| `Movie` | Movie? | Navigation zu Movie (optional) |
| `TVShowEpisode` | TVShowEpisode? | Navigation zu TVShowEpisode (optional) |

**Status:** Muss um `PlaylistId`-Eigenschaft (long?, FK auf Playlist) und `Playlist`-Navigation erweitert werden.

---

## `Movie`
**Datei:** `VideoWebPlayer/Data/Movie.cs`

Basisklasse: `MediaBaseEntry`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `MovieCollectionId` | long? | Fremdschlüssel auf MovieCollection (optional) |
| `MovieCollection` | MovieCollection? | Navigation zu MovieCollection |
| `OriginalTitle` | string? | Originaltitel (optional) |
| `Language` | string? | Sprache (optional) |
| `Year` | int? | Veröffentlichungsjahr (optional) |
| `Country` | string? | Land (optional) |
| `Studios` | string? | Studios (kommagetrennt, optional) |
| `Director` | string? | Regisseur (optional) |
| `Credits` | string? | Credits (kommagetrennt, optional) |
| `Plot` | string? | Handlungsbeschreibung (optional) |
| `ActorsClassifiedAt` | DateTime? | Zeitstempel der Schauspieler-Klassifikation (optional) |
| `MovieMediaItems` | ICollection<MovieMediaItem> | Sammlung von Media-Items |
| `GenreNames` | string? | Genre-Namen (kommagetrennt, optional) |
| `MovieGenres` | ICollection<MovieGenre> | Navigation zu Genres |
| `MovieActors` | ICollection<MovieActor> | Navigation zu Schauspielern |

**Status:** Vorhanden, kann als Playlist-Item referenziert werden.

---

## `TVShow`
**Datei:** `VideoWebPlayer/Data/TVShow.cs`

Basisklasse: `MediaBaseEntry`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Seasons` | ICollection<TVShowSeason> | Sammlung von Staffeln |
| `OriginalName` | string? | Originalname (optional) |
| `Language` | string? | Sprache (optional) |
| `Plot` | string? | Handlungsbeschreibung (optional) |
| `Status` | string? | Status (optional) |
| `Studio` | string? | Studio (optional) |
| `GenreNames` | string? | Genre-Namen (kommagetrennt, optional) |
| `TVShowGenres` | ICollection<TVShowGenre> | Navigation zu Genres |

**Status:** Vorhanden, kann als Playlist-Item referenziert werden.

---

## `TVShowSeason`
**Datei:** `VideoWebPlayer/Data/TVShowSeason.cs`

Basisklasse: `MediaBaseEntry`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `TVShowId` | long | Fremdschlüssel auf TVShow (RQ) |
| `TVShow` | TVShow | Navigation zu TVShow (RQ) |
| `Episodes` | ICollection<TVShowEpisode> | Sammlung von Episoden |

**Status:** Vorhanden, kann als Playlist-Item referenziert werden.

---

## `TVShowEpisode`
**Datei:** `VideoWebPlayer/Data/TVShowEpisode.cs`

Basisklasse: `MediaBaseEntry`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Number` | int | Episodennummer (RQ) |
| `TVShowSeasonId` | long | Fremdschlüssel auf TVShowSeason (RQ) |
| `TVShowSeason` | TVShowSeason | Navigation zu TVShowSeason (RQ) |
| `Plot` | string? | Handlungsbeschreibung (optional) |
| `GeneratedBackgroundPictureId` | long? | FK auf generiertes Background-Bild (optional) |
| `BackgroundImageRequiresUpdate` | bool | Kennzeichen für Bild-Regeneration erforderlich |
| `BackgroundImageGeneratedAt` | DateTime? | Zeitstempel der letzten Generierung (optional) |
| `GeneratedBackgroundPicture` | Picture? | Navigation zu generiertem Bild (optional) |
| `ActorsClassifiedAt` | DateTime? | Zeitstempel der Schauspieler-Klassifikation (optional) |
| `TVShowEpisodeMediaItems` | ICollection<TVShowEpisodeMediaItem> | Sammlung von Media-Items |
| `TVShowEpisodeActors` | ICollection<TVShowEpisodeActor> | Sammlung von Schauspielern |

**Status:** Vorhanden, kann als Playlist-Item referenziert werden.

---

## `MovieCollection`
**Datei:** `VideoWebPlayer/Data/MovieCollection.cs`

Basisklasse: `MediaBaseEntry`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Movies` | ICollection<Movie> | Sammlung von Filmen |

**Status:** Vorhanden, kann als Playlist-Item referenziert werden.

---

## `Genre`
**Datei:** `VideoWebPlayer/Data/Genre.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | long | Primärschlüssel |
| `Name` | string | Genre-Name |
| `MediaSourceId` | long | Fremdschlüssel auf MediaSource (RQ) |
| `MediaSource` | MediaSource | Navigation zu MediaSource |
| `StartDate` | DateTime? | Optionales Sichtbarkeitsstartdatum (optional) |
| `EndDate` | DateTime? | Optionales Sichtbarenddatum (optional) |
| `AlternateNames` | ICollection<GenreName> | Sammlung alternativer Namen |
| `MovieGenres` | ICollection<MovieGenre> | Navigation zu Movie-Genres |
| `TVShowGenres` | ICollection<TVShowGenre> | Navigation zu TVShow-Genres |
| `IsHidden` | bool | Kennzeichen für verstecktes Genre |

**Status:** Vorhanden, wird für Playlist-Genre-Verknüpfungen benötigt.

---

## `Picture`
**Datei:** `VideoWebPlayer/Data/Picture.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | long | Primärschlüssel |
| `MediaItemId` | long? | Fremdschlüssel auf MediaItem (optional) |
| `Type` | string | Bildtyp (z.B. "poster", "banner", "fanart", "thumb") |
| `Width` | int? | Bildbreite (optional) |
| `Height` | int? | Bildhöhe (optional) |
| `Description` | string? | Optionale Beschreibung (optional) |
| `MediaItem` | MediaItem? | Navigation zu MediaItem (optional) |
| `Data` | byte[] | Bilddaten |
| `ContentType` | string | MIME-Typ |
| `IsGeneratedBackground` | bool | Kennzeichen für generiertes Hintergrundbild |
| `EpisodeId` | long? | Fremdschlüssel auf Episode (optional) |

**Status:** Vorhanden, kann für Playlist-Bilder verwendet werden (über `Playlist.PictureId`).

---

## `MediaBaseEntry`
**Datei:** `VideoWebPlayer/Data/MediaBaseEntry.cs`

Basisklasse für `TVShow`, `TVShowSeason`, `TVShowEpisode`, `Movie`, `MovieCollection`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | long | Primärschlüssel |
| `Name` | string? | Titel/Name (optional) |
| `Description` | string? | Beschreibung (optional) |
| `PremieredAt` | DateTime? | Premiere-Datum (optional) |
| `ReleaseDate` | DateTime? | Veröffentlichungsdatum (optional) |
| `EndedAt` | DateTime? | Enddatum (optional) |
| `Rating` | decimal? | Bewertung (optional) |
| `Votes` | long? | Stimmenanzahl (optional) |
| `MediaSourceId` | long | Fremdschlüssel auf MediaSource |

**Status:** Vorhanden, dient als Basis für Video-Inhaltstypen.
