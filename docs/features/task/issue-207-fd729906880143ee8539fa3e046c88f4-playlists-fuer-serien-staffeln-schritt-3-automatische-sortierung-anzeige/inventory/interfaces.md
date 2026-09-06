# Interfaces

## `IPlaylistService`
Datei: `VideoWebPlayer/Services/IPlaylistService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPlaylistsAsync` | `userId: string`, `cancellationToken: CancellationToken = default` | `Task<DtoPlaylist[]>` | Liefert alle Playlists eines Benutzers |
| `GetPlaylistAsync` | `playlistId: long`, `userId: string`, `cancellationToken: CancellationToken = default` | `Task<DtoPlaylist?>` | Liefert eine einzelne Playlist oder null |
| `CreatePlaylistAsync` | `userId: string`, `name: string`, `description: string?`, `sortMode: string?`, `cancellationToken: CancellationToken = default` | `Task<DtoPlaylist>` | Erstellt eine neue Playlist |
| `UpdatePlaylistAsync` | `playlistId: long`, `userId: string`, `name: string`, `description: string?`, `sortMode: string?`, `cancellationToken: CancellationToken = default` | `Task<DtoPlaylist>` | Aktualisiert eine Playlist |
| `DeletePlaylistAsync` | `playlistId: long`, `userId: string`, `cancellationToken: CancellationToken = default` | `Task` | Löscht eine Playlist |
| `AddMediaToPlaylistAsync` | `playlistId: long`, `userId: string`, `mediaType: string`, `mediaId: long`, `cancellationToken: CancellationToken = default` | `Task<DtoPlaylistAddResult>` | Fügt Medieninhalt (mit Kaskade) hinzu |
| `RemoveMediaFromPlaylistAsync` | `playlistId: long`, `userId: string`, `mediaType: string`, `mediaId: long`, `cancellationToken: CancellationToken = default` | `Task` | Entfernt Medieninhalt |
| `GetPlaylistEntriesAsync` | `playlistId: long`, `userId: string`, `cancellationToken: CancellationToken = default` | `Task<DtoPlaylistEntry[]>` | Liefert alle Einträge einer Playlist |

### Wichtige Hinweise zu Schritt 3

**NICHT vorhanden:**
- Methode `GetPlaylistEntriesPagedAsync()` – **muss zum Interface hinzugefügt werden** für Schritt 3
- Diese Methode würde Parameter für `pageNumber` und `pageSize` benötigen
- Würde ein neues DTO `DtoPlaylistEntriesPagedResult` oder ähnlich zurückliefern mit `entries[]`, `totalCount`, `hasNextPage`

**Mögliche Signatur (zukünftig):**
```csharp
Task<DtoPlaylistEntriesPagedResult> GetPlaylistEntriesPagedAsync(
    long playlistId, 
    string userId, 
    int pageNumber, 
    int pageSize, 
    CancellationToken cancellationToken = default
);
```

---

## Optional für Schritt 3: `IPlaylistSortingStrategy`

**Status:** NICHT IMPLEMENTIERT – wird optional erwähnt in der Anforderung

Wenn ein Strategy Pattern verwendet würde, könnte folgendes Interface definiert werden:

```csharp
public interface IPlaylistSortingStrategy
{
    /// <summary>
    /// Sortiert eine Liste von Playlist-Einträgen nach der implementierten Strategie.
    /// </summary>
    Task<IReadOnlyList<DtoPlaylistEntry>> SortEntriesAsync(
        IReadOnlyList<DtoPlaylistEntry> entries,
        ApplicationDbContext db,
        PlaylistSortMode sortMode,
        CancellationToken cancellationToken
    );
}
```

**Mögliche Implementierungen:**
- `ByReleaseDateSortingStrategy` – für die automatische Sortierung nach Erscheinungsdatum
- Weitere Strategien könnten später für andere Sortierungen hinzugefügt werden

**Beachte:** Die aktuelle Implementation nutzt diese Abstraktionen noch nicht. Das Service-Pattern ist in `PlaylistService` inline implementiert via `MediaTypeHandler`.
