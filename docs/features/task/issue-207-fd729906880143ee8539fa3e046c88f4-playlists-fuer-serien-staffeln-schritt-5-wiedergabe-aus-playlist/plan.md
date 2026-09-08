# Umsetzungsplan: Playlist-Wiedergabe (Schritt 5)

## Übersicht

Schritt 5 implementiert die Wiedergabe von Titeln aus einer Playlist heraus mit automatischem und manuellem Weiterschalten sowie Playlist-Kontext-Anzeige im Video-Player. Die Umsetzung erweitert den bestehenden Video-Player und den PlaylistService um Navigations- und Kontextmanagement-Funktionalität, ohne die bereits implementierte Sortierreihenfolge- und Freischaltungsprüfung zu duplizieren. Der Playlist-Kontext wird **ausschließlich client-seitig** verwaltet (in der VideoPlayer.razor-Komponente und/oder über URL-Query-Parameter); Server-Endpunkte arbeiten zustandslos.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Playlist-Kontext-Verwaltung** | Rein client-seitig (kein server-seitiger Session-Store) | ASP.NET Core Blazor Server: Jeder HTTP-Request erzeugt einen neuen, isolierten DI-Scope. Ein als "Scoped" registrierter Service lebt nur für die Dauer eines einzelnen HTTP-Requests. Ein Kontext-Provider würde beim nächsten Request eine komplett neue, leere Instanz erhalten — es gibt keine Zustands-Teilung zwischen API-Aufrufen über einen Memory-Service. Stattdessen: Client übergibt alle erforderlichen Parameter explizit (playlistId, currentEntryId, sortMode). Kontext wird in VideoPlayer.razor-Komponentenzustand und/oder URL-Query-Parametern gespeichert, sodass Neuladen/Seitenwechsel den Kontext rekonstruieren können. |
| **Sammel-Einträge-Filterung** | Übersprung bei Navigation (GetNext/GetPrevious) | Nur TVShowEpisode und Movie sind abspielbar; Sammel-Einträge (TVShow, TVShowSeason, MovieCollection) sind nicht direkt abspielbar und werden bei der Navigation übersprungen. Die Filterung nutzt `MediaHierarchyRegistry.Handlers[MediaType].IsPlayable` oder vergleichbare Logik. |
| **Wiedergabe-Start-Endpunkt** | POST /api/playlists/{id}/play?entryId={entryId} | RESTful Konvention, Eintrag-ID als Query-Parameter optional (NULL = erste Eintrag). Response mit DtoPlaylistPlaybackStart (Playlist-Name, Position, Start-Titel). |
| **Auto-Advance-Mechanik** | VideoPlayer.razor erkennt Video-Ende, ruft PlaylistsController.AdvancePlaylistAsync auf | Entkopplung: Video-Player signalisiert Ende (bestehendes Event oder neuer EventHandler), Client-Komponente reagiert und fordert nächsten Titel an. Verhindert Polling. |
| **Wiedergabe-Kontext bei nicht-Playlist-Start** | Komponenten-Parameter null oder fehlende Query-Parameter | Wenn Video außerhalb einer Playlist gestartet wird (z. B. aus Medienbibliothek), wird der Kontext nicht gesetzt. Bisheriges Verhalten bleibt unverändert. |
| **Sortierung & Freischaltung** | Wiederverwendung bestehender Methoden | PlaylistService.SortPlaylistEntriesForModeAsync und PlaylistEntryAccessResolver.ResolveAccessibilityAsync werden unverändert eingesetzt. Keine Duplikation. |
| **Kontext-Transport zwischen Komponenten** | URL-Query-Parameter und/oder Komponenten-Cascade-Parameter | Bei Initialload der VideoPlayer.razor aus PlaylistDetail.razor: Kontext wird entweder über Navigations-URL mit Query-Parametern (?playlistId=X&entryId=Y) oder über Cascade-Parameter übergeben. VideoPlayer speichert Kontext lokal in Komponentenfeldern. Bei Next/Previous/Auto-Advance: Komponente aktualisiert Kontext lokal (kein Server-Roundtrip für Kontext-Speicherung erforderlich). |

---

## Programmabläufe

### Wiedergabe aus Playlist starten

1. Benutzer doppelklickt auf Eintrag in PlaylistDetail.razor oder klickt Play-Button
2. PlaylistDetail.razor ruft `PlaylistsController.StartPlaylistAsync(playlistId, entryId)` auf (HTTP POST /api/playlists/{id}/play?entryId={entryId})
3. Controller ruft `PlaylistService.StartPlaylistAsync(playlistId, currentUserId, entryId?)`
4. StartPlaylistAsync:
   - Prüft Berechtigung (nur Playlist-Besitzer oder freigegeben)
   - Lädt Playlist und Eintrag (falls entryId vorgegeben)
   - Falls entryId NULL: Nutzt ersten abzuspielenden Eintrag (erste sortierte, freigeschaltete Eintrag)
   - Gibt DtoPlaylistPlaybackStart (Playlist-Name, Position z. B. "3/12", Start-Titel mit StreamUrl, AuthToken, PlaylistId, CurrentEntryId) zurück
5. PlaylistDetail.razor erhält Response und navigiert zu VideoPlayer.razor mit Kontext-Informationen:
   - Via URL-Query-Parameter: `?playlistId=X&entryId=Y` oder
   - Via Cascade-Parameter an die Komponente (wenn direkter Komponentenaufruf)
6. VideoPlayer.razor bindet Kontext-Daten (playlistId, currentEntryId, totalCount) an Komponentenfelder, zeigt Playlist-Badge "[Playlist-Name: 3/12]" und Next/Previous Buttons an

Beteiligte Klassen/Komponenten: `PlaylistDetail.razor`, `PlaylistsController`, `PlaylistService`, `VideoPlayer.razor`

### Manuelles Weiterschalten (Nächster Titel)

1. Benutzer klickt "Nächster (Playlist)" Button in VideoPlayer.razor
2. VideoPlayer.razor ruft lokal eine Methode `OnNextPlaylistEntryAsync()` auf, die:
   - Die aktuellen lokalen Kontext-Werte (playlistId, currentEntryId) erfasst
   - `PlaylistsController.GetNextPlaylistEntry(playlistId, currentEntryId)` HTTP-Request durchführt
3. Controller ruft `PlaylistService.GetNextPlaylistEntryAsync(playlistId, currentEntryId, currentUserId)`
4. GetNextPlaylistEntryAsync:
   - Lädt sortierte Playlist-Einträge via `GetPlaylistEntriesAsync(sortiert nach aktuellem Sortiermodus)`
   - Findet aktuelle Position
   - Durchläuft nachfolgende Einträge
   - Überspringt nicht-abspielbare Typen (TVShow, TVShowSeason, MovieCollection)
   - Überspringt nicht-freigeschaltete Einträge (via bestehende Freischaltungsprüfung)
   - Gibt nächsten abzuspielenden Eintrag zurück, oder NULL wenn Ende erreicht
5. VideoPlayer.razor erhält Response, aktualisiert lokal:
   - Komponentenfelder: playlistId, currentEntryId (aktualisiert von Response), position (neu berechnet)
   - Lädt neue Titel (StreamUrl, MediaId, AuthToken) und startet Wiedergabe
   - Playlist-Badge wird aktualisiert (z. B. "3/12" → "4/12")
6. URL wird optional aktualisiert mit neuen Query-Parametern (?playlistId=X&entryId=Y-neu)

Beteiligte Klassen/Komponenten: `VideoPlayer.razor`, `PlaylistsController`, `PlaylistService`

### Manuelles Weiterschalten (Vorheriger Titel)

1. Benutzer klickt "Vorheriger (Playlist)" Button in VideoPlayer.razor
2. Ablauf analog zu "Nächster", aber:
3. Controller ruft `PlaylistService.GetPreviousPlaylistEntryAsync(playlistId, currentEntryId, currentUserId)`
4. GetPreviousPlaylistEntryAsync:
   - Lädt sortierte Playlist-Einträge
   - Findet aktuelle Position
   - Durchläuft vorhergehende Einträge (in umgekehrter Reihenfolge)
   - Überspringt nicht-abspielbare und nicht-freigeschaltete Einträge
   - Gibt vorherigen Eintrag zurück, oder NULL wenn am Anfang

Beteiligte Klassen/Komponenten: `VideoPlayer.razor`, `PlaylistsController`, `PlaylistService`

### Automatisches Weiterschalten (Titel-Ende)

1. VideoPlayer.razor erkennt Titel-Ende (über bestehende HTML5 Video-Events `ended` oder neuer EventHandler `onMediaEnd`)
2. VideoPlayer.razor ruft lokal eine Methode `OnMediaEndAsync()` auf, die:
   - Die aktuellen lokalen Kontext-Werte erfasst
   - `PlaylistsController.AdvancePlaylistAsync(playlistId, currentEntryId)` HTTP-Request durchführt
3. Controller ruft `PlaylistService.AdvancePlaylistAsync(playlistId, currentEntryId, currentUserId)`
4. AdvancePlaylistAsync:
   - Ruft `GetNextPlaylistEntryAsync(playlistId, currentEntryId)` auf (intern)
   - Gibt nächsten Eintrag zurück oder NULL wenn am Ende
5. VideoPlayer.razor erhält Response:
   - Falls nächster Eintrag vorhanden: Aktualisiert Komponentenfelder, lädt neuen Titel, startet Wiedergabe automatisch
   - Falls NULL (Ende): Stoppt Wiedergabe, zeigt "Ende der Playlist" an oder Schaltfläche "Neu starten"
6. Playlist-Badge wird aktualisiert

Beteiligte Klassen/Komponenten: `VideoPlayer.razor`, `PlaylistsController`, `PlaylistService`

### Wiedergabe außerhalb einer Playlist

1. Benutzer navigiert zu Video (z. B. aus Medienbibliothek, Seite mit Suchergebnissen)
2. VideoPlayer.razor wird ohne Playlist-Kontext-Parameter gestartet (playlistId = null)
3. VideoPlayer.razor:
   - Zeigt Playlist-Badge NICHT an (da Kontext null)
   - Zeigt nur globale Schaltflächen (nicht Playlist-spezifisch)
   - Ablauf entspricht bestehendem Verhalten (keine Änderungen)

Beteiligte Klassen/Komponenten: `VideoPlayer.razor`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `DtoPlaylistPlaybackStart` | DTO | Response-DTO für Playlist-Wiedergabe-Start; enthält Playlist-Name, Gesamt-Eintrag-Anzahl, aktuelle Position, Start-Titel (mit StreamUrl, MediaType, MediaId, AuthToken, StartPosition), PlaylistId, CurrentEntryId |
| `PlaylistPlaybackContext` | Value Object | Client-seitige Repräsentation des Playlist-Kontextes (PlaylistId, CurrentEntryId, TotalCount, PlaylistName); wird in VideoPlayer.razor-Komponente gespeichert; keine Persistierung auf dem Server |

---

## Änderungen an bestehenden Klassen

### `PlaylistService` (Klasse)

- **Neue Methoden:**
  - `GetNextPlaylistEntryAsync(playlistId, currentEntryId, userId, cancellationToken)` → `Task<PlaylistEntry?>` — Liefert nächsten abzuspielenden Eintrag; überspringt nicht-abspielbare und nicht-freigeschaltete Einträge; gibt NULL zurück wenn Ende erreicht
  - `GetPreviousPlaylistEntryAsync(playlistId, currentEntryId, userId, cancellationToken)` → `Task<PlaylistEntry?>` — Liefert vorherigen abzuspielenden Eintrag; Gegenstück zu GetNext
  - `StartPlaylistAsync(playlistId, userId, entryId?, cancellationToken)` → `Task<DtoPlaylistPlaybackStart>` — Initiiert Wiedergabe einer Playlist; prüft Berechtigung, gibt Start-Titel und Kontextinformationen zurück
  - `AdvancePlaylistAsync(playlistId, currentEntryId, userId, cancellationToken)` → `Task<PlaylistEntry?>` — Wird aufgerufen wenn Titel automatisch zu Ende geht; gibt nächsten Eintrag oder NULL

- **Änderungen an bestehenden Methoden:** Keine. GetPlaylistEntriesAsync wird intern von neuen Methoden genutzt.

### `PlaylistsController` (Klasse)

- **Neue Endpunkte:**
  - `POST /api/playlists/{id}/play` mit optionalem Query-Parameter `?entryId={entryId}` — Ruft `PlaylistService.StartPlaylistAsync` auf; Response: `DtoPlaylistPlaybackStart`
  - `POST /api/playlists/{id}/play/next` — Ruft `PlaylistService.GetNextPlaylistEntryAsync` auf; Response: `DtoPlaylistEntry` oder 204 No Content wenn am Ende
  - `POST /api/playlists/{id}/play/previous` — Ruft `PlaylistService.GetPreviousPlaylistEntryAsync` auf; Response: `DtoPlaylistEntry` oder 204 No Content wenn am Anfang
  - `POST /api/playlists/{id}/play/advance` — Ruft `PlaylistService.AdvancePlaylistAsync` auf (für Auto-Advance); Response: `DtoPlaylistEntry` oder 204 No Content

- **Neue Methoden (Private):** Keine neuen privaten Hilfsmethoden erforderlich (bestehende Exception-Handling Infrastruktur wird genutzt)

### `VideoPlayer.razor` (Razor-Komponente)

- **Neue Parameter:**
  - `PlaylistId` (long?, optional) — ID der aktuellen Playlist (null wenn nicht aus Playlist gespielt wird)
  - `CurrentPlaylistEntryId` (long?, optional) — ID des aktuellen Eintrags in der Playlist
  - `PlaylistName` (string?, optional) — Name der Playlist für Badge-Anzeige
  - `PlaylistTotalCount` (int?, optional) — Gesamtanzahl abzuspielender Einträge in der Playlist

- **Neue Komponentenfelder (lokaler Zustand):**
  - `private long? currentPlaylistId` — Lokale Kopie der PlaylistId
  - `private long? currentPlaylistEntryId` — Lokale Kopie der CurrentPlaylistEntryId
  - `private int? playlistTotalCount` — Lokale Kopie der TotalCount
  - `private string currentPlaylistName` — Lokale Kopie des Playlist-Namens
  - `private int currentPlaylistPosition` — Berechnet aus aktueller Position (z. B. 3 bei 12 insgesamt)

- **Neue Methoden (Private/Async):**
  - `async Task OnNextPlaylistEntryAsync()` — Ruft Controller.GetNext auf, aktualisiert Kontext lokal, lädt nächsten Titel
  - `async Task OnPreviousPlaylistEntryAsync()` — Ruft Controller.GetPrevious auf, aktualisiert Kontext lokal, lädt vorherigen Titel
  - `async Task OnMediaEndAsync()` — Ruft Controller.Advance auf, prüft auf nächsten Titel, startet Auto-Play oder stoppt

- **Neue Anzeige-Elemente:**
  - Playlist-Badge: "[Playlist-Name: Position/Gesamt]" z. B. "[Meine Favoriten: 3/12]" (nur sichtbar wenn PlaylistId != null)
  - Buttons "Nächster (Playlist)" und "Vorheriger (Playlist)" neben oder statt globalen Schaltflächen (nur sichtbar wenn PlaylistId != null)

- **Neue Event-Handler:**
  - Erkennungscode für Video-Ende-Event (bestehende HTML5 Video-Events wie `ended`)
  - Bei Titel-Ende: `OnMediaEndAsync()` aufrufen (falls PlaylistId vorhanden)

- **Geänderte Methoden:** 
  - `OnParametersSetAsync()` — Bindet übergebene Parameter an lokale Felder, berechnet Position, zerlegt URL-Query-Parameter falls vorhanden

- **Geänderte Logik bei Medienwechsel:**
  - Wenn neue StreamUrl geladen wird: Lokale Kontext-Felder werden aktualisiert
  - URL-Query-Parameter werden ggf. aktualisiert für Browser-Reload-Fähigkeit

### `PlaylistDetail.razor` (Razor-Komponente)

- **Neue Aktion:**
  - Play-Button auf Einträgen (oder Doppelklick-Handler) zur Wiedergabe-Start aus dieser Position
  - Ruft `PlaylistsController.StartPlaylistAsync(playlistId, entryId)` auf
  - Navigiert zu `VideoPlayer.razor` mit Response-Daten und Kontext via URL-Query-Parametern oder Komponenten-Parametern

- **Neue Anzeige:**
  - Optionale Markierung des aktuell abgespielten Eintrags (nicht persistent, nur visuell während dieser Browsersession)

### `IPlaylistService` (Interface)

- **Neue Methoden-Signaturen hinzufügen** (entsprechend zu PlaylistService, siehe oben):
  - `GetNextPlaylistEntryAsync(...)`
  - `GetPreviousPlaylistEntryAsync(...)`
  - `StartPlaylistAsync(...)`
  - `AdvancePlaylistAsync(...)`

---

## Datenbankmigrationen

Keine. Der Playlist-Kontext wird rein client-seitig verwaltet (VideoPlayer.razor Komponente und URL-Query-Parameter), nicht persistiert. Keine neue Spalte erforderlich.

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `StartPlaylistAsync`: `entryId` Parameter | Falls vorhanden: Muss zur Playlist gehören und vom aktuellen Benutzer freigeschaltet sein | 404 Not Found oder 403 Forbidden |
| `GetNextPlaylistEntryAsync`: `currentEntryId` | Muss zur Playlist gehören und vom aktuellen Benutzer freigeschaltet sein | 400 Bad Request oder 403 Forbidden |
| `GetPreviousPlaylistEntryAsync`: `currentEntryId` | Muss zur Playlist gehören und vom aktuellen Benutzer freigeschaltet sein | 400 Bad Request oder 403 Forbidden |
| Alle neuen Endpunkte: `playlistId` | Playlist muss existieren und gehört zum aktuellen Benutzer | 404 Not Found |
| `VideoPlayer.razor`: `PlaylistId` Parameter | Wenn vorhanden, müssen alle Kontext-Parameter (CurrentPlaylistEntryId, PlaylistName, PlaylistTotalCount) konsistent sein | Komponenten-Logging und ggf. Fallback auf null-Kontext |

---

## Konfigurationsänderungen

Keine. Die Auto-Advance-Verzögerung kann optional über appsettings.json konfiguriert werden (bestehend aus Schritt 3, falls bereits vorhanden):

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Playlists:PlaybackAutoAdvanceDelayMs` | int (optional) | 500 | Verzögerung (ms) zwischen Video-Ende-Erkennung und Auto-Advance-Start (verhindert Race Conditions) |
| `Playlists:SkipUnlockedEntries` | bool (optional) | true | Steuert, ob nicht-freigeschaltete Einträge automatisch übersprungen werden (true) oder Wiedergabe stoppt (false) |

Falls bereits in Schritt 3 konfiguriert: Keine neuen Einträge erforderlich.

---

## Seiteneffekte und Risiken

- **VideoPlayer.razor Komponentenschnittstelle:** Die neuen optionalen Parameter ändern nicht die Signatur für bestehende Aufrufer (neue Parameter sind optional), aber Implementierungen, die VideoPlayer referenzieren, müssen auf neue Events/Methoden achten. **Risiko niedrig** (neue Parameter sind optional).

- **PlaylistsController Response-Typen:** Neue Endpunkte verwenden neue DTOs. Bestehende Endpunkte unchanged. **Risiko niedrig**.

- **PlaylistService Abhängigkeiten:** GetNext/GetPrevious nutzen bestehende Sortierreihenfolgen- und Freischaltungsprüfungs-Logik. Keine Änderungen an bestehenden Methoden. **Risiko niedrig**.

- **Keine server-seitige Kontext-Persistierung:** Im Gegensatz zum ursprünglichen Plan: Es gibt keinen `IPlaylistContextProvider` Service, der Kontext zwischen Requests speichert. Das ist gewollt und eliminiert die architektonische Fehlkonstruktion. Client muss selbst Kontext verwalten (Browser SessionStorage oder URL-Parameter). **Kein Risiko** — das ist der richtige Ansatz für ASP.NET Core HTTP-API + Blazor Client.

- **Medienbibliothek und andere nicht-Playlist-Videos:** Müssen VideoPlayer ohne Playlist-Kontext-Parameter aufrufen (playlistId = null). Bestehende Einzeltitel-Wiedergabe läuft unverändert ab. **Risiko niedrig** — Dokumentation erforderlich.

- **URL-Query-Parameter-Handling:** Wenn VideoPlayer via URL-Query-Parametern aufgerufen wird, müssen Parameter korrekt aus `NavigationManager.Uri` extrahiert werden. Browser-Reload sollte Kontext bewahren. **Risiko mittel** — Query-Parameter-Parsing muss sorgfältig implementiert werden, Tests erforderlich.

---

## Umsetzungsreihenfolge

### Phase 1: Interfaces und DTOs

1. **`PlaylistPlaybackContext` Value Object anlegen**
   - Voraussetzungen: Keine
   - Beschreibung: Neue Klasse in `VideoWebPlayer/Models/` oder `VideoWebPlayer/Data/`; Properties: `PlaylistId` (long), `CurrentEntryId` (long), `TotalCount` (int), `PlaylistName` (string). Immutable Value Object (Record oder Properties mit nur Getter).

2. **`DtoPlaylistPlaybackStart` DTO anlegen**
   - Voraussetzungen: Keine
   - Beschreibung: Neue Klasse in `VideoWebPlayer.Client/Models/` oder `VideoWebPlayer/Services/DTOs/`; Properties: `PlaylistId` (long), `PlaylistName` (string), `TotalCount` (int), `CurrentPosition` (int), `CurrentEntryId` (long), `CurrentEntry` (DtoPlaylistEntry), `StreamUrl` (string), `MediaType` (string), `MediaId` (long), `AuthToken` (string), `StartPosition` (int?).

### Phase 2: PlaylistService Erweiterung

3. **`PlaylistService.GetNextPlaylistEntryAsync` implementieren**
   - Voraussetzungen: PlaylistService bestehend; `GetPlaylistEntriesAsync` arbeitet korrekt; `PlaylistEntryAccessResolver` vorhanden; `MediaHierarchyRegistry` vorhanden
   - Beschreibung: Neue öffentliche async Methode. Lädt sortierte Einträge via `GetPlaylistEntriesAsync`, findet aktuelle Position, durchläuft nachfolgende, überspringt nicht-abspielbare Typen (prüft via MediaHierarchyRegistry.Handlers[type].IsPlayable) und nicht-freigeschaltete Einträge (via PlaylistEntryAccessResolver), gibt nächsten zurück oder NULL.

4. **`PlaylistService.GetPreviousPlaylistEntryAsync` implementieren**
   - Voraussetzungen: `GetNextPlaylistEntryAsync` bereits implementiert (gleiche Struktur)
   - Beschreibung: Analog zu GetNext, aber rückwärts-Durchlauf.

5. **`PlaylistService.StartPlaylistAsync` implementieren**
   - Voraussetzungen: `GetNextPlaylistEntryAsync` vorhanden
   - Beschreibung: Prüft Berechtigung (Playlist.UserId == userId), validiert Eintrag (falls vorhanden), findet ersten abzuspielenden Eintrag (falls entryId NULL), gibt DtoPlaylistPlaybackStart zurück mit allen Kontextinformationen.

6. **`PlaylistService.AdvancePlaylistAsync` implementieren**
   - Voraussetzungen: `GetNextPlaylistEntryAsync` vorhanden
   - Beschreibung: Ruft GetNext auf, gibt Ergebnis zurück. Wrapper für Auto-Advance-Flow.

7. **`IPlaylistService` Interface aktualisieren**
   - Voraussetzungen: Alle neuen Methoden in PlaylistService vorhanden
   - Beschreibung: Füge neue Methoden-Signaturen zum Interface hinzu.

### Phase 3: PlaylistsController Erweiterung

8. **PlaylistsController neue Endpunkte implementieren**
   - Voraussetzungen: PlaylistService Methoden vorhanden; Controller bestehende Exception-Handling Infrastruktur vorhanden
   - Beschreibung: Vier neue Endpunkte hinzufügen:
     - `POST /api/playlists/{id}/play?entryId={entryId}` → `StartPlaylistAsync`
     - `POST /api/playlists/{id}/play/next` → `GetNextPlaylistEntryAsync`
     - `POST /api/playlists/{id}/play/previous` → `GetPreviousPlaylistEntryAsync`
     - `POST /api/playlists/{id}/play/advance` → `AdvancePlaylistAsync`
   - Jeweils mit bestehender Authentifizierung (CheckLogedIn), Exception-Handling via ExecuteAsync, Berechtigungs-Validierung.

### Phase 4: VideoPlayer.razor Erweiterung

9. **`VideoPlayer.razor` Parameter und Komponentenzustand erweitern**
   - Voraussetzungen: `PlaylistPlaybackContext` DTO vorhanden; Komponente bestehend
   - Beschreibung: Neue Parameter hinzufügen: `PlaylistId`, `CurrentPlaylistEntryId`, `PlaylistName`, `PlaylistTotalCount`. Neue Komponentenfelder für lokalen Zustand: `currentPlaylistId`, `currentPlaylistEntryId`, `playlistTotalCount`, `currentPlaylistName`, `currentPlaylistPosition`. `OnParametersSetAsync()` zum Binding und Query-Parameter-Parsing hinzufügen.

10. **Playlist-Badge-Anzeige in VideoPlayer.razor implementieren**
    - Voraussetzungen: Parameter und Felder vorhanden
    - Beschreibung: Neuer HTML-Block im Player (bedingt via `@if (PlaylistId.HasValue && !string.IsNullOrEmpty(PlaylistName))`) zeigt "[Playlist-Name: Position/Gesamt]". Styling: Badge neben oder über Video-Controls.

11. **Next/Previous Buttons und Auto-Advance-Handling in VideoPlayer.razor**
    - Voraussetzungen: Parameter vorhanden; private Methoden OnNextPlaylistEntryAsync, OnPreviousPlaylistEntryAsync, OnMediaEndAsync implementiert
    - Beschreibung: 
      - Zwei neue Buttons (neben oder statt globaler Navigation) mit Click-Handler → `OnNextPlaylistEntryAsync()` / `OnPreviousPlaylistEntryAsync()`. Nur sichtbar wenn PlaylistId.HasValue.
      - Video-Ende-Erkennung: HTML5 `@onended` Event oder JavaScript-Integration zum Auslösen von `OnMediaEndAsync()`.
      - Methoden-Implementierung: HTTP-Aufrufe an neue Controller-Endpunkte, lokale Kontext-Aktualisierung, Medienwechsel-Trigger.

### Phase 5: PlaylistDetail.razor Erweiterung

12. **Play-Button auf PlaylistEntry hinzufügen**
    - Voraussetzungen: PlaylistDetail.razor bestehend; PlaylistsController neue Endpunkte vorhanden
    - Beschreibung: Neuer Button (oder Doppelklick-Handler) auf jedem Eintrag in der Einträge-Liste. Click → `PlaylistsController.StartPlaylistAsync(playlistId, entryId)` aufrufen.

13. **Navigation zu VideoPlayer.razor mit Kontext**
    - Voraussetzungen: VideoPlayer.razor erweitert; PlaylistsController.StartPlaylistAsync vorhanden
    - Beschreibung: Nach Aufruf von StartPlaylistAsync: Response verarbeiten, `DtoPlaylistPlaybackStart` parsen, zu VideoPlayer.razor navigieren mit Query-Parametern (?playlistId=X&entryId=Y) oder Komponenten-Parametern. NavigationManager.NavigateTo(...) mit Query-String oder Cascade-Parameter via Router-Config.

### Phase 6: Tests

14. **Unit-Tests für `PlaylistService.GetNextPlaylistEntryAsync`**
    - Voraussetzungen: Methode vorhanden; bestehende PlaylistServiceTestBase vorhanden
    - Beschreibung: Neue Testklasse oder Tests in bestehender Klasse: 
      - ByReleaseDate-Sortierung: Nächster, Überspringen nicht-abspielbar, Überspringen nicht-freigeschaltet, NULL am Ende
      - Manual-Sortierung: Nächster, Überspringen
      - Sammel-Einträge-Überspringlogik prüfen (TVShow, TVShowSeason, MovieCollection übersprungen, TVShowEpisode/Movie nicht)

15. **Unit-Tests für `PlaylistService.GetPreviousPlaylistEntryAsync`**
    - Voraussetzungen: GetPreviousPlaylistEntryAsync vorhanden
    - Beschreibung: Analog zu GetNext, aber für vorherige Navigation, NULL am Anfang.

16. **Unit-Tests für `PlaylistService.StartPlaylistAsync`**
    - Voraussetzungen: Methode vorhanden; PlaylistServiceTestBase vorhanden
    - Beschreibung: Berechtigungsprüfung (403 wenn nicht Besitzer), Eintrag-Validierung (404 wenn nicht vorhanden), Fallback auf erste Eintrag (entryId NULL), Response-Daten (PlaylistName, Position, StreamUrl).

17. **Unit-Tests für `PlaylistService.AdvancePlaylistAsync`**
    - Voraussetzungen: Methode vorhanden
    - Beschreibung: Auto-Advance-Aufruf, Next-Eintrag Rückgabe, NULL am Ende.

18. **Controller-Tests für neue Endpunkte**
    - Voraussetzungen: PlaylistsControllerTests_* Struktur vorhanden; neue Controller-Methoden implementiert
    - Beschreibung: Neue Testdatei oder Tests in bestehenden Dateien für neue Endpunkte: /play, /play/next, /play/previous, /play/advance. Test-Cases: Authentifizierung (401), Autorisierung (403), Happy Path, Endverhalten, Fehler (404, 400).

19. **E2E-Tests für Playlist-Wiedergabe**
    - Voraussetzungen: PlaylistsE2ETestBase vorhanden; Blazor-Test-Infrastruktur vorhanden
    - Beschreibung: Neue E2E-Tests (siehe Test-Plan unten):
      - Wiedergabe aus Playlist starten (Happy Path)
      - Manuelles Weiterschalten (Next/Previous)
      - Automatisches Weiterschalten (Video-Ende)
      - Überspringen nicht-freigeschalteter Titel
      - Wiedergabe außerhalb einer Playlist
      - Kontext-Anzeige im Video-Player
      - Browser-Reload: Kontext bleibt erhalten (Query-Parameter-Handling)

### Phase 7: Dokumentation

20. **`docs/help/playlists.md` aktualisieren**
    - Voraussetzungen: Datei vorhanden; alle Features implementiert
    - Beschreibung: Abschnitt hinzufügen über Playlist-Wiedergabe: Wiedergabe starten, Navigation, Auto-Advance, Playlist-Badge-Anzeige, nicht-abspielbare Einträge (Sammlungen).

21. **`docs/help/playlists-api.md` aktualisieren**
    - Voraussetzungen: Datei vorhanden; neue Controller-Endpunkte implementiert
    - Beschreibung: Neue Endpunkte dokumentieren (/play, /play/next, /play/previous, /play/advance): Request-Format, Response-DTOs, Fehler-Codes, Beispiele, Query-Parameter-Dokumentation.

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetNextPlaylistEntryAsync_ByReleaseDateMode_ReturnsNextUnlockedEntry` | `PlaylistServiceTests_Playback` (neu) | Nächster Eintrag nach aktuellem in Release-Date-Sortierung, nur freigeschaltete |
| `GetNextPlaylistEntryAsync_ManualMode_ReturnsNextUnlockedEntry` | `PlaylistServiceTests_Playback` | Nächster Eintrag in manueller Sortierung |
| `GetNextPlaylistEntryAsync_SkipsCollectionEntries` | `PlaylistServiceTests_Playback` | TVShow/TVShowSeason/MovieCollection werden übersprungen |
| `GetNextPlaylistEntryAsync_SkipsLockedEntries` | `PlaylistServiceTests_Playback` | Nicht-freigeschaltete Einträge werden übersprungen |
| `GetNextPlaylistEntryAsync_ReturnsNullAtEnd` | `PlaylistServiceTests_Playback` | NULL zurückgegeben wenn am Ende der Playlist |
| `GetPreviousPlaylistEntryAsync_ReturnsPreviousUnlockedEntry` | `PlaylistServiceTests_Playback` | Vorheriger Eintrag, ähnliche Übersprung-Logik |
| `GetPreviousPlaylistEntryAsync_ReturnsNullAtBeginning` | `PlaylistServiceTests_Playback` | NULL wenn am Anfang |
| `StartPlaylistAsync_ValidatesPlaylistOwnership` | `PlaylistServiceTests_Playback` | Nur Besitzer kann Wiedergabe starten |
| `StartPlaylistAsync_ValidatesEntryBelongsToPlaylist` | `PlaylistServiceTests_Playback` | EntryId muss zur Playlist gehören |
| `StartPlaylistAsync_StartsAtFirstIfNoEntryIdProvided` | `PlaylistServiceTests_Playback` | Wenn entryId NULL, verwende erste (sortierte, freigeschaltete) Eintrag |
| `StartPlaylistAsync_ReturnsPlaybackStartDTO` | `PlaylistServiceTests_Playback` | DtoPlaylistPlaybackStart mit Playlist-Name, Position, Start-Titel, PlaylistId, CurrentEntryId |
| `AdvancePlaylistAsync_CallsGetNextInternally` | `PlaylistServiceTests_Playback` | AdvancePlaylistAsync delegiert zu GetNextPlaylistEntryAsync |
| `AdvancePlaylistAsync_ReturnsNextEntryOrNull` | `PlaylistServiceTests_Playback` | Nächster Eintrag oder NULL |
| `PlayEndpoint_StartsPlaylistWithValidEntry` | `PlaylistsControllerTests_Playback` (neu) | POST /api/playlists/{id}/play?entryId={entryId} funktioniert |
| `PlayEndpoint_RequiresAuthentication` | `PlaylistsControllerTests_Playback` | 401 wenn nicht authentifiziert |
| `PlayEndpoint_RequiresOwnership` | `PlaylistsControllerTests_Playback` | 403 wenn nicht Besitzer |
| `NextEndpoint_ReturnsNextEntry` | `PlaylistsControllerTests_Playback` | POST /api/playlists/{id}/play/next funktioniert |
| `NextEndpoint_Returns204AtEnd` | `PlaylistsControllerTests_Playback` | 204 No Content wenn am Ende |
| `PreviousEndpoint_ReturnsPreviousEntry` | `PlaylistsControllerTests_Playback` | POST /api/playlists/{id}/play/previous funktioniert |
| `PreviousEndpoint_Returns204AtBeginning` | `PlaylistsControllerTests_Playback` | 204 No Content wenn am Anfang |
| `AdvanceEndpoint_TriggersAutoAdvance` | `PlaylistsControllerTests_Playback` | POST /api/playlists/{id}/play/advance für Auto-Advance |
| `CreateTestPlaylistWithMixedEntries_Fixture` (Hilfsmethode) | `PlaylistServiceTestBase` (erweitert) | Erstellt Test-Playlist mit Episoden, Sammel-Einträgen, unterschiedliche Freischaltungen |
| `CreatePlaylistWithMultipleSortOrders_Fixture` (Hilfsmethode) | `PlaylistServiceTestBase` (erweitert) | Erstellt Playlists mit ByReleaseDate und Manual Sortierung |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Bestehende Tests werden nicht durch neue Features beeinflusst. PlaylistService, PlaylistsController, VideoPlayer bestehende Methoden/Endpunkte bleiben unverändert. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| **Pflicht** | Wiedergabe aus Playlist starten: Benutzer klickt Play-Button auf Eintrag in PlaylistDetail, Video-Player öffnet sich mit Playlist-Badge, korrekter Titel wird abgespielt, Kontext wird angezeigt (z. B. "Meine Favoriten: 3/12") | `PlaylistPlaybackE2ETests` (neu) / Szenario "PlaylistPlaybackStartTest" | Af-001: Benutzer kann Wiedergabe aus Playlist starten | UI-Interaction + Server-Ablauf + Video-Player Zustand + Kontextanzeige können nicht rein mit Unit-Tests validiert werden |
| **Pflicht** | Manuelles Weiterschalten (Next): Benutzer klickt "Nächster (Playlist)" Button im Video-Player, nächster freigeschalteter Titel wird abgespielt, Badge wird aktualisiert (z. B. 3/12 → 4/12), URL-Query-Parameter werden aktualisiert | `PlaylistPlaybackE2ETests` / "PlaylistNextEntryE2ETest" | Af-002: Benutzer kann zum nächsten Titel navigieren | Button-Klick + Controller-Aufruf + UI-Update + Client-State-Synchronisierung erfordern E2E |
| **Pflicht** | Manuelles Weiterschalten (Previous): Analog zu Next, aber rückwärts | `PlaylistPlaybackE2ETests` / "PlaylistPreviousEntryE2ETest" | Af-003: Benutzer kann zum vorherigen Titel navigieren | Analog zu Next |
| **Pflicht** | Automatisches Weiterschalten: Video läuft bis zum Ende, nächster Titel wird automatisch abgespielt (keine Klick erforderlich), Playlist-Badge aktualisiert sich | `PlaylistPlaybackE2ETests` / "PlaylistAutoAdvanceE2ETest" | Af-004: Video-Ende triggert automatisches Weiterschalten | Timing + Video-Events + Auto-Advance + Client-State erfordern E2E + Browser-Kontext |
| **Pflicht** | Endeverhalten: Benutzer navigiert zum letzten Titel, klickt Next/startet Auto-Advance, Wiedergabe stoppt ohne Fehler, "Ende erreicht" Meldung angezeigt, kein Crash | `PlaylistPlaybackE2ETests` / "PlaylistEndBehaviorE2ETest" | Af-005: Playlist-Ende wird ordnungsgemäß gehandhabt | Fehlerbehandlung + UI-State-Wechsel erfordern E2E |
| **Pflicht** | Übersprung nicht-freigeschalteter Titel: Playlist enthält Mix von freigeschalteten und gesperrten Titeln, Navigation überspring gesperrte, nur freigeschaltete Titel werden abgespielt | `PlaylistPlaybackE2ETests` / "PlaylistSkipLockedEntriesE2ETest" | Af-006: Nicht-freigeschaltete Titel werden übersprungen | Freischaltungs-Status + Navigation-Logik erfordern end-to-end Test |
| **Pflicht** | Playlist-Badge-Anzeige: Während Wiedergabe aus Playlist zeigt Video-Player Badge "[Playlist-Name: Position/Gesamt]" an, mit aktueller Position aktualisiert | `PlaylistPlaybackE2ETests` / "PlaylistBadgeDisplayE2ETest" | Af-007: Playlist-Kontext ist im Video-Player sichtbar | UI-Rendering + Daten-Propagation erfordern E2E |
| **Pflicht** | Wiedergabe außerhalb einer Playlist: Benutzer startet Video aus Medienbibliothek (nicht aus Playlist), kein Badge angezeigt, keine Playlist-Navigation, normales Verhalten | `PlaylistPlaybackE2ETests` / "VideoPlaybackOutsidePlaylistE2ETest" | Af-008: Nicht-Playlist-Videos zeigen keine Playlist-UI | Fehlerfall + Feature-Isolation erfordern E2E |
| **Empfohlen** | Sammel-Einträge-Übersprung: Playlist enthält TVShow oder TVShowSeason direkt, diese werden bei Navigation übersprungen, nur Episoden/Filme werden abgespielt | `PlaylistPlaybackE2ETests` / "PlaylistSkipCollectionEntriesE2ETest" | Af-009: Nicht abspielbare Medientypen werden übersprungen | Spezialfalls-Verhalten + Kaskadenauflösung erfordern Validierung |
| **Empfohlen** | Browser-Reload während Playlist-Wiedergabe: Benutzer lädt Seite neu (F5), Kontext wird aus URL-Query-Parametern rekonstruiert, Wiedergabe setzt an gleicher Stelle fort | `PlaylistPlaybackE2ETests` / "PlaylistReloadContextRecoveryE2ETest" | Af-010: Client-seitiger Zustand ist reload-resilient | URL-Parameter-Handling + Browser-Reload-Behavior erfordern E2E |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistDetailE2ETests` | Optional: Play-Button ist in PlaylistDetail sichtbar. Besteht Test dafür, dass Einträge angezeigt werden, aber kein Test für Play-Button-Interaktion. Optional: Test hinzufügen für Play-Button-Klick und Navigation zu VideoPlayer. |
| `PlaylistEntriesE2ETests` | Keine Anpassung erforderlich; bestehende Tests für Einträge-Anzeige sind unverändert. |

---

## Offene Punkte

Keine. Alle Designentscheidungen wurden durch die übergebenen Leitplanken und die Analyse der Architektur geklärt:

1. **Playlist-Kontext-Verwaltung:** ✅ Rein client-seitig (VideoPlayer.razor Komponente + URL-Query-Parameter), nicht serverseitiger Session-Store
2. **Server-Endpunkte:** ✅ Zustandslos, alle Parameter vom Client übergeben
3. **Sammel-Einträge-Behandlung:** ✅ Übersprung bei Navigation
4. **Sortierreihenfolge-Wiederverwendung:** ✅ `SortPlaylistEntriesForModeAsync` ohne Änderung
5. **Freischaltungsprüfung-Wiederverwendung:** ✅ `PlaylistEntryAccessResolver.ResolveAccessibilityAsync` ohne Änderung
6. **Wiedergabe außerhalb Playlist:** ✅ Bisheriges Verhalten unverändert (VideoPlayer ohne Kontext-Parameter)
7. **Kontext-Transport:** ✅ Via URL-Query-Parameter (?playlistId=X&entryId=Y) oder Komponenten-Cascade-Parameter
8. **IPlaylistContextProvider als Scoped Service:** ✅ ENTFERNT — architektonisch falsch für diesen Anwendungstyp
9. **Weiterschauen-Liste-Integration:** ✅ Nicht in Schritt 5 (separater späterer Schritt)
