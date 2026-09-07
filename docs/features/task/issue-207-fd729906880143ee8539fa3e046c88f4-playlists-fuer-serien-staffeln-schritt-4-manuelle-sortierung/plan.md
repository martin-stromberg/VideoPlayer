# Umsetzungsplan: Schritt 4 – Manuelle Sortierung von Playlists

## Übersicht

Schritt 4 erweitert das Playlist-Feature um benutzergesteuerte, dauerhaft gespeicherte manuelle Sortierung. Playlist-Einträge erhalten eine neue Eigenschaft `SortOrder`, die bei `SortMode == Manual` die Reihenfolge bestimmt. Benutzer können Einträge durch Drag & Drop oder Schnellaktionen („An Anfang", „An Ende") umordnen. Der Sortiermodus kann zwischen `Manual` und `ByReleaseDate` gewechselt werden; ein Wechsel zu automatischer Sortierung erfordert Bestätigung, da manuelle Sortierreihenfolge verloren geht. Service-Methoden validieren Ownership und Mode-Kompatibilität; Batch-Operationen erfolgen transaktional.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **SortOrder Feldtyp** | `long?` (nullable) | Ermöglicht Backward-Compatibility: bestehende ByReleaseDate-Einträge haben `null`, Manual-Einträge aufsteigende Werte. Genug Wertbereich für große Listen. |
| **SortOrder Werte-Raum** | Lücken erlaubt | Vereinfacht Implementierung: Drag & Drop, Add und Batch-Operationen erzeugen möglicherweise Lücken (z. B. [0, 2, 5, 8]). Defragmentierung bleibt optional für zukünftige Optimierung. |
| **Batch-Reorder Transaktionalität** | All-or-Nothing-Semantik | Verhindert inkonsistente Zustände bei gleichzeitigen Writes; wird durch EF Core Transaktion (`DbContext.SaveChangesAsync()` in `using (var transaction = ...`) implementiert. |
| **Modal-Dialog Wording** | Deutsch, explizite Warnung | Konsistenz mit Rest der UI; Wording: „Die manuelle Reihenfolge wird gelöscht und kann nicht wiederhergestellt werden. Möchten Sie fortfahren?" |
| **Drag & Drop Event-Handling** | JavaScript interop, nicht Blazor-Events | Blazor-Rendering bei Infinity-List würde zu Performance-Problemen führen; JS-Handling lagert Events aus dem Render-Zyklus aus. |
| **Exception-Handling für Mode-Wechsel** | `InvalidOperationException` mit spezifischer Message | HTTP 409 Conflict wird vom Controller abgefangen und an Client übermittelt; keine neue Exception-Klasse erforderlich (folgt bestehenden Pattern). |
| **API-Design für Reorder-Operationen** | Separate Endpoints statt erweiterte `UpdatePlaylistAsync` | Klare Verantwortung: `UpdatePlaylist` für Playlist-Properties, `/order` und `/batch-reorder` für Entry-Repositionierung. Bessere Fehlerbehandlung und Testbarkeit. |
| **Neue DTOs als separate Klassen** | `DtoReorderPlaylistEntryRequest`, `DtoBatchReorderPlaylistEntriesRequest`, `DtoChangeSortModeRequest`, `DtoChangeSortModeConflictResponse` | Klare Trennung zwischen Request- und Response-Modellen; Batch-Request nutzt nested `DtoReorderOperation` Klasse statt Tuple. |

---

## Programmabläufe

### Ablauf 1: Einzelnes Reordern eines Eintrags (Drag & Drop oder Quick-Action)

1. Benutzer zieht einen Eintrag an neue Position oder klickt Quick-Action-Button
2. Blazor-Komponente `PlaylistDetail.razor` ermittelt neue `SortOrder` basierend auf Zielposition
3. Komponente ruft `PUT /api/playlists/{id}/entries/{entryId}/order` mit `{ "newSortOrder": <long> }` auf
4. Controller `PlaylistsControllerReorderPlaylistEntryAsync()` wird aufgerufen
5. Service-Methode `ReorderPlaylistEntryAsync()` wird ausgeführt:
   - Ownership-Check: `Playlist.UserId == currentUserId` → 403 Forbidden falls falsch
   - Mode-Check: `Playlist.SortMode == Manual` → 400 Bad Request falls falsch
   - Existenz-Check: Entry existiert in Playlist → 404 Not Found falls nicht
   - Update: `PlaylistEntry.SortOrder = newSortOrder`
   - Speichern: `await _dbContext.SaveChangesAsync()`
6. Controller gibt 200 OK mit aktualisiertem `DtoPlaylistEntry` zurück
7. Blazor-Komponente updated lokale Liste und re-rendert Eintrag (optional mit Animation)

**Beteiligte Klassen/Komponenten:** `PlaylistsController`, `IPlaylistService.ReorderPlaylistEntryAsync`, `PlaylistService`, `PlaylistDetail.razor`, `DbContext`, `DtoPlaylistEntry`

---

### Ablauf 2: Batch-Reorder (mehrere Einträge in einer Transaktion)

1. Client sammelt mehrere Drag-&-Drop-Operationen oder sendet Batch-API-Request
2. POST `/api/playlists/{id}/entries/batch-reorder` wird aufgerufen mit Request Body:
   ```
   {
     "reorderOperations": [
       { "entryId": 101, "newSortOrder": 5 },
       { "entryId": 102, "newSortOrder": 3 },
       ...
     ]
   }
   ```
3. Controller `BatchReorderPlaylistEntriesAsync()` wird aufgerufen
4. Service-Methode `PlaylistService.BatchReorderPlaylistEntriesAsync()` wird ausgeführt:
   - Ownership-Check für alle Entries
   - Mode-Check für Playlist
   - Duplikat-Detektion: Falls `newSortOrder` in der Batch zweimal vorkommt → 409 Conflict zurückgeben
   - Transaktion starten: `using (var transaction = _dbContext.Database.BeginTransaction())`
   - Für jeden Eintrag in Batch: `playlistEntry.SortOrder = newSortOrder` setzen
   - `await _dbContext.SaveChangesAsync()`
   - Transaktion committen
5. Bei Exception in Schritt 4-5: Transaktion rollback, 409 Conflict mit Fehlerdetails
6. Controller gibt 200 OK mit Array aktualisierter `DtoPlaylistEntry` zurück

**Beteiligte Klassen/Komponenten:** `PlaylistsController`, `IPlaylistService.BatchReorderPlaylistEntriesAsync`, `PlaylistService`, `DbContext`, `DbContextTransaction`, `DtoBatchReorderPlaylistEntriesRequest`, `DtoPlaylistEntry`

---

### Ablauf 3: Sortiermodus-Wechsel von ByReleaseDate zu Manual

1. Benutzer öffnet Playlist-Settings und wechselt SortMode zu Manual
2. Blazor-Komponente ruft `PATCH /api/playlists/{id}/sort-mode` auf mit:
   ```
   { "newSortMode": "Manual", "confirmLossOfManualOrder": false }
   ```
   (Standardmäßig mit `false`, weil kein bestehender manueller Modus vorhanden ist)
3. Controller `ChangeSortModeAsync()` wird aufgerufen
4. Service-Methode `PlaylistService.ChangeSortModeAsync()` wird ausgeführt:
   - Ownership-Check
   - Fallunterscheidung: Zielmode == Manual (von ByReleaseDate)
   - Aktuelle Entries laden (in ByReleaseDate-Reihenfolge sortiert)
   - Für jeden Eintrag `SortOrder` initialisieren: 0, 1, 2, ...
   - `Playlist.SortMode = PlaylistSortMode.Manual`
   - `await _dbContext.SaveChangesAsync()`
5. Controller gibt 200 OK mit aktualisierter `DtoPlaylist` zurück
6. Blazor-Komponente updated Playlist-Object, re-rendert (Drag-&-Drop-Kontrollen werden jetzt aktiv)

**Beteiligte Klassen/Komponenten:** `PlaylistsController`, `IPlaylistService.ChangeSortModeAsync`, `PlaylistService`, `Playlist`, `PlaylistEntry`, `PlaylistSortMode`

---

### Ablauf 4: Sortiermodus-Wechsel von Manual zu ByReleaseDate (mit Bestätigung)

1. Benutzer wechselt in Settings SortMode zu ByReleaseDate
2. Blazor-Komponente ruft `PATCH /api/playlists/{id}/sort-mode` mit `confirmLossOfManualOrder: false` auf
3. Service-Methode `PlaylistService.ChangeSortModeAsync()` wird ausgeführt:
   - Ownership-Check
   - Fallunterscheidung: Zielmode == ByReleaseDate (von Manual)
   - Prüfe `confirmLossOfManualOrder` Parameter:
     - Falls `false`: Werfe `InvalidOperationException("Manual sort order will be lost")` → Controller fängt ab, gibt 409 Conflict mit Response `{ "isLossOfDataConfirmationRequired": true }` zurück
4. Blazor-Komponente zeigt Modal: „Warnung: Die manuelle Reihenfolge wird verloren gehen. Möchten Sie fortfahren?"
5. Benutzer klickt „Ja, ändern"
6. Blazor-Komponente ruft erneut `PATCH /api/playlists/{id}/sort-mode` mit `confirmLossOfManualOrder: true` auf
7. Service-Methode wird erneut ausgeführt:
   - Prüfe `confirmLossOfManualOrder == true`
   - Für alle Entries: `PlaylistEntry.SortOrder = null`
   - `Playlist.SortMode = PlaylistSortMode.ByReleaseDate`
   - `await _dbContext.SaveChangesAsync()`
8. Controller gibt 200 OK zurück
9. Blazor-Komponente schließt Modal, updated Playlist, Drag-&-Drop-Kontrollen werden deaktiviert

**Beteiligte Klassen/Komponenten:** `PlaylistsController`, `IPlaylistService.ChangeSortModeAsync`, `PlaylistService`, `PlaylistDetail.razor`, Modal-Dialog-Komponente, `InvalidOperationException`

---

### Ablauf 5: Neue Einträge hinzufügen bei Manual-Mode

1. Benutzer klickt „Medium hinzufügen" in Playlist mit `SortMode == Manual`
2. Blazor-Komponente ruft `POST /api/playlists/{id}/entries` mit Media-Daten auf
3. Controller `AddMediaToPlaylistAsync()` wird aufgerufen
4. Service-Methode `PlaylistService.AddMediaToPlaylistAsync()` wird ausgeführt:
   - Ownership-Check
   - Fallunterscheidung nach `Playlist.SortMode`:
     - Falls `Manual`:
       - Berechne `maxSortOrder = await _dbContext.PlaylistEntries.Where(e => e.PlaylistId == playlistId).MaxAsync(e => e.SortOrder) ?? 0`
       - Neue Entry: `SortOrder = maxSortOrder + 1` (Anhängen am Ende)
       - Falls Cascade-Kindern (z. B. Episoden einer Serie): Alle erhalten `SortOrder = maxSortOrder + 1, maxSortOrder + 2, ...`
     - Falls `ByReleaseDate`:
       - `SortOrder = null` setzen
   - Entries speichern, verwaiste Kindern löschen (Cascade-Handling wie in Schritt 2)
5. Controller gibt 200 OK mit `DtoPlaylistAddResult` zurück

**Beteiligte Klassen/Komponenten:** `PlaylistsController`, `IPlaylistService.AddMediaToPlaylistAsync`, `PlaylistService`, `PlaylistEntry`, `Playlist.SortMode`

---

### Ablauf 6: Paginierte Einträge abrufen mit korrekter Sortierung

1. Blazor-Komponente ruft `GET /api/playlists/{id}/entries/paged?pageNumber=1&pageSize=20` auf
2. Controller `GetPlaylistEntriesPaged()` wird aufgerufen
3. Service-Methode `PlaylistService.GetPlaylistEntriesPagedAsync()` wird ausgeführt:
   - Playlist laden
   - Fallunterscheidung nach `Playlist.SortMode`:
     - Falls `ByReleaseDate`: Sortiere nach `SortPlaylistEntriesByReleaseDateAsync()` (ReleaseDate → ParentId → SequenceNumber → AddedAt)
     - Falls `Manual`: Sortiere nach `SortOrder` aufsteigend, dann nach `AddedAt` als Fallback für `null`-Werte
   - Paginiere nach Sortierung: `skip = (pageNumber - 1) * pageSize; take = pageSize`
   - Konvertiere zu DTOs (mit Bild-IDs, Zugriffsprüfungen)
4. Controller gibt 200 OK mit `DtoPlaylistEntriesPagedResult` zurück

**Beteiligte Klassen/Komponenten:** `PlaylistsController`, `IPlaylistService.GetPlaylistEntriesPagedAsync`, `PlaylistService`, `PlaylistEntry.SortOrder`, `Playlist.SortMode`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `DtoReorderPlaylistEntryRequest` | DTO | Request-Modell für PUT `/api/playlists/{id}/entries/{entryId}/order`; enthält `NewSortOrder: long` |
| `DtoBatchReorderPlaylistEntriesRequest` | DTO | Request-Modell für POST `/api/playlists/{id}/entries/batch-reorder`; enthält `ReorderOperations: List<DtoReorderOperation>` |
| `DtoReorderOperation` | DTO (nested) | Einzelne Reorder-Operation innerhalb Batch; enthält `EntryId: long` und `NewSortOrder: long` |
| `DtoChangeSortModeRequest` | DTO | Request-Modell für PATCH `/api/playlists/{id}/sort-mode`; enthält `NewSortMode: string` und `ConfirmLossOfManualOrder: bool?` |
| `DtoChangeSortModeConflictResponse` | DTO | Response bei HTTP 409 Conflict; enthält `IsLossOfDataConfirmationRequired: bool` |

---

## Änderungen an bestehenden Klassen

### `PlaylistEntry` (Datenmodellklasse)

- **Neue Eigenschaften:** 
  - `SortOrder` (`long?`, optional) — Manuelle Sortierreihenfolge im Manual-Mode; `null` für ByReleaseDate-Playlists. Eindeutig je Playlist im Manual-Mode (aber Lücken erlaubt).

### `PlaylistEntryConfiguration` (EF Core Konfigurationsklasse)

- **Neue Index:** Composite Index auf `(PlaylistId, SortOrder)` mit Name `IX_PlaylistEntries_PlaylistId_SortOrder` für effiziente Abfragen im Manual-Mode.

### `DtoPlaylistEntry` (DTO-Klasse)

- **Neue Eigenschaften:** 
  - `SortOrder` (`long?`) — Wird vom Server gefüllt, wenn Playlist `SortMode == Manual` ist; `null` sonst. Im Client-Modell vorhanden, aber nur bei Manual-Mode mit echtem Wert.

### `IPlaylistService` (Service-Interface)

- **Neue Methoden:**
  - `ReorderPlaylistEntryAsync(long playlistId, string userId, long entryId, long newSortOrder, CancellationToken cancellationToken)` → `Task` — Ändert `SortOrder` eines Eintrags. Throws `PlaylistAccessDeniedException` (403), `ArgumentException` für ungültige Eingaben (400), `KeyNotFoundException` (404). Kann auch `InvalidOperationException` für Nicht-Manual-Mode-Fehler werfen → wird zu 400.
  - `BatchReorderPlaylistEntriesAsync(long playlistId, string userId, List<(long EntryId, long NewSortOrder)> reorderOperations, CancellationToken cancellationToken)` → `Task<IEnumerable<DtoPlaylistEntry>>` — Führt mehrere Reorder-Operationen atomar durch. Throws wie oben; zusätzlich `InvalidOperationException` für Duplikate in Batch → wird zu 409 Conflict.
  - `ChangeSortModeAsync(long playlistId, string userId, string newSortMode, bool? confirmLossOfManualOrder, CancellationToken cancellationToken)` → `Task<DtoPlaylist>` — Wechselt Sortiermodus. Throws `PlaylistAccessDeniedException` (403); `InvalidOperationException` mit Message "Manual sort order will be lost" beim Wechsel Manual → ByReleaseDate ohne Bestätigung → wird zu 409 Conflict mit Response `{ "isLossOfDataConfirmationRequired": true }`. Normale Erfolgs-Rückgabe ist 200 OK.

### `PlaylistService` (Service-Implementierungsklasse)

- **Geänderte Methoden:**
  - `GetPlaylistEntriesPagedAsync()` — Erweiterung der Sortierlogik: Fallunterscheidung nach `Playlist.SortMode`; wenn `Manual`, sortiere nach `SortOrder` aufsteigend (mit `ThenBy(e => e.AddedAt)` als Fallback für `null`-Werte), statt standardmäßig nach `AddedAt`.
  - `AddMediaToPlaylistAsync()` — Erweiterung der SortOrder-Zuweisung: Falls `Playlist.SortMode == Manual`, berechne `maxSortOrder = Entries.Where(...).Max(e => e.SortOrder) ?? 0` und setze neue Entries mit `SortOrder = maxSortOrder + 1, maxSortOrder + 2, ...`. Falls `Playlist.SortMode == ByReleaseDate`, setze `SortOrder = null`.

- **Neue private Hilfsmethoden:** (optional, abhängig von Implementierung)
  - `GetMaxSortOrderAsync(long playlistId)` — Berechnet höchste `SortOrder` einer Playlist; Returns `long` oder `null`.

- **Neue öffentliche Methoden:**
  - `ReorderPlaylistEntryAsync(long playlistId, string userId, long entryId, long newSortOrder, CancellationToken cancellationToken)` — Implementierung des Interface. Prüfungen: Ownership, Mode, Existenz. Update und Speicherung.
  - `BatchReorderPlaylistEntriesAsync(long playlistId, string userId, List<(long EntryId, long NewSortOrder)> reorderOperations, CancellationToken cancellationToken)` — Implementierung des Interface. Ownership und Mode für alle Entries, Duplikat-Detektion, transaktionales Speichern. Rückgabe: Array aktualisierter DTOs.
  - `ChangeSortModeAsync(long playlistId, string userId, string newSortMode, bool? confirmLossOfManualOrder, CancellationToken cancellationToken)` — Implementierung des Interface. Ownership-Check, Fallunterscheidung nach Zielmode, SortOrder-Population oder -Clearing, Speichern.

### `PlaylistsController` (API-Controller)

- **Neue Endpoints:**
  - `[HttpPut("/{id}/entries/{entryId}/order")]` — Ruft `ReorderPlaylistEntryAsync` auf. Request Body: `DtoReorderPlaylistEntryRequest`. Response: 200 OK mit `DtoPlaylistEntry`, oder 400/403/404 je nach Exception. Exception-Handling: `InvalidOperationException` → 400 Bad Request.
  - `[HttpPost("/{id}/entries/batch-reorder")]` — Ruft `BatchReorderPlaylistEntriesAsync` auf. Request Body: `DtoBatchReorderPlaylistEntriesRequest`. Response: 200 OK mit Array `DtoPlaylistEntry[]`, oder 409 Conflict für Duplikate, oder 400/403/404. Exception-Handling: `InvalidOperationException` → 409 Conflict.
  - `[HttpPatch("/{id}/sort-mode")]` — Ruft `ChangeSortModeAsync` auf. Request Body: `DtoChangeSortModeRequest`. Response: 200 OK mit `DtoPlaylist`, oder 409 Conflict mit `DtoChangeSortModeConflictResponse` für fehlende Bestätigung, oder 400/403/404. Exception-Handling: `InvalidOperationException` → Prüfe Message; falls "Manual sort order will be lost" → 409 Conflict; sonst → 400 Bad Request.

- **Geänderte Endpoints:**
  - `UpdatePlaylist()` (PUT `/{id}`) — Kann weiterhin `SortMode` aktualisieren (via `UpdatePlaylistAsync`), aber für Mode-Wechsel ist jetzt der neue `/sort-mode` Endpoint zu bevorzugen (cleaner). Falls `UpdatePlaylistAsync` zu Mode-Wechsel führt, keine extra Logik erforderlich — wird durch Service-Implementierung in `ChangeSortModeAsync` übernommen, wenn dort aufgerufen. **Vereinbarung:** `UpdatePlaylist` bleibt unverändert; Mode-Wechsel-Logik läuft über `ChangeSortModeAsync` (neuer Endpoint).

### `PlaylistDetail.razor` (Blazor-Komponente)

- **Neue Features:**
  - Drag-&-Drop-Handling: `@ondragstart`, `@ondragover`, `@ondrop`, `@ondragend` JavaScript interop auf Playlist-Einträgen (nur wenn `SortMode == Manual`).
  - Schnellaktions-Buttons: „An Anfang" (Move to Order 0) und „An Ende" (Move to Max + 1) pro Eintrag im Manual-Mode.
  - Modal-Dialog für Sortiermodus-Wechsel: Erscheint bei HTTP 409 Response von `/sort-mode` Endpoint mit `isLossOfDataConfirmationRequired: true`.
  - Reorder-API-Aufrufe: Bei Drag-&-Drop-Drop oder Button-Click `PUT /api/playlists/{id}/entries/{entryId}/order` oder `POST /api/playlists/{id}/entries/batch-reorder` aufrufen.
  - Optional: Visuelles Feedback (Loading-State, Animation) bei Reorder-Operationen.

---

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddSortOrderToPlaylistEntries` | `PlaylistEntries` Tabelle, neue Spalte `SortOrder` (`bigint`, nullable) | Fügt Spalte `SortOrder (long?, nullable)` hinzu. Initialisiert alle bestehenden Einträge mit `SortOrder = null`. Fügt Composite-Index `IX_PlaylistEntries_PlaylistId_SortOrder` auf Spalten `(PlaylistId, SortOrder)` hinzu. |

**Migration-Script Details:**
- Name: `AddSortOrderToPlaylistEntries` (Pattern: `Add{Feature}To{Entity}`)
- SQL: `ALTER TABLE PlaylistEntries ADD SortOrder bigint NULL;`
- Index: `CREATE INDEX IX_PlaylistEntries_PlaylistId_SortOrder ON PlaylistEntries(PlaylistId, SortOrder);`
- Keine Datenverlust-Risiken (neue nullable Spalte, alle bestehenden Werte sind `null`).

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ReorderPlaylistEntryRequest.NewSortOrder` | Muss `>= 0` sein (nicht-negative Long) | 400 Bad Request: "newSortOrder must be non-negative" |
| `ReorderPlaylistEntryRequest.NewSortOrder` | Darf nicht duplikat mit anderen Einträgen in gleicher Playlist sein (wird in Service geprüft, nicht auf DTO-Ebene) | 409 Conflict: "Duplicate SortOrder detected" (nur bei Batch) |
| `BatchReorderPlaylistEntriesRequest.ReorderOperations` | Liste darf nicht leer sein | 400 Bad Request: "reorderOperations list must not be empty" |
| `BatchReorderPlaylistEntriesRequest.ReorderOperations` | Kein EntryId darf zweimal vorkommen in der Batch | 400 Bad Request: "Duplicate EntryId in batch" |
| `BatchReorderPlaylistEntriesRequest.ReorderOperations` | Alle NewSortOrder-Werte müssen eindeutig sein (innerhalb Batch) | 409 Conflict: "Duplicate SortOrder in batch operations" |
| `ChangeSortModeRequest.NewSortMode` | Muss gültiger `PlaylistSortMode` Wert sein (z. B. "Manual", "ByReleaseDate") | 400 Bad Request: "Invalid SortMode value" |
| `PlaylistEntry.SortOrder` | Bei `Playlist.SortMode == Manual`: Wert muss `>= 0` sein; bei `Playlist.SortMode == ByReleaseDate`: muss `null` sein | N/A - Wird durch Service-Logik sichergestellt, nicht durch DB-Constraint |
| **Service-Level Validierung:** Ownership | `Playlist.UserId` muss gleich `currentUserId` sein für alle Reorder-Operationen | 403 Forbidden: "You are not the owner of this playlist" |
| **Service-Level Validierung:** Mode-Check | `Playlist.SortMode` muss `Manual` sein für Reorder-Operationen | 400 Bad Request: "Playlist is not in Manual sort mode" |
| **Service-Level Validierung:** Entry-Existenz | Alle Entries in Reorder-Operationen müssen zu Playlist gehören | 404 Not Found: "Playlist entry not found" |

---

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Playlists:EnableManualSorting` | `bool` (optional) | `true` | Aktiviert/Deaktiviert das manuelle Sortierungs-Feature. Falls `false`, sind Reorder-Endpoints nicht verfügbar (optional für zukünftige Feature-Flags). |
| `Playlists:ManualSortingBatchSize` | `int` (optional) | `100` | Maximale Anzahl Operationen in einer Batch-Reorder-Anfrage. Falls überschritten, 400 Bad Request. |
| `Playlists:AllowSortModeChange` | `bool` (optional) | `true` | Aktiviert/Deaktiviert das Umschalten zwischen Sortiermodi (optional für zukünftige Beschränkungen). |

**Anmerkung:** Diese Konfigurationseinträge sind optional und werden **nicht** implementiert in Schritt 4. Sie können für zukünftige Optimierungen oder Feature-Flagging hinzugefügt werden. Minimal-Konfiguration: keine neuen appsettings-Einträge erforderlich.

---

## Seiteneffekte und Risiken

- **`UpdatePlaylistAsync()` und Mode-Wechsel:** Falls `UpdatePlaylistAsync` bereits Sortiermodus-Wechsel unterstützt, könnten dort vorhandene Tests fehlschlagen, wenn sie nicht-transaktionales Verhalten erwarten. Prüfung erforderlich, ob bestehende Tests angepasst werden müssen.

- **`GetPlaylistEntriesPagedAsync()` Sortierlogik:** Bestehendes Verhalten für ByReleaseDate-Mode bleibt unverändert. Jedoch müssen Tests überprüft werden, die nach `AddedAt` sortierte Einträge erwarten — möglicherweise brechen Tests für "neue Einträge zuerst"-Szenarios im Manual-Mode.

- **`AddMediaToPlaylistAsync()` Performance:** Beim Hinzufügen von Medien mit Cascade-Kindern (z. B. Serie mit Staffeln) im Manual-Mode wird `Max(SortOrder)` abgefragt. Bei Playlists mit Tausenden Einträgen könnte dies Performance-Auswirkungen haben. Optimierung: Optional Index auf `PlaylistId` allein oder Caching der maximen SortOrder pro Playlist.

- **Infinity-List / Virtualisierung:** Bestehende `Virtualize<DtoPlaylistEntry>`-Komponente in `PlaylistDetail.razor` wird weiterhin verwendet; keine Breaking Changes. Jedoch müssen Tests überprüft werden, die auf Render-Reihenfolge prüfen.

- **JavaScript interop für Drag & Drop:** Neue Abhängigkeit auf JavaScript-Funktionen für Event-Handling. Falls kein JS-Framework vorhanden ist (z. B. nur Blazor interop), muss entsprechender JS-Code hinzugefügt werden.

- **EF Core Transaktionen:** `DbContext.Database.BeginTransaction()` wird in `BatchReorderPlaylistEntriesAsync()` verwendet. Prüfung erforderlich, ob Datenbank-Konfiguration Transaktionen unterstützt (normalerweise ja, aber Isolation Levels könnten relevant sein).

- **Exception-Handling in Controller:** `InvalidOperationException` wird in Service geworfen und im Controller abgefangen. Prüfung erforderlich, ob andere Exception-Typen in Service unbeabsichtigt nach oben propagieren (z. B. `DbUpdateException` bei Datenbankfehlern).

- **Bestehende "UpdatePlaylist" Tests:** Falls Tests `SortMode` aktualisieren, könnten sie nun andere Logik durchlaufen (falls `ChangeSortModeAsync` zusätzliche Prüfungen hat). Tests müssen überprüft und ggf. angepasst werden.

---

## Umsetzungsreihenfolge

1. **Überprüfung `PlaylistSortMode` Enum**
   - Voraussetzungen: Keine
   - Beschreibung: Verifiziere, dass `PlaylistSortMode` Enum in `VideoWebPlayer/Data/PlaylistSortMode.cs` existiert mit Werten `ByReleaseDate` und `Manual`. Falls nicht vorhanden, müsse diese zuerst angelegt werden. (Erwartete Status: bereits vorhanden aus Schritt 1.)

2. **`PlaylistEntry.SortOrder` Eigenschaft hinzufügen**
   - Voraussetzungen: `PlaylistSortMode` Enum existiert
   - Beschreibung: Füge zu `VideoWebPlayer/Data/PlaylistEntry.cs` neue Eigenschaft `public long? SortOrder { get; set; }` hinzu (nullable Long). Dokumentation: Zweck ist manuelle Sortierreihenfolge im Manual-Mode.

3. **`PlaylistEntryConfiguration` Index hinzufügen**
   - Voraussetzungen: `PlaylistEntry.SortOrder` Eigenschaft existiert
   - Beschreibung: Bearbeite `VideoWebPlayer/Data/Configurations/PlaylistEntryConfiguration.cs` und füge Composite-Index hinzu:
     ```csharp
     builder.HasIndex(e => new { e.PlaylistId, e.SortOrder })
         .HasName("IX_PlaylistEntries_PlaylistId_SortOrder");
     ```

4. **Datenbank-Migration erstellen: `AddSortOrderToPlaylistEntries`**
   - Voraussetzungen: `PlaylistEntry.SortOrder` und Index-Konfiguration existieren
   - Beschreibung: Führe `dotnet ef migrations add AddSortOrderToPlaylistEntries` aus. Prüfe generierte Migration:
     - Spalte `SortOrder (bigint, nullable)` wird hinzugefügt
     - Composite-Index `IX_PlaylistEntries_PlaylistId_SortOrder` wird erstellt
     - Alle bestehenden Einträge erhalten `SortOrder = null`
     Keine manuellen Änderungen an Migration erforderlich (EF Core sollte alles korrekt generieren).

5. **`IPlaylistService` Interface erweitern — 3 neue Methoden**
   - Voraussetzungen: `IPlaylistService` Interface existiert
   - Beschreibung: Füge folgende Methoden-Signaturen zu `VideoWebPlayer/Services/IPlaylistService.cs` hinzu:
     - `Task ReorderPlaylistEntryAsync(long playlistId, string userId, long entryId, long newSortOrder, CancellationToken cancellationToken)`
     - `Task<IEnumerable<DtoPlaylistEntry>> BatchReorderPlaylistEntriesAsync(long playlistId, string userId, List<(long EntryId, long NewSortOrder)> reorderOperations, CancellationToken cancellationToken)`
     - `Task<DtoPlaylist> ChangeSortModeAsync(long playlistId, string userId, string newSortMode, bool? confirmLossOfManualOrder, CancellationToken cancellationToken)`

6. **`DtoPlaylistEntry` DTO erweitern**
   - Voraussetzungen: `DtoPlaylistEntry` DTO existiert
   - Beschreibung: Füge zu `VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs` Eigenschaft hinzu:
     ```csharp
     public long? SortOrder { get; set; }
     ```

7. **Neue DTOs erstellen**
   - Voraussetzungen: Keine (neue Klassen)
   - Beschreibung: Erstelle folgende neue DTO-Klassen in `VideoWebPlayer.Client/Models/`:
     - `DtoReorderPlaylistEntryRequest` mit Eigenschaft `NewSortOrder: long`
     - `DtoReorderOperation` (nested in `DtoBatchReorderPlaylistEntriesRequest`) mit Eigenschaften `EntryId: long`, `NewSortOrder: long`
     - `DtoBatchReorderPlaylistEntriesRequest` mit Eigenschaft `ReorderOperations: List<DtoReorderOperation>`
     - `DtoChangeSortModeRequest` mit Eigenschaften `NewSortMode: string`, `ConfirmLossOfManualOrder: bool?`
     - `DtoChangeSortModeConflictResponse` mit Eigenschaft `IsLossOfDataConfirmationRequired: bool`

8. **`PlaylistService` Methode `GetPlaylistEntriesPagedAsync()` anpassen**
   - Voraussetzungen: `PlaylistEntry.SortOrder` existiert; Migration ist angewendet
   - Beschreibung: Bearbeite `VideoWebPlayer/Services/PlaylistService.cs` Methode `GetPlaylistEntriesPagedAsync()`:
     - Aktualisiere Sortierlogik (ca. Zeile 836-838):
       ```csharp
       var sortedEntries = playlist.SortMode == PlaylistSortMode.ByReleaseDate
           ? await SortPlaylistEntriesByReleaseDateAsync(validEntries, cancellationToken)
           : playlist.SortMode == PlaylistSortMode.Manual
               ? validEntries.OrderBy(e => e.SortOrder).ThenBy(e => e.AddedAt).ToList()
               : validEntries.OrderBy(e => e.AddedAt).ToList();
       ```
     - Fallback-Logik: Falls `SortOrder` null ist, sortiere nach `AddedAt`.

9. **`PlaylistService` Methode `AddMediaToPlaylistAsync()` anpassen**
   - Voraussetzungen: `PlaylistEntry.SortOrder` existiert; Migration ist angewendet
   - Beschreibung: Bearbeite `VideoWebPlayer/Services/PlaylistService.cs` Methode `AddMediaToPlaylistAsync()`:
     - Vor dem Speichern der neuen Entries prüfe `Playlist.SortMode`:
       ```csharp
       if (playlist.SortMode == PlaylistSortMode.Manual)
       {
           var maxSortOrder = await _dbContext.PlaylistEntries
               .Where(e => e.PlaylistId == playlistId)
               .MaxAsync(e => (long?)e.SortOrder, cancellationToken) ?? 0;
           
           // Für jeden neuen Eintrag (Entry und Cascade-Kinder):
           foreach (var entry in newEntriesToAdd)
           {
               entry.SortOrder = maxSortOrder++;
           }
       }
       else
       {
           // Für ByReleaseDate: SortOrder = null
           foreach (var entry in newEntriesToAdd)
           {
               entry.SortOrder = null;
           }
       }
       ```

10. **`PlaylistService` neue Methoden implementieren: `ReorderPlaylistEntryAsync()`**
    - Voraussetzungen: `IPlaylistService.ReorderPlaylistEntryAsync()` existiert; `GetOwnedPlaylistAsync()` Hilfsmethode existiert
    - Beschreibung: Implementiere in `VideoWebPlayer/Services/PlaylistService.cs`:
      ```csharp
      public async Task ReorderPlaylistEntryAsync(
          long playlistId, string userId, long entryId, long newSortOrder, 
          CancellationToken cancellationToken)
      {
          // 1. Ownership-Check
          var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);
          
          // 2. Mode-Check
          if (playlist.SortMode != PlaylistSortMode.Manual)
              throw new InvalidOperationException("Playlist is not in Manual sort mode");
          
          // 3. Entry-Existenz-Check
          var entry = await _dbContext.PlaylistEntries
              .FirstOrDefaultAsync(e => e.Id == entryId && e.PlaylistId == playlistId, cancellationToken);
          if (entry == null)
              throw new KeyNotFoundException("Playlist entry not found");
          
          // 4. Validierung newSortOrder >= 0
          if (newSortOrder < 0)
              throw new ArgumentException("newSortOrder must be non-negative");
          
          // 5. Update
          entry.SortOrder = newSortOrder;
          await _dbContext.SaveChangesAsync(cancellationToken);
      }
      ```

11. **`PlaylistService` neue Methoden implementieren: `BatchReorderPlaylistEntriesAsync()`**
    - Voraussetzungen: `IPlaylistService.BatchReorderPlaylistEntriesAsync()` existiert; `PlaylistEntry.SortOrder` existiert
    - Beschreibung: Implementiere in `VideoWebPlayer/Services/PlaylistService.cs`:
      ```csharp
      public async Task<IEnumerable<DtoPlaylistEntry>> BatchReorderPlaylistEntriesAsync(
          long playlistId, string userId, List<(long EntryId, long NewSortOrder)> reorderOperations, 
          CancellationToken cancellationToken)
      {
          // 1. Ownership-Check
          var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);
          
          // 2. Mode-Check
          if (playlist.SortMode != PlaylistSortMode.Manual)
              throw new InvalidOperationException("Playlist is not in Manual sort mode");
          
          // 3. Validierung: Keine leere Liste
          if (!reorderOperations.Any())
              throw new ArgumentException("reorderOperations list must not be empty");
          
          // 4. Validierung: Keine doppelten EntryIds
          var entryIds = reorderOperations.Select(op => op.EntryId).ToList();
          if (entryIds.Count != entryIds.Distinct().Count())
              throw new ArgumentException("Duplicate EntryId in batch");
          
          // 5. Validierung: Keine doppelten SortOrders in Batch
          var sortOrders = reorderOperations.Select(op => op.NewSortOrder).ToList();
          if (sortOrders.Count != sortOrders.Distinct().Count())
              throw new InvalidOperationException("Duplicate SortOrder in batch operations");
          
          // 6. Validierung: Alle Entries existieren und gehören zu Playlist
          var entries = await _dbContext.PlaylistEntries
              .Where(e => e.PlaylistId == playlistId && entryIds.Contains(e.Id))
              .ToListAsync(cancellationToken);
          if (entries.Count != reorderOperations.Count)
              throw new KeyNotFoundException("One or more playlist entries not found");
          
          // 7. Transaktionales Update
          using (var transaction = _dbContext.Database.BeginTransaction())
          {
              foreach (var operation in reorderOperations)
              {
                  var entry = entries.First(e => e.Id == operation.EntryId);
                  entry.SortOrder = operation.NewSortOrder;
              }
              await _dbContext.SaveChangesAsync(cancellationToken);
              await transaction.CommitAsync(cancellationToken);
          }
          
          // 8. Konvertiere aktualisierte Entries zu DTOs und rückgabe
          var updatedDtos = await BuildEntryDtosAsync(entries, cancellationToken);
          return updatedDtos;
      }
      ```

12. **`PlaylistService` neue Methoden implementieren: `ChangeSortModeAsync()`**
    - Voraussetzungen: `IPlaylistService.ChangeSortModeAsync()` existiert; `PlaylistSortMode` Enum existiert
    - Beschreibung: Implementiere in `VideoWebPlayer/Services/PlaylistService.cs`:
      ```csharp
      public async Task<DtoPlaylist> ChangeSortModeAsync(
          long playlistId, string userId, string newSortMode, 
          bool? confirmLossOfManualOrder, CancellationToken cancellationToken)
      {
          // 1. Ownership-Check
          var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);
          
          // 2. Parse newSortMode
          if (!Enum.TryParse<PlaylistSortMode>(newSortMode, out var targetMode))
              throw new ArgumentException("Invalid SortMode value");
          
          // 3. Fallunterscheidung nach Zielmode
          if (targetMode == PlaylistSortMode.Manual)
          {
              // Manual from ByReleaseDate: Initialisiere SortOrder
              var entries = await _dbContext.PlaylistEntries
                  .Where(e => e.PlaylistId == playlistId)
                  .OrderBy(e => e.AddedAt)  // Oder: sortiert wie in ByReleaseDate-Modus
                  .ToListAsync(cancellationToken);
              
              for (int i = 0; i < entries.Count; i++)
              {
                  entries[i].SortOrder = i;
              }
          }
          else if (targetMode == PlaylistSortMode.ByReleaseDate)
          {
              // ByReleaseDate from Manual: Bestätigung erforderlich
              if (confirmLossOfManualOrder != true)
                  throw new InvalidOperationException("Manual sort order will be lost");
              
              var entries = await _dbContext.PlaylistEntries
                  .Where(e => e.PlaylistId == playlistId)
                  .ToListAsync(cancellationToken);
              
              foreach (var entry in entries)
              {
                  entry.SortOrder = null;
              }
          }
          
          // 4. Update Playlist SortMode
          playlist.SortMode = targetMode;
          playlist.UpdatedAt = DateTime.UtcNow;
          
          // 5. Speichern
          await _dbContext.SaveChangesAsync(cancellationToken);
          
          // 6. Konvertiere zu DTO und rückgabe
          return MapToDto(playlist);
      }
      ```

13. **API-Endpoints hinzufügen: `PUT /api/playlists/{id}/entries/{entryId}/order`**
    - Voraussetzungen: `PlaylistsController` existiert; `IPlaylistService.ReorderPlaylistEntryAsync()` existiert; `DtoReorderPlaylistEntryRequest` DTO existiert
    - Beschreibung: Füge zu `VideoWebPlayer/Controllers/PlaylistsController.cs` neuen Endpoint-Handler hinzu:
      ```csharp
      [HttpPut("{id}/entries/{entryId}/order")]
      public async Task<IActionResult> ReorderPlaylistEntry(long id, long entryId, 
          [FromBody] DtoReorderPlaylistEntryRequest request, CancellationToken cancellationToken)
      {
          try
          {
              var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
              await _playlistService.ReorderPlaylistEntryAsync(id, userId, entryId, 
                  request.NewSortOrder, cancellationToken);
              return Ok();  // oder: aktualisiertes Entry zurückgeben
          }
          catch (PlaylistAccessDeniedException)
          {
              return Forbid();
          }
          catch (ArgumentException ex)
          {
              return BadRequest(new { error = ex.Message });
          }
          catch (KeyNotFoundException)
          {
              return NotFound();
          }
          catch (InvalidOperationException ex)
          {
              return BadRequest(new { error = ex.Message });
          }
      }
      ```

14. **API-Endpoints hinzufügen: `POST /api/playlists/{id}/entries/batch-reorder`**
    - Voraussetzungen: Wie oben; `DtoBatchReorderPlaylistEntriesRequest` DTO existiert
    - Beschreibung: Füge zu `VideoWebPlayer/Controllers/PlaylistsController.cs` neuen Endpoint-Handler hinzu:
      ```csharp
      [HttpPost("{id}/entries/batch-reorder")]
      public async Task<IActionResult> BatchReorderPlaylistEntries(long id, 
          [FromBody] DtoBatchReorderPlaylistEntriesRequest request, CancellationToken cancellationToken)
      {
          try
          {
              var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
              var reorderOps = request.ReorderOperations
                  .Select(op => (op.EntryId, op.NewSortOrder))
                  .ToList();
              var result = await _playlistService.BatchReorderPlaylistEntriesAsync(id, userId, 
                  reorderOps, cancellationToken);
              return Ok(result);
          }
          catch (PlaylistAccessDeniedException)
          {
              return Forbid();
          }
          catch (ArgumentException ex)
          {
              return BadRequest(new { error = ex.Message });
          }
          catch (KeyNotFoundException)
          {
              return NotFound();
          }
          catch (InvalidOperationException ex)
          {
              // Duplikat-Erkennung
              return Conflict(new { error = ex.Message });
          }
      }
      ```

15. **API-Endpoints hinzufügen: `PATCH /api/playlists/{id}/sort-mode`**
    - Voraussetzungen: Wie oben; `DtoChangeSortModeRequest` und `DtoChangeSortModeConflictResponse` DTOs existieren
    - Beschreibung: Füge zu `VideoWebPlayer/Controllers/PlaylistsController.cs` neuen Endpoint-Handler hinzu:
      ```csharp
      [HttpPatch("{id}/sort-mode")]
      public async Task<IActionResult> ChangeSortMode(long id, 
          [FromBody] DtoChangeSortModeRequest request, CancellationToken cancellationToken)
      {
          try
          {
              var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
              var result = await _playlistService.ChangeSortModeAsync(id, userId, 
                  request.NewSortMode, request.ConfirmLossOfManualOrder, cancellationToken);
              return Ok(result);
          }
          catch (PlaylistAccessDeniedException)
          {
              return Forbid();
          }
          catch (ArgumentException ex)
          {
              return BadRequest(new { error = ex.Message });
          }
          catch (KeyNotFoundException)
          {
              return NotFound();
          }
          catch (InvalidOperationException ex) when (ex.Message.Contains("Manual sort order will be lost"))
          {
              return Conflict(new DtoChangeSortModeConflictResponse 
              { 
                  IsLossOfDataConfirmationRequired = true 
              });
          }
          catch (InvalidOperationException ex)
          {
              return BadRequest(new { error = ex.Message });
          }
      }
      ```

16. **Blazor-Komponente `PlaylistDetail.razor` erweitern — Drag & Drop Handling**
    - Voraussetzungen: `PlaylistDetail.razor` existiert; JavaScript interop Infrastruktur vorhanden
    - Beschreibung: 
      - Füge JavaScript-Funktionen hinzu (z. B. in `wwwroot/js/playlist-drag-drop.js`):
        - `handleDragStart(event)`, `handleDragOver(event)`, `handleDrop(event, targetEntryId, newSortOrder)`, `handleDragEnd(event)`
      - In `PlaylistDetail.razor` Komponente:
        - Erkenne `@if (Playlist?.SortMode == "Manual")` für Aktivierung von Drag-&-Drop-Controls
        - Setze `draggable="true"` auf Eintrag-Komponenten
        - Rufe JavaScript interop auf für Event-Handler
        - Bei Drop: `await Http.PutAsJsonAsync($"/api/playlists/{PlaylistId}/entries/{entryId}/order", new { newSortOrder })`
        - Optional: Loading-State und Error-Handling

17. **Blazor-Komponente `PlaylistDetail.razor` erweitern — Quick-Action-Buttons**
    - Voraussetzungen: Drag-&-Drop in Schritt 16 abgeschlossen
    - Beschreibung:
      - Pro Eintrag (nur im Manual-Mode) zwei Buttons hinzufügen:
        - Button „An Anfang": `@onclick="async () => await MoveToBeginning(entryId)"`
          - Ruft `PUT /api/playlists/{id}/entries/{entryId}/order` mit `newSortOrder: 0` auf
          - Dann: Refresh Entry-Liste
        - Button „An Ende": `@onclick="async () => await MoveToEnd(entryId)"`
          - Berechnet `maxSortOrder = Entries.Max(e => e.SortOrder) ?? 0`
          - Ruft `PUT /api/playlists/{id}/entries/{entryId}/order` mit `newSortOrder: maxSortOrder + 1` auf
          - Dann: Refresh Entry-Liste
      - Buttons sind disabled, falls nicht Playlist-Besitzer (`!IsOwner`)

18. **Blazor-Komponente `PlaylistDetail.razor` erweitern — Modal für Sortiermodus-Wechsel**
    - Voraussetzungen: Komponente existiert; Modal-Komponente vorhanden (z. B. Bootstrap oder Custom-Modal)
    - Beschreibung:
      - Wenn Benutzer Sortiermodus ändert:
        - Falls Zielmode `Manual` (von ByReleaseDate): Keine Warnung, direkt `PATCH /api/playlists/{id}/sort-mode` mit `confirmLossOfManualOrder: false`
        - Falls Zielmode `ByReleaseDate` (von Manual): 
          - Rufe `PATCH /api/playlists/{id}/sort-mode` mit `confirmLossOfManualOrder: false` auf
          - Falls HTTP 409 Response mit `{ "isLossOfDataConfirmationRequired": true }`:
            - Zeige Modal: „Warnung: Ihre manuelle Reihenfolge wird gelöscht und kann nicht wiederhergestellt werden. Möchten Sie fortfahren?"
            - Button „Ja, ändern": Rufe `PATCH /api/playlists/{id}/sort-mode` mit `confirmLossOfManualOrder: true` auf
            - Button „Abbrechen": Schließe Modal, keine Änderung
          - Falls HTTP 200 OK: Refresh Komponente, Modal ist nicht erforderlich
      - Fehlerbehandlung: Falls API-Fehler → Error-Message anzeigen

19. **Unit-Tests schreiben (Service-Tests)**
    - Voraussetzungen: Test-Infrastruktur existiert (`PlaylistServiceTestBase`, Mock-DbContext, etc.)
    - Beschreibung: Siehe Abschnitt "Tests" unten.

20. **Integrationstests schreiben (E2E-Tests)**
    - Voraussetzungen: Test-Infrastruktur existiert (`PlaylistsE2ETestBase`, Test-Server, etc.)
    - Beschreibung: Siehe Abschnitt "Tests" unten.

21. **Datenbank-Migration anwenden**
    - Voraussetzungen: Migration `AddSortOrderToPlaylistEntries` existiert und kompiliert
    - Beschreibung: Führe `dotnet ef database update` aus (oder über Visual Studio Migration Manager). Prüfe, dass Spalte `SortOrder` in Tabelle `PlaylistEntries` angelegt wurde.

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ReorderPlaylistEntry_Manual_SuccessfullyReorders` | `PlaylistServiceTests_Reorder` (neu) | Erfolgreiches Reorder: Entry erhält neue SortOrder, wird in DB gespeichert |
| `ReorderPlaylistEntry_NotOwner_ThrowsAccessDenied` | `PlaylistServiceTests_Reorder` | Ownership-Check: Nicht-Besitzer kann Eintrag nicht umordnen (403) |
| `ReorderPlaylistEntry_NotManualMode_ThrowsInvalidOperation` | `PlaylistServiceTests_Reorder` | Mode-Check: Reorder im ByReleaseDate-Mode schlägt fehl (400) |
| `ReorderPlaylistEntry_EntryNotFound_ThrowsKeyNotFound` | `PlaylistServiceTests_Reorder` | Existenz-Check: Nicht-existierender Entry (404) |
| `ReorderPlaylistEntry_NegativeSortOrder_ThrowsArgumentException` | `PlaylistServiceTests_Reorder` | Validierung: Negative SortOrder-Werte werden abgelehnt (400) |
| `BatchReorderPlaylistEntries_SuccessfullyReorders_Multiple` | `PlaylistServiceTests_Reorder` | Erfolgreiches Batch-Reorder mit mehreren Entries |
| `BatchReorderPlaylistEntries_DuplicateSortOrder_ThrowsInvalidOperation` | `PlaylistServiceTests_Reorder` | Duplikat-Erkennung: Zwei Einträge mit gleicher SortOrder in Batch (409) |
| `BatchReorderPlaylistEntries_DuplicateEntryId_ThrowsArgumentException` | `PlaylistServiceTests_Reorder` | Duplikat-EntryId in Batch (400) |
| `BatchReorderPlaylistEntries_EmptyList_ThrowsArgumentException` | `PlaylistServiceTests_Reorder` | Leere Operationsliste (400) |
| `BatchReorderPlaylistEntries_TransactionalRollback_OnError` | `PlaylistServiceTests_Reorder` | Transaktion rollback bei Fehler: Keine partiellen Updates |
| `ChangeSortModeAsync_ManualToByReleaseDate_WithoutConfirmation_ThrowsInvalidOperation` | `PlaylistServiceTests_SortMode` (neu) | Datenverlust-Warnung: Manual → ByReleaseDate ohne Bestätigung (409) |
| `ChangeSortModeAsync_ManualToByReleaseDate_WithConfirmation_ClearsSortOrder` | `PlaylistServiceTests_SortMode` | Mit Bestätigung: SortOrder für alle Entries wird null |
| `ChangeSortModeAsync_ByReleaseDateToManual_PopulatesSortOrder` | `PlaylistServiceTests_SortMode` | ByReleaseDate → Manual: SortOrder wird auf Basis aktueller Reihenfolge initialisiert (0, 1, 2, ...) |
| `ChangeSortModeAsync_NotOwner_ThrowsAccessDenied` | `PlaylistServiceTests_SortMode` | Ownership-Check |
| `AddMediaToPlaylistAsync_ManualMode_AppendsWithSortOrder` | `PlaylistServiceTests_AddMedia` (erweitert) | Neue Einträge im Manual-Mode erhalten `SortOrder = Max + 1` |
| `AddMediaToPlaylistAsync_ByReleaseDateMode_SortOrderIsNull` | `PlaylistServiceTests_AddMedia` (erweitert) | Neue Einträge im ByReleaseDate-Mode erhalten `SortOrder = null` |
| `AddMediaToPlaylistAsync_ManualMode_CascadeChildren_SequentialSortOrder` | `PlaylistServiceTests_AddMedia` | Cascade-Kinder (z. B. Episoden) erhalten aufsteigende SortOrder |
| `GetPlaylistEntriesPagedAsync_ManualMode_SortedBySortOrder` | `PlaylistServiceTests_GetEntriesPaged` (erweitert) | Paginierte Einträge im Manual-Mode sind nach SortOrder sortiert |
| `GetPlaylistEntriesPagedAsync_ByReleaseDateMode_SortedByReleaseDate` | `PlaylistServiceTests_GetEntriesPaged` (erweitert) | Paginierte Einträge im ByReleaseDate-Mode folgen bestehender Sortierung (unverändert) |
| `GetPlaylistEntriesPagedAsync_ManualMode_NullSortOrder_Fallback` | `PlaylistServiceTests_GetEntriesPaged` | Null-SortOrder-Einträge werden als Fallback nach AddedAt sortiert |
| `CreatePlaylistEndpoint_WithSortMode_CreatesCorrectly` | `PlaylistsControllerTests_Create` (erweitert) | Playlist mit `SortMode` kann erstellt werden (bereits vorhanden?) |
| `ReorderPlaylistEntryEndpoint_WithValidRequest_Returns200` | `PlaylistsControllerTests_Reorder` (neu) | Endpoint `PUT /api/playlists/{id}/entries/{entryId}/order` gibt 200 OK zurück |
| `ReorderPlaylistEntryEndpoint_NotOwner_Returns403` | `PlaylistsControllerTests_Reorder` | Endpoint gibt 403 Forbidden zurück |
| `ReorderPlaylistEntryEndpoint_NotManualMode_Returns400` | `PlaylistsControllerTests_Reorder` | Endpoint gibt 400 Bad Request zurück |
| `ReorderPlaylistEntryEndpoint_EntryNotFound_Returns404` | `PlaylistsControllerTests_Reorder` | Endpoint gibt 404 Not Found zurück |
| `BatchReorderPlaylistEntriesEndpoint_WithValidRequest_Returns200` | `PlaylistsControllerTests_Reorder` | Endpoint `POST /api/playlists/{id}/entries/batch-reorder` gibt 200 OK zurück |
| `BatchReorderPlaylistEntriesEndpoint_DuplicateSortOrder_Returns409` | `PlaylistsControllerTests_Reorder` | Endpoint gibt 409 Conflict zurück |
| `ChangeSortModeEndpoint_ManualToByReleaseDate_WithoutConfirmation_Returns409` | `PlaylistsControllerTests_SortMode` (neu) | Endpoint gibt 409 Conflict mit `isLossOfDataConfirmationRequired: true` zurück |
| `ChangeSortModeEndpoint_ManualToByReleaseDate_WithConfirmation_Returns200` | `PlaylistsControllerTests_SortMode` | Mit Bestätigung: Endpoint gibt 200 OK zurück |
| `ChangeSortModeEndpoint_ByReleaseDateToManual_Returns200` | `PlaylistsControllerTests_SortMode` | Endpoint gibt 200 OK zurück |
| `E2E_DragDropReorder_ManualMode_PersistsSortOrder` | `PlaylistDetailE2ETests` (erweitert) | Drag-&-Drop in Blazor-Komponente persistiert neue SortOrder in DB |
| `E2E_QuickActionButtons_MoveToBeginning_Reorders` | `PlaylistDetailE2ETests` | Quick-Action „An Anfang" ordnet Entry um |
| `E2E_QuickActionButtons_MoveToEnd_Reorders` | `PlaylistDetailE2ETests` | Quick-Action „An Ende" ordnet Entry um |
| `E2E_SortModeChange_Manual_Displays_DragControls` | `PlaylistDetailE2ETests` | Nach Wechsel zu Manual-Mode: Drag-&-Drop-Kontrollen sind sichtbar |
| `E2E_SortModeChange_ByReleaseDate_ToManual_NeverWarns` | `PlaylistDetailE2ETests` | Wechsel ByReleaseDate → Manual: Keine Warnung |
| `E2E_SortModeChange_Manual_ToByReleaseDate_ShowsWarningModal` | `PlaylistDetailE2ETests` | Wechsel Manual → ByReleaseDate: Modal-Dialog wird angezeigt |
| `E2E_SortModeChange_Manual_ToByReleaseDate_ConfirmDeletion_UpdatesMode` | `PlaylistDetailE2ETests` | Bestätigung in Modal: Modus wird gewechselt, SortOrder wird gelöscht |
| `E2E_BatchReorder_MultipleEntries_AllReorder` | `PlaylistEntriesE2ETests` (erweitert) | Batch-Reorder mit mehreren Entries funktioniert |
| `E2E_InfinityList_ManualMode_VirtualizationWorks` | `PlaylistDetailE2ETests` | Virtualisierung funktioniert mit Manual-Mode-Sortierung (große Listen) |
| `CreatePlaylistEntry_WithSortOrder_IsNullable` | `PlaylistServiceTests_...` (Unit) | SortOrder ist nullable und wird bei Bedarf gespeichert |

**Hilfsmethoden und Fixtures** (in bestehenden TestBase-Klassen):
- `CreatePlaylistInManualMode()` — Erstellt Test-Playlist mit `SortMode == Manual`
- `CreatePlaylistWithEntries_ManualMode(int count)` — Erstellt Playlist mit *n* Einträgen im Manual-Mode mit aufsteigenden SortOrder-Werten (0, 1, ..., n-1)
- `VerifySortOrderSequence(List<DtoPlaylistEntry> entries, long[] expectedOrder)` — Prüft, dass Einträge erwartete SortOrder-Reihenfolge haben

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistServiceTests_GetEntriesPaged` (alle) | `GetPlaylistEntriesPagedAsync()` wurde erweitert: Sortierlogik muss für Manual-Mode angepasst werden. Tests müssen überprüft werden, ob sie ByReleaseDate-Sortierung erwarten oder flexibel sind. |
| `PlaylistServiceTests_AddMedia` (alle) | `AddMediaToPlaylistAsync()` wurde erweitert: SortOrder-Zuweisung muss getestet werden. Bestehende Tests könnten fehlschlagen, wenn sie annahmen, dass `SortOrder == null` immer. |
| `PlaylistDetailE2ETests` (wenn vorhanden) | Neue Drag-&-Drop- und Modal-Dialog-Logik wird getestet; bestehende Tests könnten Renders beeinflussen. |
| `PlaylistsControllerTests_Update` | Falls bestehende Tests `UpdatePlaylist` mit SortMode-Änderung aufrufen: Verhalten könnte sich ändern (Mode-Wechsel läuft jetzt über `/sort-mode` Endpoint, nicht über `/api/playlists/{id}`). Anpassung kann erforderlich sein. |

Falls `UpdatePlaylist` weiterhin direkt SortMode-Wechsel erlaubt (ohne `ChangeSortModeAsync` zu rufen), könnten Tests ohne Anpassung weiterhin funktionieren. Empfehlung: **Tests für beide Wege schreiben (direkt über `UpdatePlaylist` und über `/sort-mode` Endpoint)**, um sicherzustellen, dass beide Pfade konsistent sind.

### E2E-Tests (primärer Funktionsnachweis)

Für jede neue oder geänderte Benutzerinteraktion muss E2E-Abdeckung geplant werden:

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Benutzer ordnet Eintrag via Drag & Drop um und die neue Reihenfolge persistiert über mehrere Request-Zyklen | `PlaylistDetailE2ETests.cs` / `E2E_DragDropReorder_ManualMode_PersistsSortOrder` | "Playlists mit `SortMode == Manual` ermöglichen es Benutzern, die Reihenfolge ihrer Einträge durch Drag & Drop zu ändern. Die geänderte Reihenfolge wird dauerhaft in der Datenbank gespeichert und bestimmt sowohl die Anzeige als auch die Wiedergabereihfolge." | Drag-&-Drop ist Client-seitige Interaktion, die nur über Browser-Automation testbar ist. Unit-Tests können API-Aufrufe mocken, aber echte Drag-&-Drop-Mechanik erfordert Browser-Kontext. |
| Pflicht | Benutzer klickt Quick-Action-Button „An Anfang" und Eintrag wird an Position 0 verschoben | `PlaylistDetailE2ETests.cs` / `E2E_QuickActionButtons_MoveToBeginning_Reorders` | "Schnellaktionen („An Anfang", „An Ende") ermöglichen manuelle Umordnung." | Button-Click ist UI-Interaktion; Ergebnis (Eintrag an Anfang) muss in UI und DB verifiziert werden. E2E prüft Integration zwischen Frontend-Button, Backend-API und DB. |
| Pflicht | Benutzer klickt Quick-Action-Button „An Ende" und Eintrag wird an Ende der Liste verschoben | `PlaylistDetailE2ETests.cs` / `E2E_QuickActionButtons_MoveToEnd_Reorders` | Wie „An Anfang" | Wie „An Anfang" |
| Pflicht | Benutzer wechselt `SortMode` von `ByReleaseDate` zu `Manual` und Drag-&-Drop-Kontrollen werden aktiv | `PlaylistDetailE2ETests.cs` / `E2E_SortModeChange_Manual_Displays_DragControls` | "Der Sortiermodus kann zwischen Auto- und Manualmodus gewechselt werden; beim Umschalten zwischen Auto- und Manualmodus wird die chronologische Reihenfolge beibehalten." | Mode-Wechsel hat UI-Seiteneffekte (Kontrollen erscheinen/verschwinden); nur E2E kann Sichtbarkeit und Aktivierung prüfen. |
| Pflicht | Benutzer wechselt `SortMode` von `Manual` zu `ByReleaseDate` ohne Bestätigung und Modal-Dialog wird angezeigt | `PlaylistDetailE2ETests.cs` / `E2E_SortModeChange_Manual_ToByReleaseDate_ShowsWarningModal` | "Der Benutzer wird beim Wechsel zu automatischer Sortierung vor Datenverlust gewarnt." | Modal-Dialog ist UI-Element, das nur E2E-Test ansprechen kann. API-Responses und Modal-Rendering sind gekoppelt. |
| Pflicht | Benutzer bestätigt Datenverlust-Warnung in Modal und Mode wird tatsächlich gewechselt; alte SortOrder-Werte sind gelöscht | `PlaylistDetailE2ETests.cs` / `E2E_SortModeChange_Manual_ToByReleaseDate_ConfirmDeletion_UpdatesMode` | "Beim Wechsel zu automatischer Sortierung werden SortOrder-Werte gelöscht." | Bestätigung im Modal triggert API-Call mit `confirmLossOfManualOrder: true`. E2E prüft, dass Modal-Button → API-Call → DB-Änderung funktioniert. |
| Empfohlen | Benutzer fügt neue Einträge zu Playlist im Manual-Mode hinzu und diese landen automatisch am Ende | `PlaylistDetailE2ETests.cs` / `E2E_AddMediaToPlaylist_ManualMode_AppendedAtEnd` | "Neu hinzugefügte Einträge werden im manuellen Modus am Ende der Liste eingefügt." | UI-Flow: Medien hinzufügen → neue Einträge in Liste → Sortierung muss korrekt sein. E2E testet kompletten Flow; Unit-Test würde nur API prüfen, nicht UI-Rendering. |
| Empfohlen | Virtualisierte Liste (Infinity List) mit vielen Einträgen (100+) im Manual-Mode bleibt responsive | `PlaylistDetailE2ETests.cs` / `E2E_InfinityList_ManualMode_VirtualizationWorks` | "Infinity-List / Virtualisierung funktioniert auch mit Manual-Mode-Sortierung." | Performance und Virtualisierung können nur durch tatsächliche Browser-Last getestet werden. Unit-Tests würden DB-Abfragen mocken, nicht die Rendering-Performance prüfen. |
| Empfohlen | Batch-Reorder mit mehreren Entries wird über API atomisch durchgeführt; falls eine Operation fehlschlägt, sind keine partiellen Updates vorhanden | `PlaylistEntriesE2ETests.cs` / `E2E_BatchReorder_MultipleEntries_AllReorder` | "Batch-Reorder ist transaktional (all-or-nothing)." | Transaktion-Atomarität ist schwer in Unit-Tests zu prüfen (da DB gemockt wird). E2E mit echter DB verifiziert, dass bei API-Fehler keine partiellen Updates entstanden sind. |
| Optional | Drag & Drop zwischen zwei Einträgen mit Zwischenwert-Berechnung (falls implementiert) | `PlaylistDetailE2ETests.cs` / `E2E_DragDropReorder_BetweenEntries_CalculatesIntermediateSortOrder` | (Abhängig von Implementierungsdetail) | Nur E2E kann Zwischenwert-Berechnung bei echtem Drag-&-Drop testen. |

**Welche bestehenden E2E-Tests müssen angepasst werden?**

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistDetailE2ETests` (wenn vorhanden) | Falls bereits Tests für Playlist-Details existieren, könnten diese durch neue Drag-&-Drop-Event-Handling oder Modal-Dialoge beeinflusst werden. Überprüfung erforderlich. |
| `PlaylistEntriesE2ETests` (wenn vorhanden) | Falls bereits Tests für Einträge-Anzeige existieren, müssen diese auf Manual-Mode-Sortierung und neue UI-Controls (Buttons, Drag-&-Drop) überprüft werden. |

Falls keine E2E-Tests vorhanden sind, sollten alle oben geplanten E2E-Tests neu geschrieben werden.

---

## Offene Punkte

Keine offenen Punkte. Alle Anforderungen aus `requirement.md` sind geklärt und in den Plan eingearbeitet:

- **SortOrder-Werte-Raum:** Lücken sind erlaubt (vereinfacht Implementierung) — siehe Designentscheidungen.
- **Drag-&-Drop-Zwischenwerte:** Optional; Defragmentierung ist optional für zukünftige Optimierung — siehe Designentscheidungen.
- **Modal-Wording:** Deutsch, „Die manuelle Reihenfolge wird gelöscht und kann nicht wiederhergestellt werden. Möchten Sie fortfahren?" — siehe Programmabläufe.
- **Performance bei sehr großen Listen:** Infinity-List / Virtualisierung handhabt dies; Test ist geplant.
- **Rückwärtskompatibilität:** Migration setzt `SortOrder = null` für bestehende Einträge; keine Probleme.
- **Cascade-Einträge im Manual-Mode:** Alle erhalten aufsteigende SortOrder — siehe Programmablauf 5.
- **Quick-Action-Button Platzierung:** Inline neben Eintrag — siehe Designentscheidungen und Programmabläufe.
- **Batch-Reorder Transaktionalität:** All-or-Nothing via EF Core Transaktion — siehe Designentscheidungen.
- **SortMode-Eigenschaft auf Playlist:** Existiert bereits aus Schritt 1 — bestätigt.

---

**Dokumentation erstellt:** 2026-09-07  
**Branch:** task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-4-manuelle-sortierung
