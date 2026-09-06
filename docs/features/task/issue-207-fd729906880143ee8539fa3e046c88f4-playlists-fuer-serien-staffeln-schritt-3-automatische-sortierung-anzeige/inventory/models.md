# Datenmodelle

## MediaBaseEntry (Basis für alle Medientypen)

Datei: `VideoWebPlayer/Data/MediaBaseEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige Identität des Medien-Eintrags |
| `Name` | `string` | Anzeigename |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum |
| `PremieredAt` | `DateTime?` | Premiere-Datum |
| `EndedAt` | `DateTime?` | Endatum (für Serien) |
| `MediaSourceId` | `long` | **KRITISCH:** ID der zugrunde liegenden Mediaquelle. Dies ist der Schlüssel für `MediaSourceUsers`-Abfragen. |
| `CollectionId` | `long` | Collection-Identifier |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel |
| `ClassifiedAt` | `DateTime?` | Zeitstempel der letzten Klassifizierung |
| `Changed` | `bool` | Flag für Änderungsmarkierung |
| `IsManuallyEdited` | `bool` | Flag für manuell bearbeitete Metadaten |
| `PosterPictureId` | `long?` | Poster-Bild-ID |
| `BannerPictureId` | `long?` | Banner-Bild-ID |
| `FanartPictureId` | `long?` | Fanart-Bild-ID |

**Wichtig:** Alle fünf unterstützten Medientypen erben von `MediaBaseEntry` und haben damit einen `MediaSourceId`.

---

## Movie (Film)

Datei: `VideoWebPlayer/Data/Movie.cs` (erbt von `MediaBaseEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MovieCollectionId` | `long?` | **HIERARCHIE:** ID der übergeordneten Filmsammlung. Ein Film gehört zu einer Collection, die die Freischaltung definiert. |
| `MovieCollection` | `MovieCollection?` | Navigation zu übergeordneter Filmsammlung |
| `OriginalTitle` | `string?` | Originaltitel |
| `Language` | `string?` | Sprache |
| `Year` | `int?` | Veröffentlichungsjahr |
| `Country` | `string?` | Land |
| `Studios` | `string?` | Studios (kommagetrennt) |
| `Director` | `string?` | Regisseur |
| `Credits` | `string?` | Credits (kommagetrennt) |
| `Plot` | `string?` | Handlungsbeschreibung |
| `ActorsClassifiedAt` | `DateTime?` | Zeitstempel der Actor-Klassifizierung |
| `MovieMediaItems` | `ICollection<MovieMediaItem>` | Verknüpfung zu Media-Items (Dateien) |
| `MovieGenres` | `ICollection<MovieGenre>` | Verknüpfung zu Genres |
| `MovieActors` | `ICollection<MovieActor>` | Verknüpfung zu Schauspielern |

**Hierarchie für Zugriffsprüfung:** Film → **MovieCollection** (der MovieCollection wird dann freigeschaltet)

---

## MovieCollection (Filmsammlung)

Datei: `VideoWebPlayer/Data/MovieCollection.cs` (erbt von `MediaBaseEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Movies` | `ICollection<Movie>` | Sammlung der Filme in dieser Collection |

**Hierarchie für Zugriffsprüfung:** **Direkt** (kann eigenständig freigeschaltet werden)

---

## TVShow (Fernsehserie)

Datei: `VideoWebPlayer/Data/TVShow.cs` (erbt von `MediaBaseEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Seasons` | `ICollection<TVShowSeason>` | Sammlung der Staffeln dieser Serie |
| `OriginalName` | `string?` | Originaltitel |
| `Language` | `string?` | Sprache |
| `Plot` | `string?` | Handlung |
| `Status` | `string?` | Serie-Status (aktiv, beendet, etc.) |
| `Studio` | `string?` | Studio |
| `GenreNames` | `string?` | Genres (kommagetrennt) |
| `TVShowGenres` | `ICollection<TVShowGenre>` | Verknüpfung zu Genres |

**Hierarchie für Zugriffsprüfung:** **Direkt** (kann eigenständig freigeschaltet werden)

---

## TVShowSeason (Staffel)

Datei: `VideoWebPlayer/Data/TVShowSeason.cs` (erbt von `MediaBaseEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TVShowId` | `long` | **HIERARCHIE:** ID der übergeordneten Serie. Eine Staffel gehört zu einer Serie, deren Freischaltung gilt. |
| `TVShow` | `TVShow` | Navigation zu übergeordneter Serie |
| `Episodes` | `ICollection<TVShowEpisode>` | Sammlung der Episoden dieser Staffel |

**Hierarchie für Zugriffsprüfung:** Staffel → **TVShow** (die TVShow wird freigeschaltet)

---

## TVShowEpisode (Episode)

Datei: `VideoWebPlayer/Data/TVShowEpisode.cs` (erbt von `MediaBaseEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Number` | `int` | Episodennummer in der Staffel |
| `TVShowSeasonId` | `long` | **HIERARCHIE:** ID der übergeordneten Staffel. Eine Episode gehört zu einer Staffel, deren Serie die Freischaltung definiert. |
| `TVShowSeason` | `TVShowSeason` | Navigation zu übergeordneter Staffel |
| `Plot` | `string?` | Handlung der Episode |
| `GeneratedBackgroundPictureId` | `long?` | ID des generierten Hintergrunds |
| `BackgroundImageRequiresUpdate` | `bool` | Flag für Hintergrund-Neuberechnung |
| `BackgroundImageGeneratedAt` | `DateTime?` | Zeitstempel der Hintergrund-Generierung |
| `GeneratedBackgroundPicture` | `Picture?` | Navigation zum generierten Hintergrund |
| `ActorsClassifiedAt` | `DateTime?` | Zeitstempel der Actor-Klassifizierung |
| `TVShowEpisodeMediaItems` | `ICollection<TVShowEpisodeMediaItem>` | Verknüpfung zu Media-Items (Dateien) |
| `TVShowEpisodeActors` | `ICollection<TVShowEpisodeActor>` | Verknüpfung zu Schauspielern |

**Hierarchie für Zugriffsprüfung:** Episode → **Staffel** → **TVShow** (die TVShow wird freigeschaltet)

---

## MediaSourceUser (Regulärer Quellenzugriff)

Datei: `VideoWebPlayer/Data/MediaSourceUser.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceId` | `long` | ID der Mediaquelle |
| `MediaSource` | `MediaSource` | Navigation zur Mediaquelle |
| `UserId` | `string` | ID des Benutzers |
| `User` | `ApplicationUser` | Navigation zum Benutzer |

**Zweck:** Diese Tabelle definiert den regulären Quellenzugriff. Ein Benutzer mit einer `MediaSourceUser`-Eintrag für eine Quelle darf auf ALLE Medien dieser Quelle zugreifen, unabhängig von individuellen Freischaltungen.

---

## UnlockedMediaEntry (Individuelle Freischaltung)

Basierend auf Nutzung in `UnlockedMediaService.cs`:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `UserId` | `string` | ID des Benutzers |
| `MovieCollectionId` | `long?` | ID der freigeschalteten Filmsammlung (oder null) |
| `TVShowId` | `long?` | ID der freigeschalteten Serie (oder null) |
| `CreatedAt` | `DateTime` | Zeitstempel der Freischaltung |

**Zweck:** Diese Tabelle definiert individuelle Freischaltungen. Ein Benutzer mit einem Eintrag darf auf die spezifische `MovieCollection` ODER `TVShow` zugreifen, UNABHÄNGIG von Quellenzugriff.

**Wichtig:** Nur `MovieCollection` und `TVShow` können freigeschaltet werden. `Movie`, `TVShowSeason` und `TVShowEpisode` werden über ihre übergeordneten Medien freigeschaltet (Hierarchie-Auflösung).
