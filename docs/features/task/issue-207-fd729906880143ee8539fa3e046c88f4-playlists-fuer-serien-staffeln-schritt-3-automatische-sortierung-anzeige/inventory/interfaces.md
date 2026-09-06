# Bestandsaufnahme: Interfaces und Contracts

## `IPlaylistService`
Datei: `VideoWebPlayer\Services\IPlaylistService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPlaylistsAsync()` | `string userId, CancellationToken` | `Task<DtoPlaylist[]>` | Ruft alle Playlists eines Benutzers ab |
| `GetPlaylistAsync()` | `long playlistId, string userId, CancellationToken` | `Task<DtoPlaylist?>` | Ruft eine Playlist mit Zugriffsprüfung ab |
| `CreatePlaylistAsync()` | `string userId, string name, string? description, string? sortMode, CancellationToken` | `Task<DtoPlaylist>` | Erstellt neue Playlist |
| `UpdatePlaylistAsync()` | `long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken` | `Task<DtoPlaylist>` | Aktualisiert Playlist-Metadaten |
| `DeletePlaylistAsync()` | `long playlistId, string userId, CancellationToken` | `Task` | Löscht eine Playlist |
| `AddMediaToPlaylistAsync()` | `long playlistId, string userId, string mediaType, long mediaId, CancellationToken` | `Task<DtoPlaylistAddResult>` | Fügt Medium zur Playlist hinzu |
| `RemoveMediaFromPlaylistAsync()` | `long playlistId, string userId, string mediaType, long mediaId, CancellationToken` | `Task` | Entfernt Medium aus Playlist |
| `GetPlaylistEntriesAsync()` | `long playlistId, string userId, CancellationToken` | `Task<DtoPlaylistEntry[]>` | Ruft alle Einträge ab (nicht paginiert) |
| `GetPlaylistEntriesPagedAsync()` | `long playlistId, string userId, int pageNumber, int pageSize, CancellationToken` | `Task<DtoPlaylistEntriesPagedResult>` | **Ruft paginierte Einträge ab** - wird von `PlaylistDetail.razor` genutzt |

---

## `IUnlockedMediaService`
Datei: `VideoWebPlayer\Services\IUnlockedMediaService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsUnlockedAsync()` | `DtoMediaEntry entry, CancellationToken` | `Task<bool>` | Prüft ob aktueller Benutzer Medium freigeschaltet hat |
| `GetUnlockedUserIdsAsync()` | `DtoMediaEntry entry, CancellationToken` | `Task<string[]>` | Ruft alle Benutzer ab, für die Medium freigeschaltet ist |
| `SetUnlockedUsersAsync()` | `DtoMediaEntry entry, string[] userIds, CancellationToken` | `Task` | Setzt Freischaltungen (Admin nur) |
| `GetUnlockedMovieCollectionIdsForUserAsync()` | `string userId, CancellationToken` | `Task<long[]>` | Ruft Filmsammlung-IDs für Benutzer ab |
| `GetUnlockedTVShowIdsForUserAsync()` | `string userId, CancellationToken` | `Task<long[]>` | Ruft TV-Show-IDs für Benutzer ab |
| `GetUnlockedSourceIdsForUserAsync()` | `string userId, CancellationToken` | `Task<long[]>` | Ruft Medienquellen-IDs ab |

**Wichtig für Anforderung:**
- `IsUnlockedAsync()` ist der Einstiegspunkt für Freischaltungsprüfung
- Funktioniert nur mit `DtoMovieCollection` und `DtoTVShow` (andere Typen geben `false` zurück)
- Nutzt `_authService.CurrentUser` intern (keine Benutzer-ID als Parameter nötig)

---

## `IAuthService`
Datei: `VideoWebPlayer\Services\Authentication\IAuthService.cs`

| Property/Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CurrentUser` | - | `ApplicationUser?` | Property: Aktueller authentifizierter Benutzer aus HttpContext |
| `LoginAsync()` | `AuthenticationRequest` | `Task<AuthorizationToken>` | Login mit Credentials |
| `ImpersonateAsync()` | `ImpersonateRequest` | `Task<AuthorizationToken>` | Admin-Identitätswechsel |

**Wichtig für Anforderung:**
- `CurrentUser` Property stellt den Mechanismus bereit, um in `PlaylistService` den aktuellen Benutzer zu ermitteln
- Wird von `UnlockedMediaService` bereits genutzt

---

## DTOs und Rückgabewerte

### `DtoPlaylist`
- `Id`: `long`
- `Name`: `string`
- `Description`: `string?`
- `SortMode`: `string` ("ByReleaseDate" oder "Manual")
- `CreatedAt`: `DateTime`
- `UpdatedAt`: `DateTime`

### `DtoPlaylistEntry`
Datei: `VideoWebPlayer.Client\Models\DtoPlaylistEntry.cs`
- `Id`, `PlaylistId`, `MediaType`, `MediaId`, `MediaTitle`
- `ParentMediaType?`, `ParentMediaId?`, `ParentMediaTitle?`
- `AddedAt`
- `IsAccessible`: `bool` (aktuell immer `true`)
- **FEHLT:** `PosterPictureId?: long?`

### `DtoPlaylistEntriesPagedResult`
- `Entries`: `DtoPlaylistEntry[]`
- `TotalCount`: `int`
- `HasNextPage`: `bool`
- `PageNumber`: `int`
- `PageSize`: `int`

### `DtoPlaylistAddResult`
- `TopLevelEntry`: `DtoPlaylistEntry?` (null wenn Duplikat)
- `AddedEntries`: `DtoPlaylistEntry[]`
- `SkippedDuplicateCount`: `int`
- `Message`: `string`

---

## Weitere relevante Interfaces

### `IHttpContextAccessor`
- Ermöglicht Zugriff auf `HttpContext` in Services
- Wird von `IAuthService` (AuthService) genutzt um `CurrentUser` zu ermitteln

### `UserManager<ApplicationUser>` (ASP.NET Core Identity)
- `GetUserAsync(ClaimsPrincipal)` - Ruft Benutzer aus Claims ab (asynchron)
- Wird von `AuthService.CurrentUser` genutzt

---

## Service-Registrierung und Dependency Injection

Basierend auf der Codebasis sollten folgende Services in `Program.cs` registriert sein:
- `IPlaylistService` → `PlaylistService` (Singleton/Scoped)
- `IUnlockedMediaService` → `UnlockedMediaService` (Scoped)
- `IAuthService` → `AuthService` (Scoped)

Für die Anforderung muss `PlaylistService` zusätzlich injiziert bekommen:
- ✅ `IUnlockedMediaService` - **MUSS hinzugefügt werden**
- ✅ `IAuthService` - **MUSS hinzugefügt werden** (oder über `IUnlockedMediaService` indirekt nutzen)
