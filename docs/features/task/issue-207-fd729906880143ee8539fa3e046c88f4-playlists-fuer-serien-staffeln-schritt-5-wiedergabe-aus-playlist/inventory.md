# Bestandsaufnahme: Playlist-Wiedergabe (Schritt 5)

## Übersicht

Diese Bestandsaufnahme analysiert den bestehenden Code und die Infrastruktur, die für die Implementierung von Playlist-Wiedergabe (Schritt 5 des Playlist-Features für Serien/Staffeln) relevant ist. Der Fokus liegt auf:

1. **Bestehende Wiedergabe-Infrastruktur:** Video-Player-Komponente, Startpunkt, State-Handling
2. **Sortierreihenfolge-Bestimmung:** Automatischer Modus (Release Date → Hierarchie → Aufnahmezeitpunkt) und manueller Modus
3. **Freischaltungsprüfung:** PlaylistEntryAccessResolver, MediaHierarchyRegistry und deren Aufrufketten
4. **Sammel-Einträge:** Ob und wie Serien/Staffeln/Sammlungen in Playlists vollständig zu Einzeltiteln aufgelöst werden
5. **Datenmodelle und API-Endpunkte:** Playlist, PlaylistEntry, PlaylistsController

---

## Zusammenfassung der Befunde

### Vorhanden und Verwendbar

✅ **Video-Player-Komponente (`VideoPlayer.razor`):**
- Vollständig funktionale Razor-Komponente mit HTML5 `<video>` Element
- Unterstützung für `StreamUrl`, `MediaType`, `MediaId`, `AuthToken`
- Event `PositionChanged` für Positionsaktualisierungen
- JS-Integration für Playback-Tracking via `continueWatching.attach/detach`
- JavaScript-Funktionen: `setStartPosition`, `registerTimeUpdate`, `getCurrentPosition`
- **Status:** Kann als Basis erweitert werden für Playlist-Context-Anzeige und Auto-Advance-Events

✅ **Sortierreihenfolge-Bestimmung (`PlaylistService.SortPlaylistEntriesForModeAsync`):**
- Zentrale Methode, die bereits beide Sortiermode implementiert:
  - `ByReleaseDate`: Release Date → Hierarchie (Parent ID) → Episodennummer → Aufnahmezeitpunkt
  - `Manual`: SortOrder (aufsteigend) → Aufnahmezeitpunkt (Fallback)
- Verwendet `MediaHierarchyRegistry` für Metadaten-Bulk-Loading (Release Dates, Hierarchy, Pictures)
- **Status:** Kann direkt von `GetNextPlaylistEntryAsync`/`GetPreviousPlaylistEntryAsync` genutzt werden

✅ **Freischaltungsprüfung (`PlaylistEntryAccessResolver`):**
- Vollständig implementierte Logik für Bulk-Freischaltungsprüfung
- Kanonische Regel: `hasSourceAccess OR isUnlocked`
- Hierarchieauflösung für Filme und Episoden (zu ihren Sammlungen/Serien)
- Wird bereits von `PlaylistService.BuildEntryDtosAsync` aufgerufen
- **Status:** Kann ohne Änderung für Wiedergabe-Navigation genutzt werden

✅ **Datenmodelle (`Playlist`, `PlaylistEntry`):**
- `Playlist` hat alle Felder außer `CurrentEntryId` (kann hinzugefügt werden)
- `PlaylistEntry` hat alle erforderlichen Felder: `MediaType`, `MediaId`, `ParentMediaType`, `ParentMediaId`, `SortOrder`, `AddedAt`
- Konfiguration über `PlaylistConfiguration` und `PlaylistEntryConfiguration`
- **Status:** Minimal veränderbar, nur `CurrentEntryId` Property und DB-Migration nötig

✅ **Controller-Endpunkte (`PlaylistsController`):**
- Bestehende Endpunkte: GET Playlists, Entries, Entries/paged, Reorder, SortMode, etc.
- Zentrale Exception-Handling- Infrastruktur via `ExecuteAsync`
- Authentifizierungs-Integration via `CheckLogedIn()` und `CurrentUser`
- **Status:** Kann erweitert werden mit neuen Play-Endpunkten

✅ **Test-Infrastruktur:**
- Umfassende Unit-Tests für PlaylistService (52+ Tests)
- Umfassende Unit-Tests für PlaylistsController (40+ Tests)
- E2E-Tests für Playlists-UI (10+ Tests)
- Test-Basis-Klassen und Fixtures vorhanden: `PlaylistServiceTestBase`, `PlaylistsE2ETestBase`
- **Status:** Alle bestehenden Tests bestanden, 0 Fehler (siehe [tests.md](inventory/tests.md))

### Nicht Vorhanden (Neu zu Implementieren)

❌ **Playlist-Kontext-Provider (`IPlaylistContextProvider`):**
- Interface nicht definiert
- Keine Session-Verwaltung für aktuellen Wiedergabe-Kontext
- Muss neu implementiert werden

❌ **Wiedergabe-Navigation-Methoden:**
- `GetNextPlaylistEntryAsync` in `PlaylistService`
- `GetPreviousPlaylistEntryAsync` in `PlaylistService`
- `StartPlaylistAsync` in `PlaylistService`
- `AdvancePlaylistAsync` in `PlaylistService`
- Muss neu implementiert werden

❌ **Playlist-Wiedergabe-Endpunkte in Controller:**
- `POST /api/playlists/{id}/play?entryId={entryId}`
- `POST /api/playlists/{id}/play/next`
- `POST /api/playlists/{id}/play/previous`
- Muss neu implementiert werden

❌ **VideoPlayer-Erweitarungen:**
- Playlist-Badge-Anzeige (z.B. "[Meine Favoriten: 3/12]")
- Next/Previous-Buttons für Playlist-Navigation
- Auto-Advance-Event-Handling bei Titel-Ende
- Muss neu implementiert werden

❌ **Sammel-Einträge-Behandlung:**
- Noch unklar, ob Serien/Staffeln/Sammlungen als direkte Playlist-Einträge vorkommen können
- Laut Anforderung sollten sie beim Hinzufügen (Schritt 2) aufgelöst werden
- **Bestandsaufnahme erforderlich:** PlaylistService.AddMediaToPlaylistAsync macht Kaskadenauflösung via MediaHierarchyRegistry

### Sammel-Einträge - Analyseergebnis

**Befund:** Beim Hinzufügen werden Serien und Staffeln zu ihren Einzeltiteln aufgelöst:

- `AddMediaToPlaylistAsync` ruft `GetCascadeMediaIdsAsync` auf
- `MediaHierarchyRegistry.Handlers[MediaType].LoadCascadeChildrenAsync` wird genutzt:
  - TVShow → Staffeln + Episoden
  - TVShowSeason → Episoden
  - MovieCollection → Filme
- Es wird ein Top-Level-Eintrag (die Sammlung selbst) PLUS die Cascade-Kinder hinzugefügt
- **Aber:** Sammel-Einträge (TVShow, TVShowSeason, MovieCollection) können noch als separate `PlaylistEntry` Zeilen in der DB vorhanden sein

**Implikation für Wiedergabe:**
- Nur `TVShowEpisode` ist direkt abspielbar
- `Movie` braucht Sammlung für Unlock-Check
- TVShow, TVShowSeason, MovieCollection sollten bei Wiedergabe-Navigation **übersprungen** werden
- Die Filterung "nur abspielbare Medientypen" muss in `GetNextPlaylistEntryAsync` implementiert werden

---

## Details

- [Datenmodelle](inventory/models.md) - Playlist, PlaylistEntry, PlaylistSortMode, DTOs
- [Logik-Komponenten](inventory/logic.md) - PlaylistService, PlaylistEntryAccessResolver, IUnlockedMediaService, VideoPlayer.razor, PlaylistsController
- [Interfaces](inventory/interfaces.md) - IPlaylistService, IUnlockedMediaService, IPlaylistContextProvider (geplant)
- [Enums](inventory/enums.md) - PlaylistSortMode, MediaType, UnlockIdSpace
- [Tests](inventory/tests.md) - Test-Ausgangszustand, bestehende Tests, fehlende Tests

---

## Test-Ausgangszustand

- **Zeitpunkt:** 2026-09-08 20:20:29 UTC+02:00
- **Branch:** `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-3-automatische-sortierung-anzeige`
- **Commit:** `22674a80b32bbcd065c9157bf988647723667477`

**Ergebnis:** ✅ **Alle 500+ Tests bestanden**
- 0 Fehlgeschlagen
- 0 Übersprungen
- Exit-Code: 0

Nachweis: [test-run-baseline.log](inventory/test-results/test-run-baseline.log)

### Bestehende Test-Coverage für Wiedergabe-Anforderungen

| Bereich | Tests | Status |
|---------|-------|--------|
| Sortierung (ByReleaseDate mit Fallback) | 6 Tests in `PlaylistServiceTests_GetEntriesPaged` | ✅ Bestanden |
| Sortierung (Manual) | 4 Tests in `PlaylistServiceTests_GetEntriesPaged` | ✅ Bestanden |
| Freischaltungsprüfung | 18 Tests in `PlaylistServiceTests_GetEntries` | ✅ Bestanden |
| Paginierung | 11 Tests in `PlaylistServiceTests_GetEntriesPaged` | ✅ Bestanden |
| Controller-Authentifizierung | 6 Tests in `PlaylistsControllerTests_Auth` | ✅ Bestanden |
| **Wiedergabe-Navigation** | **0 Tests** | ❌ Nicht vorhanden |
| **Kontext-Provider** | **0 Tests** | ❌ Nicht vorhanden |

---

## Kritische Erkenntnisse für Implementierung

1. **Sortierung ist zentral:** `PlaylistService.SortPlaylistEntriesForModeAsync` implementiert bereits die komplette Sortierlogik. Neue `GetNext`/`GetPrevious` Methoden müssen nur sortierte Liste durchlaufen und Freischaltung prüfen.

2. **Freischaltung ist bulk-optimiert:** `PlaylistEntryAccessResolver.ResolveAccessibilityAsync` lädt Daten in Bulk (eine Query pro Medientyp) - das ist bereits optimal und kann direkt genutzt werden.

3. **Sammel-Einträge müssen gefiltert werden:** Nur `TVShowEpisode` ist abspielbar; `Movie` braucht Kontext. Implementierung von `GetNext`/`GetPrevious` muss `MediaHierarchyRegistry` nutzen, um nicht-abspielbare Typen zu filtern.

4. **Video-Player-Erweiterung ist gering:** Der Player hat bereits alle nötigen Events und JS-Hooks; neue Funktionen sind UI-Layer (Badge, Buttons) und Event-Handling (Auto-Advance).

5. **Migrationen nötig:** `CurrentEntryId` Property in `Playlist` braucht eine neue Migration, falls Persistierung gewünscht ist (Annahme: Session-based im ersten Schritt).

---

## Abhängigkeiten zu Vorigen Schritten

Diese Bestandsaufnahme zeigt, dass Schritt 5 auf folgenden Schritten aufbaut:

1. **Schritt 1-2 (Playlist CRUD + Media Add):** ✅ Vollständig implementiert und getestet
2. **Schritt 3 (Automatische Sortierung + Anzeige):** ✅ Vollständig implementiert und getestet
3. **Schritt 4 (Reorderung + Manueller Modus):** ✅ Vollständig implementiert und getestet

Schritt 5 kann auf dieser stabilen Basis aufgebaut werden ohne bestehenden Code zu brechen.

