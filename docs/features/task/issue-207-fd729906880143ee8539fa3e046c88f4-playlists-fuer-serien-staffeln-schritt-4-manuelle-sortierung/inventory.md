# Bestandsaufnahme: Drag & Drop für Playlist-Einträge (Korrektur)

Diese Bestandsaufnahme analysiert die Implementierung der Drag-&-Drop-Sortierung für Playlist-Einträge im Manual-Mode auf Grundlage der übersetzten Anforderung. Die Analyse deckt auf, dass die Funktionalität teilweise vorhanden ist, aber mit bekannten Fehlern behaftet ist, die in der Anforderung beschrieben werden.

## Zusammenfassung

**Vorhanden:**
- Basis-Infrastruktur für manuelle Sortierung mit `PlaylistEntry.SortOrder` und `PlaylistSortMode.Manual`
- Service-Logik in `PlaylistEntryReorderService` mit korrekten Methoden (`MoveEntryToBeginningAsync`, `BatchReorderEntriesAsync`)
- UI-Komponente `PlaylistEntriesList.razor` mit Drag-&-Drop-Event-Bindungen
- E2E-Test-Infrastruktur mit bestehenden Playlist-Reorder-Tests

**Bekannte Fehler (laut Anforderung):**
1. `OnEntryDropAsync` in `PlaylistEntriesList.razor` (Zeile 348) setzt `targetSortOrder` direkt, was zu Gleichständen und unvorhersehbaren Positionen führt
2. `OnEntryDragStart` fehlt `dataTransfer.setData(...)` für Firefox-Kompatibilität (Zeile 320-326)
3. E2E-Test `E2E_DragDropReorder_ManualMode_PersistsSortOrder` (Zeile 203-234) prüft nur `Assert.NotEqual()`, nicht exakte Zielposition
4. E2E-Test fehlt auch `dataTransfer.setData(...)` in JavaScript-Simulation

**Test-Ausgangszustand:** Siehe [Tests](inventory/tests.md) — 6 E2E-Tests alle bestanden; Reorder-Funktionalität funktioniert teilweise, zeigt aber nur Symptome des bekannten Fehlers nicht in den aktuellen Tests.

## Details

- [Datenmodell](inventory/models.md)
- [Logik und Services](inventory/logic.md)
- [Interfaces und API-Clients](inventory/interfaces.md)
- [Tests](inventory/tests.md)
