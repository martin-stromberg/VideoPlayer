# Interfaces

## `IFavoritesService`
**Datei:** `VideoWebPlayer/Services/IFavoritesService.cs`

Interface für Favoriten-Verwaltung mit ähnlichem Pattern wie Playlists.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetFavoritesAsync` | `string userId, CancellationToken ct` | `Task<DtoFavoriteEntry[]>` | Ruft alle Favoriten eines Benutzers ab |
| `AddFavoriteAsync` | `string userId, FavoriteEntry entry, CancellationToken ct` | `Task` | Fügt Favorite hinzu |
| `RemoveFavoriteAsync` | `string userId, FavoriteEntry entry, CancellationToken ct` | `Task` | Entfernt Favorite |
| `ToggleFavoriteAsync` | `string userId, DtoMediaEntry entry, CancellationToken ct` | `Task<bool>` | Schaltet Favorite um |

**Status:** Vorhanden, ähnliches Pattern für `IPlaylistService` anwendbar.

---

## `IGenreService`
**Datei:** `VideoWebPlayer/Services/IGenreService.cs`

Interface für Genre-Verwaltung.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `MarkGenresAsChangedAsync` | `CancellationToken ct` | `Task` | Markiert Genres als geändert |
| `GetSeasonalGenresAsync` | `CancellationToken ct` | `Task<List<Genre>>` | Ruft saisonale Genres ab |

**Status:** Vorhanden, relevant für Playlist-Genre-Ableitung.

---

## `IUnlockedMediaService`
**Datei:** `VideoWebPlayer/Services/IUnlockedMediaService.cs`

Interface für Freischaltungs-Logik von Medien.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsUnlockedAsync` | `DtoMediaEntry entry, CancellationToken ct` | `Task<bool>` | Prüft, ob Media-Eintrag freigeschalten ist |
| `GetUnlockedUserIdsAsync` | `DtoMediaEntry entry, CancellationToken ct` | `Task<string[]>` | Ruft Benutzer-IDs ab, für die Eintrag freigeschalten ist |
| `SetUnlockedUsersAsync` | `DtoMediaEntry entry, string[] userIds, CancellationToken ct` | `Task` | Setzt Freischaltung für Benutzer |
| `GetUnlockedMovieCollectionIdsForUserAsync` | `string userId, CancellationToken ct` | `Task<long[]>` | Ruft freigeschaltete Filmsammlung-IDs ab |
| `GetUnlockedTVShowIdsForUserAsync` | `string userId, CancellationToken ct` | `Task<long[]>` | Ruft freigeschaltete Serien-IDs ab |
| `GetUnlockedSourceIdsForUserAsync` | `string userId, CancellationToken ct` | `Task<long[]>` | Ruft freigeschaltete Media-Source-IDs ab |

**Status:** Vorhanden, relevant für Zugriffsprüfung in Playlists.

---

## `IAuthService`
**Datei:** `VideoWebPlayer/Services/Authentication/IAuthService.cs`

Interface für Authentifizierung und Autorisierung.

**Status:** Vorhanden, wird für Benutzer-Identifikation in Playlist-APIs verwendet.

---

## `IMediaMetadataWriteCoordinator`
**Datei:** `VideoWebPlayer/Services/IMediaMetadataWriteCoordinator.cs`

Interface für Koordination von Metadaten-Schreibvorgängen.

**Status:** Vorhanden, relevant für Playlist-Änderungen.

---

## Zu erstellende Interfaces

### `IPlaylistService`

Zentrale Interface für Playlist-Verwaltung.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreatePlaylistAsync` | `string userId, string name, PlaylistSortMode sortMode, string? description, CancellationToken ct` | `Task<Playlist>` | Erstellt neue Playlist |
| `GetPlaylistAsync` | `long playlistId, string? userId, CancellationToken ct` | `Task<Playlist?>` | Ruft Playlist ab (Public-Check beachten) |
| `UpdatePlaylistAsync` | `long playlistId, string userId, PlaylistUpdateRequest update, CancellationToken ct` | `Task<Playlist>` | Aktualisiert Playlist (nur für Besitzer) |
| `DeletePlaylistAsync` | `long playlistId, string userId, CancellationToken ct` | `Task<bool>` | Löscht Playlist (nur für Besitzer) |
| `GetUserPlaylistsAsync` | `string userId, CancellationToken ct` | `Task<List<Playlist>>` | Ruft alle Playlists eines Benutzers ab |
| `AddItemToPlaylistAsync` | `long playlistId, string userId, PlaylistItemType itemType, long itemId, CancellationToken ct` | `Task<PlaylistItem>` | Fügt Item zur Playlist hinzu |
| `RemoveItemFromPlaylistAsync` | `long playlistId, string userId, long itemId, bool enforceWatchedEntryCheck, CancellationToken ct` | `Task<bool>` | Entfernt Item aus Playlist |
| `GetPlaylistItemsAsync` | `long playlistId, int pageSize, int page, CancellationToken ct` | `Task<PaginatedList<PlaylistItem>>` | Ruft Items mit Paginierung ab |
| `ReorderPlaylistItemAsync` | `long playlistId, string userId, long itemId, long newOrder, CancellationToken ct` | `Task<PlaylistItem>` | Ändert Reihenfolge bei Manual-Mode |

**Empfohlener Standort:** `VideoWebPlayer/Services/IPlaylistService.cs`

---

### `IPlaylistGenreService`

Interface für Genre-Management in Playlists.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `DeriveGenresFromPlaylistItemsAsync` | `long playlistId, CancellationToken ct` | `Task<List<Genre>>` | Leitet Genres aus Playlist-Items ab |
| `UpdatePlaylistGenresAsync` | `long playlistId, string userId, List<long> genreIds, CancellationToken ct` | `Task` | Setzt Genres manuell |
| `GetPlaylistGenresAsync` | `long playlistId, CancellationToken ct` | `Task<List<Genre>>` | Ruft Genres ab |

**Empfohlener Standort:** `VideoWebPlayer/Services/IPlaylistGenreService.cs`

---

### `IPlaylistImageService`

Interface für Playlist-Bildverwaltung.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GeneratePlaylistImageAsync` | `long playlistId, CancellationToken ct` | `Task<Picture>` | Generiert automatisches Composite-Bild |
| `UploadPlaylistImageAsync` | `long playlistId, string userId, byte[] imageData, string contentType, CancellationToken ct` | `Task<Picture>` | Lädt benutzerdefiniertes Bild hoch |
| `GetPlaylistImageAsync` | `long playlistId, CancellationToken ct` | `Task<Picture?>` | Ruft Playlist-Bild ab |

**Empfohlener Standort:** `VideoWebPlayer/Services/IPlaylistImageService.cs`

---

### `IPlaylistSortingService`

Interface für Sortierlogik.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSortedItemsAsync` | `long playlistId, PlaylistSortMode sortMode, CancellationToken ct` | `Task<List<PlaylistItem>>` | Liefert sortierte Items |
| `CalculateAutomaticOrderAsync` | `long playlistId, PlaylistItemType newItemType, long newItemId, CancellationToken ct` | `Task<long>` | Bestimmt Position bei Automatic-Mode |

**Empfohlener Standort:** `VideoWebPlayer/Services/IPlaylistSortingService.cs`

---

### `IPlaylistValidationService`

Interface für Zugriffsprüfung und Validierung.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ValidatePlaylistItemAsync` | `long playlistId, PlaylistItemType itemType, long itemId, CancellationToken ct` | `Task<bool>` | Prüft Existenz und Validität |
| `ValidateDuplicateAsync` | `long playlistId, PlaylistItemType itemType, long itemId, CancellationToken ct` | `Task<bool>` | Prüft auf Duplikate |
| `ValidateAccessAsync` | `long playlistId, string userId, bool requireEdit, CancellationToken ct` | `Task<bool>` | Prüft Zugriff (Public vs. Private) |

**Empfohlener Standort:** `VideoWebPlayer/Services/IPlaylistValidationService.cs`

---

### `IContinueWatchingPlaylistService`

Interface für Weiterschauen-Playlist-Integration.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateOrUpdateContinueWatchingWithPlaylistAsync` | `string userId, PlaylistItemType videoType, long videoId, long? playlistId, TimeSpan position, TimeSpan duration, CancellationToken ct` | `Task` | Erstellt/aktualisiert Weiterschauen-Eintrag mit Playlist |
| `GetContinueWatchingEntriesAsync` | `string userId, bool includePlaylistInfo, CancellationToken ct` | `Task<List<ContinueWatchingDto>>` | Ruft Weiterschauen-Einträge ab |
| `ReplaceOrDeleteContinueWatchingOnPlaylistItemRemovalAsync` | `long playlistId, long itemId, CancellationToken ct` | `Task` | Sicherheitslogik beim Entfernen |

**Empfohlener Standort:** `VideoWebPlayer/Services/IContinueWatchingPlaylistService.cs`
