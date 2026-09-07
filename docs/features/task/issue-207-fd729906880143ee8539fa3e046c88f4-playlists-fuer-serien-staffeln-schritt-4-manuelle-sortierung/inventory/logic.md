# Logik und Services

## `PlaylistEntryReorderService`
Datei: `VideoWebPlayer/Services/PlaylistEntryReorderService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetMaxSortOrderAsync(playlistId, cancellationToken)` | Public | Gibt aktuellen max. SortOrder einer Playlist zurück oder null |
| `ReorderEntryAsync(playlist, entryId, newSortOrder, cancellationToken)` | Public | Ändert SortOrder eines einzelnen Eintrags (erlaubt Gleichstände!) |
| `MoveEntryToBeginningAsync(playlist, entryId, cancellationToken)` | Public | Verschiebt Eintrag an Anfang: inkrementiert alle anderen SortOrder um 1, setzt gezogenen auf 0 |
| `BatchReorderEntriesAsync(playlist, reorderOperations, cancellationToken)` | Public | Atomar mehrere Einträge reordern (prüft auf eindeutige SortOrder) |
| `EnsureManualSortMode(playlist)` | Public Static | Wirft `PlaylistNotInManualSortModeException` wenn nicht in Manual-Mode |

**Besonderheiten:**
- `ReorderEntryAsync` erlaubt bewusst Gleichstände bei SortOrder (Tiebreaker = AddedAt)
- `MoveEntryToBeginningAsync` nutzt explizite Transaktion für Konsistenz zweier separater DB-Operationen
- `BatchReorderEntriesAsync` prüft auf eindeutige SortOrder und wirft `InvalidOperationException` bei Duplikaten

## `PlaylistService`
Datei: `VideoWebPlayer/Services/PlaylistService.cs` (Auszug)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ReorderPlaylistEntryAsync(playlistId, userId, entryId, newSortOrder, ...)` | Public | Wrapper um `PlaylistEntryReorderService.ReorderEntryAsync` |
| `MoveEntryToBeginningAsync(playlistId, userId, entryId, ...)` | Public | Wrapper um `PlaylistEntryReorderService.MoveEntryToBeginningAsync` |

## `PlaylistEntriesList.razor` (Komponente)
Datei: `VideoWebPlayer/Components/Playlists/PlaylistEntriesList.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnEntryDragStart(entry)` | Private | Event-Handler für dragstart-Event (setzt draggedEntry Variable) |
| `OnEntryDropAsync(targetEntry)` | Private async | Event-Handler für drop-Event - **ENTHÄLT FEHLER** |
| `OnEntryDragEnd()` | Private | Event-Handler für dragend-Event (setzt draggedEntry auf null) |
| `MoveEntryToBeginningAsync(entry)` | Private async | Ruft PlaylistClient.MoveEntryToBeginningAsync auf |
| `MoveEntryToEndAsync(entry)` | Private async | Ruft PlaylistClient.RequestMaxSortOrderAsync und ReorderEntryAsync auf |
| `ReorderEntryAsync(entry, resolveNewSortOrder)` | Private async | Generischer Wrapper für Reorder-Aufrufe |

**Fehler in OnEntryDropAsync (Zeile 328-349):**
- Zeile 348: `await ReorderEntryAsync(entryToMove, () => Task.FromResult(targetSortOrder))`
- Setzt gezogenen Eintrag direkt auf targetSortOrder, ohne andere Einträge zu verschieben
- Führt zu Gleichständen, die durch AddedAt aufgelöst werden (unprediktabel)
- Sollte das Muster von `MoveEntryToBeginningAsync` nutzen (alle anderen verschieben)

**Fehler in OnEntryDragStart (Zeile 320-326):**
- Fehlt: `dataTransfer.setData("text/plain", entry.Id.ToString())`
- Firefox erfordert mindestens einen `setData`-Aufruf für Drag-&-Drop
- Chrome/Safari funktioniert auch ohne, aber Firefox ignoriert die Operation

## Rendering und Event-Bindungen (PlaylistEntriesList.razor)
- Zeile 62: `draggable="@(IsManualMode ? "true" : "false")"`
- Zeile 63: `@ondragstart="() => OnEntryDragStart(entry)"`
- Zeile 64: `@ondragover:preventDefault="IsManualMode"`
- Zeile 65: `@ondrop="() => OnEntryDropAsync(entry)"`
- Zeile 66: `@ondragend="OnEntryDragEnd"`
