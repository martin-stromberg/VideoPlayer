# Tasks: Playlists anlegen und verwalten

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `PlaylistSortMode` Enum mit Werten `ByReleaseDate` und `Manual` anlegen | Offen | — |
| 2 | Datenmodell | `PlaylistSettings` Konfigurationsklasse anlegen (optional: `MaxPlaylistsPerUser` Property) | Offen | — |
| 3 | Datenmodell | `Playlist` Entity-Klasse anlegen mit Properties: Id, UserId, Name, Description, SortMode, CreatedAt, UpdatedAt | Offen | — |
| 4 | Events | `PlaylistCreatedEvent` Klasse anlegen | Offen | — |
| 5 | Events | `PlaylistUpdatedEvent` Klasse anlegen | Offen | — |
| 6 | Events | `PlaylistDeletedEvent` Klasse anlegen | Offen | — |
| 7 | DTOs | `DtoPlaylist` DTO-Klasse anlegen in VideoWebPlayer.Client/Models/ | Offen | — |
| 8 | DTOs | `DtoCreatePlaylistRequest` DTO-Klasse anlegen | Offen | — |
| 9 | DTOs | `DtoUpdatePlaylistRequest` DTO-Klasse anlegen | Offen | — |
| 10 | Datenbank | `ApplicationDbContext` um `DbSet<Playlist> Playlists` erweitern | Offen | — |
| 11 | Datenbank | `ApplicationDbContext.OnModelCreating()` mit Playlist-Konfiguration erweitern (Primary Key, Foreign Key, Unique Index, Constraints) | Offen | — |
| 12 | Datenbank | Datenbank-Migration `AddPlaylistsTable` erstellen via `dotnet ef migrations add` | Offen | — |
| 13 | Datenbank | Datenbank-Migration ausführen via `dotnet ef database update` | Offen | — |
| 14 | Service | `IPlaylistService` Interface anlegen mit CRUD-Methoden | Offen | — |
| 15 | Service | `PlaylistService` implementieren mit vollständiger CRUD-Logik, Validierung, Ownership-Checks | Offen | — |
| 16 | Service | Event-Publishing in `PlaylistService` integrieren (nach SaveChangesAsync) | Offen | — |
| 17 | Konfiguration | `IPlaylistService` in `ServiceCollectionExtensions.cs` registrieren | Offen | — |
| 18 | Konfiguration | `PlaylistSettings` (optional) in `ServiceCollectionExtensions.cs` registrieren | Offen | — |
| 19 | API | `PlaylistsController` anlegen mit REST-Endpoints (GET all, GET single, POST, PUT, DELETE) | Offen | — |
| 20 | API | Error-Handling im `PlaylistsController` implementieren (Exception-Mapping zu HTTP-Status) | Offen | — |
| 21 | UI | `PlaylistsList.razor` Blazor-Komponente anlegen mit Tabelle, Aktionen, Bestätigungsdialog | Offen | — |
| 22 | UI | `PlaylistForm.razor` Blazor-Komponente anlegen mit Validierungsfeedback, Create/Edit-Modus | Offen | — |
| 23 | Tests | Unit-Tests für `PlaylistService` schreiben (CRUD, Validierung, Ownership, Events) | Offen | — |
| 24 | Tests | Unit-Tests für `PlaylistsController` schreiben (Endpoints, HTTP-Status, Error-Mapping) | Offen | — |
| 25 | Tests | Integration-Tests mit echtem DbContext schreiben (optional: Persistierung, Unique Index, Cascade-Delete) | Offen | — |
| 26 | E2E-Tests | E2E-Test: Happy Path Playlist erstellen | Offen | — |
| 27 | E2E-Tests | E2E-Test: Playlists auflisten | Offen | — |
| 28 | E2E-Tests | E2E-Test: Playlist bearbeiten | Offen | — |
| 29 | E2E-Tests | E2E-Test: Playlist löschen mit Bestätigungsdialog | Offen | — |
| 30 | E2E-Tests | E2E-Test: Validierungsfehler (leerer Name) | Offen | — |
| 31 | E2E-Tests | E2E-Test: Fehler bei Duplikat-Name | Offen | — |
| 32 | E2E-Tests | E2E-Test: Authentifizierung (unauthentifizierter Zugriff blockiert) | Offen | — |
| 33 | E2E-Tests | E2E-Test: Ownership (Benutzer A sieht nicht Playlists von Benutzer B) | Offen | — |
| 34 | E2E-Tests | E2E-Test: SortMode-Default (neu erstellte Playlists haben ByReleaseDate) | Offen | — |
