# Interfaces

## IUnlockedMediaService

Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsUnlockedAsync` | `DtoMediaEntry entry, CancellationToken cancellationToken = default` | `Task<bool>` | Prüft, ob ein einzelner Medien-Eintrag für den aktuellen Benutzer freigeschaltet ist. Unterstützt nur `DtoMovieCollection` und `DtoTVShow` (andere geben `false` zurück). |
| `GetUnlockedUserIdsAsync` | `DtoMediaEntry entry, CancellationToken cancellationToken = default` | `Task<string[]>` | Ruft alle Benutzer-IDs ab, für die ein Medien-Eintrag freigeschaltet ist. |
| `SetUnlockedUsersAsync` | `DtoMediaEntry entry, string[] userIds, CancellationToken cancellationToken = default` | `Task` | Setzt die Benutzer, für die ein Medien-Eintrag freigeschaltet sein soll (ersetzt vorherige). |
| `GetUnlockedMovieCollectionIdsForUserAsync` | `string userId, CancellationToken cancellationToken = default` | `Task<long[]>` | **BULK-LOAD:** Ruft alle `MovieCollection`-IDs ab, die für einen Benutzer freigeschaltet sind. Eine Query für beliebig viele Collections. |
| `GetUnlockedTVShowIdsForUserAsync` | `string userId, CancellationToken cancellationToken = default` | `Task<long[]>` | **BULK-LOAD:** Ruft alle `TVShow`-IDs ab, die für einen Benutzer freigeschaltet sind. Eine Query für beliebig viele Shows. |
| `GetUnlockedSourceIdsForUserAsync` | `string userId, CancellationToken cancellationToken = default` | `Task<long[]>` | Ruft alle `MediaSourceId`-Werte ab, die für die Benutzer-Freischaltungen relevant sind. |

**Aktuell in PlaylistService genutzt (Zeilen 646–647):**
```csharp
private async Task<(HashSet<long> UnlockedMovieCollectionIds, HashSet<long> UnlockedTVShowIds)> LoadUnlockedMediaIdsAsync(
    string userId, CancellationToken cancellationToken)
{
    var unlockedMovieCollectionIds = await _unlockedMediaService.GetUnlockedMovieCollectionIdsForUserAsync(userId, cancellationToken);
    var unlockedTVShowIds = await _unlockedMediaService.GetUnlockedTVShowIdsForUserAsync(userId, cancellationToken);
    return (unlockedMovieCollectionIds.ToHashSet(), unlockedTVShowIds.ToHashSet());
}
```

**Limitation:** Das Interface lädt nur `MovieCollection`- und `TVShow`-IDs. Es gibt keine Methode zum Bulk-Load des regulären Quellenzugriffs (`MediaSourceUsers`). Das muss in `PlaylistService` zusätzlich implementiert werden.

---

## IPlaylistService

Datei: `VideoWebPlayer/Services/IPlaylistService.cs` (impliziert durch Implementierung)

Die öffentliche API von `PlaylistService`:

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPlaylistsAsync` | `string userId, CancellationToken cancellationToken = default` | `Task<DtoPlaylist[]>` | Ruft alle Playlists eines Benutzers ab. |
| `GetPlaylistAsync` | `long playlistId, string userId, CancellationToken cancellationToken = default` | `Task<DtoPlaylist?>` | Ruft eine einzelne Playlist ab (mit Zugriffsprüfung). |
| `CreatePlaylistAsync` | `string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default` | `Task<DtoPlaylist>` | Erstellt eine neue Playlist. |
| `UpdatePlaylistAsync` | `long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default` | `Task<DtoPlaylist>` | Aktualisiert eine Playlist. |
| `DeletePlaylistAsync` | `long playlistId, string userId, CancellationToken cancellationToken = default` | `Task` | Löscht eine Playlist. |
| `AddMediaToPlaylistAsync` | `long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default` | `Task<DtoPlaylistAddResult>` | Fügt ein Medium (oder mehrere via Cascade) zu einer Playlist hinzu. Nutzt `IsEntryAccessible` über `BuildEntryDtosAsync`. |
| `RemoveMediaFromPlaylistAsync` | `long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default` | `Task` | Entfernt ein Medium aus einer Playlist. |
| `GetPlaylistEntriesAsync` | `long playlistId, string userId, CancellationToken cancellationToken = default` | `Task<DtoPlaylistEntry[]>` | Ruft alle Einträge einer Playlist ab. Nutzt `IsEntryAccessible` über `BuildEntryDtosAsync`. |
| `GetPlaylistEntriesPagedAsync` | `long playlistId, string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default` | `Task<DtoPlaylistEntriesPagedResult>` | Ruft eine paginierte Seite der Einträge ab. Nutzt `IsEntryAccessible` über `BuildEntryDtosAsync`. |

---

## ApplicationDbContext

Relevant für Datenzugriff:

| DbSet | Entitätstyp | Zweck |
|-------|-------------|-------|
| `MediaSourceUsers` | `MediaSourceUser` | Regulärer Quellenzugriff pro Benutzer |
| `UnlockedMediaEntries` | `UnlockedMediaEntry` | Individuelle Freischaltungen pro Benutzer |
| `Movies` | `Movie` | Filme |
| `MovieCollections` | `MovieCollection` | Filmsammlungen |
| `TVShows` | `TVShow` | Serien |
| `TVShowSeasons` | `TVShowSeason` | Staffeln |
| `TVShowEpisodes` | `TVShowEpisode` | Episoden |
| `PlaylistEntries` | `PlaylistEntry` | Einträge in Playlists |
| `Playlists` | `Playlist` | Playlists |
