# Umsetzungsplan: Namenssuche für Playlist-Einträge statt Roher Medien-IDs

## Übersicht

Die aktuelle Implementierung erfordert von Benutzern, dass sie interne Datenbank-IDs von Medieninhalten kennen und manuell eingeben – dies ist nicht praktikabel. Der Plan sieht vor, die Eingabe-UI (`PlaylistEntriesList.razor` Zeilen 22–30) von einem Dropdown + Number-Input durch eine echte Namenssuche/Auswahl-Oberfläche (neue Komponente `MediaSearchSelector.razor`) zu ersetzen. Diese muss alle fünf Medientypen unterstützen. Die Backend-Suche wird durch Erweiterung von `ItemsController.Get` ermöglicht, um alle fehlenden Medientypen (Movies, TVShowSeasons, TVShowEpisodes) nachzutragen.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Such-Endpoint-Strategie | Erweiterung des bestehenden `ItemsController.Get` um Movies, TVShowSeasons, TVShowEpisodes | Minimiert Duplikation, nutzt bestehende Zugriffskontroll-Infrastruktur (MediaSourceUsers, IUnlockedMediaService). Risiko für bestehende Aufrufer ist gering, da neue Typen nur hinzugefügt werden. |
| Fehlerhafte Type-Setter (MovieCollection) | Behebung des Bugs: Type wird korrekt als "MovieCollection" statt "Movie" gesetzt | Semantische Korrektheit; bestehende Aufrufer von `ItemsController.Get` erhalten dann korrekte Typ-Werte. Dies ist ein Korrektur-Commit und sollte in nachfolgenden Test-Runs validiert werden. |
| MediaSearchSelector-Komponente | Inspiziert von `Actors.razor` (Live-Suchmuster) und nutzt `MediaBox.razor` für Ergebnis-Anzeige | Konsistent mit bewährten Patterns im Projekt; Live-Suche via `@bind:event="oninput"`; EventCallback-basierte Auswahl. |
| Client-Suchmethode | Nutze bestehende `ItemsController.Get`-Methode über generische `VideoWebPlayerClient`-HTTP-Helfer statt neue `IPlaylistApiClient`-Methode | Einfacher, keine neuen Interface-Methoden erforderlich, VideoWebPlayerClient ist bereits die zentrale HTTP-Facade. |
| Bild-Auflösung in Such-Ergebnissen | MediaEntryDto.PictureId wird von MediaSearchSelector direkt verwendet; keine zusätzliche serverseitige Auflösung nötig | `MediaEntryDto` liefert bereits die Rohdaten (Type, Id, Title, PictureId); die UI kümmert sich um Fallback (Poster → Banner → Fanart). |

---

## Programmabläufe

### Benutzerfluss: Neue Eingabe-Oberfläche für Playlist-Einträge

1. Benutzer öffnet Playlist-Detail-Seite (`PlaylistDetailPage.razor`)
2. `PlaylistEntriesList.razor` wird geladen; der alte Dropdown + Number-Input wird durch `<MediaSearchSelector @ref="searchSelectorRef" OnMediaSelected="OnMediaSelectedAsync" />` ersetzt
3. Benutzer gibt einen Suchbegriff in das Eingabefeld der `MediaSearchSelector` ein (z. B. „Breaking")
4. Bei jeder Eingabeänderung (oninput-Event) wird ein HTTP GET an `/api/items?search=Breaking&page=0&size=30` abgesetzt
5. Backend (`ItemsController.Get`) durchsucht alle 5 Medientypen nach Name-Match:
   - Movies.Name LIKE search
   - TVShows.Name LIKE search
   - TVShowSeasons.Name LIKE search
   - TVShowEpisodes.Name LIKE search
   - MovieCollections.Name LIKE search
6. Zugriffskontrolle (MediaSourceUsers + IUnlockedMediaService) wird angewendet; nur zugängliche Inhalte werden zurückgegeben
7. Ergebnisse werden als `List<MediaEntryDto>` serialisiert und zum Client zurückgegeben
8. `MediaSearchSelector` rendert Ergebnisse als MediaBox-Kacheln in einer virtuellen Liste
9. Benutzer klickt auf ein Such-Ergebnis
10. `OnMediaSelected`-EventCallback wird aufgerufen mit `(mediaType: "Movie", mediaId: 123)`
11. `PlaylistEntriesList.OnMediaSelectedAsync` wird aufgerufen, setzt `newEntryMediaType` und `newEntryMediaId` und ruft `AddEntryAsync` auf
12. `AddEntryAsync` sendet `DtoAddMediaToPlaylistRequest` an `/api/playlists/{id}/entries`
13. Backend (`PlaylistsController.AddMediaToPlaylist`) ruft `PlaylistService.AddMediaToPlaylistAsync` auf
14. `PlaylistService` führt Duplikat-Check, Kaskaden-Auflösung (falls TVShow/TVShowSeason) und Zugriffsschutz durch (später beim Abruf)
15. Neue Einträge werden in DB gespeichert, Bestätigungs-Nachricht wird angezeigt
16. Einträge-Tabelle wird neu geladen und angezeigt

Beteiligte Klassen/Komponenten: `MediaSearchSelector.razor`, `PlaylistEntriesList.razor`, `MediaBox.razor`, `ItemsController`, `PlaylistsController`, `PlaylistService`, `IUnlockedMediaService`

### Backend-Ablauf: Erweiterung der Medien-Suche in ItemsController.Get

1. HTTP GET `/api/items?search={term}&page={p}&size={s}` wird empfangen
2. Benutzer-Autorisierung wird geprüft
3. Alle 5 Medientypen werden durchsucht (neu: Movies, TVShowSeasons, TVShowEpisodes):
   - QueryableBuilder für jeden Typ: Name-Filter, Genre-Filter (falls genreId), MediaSource-Filter (falls mediaSourceId)
   - Ergebnisse in `MediaEntryDto` konvertieren; **Type wird korrekt gesetzt** (nicht "Movie" für MovieCollections)
   - Zugriffskontrolle: `MediaSourceUsers` + `GetUnlockedMovieIdsForUserAsync` (neu) / `GetUnlockedTVShowSeasonIdsForUserAsync` (neu) / `GetUnlockedTVShowEpisodeIdsForUserAsync` (neu)
4. Alle Listen zusammenführen
5. Nach Title sortieren und paging (skip/take) anwenden
6. `List<MediaEntryDto>` zurückgeben

Beteiligte Klassen/Komponenten: `ItemsController`, `IUnlockedMediaService`, `MediaEntryDto`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `MediaSearchSelector.razor` | Razor-Komponente | Suchfeld mit Live-Suche über alle 5 Medientypen. Rendert Ergebnisse als MediaBox-Kacheln. EventCallback `OnMediaSelected` gibt (MediaType, MediaId) zurück. Inspiriert von `Actors.razor` und `MediaBox.razor`. |

---

## Änderungen an bestehenden Klassen

### `ItemsController.cs` (Controller)

- **Geänderte Methoden:**
  - `Get(...)` (Zeilen 116–226) — Erweitert um:
    - Queries für `Movies`, `TVShowSeasons`, `TVShowEpisodes` neben bestehenden `MovieCollections` und `TVShows`
    - Korrekte Type-Setter: Type wird statt `"Movie"` für MovieCollections korrekt als `"MovieCollection"` gesetzt (Bugfix)
    - Nutzen von `IUnlockedMediaService.GetUnlockedMovieIdsForUserAsync` (neu), `GetUnlockedTVShowSeasonIdsForUserAsync` (neu), `GetUnlockedTVShowEpisodeIdsForUserAsync` (neu) für Zugriffskontrolle auf neue Typen
    - Logik bleibt sonst identisch: Filterung nach mediaSourceId, Name-Suche, Genre-Filter, Paging

### `IUnlockedMediaService` (Interface)

- **Neue Methoden:**
  - `GetUnlockedMovieIdsForUserAsync(string userId)` → `Task<long[]>` — Gibt alle Movie-Ids zurück, die für den Benutzer entsperrt sind (analog zu bestehenden GetUnlockedMovieCollectionIdsForUserAsync / GetUnlockedTVShowIdsForUserAsync)
  - `GetUnlockedTVShowSeasonIdsForUserAsync(string userId)` → `Task<long[]>` — Analog für TVShowSeasons
  - `GetUnlockedTVShowEpisodeIdsForUserAsync(string userId)` → `Task<long[]>` — Analog für TVShowEpisodes

### `UnlockedMediaService` (Implementierung)

- **Neue Methoden:**
  - `GetUnlockedMovieIdsForUserAsync(string userId)` → `Task<long[]>` — Implementierung: Datenbankabfrage nach Rows in [Unlock-Tabelle] (vermutlich `UnlockedMovies`) gefiltert nach userId
  - `GetUnlockedTVShowSeasonIdsForUserAsync(string userId)` → `Task<long[]>` — Analog
  - `GetUnlockedTVShowEpisodeIdsForUserAsync(string userId)` → `Task<long[]>` — Analog

**Abhängigkeit:** Die genaue Implementierung dieser Methoden hängt davon ab, wie Unlocks in der Datenbank gespeichert sind (z. B. über `UnlockedMovies`, `UnlockedTVShowSeasons`, `UnlockedTVShowEpisodes`-Tabellen oder eine einheitliche Unlock-Tabelle). Dies müsste aus dem bestehenden Code für MovieCollections/TVShows abgelesen werden.

### `PlaylistEntriesList.razor` (Razor-Komponente)

- **Geänderte UI-Abschnitte:**
  - Zeilen 22–30: Die aktuelle Dropdown + Number-Input wird **vollständig ersetzt** durch:
    ```razor
    <MediaSearchSelector @ref="searchSelectorRef" OnMediaSelected="OnMediaSelectedAsync" />
    ```
  
- **Neue Methoden:**
  - `OnMediaSelectedAsync(string mediaType, long mediaId)` → `Task` — Callback von `MediaSearchSelector`; setzt `newEntryMediaType` und `newEntryMediaId` und ruft `AddEntryAsync()` auf
  
- **Gelöschte / Nicht mehr benötigte:**
  - Properties `newEntryMediaType`, `newEntryMediaId` können entfernt werden, da MediaSearchSelector diese Zuständigkeit übernimmt (alternativ: Properties werden nur noch intern genutzt)
  - Das `<select>`-Element mit den MediaTypeValues-Options
  - Das `<input type="number">` für die ID
  
  **Alternative Implementierung:** Properties bleiben zur Vereinfachung der Komponenten-Kommunikation erhalten; MediaSearchSelector bindet diese nur indirekt.

- **Bestehende Methoden bleiben unverändert:**
  - `AddEntryAsync()` — Hat keine Änderung (nutzt weiterhin `newEntryMediaType` und `newEntryMediaId`)
  - `RemoveEntryAsync()`, `MoveEntryToBeginningAsync()`, `MoveEntryToEndAsync()` — Unverändert
  - `ItemsProviderAsync` — Unverändert
  - Drag & Drop Handling — Unverändert

### `VideoWebPlayerClient.cs` (HTTP-Facade)

- **Neue Methode (oder Overload bestehender HTTP-Helper):**
  - Optional: `RequestItemsAsync(string search, int page = 0, int size = 30, CancellationToken cancellationToken = default)` → `Task<List<MediaEntryDto>>` — Wrapper um HTTP GET `/api/items?search={search}&page={page}&size={size}`
  
  **Alternative:** MediaSearchSelector ruft `HttpGetAsync<List<MediaEntryDto>>` direkt auf (via Dependency Injection von VideoWebPlayerClient). Keine neue Methode erforderlich.

### `MediaEntryDto.cs` (DTO)

- **Keine Änderung erforderlich** — Alle benötigten Felder sind bereits vorhanden (Type, Id, Title, PictureId)

---

## Datenbankmigrationen

Keine. Die notwendigen Unlock-Tabellen für Movies, TVShowSeasons und TVShowEpisodes sollten bereits existieren (da `UnlockedMediaService` bereits solche Methoden für diese Typen nutzen kann). Falls sie nicht existieren, müssen sie in einem separaten Datenbankschritt angelegt werden — dies ist jedoch nicht Teil dieser Anforderung.

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Suchbegriff in MediaSearchSelector | Mindestens 1 Zeichen oder leer (für Zurücksetzen) | Leere Suchergebnisse; leerer String zurücksetzen ist valide |
| MediaType aus Such-Ergebnissen | Muss einer der Werte aus `MediaTypeValues` sein (Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection) | Backend würde 400 BadRequest zurückgeben wenn ungültiger Typ an PlaylistsController.AddMediaToPlaylist gesendet wird |
| MediaId aus Such-Ergebnissen | Muss > 0 sein | PlaylistService.AddMediaToPlaylistAsync wirft ArgumentException bei mediaId ≤ 0 |
| Zugriffskontrolle in ItemsController.Get | Benutzer muss entweder Zugriff auf die MediaSource haben ODER das Medium muss in UnlockedMedia-Tabelle eingetragen sein | Such-Ergebnis wird gefiltert und nicht zurückgegeben |

---

## Konfigurationsänderungen

Keine.

---

## Seiteneffekte und Risiken

- **ItemsController.Get Erweiterung — Betroffene bestehende Aufrufer:**
  - Jede UI oder jedes Script, das `ItemsController.Get` aufruft, erhält nun zusätzliche Medientypen in den Such-Ergebnissen
  - Das Bugfix (Type korrekt als "MovieCollection" statt "Movie") könnte bestehenden Code brechen, der hardcoded auf Type="Movie" prüft
  - **Risiko:** Gering; die Erweiterung ist additiv (neue Typen hinzugefügt). Das Bugfix ist eine Semantik-Korrektur und sollte als solche dokumentiert werden.
  - **Mitigierung:** In Release Notes dokumentieren; bestehende Aufrufer sollten ohnehin Type-String-Vergleiche über `MediaTypeValues`-Konstanten machen.

- **IUnlockedMediaService Erweiterung:**
  - Neue Methoden müssen in allen Implementierungen hinzugefügt werden
  - Falls IUnlockedMediaService Implementierungen per DI registriert sind, sollten sie automatisch verfügbar sein

- **PlaylistEntriesList.razor UI-Änderung:**
  - Bestehende E2E Tests, die den alten Dropdown + Number-Input ansteuern, werden fehlschlagen
  - **Betroffene Tests:** PlaylistDetailE2ETests, PlaylistEntriesE2ETests (siehe Tests-Sektion)

- **Performance-Überlegung:**
  - Live-Suche bei jeder Eingabeänderung könnte zu vielen HTTP-Requests führen
  - Mitigierung: Debounce im MediaSearchSelector (z. B. 300–500ms) implementieren, damit nicht auf jedem Keystroke eine Abfrage erfolgt
  - Alternativ: Suchbegriff muss mindestens N Zeichen lang sein bevor Suche gestartet wird

---

## Umsetzungsreihenfolge

1. **Backend-Erweiterung vorbereiten: IUnlockedMediaService erweitern**
   - Voraussetzungen: Keine (Interface-Definition)
   - Beschreibung: Füge drei neue Methoden hinzu: `GetUnlockedMovieIdsForUserAsync`, `GetUnlockedTVShowSeasonIdsForUserAsync`, `GetUnlockedTVShowEpisodeIdsForUserAsync` zu `IUnlockedMediaService`-Interface. Signatures orientieren sich an bestehenden Methoden.

2. **Backend-Implementierung: UnlockedMediaService erweitern**
   - Voraussetzungen: Schritt 1 (neue Interface-Methoden definiert)
   - Beschreibung: Implementiere die drei neuen Methoden in `UnlockedMediaService.cs`. Abfragen sollten die entsprechenden DB-Tabellen durchsuchen (UnlockedMovies, UnlockedTVShowSeasons, UnlockedTVShowEpisodes oder ähnlich) nach Rows mit UserId == userId und geben die Ids als Array zurück.

3. **Backend-Bugfix und Erweiterung: ItemsController.Get anpassen**
   - Voraussetzungen: Schritt 2 (neue UnlockedMediaService-Methoden vorhanden)
   - Beschreibung: 
     - **Bugfix:** Type-Setter für MovieCollections: ändere `Type = "Movie"` zu `Type = "MovieCollection"`
     - **Erweiterung:** Füge Queries für Movies, TVShowSeasons, TVShowEpisodes hinzu (analog zu bestehenden MovieCollections/TVShows). Jede Query nutzt den entsprechenden `DbSet`, wendet die gleichen Filter an (mediaSourceId, search, genreId) und nutzt die neue UnlockedMediaService-Methode für Zugriffskontrolle.

4. **Client-Suchmethode bereitstellen (optional)**
   - Voraussetzungen: Schritt 3 (ItemsController.Get erweitert)
   - Beschreibung: Optional: Neue öffentliche Methode `RequestItemsAsync` zu `VideoWebPlayerClient` hinzufügen als Wrapper um HTTP GET `/api/items?search=...&page=...&size=...`. Falls implementiert, stellt diese Methode eine saubere API für die neue Komponente bereit.

5. **Neue Komponente erstellen: MediaSearchSelector.razor**
   - Voraussetzungen: Schritt 4 (oder Schritt 3 falls Schritt 4 übersprungen) — ItemsController.Get muss erweitert sein
   - Beschreibung: Erstelle `VideoWebPlayer/Components/Playlists/MediaSearchSelector.razor` mit:
     - Eingabefeld: `<input type="text" @bind="searchTerm" @bind:event="oninput" />`
     - Lade-Indikator während HTTP-Request läuft
     - Debounce-Logik (~300–500ms) um zu viele Requests zu vermeiden
     - HTTP GET an `/api/items?search={searchTerm}` (via VideoWebPlayerClient oder HTTP-Helper)
     - Ergebnis-Rendering: MediaBox.razor für jedes Suchergebnis (oder virtuelle Liste falls viele Ergebnisse)
     - Callback: `OnMediaSelected` EventCallback wird mit (mediaType, mediaId) aufgerufen bei Klick auf Ergebnis
     - Inspiriert von Actors.razor (Live-Such-Pattern) und MediaBox.razor (Anzeige-Pattern)

6. **Komponenten-Integration: PlaylistEntriesList.razor anpassen**
   - Voraussetzungen: Schritt 5 (MediaSearchSelector vorhanden)
   - Beschreibung:
     - Ersetze Zeilen 22–30 (Dropdown + Number-Input) durch `<MediaSearchSelector @ref="searchSelectorRef" OnMediaSelected="OnMediaSelectedAsync" />`
     - Füge neue Methode `OnMediaSelectedAsync(string mediaType, long mediaId)` hinzu, die `newEntryMediaType` und `newEntryMediaId` setzt und `AddEntryAsync()` aufruft
     - Optional: Entferne die nunmehr ungenutzte UI für manuelles Dropdown/Input; behalte aber Properties für interne Nutzung

7. **Unit Tests für neue Backend-Methoden schreiben**
   - Voraussetzungen: Schritt 2, 3 (neue Methoden implementiert)
   - Beschreibung: 
     - Tests für UnlockedMediaService-Methoden (GetUnlockedMovieIdsForUserAsync, etc.) — analog zu bestehenden GetUnlockedMovieCollectionIdsForUserAsync-Tests
     - Tests für ItemsController.Get mit Movies, TVShowSeasons, TVShowEpisodes im Suchbegriff — prüfe, dass alle Typen zurückgegeben und korrekt gefiltert werden
     - Tests für Zugriffskontrolle: Benutzer ohne Zugriff sollte keine Ergebnisse sehen
     - Bugfix-Tests: Type-Feld wird korrekt gesetzt für alle Typen

8. **Unit Tests für neue Komponente schreiben**
   - Voraussetzungen: Schritt 5 (MediaSearchSelector vorhanden)
   - Beschreibung:
     - Tests für MediaSearchSelector (bUnit oder ähnlich): Input-Bindung, Debounce-Logik, HTTP-Call bei Suchbegriff-Änderung, EventCallback wird mit korrekten Parametern aufgerufen

9. **E2E Tests anpassen: Betroffene Tests updaten**
   - Voraussetzungen: Schritt 6 (PlaylistEntriesList angepasst), Schritt 5 (MediaSearchSelector vorhanden)
   - Beschreibung:
     - PlaylistDetailE2ETests: Anpasse Tests, die den alten Dropdown + Number-Input ansteuern — nutze neue MediaSearchSelector statt dessen (CSS-Selektoren/Komponenten-Referenzen updaten)
     - PlaylistEntriesE2ETests: Analog
     - PlaylistReorderE2ETests: Prüfe, ob Betroffung nötig (wahrscheinlich nicht, da nur Reordering getestet)

10. **Neue E2E Tests schreiben: Medien-Sucherlebnis testen**
    - Voraussetzungen: Schritt 5 (MediaSearchSelector vorhanden), Schritt 3 (ItemsController.Get erweitert)
    - Beschreibung:
      - E2E Test: Benutzer gibt Suchbegriff in MediaSearchSelector ein, wählt Film aus Such-Ergebnis aus, Film wird zu Playlist hinzugefügt
      - E2E Test: Suche nach Serie mit Kaskaden-Auflösung (alle Episoden werden hinzugefügt)
      - E2E Test: Suche nach Staffel, wählt Staffel aus, alle Episoden dieser Staffel werden hinzugefügt
      - E2E Test: Suche nach Episode, Episode wird hinzugefügt (kein Cascade)
      - E2E Test: Suche nach Filmsammlung, Sammlung wird hinzugefügt
      - E2E Test: Benutzer ohne Zugriff auf Medium — Medium erscheint nicht in Such-Ergebnissen
      - E2E Test: Leere Suchergebnisse — aussagekräftige Nachricht wird angezeigt

11. **Dokumentation aktualisieren (falls nötig)**
    - Voraussetzungen: Schritt 6 (Umsetzung abgeschlossen), Schritt 10 (Tests abgeschlossen)
    - Beschreibung:
      - `docs/help/playlists.md`: Dokumentiere die neue Namenssuche-Oberfläche statt Roher ID-Eingabe. Zeige Bildschirmaufnahmen oder Bedienanleitung.
      - `docs/help/playlists-api.md`: Falls API-Dokumentation vorhanden, notiere, dass ItemsController.Get nun alle 5 Medientypen unterstützt und Type-Setter korrekt ist.
      - `README.md`: Optional; falls relevante Playlist-Features dokumentiert sind, erwähne die neue Suche-Funktionalität.

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetUnlockedMovieIds_UserHasUnlockedMovies_ReturnsMovieIds` | `VideoWebPlayer.Tests/Services/UnlockedMediaServiceTests.cs` (neu/erweitert) | Prüft, dass `GetUnlockedMovieIdsForUserAsync` korrekt Movie-Ids aus DB abruft |
| `GetUnlockedTVShowSeasonIds_UserHasUnlockedSeasons_ReturnsSeasonIds` | Analog | Analog für TVShowSeasons |
| `GetUnlockedTVShowEpisodeIds_UserHasUnlockedEpisodes_ReturnsEpisodeIds` | Analog | Analog für TVShowEpisodes |
| `Get_SearchMovies_ReturnsMatchingMovies` | `VideoWebPlayer.Tests/Controllers/ItemsControllerTests.cs` (neu/erweitert) | ItemsController.Get mit search-Parameter für Movies liefert nur Filme mit passendem Namen |
| `Get_SearchTVShowSeasons_ReturnsMatchingSeasons` | Analog | Analog für Staffeln |
| `Get_SearchTVShowEpisodes_ReturnsMatchingEpisodes` | Analog | Analog für Episoden |
| `Get_TypeFieldCorrect_MovieCollectionType` | Analog | Bugfix-Test: Type wird korrekt als "MovieCollection" (nicht "Movie") gesetzt |
| `Get_AllTypes_UserNoAccess_ReturnEmptyList` | Analog | Zugriffskontrolle: Benutzer ohne Zugriff erhält leere Such-Ergebnisse für alle Typen |
| `Get_AllTypes_UserHasAccess_IncludesMoviesSeasonEpisodes` | Analog | Zugriffskontrolle: Benutzer mit Zugriff sieht alle 5 Medientypen in Ergebnissen |
| `SearchTermInput_DebounceWorks_RespectsDelay` | `VideoWebPlayer.Tests/Components/MediaSearchSelectorTests.cs` (neu) | Prüft Debounce-Logik: mehrere schnelle Input-Änderungen → nur eine HTTP-Abfrage nach Debounce-Verzögerung |
| `EventCallback_OnMediaSelected_InvokedWithCorrectParameters` | Analog | Prüft, dass OnMediaSelected-Callback mit (mediaType, mediaId) aufgerufen wird |
| `HttpCall_ItemsEndpoint_ReceivesCorrectUrl` | Analog | Prüft, dass HTTP GET korrekte URL mit search-Parameter baut |
| `PlaylistEntriesList_OnMediaSelectedAsync_CallsAddEntryAsync` | `VideoWebPlayer.Tests/Components/PlaylistEntriesListTests.cs` (angepasst) | Integration-Test: MediaSearchSelector-Callback ruft AddEntryAsync auf |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| PlaylistDetailE2ETests.cs | UI-Änderung: Alter Dropdown + Number-Input existiert nicht mehr. CSS-Selektoren/Komponenten-Interaktionen müssen auf MediaSearchSelector angepasst werden. |
| PlaylistEntriesE2ETests.cs | UI-Änderung: Tests für "Medien zu Playlist hinzufügen" müssen neue MediaSearchSelector-Interaktion statt Dropdown+Input verwenden. |
| PlaylistReorderE2ETests.cs | Wahrscheinlich **nicht betroffen**, da Reordering über Drag & Drop erfolgt; die Add-UI wird nicht getestet. Prüfung erforderlich. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Happy Path: Film suchen, Auswahl treffen, zu Playlist hinzufügen | PlaylistDetailE2ETests (neu/erweitert) | Benutzer kann Film anhand Name suchen und hinzufügen | UI-Interaktion (Eingabe, Suchergebnisse, Klick, Bestätigung) kann nur E2E getestet werden |
| Pflicht | Serie hinzufügen triggert Kaskaden-Auflösung | PlaylistDetailE2ETests (neu) | Serie wird mit allen Episoden hinzugefügt | Kaskaden-Logik ist geschäftskritisch; E2E-Test sichert ab, dass UI + Backend + DB zusammenspielen |
| Pflicht | Staffel auswählen → alle Episoden dieser Staffel hinzufügen | PlaylistDetailE2ETests (neu) | Staffel wird mit Episoden hinzugefügt | Kaskaden-Logik für Staffeln |
| Pflicht | Episode auswählen (kein Cascade) | PlaylistDetailE2ETests (neu) | Einzelne Episode wird hinzugefügt | Keine Cascade — prüft korrektes Verhalten für einfachen Typ |
| Pflicht | Filmsammlung suchen und hinzufügen | PlaylistDetailE2ETests (neu) | Sammlung wird hinzugefügt | Abdeckung aller 5 Typen |
| Pflicht | Benutzer ohne Zugriff — Medium nicht in Suchergebnissen | PlaylistDetailE2ETests (neu) | Zugriffskontrolle funktioniert (Medium nicht in Such-Ergebnissen) | Sicherheitskritisch; E2E validiert, dass Backend-Filter und UI zusammenpassen |
| Pflicht | Leere Suchergebnisse — Nachricht angezeigt | PlaylistDetailE2ETests (neu) | "Keine Ergebnisse" oder ähnliche Nachricht wird angezeigt | UX-Kriterium; nur über E2E testbar |
| Optional | Duplikat-Add verhindern (Medium existiert bereits) | PlaylistEntriesE2ETests (erweitert) | Doppelt-Klick auf gleiche Media → Duplikat wird übersprungen (oder Fehler) | Duplikat-Handling ist Unit-getestet; E2E kann aber verifizieren, dass UI-Flow stimmt |
| Optional | Drag & Drop funktioniert nach Suche+Add | PlaylistReorderE2ETests (prüfen ob betroffen) | Neue Einträge können per Drag & Drop reordered werden | Prüft Integr. zwischen neuer Suche+Add und bestehendem Reorder-Feature |

### Bestehende E2E-Tests anpassen

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| PlaylistDetailE2ETests (mehrere Tests) | UI-Änderung: Tests steuern weiterhin Add-Funktionalität an, müssen aber Selektoren und Interaktionen anpassen. Statt `select.SetValue("Movie")` + `input.SetValue("123")` + `button.Click()` → neuer Flow via MediaSearchSelector (Textfeld, Suche, Ergebnis-Klick). |
| PlaylistEntriesE2ETests (mehrere Tests) | Analog |

---

## Offene Punkte

Keine. Alle Designentscheidungen und technischen Fragen sind in der Anforderung und Bestandsaufnahme beantwortet:

- **Endpoint-Strategie:** ✅ Entschieden — Erweiterung von `ItemsController.Get`
- **UI-Komponente:** ✅ Entschieden — Neue `MediaSearchSelector.razor` inspiriert von Actors.razor
- **Client-Suchmethode:** ✅ Entschieden — Nutze `VideoWebPlayerClient`-HTTP-Helper (optional: neue Wrapper-Methode)
- **Bild-Auflösung:** ✅ Entschieden — MediaEntryDto.PictureId wird direkt genutzt
- **Debounce-Strategie:** ✅ Empfohlen — ~300–500ms Debounce für Performance

---

## Zusammenfassung der kritischen Pfade

1. **Backend: Drei neue Interface-Methoden** → Implementierung in Service → Integration in ItemsController.Get
2. **Backend: ItemsController.Get Bugfix + Erweiterung** — Additive Änderung, gering Risiko
3. **Frontend: Neue MediaSearchSelector-Komponente** — Inspiriert von bewährten Patterns (Actors.razor, MediaBox.razor)
4. **Frontend: PlaylistEntriesList.razor Anpassung** — Austausch Input-UI; AddEntryAsync-Logik bleibt
5. **Tests: E2E Tests für neue Sucherlebnis** — Sicherheitskritisch (Zugriffskontrolle) und UX-kritisch
6. **Tests: Unit Tests für neue Backend-Methoden** — Coverage der 5 Medientypen

Das Feature behält alle bestehende Logik (Duplikat-Handling, Kaskaden, Zugriffsschutz) bei und ersetzt nur den Eingabe-Mechanismus.
