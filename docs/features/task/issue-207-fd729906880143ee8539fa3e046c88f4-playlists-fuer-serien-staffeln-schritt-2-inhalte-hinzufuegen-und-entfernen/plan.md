# Umsetzungsplan: Playlists – Inhalte hinzufügen und entfernen (Schritt 2)

## Übersicht

Der Umsetzungsplan erweitert das bestehende Playlist-Feature um die Kernfunktionalität zum Hinzufügen und Entfernen von Medieinhalten. Benutzer können einzelne oder gruppierte Medieninhalte (Filme, Episoden, Staffeln, Serien, Filmsammlungen) ihrer Playlist hinzufügen. Die Verwaltung erfolgt auf Ebene einzelner Titel mit Duplikatsprüfung pro Playlist und Berechtigungskontrolle. Betroffen sind Datenmodell (neue Tabelle `PlaylistEntry`), Service-Logik (neue Methoden mit Cascade-Logik), Controller-Endpoints (POST/DELETE/GET), DTOs und Tests.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Duplikat-Response-Format** | HTTP 409 Conflict mit strukturiertem Fehler: `{ "error": "Medieninhalt bereits in dieser Playlist vorhanden" }` | Standard HTTP Semantik für Konflikte. Client kann klar zwischen Erfolg (200) und Konflikt (409) unterscheiden. Eindeutig und REST-konform. |
| **Entfernen nicht vorhandener Einträge** | HTTP 404 Not Found statt 204 | Gibt explizites Feedback, dass der Eintrag nicht gefunden wurde. Hilft bei Debugging und verhindert stille Fehler. |
| **Medienbestand-Synchronisation** | Verwaiste Einträge werden durch DB-Trigger oder Scheduled Job gelöst (Implementierung in späterem Schritt) | Effizienter als aktive Abfrage bei jedem Read. Blockiert User-Operationen nicht. Kann später implementiert werden. |
| **Cascade-Response-Format** | Bei Hinzufügen einer Serie/Staffel/Sammlung: Top-Level-Eintrag wird zurückgegeben, Cascade-Duplicates werden still übersprungen, neue Cascade-Einträge werden eingefügt | Einfache API. Client erhält Rückmeldung über die angeforderte Aktion. Interne Duplikate sind Implementierungsdetail. |
| **Konfiguration MaxPlaylistItemCount** | Eigenschaft zu `PlaylistSettings` hinzufügen, aber Enforcement in diesem Schritt nicht implementieren | Vorbereitung für späteren Schritt. Konfiguration ist vorhanden, kann aber „null" bleiben (unbegrenzt). |
| **ParentMediaType/ParentMediaId als String vs. Enum** | Als `string` im Entity und DTO, nicht als Enum | Flexibel für zukünftige Erweiterungen, vermeidet EF Core Enum-Komplexität. Gültige Werte durch Validierung in Service sichergestellt. |

## Programmabläufe

### Medieninhalt zur Playlist hinzufügen (mit Cascade-Logik)

1. Client sendet POST-Request an `/api/playlists/{id}/entries` mit `{ mediaType, mediaId }`
2. `PlaylistsController.AddMediaToPlaylistAsync` extrahiert aktuellen Benutzer aus Auth-Token
3. `PlaylistService.AddMediaToPlaylistAsync` wird aufgerufen
4. Service prüft Berechtigung: Ist der Benutzer Besitzer der Playlist? Falls nein → werfe `PlaylistAccessDeniedException`
5. Service validiert `mediaType` gegen die 5 gültigen Typen. Falls ungültig → werfe `InvalidOperationException`
6. Service prüft Existenz des Medieninhalts via `IMediaService` (oder direkter DB-Abfrage). Falls nicht existent → werfe `KeyNotFoundException`
7. **Cascade-Logik:**
   - Wenn `mediaType == "TVShow"`: Lade alle `TVShowSeason` für diese Serie; für jede Staffel lade alle `TVShowEpisode`
   - Wenn `mediaType == "TVShowSeason"`: Lade alle `TVShowEpisode` für diese Staffel
   - Wenn `mediaType == "MovieCollection"`: Lade alle `Movie` für diese Sammlung
   - Für jeden geladenen Eintrag: Prüfe auf Duplikat via Composite Unique Index `(PlaylistId, MediaType, MediaId)`
8. Erstelle `PlaylistEntry` für den angeforderten Top-Level-Eintrag (mit `ParentMediaType = null`, `ParentMediaId = null`)
9. Erstelle `PlaylistEntry` für jeden Cascade-Eintrag (mit `ParentMediaType` und `ParentMediaId` gesetzt)
10. Duplicate bei Cascade-Einträgen werden still übersprungen
11. Speichere alle neuen Einträge in DB (Transaktion)
12. Gib `DtoPlaylistEntry` für den Top-Level-Eintrag zurück
13. Bei Duplikat des Top-Level-Eintrags: Gib 409 Conflict zurück mit `{ "error": "..." }`

Beteiligte Klassen/Komponenten: `PlaylistsController`, `PlaylistService`, `IMediaService`, `PlaylistEntry` (Entity), `DtoPlaylistEntry`, `ApplicationDbContext`, `Exceptions.PlaylistAccessDeniedException`

### Medieninhalt aus Playlist entfernen

1. Client sendet DELETE-Request an `/api/playlists/{id}/entries/{mediaType}/{mediaId}`
2. `PlaylistsController.RemoveMediaFromPlaylistAsync` extrahiert aktuellen Benutzer
3. `PlaylistService.RemoveMediaFromPlaylistAsync` wird aufgerufen
4. Service prüft Berechtigung (nur Besitzer). Falls nicht → werfe `PlaylistAccessDeniedException`
5. Service sucht Eintrag mit `(PlaylistId, MediaType, MediaId)`. Falls nicht gefunden → werfe `KeyNotFoundException`
6. Service löscht den Eintrag aus DB
7. Gib 204 No Content zurück
8. Bei nicht gefundenem Eintrag: Gib 404 Not Found zurück

Beteiligte Klassen/Komponenten: `PlaylistsController`, `PlaylistService`, `ApplicationDbContext`

### Alle Einträge einer Playlist abrufen (mit Bereinigung verwaister Einträge)

1. Client sendet GET-Request an `/api/playlists/{id}/entries`
2. `PlaylistsController.GetPlaylistEntriesAsync` extrahiert aktuellen Benutzer
3. `PlaylistService.GetPlaylistEntriesAsync` wird aufgerufen
4. Service prüft Berechtigung (nur Besitzer). Falls nicht → werfe `PlaylistAccessDeniedException`
5. Service prüft Existenz der Playlist. Falls nicht → werfe `KeyNotFoundException`
6. Service lädt alle `PlaylistEntry`-Objekte für diese Playlist aus DB (noch unsortiert)
7. **Für jeden Eintrag: Prüfe via `IMediaService`, ob der referenzierte Medieninhalt noch existiert**
   - Falls ja: Eintrag wird beibehalten
   - Falls nein: Eintrag wird still aus der DB gelöscht und nicht in der Ergebnisliste aufgeführt (kein Fehler, kein Log-Eintrag für den Anwender)
8. Service konvertiert jede verbleibende Entity zu `DtoPlaylistEntry` (einschließlich Titels des Medieninhalts — Lookup erforderlich)
9. Gib `DtoPlaylistEntry[]` zurück (200 OK)
10. Bei nicht gefundener Playlist: Gib 404 Not Found zurück

Beteiligte Klassen/Komponenten: `PlaylistsController`, `PlaylistService`, `PlaylistEntry`, `DtoPlaylistEntry`, `ApplicationDbContext`, `IMediaService` (für Existenzprüfung und Titel-Lookup)

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PlaylistEntry` | Datenmodellklasse (Entity Framework) | Repräsentiert einen Eintrag in einer Playlist (Medieninhalt mit Quellreferenz) |
| `DtoPlaylistEntry` | DTO (Data Transfer Object) | Client-Modell für Playlist-Einträge (Antworten und interne Darstellung) |
| `DtoAddMediaToPlaylistRequest` | DTO (Request) | Client-Request zum Hinzufügen eines Medieninhalts |
| `DtoRemoveMediaFromPlaylistRequest` | DTO (Request, optional) | Client-Request zum Entfernen eines Medieninhalts (optional; kann auch Route-Parameter verwenden) |

## Änderungen an bestehenden Klassen

### `Playlist` (Entity Framework Datenmodellklasse)

- **Neue Navigation:** `PlaylistEntries` (ICollection<PlaylistEntry>) — Sammlung der Einträge dieser Playlist

### `IPlaylistService` (Interface)

- **Neue Methoden:**
  - `AddMediaToPlaylistAsync(playlistId: long, userId: string, mediaType: string, mediaId: long, cancellationToken: CancellationToken)` → `DtoPlaylistEntry`
    - Fügt einen Medieninhalt (mit Cascade-Logik) zur Playlist hinzu
  - `RemoveMediaFromPlaylistAsync(playlistId: long, userId: string, mediaType: string, mediaId: long, cancellationToken: CancellationToken)` → `Task`
    - Entfernt einen Medieninhalt aus der Playlist
  - `GetPlaylistEntriesAsync(playlistId: long, userId: string, cancellationToken: CancellationToken)` → `DtoPlaylistEntry[]`
    - Ruft alle Einträge einer Playlist ab

### `PlaylistService` (Implementierung)

- **Neue private Hilfsmethoden:**
  - `ValidateMediaType(mediaType: string)` → `void` — Prüft, ob mediaType eines der 5 gültigen Typen ist
  - `GetCascadeMediaIds(mediaType: string, mediaId: long)` → `Task<IEnumerable<(string type, long id)>>` — Lade alle Cascade-Einträge für einen Top-Level-Eintrag
  - `CheckMediaExists(mediaType: string, mediaId: long)` → `Task<bool>` — Prüft, ob der Medieninhalt existiert
  - `ToDto(playlistEntry: PlaylistEntry, mediaTitle: string, parentMediaTitle: string?)` → `DtoPlaylistEntry` — Konvertiert Entity zu DTO
- **Implementierung der drei neuen Interface-Methoden** mit:
  - Berechtigungsprüfung (nur Besitzer)
  - MediaType-Validierung
  - Cascade-Logik für TVShow/TVShowSeason/MovieCollection
  - Duplikatsprüfung
  - Medienbestand-Existenzprüfung
  - Transaktionen für mehrere Inserts

### `PlaylistSettings` (Konfigurationsklasse)

- **Neue Eigenschaft:** `MaxPlaylistItemCount` (int?, Standard: null) — Optionale maximale Anzahl Einträge pro Playlist (Enforcement in späterem Schritt)

### `PlaylistsController` (API-Controller)

- **Neue Endpoints:**
  - `POST /api/playlists/{id}/entries` — Ruft `AddMediaToPlaylistAsync` auf, gibt `DtoPlaylistEntry` oder 409 zurück
  - `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}` — Ruft `RemoveMediaFromPlaylistAsync` auf, gibt 204 oder 404 zurück
  - `GET /api/playlists/{id}/entries` — Ruft `GetPlaylistEntriesAsync` auf, gibt `DtoPlaylistEntry[]` zurück

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddPlaylistEntriesTable` | Neue Tabelle `PlaylistEntries` | Erstelle Tabelle mit Spalten: `Id`, `PlaylistId`, `Playlist` (FK), `MediaType`, `MediaId`, `ParentMediaType`, `ParentMediaId`, `AddedAt`. Composite Unique Index auf `(PlaylistId, MediaType, MediaId)`. FK mit Cascade Delete zu `Playlists`. |
| — | `Playlists` | Keine direkten Änderungen (Navigation wird via Konfiguration hinzugefügt) |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `DtoAddMediaToPlaylistRequest.mediaType` | Muss eines der 5 Typen sein: `Movie`, `TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection` | 400 Bad Request mit Meldung "Ungültiger Medientyp" |
| `DtoAddMediaToPlaylistRequest.mediaId` | Muss > 0 sein | 400 Bad Request |
| Medieninhalt | Muss in Datenbank existieren (via `IMediaService` oder direct query) | 404 Not Found mit Meldung "Medieninhalt nicht gefunden" |
| Duplikat | Eintrag mit `(PlaylistId, MediaType, MediaId)` darf nicht bereits existieren | 409 Conflict mit Meldung "Medieninhalt bereits in dieser Playlist vorhanden" |
| Berechtigung | Anfragender Benutzer muss Besitzer der Playlist sein | 403 Forbidden oder 401 Unauthorized |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Playlists:MaxPlaylistItemCount` | `int?` | `null` (unbegrenzt) | Optionale maximale Anzahl Einträge pro Playlist (Enforcement ist Future Work) |

## Seiteneffekte und Risiken

- **PlaylistDetail.razor-Komponente:** Muss erweitert werden, um neue GET-Endpoint zu nutzen und Einträge anzuzeigen. Aktuell hat die Komponente keine Bindings für PlaylistEntries.
- **PlaylistsE2ETests:** Tests, die `PlaylistDetail`-Komponente laden, können durch fehlende Einträge-Anzeige betroffen sein (müssen u. U. erweitert werden).
- **Medienbestand-Konsistenz:** Falls ein Medieninhalt gelöscht wird, bleiben verwaiste `PlaylistEntry`-Reihen bestehen. Diese müssen durch späteren Schritt (Trigger/Job) bereinigt werden. Momentan können Clients auf gelöschte Inhalte verweisen.
- **Cascade-Abruf-Performance:** Bei Hinzufügen einer großen Serie können viele Episoden auf einmal geladen und eingefügt werden. Transaktions- und Performance-Test erforderlich.

## Umsetzungsreihenfolge

1. **Enum oder Konstanten für MediaTypes definieren**
   - Voraussetzungen: Keine
   - Beschreibung: Erstelle Konstanten oder Enum für die 5 gültigen Medientypen (`Movie`, `TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection`). Diese werden in Validierung und Cascade-Logik verwendet. Empfohlen: Statische Klasse `MediaTypeConstants` mit String-Konstanten zur Flexibilität.

2. **PlaylistEntry-Entity erstellen**
   - Voraussetzungen: Mediatype-Konstanten
   - Beschreibung: Neue Klasse `PlaylistEntry.cs` mit Eigenschaften `Id`, `PlaylistId`, `Playlist`, `MediaType`, `MediaId`, `ParentMediaType`, `ParentMediaId`, `AddedAt`. Navigation `Playlist` hinzufügen.

3. **EF Core Konfiguration für PlaylistEntry erstellen**
   - Voraussetzungen: PlaylistEntry-Entity
   - Beschreibung: Datei `PlaylistEntryConfiguration.cs` im Ordner `Data/Configurations`. Konfiguriere Composite Unique Constraint auf `(PlaylistId, MediaType, MediaId)`, Foreign Key mit Cascade Delete zu Playlist.

4. **Playlist-Entity um PlaylistEntries-Navigation erweitern**
   - Voraussetzungen: PlaylistEntry-Entity und Konfiguration
   - Beschreibung: Füge Eigenschaft `public ICollection<PlaylistEntry> PlaylistEntries { get; set; }` zu Playlist hinzu.

5. **EF Core Migration erstellen und anwenden**
   - Voraussetzungen: PlaylistEntry-Konfiguration, aktualisierte Playlist-Entity
   - Beschreibung: Führe `Add-Migration AddPlaylistEntriesTable` aus. Prüfe Migration auf Richtigkeit (Composite Index, FK, Cascade Delete). Führe Migration aus oder speichere für späteren Punkt.

6. **DTOs erstellen: DtoPlaylistEntry, DtoAddMediaToPlaylistRequest, DtoRemoveMediaFromPlaylistRequest**
   - Voraussetzungen: Keine (können parallel zu Entity-Erstellung erfolgen)
   - Beschreibung: Neue DTO-Klassen im Verzeichnis `VideoWebPlayer.Client/Models`. `DtoPlaylistEntry` enthält alle Eigenschaften zur Anzeige, `DtoAddMediaToPlaylistRequest` Request-Format.

7. **IMediaService-Methoden recherchieren oder stub**
   - Voraussetzungen: MediaType-Konstanten
   - Beschreibung: Recherchiere bestehende `IMediaService`-Schnittstelle (oder vergleichbaren Service) für Abfragen wie „Hole TVShowSeason für TVShowId", „Hole alle TVShowEpisodes für SeasonId" etc. Falls nicht vorhanden, müssen Stubs oder direkte DB-Abfragen geplant werden.

8. **PlaylistService erweitern: Hilfsmethoden hinzufügen**
   - Voraussetzungen: PlaylistEntry-Entity, MediaType-Konstanten, IMediaService recherchiert
   - Beschreibung: Implementiere private Hilfsmethoden `ValidateMediaType`, `CheckMediaExists`, `GetCascadeMediaIds`, `ToDto(playlistEntry, ...)`. Diese sollten gut testbar sein.

9. **PlaylistService erweitern: AddMediaToPlaylistAsync implementieren**
   - Voraussetzungen: Hilfsmethoden (Schritt 8), ApplicationDbContext konfiguriert
   - Beschreibung: Vollständige Implementierung mit Berechtigungsprüfung, MediaType-Validierung, Cascade-Logik, Duplikatsprüfung, Transaktion. Rückgabe `DtoPlaylistEntry`.

10. **PlaylistService erweitern: RemoveMediaFromPlaylistAsync implementieren**
    - Voraussetzungen: Hilfsmethoden
    - Beschreibung: Implementierung mit Berechtigungsprüfung, Lookup, Löschung.

11. **PlaylistService erweitern: GetPlaylistEntriesAsync implementieren (mit Bereinigung verwaister Einträge)**
    - Voraussetzungen: Hilfsmethoden, `ToDto`
    - Beschreibung: Implementierung mit Berechtigungsprüfung, Laden aller Einträge, **Prüfung jedes Eintrags auf Existenz des referenzierten Medieninhalts via `IMediaService`**. Verwaiste Einträge (Medieninhalt existiert nicht mehr) werden still aus der DB gelöscht und nicht in der Ergebnisliste zurückgegeben. Verbleibende Einträge werden zu DTOs konvertiert.

12. **IPlaylistService-Interface erweitern**
    - Voraussetzungen: Neue Service-Methoden implementiert (Schritte 9–11)
    - Beschreibung: Füge Methodensignaturen zu Interface hinzu: `AddMediaToPlaylistAsync`, `RemoveMediaFromPlaylistAsync`, `GetPlaylistEntriesAsync`.

13. **PlaylistSettings erweitern**
    - Voraussetzungen: Keine
    - Beschreibung: Füge Eigenschaft `MaxPlaylistItemCount` hinzu. Keine Enforcement in diesem Schritt.

14. **PlaylistsController erweitern: POST /api/playlists/{id}/entries**
    - Voraussetzungen: Service-Methoden, DTOs, `AddMediaToPlaylistAsync` implementiert
    - Beschreibung: Neue Action-Methode mit Parameter `id` und `request: DtoAddMediaToPlaylistRequest`. Rufe `AddMediaToPlaylistAsync` auf, gebe Antwort als JSON zurück. Error-Handling: 400 (Bad Request), 404 (Not Found), 409 (Conflict), 403 (Forbidden).

15. **PlaylistsController erweitern: DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}**
    - Voraussetzungen: Service-Methoden, `RemoveMediaFromPlaylistAsync` implementiert
    - Beschreibung: Neue Action-Methode mit Route-Parametern. Rufe Service auf, gebe 204 zurück. Error-Handling: 404, 403.

16. **PlaylistsController erweitern: GET /api/playlists/{id}/entries**
    - Voraussetzungen: Service-Methoden, `GetPlaylistEntriesAsync` implementiert
    - Beschreibung: Neue Action-Methode mit Parameter `id`. Rufe Service auf, gebe `DtoPlaylistEntry[]` zurück. Error-Handling: 404, 403.

17. **PlaylistDetail.razor erweitern: PlaylistEntries-Anzeige und Verwaltung**
    - Voraussetzungen: Alle Controller-Endpoints implementiert, DTOs verfügbar
    - Beschreibung: Erweitere Komponente um: (a) GET-Aufruf für Einträge laden, (b) Unsortierte Liste der Einträge anzeigen, (c) Delete-Button pro Eintrag, (d) Hinzufügen-UI (Dropdown für MediaType, Such-/Auswahlfeld für Inhalt, Hinzufügen-Button). UI-Feedback für Duplikate (Toast).

18. **Unit-Tests für PlaylistService: AddMediaToPlaylistAsync**
    - Voraussetzungen: PlaylistService-Methode implementiert, Test-Infrastruktur (PlaylistServiceTestBase)
    - Beschreibung: Neue Testklasse `PlaylistServiceTests_AddMedia` mit Tests für: Erfolgsfall, Duplikat-Handling, Cascade (TVShow→Staffeln/Episoden), Berechtigungsprüfung, nicht vorhandener Medieninhalt, ungültiger MediaType.

19. **Unit-Tests für PlaylistService: RemoveMediaFromPlaylistAsync**
    - Voraussetzungen: PlaylistService-Methode implementiert
    - Beschreibung: Testklasse `PlaylistServiceTests_RemoveMedia` mit Tests für: Erfolgsfall, Eintrag nicht vorhanden, Berechtigungsprüfung.

20. **Unit-Tests für PlaylistService: GetPlaylistEntriesAsync**
    - Voraussetzungen: PlaylistService-Methode implementiert
    - Beschreibung: Testklasse `PlaylistServiceTests_GetEntries` mit Tests für: Erfolgsfall (mit Daten), leere Playlist, Berechtigungsprüfung.

21. **Integration-Tests für PlaylistsController: neue Endpoints**
    - Voraussetzungen: Controller-Methoden implementiert, Controller-Test-Infrastruktur
    - Beschreibung: Tests in `PlaylistsControllerTests_Entries` (neue Klasse oder erweiterte) für POST, DELETE, GET. Error-Handling testen (400, 404, 409, 403).

22. **E2E-Tests: Medieninhalt hinzufügen (Happy Path)**
    - Voraussetzungen: PlaylistDetail.razor erweitert, alle Komponenten funktionsfähig
    - Beschreibung: Test in `PlaylistDetailE2ETests` (erweitert) oder neue Klasse `PlaylistEntriesE2ETests`: Benutzer navigiert zu Playlist, wählt Film aus, fügt ihn hinzu, Film erscheint in Liste.

23. **E2E-Tests: Medieninhalt entfernen**
    - Voraussetzungen: PlaylistDetail.razor mit Delete-Button
    - Beschreibung: Test: Benutzer laden Playlist mit Einträgen, löschen einen, Eintrag verschwindet.

24. **E2E-Tests: Duplikat-Handling**
    - Voraussetzungen: PlaylistDetail.razor mit Duplikat-Feedback
    - Beschreibung: Test: Benutzer versucht, denselben Film zweimal hinzuzufügen, erhält Toast-Meldung.

25. **E2E-Tests: Cascade-Hinzufügen (Serie)**
    - Voraussetzungen: PlaylistDetail.razor, Test-Daten mit Serie, Staffeln, Episoden
    - Beschreibung: Test: Benutzer fügt Serie hinzu, alle Staffeln und Episoden erscheinen in Liste.

26. **Dokumentation und Code-Review vorbereiten**
    - Voraussetzungen: Alle Tests grün
    - Beschreibung: Code-Dokumentation (XML-Kommentare), CHANGELOG-Eintrag, Code-Review-Vorbereitung.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AddMedia_ValidMovie_Success` | `PlaylistServiceTests_AddMedia` | Erfolgreicher Hinzufügen eines Films |
| `AddMedia_Duplicate_Returns409` | `PlaylistServiceTests_AddMedia` | Duplikat wird mit 409 erkannt |
| `AddMedia_InvalidMediaType_Throws400` | `PlaylistServiceTests_AddMedia` | Ungültiger MediaType wirft InvalidOperationException |
| `AddMedia_MediaNotFound_Throws404` | `PlaylistServiceTests_AddMedia` | Nicht vorhandener Medieninhalt wirft KeyNotFoundException |
| `AddMedia_NotOwner_ThrowsForbidden` | `PlaylistServiceTests_AddMedia` | Nicht-Besitzer wirft PlaylistAccessDeniedException |
| `AddMedia_TVShow_CascadesEpisodes` | `PlaylistServiceTests_AddMedia` | Bei Serie: alle Staffeln und Episoden werden hinzugefügt |
| `AddMedia_TVShowSeason_CascadesEpisodes` | `PlaylistServiceTests_AddMedia` | Bei Staffel: alle Episoden werden hinzugefügt |
| `AddMedia_MovieCollection_CascadesMovies` | `PlaylistServiceTests_AddMedia` | Bei Sammlung: alle Filme werden hinzugefügt |
| `AddMedia_CascadeWithDuplicates_SkipsDuplicates` | `PlaylistServiceTests_AddMedia` | Bei Cascade: Duplikate werden übersprungen |
| `RemoveMedia_ValidEntry_Success` | `PlaylistServiceTests_RemoveMedia` | Erfolgreicher Entfernen eines Eintrags |
| `RemoveMedia_NotFound_Throws404` | `PlaylistServiceTests_RemoveMedia` | Eintrag nicht vorhanden wirft KeyNotFoundException |
| `RemoveMedia_NotOwner_ThrowsForbidden` | `PlaylistServiceTests_RemoveMedia` | Nicht-Besitzer wirft PlaylistAccessDeniedException |
| `GetEntries_ReturnsAllEntries` | `PlaylistServiceTests_GetEntries` | Alle Einträge werden zurückgegeben |
| `GetEntries_EmptyPlaylist_ReturnsEmpty` | `PlaylistServiceTests_GetEntries` | Leere Playlist gibt leeres Array zurück |
| `GetEntries_NotOwner_ThrowsForbidden` | `PlaylistServiceTests_GetEntries` | Nicht-Besitzer wirft PlaylistAccessDeniedException |
| `GetEntries_PlaylistNotFound_Throws404` | `PlaylistServiceTests_GetEntries` | Nicht vorhandene Playlist wirft KeyNotFoundException |
| `GetEntries_RemovesOrphanedEntries` | `PlaylistServiceTests_GetEntries` | **Verwaiste Einträge (Medieninhalt existiert nicht) werden still gelöscht und nicht zurückgegeben** |
| `GetEntries_OrphanedEntry_DeletedFromDatabase` | `PlaylistServiceTests_GetEntries` | **Verwaister Eintrag wird aus DB gelöscht (Prüfung via Direktabfrage nach Methodenaufrufen)** |
| `CreateTestPlaylistWithEntries()` | `PlaylistServiceTestBase` | Hilfsmethode zur Erstellung von Test-Playlists mit Einträgen |
| `CreateTestMediaEntry()` | `PlaylistServiceTestBase` | Hilfsmethode zur Erstellung von Test-PlaylistEntry-Objekten |
| `POST_AddMedia_Success` | `PlaylistsControllerTests_Entries` | Controller-Endpoint POST `/api/playlists/{id}/entries` erfolgreich |
| `POST_AddMedia_Duplicate_Returns409` | `PlaylistsControllerTests_Entries` | Duplikat-Response ist 409 |
| `POST_AddMedia_BadRequest_Returns400` | `PlaylistsControllerTests_Entries` | Ungültiger Request gibt 400 zurück |
| `POST_AddMedia_NotFound_Returns404` | `PlaylistsControllerTests_Entries` | Medieninhalt nicht vorhanden → 404 |
| `POST_AddMedia_Forbidden_Returns403` | `PlaylistsControllerTests_Entries` | Nicht-Besitzer → 403 |
| `DELETE_RemoveMedia_Success` | `PlaylistsControllerTests_Entries` | DELETE-Endpoint erfolgreich (204) |
| `DELETE_RemoveMedia_NotFound_Returns404` | `PlaylistsControllerTests_Entries` | Eintrag nicht vorhanden → 404 |
| `DELETE_RemoveMedia_Forbidden_Returns403` | `PlaylistsControllerTests_Entries` | Nicht-Besitzer → 403 |
| `GET_GetEntries_Success` | `PlaylistsControllerTests_Entries` | GET-Endpoint gibt alle Einträge zurück |
| `GET_GetEntries_EmptyArray` | `PlaylistsControllerTests_Entries` | Leere Playlist gibt leeres Array zurück |
| `GET_GetEntries_Forbidden_Returns403` | `PlaylistsControllerTests_Entries` | Nicht-Besitzer → 403 |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistDetailE2ETests` | PlaylistDetail.razor wird erweitert um Einträge-Anzeige; E2E-Tests müssen möglicherweise aktualisiert werden, um neue UI-Elemente nicht zu brechen oder gezielt zu testen. |
| `PlaylistServiceTests_GetPlaylist` (oder ähnlich) | Falls bestehende Tests einen Eintrag in GetPlaylistEntriesAsync prüfen, müssen diese ggf. angepasst werden, wenn die zu testenden Medieninhalte simuliert gelöscht werden. |
| (Keine anderen direkt betroffen) | PlaylistService/Controller CRUD-Tests für Playlists selbst bleiben ungeändert, da nur neue Methoden hinzugefügt werden. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Benutzer fügt Film zu Playlist hinzu, Film erscheint in Liste | `PlaylistEntriesE2ETests` oder erweiterte `PlaylistDetailE2ETests` | Medieninhalt hinzufügen (Happy Path) | E2E testet den gesamten Benutzerfluss vom Browser über UI bis zur Datenbankabfrage. Unit-Tests der Service-Methode allein zeigen nicht, ob die UI korrekt auf den neuen Eintrag reagiert. |
| Pflicht | Benutzer entfernt Film aus Playlist, Film verschwindet | `PlaylistEntriesE2ETests` | Medieninhalt entfernen (Happy Path) | Wie oben: vollständiger Benutzerfluss notwendig. |
| Pflicht | Benutzer versucht, Film zweimal hinzuzufügen; 2. Versuch zeigt Duplikat-Fehlermeldung | `PlaylistEntriesE2ETests` | Duplikat-Handling mit UI-Feedback | E2E zeigt, dass Toast/Alert korrekt angezeigt wird und Benutzer versteht, was schiefgegangen ist. |
| Pflicht | Benutzer fügt Serie hinzu; alle Staffeln und Episoden erscheinen | `PlaylistEntriesE2ETests` | Cascade-Logik funktioniert von Benutzer-Perspektive | E2E validiert, dass Cascade-Automatik tatsächlich alle erwarteten Einträge in der UI anzeigt. |
| Empfohlen | Benutzer ohne Berechtigung versucht, Film zu anderer Playlist hinzuzufügen; erhält Fehler 403 | `PlaylistEntriesE2ETests` | Berechtigungsprüfung funkioniert | E2E mit mehreren Benutzern simuliert echte Berechtigungsszenarios. |
| Empfohlen | Benutzer fügt große Serie mit vielen Episoden hinzu; UI bleibt responsiv | `PlaylistEntriesE2ETests` (Performance-Test) | Performance bei Cascade-Abruf | Unit-Tests können große Datenmengen nicht realistisch testen; E2E mit Browser zeigt UI-Responsivität. |
| Empfohlen | **Benutzer hat Film in Playlist, Film wird danach gelöscht; beim Neuladen der Playlist ist der Film verschwunden (still, ohne Fehler)** | `PlaylistEntriesE2ETests` oder erweiterte `PlaylistDetailE2ETests` | **Verwaiste Einträge werden still entfernt** | **E2E validiert, dass die Bereinigung verwaister Einträge tatsächlich funktioniert und dass kein Fehler/Log für den Nutzer sichtbar ist.** |

**Begründung für E2E-Notwendigkeit:** Alle neuen Benutzerflüsse (Hinzufügen, Entfernen, Duplikat-Feedback, Cascade-Anzeige) sind über die PlaylistDetail-UI erreichbar und sichtbar. Unit-Tests der Service-Methoden können die Geschäftslogik validieren, aber nicht, dass die UI korrekt auf API-Responses reagiert, dass Fehler-Toast korrekt angezeigt werden, oder dass die Liste nach Operationen aktualisiert wird. E2E-Tests sind daher nicht optional.

**Betroffene bestehende E2E-Tests:**
| Test / Testklasse | Grund der Anpassung |
|---|---|
| `PlaylistDetailE2ETests` (alle) | PlaylistDetail.razor wird erweitert; bestehende Tests, die die Komponente laden, können durch neue UI-Elemente beeinträchtigt sein (z. B. wenn Tests spezifische Element-Selektoren nutzen). Überprüfung und ggf. Aktualisierung erforderlich. |

## Offene Punkte

Keine offenen Punkte. Alle vier Punkte aus dem bisherigen Entwurf wurden durch Projektentscheidungen geklärt:

1. **Titel-Nachladen:** Direkter Lookup via `IMediaService` beim Konvertieren zu DTO ✓
2. **IMediaService-Methoden:** Expliziter Rechercheschritt in Umsetzungsreihenfolge (Schritt 7) ✓
3. **Performance bei Cascade:** Kein hartes Limit, aber Performance im Blick behalten (z. B. Batch-Insert) ✓
4. **Verwaiste Einträge:** Bereinigung "on read" in `GetPlaylistEntriesAsync` umgesetzt (stille Löschung, kein Scheduled Job erforderlich) ✓

---

**Erstellungsdatum:** 2026-09-05  
**Status:** Planung abgeschlossen, bereit zur Implementierung
