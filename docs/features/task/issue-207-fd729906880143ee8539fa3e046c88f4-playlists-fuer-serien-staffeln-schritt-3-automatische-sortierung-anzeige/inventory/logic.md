# Bestandsaufnahme: Logikklassen und Services

## `PlaylistService`
Datei: `VideoWebPlayer\Services\PlaylistService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `.ctor(ApplicationDbContext, EventManager, IOptions<PlaylistSettings>)` | public | Konstruktor mit Dependency Injection von DB-Kontext und Event Manager |
| `GetPlaylistsAsync(string userId, CancellationToken)` | public | Ruft alle Playlists eines Benutzers ab (asynchron) |
| `GetPlaylistAsync(long playlistId, string userId, CancellationToken)` | public | Ruft eine einzelne Playlist mit Zugriffsprüfung ab |
| `CreatePlaylistAsync(string userId, string name, string? description, string? sortMode, CancellationToken)` | public | Erstellt eine neue Playlist |
| `UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken)` | public | Aktualisiert Metadaten einer Playlist |
| `DeletePlaylistAsync(long playlistId, string userId, CancellationToken)` | public | Löscht eine Playlist |
| `AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken)` | public | Fügt Medien zur Playlist hinzu (mit Kaskadenlogik) |
| `RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken)` | public | Entfernt ein Medium aus der Playlist |
| `GetPlaylistEntriesAsync(long playlistId, string userId, CancellationToken)` | public | Ruft alle Einträge einer Playlist ab (nicht paginiert) |
| `GetPlaylistEntriesPagedAsync(long playlistId, string userId, int pageNumber, int pageSize, CancellationToken)` | public | **Ruft paginierte Einträge einer Playlist ab** (Zeile 550) - nutzt `LoadTitlesForMediaRefsAsync` um Titel und Metadaten für die aktuelle Seite zu laden |
| `ToDto(Playlist)` | private static | Konvertiert Playlist-Entity zu DTO (Zeile 243) |
| `ToDto(PlaylistEntry, string mediaTitle, string? parentMediaTitle)` | private static | **Konvertiert PlaylistEntry-Entity zu DtoPlaylistEntry** (Zeile 535) - setzt `IsAccessible` hart auf `true` |
| `LoadValidPlaylistEntriesAsync(long playlistId, CancellationToken)` | private | Lädt Einträge und entfernt verwaiste Einträge (Zeile 592) |
| `LoadTitlesForMediaRefsAsync(IEnumerable<(string, long)>, CancellationToken)` | private | Lädt Titel für Media-Referenzen, gruppiert nach Medientyp (Zeile 634) |
| `GetParentMediaRefs(IEnumerable<PlaylistEntry>)` | private static | Projiziert übergeordnete Medien-Referenzen (Zeile 654) |
| `SortPlaylistEntriesByReleaseDateAsync(List<PlaylistEntry>, CancellationToken)` | private | Sortiert Einträge nach Veröffentlichungsdatum mit Hierarchie-Beachtung (Zeile 676) |

### Abhängigkeiten (Konstruktor, Zeile 30-38):
- `ApplicationDbContext _db` - Datenbankkontext
- `EventManager _eventManager` - Event Publisher
- `IOptions<PlaylistSettings>? playlistSettings` - Konfiguration (optional)

**Status der Anforderung:**
- ❌ `IUnlockedMediaService` ist NICHT injiziert (benötigt für Freischaltungsprüfung)
- ❌ `IAuthService` ist NICHT injiziert (benötigt um aktuellen Benutzer zu ermitteln)
- ❌ `ToDto()` (Zeile 535-547) setzt `IsAccessible` hart auf `true` statt echte Prüfung
- ❌ Keine Logik zur Befüllung von `PosterPictureId` in `ToDto()`

### MediaTypeHandlers (interne Architektur):
Das Service nutzt ein Dictionary mit `MediaTypeHandler`-Objekten (Zeile 271-380), die pro Medientyp definieren:
- `LoadTitlesAsync` - Titel laden
- `LoadExistingIdsAsync` - Überprüfung auf Existenz ohne Titel
- `LoadCascadeChildrenAsync` - (Optional) Kaskadenlogik für Kinder (z.B. Episodes unter TVShowSeason)
- `LoadReleaseDateAsync` - Veröffentlichungsdaten laden
- `GetHierarchySequenceAsync` - Hierarchie und Sequenznummern

Dieses Pattern kann als Vorlage für das Laden von `PosterPictureId` erweitert werden.

---

## `IUnlockedMediaService`
Datei: `VideoWebPlayer\Services\IUnlockedMediaService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsUnlockedAsync()` | `DtoMediaEntry entry, CancellationToken` | `Task<bool>` | Prüft ob ein Medium für aktuellen Benutzer freigeschaltet ist |
| `GetUnlockedUserIdsAsync()` | `DtoMediaEntry entry, CancellationToken` | `Task<string[]>` | Ruft alle Benutzer-IDs ab, für die ein Medium freigeschaltet ist |
| `SetUnlockedUsersAsync()` | `DtoMediaEntry entry, string[] userIds, CancellationToken` | `Task` | Setzt die Benutzer, für die ein Medium freigeschaltet ist (ersetzt existierende) |
| `GetUnlockedMovieCollectionIdsForUserAsync()` | `string userId, CancellationToken` | `Task<long[]>` | Ruft alle Filmsammlung-IDs ab, die für einen Benutzer freigeschaltet sind |
| `GetUnlockedTVShowIdsForUserAsync()` | `string userId, CancellationToken` | `Task<long[]>` | Ruft alle TV-Show-IDs ab, die für einen Benutzer freigeschaltet sind |
| `GetUnlockedSourceIdsForUserAsync()` | `string userId, CancellationToken` | `Task<long[]>` | Ruft Media-Source-IDs ab, die für einen Benutzer freigeschaltet sind |

---

## `UnlockedMediaService`
Datei: `VideoWebPlayer\Services\UnlockedMediaService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `.ctor(ApplicationDbContext, IAuthService)` | public | Konstruktor mit DB und Auth-Service |
| `IsUnlockedAsync(DtoMediaEntry, CancellationToken)` | public | Prüft Freischaltungsstatus gegen DB; nutzt `_authService.CurrentUser` um Benutzer zu ermitteln (Zeile 38) |
| `GetUnlockedUserIdsAsync(DtoMediaEntry, CancellationToken)` | public | Ruft alle Benutzer ab, für die Medium freigeschaltet ist |
| `SetUnlockedUsersAsync(DtoMediaEntry, string[], CancellationToken)` | public | Setzt Freischaltungen (Admin-Operation) |
| `GetUnlockedMovieCollectionIdsForUserAsync(string, CancellationToken)` | public | Filtert Filmsammlung-Freischaltungen |
| `GetUnlockedTVShowIdsForUserAsync(string, CancellationToken)` | public | Filtert TV-Show-Freischaltungen |
| `GetUnlockedSourceIdsForUserAsync(string, CancellationToken)` | public | Aggregiert Source-IDs aus Filmsammlung- und Show-Freischaltungen |
| `GetIds(DtoMediaEntry)` | private static | Extrahiert MovieCollectionId oder TVShowId aus DtoMediaEntry (Zeile 142) |

### Abhängigkeiten (Konstruktor, Zeile 25-29):
- `ApplicationDbContext _db` - Datenbankkontext
- `IAuthService _authService` - Authentication Service für aktuellen Benutzer

### Wichtige Implementierungsdetails:
- `IsUnlockedAsync()` (Zeile 32-49): Nutzt `_authService.CurrentUser` (Zeile 38) um aktuellen Benutzer zu ermitteln
- Unterstützt nur `DtoMovieCollection` und `DtoTVShow` (Zeile 142-148); andere Medientypen geben `(null, null)` zurück
- Prüft gegen `UnlockedMediaEntry` DB-Tabelle
- Alle Abfragen sind async und nutzen `AsNoTracking()` für Performance

---

## `IAuthService`
Datei: `VideoWebPlayer\Services\Authentication\IAuthService.cs`

| Methode / Property | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CurrentUser` | - | `ApplicationUser?` | Property: Ruft aktuellen authentifizierten Benutzer ab (kann null sein) |
| `ImpersonateAsync()` | `ImpersonateRequest` | `Task<AuthorizationToken>` | Admin-Operation: Gibt Token für anderen Benutzer aus |
| `LoginAsync()` | `AuthenticationRequest` | `Task<AuthorizationToken>` | Authentifizierung mit E-Mail und Passwort |

### Implementierung (`AuthService` Klasse, Zeile 93-189):
- `CurrentUser` Property (Zeile 130-136): Ruft `_userManager.GetUserAsync(_httpContextAccessor?.HttpContext?.User).Result` auf
  - Nutzt `IHttpContextAccessor` um auf `HttpContext.User` zuzugreifen
  - **Hinweis:** Nutzt `.Result` (synchrone Blockierung) - asynchrone Alternative könnte benötigt werden für `PlaylistService.ToDto()`

### Abhängigkeiten:
- `IHttpContextAccessor _httpContextAccessor` - HTTP-Kontext-Zugriff
- `UserManager<ApplicationUser> _userManager` - Identity-Benutzerverwaltung
- Weitere für Auth nicht relevante Dependencies

---

## Abhängigkeiten zwischen Services

```
PlaylistService
├─ (aktuell) ─ ApplicationDbContext
├─ (aktuell) ─ EventManager
├─ (FEHLT)   ─ IUnlockedMediaService (für IsAccessible-Befüllung)
└─ (FEHLT)   ─ IAuthService (zum Ermitteln des aktuellen Benutzers)

UnlockedMediaService
├─ ApplicationDbContext
└─ IAuthService
    └─ IHttpContextAccessor
    └─ UserManager<ApplicationUser>

MediaTypeHandlers (intern in PlaylistService)
└─ Nutzen ApplicationDbContext direkt für Titel-, Datum-, Hierarchie-Abfragen
```
