# Bestandsaufnahme: Datenmodelle

## `DtoPlaylistEntry`
Datei: `VideoWebPlayer.Client\Models\DtoPlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige Kennung des Playlist-Eintrags |
| `PlaylistId` | `long` | Fremdschlüssel zur Playlist |
| `MediaType` | `string` | Medientyp (z.B. "Movie", "TVShow", "TVShowSeason", "TVShowEpisode", "MovieCollection") |
| `MediaId` | `long` | Kennung der referenzierten Medienentität |
| `MediaTitle` | `string` | Titel des Medieneintrags (wird von `PlaylistService.ToDto()` befüllt) |
| `ParentMediaType` | `string?` | Medientyp des übergeordneten Elements (null für Top-Level-Einträge) |
| `ParentMediaId` | `long?` | Kennung des übergeordneten Elements (null für Top-Level-Einträge) |
| `ParentMediaTitle` | `string?` | Titel des übergeordneten Elements (wird von `PlaylistService.ToDto()` befüllt) |
| `AddedAt` | `DateTime` | Zeitstempel, wann der Eintrag hinzugefügt wurde |
| `IsAccessible` | `bool` | Freischaltungsstatus (aktuell immer `true`, Platzhalter für zukünftige Freischaltungsprüfung - Kommentar Zeile 15) |

**Hinweis:** Das Feld `PosterPictureId` fehlt noch und muss hinzugefügt werden (siehe Anforderung Phase 1).

---

## `PlaylistEntry` (Datenbankentität)
Datei: `VideoWebPlayer\Data\PlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige Kennung (Primary Key) |
| `PlaylistId` | `long` | Fremdschlüssel zur Playlist-Entität |
| `Playlist` | `Playlist` | Navigationsproperty zur Playlist |
| `MediaType` | `string` (required) | Medientyp der referenzierten Medienentität |
| `MediaId` | `long` | Kennung der referenzierten Medienentität |
| `ParentMediaType` | `string?` | Medientyp des übergeordneten Elements (null bei Top-Level) |
| `ParentMediaId` | `long?` | Kennung des übergeordneten Elements (null bei Top-Level) |
| `AddedAt` | `DateTime` | Zeitstempel des Hinzufügens (Default: `DateTime.UtcNow`) |

**Hinweis:** Diese Datenbankentität speichert keine `PosterPictureId`, da die Bilder-IDs aus den referenzierten Medienentitäten (Film, Serie, etc.) geladen werden müssen.

---

## `MediaBaseEntry` (Basisklasse für alle Medienentitäten)
Datei: `VideoWebPlayer\Data\MediaBaseEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige Kennung |
| `Name` | `string` | Anzeigename |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum |
| `PremieredAt` | `DateTime?` | Erstausstrahlungsdatum |
| `EndedAt` | `DateTime?` | Enddatum |
| `MediaSourceId` | `long` | Fremdschlüssel zur Medienquelle |
| `CollectionId` | `long` | Sammlungskennung |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel |
| `ClassifiedAt` | `DateTime?` | Zeitstempel der letzten Klassifizierung |
| `Changed` | `bool` | Indikator für Änderungen |
| `IsManuallyEdited` | `bool` | Schutz vor Scan-Überschreibungen |
| `PosterPictureId` | `long?` | **Kennung des Posterbildes** |
| `BannerPictureId` | `long?` | Kennung des Bannnerbildes |
| `FanartPictureId` | `long?` | Kennung des Fanart-Bildes |
| `PosterPicture` | `Picture?` | Navigationsproperty zum Posterbild |
| `BannerPicture` | `Picture?` | Navigationsproperty zum Bannner |
| `FanartPicture` | `Picture?` | Navigationsproperty zum Fanart |

**Hinweis:** Alle Medientypen (Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection) erben von `MediaBaseEntry` und haben daher Zugriff auf `PosterPictureId`, `BannerPictureId` und `FanartPictureId`.

---

## `UnlockedMediaEntry` (Datenbankentität)
Datei: `VideoWebPlayer\Data\UnlockedMediaEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige Kennung (Primary Key) |
| `UserId` | `string` (required) | Benutzer-ID, für den das Medium freigeschaltet ist |
| `User` | `ApplicationUser?` | Navigationsproperty zum Benutzer |
| `MovieCollectionId` | `long?` | Kennung der Filmsammlung (falls freigeschaltet) |
| `TVShowId` | `long?` | Kennung der TV-Serie (falls freigeschaltet) |
| `CreatedAt` | `DateTime` | Zeitstempel der Freischaltung (Default: `DateTime.UtcNow`) |

**Hinweis:** Eine `UnlockedMediaEntry` stellt dar, dass ein Benutzer entweder für eine Filmsammlung ODER eine TV-Serie freigeschaltet ist. Der Service `UnlockedMediaService` unterstützt nur diese zwei Medientypen für Freischaltungen.
