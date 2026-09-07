# Tasks: Drag & Drop für Playlist-Einträge (Korrektur)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `PlaylistEntryReorderService.MoveEntryBetweenAsync` implementieren — neue Methode für Verschiebung zu beliebiger Position mit konsistenter Anpassung aller betroffenen Einträge | Offen | — |
| 2 | Logik | `PlaylistService.MoveEntryBetweenAsync` implementieren — Wrapper um Service-Methode mit Berechtigung und Mode-Check | Offen | — |
| 3 | UI | `PlaylistEntriesList.razor`: `OnEntryDragStart` ergänzen — `dataTransfer.setData("text/plain", entry.Id.ToString())` für Firefox-Kompatibilität | Offen | — |
| 4 | UI | `PlaylistEntriesList.razor`: `OnEntryDropAsync` korrigieren — ersetze fehlerhafte Logik durch Aufruf von `PlaylistService.MoveEntryBetweenAsync` | Offen | — |
| 5 | Tests (E2E) | `E2E_DragDropReorder_ManualMode_PersistsSortOrder` anpassen — JavaScript-Simulation um `dt.setData('text/plain', targetId.toString())` ergänzen | Offen | — |
| 6 | Tests (E2E) | `E2E_DragDropReorder_ManualMode_PersistsSortOrder` anpassen — Assertion erweitern: Ersetze `Assert.NotEqual(initialOrder, afterDrop)` durch explizite Positionsverifizierung (exakte Zielposition nach Drop und Reload) | Offen | — |
| 7 | Tests (E2E) | E2E-Test `E2E_DragDropReorder_MultiplePositions_MaintainsConsistentOrder` hinzufügen (optional) — prüft mehrfache Verschiebungen und konsistente Anpassung aller betroffenen Einträge | Offen | — |
