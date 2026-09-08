# Datenmodell für Unicode-korrekte Namenssuche

## `MediaBaseEntry`
Datei: `VideoWebPlayer/Data/MediaBaseEntry.cs`

Basisklasse für alle Medientypen. Die `Name`-Eigenschaft ist das Ziel der Suche.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| Id | long | Entry-Kennung |
| Name | string | **Anzeigename des Mediums — dies ist die Eigenschaft, auf der die Suche angewendet wird** |
| ReleaseDate | DateTime? | Erscheinungsdatum |
| PremieredAt | DateTime? | Uraufführungsdatum |
| EndedAt | DateTime? | Enddatum |
| MediaSourceId | long | Referenz zur Medienquelle (FK) |
| CollectionId | long | Referenz zur Sammlung |
| CreatedAt | DateTime | Erstellt-Zeitstempel |
| ClassifiedAt | DateTime? | Zuletzt klassifiziert-Zeitstempel |
| Changed | bool | Markierung für Änderungen |
| IsManuallyEdited | bool | Schutz für manuell bearbeitete Metadaten |
| PosterPictureId | long? | FK zu Poster-Bild |
| BannerPictureId | long? | FK zu Banner-Bild |
| FanartPictureId | long? | FK zu Fanart-Bild |
| PosterPicture | Picture? | Navigation zu Poster-Bild |
| BannerPicture | Picture? | Navigation zu Banner-Bild |
| FanartPicture | Picture? | Navigation zu Fanart-Bild |

**Anmerkung:** Die `Name`-Eigenschaft wird in `ItemsController.ApplySearchFilter<T>()` durchsucht. Dies ist die zentrale Stelle für die Anforderung.

## `Movie`
Datei: `VideoWebPlayer/Data/Movie.cs`

Erbt von `MediaBaseEntry`.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| (alle von `MediaBaseEntry`) | - | - |
| MovieCollectionId | long? | Referenz zur übergeordneten MovieCollection (FK) |
| MovieCollection | MovieCollection? | Navigation zur übergeordneten MovieCollection |
| OriginalTitle | string? | Originaltitel |
| Language | string? | Sprache |
| Year | int? | Erscheinungsjahr |
| Country | string? | Land |
| Studios | string? | Studios (kommagetrennt) |
| Director | string? | Regisseur(e) |

## `MovieCollection`
Datei: `VideoWebPlayer/Data/MovieCollection.cs`

Erbt von `MediaBaseEntry`. Container für mehrere Movies.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| (alle von `MediaBaseEntry`) | - | - |

**Anmerkung:** Keine weiteren Eigenschaften als die geerbten. Die Name-Eigenschaft ist hier auch Ziel der Suche.

## `TVShow`
Datei: `VideoWebPlayer/Data/TVShow.cs`

Erbt von `MediaBaseEntry`. Container für Staffeln und Episoden.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| (alle von `MediaBaseEntry`) | - | - |

**Anmerkung:** Keine weiteren Eigenschaften als die geerbten. Die Name-Eigenschaft ist hier auch Ziel der Suche.

## `TVShowSeason`
Datei: `VideoWebPlayer/Data/TVShowSeason.cs`

Erbt von `MediaBaseEntry`. Staffel einer Serie.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| (alle von `MediaBaseEntry`) | - | - |
| TVShowId | long | Referenz zur übergeordneten TVShow (FK) |
| TVShow | TVShow? | Navigation zur übergeordneten TVShow |

**Anmerkung:** Die Name-Eigenschaft ist hier auch Ziel der Suche.

## `TVShowEpisode`
Datei: `VideoWebPlayer/Data/TVShowEpisode.cs`

Erbt von `MediaBaseEntry`. Episode einer Staffel.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| (alle von `MediaBaseEntry`) | - | - |
| Number | int | Episodennummer |
| TVShowSeasonId | long | Referenz zur übergeordneten TVShowSeason (FK) |
| TVShowSeason | TVShowSeason? | Navigation zur übergeordneten TVShowSeason |

**Anmerkung:** Die Name-Eigenschaft ist hier auch Ziel der Suche.

## Zusammenfassung für die Anforderung

Alle fünf genannten Medientypen erben die `Name`-Eigenschaft von `MediaBaseEntry`:
- `Movie`
- `TVShow`
- `TVShowSeason`
- `TVShowEpisode`
- `MovieCollection`

Die `Name`-Eigenschaft ist ein C#-String, wird aber in SQLite als TEXT-Spalte ohne explizite Collation gespeichert. Dies ist der Ausgangspunkt des Problems: Wenn die Suche in `ItemsController.ApplySearchFilter<T>()` durchgeführt wird, wird der Suchbegriff mit `.ToLower()` (C#, vollständige Unicode-Faltung) verarbeitet, aber die Datenbankwerte werden mit SQLites `lower()`-Funktion verglichen, die ohne ICU-Erweiterung unzureichend ist.
