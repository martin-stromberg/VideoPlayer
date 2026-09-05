# API-Controller

## `PlaylistsController`
Datei: `VideoWebPlayer/Controllers/PlaylistsController.cs`

Alle Endpoints setzen `[BearerTokenCheck]`-Attribut voraus. Klasse erbt von `ApiBaseController`.

### Bestehende Endpoints

| HTTP-Methode | Route | Methode | Status-Codes | Beschreibung |
|--------------|-------|---------|--------------|-------------|
| GET | `/api/playlists` | `GetPlaylists()` | 200, 401, 500 | Ruft alle Playlists des aktuellen Benutzers ab |
| GET | `/api/playlists/{id}` | `GetPlaylist(id)` | 200, 404, 403, 401, 500 | Ruft eine einzelne Playlist ab |
| POST | `/api/playlists` | `CreatePlaylist(request)` | 200, 400, 409, 401, 500 | Erstellt eine neue Playlist |
| PUT | `/api/playlists/{id}` | `UpdatePlaylist(id, request)` | 200, 404, 403, 400, 409, 401, 500 | Aktualisiert eine Playlist |
| DELETE | `/api/playlists/{id}` | `DeletePlaylist(id)` | 204, 404, 403, 401, 500 | Löscht eine Playlist |

**Fehlerbehandlung:**
- `UnauthorizedAccessException` → 401
- `PlaylistAccessDeniedException` → 403
- `InvalidOperationException` → 400 oder 409 (abhängig von Fehlermeldung)
- `KeyNotFoundException` → 404

### Zu erweitern für Schritt 2

| HTTP-Methode | Route | Methode | Status-Codes | Beschreibung |
|--------------|-------|---------|--------------|-------------|
| POST | `/api/playlists/{id}/entries` | `AddMediaToPlaylistAsync(id, request)` | 200, 400, 404, 409, 403, 401 | Fügt Medieninhalt zur Playlist hinzu |
| DELETE | `/api/playlists/{id}/entries/{mediaType}/{mediaId}` | `RemoveMediaFromPlaylistAsync(id, mediaType, mediaId)` | 204, 404, 403, 401, 500 | Entfernt Medieninhalt aus Playlist |
| GET | `/api/playlists/{id}/entries` | `GetPlaylistEntriesAsync(id)` | 200, 404, 403, 401, 500 | Ruft alle Einträge einer Playlist ab (unsortiert) |

**Zu erwartende Request/Response für neue Endpoints:**
- POST `/api/playlists/{id}/entries`: Request = `DtoAddMediaToPlaylistRequest`, Response = `DtoPlaylistEntry` oder strukturierte Multiplex-Antwort
- DELETE `/api/playlists/{id}/entries/{mediaType}/{mediaId}`: Response = 204 No Content
- GET `/api/playlists/{id}/entries`: Response = `DtoPlaylistEntry[]`

**Fehlerbehandlung für neue Endpoints:**
- 400: Ungültiger Medientyp
- 409: Duplikat vorhanden
- 404: Medieninhalt nicht vorhanden oder Eintrag nicht in Playlist
- 403: Zugriff verweigert (Nicht-Besitzer)

