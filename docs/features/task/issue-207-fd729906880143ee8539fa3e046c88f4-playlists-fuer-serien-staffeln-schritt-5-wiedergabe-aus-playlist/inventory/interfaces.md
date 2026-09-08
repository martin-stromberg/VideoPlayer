# Interfaces - Playlist-Wiedergabe

## `IPlaylistService`
Datei: `VideoWebPlayer/Services/IPlaylistService.cs`

### Methoden (vollständige Liste)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPlaylistsAsync` | `string userId, CancellationToken` | `Task<DtoPlaylist[]>` | Ruft alle Playlists für einen Benutzer ab |
| `GetPlaylistAsync` | `long playlistId, string userId, CancellationToken` | `Task<DtoPlaylist?>` | Ruft eine einzelne Playlist ab |
| `CreatePlaylistAsync` | `string userId, string name, string? description, string? sortMode, CancellationToken` | `Task<DtoPlaylist>` | Erstellt neue Playlist |
| `UpdatePlaylistAsync` | `long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken` | `Task<DtoPlaylist>` | Aktualisiert Playlist (sortMode wird ignoriert) |
| `DeletePlaylistAsync` | `long playlistId, string userId, CancellationToken` | `Task` | Löscht eine Playlist |
| `AddMediaToPlaylistAsync` | `long playlistId, string userId, string mediaType, long mediaId, CancellationToken` | `Task<DtoPlaylistAddResult>` | Fügt Medien hinzu (mit Kakadenauflösung) |
| `RemoveMediaFromPlaylistAsync` | `long playlistId, string userId, string mediaType, long mediaId, CancellationToken` | `Task` | Entfernt Medien |
| `GetPlaylistEntriesAsync` | `long playlistId, string userId, CancellationToken` | `Task<DtoPlaylistEntry[]>` | Ruft alle Einträge ab (sortiert) |
| `GetPlaylistEntriesPagedAsync` | `long playlistId, string userId, int pageNumber, int pageSize, CancellationToken` | `Task<DtoPlaylistEntriesPagedResult>` | Ruft paginierte Einträge ab (sortiert) |
| `ReorderPlaylistEntryAsync` | `long playlistId, string userId, long entryId, long newSortOrder, CancellationToken` | `Task` | Ändert Sortierreihenfolge eines Eintrags |
| `BatchReorderPlaylistEntriesAsync` | `long playlistId, string userId, List<(long EntryId, long NewSortOrder)> ops, CancellationToken` | `Task<DtoPlaylistEntry[]>` | Ändert mehrere Einträge atomar |
| `ChangeSortModeAsync` | `long playlistId, string userId, string newMode, bool? confirm, CancellationToken` | `Task<DtoPlaylist>` | Wechselt Sortiermodus |
| `GetMaxSortOrderAsync` | `long playlistId, string userId, CancellationToken` | `Task<long?>` | Ruft maximale `SortOrder` ab |
| `MoveEntryToBeginningAsync` | `long playlistId, string userId, long entryId, CancellationToken` | `Task<DtoPlaylistEntry>` | Verschiebt Eintrag an den Anfang |
| `MoveEntryBetweenAsync` | `long playlistId, string userId, long entryId, long targetOrder, CancellationToken` | `Task` | Verschiebt Eintrag zwischen Positionen |

### Relevante Hinweise für Wiedergabe
- **Sortierung:** `GetPlaylistEntriesAsync` und `GetPlaylistEntriesPagedAsync` liefern bereits sortierte Einträge (nach `SortPlaylistEntriesForModeAsync`)
- **Freischaltung:** Einträge enthalten `IsAccessible` Flag, das bei der Auflösung berechnet wird
- **Kein Playback-State:** Keine Methoden zur Verfolgung der aktuellen Wiedergabeposition in der `IPlaylistService` (wird über neue `IPlaylistContextProvider` geplant)

**Neu für diese Anforderung:**
```csharp
Task<DtoPlaylistEntry?> GetNextPlaylistEntryAsync(long playlistId, long currentEntryId, CancellationToken)
Task<DtoPlaylistEntry?> GetPreviousPlaylistEntryAsync(long playlistId, long currentEntryId, CancellationToken)
Task<DtoPlaylistPlaybackStart> StartPlaylistAsync(long playlistId, string userId, long? entryId, CancellationToken)
Task<DtoPlaylistEntry?> AdvancePlaylistAsync(long playlistId, long currentEntryId, CancellationToken)
```

---

## `IUnlockedMediaService`
Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs`

### Methoden (für Wiedergabe relevant)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsUnlockedAsync` | `DtoMediaEntry entry, CancellationToken` | `Task<bool>` | Prüft, ob Eintrag für aktuellen Benutzer freigeschalten ist |
| `GetUnlockedMovieCollectionIdsForUserAsync` | `string userId, CancellationToken` | `Task<long[]>` | Lädt freigeschaltete Filmsammlungen für Benutzer |
| `GetUnlockedTVShowIdsForUserAsync` | `string userId, CancellationToken` | `Task<long[]>` | Lädt freigeschaltete Serien für Benutzer |
| `GetMediaSourceIdsForUserAsync` | `string userId, CancellationToken` | `Task<long[]>` | Lädt Media-Sources mit regulärem Zugriff |
| `IsAccessible` | `bool hasSourceAccess, bool isUnlocked` | `bool` | **Kanonische Zugriffsprüfung**: `hasSourceAccess OR isUnlocked` |

### Wichtig für Wiedergabe
- `IsAccessible` wird von `PlaylistEntryAccessResolver.CheckEntryAccessible` aufgerufen
- Die Regel ist einfach: Zugriff wenn (1) regulärer Source-Zugriff ODER (2) explizite Unlock-Freigabe
- Wird bei jedem Laden von Playlist-Einträgen aufgerufen, um aktuelle Freischaltungen zu prüfen

---

## `IPlaylistContextProvider` (NEU für Schritt 5)
Datei: `VideoWebPlayer/Services/IPlaylistContextProvider.cs` (noch nicht implementiert)

### Geplante Methoden

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `SetPlaylistContext` | `long playlistId, long currentEntryId, int totalCount` | `void` | Setzt aktiven Playlist-Kontext während Wiedergabe |
| `GetPlaylistContext` | `(keine)` | `PlaylistContext?` | Ruft aktuellen Kontext ab oder `null`, wenn nicht aus Playlist |
| `ClearPlaylistContext` | `(keine)` | `void` | Löscht Kontext (z.B. beim Start eines Videos außerhalb Playlist) |

### Geplante Datenstruktur

```csharp
public class PlaylistContext
{
    public long PlaylistId { get; set; }
    public long CurrentEntryId { get; set; }
    public int TotalCount { get; set; }
    public string PlaylistName { get; set; }  // optional, für UI
    public int CurrentPosition => /* berechnet aus sortierter Position */
}
```

### Zweck
- Verwaltet Wiedergabe-Kontext während einer Sitzung
- Ermöglicht UI-Komponenten und Services, auf aktuellen Kontext zuzugreifen
- Lifecycle: wird bei `StartPlaylistAsync` gesetzt, bei Weiterschalten aktualisiert, gelöscht beim Schließen oder Start außerhalb Playlist

