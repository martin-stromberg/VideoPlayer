# API-Client-Interfaces

## `IPlaylistApiClient`
Datei: `VideoWebPlayer.Client/IPlaylistApiClient.cs`

Client-seitige Playlist-API-Operationen. Implementiert von `VideoWebPlayerClient`.

### Playlist-Verwaltung

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RequestPlaylistsAsync()` | - | `Task<IEnumerable<DtoPlaylist>>` | Alle Playlisten des Benutzers abrufen |
| `RequestPlaylistAsync(long playlistId)` | playlistId | `Task<DtoPlaylist?>` | Einzelne Playlist abrufen |
| `CreatePlaylistAsync(DtoCreatePlaylistRequest request)` | request | `Task<DtoPlaylist>` | Neue Playlist erstellen |
| `UpdatePlaylistAsync(long playlistId, DtoUpdatePlaylistRequest request)` | playlistId, request | `Task<DtoPlaylist>` | Playlist aktualisieren |
| `DeletePlaylistAsync(long playlistId)` | playlistId | `Task` | Playlist löschen |
| `ChangeSortModeAsync(long playlistId, DtoChangeSortModeRequest request)` | playlistId, request | `Task<DtoPlaylist>` | Sortiermodus ändern |

### Einträge verwalten

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RequestPlaylistEntriesPagedAsync(long playlistId, int pageNumber, int pageSize, CancellationToken cancellationToken)` | playlistId, pageNumber, pageSize, cancellationToken | `Task<DtoPlaylistEntriesPagedResult>` | Paginierte Einträge abrufen |
| `AddMediaToPlaylistAsync(long playlistId, DtoAddMediaToPlaylistRequest request)` | playlistId, request | `Task<DtoPlaylistAddResult>` | Medium zur Playlist hinzufügen |
| `RemoveMediaFromPlaylistAsync(long playlistId, string mediaType, long mediaId)` | playlistId, mediaType, mediaId | `Task` | Medium aus Playlist entfernen |
| `ReorderPlaylistEntryAsync(long playlistId, long entryId, DtoReorderPlaylistEntryRequest request)` | playlistId, entryId, request | `Task` | Einzelnen Eintrag neu ordnen (Manual-Mode) |
| `BatchReorderPlaylistEntriesAsync(long playlistId, DtoBatchReorderPlaylistEntriesRequest request)` | playlistId, request | `Task<IEnumerable<DtoPlaylistEntry>>` | Mehrere Einträge atomar neu ordnen |
| `RequestMaxSortOrderAsync(long playlistId)` | playlistId | `Task<long?>` | Maximale manuelle Sortierreihenfolge abrufen |
| `MoveEntryToBeginningAsync(long playlistId, long entryId)` | playlistId, entryId | `Task<DtoPlaylistEntry>` | Eintrag an den Anfang verschieben |
| `MoveEntryBetweenAsync(long playlistId, long entryId, DtoReorderPlaylistEntryRequest request)` | playlistId, entryId, request | `Task` | Eintrag zwischen zwei Positionen verschieben |

### Wiedergabe und Navigation

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StartPlaylistAsync(long playlistId, long? entryId)` | playlistId, entryId (optional) | `Task<DtoPlaylistPlaybackStart>` | **Start der Wiedergabe:** Ermittelt Starteintrag, Stream-URL und **StartPositionSeconds** aus Weiterschauen-DB. Wird für Initial-Start und Restart genutzt. Liefert vollständige `DtoPlaylistPlaybackStart` mit Position. |
| `GetNextPlaylistEntryAsync(long playlistId, long currentEntryId)` | playlistId, currentEntryId | `Task<DtoPlaylistNavigationResult?>` | **Navigation Nächster:** Nächster spielbarer Eintrag. **LIMITATION:** Liefert nur `Entry` und `Position`, **KEINE** `StartPositionSeconds`. |
| `GetPreviousPlaylistEntryAsync(long playlistId, long currentEntryId)` | playlistId, currentEntryId | `Task<DtoPlaylistNavigationResult?>` | **Navigation Vorheriger:** Vorheriger spielbarer Eintrag. **LIMITATION:** Liefert nur `Entry` und `Position`, **KEINE** `StartPositionSeconds`. |
| `AdvancePlaylistAsync(long playlistId, long currentEntryId)` | playlistId, currentEntryId | `Task<DtoPlaylistNavigationResult?>` | **Auto-Advance bei Medienende:** Nächster Eintrag. **LIMITATION:** Liefert nur `Entry` und `Position`, **KEINE** `StartPositionSeconds`. |

## Vergleich: Volle Information vs. Begrenzte Information

### `StartPlaylistAsync()` — **Vollständig**
```csharp
DtoPlaylistPlaybackStart {
  PlaylistId, PlaylistName, TotalCount, CurrentPosition,
  CurrentEntryId, CurrentEntry,
  StreamUrl, MediaType, MediaId,
  StartPositionSeconds  ← ✅ VORHANDEN
}
```

### Navigation-APIs (Next/Previous/Advance) — **Begrenzt**
```csharp
DtoPlaylistNavigationResult {
  Entry,      // ← nur der neue Eintrag, nicht die Position
  Position    // ← Position in der Playlist, nicht die Weiterschauen-Position
  // StartPositionSeconds  ← ❌ FEHLT!
}
```

## Backend-Erweiterungspunkte

Um Szenario A (neue Position in Response) zu implementieren, müsste das Backend:

1. **`DtoPlaylistNavigationResult` erweitern:**
   ```csharp
   public class DtoPlaylistNavigationResult
   {
       public DtoPlaylistEntry Entry { get; set; }
       public int Position { get; set; }
       public long StartPositionSeconds { get; set; }  // ← Neu
   }
   ```

2. **Navigation-Endpoints aktualisieren** (`/play/next`, `/play/previous`, `/play/advance`):
   - Nach Ermittlung des neuen Eintrags: `ContinueWatchingEntry` mit `(playlistId, newEntryId)` abfragen
   - `StartPositionSeconds` in Response einschließen

3. **Betroffene Methoden:**
   - `PlaylistsController.GetNextEntry(...)`
   - `PlaylistsController.GetPreviousEntry(...)`
   - `PlaylistsController.AdvanceEntry(...)`

## Fehlende Methoden (im Kontext dieser Anforderung)

Derzeit gibt es **keine** Methode wie:
```csharp
Task<long> GetContinueWatchingPositionAsync(long entryId, long? playlistId);
```

Dies würde für Szenario B erforderlich sein (zusätzlicher API-Call pro Navigation).

## Zusammenfassung für die Implementierung

- **API-Status:** Die Basis-Navigation ist implementiert, aber ohne Weiterschauen-Position in der Response
- **Für Szenario A:** Backend-Änderung erforderlich (neue Felder in `DtoPlaylistNavigationResult`)
- **Für Szenario B:** Neue API-Methode oder Erweiterung einer bestehenden erforderlich
- **Für Szenario C:** Nur Front-End-Änderung erforderlich (Parent setzt `StartPositionSeconds = null`)

