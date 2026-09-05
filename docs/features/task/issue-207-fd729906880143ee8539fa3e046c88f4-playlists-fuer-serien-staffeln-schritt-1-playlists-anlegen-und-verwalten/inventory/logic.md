# Logik-Klassen und Services

## `IPlaylistService` (Interface)
Datei: `VideoWebPlayer/Services/IPlaylistService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetPlaylistsAsync(string userId, CancellationToken)` | public | Gibt alle Playlists eines Benutzers zurück |
| `GetPlaylistAsync(long playlistId, string userId, CancellationToken)` | public | Gibt eine einzelne Playlist zurück oder null wenn nicht gefunden |
| `CreatePlaylistAsync(string userId, string name, string? description, string? sortMode, CancellationToken)` | public | Erstellt eine neue Playlist mit Validierung und Duplikat-Check |
| `UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken)` | public | Aktualisiert eine Playlist mit Eigentumscheck und Validierung |
| `DeletePlaylistAsync(long playlistId, string userId, CancellationToken)` | public | Löscht eine Playlist mit Eigentumscheck |

## `PlaylistService` (Implementierung)
Datei: `VideoWebPlayer/Services/PlaylistService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetPlaylistsAsync(userId, ct)` | public | Ruft alle Playlists aus der DB ab, sortiert nach `CreatedAt` und `Id` |
| `GetPlaylistAsync(playlistId, userId, ct)` | public | Ruft eine einzelne Playlist ab; wirft `PlaylistAccessDeniedException` bei fremder Playlist |
| `CreatePlaylistAsync(userId, name, desc, sortMode, ct)` | public | Erstellt Playlist nach Validierung; wirft bei Duplikat oder Max-Limit erreicht |
| `UpdatePlaylistAsync(playlistId, userId, name, desc, sortMode, ct)` | public | Aktualisiert Playlist nach Eigentumscheck und Validierung |
| `DeletePlaylistAsync(playlistId, userId, ct)` | public | Löscht Playlist nach Eigentumscheck |
| `ValidateName(name)` | private static | Prüft Name: nicht leer, max. 255 Zeichen; gibt trimmed zurück |
| `ValidateDescription(description)` | private static | Prüft Beschreibung: max. 2000 Zeichen; gibt null bei leer zurück |
| `ParseSortMode(sortMode, fallback)` | private static | Konvertiert String zu `PlaylistSortMode` mit Fallback-Wert |
| `ToDto(playlist)` | private static | Konvertiert Entity zu DTO |

**Abhängigkeiten:**
- `ApplicationDbContext` - Datenbankzugriff
- `EventManager` - publiziert Events
- `PlaylistSettings` - optionale Konfiguration (z. B. `MaxPlaylistsPerUser`)

**Publizierte Events:**
- `PlaylistCreatedEvent` - nach erfolgreicher Erstellung
- `PlaylistUpdatedEvent` - nach erfolgreicher Änderung
- `PlaylistDeletedEvent` - nach erfolgreicher Löschung

## `PlaylistsController`
Datei: `VideoWebPlayer/Controllers/PlaylistsController.cs`

| Methode | HTTP-Verb | Route | Kurzbeschreibung |
|---------|-----------|-------|------------------|
| `GetPlaylists()` | GET | `/api/playlists` | Alle Playlists des aktuellen Benutzers |
| `GetPlaylist(long id)` | GET | `/api/playlists/{id}` | Einzelne Playlist mit Eigentumscheck; 403 bei fremder Playlist |
| `CreatePlaylist(DtoCreatePlaylistRequest request)` | POST | `/api/playlists` | Neue Playlist erstellen; 409 bei Duplikat |
| `UpdatePlaylist(long id, DtoUpdatePlaylistRequest request)` | PUT | `/api/playlists/{id}` | Playlist aktualisieren mit Eigentumscheck |
| `DeletePlaylist(long id)` | DELETE | `/api/playlists/{id}` | Playlist löschen mit Eigentumscheck; 204 NoContent bei Erfolg |

**Attribute:**
- `[ApiController]` - API-Kontroller
- `[Route("api/playlists")]` - Basis-Route
- `[BearerTokenCheck]` - Erfordert gültiges Bearer-Token

**Fehlerbehandlung:**
- 401 Unauthorized: Nicht angemeldet
- 403 Forbidden: Zugriff auf fremde Playlist (via `PlaylistAccessDeniedException`)
- 404 Not Found: Playlist nicht gefunden
- 409 Conflict: Name existiert bereits (via `InvalidOperationException`)

## `VideoWebPlayerClient`
Datei: `VideoWebPlayer.Client/VideoWebPlayerClient.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `RequestPlaylistsAsync()` | Gibt alle Playlists des aktuellen Benutzers zurück (GET `/api/playlists`) |
| `RequestPlaylistAsync(long playlistId)` | Gibt eine einzelne Playlist zurück oder null bei 404 (GET `/api/playlists/{id}`) |
| `CreatePlaylistAsync(DtoCreatePlaylistRequest request)` | Erstellt neue Playlist (POST `/api/playlists`) |
| `UpdatePlaylistAsync(long playlistId, DtoUpdatePlaylistRequest request)` | Aktualisiert Playlist (PUT `/api/playlists/{id}`) |
| `DeletePlaylistAsync(long playlistId)` | Löscht Playlist (DELETE `/api/playlists/{id}`) |

**Fehlerbehandlung:**
- `RequestPlaylistAsync()` fängt `HttpStatusCode.NotFound` ab und gibt `null` zurück
- Andere Fehler werfen `HttpRequestException` mit Status-Code

**Wichtig:** Bei 403 (fremde Playlist) wird eine Exception geworfen (kein null-Return).
