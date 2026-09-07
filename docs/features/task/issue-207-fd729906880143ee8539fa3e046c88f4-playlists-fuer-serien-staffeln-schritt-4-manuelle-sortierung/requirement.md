# Technische Anforderungsübersetzung: Schritt 4 – Manuelle Sortierung von Playlists

**Aufgaben-ID:** fd729906-8801-43ee-8539-fa3e046c88f4  
**Branch:** task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-4-manuelle-sortierung  
**Schritt:** 4 von n  
**Erstellt:** 2026-09-07

---

## Fachliche Zusammenfassung

Schritt 4 erweitert das Playlist-Feature um Benutzergesteuerte, persistent gespeicherte manuelle Sortierung. Playlists mit `SortMode == Manual` ermöglichen es Benutzern, die Reihenfolge ihrer Einträge durch Drag & Drop oder Schnellaktionen („An Anfang", „An Ende") zu ändern. Die geänderte Reihenfolge wird dauerhaft in der Datenbank gespeichert und bestimmt sowohl die Anzeige als auch die Wiedergabereihfolge. Neu hinzugefügte Einträge werden im manuellen Modus am Ende der Liste eingefügt; beim Umschalten zwischen Auto- und Manualmodus wird die chronologische Reihenfolge beibehalten bzw. wiederhergestellt, wobei der Benutzer beim Wechsel zu automatischer Sortierung vor Datenverlust gewarnt wird. Das Reordering ist auf den Playlist-Besitzer beschränkt.

---

## Betroffene Klassen und Komponenten

### Erweiterungen bestehender Datenmodellklassen

- **`PlaylistEntry`** (Erweiterung)
  - Neue Eigenschaft: `SortOrder` (long?, optional)
    - Eindeutige manuelle Sortierreihenfolge je Playlist im Manual-Mode
    - `null` für Einträge in Playlists mit `SortMode == ByReleaseDate`
    - Bei Manual-Mode: aufsteigende positive Werte, lückenlos (0, 1, 2, ...) oder dünn besetzt
    - Wird bei der Datenbankbereinigung (Defragmentierung) optional verdichtet

### Logikklassen / Services (Neue oder Erweiterte)

- **`IPlaylistService`** (Erweiterung)
  - `ReorderPlaylistEntryAsync(playlistId, userId, entryId, newSortOrder)` 
    - Ändert die `SortOrder` eines Eintrags im Manual-Mode
    - Gibt ggf. `SortOrder`-Werte benachbarter Einträge an (für Batch-Updates bei Drag & Drop)
    - HTTP 403 wenn nicht Besitzer, HTTP 400 wenn nicht Manual-Mode, HTTP 404 wenn Eintrag nicht existiert
  
  - `BatchReorderPlaylistEntriesAsync(playlistId, userId, reorderOperations)`
    - Nimmt eine Liste von (EntryId, NewSortOrder) Tupeln entgegen
    - Führt alle Änderungen atomar durch
    - Gibt HTTP 409 zurück, wenn `SortOrder` Konflikte entstehen (Duplikate innerhalb der Batch)
  
  - `GetPlaylistEntriesPagedAsync(playlistId, sortMode?, pageNumber, pageSize)`
    - Erweitert: Sortierung nach `SortOrder` (aufsteigend) statt `AddedAt` wenn `SortMode == Manual`
    - Bestehende Sortier-Fallback-Kette (ReleaseDate → ParentId → SequenceNumber → AddedAt) bleibt für `ByReleaseDate` bestehen
  
  - `AddMediaToPlaylistAsync(playlistId, userId, mediaType, mediaId)`
    - Erweitert: Neue Einträge erhalten bei `SortMode == Manual` automatisch `SortOrder = Max(existingOrders) + 1`
    - Bei `SortMode == ByReleaseDate` bleibt `SortOrder = null`
  
  - `ChangeSortModeAsync(playlistId, userId, newSortMode, confirmLossOfManualOrder?)`
    - Neuer Service-Endpoint (oder Erweiterung von `UpdatePlaylistAsync`)
    - Beim Wechsel `Manual → ByReleaseDate`: Falls `confirmLossOfManualOrder == false`, wird HTTP 409 zurückgegeben mit Signal `IsLossOfDataConfirmationRequired == true`
    - Beim Wechsel `ByReleaseDate → Manual`: `SortOrder` wird auf Basis der aktuellen Sortierreihenfolge neu initialisiert
    - Befüllt `SortOrder` entsprechend vor/nach dem Wechsel

- **`PlaylistSortingService`** (Neue oder Erweiterung)
  - `SortPlaylistEntriesAsync(playlistId, sortMode)`
    - Liefert sortierte Einträge je nach `SortMode`
    - Manual: Nach `SortOrder` aufsteigend
    - ByReleaseDate: Nach bestehender Sortier-Fallback-Kette
  
  - `CalculateNextSortOrderAsync(playlistId, referenceEntry?)`
    - Liefert einen Wert für die nächste `SortOrder` beim Hinzufügen
    - Kann eine Zwischenwert-Berechnung für Drag-Drop-Einfügungen durchführen (z. B. zwischen zwei Einträgen)

- **`PlaylistValidationService`** (Erweiterung)
  - `ValidateSortOrderAsync(playlistId, newSortOrder)` (optional)
    - Prüft, ob die neue `SortOrder` in den gültigen Bereich fällt und keine Duplikate entstehen

### UI-Komponenten (Blazor)

- **`PlaylistDetail.razor`** (Erweiterung)
  - Erkennung des Sortiermodus und Anzeige entsprechender Kontrollen
  - Für `SortMode == Manual`:
    - Aktivierung von Drag-&-Drop-Handling (bereits existierende Virtualize-Komponente erweitern)
    - Anzeige von Schnellaktions-Buttons („An Anfang", „An Ende") pro Eintrag
    - Echtzeit-Reorder-API-Aufrufe bei Drop / Button-Click
    - Optionale Reorder-Animation oder visuelles Feedback
  
  - Für `SortMode == ByReleaseDate`:
    - Sperren der Reorder-Kontrollen (read-only Ansicht behalten)
  
  - Modale Dialogbox bei Sortiermodus-Wechsel:
    - Wird angezeigt, wenn `ChangeSortModeAsync` mit `confirmLossOfManualOrder == false` antwortet
    - Text-Warnung: „Die manuelle Reihenfolge wird verloren gehen. Möchten Sie fortfahren?"
    - Bestätigungs-Button triggert `ChangeSortModeAsync(..., confirmLossOfManualOrder = true)`

### API-Endpoints (ASP.NET Core / PlaylistsController)

- **Neue Endpoints:**
  - `PUT /api/playlists/{id}/entries/{entryId}/order`
    - Request Body: `{ "newSortOrder": <long> }`
    - Ruft `PlaylistService.ReorderPlaylistEntryAsync()` auf
    - Responses: 200 OK (Eintrag reordered), 403 Forbidden (nicht Besitzer), 400 Bad Request (nicht Manual-Mode), 404 Not Found
  
  - `POST /api/playlists/{id}/entries/batch-reorder`
    - Request Body: `{ "reorderOperations": [ { "entryId": <long>, "newSortOrder": <long> }, ... ] }`
    - Ruft `PlaylistService.BatchReorderPlaylistEntriesAsync()` auf
    - Responses: 200 OK (alle reordered), 409 Conflict (SortOrder-Duplikate), 403 Forbidden, 400 Bad Request, 404 Not Found
  
  - `PATCH /api/playlists/{id}/sort-mode`
    - Request Body: `{ "newSortMode": "Manual|ByReleaseDate", "confirmLossOfManualOrder": bool }`
    - Ruft `PlaylistService.ChangeSortModeAsync()` auf
    - Responses: 200 OK (Modus geändert), 409 Conflict (Bestätigung erforderlich; Response trägt `{ "isLossOfDataConfirmationRequired": true }`)

- **Modifizierte Endpoints:**
  - `GET /api/playlists/{id}/entries/paged` (bereits vorhanden in Schritt 3)
    - Sortierung wird je nach `Playlist.SortMode` durchgeführt

### Tests

- **Unit-Tests (PlaylistServiceTests):**
  - `ReorderPlaylistEntry_Manual_SuccessfullyReorders` — Erfolgreiches Reorder-Szenario
  - `ReorderPlaylistEntry_NotOwner_ReturnsUnauthorized` — Ownership-Check
  - `ReorderPlaylistEntry_NotManualMode_ReturnsBadRequest` — Mode-Validierung
  - `BatchReorderPlaylistEntries_DuplicateSortOrder_ReturnsConflict` — Duplikat-Erkennung in Batch
  - `AddMediaToPlaylistAsync_ManualMode_AppendsAtEnd` — Neue Einträge landen am Ende
  - `AddMediaToPlaylistAsync_ByReleaseDateMode_SortOrderIsNull` — SortOrder-Handling bei Automatic
  - `ChangeSortModeAsync_ManualToByReleaseDate_WithoutConfirmation_ReturnsConflict` — Datenverlust-Warnung
  - `ChangeSortModeAsync_ManualToByReleaseDate_WithConfirmation_ClearsSortOrder` — Wechsel mit Bestätigung
  - `ChangeSortModeAsync_ByReleaseDateToManual_PopulatesSortOrder` — Wechsel von Auto zu Manual initialisiert SortOrder

- **Integrationstests (PlaylistDetailE2ETests, PlaylistEntriesE2ETests):**
  - Komplette Drag-&-Drop-Workflows (über API simuliert)
  - Sortiermodus-Wechsel mit Modal-Dialog (Blazor interop)
  - Batch-Reorder mit vielen Einträgen
  - Infinity-List-Render mit Manual-Sort bei vielen Einträgen (Virtualisierung-Test)
  - Persistenz über mehrere Requests hinweg

---

## Implementierungsansatz

### 1. Datenbankmigrationen

- **Neue Migration:** `AddSortOrderToPlaylistEntries`
  - Fügt Spalte `SortOrder` (long?, nullable) zu `PlaylistEntries` hinzu
  - Indizes:
    - Index auf `(PlaylistId, SortOrder)` für effizientes Manual-Mode-Sorting
  - Initialisierung:
    - Setze `SortOrder = null` für alle bestehenden Einträge (Backward-Compatibility)
    - Nach Migration: Schreibe EF Core-Migrationslogik, die für jede Playlist im Manual-Mode `SortOrder` aufsteigend initialisiert (0, 1, 2, ...)

- **PlaylistEntryConfiguration (EF Core):**
  ```csharp
  builder.Property(e => e.SortOrder)
      .IsRequired(false)
      .HasDefaultValue(null);
  
  builder.HasIndex(e => new { e.PlaylistId, e.SortOrder })
      .HasName("IX_PlaylistEntries_PlaylistId_SortOrder");
  ```

### 2. Service-Layer — Sortierungs-Logik

- **Erweiterung `PlaylistService.GetPlaylistEntriesPagedAsync()`:**
  - Prüfe `Playlist.SortMode`
  - Falls `Manual`: Sortiere nach `SortOrder` aufsteigend, dann nach `AddedAt` (Fallback für null-Werte)
  - Falls `ByReleaseDate`: Nutze bestehende Fallback-Kette (unverändert)
  - Paginierung erfolgt nach Sortierung

- **Neue Methode `PlaylistService.ReorderPlaylistEntryAsync()`:**
  - Ownership-Check: Prüfe `Playlist.UserId == currentUserId`
  - Mode-Check: Prüfe `Playlist.SortMode == Manual`
  - Existenz-Check: Prüfe, ob Entry existiert
  - Update: Setze `PlaylistEntry.SortOrder = newSortOrder`
  - Rückgabe: DTO mit aktueller Position und (optional) der aktuellen Reihenfolge benachbarter Einträge für Client-seitige Animations-Optimierung

- **Neue Methode `PlaylistService.BatchReorderPlaylistEntriesAsync()`:**
  - Ownership-Check für alle Entries
  - Mode-Check
  - Duplikat-Detektion: Wenn `newSortOrder` in der Batch zweimal vorkommt → HTTP 409
  - Atomare Ausführung via `DbContext.SaveChangesAsync()` in einer Transaktion
  - Rückgabe: Liste der aktualisierten Entries

- **Erweiterung `PlaylistService.AddMediaToPlaylistAsync()`:**
  - Nach Top-Level-Eintrag und Cascade-Kindern hinzufügen:
    - Falls `Playlist.SortMode == Manual`:
      - Berechne `maxSortOrder = Entries.Where(e => e.PlaylistId == id).Max(e => e.SortOrder) ?? 0`
      - Setze neue Entries: `SortOrder = maxSortOrder + 1, maxSortOrder + 2, ...` (für Cascade-Einträge)
    - Falls `Playlist.SortMode == ByReleaseDate`:
      - Setze `SortOrder = null`

- **Neue Methode `PlaylistService.ChangeSortModeAsync()`:**
  - Ownership-Check
  - Fallunterscheidung nach Ziel-Mode:
    
    **Zielmode: Manual** (von `ByReleaseDate`)
    - Initialisiere `SortOrder` für alle Entries basierend auf aktueller Sortierreihenfolge
    - Sortiere die Entries nach Fallback-Kette (wie in `GetPlaylistEntriesPagedAsync`)
    - Weise aufsteigende `SortOrder`-Werte zu: 0, 1, 2, ...
    - Speichere Änderung: `Playlist.SortMode = Manual`
    - Rückgabe: 200 OK
    
    **Zielmode: ByReleaseDate** (von `Manual`)
    - Prüfe `confirmLossOfManualOrder` Parameter:
      - Wenn `false`: Werfe `PlaylistSortModeChangePendingException` → HTTP 409 mit Response Body `{ "isLossOfDataConfirmationRequired": true }`
      - Wenn `true`: Setze `SortOrder = null` für alle Entries
    - Speichere Änderung: `Playlist.SortMode = ByReleaseDate`
    - Rückgabe: 200 OK

- **Neue Methode `PlaylistSortingService.CalculateNextSortOrderAsync()`:**
  - Berechnet einen geeigneten `SortOrder`-Wert für neue Einträge oder Einfügungen
  - Standard (Anhängen): `Max(existingSortOrders) + 1`
  - Optional (Einfügung zwischen zwei Einträgen): `(SortOrderBefore + SortOrderAfter) / 2` (Fließkomma-Zwischenwert als `long` gerundet)

### 3. API und Authorization

- **Alle Sortiermodus-Wechsel-Operationen:**
  - Prüfe Ownership: `Playlist.UserId == currentUserId`
  - Prüfe Modus-Kompatibilität (wenn Zielmode Manual, prüfe ob Playlist diese Mode unterstützt)

- **Reorder-Operationen (ReorderPlaylistEntry, BatchReorderPlaylistEntries):**
  - Prüfe Ownership (wie oben)
  - Prüfe, dass der aktuellen Modus der Playlist tatsächlich `Manual` ist
  - Verbiete ungültige `SortOrder`-Werte (z. B. negative Zahlen) mit HTTP 400

### 4. UI-Layer (Blazor Components)

- **PlaylistDetail.razor — Drag & Drop Handling:**
  - Erkenne `Playlist.SortMode`
  - Nur für Manual-Mode: Aktiviere Drag-&-Drop auf Einträgen via JavaScript interop
    - HTML-Attribut: `draggable="true"` auf Eintrag-Komponenten
    - JavaScript-Event-Handler: `ondragstart`, `ondragover`, `ondrop`, `ondragend`
    - Drag-Operation bestimmt Ziel-`SortOrder` basierend auf Zielposition innerhalb der Liste
    - `drop`-Event triggert `await PlaylistsController.ReorderPlaylistEntryAsync()` oder `.BatchReorderPlaylistEntriesAsync()`
  
  - **Schnellaktions-Buttons:**
    - Für jeden Eintrag (nur Manual-Mode): Zwei Buttons:
      - „An Anfang" → `ReorderPlaylistEntryAsync(entryId, newSortOrder: 0)`, dann Refresh
      - „An Ende" → `ReorderPlaylistEntryAsync(entryId, newSortOrder: Max + 1)`, dann Refresh
    - Buttons sind disabled, wenn nicht Besitzer

  - **Infinity-List / Virtualisierung:**
    - Bestehende `Virtualize<DtoPlaylistEntry>`-Komponente nutzen
    - `ItemsProviderAsync` liefert Einträge sortiert nach Manual-/Auto-Mode (via `GetPlaylistEntriesPagedAsync`)
    - Keine Änderungen an der Virtualisierungs-Logik selbst erforderlich; nur Sortierung passt sich an

- **PlaylistDetail.razor — Modal für Sortiermodus-Wechsel:**
  - In `UpdateSortMode()`-Handler:
    - Rufe `ChangeSortModeAsync(..., confirmLossOfManualOrder: false)` auf
    - Falls Response HTTP 409 und `isLossOfDataConfirmationRequired == true`:
      - Zeige Modal: „Ihre manuelle Reihenfolge wird gelöscht und kann nicht wiederhergestellt werden. Möchten Sie fortfahren?"
      - Buttons: „Ja, ändern" → Rufe erneut `ChangeSortModeAsync(..., confirmLossOfManualOrder: true)` auf
      - Button: „Abbrechen" → Schließe Modal, keine Änderung
    - Falls Response HTTP 200: Refresh Komponente, Modal schließen

### 5. Abhängigkeiten und Hooks

- **Bestehende Mechaniken:**
  - Ownership-Check (wie in Schritt 2)
  - Zugriffsprüfung auf Medieninhalte (wie in Schritt 3)
  - Infinity-List / Virtualisierung (wie in Schritt 3)

- **Neue Anforderungen:**
  - `ChangeSortModeAsync` interagiert mit bestehender Sortier-Fallback-Kette (muss beim Wechsel zu Manual die aktuelle Reihenfolge als Ausgangspunkt nehmen)
  - Batch-Reorder muss transaktional sein

### 6. Performance-Überlegungen

- **SortOrder Index:**
  - Composite Index `(PlaylistId, SortOrder)` ermöglicht effiziente Sortierung im Manual-Mode
  - Abfrage: `PlaylistEntries.Where(e => e.PlaylistId == id).OrderBy(e => e.SortOrder).ThenBy(e => e.AddedAt)`

- **Drag-&-Drop mit großen Listen:**
  - JavaScript interop lagert Drag-&-Drop-Event-Handling aus dem Blazor-Rendering aus
  - Optional: Debounce API-Aufrufe oder Batch-Updates sammeln und in einer Transaktion speichern

- **Migrationen:**
  - Nach Migration mit vielen Einträgen: Optional Datenbankdefragmentierung (`SortOrder` komprimieren)

---

## Konfiguration

| Parameter | Typ | Standard | Beschreibung |
|-----------|-----|---------|--------------|
| `Playlists:EnableManualSorting` | `bool` | `true` | Aktiviert/Deaktiviert manuelle Sortierung Feature (optional) |
| `Playlists:ManualSortingBatchSize` | `int` | `100` | Maximale Einträge in einer Batch-Reorder-Operation |
| `Playlists:AllowSortModeChange` | `bool` | `true` | Aktiviert/Deaktiviert das Umschalten zwischen Sortiermodi (optional) |

Diese Parameter werden in `PlaylistSettings` gelesen (falls Konfiguration erforderlich; Minimal-Konfiguration: keine speziellen Parameter nötig).

---

## Offene Fragen

1. **SortOrder-Werte-Raum:** 
   - Sollen `SortOrder`-Werte im Bereich [0, n) dicht besetzt sein, oder können Lücken entstehen (z. B. [0, 2, 5, 7])?
   - Annahme: Lücken sind erlaubt (einfacher zu implementieren); Defragmentierung ist optional.

2. **Drag-&-Drop-Zwischenwerte:**
   - Sollen Drag-&-Drop-Einfügungen zwischen zwei Einträgen mit halben Werten arbeiten (z. B. 2.5 als `long`), oder soll nach jeder Einfügung neu durchnummeriert werden?
   - Annahme: Bei häufigen Umordnungen erfolgt eine Defragmentierung; Zwischen-Berechnungen sind optional.

3. **Modal-Bestätigung Wording:**
   - Die Anforderung nennt „Anwender wird vorab darauf hingewiesen, dass seine manuelle Reihenfolge dadurch verloren geht." — Soll dies in Deutsch oder Englisch erfolgen? Soll ein spezifischer Wording vorgegeben werden?
   - Annahme: Deutsch (wie übrige UI); Wording siehe Komponenten-Beschreibung oben.

4. **Performance bei sehr großen Listen:**
   - Gibt es eine fachliche Obergrenze für die Anzahl Einträge pro Playlist im Manual-Mode?
   - Annahme: Keine feste Obergrenze; Infinity-List mit Virtualisierung handhabt Performanz.

5. **Rückwärtskompatibilität:**
   - Für bestehende Playlists im Manual-Mode ohne `SortOrder`: Sollen diese automatisch initialisiert werden (z. B. Migration)? Oder wird die `AddedAt`-Sortierung beibehalten, bis eine Reorder-Operation erfolgt?
   - Annahme: Migration initialisiert bestehende Playlists im Manual-Mode mit aufsteigenden `SortOrder`-Werten.

6. **Cascade-Einträge im Manual-Mode:**
   - Wenn ein Benutzer eine Serie zu einer Manual-Mode-Playlist hinzufügt und die Cascade erzeugt mehrere Einträge (Staffeln, Episoden): Erhalten alle eine aufsteigende `SortOrder`, oder wird die Hierarchie beibehalten (z. B. alle Episoden einer Staffel vor der nächsten Staffel)?
   - Annahme: Alle erhalten aufsteigende `SortOrder` (flache Struktur); Hierarchie-Information ist in `ParentMediaType`/`ParentMediaId` gespeichert.

7. **Zu-Anfang-/Zu-Ende-Button Platzierung:**
   - Wo sollen diese Buttons angezeigt werden? Inline neben dem Eintrag, oder in einem Kontext-Menü?
   - Annahme: Inline-Buttons neben dem Eintrag (wie Drag-Handle und Delete-Button in Schritt 2).

8. **Batch-Reorder Transaktionalität:**
   - Soll eine Batch-Operation atomar sein (all-or-nothing), oder sollen erfolgreiche Einträge gespeichert werden, wenn einzelne fehlschlagen?
   - Annahme: Atomare Ausführung (all-or-nothing) für Datenkonsistenz.

9. **SortMode-Eigenschaft auf Playlist:**
   - Wird davon ausgegangen, dass `Playlist.SortMode` bereits in Schritt 1 existiert? (Ja, laut Kontext: „Der Sortiermodus (automatisch/manuell) wird bereits in Schritt 1 an der Playlist gepflegt")
   - Bestätigung: Ja, Eigenschaft existiert bereits mit Werten `Automatic` und `Manual`.
