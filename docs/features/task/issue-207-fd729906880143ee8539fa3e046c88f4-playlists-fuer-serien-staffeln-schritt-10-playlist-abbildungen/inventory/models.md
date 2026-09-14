# Datenmodelle – Bestandsaufnahme

## `Picture`
Datei: `VideoWebPlayer/Data/Picture.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `MediaItemId` | `long?` | Fremdschlüssel zu MediaItem |
| `Type` | `string` | Bildtyp (z.B. "poster", "banner", "fanart", "thumb") |
| `Width` | `int?` | Bildbreite in Pixeln |
| `Height` | `int?` | Bildhöhe in Pixeln |
| `Description` | `string?` | Optionale Beschreibung |
| `Data` | `byte[]` | Bilddaten (Binärdaten) |
| `ContentType` | `string` | MIME-Typ (z.B. "image/jpeg", "image/png") |
| `IsGeneratedBackground` | `bool` | Kennzeichnet, ob Bild von `EpisodeBackgroundImageGenerator` erzeugt wurde |
| `EpisodeId` | `long?` | Fremdschlüssel zu TVShowEpisode (für automatisch erzeugte Episoden-Hintergrundbilder) |
| `MediaItem` | `MediaItem?` | Navigationseigenschaft zu MediaItem |

**Bemerkungen:**
- Picture wird als generische Tabelle zur Speicherung aller Bilddaten verwendet
- `EpisodeId` ist das aktuelle Muster für Fremdschlüsselreferenzen zu Entitäten, die Bilder benötigen
- `IsGeneratedBackground` wird zur Kennzeichnung automatisch erzeugter Bilder genutzt
- Für Playlist-Cover müsste ein ähnliches Muster etabliert werden (entweder `PlaylistId` oder `IsPlaylistCover` + `PlaylistId`)

## `MediaBaseEntry`
Datei: `VideoWebPlayer/Data/MediaBaseEntry.cs` (Basisklasse für alle Medien-Entitäten)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` | Anzeigename |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum |
| `PremieredAt` | `DateTime?` | Premiere-Datum |
| `EndedAt` | `DateTime?` | Enddatum |
| `MediaSourceId` | `long` | Fremdschlüssel zu MediaSource |
| `CollectionId` | `long` | Sammlungs-ID |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel |
| `ClassifiedAt` | `DateTime?` | Zeitstempel der letzten Klassifizierung |
| `Changed` | `bool` | Kennzeichnet, ob Eintrag geändert wurde |
| `IsManuallyEdited` | `bool` | Kennzeichnet, ob Metadaten manuell bearbeitet wurden |
| `PosterPictureId` | `long?` | Fremdschlüssel zu Picture (Poster-Bild) |
| `BannerPictureId` | `long?` | Fremdschlüssel zu Picture (Banner-Bild) |
| `FanartPictureId` | `long?` | Fremdschlüssel zu Picture (Fanart-Bild) |
| `PosterPicture` | `Picture?` | Navigationseigenschaft zu Poster-Picture |
| `BannerPicture` | `Picture?` | Navigationseigenschaft zu Banner-Picture |
| `FanartPicture` | `Picture?` | Navigationseigenschaft zu Fanart-Picture |

## `Movie`
Datei: `VideoWebPlayer/Data/Movie.cs`

Erbt von `MediaBaseEntry`. Zusätzliche Eigenschaften:
| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MovieCollectionId` | `long?` | Fremdschlüssel zu MovieCollection (Zugehörigkeit zu Sammlung) |
| `OriginalTitle` | `string?` | Originaltitel |
| `Language` | `string?` | Sprache |
| `Year` | `int?` | Produktionsjahr |
| `Country` | `string?` | Produktionsland |
| `Studios` | `string?` | Studios (kommagetrennt) |
| `Director` | `string?` | Regisseur |
| `Credits` | `string?` | Credits (kommagetrennt) |
| `Plot` | `string?` | Handlungsbeschreibung |
| `ActorsClassifiedAt` | `DateTime?` | Zeitstempel der letzten Schauspieler-Klassifizierung |
| `GenreNames` | `string?` | Genre-Namen (kommagetrennt) |
| `MovieMediaItems` | `ICollection<MovieMediaItem>` | Verknüpfung zu MediaItems |
| `MovieGenres` | `ICollection<MovieGenre>` | Verknüpfung zu Genres |
| `MovieActors` | `ICollection<MovieActor>` | Verknüpfung zu Schauspielern |

**Poster-Referenzen:** Nutzt geerbte `PosterPictureId` und `PosterPicture` aus `MediaBaseEntry`

## `TVShow`
Datei: `VideoWebPlayer/Data/TVShow.cs`

Erbt von `MediaBaseEntry`. Zusätzliche Eigenschaften:
| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OriginalName` | `string?` | Originalname |
| `Language` | `string?` | Sprache |
| `Plot` | `string?` | Handlungsbeschreibung |
| `Status` | `string?` | Status (laufend, beendet, etc.) |
| `Studio` | `string?` | Studio-Name |
| `GenreNames` | `string?` | Genre-Namen (kommagetrennt) |
| `Seasons` | `ICollection<TVShowSeason>` | Navigationseigenschaft zu Staffeln |
| `TVShowGenres` | `ICollection<TVShowGenre>` | Verknüpfung zu Genres |

**Poster-Referenzen:** Nutzt geerbte `PosterPictureId` und `PosterPicture` aus `MediaBaseEntry`

## `TVShowSeason`
Datei: `VideoWebPlayer/Data/TVShowSeason.cs`

Erbt von `MediaBaseEntry`. Zusätzliche Eigenschaften:
| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TVShowId` | `long` | Fremdschlüssel zu TVShow (Zugehörigkeit zur Serie) |
| `TVShow` | `TVShow` | Navigationseigenschaft zur Serie |
| `Episodes` | `ICollection<TVShowEpisode>` | Navigationseigenschaft zu Episoden |

**Poster-Referenzen:** Nutzt geerbte `PosterPictureId` und `PosterPicture` aus `MediaBaseEntry`

## `TVShowEpisode`
Datei: `VideoWebPlayer/Data/TVShowEpisode.cs`

Erbt von `MediaBaseEntry`. Zusätzliche Eigenschaften:
| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Number` | `int` | Episodennummer |
| `TVShowSeasonId` | `long` | Fremdschlüssel zu TVShowSeason |
| `TVShowSeason` | `TVShowSeason` | Navigationseigenschaft zur Staffel |
| `Plot` | `string?` | Handlungsbeschreibung |
| `GeneratedBackgroundPictureId` | `long?` | Fremdschlüssel zu Picture (automatisch erzeugtes Hintergrundbild) |
| `BackgroundImageRequiresUpdate` | `bool` | Kennzeichnet, ob Hintergrundbild neu erzeugt werden muss |
| `BackgroundImageGeneratedAt` | `DateTime?` | Zeitstempel der letzten Hintergrundbild-Generierung |
| `GeneratedBackgroundPicture` | `Picture?` | Navigationseigenschaft zum generierten Hintergrundbild |
| `ActorsClassifiedAt` | `DateTime?` | Zeitstempel der letzten Schauspieler-Klassifizierung |
| `TVShowEpisodeMediaItems` | `ICollection<TVShowEpisodeMediaItem>` | Verknüpfung zu MediaItems |
| `TVShowEpisodeActors` | `ICollection<TVShowEpisodeActor>` | Verknüpfung zu Schauspielern |

**Poster-Referenzen:** Nutzt geerbte `PosterPictureId` und `PosterPicture` aus `MediaBaseEntry`
**Bemerkungen:** Das Muster mit `GeneratedBackgroundPictureId` und `BackgroundImageRequiresUpdate` könnte als Vorbild für Playlist-Cover dienen

## `MovieCollection`
Datei: `VideoWebPlayer/Data/MovieCollection.cs`

Erbt von `MediaBaseEntry`. Zusätzliche Eigenschaften:
| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Movies` | `ICollection<Movie>` | Navigationseigenschaft zu Filmen in der Sammlung |

**Poster-Referenzen:** Nutzt geerbte `PosterPictureId` und `PosterPicture` aus `MediaBaseEntry`

## `Playlist`
Datei: `VideoWebPlayer/Data/Playlist.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `UserId` | `string` (required) | Fremdschlüssel zu ApplicationUser (Besitzer) |
| `Name` | `string` (required, max 255) | Playlist-Name |
| `Description` | `string?` (max 2000) | Optionale Beschreibung |
| `SortMode` | `PlaylistSortMode` | Sortiermodus (ByReleaseDate oder Manual) |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel |
| `UpdatedAt` | `DateTime` | Letzter Änderungszeitstempel |
| `GenresManuallyOverridden` | `bool` | Kennzeichnet, ob Genres manuell überschrieben wurden (Schritt 9) |
| `PlaylistEntries` | `ICollection<PlaylistEntry>` | Navigationseigenschaft zu Einträgen |

**Bemerkungen zu Schritt 10:**
- Noch KEINE `CoverPictureId` und `CoverPictureIsUserUploaded` Properties vorhanden
- Diese müssen neu hinzugefügt werden
- Das `GenresManuallyOverridden` Flag aus Schritt 9 zeigt das etablierte Muster für "manuell überschrieben" Flags

## `PlaylistEntry`
Datei: `VideoWebPlayer/Data/PlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `PlaylistId` | `long` | Fremdschlüssel zu Playlist |
| `Playlist` | `Playlist` | Navigationseigenschaft zur Playlist |
| `MediaType` | `string` (required) | Medientyp des Eintrags (z.B. "Movie", "TVShow", "TVShowSeason", "TVShowEpisode", "MovieCollection") |
| `MediaId` | `long` | ID der referenzierten Media-Entität |
| `ParentMediaType` | `string?` | Medientyp der Sammlung, aus der dieser Eintrag hinzugefügt wurde (z.B. "TVShow", "TVShowSeason", "MovieCollection") |
| `ParentMediaId` | `long?` | ID der Sammlung, aus der dieser Eintrag hinzugefügt wurde |
| `AddedAt` | `DateTime` | Zeitstempel, wann Eintrag zur Playlist hinzugefügt wurde |
| `SortOrder` | `long?` | Manuelle Sortierreihenfolge (nur bei Manual-SortMode) |

**Bemerkungen:**
- Das `MediaType`/`MediaId` Muster wird zur Abfrage der verfügbaren Bilder für Collage-Generierung genutzt
- Die Prioritätsreihenfolge der Medientypen ist: TVShow → TVShowEpisode → MovieCollection → Movie

## `ApplicationDbContext` (Auszug)
Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`

DbSet Properties (relevant für Schritt 10):
| DbSet-Property | Typ | Beschreibung |
|---|---|---|
| `Pictures` | `DbSet<Picture>` | Alle Bilder |
| `Playlists` | `DbSet<Playlist>` | Alle Playlists |
| `PlaylistEntries` | `DbSet<PlaylistEntry>` | Alle Playlist-Einträge |
| `Movies` | `DbSet<Movie>` | Alle Filme |
| `TVShows` | `DbSet<TVShow>` | Alle Serien |
| `TVShowSeasons` | `DbSet<TVShowSeason>` | Alle Staffeln |
| `TVShowEpisodes` | `DbSet<TVShowEpisode>` | Alle Episoden |
| `MovieCollections` | `DbSet<MovieCollection>` | Alle Film-Sammlungen |

