# Datenmodelle

## `PlaylistEntry`
Datei: `VideoWebPlayer/Data/PlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige Kennung des Eintrags |
| `PlaylistId` | `long` | Fremdschlüssel zur zugehörigen Playlist |
| `Playlist` | `Playlist` | Navigation zur zugehörigen Playlist |
| `MediaType` | `string` | Medientyp (z. B. "Movie", "TVShowEpisode") – normalisiert nach `MediaType` enum |
| `MediaId` | `long` | Kennung des referenzierten Mediainhalts |
| `ParentMediaType` | `string?` | Medientyp der Sammlung, durch die dieser Eintrag hinzugefügt wurde (z. B. "TVShow", "TVShowSeason", "MovieCollection"), oder `null` für Top-Level-Einträge |
| `ParentMediaId` | `long?` | Kennung der Sammlung, oder `null` für Top-Level-Einträge |
| `AddedAt` | `DateTime` | Zeitstempel, wenn der Eintrag zur Playlist hinzugefügt wurde |

**Zweck:** Speichert einzelne Medieneinträge, die in einer Playlist enthalten sind. Die Felder `ParentMediaType` und `ParentMediaId` ermöglichen die Fallback-Sortierung nach Hierarchie.

**Verwendet für Sortierung:** `ParentMediaType`, `ParentMediaId`, `AddedAt` sind essentiell für die Sortierlogik in Schritt 3.

---

## `Playlist`
Datei: `VideoWebPlayer/Data/Playlist.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige Kennung der Playlist |
| `UserId` | `string` | Fremdschlüssel zum Besitzer (ApplicationUser) |
| `Name` | `string` | Name der Playlist (erforderlich, max. 255 Zeichen) |
| `Description` | `string?` | Optionale Beschreibung (max. 2000 Zeichen) |
| `SortMode` | `PlaylistSortMode` | Sortiermodus (ByReleaseDate oder Manual) |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel |
| `UpdatedAt` | `DateTime` | Letzter Aktualisierungszeitstempel |
| `PlaylistEntries` | `ICollection<PlaylistEntry>` | Navigation zu allen Einträgen der Playlist |

**Zweck:** Repräsentiert eine benutzereigene Playlist mit ihren Metadaten und Einträgen.

---

## `Movie`
Datei: `VideoWebPlayer/Data/Movie.cs`

**Basisklasse:** `MediaBaseEntry`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MovieCollectionId` | `long?` | Fremdschlüssel zur optionalen MovieCollection |
| `MovieCollection` | `MovieCollection?` | Navigation zur MovieCollection |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum (geerbt, geladen aus NFO) |
| `PremieredAt` | `DateTime?` | Uraufführungsdatum (geerbt, geladen aus NFO) |
| `OriginalTitle` | `string?` | Originaltitel aus NFO |
| `Year` | `int?` | Veröffentlichungsjahr |
| `Country` | `string?` | Land |
| `Studios` | `string?` | Studios (kommagetrennt) |
| `Director` | `string?` | Regisseur |

**Zweck:** Speichert Filmmetadaten. `ReleaseDate` wird für die Sortierung nach Erscheinungsdatum verwendet.

---

## `TVShow`
Datei: `VideoWebPlayer/Data/TVShow.cs`

**Basisklasse:** `MediaBaseEntry`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Seasons` | `ICollection<TVShowSeason>` | Navigation zu allen Staffeln der Serie |
| `PremieredAt` | `DateTime?` | Uraufführungsdatum (geerbt, geladen aus NFO) |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum (geerbt) |
| `OriginalName` | `string?` | Originaltitel |

**Zweck:** Speichert TV-Serien-Metadaten. `PremieredAt` oder `ReleaseDate` wird für Sortierung verwendet. Die `Seasons` Collection ermöglicht die Hierarchie-Navigation für Fallback-Sortierung.

---

## `TVShowSeason`
Datei: `VideoWebPlayer/Data/TVShowSeason.cs`

**Basisklasse:** `MediaBaseEntry`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TVShowId` | `long` | Fremdschlüssel zur übergeordneten TVShow |
| `TVShow` | `TVShow` | Navigation zur übergeordneten TVShow |
| `Episodes` | `ICollection<TVShowEpisode>` | Navigation zu allen Episoden der Staffel |
| `PremieredAt` | `DateTime?` | Uraufführungsdatum (geerbt) |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum (geerbt) |

**Zweck:** Speichert Staffel-Metadaten. Hierarchie-Navigationen ermöglichen Fallback-Sortierung nach Serie und dann nach Staffel-Nummer.

---

## `TVShowEpisode`
Datei: `VideoWebPlayer/Data/TVShowEpisode.cs`

**Basisklasse:** `MediaBaseEntry`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Number` | `int` | Episodennummer innerhalb der Staffel |
| `TVShowSeasonId` | `long` | Fremdschlüssel zur übergeordneten TVShowSeason |
| `TVShowSeason` | `TVShowSeason` | Navigation zur übergeordneten TVShowSeason |
| `ReleaseDate` | `DateTime?` | Ausstrahlungsdatum (geerbt, geladen aus NFO als "aired") |
| `PremieredAt` | `DateTime?` | Uraufführungsdatum (geerbt) |
| `Plot` | `string?` | Handlungszusammenfassung |

**Zweck:** Speichert Episoden-Metadaten. `ReleaseDate` wird für Sortierung nach Erscheinungsdatum verwendet. `Number` wird für Fallback-Sortierung nach Episodenreihenfolge verwendet.

---

## `MovieCollection`
Datei: `VideoWebPlayer/Data/MovieCollection.cs`

**Basisklasse:** `MediaBaseEntry`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Movies` | `ICollection<Movie>` | Navigation zu allen Filmen in der Sammlung |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum (geerbt) |
| `PremieredAt` | `DateTime?` | Uraufführungsdatum (geerbt) |

**Zweck:** Speichert Filmsammlungs-Metadaten.

---

## `MediaBaseEntry`
Datei: `VideoWebPlayer/Data/MediaBaseEntry.cs`

**Basisklasse für:** `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige Kennung |
| `Name` | `string` | Anzeigename |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum – primär für Sortierung nach Erscheinungsdatum |
| `PremieredAt` | `DateTime?` | Uraufführungsdatum – Alternative zu ReleaseDate |
| `EndedAt` | `DateTime?` | Enddatum |
| `MediaSourceId` | `long` | Fremdschlüssel zur Medienquelle |
| `CollectionId` | `long` | Fremdschlüssel zur Collection |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel |
| `ClassifiedAt` | `DateTime?` | Zeitstempel der letzten Klassifikation |
| `Changed` | `bool` | Flag für Änderungen |
| `IsManuallyEdited` | `bool` | Flag für manuell bearbeitete Metadaten |
| `PosterPictureId` | `long?` | Fremdschlüssel zum Posterbild |
| `BannerPictureId` | `long?` | Fremdschlüssel zum Bannerbild |
| `FanartPictureId` | `long?` | Fremdschlüssel zum Fanartbild |

**Zweck:** Abstrakte Basisklasse für alle Medientypen mit gemeinsamen Eigenschaften. `ReleaseDate` und `PremieredAt` sind die Hauptquellen für Erscheinungsdatum-Daten.
