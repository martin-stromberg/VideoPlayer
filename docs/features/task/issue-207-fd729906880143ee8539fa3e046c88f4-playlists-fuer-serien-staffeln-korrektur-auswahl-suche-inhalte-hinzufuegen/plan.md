# Umsetzungsplan: Korrektur der Medienauswahl-Suche für Playlists

## Übersicht

Die Implementierung behebt zwei kritische Fehler in der Playlist-Medienauswahl:
1. **Case-sensitive Namenssuche**: Benutzer, die mit abweichender Groß-/Kleinschreibung suchen, finden vorhandene Medien nicht (z. B. Suche nach „breaking bad" findet nicht „Breaking Bad").
2. **Unbeabsichtigte Vergrößerung des Quellen-Browsing**: Die Erweiterung um neue Medientypen liefert beim Quellen-Browsing statt 2 nun 5 Typen, was zu redundanten Einträgen führt.

Die Lösung nutzt einen Opt-in-Parameter (`includeIndividualMediaTypes`), um die Playlist-Suche vom bestehenden Quellen-Browsing sauber zu trennen, und implementiert case-insensitive Suche in der Datenbank.

**Betroffene Bereiche:** Server-Logik (`ItemsController`), Client (`VideoWebPlayerClient`), UI (`MediaSearchSelector.razor`), ViewModel (`MediaSourceDetailsViewModel`), Tests.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Separation Playlist-Suche vs. Quellen-Browsing** | Opt-in-Parameter `includeIndividualMediaTypes` in `MediaEntryFilter` | Option A (Empfehlung aus Anforderung): Minimale Code-Änderung, keine Duplizierung, kein neuer Endpunkt erforderlich. Parameter wird nur von `MediaSearchSelector` gesetzt, Quellen-Browsing und andere Aufrufer setzen ihn nicht (Default: `false`). |
| **Case-insensitive Suche (Technik)** | `.ToLower().Contains()` beiderseits oder `EF.Functions.Like()` mit case-insensitive Flag | Muss mit SQLite-Provider konkret getestet werden. Beide Optionen sind auf SQLite-Ebene funktionsfähig. `ToLower()` ist portabler, `Like` mit Flag-Parameter ist näher an SQL-Standard. **Testentscheidung vor Implementierung erforderlich**. |

## Programmabläufe

### Playlist-Medienauswahl (mit case-insensitiver Suche und allen 5 Medientypen)

1. Benutzer öffnet die Playlist-Medienauswahl-Komponente (`MediaSearchSelector.razor`)
2. Benutzer gibt einen Suchbegriff ein (z. B. „breaking bad", „The Office", „Staffel 1")
3. `MediaSearchSelector` ruft `_client.RequestItemsAsync(search, ...)` auf
4. `VideoWebPlayerClient.RequestItemsAsync()` ruft `RequestItemsCoreAsync(mediaSourceId: null, search: {term}, includeIndividualMediaTypes: true, ...)` auf
5. `RequestItemsCoreAsync()` sendet GET-Request an `/api/items?search={term}&includeIndividualMediaTypes=true` (weitere Parameter wie `page`, `size`)
6. `ItemsController.Get()` empfängt die Parameter und prüft: `includeIndividualMediaTypes == true`
7. `ItemsController.Get()` ruft alle fünf `Get*EntriesAsync()`-Methoden auf (mit `MediaEntryFilter` einschließlich `IncludeIndividualMediaTypes: true`):
   - `GetMovieCollectionEntriesAsync()` — case-insensitive Suche nach `Search`
   - `GetTVShowEntriesAsync()` — case-insensitive Suche nach `Search`
   - `GetMovieEntriesAsync()` — **NUR**, wenn `IncludeIndividualMediaTypes: true`; case-insensitive Suche
   - `GetSeasonEntriesAsync()` — **NUR**, wenn `IncludeIndividualMediaTypes: true`; case-insensitive Suche
   - `GetEpisodeEntriesAsync()` — **NUR**, wenn `IncludeIndividualMediaTypes: true`; case-insensitive Suche
8. Alle Methoden verwenden case-insensitive Vergleich (z. B. `e.Name.ToLower().Contains(filter.Search.ToLower())` oder `EF.Functions.Like(e.Name, $"%{filter.Search}%", "i")`)
9. Ergebnisse werden gefiltert nach Zugriffskontrolle (`UnlockedMediaService`)
10. `ItemsController.Get()` kombiniert Ergebnisse aller Typen, paginiert und liefert `List<MediaEntryDto>` zurück
11. `MediaSearchSelector` zeigt Ergebnisse an

**Beteiligte Klassen/Komponenten:** `MediaSearchSelector.razor`, `VideoWebPlayerClient.cs`, `ItemsController.cs`, `GetMovieCollectionEntriesAsync()`, `GetTVShowEntriesAsync()`, `GetMovieEntriesAsync()`, `GetSeasonEntriesAsync()`, `GetEpisodeEntriesAsync()`, `MediaEntryFilter`, `UnlockedMediaService`

### Quellen-Browsing (weiterhin nur 2 Medientypen, keine Regression)

1. Benutzer navigiert zur Detail-Seite einer Medienquelle (z. B. Netflix, Prime Video)
2. `MediaSourceDetailsViewModel.LoadNextPageAsync()` wird aufgerufen
3. `LoadNextPageAsync()` ruft `_client.RequestSourceItems(mediaSourceId: {id}, ...)` auf
4. `VideoWebPlayerClient.RequestSourceItems()` ruft `RequestItemsCoreAsync(mediaSourceId: {id}, search: null, includeIndividualMediaTypes: false, ...)` auf (oder setzt Parameter gar nicht, da Default `false`)
5. `RequestItemsCoreAsync()` sendet GET-Request an `/api/items?mediaSourceId={id}&includeIndividualMediaTypes=false` (oder Parameter gar nicht gesendet)
6. `ItemsController.Get()` empfängt `mediaSourceId` und `includeIndividualMediaTypes: false` (oder nicht gesetzt)
7. `ItemsController.Get()` ruft nur die zwei ursprünglichen Methoden auf:
   - `GetMovieCollectionEntriesAsync()` — case-insensitive Suche (fallweise)
   - `GetTVShowEntriesAsync()` — case-insensitive Suche (fallweise)
8. `GetMovieEntriesAsync()`, `GetSeasonEntriesAsync()`, `GetEpisodeEntriesAsync()` werden **NICHT** aufgerufen
9. Ergebnisse werden nach `mediaSourceId` gefiltert (bestehende Logik)
10. `MediaSourceDetailsViewModel` erhält nur 2 Medientypen (Regression verhindert)

**Beteiligte Klassen/Komponenten:** `MediaSourceDetailsViewModel.cs`, `VideoWebPlayerClient.cs`, `ItemsController.cs`, `RequestSourceItems()`, `RequestItemsCoreAsync()`

## Neue Klassen

Keine neuen Klassen erforderlich.

## Änderungen an bestehenden Klassen

### `MediaEntryFilter` (Record in `ItemsController.cs`)

- **Neue Eigenschaften:** `IncludeIndividualMediaTypes` (`bool`, Standard: `false`) — Steuert, ob die neuen Medientypen (Movie, TVShowSeason, TVShowEpisode) in das Ergebnis aufgenommen werden. Nur `true` beim Playlist-Suchfluss.

**Lage:** `VideoWebPlayer/Controllers/ItemsController.cs`, Zeile 179 (ungefähr)

### `ItemsController` (`public partial class`)

- **Geänderte Methode:** `Get(long? mediaSourceId, int page, int size, string? search, long? genreId)` 
  - **Änderung:** Logik hinzufügen, um nur `GetMovieEntriesAsync()`, `GetSeasonEntriesAsync()`, `GetEpisodeEntriesAsync()` aufzurufen, wenn `filter.IncludeIndividualMediaTypes == true`. Bestehende Aufrufe (z. B. vom Quellen-Browsing) setzen diesen Parameter nicht oder auf `false`, damit weiterhin nur 2 Typen geliefert werden.
  - **Konstruktion von `MediaEntryFilter`:** Neues Feld `IncludeIndividualMediaTypes` übergeben (Default: `false` oder aus Query-Parameter gelesen, falls vorhanden).
  - **Lage:** `VideoWebPlayer/Controllers/ItemsController.cs`, Zeile ~190–335

- **Geänderte Methode:** `GetMovieCollectionEntriesAsync(MediaEntryFilter filter, ...)`
  - **Änderung:** `.Where(e => e.Name.Contains(filter.Search))` → case-insensitive Variante (z. B. `.Where(e => e.Name.ToLower().Contains(filter.Search.ToLower()))` oder `EF.Functions.Like()`)
  - **Lage:** `VideoWebPlayer/Controllers/ItemsController.cs`, Zeile ~188

- **Geänderte Methode:** `GetTVShowEntriesAsync(MediaEntryFilter filter, ...)`
  - **Änderung:** `.Where(e => e.Name.Contains(filter.Search))` → case-insensitive Variante
  - **Lage:** Zeile ~226

- **Geänderte Methode:** `GetMovieEntriesAsync(MediaEntryFilter filter, ...)`
  - **Änderung:** `.Where(e => e.Name.Contains(filter.Search))` → case-insensitive Variante
  - **Lage:** Zeile ~259

- **Geänderte Methode:** `GetSeasonEntriesAsync(MediaEntryFilter filter, ...)`
  - **Änderung:** `.Where(e => e.Name.Contains(filter.Search))` → case-insensitive Variante
  - **Lage:** Zeile ~292

- **Geänderte Methode:** `GetEpisodeEntriesAsync(MediaEntryFilter filter, ...)`
  - **Änderung:** `.Where(e => e.Name.Contains(filter.Search))` → case-insensitive Variante
  - **Lage:** Zeile ~325

**Lage:** `VideoWebPlayer/Controllers/ItemsController.cs`

### `VideoWebPlayerClient` (`public partial class`)

- **Geänderte Methode:** `RequestItemsCoreAsync(long? mediaSourceId, int page, int size, string? search, long? genreId, CancellationToken ct = default)`
  - **Änderung:** Query-Parameter `includeIndividualMediaTypes` hinzufügen. Wert wird übergeben als `query.Add("includeIndividualMediaTypes", filter.IncludeIndividualMediaTypes.ToString())` (oder ähnlich).
  - **Aber:** Diese Methode ist internal und wird von zwei verschiedenen Aufrufen verwendet (`RequestSourceItems` und `RequestItemsAsync`). Daher eine der folgenden Varianten:
    - **Variante A:** Neuer optionaler Parameter `bool includeIndividualMediaTypes = false` hinzufügen → wird an Query-Parameter propagiert
    - **Variante B:** Neue interne Hilfsmethode, die `includeIndividualMediaTypes` übergeben kann
  - **Empfehlung:** Variante A (neuer Parameter) ist sauberer und konsistent mit anderen Parametern.
  - **Lage:** `VideoWebPlayer.Client/VideoWebPlayerClient.cs`, Zeile ~455 (ungefähr)

- **Geänderte Methode:** `RequestSourceItems(long mediaSourceId, int page, int size, ...)`
  - **Änderung:** Ruft `RequestItemsCoreAsync()` auf mit `includeIndividualMediaTypes: false` (oder setzt Parameter gar nicht, da Default `false`)
  - **Lage:** `VideoWebPlayer.Client/VideoWebPlayerClient.cs` (Datei oder Partial-Datei)

- **Geänderte Methode:** `RequestItemsAsync(string? search, int page, int size, ...)`
  - **Änderung:** Ruft `RequestItemsCoreAsync()` auf mit `includeIndividualMediaTypes: true`
  - **Lage:** `VideoWebPlayer.Client/VideoWebPlayerClient.cs` oder Partial-Datei

**Lage:** `VideoWebPlayer.Client/VideoWebPlayerClient.cs`

### `MediaSearchSelector.razor` (Component)

- **Geänderte Logik:** Komponente ruft weiterhin `Client.RequestItemsAsync()` auf (keine Änderung der Aufruf-Stelle selbst).
  - **Aber:** Wenn `RequestItemsAsync()` intern `includeIndividualMediaTypes: true` setzt (siehe oben), ist keine Änderung der Komponente erforderlich.
  - **Alternativ:** Falls `RequestItemsAsync()` von der Komponente einen Parameter erhält, dann muss Komponente `includeIndividualMediaTypes: true` übergeben.
  - **Empfehlung:** `RequestItemsAsync()` setzt `includeIndividualMediaTypes: true` intern (sauberer); Komponente ändert sich nicht.
  - **Lage:** `VideoWebPlayer/Components/Playlists/MediaSearchSelector.razor`, Zeile ~105

**Lage:** `VideoWebPlayer/Components/Playlists/MediaSearchSelector.razor`

### `MediaSourceDetailsViewModel` (ViewModel-Klasse)

- **Keine direkten Änderungen erforderlich.**
  - Die Methode `LoadNextPageAsync()` ruft weiterhin `_client.RequestSourceItems()` auf (Zeile ~100).
  - `RequestSourceItems()` wird angepasst, um `includeIndividualMediaTypes: false` zu setzen (oder nicht zu setzen, da Default `false`).
  - Das ViewModel selbst ändert sich nicht.
- **Validierung erforderlich:** Nach Implementierung muss getestet werden, dass `LoadNextPageAsync()` weiterhin nur 2 Medientypen empfängt.

**Lage:** `VideoWebPlayer/Services/MediaSourceDetailsViewModel.cs` (oder ähnlich)

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Alle Aufrufer von `ItemsController.Get()` prüfen:** Der neue Parameter `includeIndividualMediaTypes` muss mit einem sinnvollen Default-Wert versehen werden (Standard: `false`). Alle bestehenden Aufrufer, die diesen Parameter nicht setzen, dürfen kein verändertes Verhalten zeigen.
  - **Bekannte Aufrufer:** 
    - `VideoWebPlayerClient.RequestSourceItems()` — setzt `false` (oder setzt nicht, da Default)
    - `VideoWebPlayerClient.RequestItemsAsync()` — setzt `true`
    - Mögliche andere interne Aufrufer (Code-Suche empfohlen)
- **Case-insensitive Suche auf SQLite:** Die Technik (ToLower vs. Like) muss mit dem SQLite EF Core Provider konkret getestet werden, bevor sie in alle 5 Methoden übernommen wird. Unterschiedliches Verhalten in Development (vielleicht ein anderer Provider) vs. Production (SQLite) ist ein Risiko.
- **Bestehende Tests verwenden exakte Groß-/Kleinschreibung:** Alle 480 bestehenden Tests sollten weiterhin grün sein, da sie keine abweichende Groß-/Kleinschreibung verwenden. Aber: Case-insensitive Vergleich könnte unerwartete Trefferquoten erhöhen (z. B. wenn zuvor „Test" nicht „test" fand, jetzt aber beide gefunden werden). Regressions-Tests sind erforderlich.
- **Pagination bei 5 Typen:** Wenn `includeIndividualMediaTypes: true` gesetzt ist, werden 5 statt 2 Typen durchsucht. Die Pagination muss über alle 5 Typen hinweg funktionieren (nicht pro Typ). Die bestehende Implementierung (`page` und `size` Parameter) muss überprüft werden.

## Umsetzungsreihenfolge

1. **Case-insensitive Suche: Technik mit SQLite testen**
   - Voraussetzungen: SQLite-Testdatenbank, ein existierendes Unit-Test-Setup
   - Beschreibung: Kleine Probe-Implementierung (eine Methode, z. B. `GetMovieCollectionEntriesAsync()`) mit zwei Techniken testen (`ToLower().Contains()` vs. `EF.Functions.Like()`), um sicherzustellen, dass beide case-insensitiv auf SQLite funktionieren. Ergebnis dokumentieren, Technik wählen.

2. **`MediaEntryFilter` um `IncludeIndividualMediaTypes` erweitern**
   - Voraussetzungen: `MediaEntryFilter` Record ist vorhanden und anpassbar; alle Aufrufer von `MediaEntryFilter` sind identifiziert
   - Beschreibung: `IncludeIndividualMediaTypes` (`bool`, Standard: `false`) zum Record hinzufügen. Compiler-Fehler bei allen Aufrufen notieren (müssen im nächsten Schritt angepasst werden).

3. **`ItemsController.Get()` anpassen: Bedingte Aufruflogik implementieren**
   - Voraussetzungen: `MediaEntryFilter.IncludeIndividualMediaTypes` vorhanden
   - Beschreibung: In `Get()` Logik hinzufügen: `GetMovieEntriesAsync()`, `GetSeasonEntriesAsync()`, `GetEpisodeEntriesAsync()` nur aufrufen, wenn `filter.IncludeIndividualMediaTypes == true`. `MediaEntryFilter` mit `IncludeIndividualMediaTypes: false` initialisieren (Default-Verhalten für bestehende Aufrufer).

4. **Case-insensitive Suche in alle 5 `Get*EntriesAsync()` Methoden implementieren**
   - Voraussetzungen: Gewählte Technik aus Schritt 1 dokumentiert; alle 5 Methoden identifiziert
   - Beschreibung: `.Where(e => e.Name.Contains(filter.Search))` in allen 5 Methoden (`GetMovieCollectionEntriesAsync()`, `GetTVShowEntriesAsync()`, `GetMovieEntriesAsync()`, `GetSeasonEntriesAsync()`, `GetEpisodeEntriesAsync()`) durch case-insensitive Variante ersetzen. Beispiel: `.Where(e => e.Name.ToLower().Contains(filter.Search.ToLower()))` oder `EF.Functions.Like()`.

5. **`VideoWebPlayerClient.RequestItemsCoreAsync()` um `includeIndividualMediaTypes` Parameter erweitern**
   - Voraussetzungen: `RequestItemsCoreAsync()` ist identifiziert; Alle Aufrufer sind bekannt
   - Beschreibung: Neuer optionaler Parameter `bool includeIndividualMediaTypes = false` hinzufügen. Im Query-String des API-Aufrufs als `&includeIndividualMediaTypes={value}` anhängen.

6. **`VideoWebPlayerClient.RequestSourceItems()` anpassen**
   - Voraussetzungen: `RequestItemsCoreAsync()` hat neuen Parameter; Methode ist identifiziert
   - Beschreibung: Ruft `RequestItemsCoreAsync()` auf mit `includeIndividualMediaTypes: false` (oder setzt Parameter gar nicht, da Default).

7. **`VideoWebPlayerClient.RequestItemsAsync()` anpassen**
   - Voraussetzungen: `RequestItemsCoreAsync()` hat neuen Parameter; Methode ist identifiziert
   - Beschreibung: Ruft `RequestItemsCoreAsync()` auf mit `includeIndividualMediaTypes: true`.

8. **Tests: Case-insensitive Suche hinzufügen**
   - Voraussetzungen: Alle Case-insensitive Implementierungen sind abgeschlossen; Testinfrastruktur (ItemsControllerTestFactory, Hilfsmethoden) ist vorhanden
   - Beschreibung: Neue Testmethoden in `ItemsControllerTests_Search.cs` hinzufügen (z. B. `Get_SearchMovies_CaseInsensitive_ReturnsMatchingMovies()`, die nach „breaking" sucht und „Breaking Bad" findet). Mindestens 3 Tests (Movies, Seasons, Episodes) mit abweichender Groß-/Kleinschreibung.

9. **Tests: Quellen-Browsing-Regression absichern**
   - Voraussetzungen: Opt-in-Parameter ist implementiert und bedingte Aufruflogik funktioniert; Testinfrastruktur vorhanden
   - Beschreibung: Neue Testmethoden in `ItemsControllerTests_Search.cs`:
     - `Get_WithMediaSourceId_WithoutOptIn_Returns2Types()` — Verifiziert, dass ohne `includeIndividualMediaTypes: true` nur MovieCollection und TVShow zurückgegeben werden.
     - `Get_WithMediaSourceId_WithOptIn_Returns5Types()` — Verifiziert, dass mit `includeIndividualMediaTypes: true` alle 5 Typen zurückgegeben werden.
     - `Get_WithoutMediaSourceId_WithoutOptIn_Returns2Types()` — Verifiziert, dass Playlist-Suche ohne `includeIndividualMediaTypes: true` keine individuellen Typen liefert (diesen Fall später durch nachstehenden Test überschreiben).
     - `Get_WithoutMediaSourceId_WithOptIn_Returns5Types()` — Verifiziert, dass Playlist-Suche mit `includeIndividualMediaTypes: true` alle 5 Typen liefert.

10. **Tests: Existing Tests auf Regressions überprüfen**
    - Voraussetzungen: Alle Implementierungen abgeschlossen; alle neuen Tests grün
    - Beschreibung: Alle 480 bestehenden Tests erneut ausführen. Sicherstellen, dass keine Tests durch die case-insensitive Suche oder die Opt-in-Logik fehlschlagen. Falls Tests fehlschlagen, Ursache analysieren und Tests oder Implementierung anpassen (wahrscheinlich: Tests verwenden nicht-gematched Case und erwarten keine Treffer; case-insensitive Suche findet jetzt doch Treffer).

11. **E2E-Tests: Playlist-Medienauswahl mit case-insensitiver Suche**
    - Voraussetzungen: Alle Implementierungen und Unit-Tests abgeschlossen; E2E-Testinfrastruktur vorhanden
    - Beschreibung: Neue E2E-Testmethode in `PlaylistDetailE2ETests.cs` oder `MediaSearchSelectorE2ETests.cs` (falls vorhanden):
      - Szenario: Benutzer öffnet Playlist-Medienauswahl, gibt "breaking" ein, erwartet "Breaking Bad" in Ergebnissen zu sehen.
      - Testet: Browser-Aufruf der Komponente → Eingabe des Suchbegriffs → Verifizierung, dass die richtige Menge und Typen von Ergebnissen angezeigt werden (inklusive Movies, Seasons, Episodes).

12. **E2E-Tests: Quellen-Browsing-Regression absichern (falls E2E-Tests existieren)**
    - Voraussetzungen: E2E-Testinfrastruktur für `MediaSourceDetailsViewModel` oder ähnlich vorhanden
    - Beschreibung: Verifizieren in E2E-Test, dass die Detail-Seite einer Medienquelle nach wie vor nur MovieCollections und TVShows auflistet, nicht einzelne Movies, Seasons oder Episodes (Regression-Test). Falls keine E2E-Tests für Quellen-Browsing existieren, kann dieser Schritt übersprungen werden (Unit-Tests reichen aus).

13. **Dokumentation aktualisieren**
    - Voraussetzungen: Alle Implementierungen und Tests abgeschlossen
    - Beschreibung: 
      - `docs/help/playlists-api.md`: Sektion „Medien-Suche" aktualisieren, um zu dokumentieren, dass die Suche case-insensitiv ist.
      - Controller XML-Kommentare bei `Get()` und `ItemsController` aktualisieren, um neuen Parameter `includeIndividualMediaTypes` zu dokumentieren.
      - Ggf. andere Dokumentationsdateien überprüfen (z. B. `docs/features.md`).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Get_SearchMovies_CaseInsensitive_LowerCase()` | `ItemsControllerTests_Search` | Suche nach „breaking" findet „Breaking Bad" in Movies |
| `Get_SearchMovies_CaseInsensitive_UpperCase()` | `ItemsControllerTests_Search` | Suche nach „BREAKING" findet „Breaking Bad" in Movies |
| `Get_SearchTVShowSeasons_CaseInsensitive()` | `ItemsControllerTests_Search` | Suche nach „staffel" findet „Staffel Eins" in Seasons |
| `Get_SearchTVShowEpisodes_CaseInsensitive()` | `ItemsControllerTests_Search` | Suche nach „pilotfolge" findet „Pilotfolge" in Episodes |
| `Get_WithMediaSourceId_IncludeIndividualMediaTypes_False_Returns2Types()` | `ItemsControllerTests_Search` | Mit `mediaSourceId` und `includeIndividualMediaTypes: false` werden nur MovieCollection und TVShow zurückgegeben (keine Movies, Seasons, Episodes) |
| `Get_WithMediaSourceId_IncludeIndividualMediaTypes_True_Returns5Types()` | `ItemsControllerTests_Search` | Mit `mediaSourceId` und `includeIndividualMediaTypes: true` werden alle 5 Typen zurückgegeben |
| `Get_PlaylistSearch_WithOptIn_Returns5Types()` | `ItemsControllerTests_Search` | Playlist-Suche (ohne `mediaSourceId`) mit `includeIndividualMediaTypes: true` gibt alle 5 Typen |
| `Get_PlaylistSearch_WithoutOptIn_Returns2Types()` | `ItemsControllerTests_Search` | Playlist-Suche (ohne `mediaSourceId`) ohne Opt-in gibt nur 2 Typen (Regression-Test) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Alle Tests in `ItemsControllerTests_Search` | Müssen nach case-insensitive Implementierung überprüft werden, ob sie weiterhin grün sind (z. B. falls ein Test nach exaktem Case sucht und keine Treffer erwartet, aber case-insensitive Suche nun Treffer liefert) |
| `MediaSearchSelectorTests` | Tests verwenden `RequestItemsAsync()`, die jetzt `includeIndividualMediaTypes: true` setzt. Falls Tests die Ergebnistypen prüfen, müssen sie auf 5 Typen statt 2 angepasst werden |

Falls alle bestehenden Tests ohne Anpassung grün bleiben, kann dieser Eintrag auf „Keine" gesetzt werden.

### E2E-Tests (primärer Funktionsnachweis)

Der Benutzerfluss der Playlist-Medienauswahl muss durch E2E-Tests validiert werden, da die Case-insensitivity-Anforderung und das Opt-in-Verhalten nur im Browser mit echter Komponenten-Interaktion vollständig getestet werden können. Unit-Tests validieren die Datenbankabfragen, aber nicht die Komponenten-Zusammensetzung und das echte Navigations-Verhalten.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Benutzer sucht mit abweichender Groß-/Kleinschreibung nach Medium und findet es | `PlaylistDetailE2ETests` (neue Methode: `AddMedia_SearchCaseInsensitive_FindsMedia()`) | Suche nach „breaking bad" (Kleinbuchstaben) findet „Breaking Bad" (gemischte Schreibung) in den Ergebnissen und Benutzer kann es zur Playlist hinzufügen | Browser-Rendering und Komponenten-Interaktion müssen verifiziert werden; Unit-Tests prüfen nur die Datenbankabfrage |
| Pflicht | Playlist-Medienauswahl liefert alle 5 Medientypen (Movies, MovieCollections, TVShows, Seasons, Episodes) | `PlaylistDetailE2ETests` (neue Methode: `AddMedia_Search_ReturnsAll5Types()`) | Suche nach Suchbegriff zeigt Ergebnisse aller 5 Typen; Benutzer kann jeden Typ auswählen | Nur E2E validiert, dass Komponente alle Typen korrekt render und auswahlbar macht |
| Sollte | Quellen-Browsing zeigt weiterhin nur 2 Typen (Regression-Test) | `MediaSourceDetailsE2ETests` (falls vorhanden, neue Methode: `BrowseSource_ShowsOnly2Types()`) oder Integration in bestehendes E2E | Detail-Seite einer Medienquelle zeigt nur MovieCollections und TVShows, nicht einzelne Movies, Seasons, Episodes | Validiert Regression-Prävention: UI zeigt nicht plötzlich doppelte/einzelne Einträge statt Collections |

### Betroffene bestehende E2E-Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistDetailE2ETests` (bestehende Such-Tests, falls vorhanden) | Tests könnten neuen Medientypen in Suchergebnissen konfrontiert werden; Assertions auf Ergebnisanzahl oder -typen müssen eventuell angepasst werden |
| `MediaSourceDetailsE2ETests` (falls vorhanden) | Regression-Test erforderlich, um zu verifizieren, dass Quellen-Browsing weiterhin nur 2 Typen zeigt |

Falls keine bestehenden E2E-Tests betroffen sind oder keine vorhanden sind: „Keine."

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | **Technik für case-insensitive Suche:** Soll `ToLower().Contains()` beiderseits oder `EF.Functions.Like()` mit case-insensitive Flag verwendet werden? | **Empfehlung:** Beide Techniken vor Implementierung mit dem SQLite EF Core Provider konkret testen (siehe Schritt 1 der Umsetzungsreihenfolge). `ToLower()` ist portabler und einfacher, `Like` mit Flag ist näher am SQL-Standard. Nach Probe-Test entscheiden und dokumentieren. |
| 2 | **E2E-Test-Infrastruktur:** Existieren bereits E2E-Tests für `MediaSourceDetailsViewModel` (Quellen-Browsing) oder nur für Playlist-Details? | **Empfehlung:** Code-Suche nach E2E-Test-Dateien durchführen (z. B. `*E2ETests.cs`). Falls Quellen-Browsing E2E nicht getestet ist, kann Regressions-Test übersprungen werden; Unit-Test reicht aus. |

