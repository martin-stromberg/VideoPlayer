# Logikklassen / Services

## `IPlaylistService` (Interface)
Datei: `VideoWebPlayer/Services/IPlaylistService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetPlaylistsAsync(userId, cancellationToken)` | öffentlich | Ruft alle Playlists eines Benutzers ab |
| `GetPlaylistAsync(playlistId, userId, cancellationToken)` | öffentlich | Ruft eine einzelne Playlist ab (mit Eigentumspr
üfung) |
| `CreatePlaylistAsync(userId, name, description, sortMode, cancellationToken)` | öffentlich | Erstellt eine neue Playlist |
| `UpdatePlaylistAsync(playlistId, userId, name, description, sortMode, cancellationToken)` | öffentlich | Aktualisiert eine Playlist |
| `DeletePlaylistAsync(playlistId, userId, cancellationToken)` | öffentlich | Löscht eine Playlist |

**Zu erweitern für Schritt 2:**
- `AddMediaToPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken)` → `DtoPlaylistEntry`
- `RemoveMediaFromPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken)` → `void`
- `GetPlaylistEntriesAsync(playlistId, userId, cancellationToken)` → `DtoPlaylistEntry[]`

---

## `PlaylistService` (Implementierung)
Datei: `VideoWebPlayer/Services/PlaylistService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetPlaylistsAsync(userId, cancellationToken)` | öffentlich | Implementiert Interface-Methode |
| `GetPlaylistAsync(playlistId, userId, cancellationToken)` | öffentlich | Implementiert Interface-Methode mit Eigentumspr
üfung |
| `CreatePlaylistAsync(userId, name, description, sortMode, cancellationToken)` | öffentlich | Erstellt Playlist mit Duplikatsprüfung |
| `UpdatePlaylistAsync(playlistId, userId, name, description, sortMode, cancellationToken)` | öffentlich | Aktualisiert Playlist mit Validierung |
| `DeletePlaylistAsync(playlistId, userId, cancellationToken)` | öffentlich | Löscht Playlist mit Eigentumspr
üfung |
| `ValidateName(name)` | privat statisch | Validiert Playlist-Name |
| `ValidateDescription(description)` | privat statisch | Validiert Beschreibung |
| `ParseSortMode(sortMode, fallback)` | privat statisch | Parsed SortMode-String zu Enum |
| `ToDto(playlist)` | privat statisch | Konvertiert Entity zu DTO |

**Abhängigkeiten:**
- `ApplicationDbContext _db` (Entity Framework Context)
- `EventManager _eventManager` (Event Publishing)
- `PlaylistSettings _playlistSettings` (Konfiguration)

**Abonnierte Events:** Keine

**Publizierte Events:**
- `PlaylistCreatedEvent` (nach erfolgreicher Erstellung)
- `PlaylistUpdatedEvent` (nach erfolgreicher Aktualisierung)
- `PlaylistDeletedEvent` (nach erfolgreicher Löschung)

**Zu erweitern für Schritt 2:**
- Implementierung der drei neuen Methoden mit:
  - Berechtigungsprüfung
  - Validierung von MediaType
  - Cascade-Logik
  - Duplikatsprüfung
  - Optionale MaxPlaylistItemCount-Prüfung
  - Medienbestand-Existenzprüfung

---

## Sonstige Services

### `IMediaService` oder analoge Klasse
**Status:** Muss recherchiert werden (nicht im Kontext vorhanden)
- Benötigt für Cascade-Logik und Medienbestand-Abfragen
- Hilfsmethoden erforderlich für:
  - Abfrage von Medieninhalten nach Typ und ID
  - Cascade-Abfragen (Episoden einer Staffel, Filme einer Sammlung, etc.)

