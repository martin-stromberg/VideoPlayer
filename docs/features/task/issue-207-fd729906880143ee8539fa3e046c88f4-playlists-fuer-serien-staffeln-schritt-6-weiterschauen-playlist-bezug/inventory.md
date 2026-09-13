# Bestandsaufnahme: Weiterschauen mit Playlist-Bezug (Schritt 6)

Diese Bestandsaufnahme analysiert den bestehenden Projekt-Code bezüglich der Anforderung, das Weiterschauen-System um optionale Playlist-Bindung zu erweitern. Der Fokus liegt auf der Identifizierung fehlender Komponenten und der Deduplizierungsstrategie für Weiterschauen-Einträge.

---

## Zusammenfassung

### Was ist vorhanden

1. **Datenmodell (teilweise):**
   - `ContinueWatchingEntry` existiert mit den Eigenschaften: UserId, MovieId, TVShowEpisodeId, Position, Duration, ListOrder, UpdatedAt
   - `Playlist` und `PlaylistPlaybackContext` existieren bereits von Schritt 5 (Playlist-Wiedergabe)
   - Indizes auf (UserId, MovieId), (UserId, TVShowEpisodeId), (UserId, ListOrder, UpdatedAt)

2. **Service-Logik (teilweise):**
   - `ContinueWatchingService` verwaltet Weiterschauen-Einträge
   - Deduplizierungsstrategie: Pro Serie und Filmsammlung wird nur ein Eintrag pro Benutzer beibehalten
   - Deduplizierung erfolgt durch `RemoveExistingTVShowEntry()` und `RemoveExtsingMovieCollectionEntry()` (Service-Methoden, nicht Datenbankebene)
   - `ProcessBufferedEntryAsync()` erstellt/aktualisiert Einträge und prüft Abschluss-Schwelle
   - `ReportProgressAsync()` puffert Wiedergabe-Fortschritt

3. **API-Endpoints (teilweise):**
   - `POST /api/continue-watching/progress` — Meldet Fortschritt
   - `POST /api/continue-watching/hide` — Verbirgt Eintrag
   - `POST /api/continue-watching/skip` — Überspringt Eintrag
   - `GET /api/continue-watching` — Gibt Liste zurück

4. **UI-Komponenten (teilweise):**
   - `ContinueWatchingList.razor` zeigt Weiterschauen-Einträge an
   - `VideoPlayer.razor` implementiert Playlist-Wiedergabe mit `PlaylistPlaybackContext` (Schritt 5)
   - Navigation (Next/Previous/Advance) in Playlists vorhanden

5. **Tests (vorhanden):**
   - 31 Tests für ContinueWatching-Funktionalität, alle bestanden
   - Tests abdecken: Episode-Navigation, Hide/Skip-Aktionen, SignalR-Events, Gesehen-Markierung, E2E-Szenarien

### Was fehlt für die Anforderung

1. **Datenmodell (zu erweitern):**
   - ❌ `PlaylistId` (long?, nullable FK) in `ContinueWatchingEntry`
   - ❌ Navigations-Property `Playlist` in `ContinueWatchingEntry`
   - ❌ Unique Constraint auf `(UserId, (MovieId OR TVShowEpisodeId), PlaylistId)`
   - ❌ Migration für neue Spalte

2. **DTO-Erweiterungen:**
   - ❌ `PlaylistId` (long?) in `ContinueWatchingDto`
   - ❌ `PlaylistName` (string?) in `ContinueWatchingDto`

3. **Service-Methoden (zu erweitern/neu):**
   - ❌ `CreateOrUpdateContinueWatchingWithPlaylistAsync()` (neu)
   - ❌ Erweiterung von `ReportProgressAsync()` um PlaylistId-Parameter
   - ❌ Erweiterung von `ProcessBufferedEntryAsync()` um PlaylistId-Parameter
   - ❌ Erweiterung von `RemoveExistingTVShowEntry()` um PlaylistId-Filter
   - ❌ Erweiterung von `RemoveExtsingMovieCollectionEntry()` um PlaylistId-Filter
   - ❌ `DeleteContinueWatchingEntryAsync()` (neu)

4. **API-Request-Erweiterungen:**
   - ❌ `PlaylistId` (long?) Parameter in `ProgressRequest`
   - ❌ `PlaylistId` (long?) Parameter in `ContinueWatchingActionRequest`

5. **UI-Erweiterungen:**
   - ❌ Anzeige von Playlist-Zuordnung in `ContinueWatchingList.razor`
   - ❌ Playlist-Rekonstruktion beim Fortsetzen aus der Weiterschauen-Liste

6. **JavaScript-Erweiterungen:**
   - ❌ `continueWatching.js` — Übergabe von `playlistId` an Progress-API

7. **Tests (neu zu schreiben):**
   - ❌ `ContinueWatchingServicePlaylistTests` — Tests für Playlist-Bezug
   - ❌ `ContinueWatchingServiceMultipleEntriesTests` — Tests für mehrfaches Vorkommen
   - ❌ `ContinueWatchingServiceRemovalTests` — Tests für Entfernung mit Playlist-Filter
   - ❌ `ContinueWatchingDtoTests` — Tests für DTO mit Playlist-Properties

### Deduplizierungs-Strategie (aktuell und erforderlich)

#### Aktuell (Schritt 1-5)

- **Eindeutigkeits-Schlüssel:** `(UserId, VideoId)` pro Serie/Sammlung
- **Durchsetzungs-Ort:** Service-Logik (`RemoveExistingTVShowEntry`, `RemoveExtsingMovieCollectionEntry`)
- **Verhalten:** 
  - Beim Erstellen eines neuen Eintrags werden alle anderen Einträge derselben Serie/Sammlung entfernt
  - Pro Benutzer und Serie/Sammlung existiert nur ein Eintrag

#### Erforderlich für Schritt 6

- **Neuer Eindeutigkeits-Schlüssel:** `(UserId, VideoId, PlaylistId)` 
- **Durchsetzungs-Ort:** Datenbankebene (Unique Constraint) + Service-Logik
- **Verhalten:**
  - Ein Video kann pro Benutzer mehrfach vorkommen (mit unterschiedlichen PlaylistId-Werten oder null)
  - Für die gleiche PlaylistId wird das älteste Duplikat entfernt
  - Einträge ohne PlaylistId und mit PlaylistId sollten unabhängig sein
  - Deduplizierung muss berücksichtigen: Ist PlaylistId null oder eine konkrete ID?

### Playlist-Wiedergabe (Schritt 5 — bereits implementiert)

**Status:** Von Schritt 5 bereits vorhanden und funktionsfähig

- `PlaylistPlaybackContext` (record) mit: PlaylistId, CurrentEntryId, TotalCount, PlaylistName
- `VideoPlayer.razor` empfängt `PlaylistContext` als Parameter
- Navigation (Next/Previous/Advance/Restart) via `IPlaylistApiClient`
- Lokaler Zustand im Player (currentPlaylistId, currentPlaylistPosition, etc.)
- Badge-Anzeige: `[PlaylistName: Position/Total]`

**Integration mit Schritt 6:**
- PlaylistId muss von `VideoPlayer.razor` beim Progress-Report an `continueWatching.js` übergeben werden
- Beim Start aus einer Weiterschauen-Liste muss `PlaylistPlaybackContext` rekonstruiert werden (falls PlaylistId existiert)

---

## Details

- [Datenmodelle](inventory/models.md)
- [Logik und Services](inventory/logic.md)
- [Interfaces und API-Contracts](inventory/interfaces.md)
- [Tests und Ausgangszustand](inventory/tests.md)

---

## Kritische Erkenntnisse für die Implementierung

### 1. Deduplizierungsstrategie muss auf Playlist-Ebene arbeiten

Die aktuelle Deduplizierung arbeitet auf Serie/Sammlung-Ebene und ist in der Service-Logik implementiert. Für Schritt 6 muss die Logik erweitert werden:

```
Aktuell:        (UserId, SeriesId) → ein Eintrag
Schritt 6:      (UserId, SeriesId, PlaylistId) → ein Eintrag pro Playlist-Kombination
```

**Wichtig:** `RemoveExistingTVShowEntry()` muss zusätzlich prüfen, ob die neue PlaylistId mit der bestehenden übereinstimmt, bevor es Einträge entfernt.

### 2. PlaylistId-Übergabe über mehrere Schichten

Progress-Report-Fluss:
1. `VideoPlayer.razor` hat `PlaylistContext` (von Schritt 5 bekannt)
2. Muss PlaylistId an `continueWatching.js` übergeben (derzeit nicht unterstützt)
3. `continueWatching.js` sendet PlaylistId in POST-Body an `/api/continue-watching/progress`
4. Controller parst PlaylistId und gibt sie an `ContinueWatchingService` weiter
5. Service nutzt PlaylistId für Deduplizierung

### 3. Nullability der PlaylistId

PlaylistId ist nullable (optional). Das bedeutet:
- Ein Video kann ohne Playlist-Bezug in der Weiterschauen-Liste sein (PlaylistId = null)
- Gleiches Video kann mit PlaylistId = 5 auch in der Liste sein
- Diese sind separate Einträge und müssen unabhängig verwaltet werden
- Unique Constraint muss mit nullables Komponenten umgehen

### 4. Backwards-Compatibility

Nach der Migration werden bestehende `ContinueWatchingEntry`-Einträge PlaylistId = null haben. Diese müssen weiterhin funktionieren (Non-Playlist-Wiedergabe).

### 5. GetListAsync muss Playlist-Info laden

`ContinueWatchingService.GetListAsync()` muss erweitert werden, um:
- `PlaylistId` und `PlaylistName` zu laden (via Join mit Playlist-Tabelle)
- Diese in das DTO zu kopieren
- Berechtigungen zu prüfen (Benutzer hat Zugriff auf die Playlist)

---

## Test-Ausgangszustand

**Alle bestehenden Tests bestanden:** 31/31 ✓

Nachweis: [test-results/TestResults_ContinueWatching.trx](inventory/test-results/TestResults_ContinueWatching.trx)

Zeitpunkt: 2026-09-13T12:30:00Z UTC
Branch: task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-6-weiterschauen-playlist-bezug
Commit: f48a054

---

## Abhängigkeiten zu anderen Komponenten

- **Von Schritt 5 abhängig:** `PlaylistPlaybackContext`, `PlaylistService`, Playlist-Entität
- **Abhängig von:** `WatchedStatusService` (Gesehen-Markierung), `MediaUpdateNotificationService` (SignalR), `ProgramSettingsService`
- **UI-Abhängigkeiten:** `VideoPlayer.razor`, `ContinueWatchingList.razor`, `continueWatching.js`

---

## Offene Fragen aus der Anforderung (noch zu klären)

1. **Unique Constraint Formulierung:** Wie wird der Constraint in EF Core mit Nullable-Komponenten am besten definiert?
2. **Cascade-Delete-Verhalten:** Wenn Playlist gelöscht wird, sollen zugehörige Einträge gelöscht oder auf PlaylistId=null gesetzt werden?
3. **UI-Display bei Playlist-Bezug:** Wie wird die Playlist-Zuordnung angezeigt? Badge, Text, Gruppierung?
4. **Fehlerbehandlung:** Was geschieht, wenn Playlist gelöscht wurde oder Video nicht mehr darin enthalten ist?
5. **Sortierung mehrerer Einträge:** Wenn ein Video mehrfach vorkommen kann, wie wird die Sortierung gehandhabt?
