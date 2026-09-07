# Übersetzung der Kundenanforderung: Drag & Drop für Playlist-Einträge (Korrektur)

## Fachliche Zusammenfassung

Die Drag-&-Drop-Funktionalität zum Umsortieren von Einträgen in Manual-Mode-Playlists funktioniert nicht korrekt: der gezogene Eintrag wird nicht an der exakten Position abgelegt, wo der Benutzer ihn ablegt, sondern an einer durch die Auflösungsreihenfolge (`AddedAt`-Tiebreaker) zufällig bestimmten Position. Das ist dasselbe Fehlermuster, das bereits für die Schnellaktion „An Anfang" durch eine gezielte Verschiebe-Logik behoben wurde. Die Implementierung in `PlaylistEntriesList.razor` (Methode `OnEntryDropAsync`) muss so angepasst werden, dass sie wie „An Anfang"/„An Ende" alle beteiligten Einträge konsistent umsortiert, statt kollidierenden `SortOrder`-Werte zu setzen.

Zusätzlich ist der E2E-Test zu schwach und würde den Fehler nicht abfangen; er muss die exakte resultierende Reihenfolge verifizieren. Im `dragstart`-Handler fehlt außerdem ein `dataTransfer.setData(...)`-Aufruf, der für die Firefox-Kompatibilität erforderlich ist.

## Betroffene Klassen und Komponenten

### UI-Komponenten
- `VideoWebPlayer/Components/Playlists/PlaylistEntriesList.razor` — **`OnEntryDropAsync`-Methode** (Zeile 328–349) und **`OnEntryDragStart`-Methode** (Zeile 320–326)
  - Fehler: `OnEntryDropAsync` ruft aktuell `ReorderEntryAsync(entryToMove, () => Task.FromResult(targetSortOrder))` auf
  - Problem: Dies weist dem gezogenen Eintrag denselben `SortOrder` wie dem Ziel-Eintrag zu
  - Fehlende Ergänzung: Im `dragstart`-Handler fehlt `dataTransfer.setData(...)`

### Services
- `VideoWebPlayer/Services/PlaylistEntryReorderService.cs` — existiert bereits mit korrekten Methoden
  - `MoveEntryToBeginningAsync` — implementiert das korrekte Muster (wird für Vorlage herangezogen)
  - `BatchReorderEntriesAsync` — kann für atomare Mehrfach-Reordering genutzt werden (optional)

### Tests
- `VideoWebPlayer.Tests/PlaylistReorderE2ETests.cs` — `E2E_DragDropReorder_ManualMode_PersistsSortOrder` (Zeile 203–234)
  - Aktuell prüft der Test nur, dass sich *irgendetwas* geändert hat (`Assert.NotEqual(initialOrder, afterDrop)`)
  - Problem: Der Test besteht auch mit dem beschriebenen Fehlverhalten
  - Fehlende Ergänzung: Kein `dataTransfer.setData(...)` im `dragstart`-Handler des Tests

## Implementierungsansatz

### 1. Fehler in `OnEntryDropAsync` korrigieren

Gegenwärtig:
```csharp
await ReorderEntryAsync(entryToMove, () => Task.FromResult(targetSortOrder));
```

Korrekt sollte die Implementierung sein:
- Die neue Position des gezogenen Eintrags muss exakt die Position des Ziel-Eintrags sein (oder eine differenzierte Position vor/nach, abhängig von der genauen Drop-Position)
- Alle Einträge zwischen Quell- und Zielposition müssen konsistent verschoben werden (um je ±1 erhöht/erniedrigt)
- Dies kann durch einen Aufruf an `PlaylistEntryReorderService.MoveEntryToBeginningAsync` (für Drop auf Position 0) oder durch eine zum `BatchReorderEntriesAsync`-Muster analoge Logik erreicht werden

Implementierungsoptionen:
- **Option A (empfohlen):** Nutze `PlaylistEntryReorderService` und implementiere eine neue Methode `MoveEntryBetweenAsync(Playlist, long entryId, long targetSortOrder)`, die analog zu `MoveEntryToBeginningAsync` alle Einträge im Bereich der Zielposition verschiebt
- **Option B:** Nutze `BatchReorderEntriesAsync`, um alle betroffenen Einträge in einer atomaren Operation neu zu ordnen

### 2. `dragstart`-Handler ergänzen

In `OnEntryDragStart` oder in der Razor-Komponente selbst (via JavaScript Interop oder inline Handler):
```csharp
dataTransfer.setData("text/plain", entryId.ToString());
// oder ein beliebiger MIME-Type; Firefox erfordert mindestens einen Aufruf
```

Dies ist erforderlich, damit Firefox die Drag-&-Drop-Operation überhaupt startet.

### 3. E2E-Test verstärken

Der Test `E2E_DragDropReorder_ManualMode_PersistsSortOrder` muss:
- **Exakte Zielposition verifizieren:** Anstatt nur zu prüfen, dass sich die Reihenfolge "irgendwie" geändert hat, muss der Test explizit eine erwartete Zielposition festlegen und diese verifizieren. Beispiel:
  - Ziehe den Eintrag an Position 2 (Index 2, letzter von drei) auf Position 0 (Index 0, erster)
  - Verifiziere, dass die exakte resultierende Reihenfolge `[3rd, 1st, 2nd]` ist (nicht nur „nicht wie am Anfang")
- **`dataTransfer.setData(...)`-Aufruf hinzufügen:** Der Test-Code (in der JavaScript-Callback auf Zeile 213–221) muss ergänzt werden:
  ```javascript
  dt.setData('text/plain', targetId.toString());
  ```
  Dies ist erforderlich, damit Firefox die Simulation korrekt verarbeitet.

## Konfiguration

Keine Konfigurationsänderungen nötig. Die Verschiebe-Logik ist vollständig im Service `PlaylistEntryReorderService` implementiert.

## Offene Fragen

1. **Präzision der Drop-Position:** Soll Drag & Drop die genaue Position relativ zum Ziel-Eintrag berücksichtigen (z. B. obere Hälfte = vor dem Eintrag, untere Hälfte = nach dem Eintrag)? Oder genügt es, den Eintrag immer auf die `SortOrder` des Ziel-Eintrags zu setzen und alle dazwischen zu verschieben?
   - **Aktuelle Annahme:** Der gezogene Eintrag erhält die `SortOrder` des Ziel-Eintrags, und alle anderen Einträge verschieben sich entsprechend (analog zum Muster von „An Anfang").

2. **Atomarität:** Sollen die Verschiebe-Operationen atomar sein (d. h. alle-oder-nichts)? Die `MoveEntryToBeginningAsync`-Methode verwendet eine explizite Transaktion; dies wird für Konsistenz empfohlen.
   - **Aktuelle Annahme:** Ja, atomar (wie bei `MoveEntryToBeginningAsync`).

3. **Browser-Kompatibilität:** Reicht ein einfacher `dataTransfer.setData('text/plain', ...)` aus, oder braucht es einen spezifischeren MIME-Type?
   - **Aktuelle Annahme:** `text/plain` oder `text/html` genügt; Firefox erfordert nur einen Aufruf, egal welcher MIME-Type.
