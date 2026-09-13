# Umsetzungsplan: Weiterschauen mit Playlist-Bezug (Schritt 6)

## Übersicht

Das bestehende Weiterschauen-System wird um optionale Playlist-Bindung erweitert. `ContinueWatchingEntry` erhält eine neue nullable Foreign Key Eigenschaft `PlaylistId`, wodurch ein Video mehrfach in der Weiterschauen-Liste erscheinen kann — einmal pro Playlist sowie optional ohne Playlist-Kontext. Die Deduplizierungsstrategie wird von `(UserId, VideoId)` auf `(UserId, VideoId, PlaylistId)` erweitert, um mehrfaches Vorkommen desselben Videos mit unterschiedlichen Playlists zu ermöglichen. Der globale Gesehen-Status (`WatchedEntry`) bleibt playlist-übergreifend und unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Deduplizierung auf Playlist-Ebene** | Service Layer Pattern mit Extended Repository-Queries | Die bestehende Service-Logik (`RemoveExistingTVShowEntry`, `RemoveExtsingMovieCollectionEntry`) wird erweitert, um PlaylistId-Filter zu berücksichtigen. Datenbankebene (Unique Constraint) erzwingt Eindeutigkeit, Service prüft zusätzlich vor dem Speichern. |
| **PlaylistId-Übergabe durch die Architektur** | Fließt von VideoPlayer.razor → continueWatching.js → HTTP-Request → Controller → Service | PlaylistId wird aus PlaylistPlaybackContext extrahiert (falls vorhanden) und über mehrere Schichten propagiert. NULL-Wert signalisiert Non-Playlist-Wiedergabe. |
| **Playlist-Validierung** | Ownership-Check über IPlaylistService; Validierung beim Speichern in Service | Nur der Besitzer darf Einträge mit Playlist-Bezug erstellen. Validierung im Service-Layer, nicht im Controller (DDD-Prinzip: Geschäftslogik gehört in den Service). |
| **Cascade-Delete-Verhalten** | SetNull statt Cascade | Wenn Playlist gelöscht wird, bleiben Einträge bestehen (PlaylistId wird NULL), statt vollständig gelöscht zu werden. Dies ermöglicht eine robustere Fehlerbehandlung und verhindert Datenverlust. |
| **Sortierung mehrfacher Vorkommen** | ListOrder bleibt primär, mehrfache Einträge können durcheinander sortiert sein | Keine Gruppierung nach Playlist in der Default-Sortierung. UI kann später optional Gruppierung hinzufügen, ohne Datenmodell zu ändern. |

## Programmabläufe

### Fortschritts-Speicherung mit Playlist-Kontext (Happy Path)

1. Benutzer startet Video aus einer Playlist in `VideoPlayer.razor`
2. `VideoPlayer.razor` erhält `PlaylistPlaybackContext` mit `PlaylistId`
3. Bei Wiedergabefortschritt wird `UpdatePosition()` aufgerufen (Callback aus HTML5-Video)
4. `VideoPlayer.razor` ruft `continueWatching.attach()` auf und übergibt `playlistId` aus dem Context
5. JavaScript (`continueWatching.js`) registriert `timeupdate`-Events und sendet regelmäßig POST an `/api/continue-watching/progress`
6. HTTP-Request enthält: `{ mediaType, mediaId, positionSeconds, durationSeconds, playlistId }`
7. `ContinueWatchingController.PostProgress()` parst Request und ruft `ContinueWatchingService.ReportProgressAsync(userId, mediaType, mediaId, position, duration, playlistId, ct)` auf
8. `ContinueWatchingService.ReportProgressAsync()` puffert Eintrag in `ContinueWatchingBuffer` mit PlaylistId
9. `ContinueWatchingService.ProcessBufferedEntryAsync()` wird periodisch aufgerufen:
   - Prüft ob Eintrag mit `(UserId, VideoId, PlaylistId)` bereits existiert
   - Falls ja: Position/Duration aktualisieren
   - Falls nein: Neuen Eintrag erstellen
   - Ruft `RemoveExistingTVShowEntry(userId, episodeId, playlistId, ct)` oder `RemoveExtsingMovieCollectionEntry(userId, movieId, playlistId, ct)` auf
   - Diese Methoden entfernen nur Einträge mit identischer Serie/Sammlung UND identischer PlaylistId
10. Eintrag wird in Datenbank gespeichert (oder aktualisiert)
11. `MediaUpdateNotificationService.NotifyContinueWatchingUpdatedAsync()` sendet SignalR-Event an Benutzer

**Beteiligte Klassen/Komponenten:** `VideoPlayer.razor`, `continueWatching.js`, `ContinueWatchingController`, `ContinueWatchingService`, `ContinueWatchingBuffer`, `ApplicationDbContext`, `MediaUpdateNotificationService`

### Fortsetzen aus Weiterschauen-Liste mit Playlist-Rekonstruktion

1. Benutzer ruft `ContinueWatchingList.razor` auf (zeigt alle Einträge)
2. `GetListAsync(user, ct)` wird aufgerufen und ruft alle Einträge mit ihren Playlist-Informationen ab:
   - LEFT JOIN mit `Playlist`-Tabelle zur Ermittlung von `PlaylistName`
   - Laden von `PlaylistId` und `PlaylistName` in DTO
3. UI zeigt für jeden Eintrag optional Badge/Info mit Playlist-Name an
4. Benutzer klickt auf Eintrag mit PlaylistId
5. `ContinueWatchingList.razor` prüft, ob `PlaylistId` nicht NULL ist
6. Falls PlaylistId vorhanden: Ruft `IPlaylistApiClient.StartPlaylistAsync(playlistId, entryId, ct)` auf zur Rekonstruktion des `PlaylistPlaybackContext`
7. Falls kein PlaylistId: Startet `VideoPlayer.razor` ohne Playlist-Kontext (bisheriges Verhalten)
8. `VideoPlayer.razor` erhält rekonstruierten `PlaylistPlaybackContext` und folgt dann dem Happy Path Ablauf oben

**Beteiligte Klassen/Komponenten:** `ContinueWatchingList.razor`, `ContinueWatchingService`, `ApplicationDbContext`, `IPlaylistService`, `VideoPlayer.razor`, `IPlaylistApiClient`

### Hide/Skip mit Playlist-Filter

1. Benutzer klickt „Verstecken" oder „Überspringen" auf Eintrag
2. `ContinueWatchingList.razor` sendet `ContinueWatchingActionRequest` mit `mediaType`, `mediaId`, `playlistId`
3. `ContinueWatchingController.PostHide()` oder `PostSkip()` parst Request und ruft Service mit PlaylistId auf
4. `ContinueWatchingService.HideAsync(userId, mediaType, mediaId, playlistId, ct)` wird aufgerufen:
   - Löscht nur den Eintrag mit genau dieser `(UserId, VideoId, PlaylistId)`-Kombination
   - Andere Einträge mit gleicher VideoId aber unterschiedlicher PlaylistId bleiben bestehen
5. Bei Skip: Nächstes Video wird ermittelt und neuer Eintrag mit gleicher PlaylistId erstellt (falls nicht bereits vorhanden)
6. SignalR-Event wird gesendet

**Beteiligte Klassen/Komponenten:** `ContinueWatchingList.razor`, `ContinueWatchingController`, `ContinueWatchingService`, `MediaUpdateNotificationService`

### Markierung als Gesehen (Global, Playlist-Übergreifend)

1. Benutzer markiert Video als „gesehen" (über Kontext-Menü oder Gesehen-Button)
2. `WatchedStatusService.MarkAsWatchedAsync()` wird aufgerufen
3. Service erstellt `WatchedEntry` (global, nicht playlist-spezifisch)
4. `ContinueWatchingService` wird benachrichtigt (über Service-Abhängigkeit oder Listener-Pattern)
5. Service löscht ALLE `ContinueWatchingEntry`-Einträge für dieses Video und diesen Benutzer (unabhängig von PlaylistId)
   - Dies sichert, dass ein gesehenes Video in keiner Playlist mehr in Weiterschauen-Liste auftaucht

**Beteiligte Klassen/Komponenten:** `WatchedStatusService`, `ContinueWatchingService`, `ApplicationDbContext`

## Neue Klassen

Keine neuen Klassen erforderlich. Alle Funktionalität wird durch Erweiterung bestehender Klassen implementiert.

## Änderungen an bestehenden Klassen

### `ContinueWatchingEntry` (Entity/Datenmodell)

- **Neue Eigenschaften:**
  - `PlaylistId` (long?) — Foreign Key zu `Playlist`, nullable, Eindeutig-Schlüssel-Komponente
  - `Playlist` (Playlist?) — Navigations-Property zur `Playlist`-Entität

- **Datenbankebenen-Constraints (Migration):**
  - Foreign Key Constraint: `ContinueWatchingEntry.PlaylistId → Playlist.Id`, with OnDelete = SetNull
  - Unique Constraint: `UC_ContinueWatchingEntry_UserIdVideoIdPlaylistId` auf Kombination `(UserId, MovieId, PlaylistId)` UND `(UserId, TVShowEpisodeId, PlaylistId)` — Implementierung über zwei separate bedingte Indizes (conditional indexes mit Fallunterscheidung nach MovieId IS NOT NULL vs. TVShowEpisodeId IS NOT NULL)

### `ContinueWatchingDto` (Transfer Object)

- **Neue Eigenschaften:**
  - `PlaylistId` (long?) — Optional, wird bei Bedarf befüllt
  - `PlaylistName` (string?) — Optional, enthält Playlist-Name zur UI-Anzeige

### `ContinueWatchingService` (Service Layer)

- **Geänderte Methoden:**
  - `ReportProgressAsync(userId, mediaType, mediaId, position, duration, playlistId?, ct)` — Neue Parameter `playlistId` (long?)
  - `ProcessBufferedEntryAsync(userId, mediaType, mediaId, position, duration, playlistId?, ct)` — Neue Parameter `playlistId` (long?), erweitert Deduplizierungs-Logik
  - `RemoveExistingTVShowEntry(userId, episodeId, playlistId?, ct)` — Neue Parameter `playlistId` (long?), entfernt nur Einträge mit identischer PlaylistId
  - `RemoveExtsingMovieCollectionEntry(userId, movieId, playlistId?, ct)` — Neue Parameter `playlistId` (long?), entfernt nur Einträge mit identischer PlaylistId
  - `HideAsync(userId, mediaType, mediaId, playlistId?, ct)` — Neue Parameter `playlistId` (long?)
  - `SkipAsync(userId, mediaType, mediaId, playlistId?, ct)` — Neue Parameter `playlistId` (long?)
  - `GetListAsync(user, ct)` — Erweitert LINQ-Query um LEFT JOIN mit `Playlist`-Tabelle, befüllt `PlaylistId` und `PlaylistName` in DTO

- **Neue Methoden:**
  - `ValidatePlaylistOwnershipAsync(userId, playlistId, ct)` — Prüft, ob Benutzer Eigentümer der Playlist ist; ruft `IPlaylistService` auf

### `ContinueWatchingController` (API)

- **Geänderte Methoden:**
  - `PostProgress(ProgressRequest, ct)` — Request-Typ wird erweitert (s.u.), Methode gibt `playlistId` an Service weiter
  - `PostHide(ContinueWatchingActionRequest, ct)` — Request-Typ wird erweitert (s.u.), Methode gibt `playlistId` an Service weiter
  - `PostSkip(ContinueWatchingActionRequest, ct)` — Request-Typ wird erweitert (s.u.), Methode gibt `playlistId` an Service weiter

### `ProgressRequest` (DTO im Controller)

- **Neue Eigenschaften:**
  - `PlaylistId` (long?) — Optional, wird von Client übergeben wenn Playlist aktiv ist

### `ContinueWatchingActionRequest` (DTO im Controller)

- **Neue Eigenschaften:**
  - `PlaylistId` (long?) — Optional, wird von Client übergeben wenn Playlist aktiv ist

### `VideoPlayer.razor` (UI-Komponente)

- **Geänderte Methoden:**
  - `OnAfterRenderAsync(firstRender)` — Beim Aufruf von `continueWatching.attach()` wird zusätzlich `PlaylistContext?.PlaylistId` übergeben
  - `ApplyPlaylistContext(context, position)` — Setzt neuen lokalen State `currentPlaylistId = context.PlaylistId` für späteren Gebrauch in Progress-Reporting

- **Neue JavaScript-Interop Aufrufe:**
  - Beim Aufruf von `continueWatching.attach()` wird zusätzlicher Parameter `playlistId` übergeben

### `continueWatching.js` (JavaScript)

- **Geänderte Funktion:**
  - `attach(videoEl, mediaType, mediaId, baseUrl, bearerToken, playlistId)` — Neuer optionaler Parameter `playlistId` (long?)
  - Payload beim POST an `/api/continue-watching/progress` wird erweitert um: `playlistId: playlistId ?? null`

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddPlaylistIdToContinueWatchingEntry` | `ContinueWatchingEntry.PlaylistId` (neue Spalte) | Neue Spalte `PlaylistId (long?)` hinzufügen, Foreign Key zu `Playlist.Id` mit OnDelete=SetNull. Unique Constraint mit bedingten Indizes für `(UserId, MovieId, PlaylistId)` und `(UserId, TVShowEpisodeId, PlaylistId)`. |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ProgressRequest.PlaylistId` | Falls nicht NULL: Playlist existiert und gehört zum Benutzer | 400 Bad Request / 404 Not Found / 403 Forbidden |
| `ContinueWatchingActionRequest.PlaylistId` | Falls nicht NULL: Playlist existiert und gehört zum Benutzer | 400 Bad Request / 404 Not Found / 403 Forbidden |
| `ContinueWatchingEntry.PlaylistId` | Falls nicht NULL: Foreign Key muss auf existierende Playlist zeigen | Datenbankebenen-Constraint (FK) verhindert Speicherung |
| `ContinueWatchingEntry (Unique)` | Pro Benutzer darf `(VideoId, PlaylistId)` maximal einmal vorkommen | Datenbankebenen-Constraint (UC) verhindert Speicherung; Service prüft vor Insert/Update |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Eindeutigkeits-Constraint Migrationen:** Bestehende Daten vor Migration müssen auf Duplikate überprüft werden. Nach Migration können Duplikate technisch nicht mehr entstehen.

- **Bekannte Testfehler bei Duplikat-Daten:** Wenn vor Migration Duplikate existieren (zwei ContinueWatchingEntry mit gleichem UserId+VideoId), schlägt die Migration beim Erstellen des Unique Constraint fehl. Dies ist durch Prüfung und ggf. Cleanup vor Migration zu verhindern.

- **Backwards-Compatibility für Non-Playlist-Einträge:** Nach Migration haben alle bestehenden Einträge `PlaylistId = NULL`. Diese funktionieren weiterhin korrekt; Progress-Reporting ohne PlaylistId wird behandelt wie bisher.

- **E2E-Szenarien mit mehrfachen Vorkommen:** Wenn ein Video mit und ohne Playlist UND mit unterschiedlichen Playlists in der Liste auftaucht, müssen UI-Tests prüfen, dass Weiterschauen-Liste korrekt beide anzeigt und nicht collapsed/dedupliziert werden.

- **Abhängigkeit auf IPlaylistService:** Service-Methoden müssen `IPlaylistService` als Dependency erhalten. Falls nicht injiziert, schlägt DI-Container beim Starten fehl. Abhängigkeit muss bereits vor Schritt 6 im Repo vorhanden sein (von Schritt 5).

- **Gesehen-Markierung mit Playlist-Kontext:** Beim Markieren als „Gesehen" müssen ALLE ContinueWatchingEntry-Einträge für das Video gelöscht werden, unabhängig von PlaylistId. Bestehende Tests prüfen dies möglicherweise nur für Non-Playlist-Szenarios — neue Tests müssen Playlist-Mixed-Szenarien abdecken.

## Umsetzungsreihenfolge

1. **Migration erzeugen und anwenden**
   - Voraussetzungen: EF Core 10.0 DbContext-Setup, bestehende Migrationen im Repo funktionieren
   - Beschreibung: Neue Migration `AddPlaylistIdToContinueWatchingEntry` erstellen, die `PlaylistId`-Spalte hinzufügt, Foreign Key und Unique Constraints definiert. Migration vor Codeänderungen lokal testen.

2. **ContinueWatchingEntry Entity erweitern**
   - Voraussetzungen: Migration existiert (Schritt 1)
   - Beschreibung: `PlaylistId` (long?) und `Playlist` (Navigation) zu Entity hinzufügen. Fluent API/Data Annotations für FK konfigurieren.

3. **ContinueWatchingDto erweitern**
   - Voraussetzungen: Keine
   - Beschreibung: `PlaylistId` (long?) und `PlaylistName` (string?) Eigenschaften hinzufügen.

4. **ContinueWatchingService Methoden-Signaturen erweitern**
   - Voraussetzungen: ContinueWatchingEntry erweitert (Schritt 2), IPlaylistService als Dependency verfügbar (von Schritt 5)
   - Beschreibung: Methoden `ReportProgressAsync`, `ProcessBufferedEntryAsync`, `RemoveExistingTVShowEntry`, `RemoveExtsingMovieCollectionEntry`, `HideAsync`, `SkipAsync` um `playlistId?` Parameter erweitern. Neue Methode `ValidatePlaylistOwnershipAsync` hinzufügen.

5. **ContinueWatchingService Deduplizierungs-Logik anpassen**
   - Voraussetzungen: Methoden-Signaturen erweitert (Schritt 4)
   - Beschreibung: `RemoveExistingTVShowEntry` und `RemoveExtsingMovieCollectionEntry` so anpassen, dass sie nur Einträge mit identischer PlaylistId entfernen (oder NULL, wenn playlistId NULL ist). Filter-Logik im LINQ-Query hinzufügen.

6. **ContinueWatchingService GetListAsync erweitern (Playlist-Rekonstruktion)**
   - Voraussetzungen: ContinueWatchingDto erweitert (Schritt 3), Service-Signaturen erweitert (Schritt 4)
   - Beschreibung: `GetListAsync` LINQ-Query erweitern mit LEFT JOIN zu `Playlist`-Tabelle. `PlaylistId` und `PlaylistName` aus Join in DTO-Instanzen kopieren. Ownership-Validierung prüfen (Benutzer darf nur auf Playlists zugreifen, die er besitzt oder die öffentlich sind).

7. **ContinueWatchingService Validierung für PlaylistId hinzufügen**
   - Voraussetzungen: `ValidatePlaylistOwnershipAsync` implementiert (Schritt 4)
   - Beschreibung: In `ReportProgressAsync` und `ProcessBufferedEntryAsync` vor dem Speichern Validierung hinzufügen: Falls `playlistId` nicht NULL ist, rufe `ValidatePlaylistOwnershipAsync` auf. Bei Fehler werfe Exception mit aussagekräftiger Fehlermeldung.

8. **ProgressRequest und ContinueWatchingActionRequest DTOs erweitern**
   - Voraussetzungen: Keine
   - Beschreibung: `PlaylistId` (long?) Property zu beiden Request-Klassen im Controller hinzufügen.

9. **ContinueWatchingController Methoden anpassen**
   - Voraussetzungen: Request-DTOs erweitert (Schritt 8), Service-Signaturen erweitert (Schritt 4)
   - Beschreibung: `PostProgress`, `PostHide`, `PostSkip` Methoden um Übergabe von `request.PlaylistId` an Service anpassen. Error Handling für Validierungsfehler hinzufügen (400/404/403 Responses).

10. **VideoPlayer.razor erweitern (PlaylistId an JavaScript übergeben)**
    - Voraussetzungen: Keine (aber Schritt 11 muss parallel oder danach durchgeführt werden)
    - Beschreibung: In `OnAfterRenderAsync` bei Aufruf von `continueWatching.attach()` zusätzlichen Parameter `PlaylistContext?.PlaylistId` übergeben. Falls PlaylistContext null ist, `null` übergeben. Property `currentPlaylistId` speichern für späteren Gebrauch.

11. **continueWatching.js erweitern (PlaylistId in Payload)**
    - Voraussetzungen: VideoPlayer.razor erweitert (Schritt 10)
    - Beschreibung: `attach()`-Funktion Signatur um Parameter `playlistId` erweitern. Bei POST-Request an `/api/continue-watching/progress` `playlistId` in Payload einschließen: `{ ..., playlistId: playlistId ?? null }`.

12. **ContinueWatchingList.razor UI-Erweiterung (Playlist-Anzeige)**
    - Voraussetzungen: ContinueWatchingDto erweitert (Schritt 3), ContinueWatchingService.GetListAsync erweitert (Schritt 6)
    - Beschreibung: Für jeden Eintrag in der Liste prüfen, ob `PlaylistId != null`. Falls ja: Badge oder Text mit `PlaylistName` anzeigen (z.B. „In Playlist: [Name]"). Keine Änderung der Sortierung erforderlich.

13. **ContinueWatchingList.razor Interaktion (Playlist-Rekonstruktion beim Fortsetzen)**
    - Voraussetzungen: ContinueWatchingList UI erweitert (Schritt 12), IPlaylistApiClient verfügbar (von Schritt 5)
    - Beschreibung: Beim Klick auf Eintrag prüfen: Falls `entry.PlaylistId != null`, rufe `IPlaylistApiClient.StartPlaylistAsync(entry.PlaylistId, ...)` auf zur Rekonstruktion des `PlaylistPlaybackContext`, dann starte VideoPlayer. Falls `entry.PlaylistId == null`, starte VideoPlayer ohne Playlist-Kontext.

14. **Unit-Tests schreiben: ContinueWatchingServicePlaylistTests**
    - Voraussetzungen: Alle Service-Änderungen (Schritte 4-7), Test-Setup-Infrastruktur verfügbar
    - Beschreibung: Neue Testklasse mit Tests für Erstellen/Aktualisieren mit PlaylistId. Szenarien: (1) Eintrag mit PlaylistId erstellen, (2) Duplikat mit gleicher PlaylistId erkennen und aktualisieren, (3) Duplikat mit unterschiedlicher PlaylistId zulassen, (4) Eintrag ohne PlaylistId unabhängig von Einträgen mit PlaylistId halten.

15. **Unit-Tests schreiben: ContinueWatchingServiceMultipleEntriesTests**
    - Voraussetzungen: Deduplizierungs-Logik angepasst (Schritt 5), Test-Setup-Infrastruktur verfügbar
    - Beschreibung: Tests für Szenarien mit mehrfachen Vorkommen: (1) Video mit 3 verschiedenen PlaylistIds, (2) Video mit PlaylistId=null + mehreren PlaylistIds, (3) Sortierung bleibt durch ListOrder konsistent, (4) Gesehen-Markierung löscht alle Varianten.

16. **Unit-Tests schreiben: ContinueWatchingServiceRemovalTests**
    - Voraussetzungen: Hide/Skip-Logik mit PlaylistId angepasst (Schritt 5, 4), Test-Setup-Infrastruktur verfügbar
    - Beschreibung: Tests für `RemoveExistingTVShowEntry` und `RemoveExtsingMovieCollectionEntry` mit PlaylistId-Filter. Szenarien: (1) Entfernung mit PlaylistId entfernt nur Einträge dieser Playlist, (2) Entfernung mit PlaylistId=null entfernt nur nicht-Playlist-Einträge, (3) Skip mit PlaylistId erstellt Folge-Eintrag mit gleicher PlaylistId.

17. **Unit-Tests schreiben: ContinueWatchingDtoTests**
    - Voraussetzungen: ContinueWatchingDto erweitert (Schritt 3)
    - Beschreibung: Tests für neue DTO-Eigenschaften. Szenarien: (1) PlaylistId wird korrekt aus Entity kopiert, (2) PlaylistName wird aus Join korrekt befüllt, (3) NULL-Werte werden korrekt behandelt.

18. **E2E-Tests schreiben: Playlist-Weiterschauen Happy Path**
    - Voraussetzungen: Alle Code-Änderungen (Schritte 1-13), E2E-Test-Infrastruktur verfügbar
    - Beschreibung: Test für kompletten Flow: (1) Video aus Playlist starten, (2) Progress-Report mit PlaylistId wird gesendet, (3) Eintrag mit PlaylistId wird erstellt, (4) Eintrag in Weiterschauen-Liste ist sichtbar mit Playlist-Badge.

19. **E2E-Tests schreiben: Mehrfaches Vorkommen**
    - Voraussetzungen: Alle Code-Änderungen (Schritte 1-13), E2E-Test-Infrastruktur verfügbar
    - Beschreibung: Test: (1) Video mit Playlist1 starten und Progress speichern, (2) Video ohne Playlist starten und Progress speichern, (3) Video mit Playlist2 starten und Progress speichern, (4) Weiterschauen-Liste zeigt alle 3 Einträge, (5) Sortierung nach ListOrder bleibt konsistent.

20. **E2E-Tests schreiben: Gesehen-Markierung mit Playlist-Mix**
    - Voraussetzungen: Alle Code-Änderungen (Schritte 1-13), E2E-Test-Infrastruktur verfügbar
    - Beschreibung: Test: (1) Video mit 3 PlaylistId-Varianten in Weiterschauen-Liste, (2) Mark as Watched wird aufgerufen, (3) Alle 3 Varianten werden aus Weiterschauen-Liste entfernt, (4) WatchedEntry wird global angelegt (unabhängig von Playlist).

21. **E2E-Tests schreiben: Playlist-Rekonstruktion beim Fortsetzen**
    - Voraussetzungen: Alle Code-Änderungen (Schritte 1-13), E2E-Test-Infrastruktur verfügbar
    - Beschreibung: Test: (1) Video mit PlaylistId in Weiterschauen-Liste klicken, (2) VideoPlayer startet mit rekonstruiertem PlaylistPlaybackContext, (3) Playlist-Badge wird angezeigt, (4) Navigation (Next/Previous) funktioniert innerhalb der Playlist.

22. **E2E-Tests schreiben: Hide/Skip mit Playlist-Filter**
    - Voraussetzungen: Alle Code-Änderungen (Schritte 1-13), E2E-Test-Infrastruktur verfügbar
    - Beschreibung: Test: (1) Video mit PlaylistId im Kontext-Menü Verstecken klicken, (2) Nur dieser Eintrag wird entfernt, (3) Eintrag ohne PlaylistId bleibt, (4) Skip mit PlaylistId ersetzt mit Folge-Video mit gleicher PlaylistId.

23. **Bestehende Tests anpassen/erweitern (Regression)**
    - Voraussetzungen: Alle vorigen Schritte abgeschlossen
    - Beschreibung: Bestehende 31 ContinueWatching-Tests durchlaufen und prüfen, ob sie noch grün sind (sollten, da PlaylistId nullable ist und ON NULL ignoriert werden kann). Bei Fehlern: Tests für Non-Playlist-Szenarien (PlaylistId=null) anpassen. Besonders prüfen: `ContinueWatchingServiceGetNextEpisodeTests`, `ContinueWatchingContextMenuActionTests`, `ContinueWatchingServiceSignalRTests`, `ContinueWatchingWatchedStatusTests`, `ContinueWatchingE2ETests`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Playlist_CreateEntry_WithSamePlaylistId_UpdatesExisting` | `ContinueWatchingServicePlaylistTests` | Deduplizierung: Zwei Einträge mit identischer PlaylistId führen zu Update, nicht zu Duplikat |
| `Playlist_CreateEntry_WithDifferentPlaylistIds_AllowsBoth` | `ContinueWatchingServicePlaylistTests` | Dasselbe Video mit verschiedenen PlaylistIds können coexistieren |
| `Playlist_CreateEntry_WithAndWithoutPlaylistId_Independent` | `ContinueWatchingServicePlaylistTests` | Eintrag ohne PlaylistId (null) ist unabhängig von Einträgen mit PlaylistId |
| `Playlist_ValidateOwnership_NonOwner_Throws` | `ContinueWatchingServicePlaylistTests` | Validierung schlägt fehl wenn Benutzer nicht Besitzer der Playlist ist |
| `Playlist_RemoveExistingTVShow_OnlyRemovesMatchingPlaylistId` | `ContinueWatchingServiceRemovalTests` | `RemoveExistingTVShowEntry` löscht nur Einträge mit identischer PlaylistId |
| `Playlist_RemoveExistingMovie_OnlyRemovesMatchingPlaylistId` | `ContinueWatchingServiceRemovalTests` | `RemoveExistingMovieCollectionEntry` löscht nur Einträge mit identischer PlaylistId |
| `MultipleEntries_SameVideoThreePlaylists_AllExist` | `ContinueWatchingServiceMultipleEntriesTests` | Video kann mit 3 verschiedenen PlaylistIds in Liste vorkommen |
| `MultipleEntries_SameVideoWithAndWithoutPlaylist_Separate` | `ContinueWatchingServiceMultipleEntriesTests` | Eintrag mit PlaylistId=null bleibt separate von Einträgen mit PlaylistId |
| `MultipleEntries_MarkWatched_DeletesAllVariants` | `ContinueWatchingServiceMultipleEntriesTests` | Gesehen-Markierung löscht alle ContinueWatchingEntry-Varianten unabhängig von PlaylistId |
| `DTO_PlaylistIdAndName_PopulatedFromDb` | `ContinueWatchingDtoTests` | DTO-Properties PlaylistId und PlaylistName werden korrekt aus DB-Join befüllt |
| `DTO_PlaylistIdNull_NameNull` | `ContinueWatchingDtoTests` | Wenn PlaylistId=null, dann PlaylistName auch null im DTO |
| `E2E_Playlist_CreateAndReportProgress_EntryCreated` | `ContinueWatchingE2ETests` (erweitert) | Happy Path: Video aus Playlist starten, Progress report mit PlaylistId erstellt Eintrag mit PlaylistId |
| `E2E_MultipleEntriesPlaylist_AllThreeVariantsVisible` | `ContinueWatchingE2ETests` (erweitert) | Video mit 3 PlaylistId-Varianten in Weiterschauen-Liste alle sichtbar |
| `E2E_MarkWatchedPlaylist_RemovesAllVariants` | `ContinueWatchingE2ETests` (erweitert) | Mark as Watched entfernt alle ContinueWatchingEntry-Varianten |
| `E2E_HideWithPlaylistId_OnlyRemovesMatching` | `ContinueWatchingE2ETests` (erweitert) | Hide löscht nur den Eintrag mit exakter PlaylistId |
| `E2E_SkipWithPlaylistId_NextWithSamePlaylistId` | `ContinueWatchingE2ETests` (erweitert) | Skip ersetzt Video mit Folge-Video, behält PlaylistId |
| Helper: `CreateTestPlaylist(db, userId, name)` | `ContinueWatchingServiceTestBase` | Erstellt Test-Playlist für Tests |
| Helper: `CreateTestPlaylistEntry(db, playlistId, mediaId, mediaType)` | `ContinueWatchingServiceTestBase` | Erstellt Test-PlaylistEntry |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ContinueWatchingServiceGetNextEpisodeTests` | Tests setzen PlaylistId implizit auf null; wenn RemoveExistingTVShowEntry-Signatur sich ändert, müssen Aufrufe im Service prüft werden (sollten aber durch nullable Parameter unkritisch sein) |
| `ContinueWatchingContextMenuActionTests` | Tests für Hide/Skip müssen prüfen, dass PlaylistId=null weiterhin funktioniert; möglicherweise müssen mock-Aufrufe für neue ValidatePlaylistOwnership angepasst werden |
| `ContinueWatchingServiceSignalRTests` | Tests sollten weiterhin grün sein, da SignalR-Event-Logik unverändert (nur PlaylistId wird zusätzlich erfasst, Signal selbst bleibt gleich) |
| `ContinueWatchingWatchedStatusTests` | Tests sollten grün bleiben; Gesehen-Markierung muss aber ALLE Varianten löschen (auch mit PlaylistId) — Tests müssen ggf. erweitert werden um zu prüfen, dass mehrere Varianten gelöscht werden |
| `ContinueWatchingE2ETests` | Bestehende E2E-Tests (4 Tests) sollten grün bleiben, da sie Non-Playlist-Szenarien (PlaylistId=null) testen; neue E2E-Tests adressieren Playlist-Szenarien |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Happy Path: Video aus Playlist starten, Fortschritt speichern, in Weiterschauen-Liste mit Playlist-Badge angezeigt | `ContinueWatchingE2ETests.E2E_Playlist_CreateAndReportProgress_EntryCreated` | Anforderung: „Video kann mit PlaylistId in Weiterschauen-Liste erscheinen" | E2E testet kompletten HTTP-Request-/Response-Fluss und Datenbankzustand; Unit-Tests können nur Service-Logik isoliert prüfen |
| Pflicht | Mehrfaches Vorkommen: Dasselbe Video mit 3 PlaylistId-Varianten (Playlist1, Playlist2, null) alle in Liste sichtbar | `ContinueWatchingE2ETests.E2E_MultipleEntriesPlaylist_AllThreeVariantsVisible` | Anforderung: „Video kann mehrfach in Weiterschauen-Liste mit unterschiedlichen Playlist-Bindungen vorkommen" | Deduplizierung ist komplex; Unit-Tests prüfen Service-Logik, E2E prüft tatsächliche Persistierung und Abruf durch GET-Endpoint |
| Pflicht | Gesehen-Markierung: Video mit 3 PlaylistId-Varianten wird als Gesehen markiert, alle 3 ContinueWatchingEntry-Einträge werden gelöscht | `ContinueWatchingE2ETests.E2E_MarkWatchedPlaylist_RemovesAllVariants` | Anforderung: „Globale Gesehen-Markierung wirkt playlist-übergreifend" | E2E testet Integration zwischen WatchedStatusService und ContinueWatchingService |
| Pflicht | Playlist-Rekonstruktion: Klick auf Eintrag mit PlaylistId startet VideoPlayer mit PlaylistPlaybackContext | `ContinueWatchingE2ETests.E2E_ResumeFromPlaylistEntry_PlaylistContextRecovered` | Anforderung: „Fortsetzen aus Weiterschauen-Liste rekonstruiert Playlist-Kontext" | E2E testet JavaScript-Interop und Navigation zwischen Razor-Komponenten |
| Stark empfohlen | Hide mit Playlist-Filter: Eintrag mit PlaylistId verstecken entfernt nur diesen, nicht andere Varianten | `ContinueWatchingE2ETests.E2E_HideWithPlaylistId_OnlyRemovesMatching` | Anforderung: „Separate Verwaltung pro Playlist-Kombination" | E2E testet HTTP-Request und Datenbankzustand nach Delete-Operation |
| Stark empfohlen | Skip mit Playlist-Filter: Überspringen ersetzt mit Folge-Video, erhält PlaylistId | `ContinueWatchingE2ETests.E2E_SkipWithPlaylistId_NextWithSamePlaylistId` | Anforderung: „Skip behält Playlist-Kontext bei" | E2E testet komplexen Navigations-Ablauf (GetNextEpisode/Movie + Erstellen mit PlaylistId) |
| Empfohlen | Fehlerfall: Playlist wurde gelöscht, Eintrag mit dieser PlaylistId sollte immer noch funktionieren (ON DELETE SET NULL) | `ContinueWatchingE2ETests.E2E_PlaylistDeleted_EntryBecomesFree` | Anforderung: „Robustheit bei gelöschter Playlist" | Testet Migration und Cascade-Delete-Behavior |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ContinueWatchingE2ETests.HappyPath_EpisodeCompleted_NextEpisodeAppearsInContinueWatchingList` | Sollte grün bleiben (PlaylistId=null), aber möglicherweise muss GetListAsync-Mock angepasst werden wenn DTO-Struktur sich ändert |
| `ContinueWatchingE2ETests.SeasonTransition_LastEpisodeOfSeasonCompleted_FirstEpisodeOfNextSeasonAppears` | Sollte grün bleiben (PlaylistId=null) |
| `ContinueWatchingE2ETests.EpisodeGap_EpisodeCompleted_NextAvailableEpisodeAppearsInContinueWatchingList` | Sollte grün bleiben (PlaylistId=null) |
| `ContinueWatchingE2ETests.SeriesEnd_LastEpisodeOfLastSeasonCompleted_NoContinueWatchingEntryCreated` | Sollte grün bleiben (PlaylistId=null) |

## Offene Punkte

Keine. Alle Anforderungen sind durch die Bestandsaufnahme und technische Anforderungsübersetzung klar definiert.
