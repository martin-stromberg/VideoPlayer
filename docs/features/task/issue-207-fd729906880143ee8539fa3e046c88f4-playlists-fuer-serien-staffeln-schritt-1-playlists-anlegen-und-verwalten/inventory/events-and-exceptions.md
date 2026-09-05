# Events und Exceptions

## Events

### `PlaylistCreatedEvent`
Datei: `VideoWebPlayer/Events/PlaylistCreatedEvent.cs`

Wird publiziert, nachdem eine Playlist erfolgreich erstellt wurde (von `PlaylistService.CreatePlaylistAsync`).

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Playlist` | `Playlist` | Die neu erstellte Playlist (Entität) |

**Abonnenten:** Derzeit keine bekannten Abonnenten im Projekt.

### `PlaylistUpdatedEvent`
Datei: `VideoWebPlayer/Events/PlaylistUpdatedEvent.cs`

Wird publiziert, nachdem eine Playlist erfolgreich aktualisiert wurde (von `PlaylistService.UpdatePlaylistAsync`).

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Playlist` | `Playlist` | Die aktualisierte Playlist (Entität) |

**Abonnenten:** Derzeit keine bekannten Abonnenten im Projekt.

### `PlaylistDeletedEvent`
Datei: `VideoWebPlayer/Events/PlaylistDeletedEvent.cs`

Wird publiziert, nachdem eine Playlist erfolgreich gelöscht wurde (von `PlaylistService.DeletePlaylistAsync`).

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `PlaylistId` | `long` | ID der gelöschten Playlist |
| `UserId` | `string` | ID des Besitzers der gelöschten Playlist |

**Abonnenten:** Derzeit keine bekannten Abonnenten im Projekt.

## Exceptions

### `PlaylistAccessDeniedException`
Datei: `VideoWebPlayer/Services/Exceptions/PlaylistAccessDeniedException.cs`

Wird geworfen, wenn ein Benutzer versucht, auf eine Playlist eines anderen Benutzers zuzugreifen oder diese zu modifizieren.

**Verwendungsorte:**
- `PlaylistService.GetPlaylistAsync()` - bei fremder Playlist
- `PlaylistService.UpdatePlaylistAsync()` - bei fremder Playlist
- `PlaylistService.DeletePlaylistAsync()` - bei fremder Playlist

**HTTP-Mapping:** Im `PlaylistsController` wird diese Exception zu HTTP 403 (Forbidden) konvertiert.
