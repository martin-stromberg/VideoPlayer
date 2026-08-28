# Bestandsaufnahme – Einzelfreischaltungen

## Architektur

- **Projekte:**
  - `VideoWebPlayer` – ASP.NET Core Blazor-Server-Anwendung mit API-Controllern und EF Core.
  - `VideoWebPlayer.Client` – Client-Bibliothek mit DTOs und HTTP-Helpern.
  - `VideoWebPlayer.Tests` – E2E- und Service-Tests.

- **Technologien:**
  - .NET 8 (vermutlich), Blazor Server (InteractiveServer), Entity Framework Core (Code First mit Migrations), ASP.NET Core Identity.
  - Berechtigungen basieren aktuell auf `MediaSourceUser` (n:n-Zuordnung zwischen Benutzer und MediaSource).

## Berechtigungsmodell

- `MediaSourceUser` (`VideoWebPlayer/Data/MediaSourceUser.cs`)
  - Verknüpft einen Benutzer (`UserId`) mit einer Quelle (`MediaSourceId`).
  - Wird in `ApplicationDbContext` als `DbSet<MediaSourceUser> MediaSourceUsers` verwaltet.
  - Freigabe erfolgt administrativ in `MediaSourceAdminDetails.razor` (Checkbox-Liste der Benutzer).
- Benutzer sieht im NavMenu nur Quellen, deren `MediaSourceUser` existiert.
- Items-Controller filtert MovieCollections und TVShows auf erlaubte `mediaSourceIds` des Benutzers.
- `RecentEntryService.GetRecentEntriesAsync` filtert ebenfalls nach `mediaSourceIds`.

## Favoritenmuster (wiederverwendbar für Einzelfreischaltungen)

- `FavoriteEntry` (`VideoWebPlayer/Data/FavoriteEntry.cs`)
  - Enthält `UserId` und optionale Fremdschlüssel: `MovieCollectionId`, `TVShowId`, `TVShowSeasonId`, `TVShowEpisodeId`, `MovieId`.
- `IFavoritesService` / `FavoritesService`
  - `ToggleFavoriteAsync`, `AddFavoriteAsync`, `RemoveFavoriteAsync`.
- `FavoritesController`
  - `api/favorites/toggle` akzeptiert `DtoMediaEntry` und toggelt Favoritenstatus.
- UI:
  - `TVShowDetails.razor` – `favorite-btn` mit Toggle.
  - `MovieCollectionDetails.razor` – `favorite-btn` für Sammlung/Film.
- DTOs (`VideoWebPlayer.Client/Models/DtoMovie.cs`)
  - `DtoMediaEntry` hat `IsFavorite`. Möglicher Weg: neues `IsUnlocked`/`IsGranted` Flag analog.

## Datenmodell (relevant)

- `MediaBaseEntry` (Basisklasse)
  - `Id`, `Name`, `MediaSourceId`, `CollectionId`, `CreatedAt`, etc.
- `TVShow` und `MovieCollection` erben von `MediaBaseEntry`.
- `MediaSource` hat `MediaSourceUsers` (Benutzer, die die Quelle sehen dürfen).

## API-Endpunkte (relevant)

- `SourcesController`
  - `GET api/sources` – Quellen des Benutzers.
  - `GET api/sources/{id}` – Einzelne Quelle mit Berechtigungsprüfung.
- `ItemsController`
  - `GET api/items` – Liste der MovieCollections/TVShows, gefiltert nach `MediaSourceUsers`.
  - `GET api/items/{type}/{id}` – Detail eines Films/Serien/Seasons/Episodes.
  - `GET api/items/recent` – Recent-Entries.
- `FavoritesController`
  - `api/favorites/toggle` usw.

## UI-Komponenten (relevant)

- `Components/Pages/TV/TVShowDetails.razor`
  - Favorite-Button oben im Header; Admin-Aktionsbar; Edit/Play Buttons.
- `Components/Pages/Movies/MovieCollectionDetails.razor`
  - Analog Favorite-Button; Film-Auswahl horizontal.
- `Components/Shared/Home/RecentEntriesList.razor`
  - Zeigt „Neu im Programm“ als `MediaBox`-Kacheln.
- `Components/Layout/NavMenu.razor`
  - Listet `mediaSources` des Benutzers.

## Styling

- `wwwroot/app.css` enthält `.favorite-btn` Styling.
- Ein neuer `.unlock-btn` kann nahe `favorite-btn` hinzugefügt werden.

## Tests

- `VideoWebPlayer.Tests/FavoritesServiceContextMenuActionTests.cs`
- Verschiedene `*E2ETests.cs` zeigen Muster für UI-Tests.

## Offene Punkte (zu klären)

1. Sollen alle Benutzer oder nur bestimmte freigeschaltet werden? → Favorit-ähnlich pro Benutzer vs. global?
2. Symbolwahl: Eigenes Unicode-Symbol oder SVG?
3. Soll die Freischaltung direkt wie Favoriten togglen (pro Benutzer sichtbar) oder wie Quellenfreigabe (globale Admin-Freigabe für andere)?
