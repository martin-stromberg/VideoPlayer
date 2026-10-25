# UI, DTOs und API-Modelle — Bestandsaufnahme

**Befund:** Keine der DTO-/UI-Artefakte enthält eine Altersfreigabe. Anzeigestellen und Editierpfad existieren und sind die in der Anforderung genannten Erweiterungspunkte.

## DTOs
Datei: `VideoWebPlayer.Client/Models/DtoMovie.cs` (enthält alle Detail-DTOs in einer Datei)

| Typ | Rolle | Relevante Eigenschaften |
|-----|-------|-------------------------|
| `DtoMediaEntry` | Polymorphe Basisklasse (`$type`-Diskriminator; `mediaEntry`, `movie`, `movieCollection`, `show`, `season`, `episode`) | `Id`, `Name`, `MediaSourceId`, `ReleaseDate`, `PremieredAt`, `EndedAt`, `PosterPictureId`, `BannerPictureId`, `FanartPictureId`, `PosterPicture`/`BannerPicture`/`FanartPicture` (`DtoPicture`), `IsFavorite`, `IsUnlocked`, `IsManuallyEdited`, `WatchedAt` |
| `DtoMovie` | Film-Detail | `GenreNames`, `Plot`, `Collection` (`DtoMovieCollection`) |
| `DtoMovieCollection` | Filmsammlung | `Movies` (`[IgnoreAssignProperty]`) |
| `DtoTVShow` | Serie | `Seasons` (`[IgnoreAssignProperty]`), `GenreNames`, `Plot` |
| `DtoTVShowSeason` | Staffel | `Number`, `Show`, `Episodes` (`[IgnoreAssignProperty]`) |
| `DtoTVShowEpisode` | Episode | `Season`, `Number`, `Plot`, `GeneratedBackgroundPictureId` |
| `MediaMetadataUpdateRequest` | Editier-Request (`POST api/items/metadata`) | `ObjectType`, `Id`, `Name`, `ReleaseDate`, `PremieredAt`, `Plot`, `GenreNames` — **kein Rating-Feld** |
| `DtoGenreOption` | Genre-Auswahl | `Id`, `Name` |
| `DtoPicture` | Bild | `Id`, `MediaItemId`, `Type`, `Width`, `Height`, `Description`, `Data`, `ContentType` |

Wichtig: `ItemsController` befüllt Detail-DTOs per Reflection (`ApiBaseController.Create<T>`) — ein namensgleiches neues Property auf Entity + DTO wird ohne weiteren Controller-Code ausgeliefert.

## `MediaEntryDto`
Datei: `VideoWebPlayer.Client/Models/MediaEntryDto.cs` (Namespace `VideoWebPlayer.Controllers.Models`)

Flaches Listen-DTO für `GET api/items`: `Type`, `Id`, `Title`, `Description`, `Url`, `CreatedAt`, `PictureId`, `ItemCount`, `WatchedAt`. Anker für ein optionales Badge/Filter in Listen (Offene Frage 7).

## Detailseiten (Meta-Zeile = natürliche Anzeigestelle)

### `MovieCollectionDetails.razor`
Datei: `VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor`

- Meta-Zeile Film: Zeilen 48–51 — `<div class="collection-meta">` mit `collection-year` (`GetYear(selectedMovie)` → `ReleaseDate?.Year`) und `collection-genres` (`GetGenres(selectedMovie)` → `GenreNames`).
- Meta-Zeile Sammlung: Zeilen 105–108 — Jahr + aggregierte Genres der Filme.
- Editierformular `RenderEditForm()` (ab Zeile 280): `InputText` Titel, `InputDate` Datum, Genre-Auswahl (`editSupportsGenres` nur für `DtoMovie`, Zeile 432), `InputTextArea` Plot; Dirty-Check/Verwerfen-Dialog (`showDiscardDialog`).
- `SaveEditAsync` (Zeilen ~453–465) baut `MediaMetadataUpdateRequest` (`ObjectType`, `ReleaseDate`, `PremieredAt = null`, `GenreNames`).

### `TVShowDetails.razor`
Datei: `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor`

- Meta-Zeile Episode: Zeilen 96–99 — `tvshow-year` (`GetEpisodeDisplayDate`) + `tvshow-episode-number`.
- Meta-Zeile Staffel: Zeilen 105–108 — `PremieredAt?.Year` + Episodenzahl.
- Meta-Zeile Serie: Zeilen 114–117 — `tvshow-year` (`GetShowDisplayDate` → `ReleaseDate ?? PremieredAt`) + `tvshow-genres` (`show.GenreNames`).
- Editierformular analog (`RenderEditForm` ab Zeile 298; `editSupportsGenres` nur `DtoTVShow`, `editUsesPremieredAt` nur `DtoTVShowSeason`, Zeilen 510–512); `SaveEditAsync` (Zeilen ~538–549) setzt `ReleaseDate`/`PremieredAt` typabhängig.

## Editier-Formularmodell

### `MetadataEditModel`
Datei: `VideoWebPlayer/Components/Shared/Media/MetadataEditModel.cs`

| Eigenschaft | Typ | Validierung |
|-------------|-----|-------------|
| `Name` | `string` | `[Required]`, `[StringLength(512)]` |
| `Date` | `DateTime?` | – |
| `Plot` | `string` | `[StringLength(10000)]` |
| `NewGenres` | `string` | – (kommagetrennt) |

## Listen-Komponenten (optionale Badge-Stellen)

### `MediaBox.razor`
Datei: `VideoWebPlayer/Components/Shared/Media/MediaBox.razor`

Poster-Karte mit `Title`, `Subtitle`, `ImageUrl`, `LinkUrl`, `WatchedAt` (`WatchedIndicator`), optionalem Kontextmenü (`Actions`, `OnActionSelected`). Overlay-Bereich `media-card-overlay`/`media-titles` (Zeilen 87–95) ist der natürliche Badge-Anker.

### `MediaBaseEntryList.razor`
Datei: `VideoWebPlayer/Components/Shared/Media/MediaBaseEntryList.razor`

Rendert `List<MediaBaseEntry>` als `MediaBox`-Karten; `GetLinkUrl` mappt Entity-Typname → Route (`/movie/{id}`, `/tvshow/{id}`, `/tvshow/season/{id}`, `/tvshow/episode/{id}`, `/moviecollection/{id}`).

## Admin-/Editierfreigabe
- Serverseitig: `ItemsController.UpdateMetadata` verlangt `IsAdmin`-Claim (`ForbiddenAccessException` → 403).
- UI-seitig: Edit-Schaltflächen nur bei `isAdmin` (`TVShowDetails.razor` Zeile 51; `RenderActionButtons` in `MovieCollectionDetails.razor`).

## Hilfe-/Dokumentation
`docs/help/` enthält u. a. `medien-editiermodus.md`, `medienbibliothek.md`, `medienquellen/*` (NFO-basierte Metadaten werden dort fachlich beschrieben) — bei Umsetzung zu ergänzen; `docs/SECRETS_MANAGEMENT.md` regelt API-Key-Ablage.
