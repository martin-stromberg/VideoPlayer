# Bestandsaufnahme: Interfaces

## `IPlaylistService` Interface
Datei: `VideoWebPlayer\Services\IPlaylistService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPlaylistsAsync` | `string userId`, `CancellationToken cancellationToken = default` | `Task<DtoPlaylist[]>` | Ruft alle Playlists für einen Benutzer ab |
| `GetPlaylistAsync` | `long playlistId`, `string userId`, `CancellationToken cancellationToken = default` | `Task<DtoPlaylist?>` | Ruft eine Playlist ab (oder null) |
| `CreatePlaylistAsync` | `string userId`, `string name`, `string? description`, `string? sortMode`, `CancellationToken cancellationToken = default` | `Task<DtoPlaylist>` | Erstellt Playlist |
| `UpdatePlaylistAsync` | `long playlistId`, `string userId`, `string name`, `string? description`, `string? sortMode`, `CancellationToken cancellationToken = default` | `Task<DtoPlaylist>` | Aktualisiert Playlist |
| `DeletePlaylistAsync` | `long playlistId`, `string userId`, `CancellationToken cancellationToken = default` | `Task` | Löscht Playlist |
| **`AddMediaToPlaylistAsync`** | `long playlistId`, `string userId`, `string mediaType`, `long mediaId`, `CancellationToken cancellationToken = default` | `Task<DtoPlaylistEntry>` | **Kritische Methode** — Aktuell: Gibt nur einen Eintrag zurück, wirft Exception bei Duplikat |
| `RemoveMediaFromPlaylistAsync` | `long playlistId`, `string userId`, `string mediaType`, `long mediaId`, `CancellationToken cancellationToken = default` | `Task` | Entfernt einen Eintrag |
| `GetPlaylistEntriesAsync` | `long playlistId`, `string userId`, `CancellationToken cancellationToken = default` | `Task<DtoPlaylistEntry[]>` | Ruft alle Einträge ab, entfernt Waisenkinder |

### Verbraucher des `IPlaylistService` Interface

1. **`PlaylistsController`** (Zeile 17)
   - Injiziert `IPlaylistService _playlistService`
   - Ruft alle Methoden auf und mappt Exceptions zu HTTP-Status-Codes
   - `AddMediaToPlaylist` (Zeile 210–244): Nutzt `MapInvalidOperationException`, um "bereits in dieser Playlist vorhanden" auf HTTP 409 zu mappen

2. **`PlaylistDetail.razor`** (Komponente)
   - Injiziert `VideoWebPlayerClient`
   - Der Client ruft seinerseits den API-Endpoint auf (siehe [DTOs](dtos.md))
   - `AddEntryAsync` (Zeile 188–212) fängt `HttpRequestException` mit StatusCode 409 ab und zeigt Fehlermeldung

### Interface-Signaturen-Änderung erforderlich (Anforderung)

Aktuell:
```csharp
Task<DtoPlaylistEntry> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default);
```

Nach Anforderung (Option A):
```csharp
Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default);
```

Das ist eine **Breaking Change**, die folgende Verbraucher betrifft:
- PlaylistsController (Zeile 215–217) — Muss Response umwandeln
- VideoWebPlayerClient (Zeile 450–455) — Muss Rückgabetyp ändern
- PlaylistDetail.razor (Zeile 193–198) — Muss Fehlerbehandlung anpassen (nicht mehr HTTP 409)
