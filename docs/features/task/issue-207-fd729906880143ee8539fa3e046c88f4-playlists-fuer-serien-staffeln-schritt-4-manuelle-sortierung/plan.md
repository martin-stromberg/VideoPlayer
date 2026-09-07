# Umsetzungsplan: Drag & Drop für Playlist-Einträge (Korrektur)

## Übersicht

Diese Umsetzung behebt die fehlerhafte Drag-&-Drop-Funktionalität zum Umsortieren von Einträgen in Manual-Mode-Playlists. Das Problem liegt darin, dass der gezogene Eintrag nicht an der exakten Position abgelegt wird, sondern aufgrund von kollidierenden `SortOrder`-Werten an einer durch den `AddedAt`-Tiebreaker zufällig bestimmten Position landet. Die Korrektur betrifft die UI-Komponente `PlaylistEntriesList.razor` (Methoden `OnEntryDragStart` und `OnEntryDropAsync`) und den E2E-Test `E2E_DragDropReorder_ManualMode_PersistsSortOrder`. Die Verschiebe-Logik wird nach dem bewährten Muster von `MoveEntryToBeginningAsync` implementiert — alle Einträge zwischen Quell- und Zielposition werden konsistent um ±1 verschoben statt kollidierenden `SortOrder`-Werte zu setzen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Drag-&-Drop-Verschiebungslogik | Nutze Muster von `MoveEntryToBeginningAsync`: Alle Einträge im betroffenen Bereich verschieben sich konsistent um ±1; gezogener Eintrag erhält `SortOrder` des Ziel-Eintrags | Verhindert `SortOrder`-Kollisionen und garantiert exakte, vorhersehbare Positionen. Atomar durch explizite Transaktion. Bewährtes Muster im Service bereits vorhanden. |
| Firefox-Kompatibilität für Drag & Drop | Füge `dataTransfer.setData("text/plain", entryId.ToString())` in `OnEntryDragStart` ein | Firefox erfordert mindestens einen `setData`-Aufruf, um Drag-&-Drop zu starten; Chrome/Safari funktionieren auch ohne, aber standardkonformes Verhalten gebietet die Ergänzung. |
| Reorder-Service-Methode für beliebige Positionen | Neue Methode `MoveEntryBetweenAsync(playlist, entryId, targetSortOrder)` in `PlaylistEntryReorderService` | Analoge Implementierung zu `MoveEntryToBeginningAsync`, aber für beliebige Zielposition statt nur Position 0. Atomare Mehrfach-Reordering mit Transaktionsschutz. |

## Programmabläufe

### Drag-&-Drop-Reorder

1. Benutzer startet Drag über einen Eintrag in der `PlaylistEntriesList.razor`-Komponente
2. `OnEntryDragStart` speichert den zu verschiebenden Eintrag in Variable `draggedEntry` und ruft `dataTransfer.setData("text/plain", entry.Id.ToString())` auf (Firefox-Kompatibilität)
3. Benutzer bewegt Mauszeiger über Ziel-Eintrag
4. `ondragover` wird für jeden Ziel-Eintrag ausgelöst; `@ondragover:preventDefault="IsManualMode"` erlaubt Drop nur im Manual-Mode
5. Benutzer lässt Maustaste über Ziel-Eintrag los
6. `OnEntryDropAsync` wird mit Ziel-Eintrag als Parameter aufgerufen
7. Methode ermittelt die `SortOrder` des Ziel-Eintrags
8. Methode ruft `PlaylistEntryReorderService.MoveEntryBetweenAsync` auf, die:
   - Alle Einträge zwischen Quell- und Zielposition ermittelt
   - Diese Einträge konsistent um ±1 verschiebt (abhängig von Verschiebungsrichtung)
   - Gezogenen Eintrag auf `SortOrder` des ursprünglichen Ziel-Eintrags setzt
   - Die gesamte Operation atomar ausführt (Transaktion)
   - Persistierung in Datenbank durchführt
9. UI aktualisiert sich entsprechend
10. `OnEntryDragEnd` wird aufgerufen, um `draggedEntry` auf null zu setzen und Drag-Zustand zu bereinigen

Beteiligte Klassen/Komponenten: `PlaylistEntriesList.razor`, `PlaylistEntryReorderService`, `PlaylistService`, `PlaylistEntry` (Datenmodell)

## Neue Klassen

Keine — alle erforderlichen Klassen und Services existieren bereits.

## Änderungen an bestehenden Klassen

### `PlaylistEntriesList.razor` (Razor-Komponente)

- **Geänderte Methoden:**
  - `OnEntryDragStart` (Zeile 320–326) — Füge `dataTransfer.setData("text/plain", entry.Id.ToString())` ein für Firefox-Kompatibilität. Parameter: `entry` (PlaylistEntryDto). Rückgabewert: void. Effekt: setzt `draggedEntry = entry` und registriert Drag-Daten.
  - `OnEntryDropAsync` (Zeile 328–349) — Ersetze fehlerhafte Logik durch Aufruf von `PlaylistEntryReorderService.MoveEntryBetweenAsync`. Parameter: `targetEntry` (PlaylistEntryDto). Rückgabewert: Task. Neue Logik: 1) Lade Ziel-`SortOrder`, 2) Rufe `PlaylistEntryReorderService.MoveEntryBetweenAsync(Playlist, draggedEntry.Id, targetSortOrder, cancellationToken)` auf, 3) Aktualisiere lokale `entries`-Liste mit neuem Sortorder.

### `PlaylistEntryReorderService` (Service-Klasse)

- **Neue Methoden:**
  - `MoveEntryBetweenAsync(playlist, entryId, targetSortOrder, cancellationToken)` — Verschiebt Eintrag zu beliebiger Position mit konsistenter Anpassung aller betroffenen Einträge. Parameter: `Playlist playlist`, `long entryId`, `long targetSortOrder`, `CancellationToken cancellationToken`. Rückgabewert: `Task`. Logik: 1) Lade aktuellen Eintrag und Ziel-SortOrder, 2) Lade alle Einträge im Bereich (Min(aktuell, ziel) bis Max(aktuell, ziel)), 3) Verschiebe alle Einträge im Bereich um ±1 (abhängig von Richtung), 4) Setze gezogenen Eintrag auf targetSortOrder, 5) Speichere alle Changes atomar (Transaktion).

### `PlaylistService` (Service-Wrapper)

- **Neue Methoden:**
  - `MoveEntryBetweenAsync(playlistId, userId, entryId, targetSortOrder, ...)` — Wrapper um `PlaylistEntryReorderService.MoveEntryBetweenAsync`. Parameter: `long playlistId`, `string userId`, `long entryId`, `long targetSortOrder`, `CancellationToken cancellationToken`. Rückgabewert: `Task`. Logik: 1) Lade Playlist, 2) Prüfe Besitzerschaft und Manual-Mode, 3) Rufe `PlaylistEntryReorderService.MoveEntryBetweenAsync` auf.

## Datenbankmigrationen

Keine — die erforderlichen Spalten (`PlaylistEntry.SortOrder` und `PlaylistEntry.AddedAt`) existieren bereits.

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Bestehende Validierungen (`PlaylistNotInManualSortModeException` in `EnsureManualSortMode`, Bounds-Checks in `ReorderEntryAsync`) genügen.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Drag-&-Drop-Verhalten:** Die Änderung von fehlerhafter zu korrekter Logik behebt das unerwartet zufällige Positionieren. Endanwender werden vorher unerklärliche Reorderings nicht mehr erleben. Kein Risiko, da die neue Logik dem erwarteten Verhalten entspricht.
- **Transaktionsschutz:** `MoveEntryBetweenAsync` nutzt explizite Transaktion wie `MoveEntryToBeginningAsync`. Dies erhöht Datenkonsistenz, könnte aber in Szenarien mit sehr hohem Durchsatz zu Locking-Konflikten führen. Da Playlist-Reorderings durch einzelne Benutzer aus einer UI erfolgen, ist dies kein praktisches Risiko.
- **Keine Auswirkungen auf andere Features:** Die Änderungen sind lokal auf Drag-&-Drop-Reorder beschränkt. Quick-Action-Buttons („An Anfang", „An Ende") nutzen bereits korrekte Methoden und sind nicht betroffen.

## Umsetzungsreihenfolge

1. **Neue Methode `MoveEntryBetweenAsync` in `PlaylistEntryReorderService` implementieren**
   - Voraussetzungen: `PlaylistEntryReorderService` existiert bereits; Zugriff auf DbContext und Transaktionen verfügbar.
   - Beschreibung: Implementiere Methode mit folgender Logik: (a) Lade Quell-Eintrag und ermittle aktuelle `SortOrder`; (b) Lade alle Einträge zwischen Min(aktuelle SortOrder, Ziel-SortOrder) und Max(aktuelle SortOrder, Ziel-SortOrder); (c) Verschiebe alle betroffenen Einträge um ±1 (Richtung abhängig davon, ob nach vorne oder hinten verschoben wird); (d) Setze Quell-Eintrag auf Ziel-SortOrder; (e) Speichere alle Changes in expliziter Transaktion.

2. **Wrapper-Methode `MoveEntryBetweenAsync` in `PlaylistService` hinzufügen**
   - Voraussetzungen: `MoveEntryBetweenAsync` existiert in `PlaylistEntryReorderService`; `PlaylistService` mit bestehenden Wrappern verfügbar.
   - Beschreibung: Implementiere Wrapper analog zu `MoveEntryToBeginningAsync`: (a) Lade Playlist mit Berechtigung und Mode-Check; (b) Rufe `PlaylistEntryReorderService.MoveEntryBetweenAsync` auf; (c) Gebe Task zurück.

3. **Korrektur von `OnEntryDragStart` in `PlaylistEntriesList.razor`**
   - Voraussetzungen: `PlaylistEntriesList.razor` existiert; Razor-Syntax und Interop verfügbar.
   - Beschreibung: Ergänze `OnEntryDragStart`-Methode um `dataTransfer.setData("text/plain", entry.Id.ToString())` aufgerufen über Interop oder direkten JavaScript-Handler. Dies kann erfolgen über: (a) Inlining eines JavaScript-Handlers mit `@ondragstart="..."` und Zugriff auf `DataTransfer`, oder (b) C#-basierter Aufruf von JavaScript über Interop mit Übergabe der Entry-ID. Bestehendes Muster aus Komponente nutzen; keine neuen Abhängigkeiten.

4. **Korrektur von `OnEntryDropAsync` in `PlaylistEntriesList.razor`**
   - Voraussetzungen: `MoveEntryBetweenAsync` existiert in `PlaylistEntryReorderService` und `PlaylistService`; Komponenten-Zugriff auf Playlist und Service-Client verfügbar.
   - Beschreibung: Ersetze fehlerhafte Logik (Zeile 348) durch Aufruf von `PlaylistService.MoveEntryBetweenAsync`. Neue Logik: (a) Lade `targetSortOrder` von `targetEntry`; (b) Rufe `PlaylistClient.MoveEntryBetweenAsync(PlaylistId, draggedEntry.Id, targetSortOrder, cancellationToken)` auf; (c) Aktualisiere lokale `entries`-Liste mit neuem Sortorder. Nutze bestehendes Fehlerbehandlungsmuster der Komponente.

5. **E2E-Test `E2E_DragDropReorder_ManualMode_PersistsSortOrder` anpassen und erweitern**
   - Voraussetzungen: Test existiert bereits; PlaylistsE2ETestBase und Hilfsmethoden verfügbar; Playwright für Browser-Automation konfiguriert.
   - Beschreibung: (a) Ergänze JavaScript-Simulation (Zeile 213–221) um `dt.setData('text/plain', targetId.toString())` nach `dt.dataTransfer.setData()` oder ähnliches; (b) Ersetze schwache Assertion `Assert.NotEqual(initialOrder, afterDrop)` durch explizite Positionsverifikation: z.B. „Ziehe Eintrag an Position 2 auf Position 0, verifiziere exakte Reihenfolge [3rd, 1st, 2nd] nach Drop, persistiere und verifiziere erneut nach Reload"; (c) Nutze bestehende Test-Infrastruktur (SetupManualPlaylistWithThreeEntriesAsync, EvalOnSelectorAsync) ohne neue Abhängigkeiten.

## Tests

### Neue Tests

Keine neuen Testklassen oder Hilfsmethoden erforderlich — die bestehende E2E-Test-Infrastruktur genügt nach Anpassung des vorhandenen Tests.

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| — | — | — |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_DragDropReorder_ManualMode_PersistsSortOrder` (in `VideoWebPlayer.Tests/PlaylistReorderE2ETests.cs`, Zeile 203–234) | Schwache Assertion muss durch explizite Positionsverifizierung ersetzt werden; JavaScript-Simulation muss `dataTransfer.setData()` aufrufen. |

### E2E-Tests (primärer Funktionsnachweis)

Die Drag-&-Drop-Reorder ist eine reine Benutzerinteraktion über die Browser-UI; kein Unit- oder Integrations-Test kann den tatsächlichen Benutzerfluss (Mausziehen, Drop-Position, Persistierung) abdecken. E2E-Tests sind daher notwendig und primär.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Happy Path: Drag Eintrag an Position 2 auf Position 0, verifiziere exakte Reihenfolge [3rd, 1st, 2nd] nach Drop, persistiere und verifiziere nach Reload | `VideoWebPlayer.Tests/PlaylistReorderE2ETests.cs` / `E2E_DragDropReorder_ManualMode_PersistsSortOrder` (erweitert) | Eintrag wird an exakter Zielposition abgelegt; Reihenfolge wird persistiert; kein Gleichstand in `SortOrder` durch `AddedAt`-Tiebreaker | Drag-&-Drop ist reine Browser-Benutzerinteraktion; nur E2E kann Mausziehen, Drop-Erkennung und UI-Aktualisierung vollständig prüfen. Unit-Tests können Verschiebe-Logik des Service testen, aber nicht die Komponenten-Integration und Browser-Events. |
| Optional | Edge Case: Drag mehrere Positionen nach unten / oben, verifiziere konsistente Verschiebung aller betroffenen Einträge | `VideoWebPlayer.Tests/PlaylistReorderE2ETests.cs` / neue Testmethode z.B. `E2E_DragDropReorder_MultiplePositions_MaintainsConsistentOrder` | Mehrfache Verschiebungen zeigen, dass Transaktionssicherheit und konsistente Anpassung funktionieren | Verifiziert, dass `MoveEntryBetweenAsync` korrekt arbeitet über mehrere Szenarien hinweg; entspricht Benutzererwartung bei wiederholtem Umsortieren. |

Bestehende E2E-Tests, die **nicht** angepasst werden müssen:
- `E2E_SortModeChange_ByReleaseDateToManual_ActivatesManualModeControls` — Testet Mode-Wechsel, nicht Drag-&-Drop; davon unberührt.
- `E2E_SortModeChange_ManualToByReleaseDate_ShowsWarningModal_AndConfirmingDeactivatesControls` — Testet Mode-Wechsel; davon unberührt.
- `E2E_SortModeChange_Manual_ToByReleaseDate_Cancel_KeepsManualMode` — Testet Mode-Wechsel; davon unberührt.
- `E2E_QuickActionButton_MoveToEnd_MovesEntryToLastPosition` — Testet Quick-Action-Button, nutzt bereits korrekte Service-Methode; davon unberührt.
- `E2E_QuickActionButton_MoveToBeginning_MovesEntryToFirstPosition` — Testet Quick-Action-Button, nutzt bereits korrekte Service-Methode; davon unberührt.

## Offene Punkte

Keine — alle offenen Punkte aus der Anforderung wurden beantwortet und sind im Plan eingearbeitet:

1. **Präzision der Drop-Position** — geklärt: Der gezogene Eintrag erhält die `SortOrder` des Ziel-Eintrags; alle anderen Einträge zwischen Quell- und Zielposition verschieben sich um ±1 (analog zu `MoveEntryToBeginningAsync`).
2. **Atomarität** — geklärt: Ja, atomar durch explizite Transaktion (wie bei `MoveEntryToBeginningAsync`).
3. **Browser-Kompatibilität** — geklärt: `dataTransfer.setData("text/plain", ...)` genügt; Firefox erfordert nur einen `setData`-Aufruf, unabhängig vom MIME-Type.
