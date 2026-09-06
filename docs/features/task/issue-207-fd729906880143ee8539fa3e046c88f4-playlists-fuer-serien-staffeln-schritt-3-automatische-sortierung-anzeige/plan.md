# Umsetzungsplan: Schritt 3 – Automatische Sortierung und performante Anzeige mit Infinity-List

## Übersicht

In Schritt 3 wird die Playlist-Detailseite (`PlaylistDetail.razor`) um performante, paginierte Anzeige mit Virtual Scrolling erweitert. Der Service-Layer erhält eine neue Methode `GetPlaylistEntriesPagedAsync()`, die Einträge nach Erscheinungsdatum sortiert (mit Fallback-Kette) und seitenweise bereitstellt. Das Frontend lädt initial die erste Seite und zusätzliche Seiten beim Scrollen. Ausgegrenzte Inhalte (keine Lizenz) werden visuell unterschieden, bleiben aber in der sortierten Liste sichtbar.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Virtual-Scrolling-Implementierung** | Blazor eingebaute `Virtualize<T>` Komponente (Namespace `Microsoft.AspNetCore.Components.Web`) mit `ItemsProviderAsync`-Delegate | Keine zusätzliche NuGet-Abhängigkeit erforderlich; `Virtualize<T>` ist Teil des ASP.NET Core Blazor Frameworks (gebündelt mit dem Framework, nicht separat). VideoWebPlayer.csproj hat keine UI-Komponentenbibliotheken wie Radzen.Blazor; daher wird bewusst die eingebaute Komponente genutzt. |
| **Sortierlogik-Implementierung** | Inline in `PlaylistService` via erweiterte `MediaTypeHandler` und neue Hilfsmethoden | Konsistent mit bestehendem Code-Pattern (K.I.S.S. — Keep It Simple); kein Strategie-Interface nötig für diese Anforderung. Wenn zukünftig mehrere Sortierstrategien (z. B. in Schritt 4 für manuelles Drag-and-Drop) hinzukommen, kann Strategy Pattern dann eingeführt werden. |
| **Paginierung** | Server-seitig im Backend (`GetPlaylistEntriesPagedAsync`); Frontend lädt seitenweise | Skaliert besser bei großen Playlists; Sortierung erfolgt nur für jeweils eine Seite im RAM. |
| **Erscheinungsdatum-Daten & Hierarchie-Laden** | `LoadReleaseDateAsync()` und `GetHierarchySequenceAsync()` in `MediaTypeHandler` integriert | Flexibel, folgt bestehendem Pattern; erlaubt pro Medientyp spezifische Ladefunktionen ohne Multiplizierung von Code. |
| **Lizenzprüfung / Ausgegrenzung** | `IsAccessible: bool` im DTO, vorerst immer `true` (Platzhalter) | Struktur ist vorbereitet für zukünftige Lizenz-Service-Integration; aktuell als Platzhalter, da kein Lizenz-Service im Repo existiert. Frontend nutzt `IsAccessible` für visuelle Unterscheidung (Opacity 0.5, graue Farbe, disabled Click-Handler). |
| **DTO für Paginierung** | Neues DTO `DtoPlaylistEntriesPagedResult { Entries: DtoPlaylistEntry[], TotalCount: int, HasNextPage: bool, PageNumber: int, PageSize: int }` | Standard-Pattern für paginierte APIs; ermöglicht Frontend präzise Lazy-Loading-Logik. |
| **Erscheinungsdatum-Fallback-Kette pro Medientyp** | 1. `ReleaseDate`, 2. `PremieredAt`, 3. Hierarchie (Erstes Kind-Datum), 4. `AddedAt` | Basiert auf bestehenden Datenmodellen (models.md Bestandsaufnahme); ermöglicht robuste Sortierung auch bei unvollständigen Daten. |
| **Paginierungsparameter** | Seitennummern-basiert (`pageNumber` 1-basiert, `pageSize` mit Defaults und Maximum) | Einfacher zu verstehen und zu implementieren als Cursor-basierte Pagination; Standard-Konvention. Default 20, Maximum 100 verhindert Performance-Probleme. |

---

## Programmabläufe

### Abruf paginierter Playlist-Einträge mit Sortierung

**Szenario:** Benutzer öffnet `PlaylistDetail` oder scrollt zum Ende der aktuellen Seite.

1. **Frontend** ruft API-Endpoint auf: `GET /api/playlists/{playlistId}/entries?pageNumber=1&pageSize=20` (mit Benutzer-ID in JWT)
2. **Controller** (`PlaylistsController.GetPlaylistEntriesPagedAsync()`) validiert Parameter und ruft Service-Methode auf
3. **`PlaylistService.GetPlaylistEntriesPagedAsync()`:**
   - a. Prüft Berechtigung via `GetOwnedPlaylistAsync(playlistId, userId)` → `PlaylistAccessDeniedException` falls nicht Besitzer
   - b. Lädt alle `PlaylistEntry` aus DB mit `playlistId`
   - c. Filtert verwaiste Einträge (Medien-ID nicht mehr in entsprechender Tabelle) und löscht sie aus DB
   - d. Lädt Medien-Titel via `MediaTypeHandler.LoadTitlesAsync()` für alle Einträge
   - e. Prüft `Playlist.SortMode`:
     - Falls `ByReleaseDate`: Sortierlogik starten
     - Falls `Manual`: Sortierung nach manuell gespeicherter Reihenfolge (Schritt 4) — **für Schritt 3 nicht relevant**
   - f. **Sortierlogik (nur für `ByReleaseDate`):**
     - i. Lädt Erscheinungsdaten pro Medientyp via `MediaTypeHandler.LoadReleaseDateAsync()` → `Dictionary<mediaId, DateTime?>`
     - ii. Lädt Hierarchie-Sequenzen pro Medientyp via `MediaTypeHandler.GetHierarchySequenceAsync()` → `Dictionary<mediaId, (parentId, sequenceNumber)>`
     - iii. Sortiert `PlaylistEntry`-Liste nach Vergleicher (Erscheinungsdatum → Hierarchie → AddedAt)
   - g. Paginiert: `skip = (pageNumber - 1) * pageSize`, `take = pageSize`
   - h. Konvertiert zu `DtoPlaylistEntry` mit `IsAccessible` Flag (aktuell immer `true`)
   - i. Berechnet `hasNextPage = (skip + take) < totalCount` (vor Paginierung gezählte Gesamtanzahl)
   - j. Rückgabe: `DtoPlaylistEntriesPagedResult { Entries, TotalCount, HasNextPage }`
4. **Frontend** (Razor-Komponente) rendert `Entries` mit Virtual Scroll; zeigt Indikator "weitere Einträge" falls `HasNextPage == true`
5. **Frontend** bei Scroll zum Ende: ruft nächste Seite auf (pageNumber + 1), appended `Entries` zur bestehenden Liste

Beteiligte Klassen/Komponenten: `PlaylistService`, `MediaTypeHandler`, `PlaylistsController`, `PlaylistDetail.razor`, `DtoPlaylistEntriesPagedResult`, `DtoPlaylistEntry`

---

### Sortierlogik für Erscheinungsdatum mit Fallback

**Szenario:** `PlaylistService` sortiert Einträge nach `Playlist.SortMode == ByReleaseDate`.

1. Für jeden `PlaylistEntry` wird ein Sortier-Schlüssel berechnet:
   - **Primär:** Erscheinungsdatum (ReleaseDate oder PremieredAt aus Medienentität)
   - **Sekundär (bei NULL oder Tie):** Hierarchie-Typ und -Position
     - TVShow → TVShowSeason → TVShowEpisode (basierend auf `ParentMediaType`, `ParentMediaId`)
     - Episode-Nummer innerhalb Staffel (von `TVShowEpisode.Number`)
   - **Tertiär (bei weiterem Tie):** AddedAt (Aufnahmezeit in Playlist)
2. Alle Einträge sortieren nach Sortier-Schlüssel (aufsteigend nach Datum)
3. Ausgegrenzte Einträge (zukünftig `IsAccessible == false`) werden **nicht** gefiltert, bleiben in Liste sichtbar

Beteiligte Klassen/Komponenten: `PlaylistService`, `MediaTypeHandler`

---

### Lazy Loading und Virtual Scrolling im Frontend

**Szenario:** Benutzer scrollt in `PlaylistDetail` durch lange Playlist.

1. **Komponenten-Init:** `PlaylistDetail` lädt Seite 1 (pageSize z. B. 20)
2. **Rendering:** Blazor `Virtualize<T>` Komponente rendert sichtbare Einträge; Einträge außerhalb des Viewports werden nicht in den DOM gerendert (virtualisiert)
3. **`ItemsProviderAsync`-Delegate:** `Virtualize<T>` ruft den konfigurierten Delegate auf, um Einträge bei Bedarf nachzuladen
4. **Scroll-Event:** Benutzer scrollt; `Virtualize<T>` erkennt, dass der Viewport dem Ende nähert
5. **Lazy-Load-Trigger:** Wenn `HasNextPage == true` und Benutzer nähert sich dem Ende:
   - Frontend-Delegate ruft asynchron nächste Seite auf (pageNumber++)
   - Service liefert neue Einträge
   - `Virtualize<T>` fügt neue Einträge zur bestehenden Liste hinzu
   - `Virtualize<T>` rendert Viewport mit erweiterter Liste neu
6. **Repeat:** Bis `HasNextPage == false`

Beteiligte Klassen/Komponenten: `PlaylistDetail.razor`, `Virtualize<T>` (Microsoft.AspNetCore.Components.Web)

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `DtoPlaylistEntriesPagedResult` | Datenklasse (DTO) | Paginiertes Ergebnis mit `Entries[]`, `TotalCount`, `HasNextPage` |

---

## Änderungen an bestehenden Klassen

### `PlaylistService` (Logikklasse)

- **Neue Methoden:**
  - `GetPlaylistEntriesPagedAsync(playlistId: long, userId: string, pageNumber: int, pageSize: int, cancellationToken: CancellationToken)` → Task<DtoPlaylistEntriesPagedResult>
    - Abruft paginierte, sortierte Playlist-Einträge mit allen Metadaten
  - `SortPlaylistEntriesByReleaseDateAsync(entries: List<PlaylistEntry>, cancellationToken: CancellationToken)` → Task<List<PlaylistEntry>>
    - Private Hilfsmethode: Sortiert Einträge nach Erscheinungsdatum mit Fallback-Kette
  - `BuildPlaylistEntriesSortKeyAsync(entry: PlaylistEntry, mediaType: string, releaseDate: DateTime?, sequenceInfo: (parentId?, sequenceNum?), cancellationToken: CancellationToken)` → Task<(DateTime?, int?, DateTime?)>
    - Private Hilfsmethode: Berechnet Sortier-Schlüssel pro Eintrag

- **Erweiterung: `MediaTypeHandler` (innere Klasse)**
  - Neue Property: `LoadReleaseDateAsync: Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, DateTime?>>>`
    - Lädt Erscheinungsdatum pro Medientyp (ReleaseDate, PremieredAt oder Fallback)
  - Neue Property: `GetHierarchySequenceAsync: Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, (long? ParentId, int? SequenceNumber)>>>`
    - Lädt Hierarchie-Position (Parent-ID und Sequenznummer) pro Medientyp

- **Handler-Implementierungen (pro Medientyp):**
  - `Movie`: `LoadReleaseDateAsync` → Movie.ReleaseDate ?? Movie.PremieredAt ?? null; `GetHierarchySequenceAsync` → (null, null)
  - `TVShowEpisode`: `LoadReleaseDateAsync` → TVShowEpisode.ReleaseDate ?? TVShowEpisode.PremieredAt ?? TVShow.PremieredAt ?? null; `GetHierarchySequenceAsync` → (TVShowSeasonId, Episode.Number)
  - `TVShowSeason`: `LoadReleaseDateAsync` → TVShowSeason.PremieredAt ?? FirstEpisode.AirDate ?? TVShow.PremieredAt ?? null; `GetHierarchySequenceAsync` → (TVShowId, Season-Nummer)
  - `TVShow`: `LoadReleaseDateAsync` → TVShow.PremieredAt ?? FirstSeason.FirstEpisode.AirDate ?? null; `GetHierarchySequenceAsync` → (null, null)
  - `MovieCollection`: `LoadReleaseDateAsync` → MovieCollection.ReleaseDate ?? MovieCollection.PremieredAt ?? null; `GetHierarchySequenceAsync` → (null, null)

### `IPlaylistService` (Interface)

- **Neue Methode:**
  - `GetPlaylistEntriesPagedAsync(playlistId: long, userId: string, pageNumber: int, pageSize: int, cancellationToken: CancellationToken = default)` → Task<DtoPlaylistEntriesPagedResult>

### `DtoPlaylistEntry` (DTO)

- **Neue Property:**
  - `IsAccessible: bool` — Gibt an, ob Benutzer Zugriff auf diesen Medieninhalt hat (aktuell immer `true`; später Lizenz-Integration)

### `PlaylistDetail.razor` (Razor-Komponente)

- **Neue Properties:**
  - `CurrentPageNumber: int = 1` — Aktuelle Seite beim Lazy Loading
  - `IsLoadingMore: bool = false` — Flag für Ladeindikator
  - `AllEntries: List<DtoPlaylistEntry> = new()` — Akkumulierte Einträge aller bisherigen Seiten
  - `HasMorePages: bool = true` — Aus `DtoPlaylistEntriesPagedResult.HasNextPage`
  - `TotalCount: int = 0` — Aus `DtoPlaylistEntriesPagedResult.TotalCount`
  - `VirtualizeReference: Virtualize<DtoPlaylistEntry>` — Referenz zur `Virtualize<T>` Komponente

- **Neue Methoden:**
  - `LoadInitialPageAsync()` → Task — Lädt Seite 1 beim Komponenten-Init
  - `ItemsProviderAsync(ItemsProviderRequest request)` → Task<ItemsProviderResult<DtoPlaylistEntry>> — Delegate für `Virtualize<T>` Item-Provider; lädt Seiten bei Bedarf

- **Markups-Änderungen:**
  - Ersetze unkonditionalen `@foreach(var entry in entries)` durch `<Virtualize<DtoPlaylistEntry> ItemsProviderAsync="ItemsProviderAsync" @ref="VirtualizeReference">`
  - Für jeden Eintrag: Bedingtes Styling basierend auf `entry.IsAccessible`
    - Wenn `IsAccessible == false`: CSS-Klasse `opacity-50`, Farbe grau, Click-Handler `disabled` oder `@onclick="@((e) => { /* do nothing */ })"`
    - Wenn `IsAccessible == true`: Normal klickbar, führt Play-Aktion aus

### `PlaylistsController` (API-Controller)

- **Neue Endpoint-Methode:**
  - `GetPlaylistEntriesPagedAsync(playlistId: long, pageNumber: int = 1, pageSize: int = 20)` → Task<IActionResult>
  - HTTP: `GET /api/playlists/{playlistId}/entries?pageNumber=1&pageSize=20`
  - Authentifizierung: JWT, extrahiere userId
  - Validierung: pageNumber >= 1, pageSize >= 1 und <= max (z. B. 100)
  - Rückgabe: `DtoPlaylistEntriesPagedResult` oder `404 / 403` auf Fehler

### `PlaylistSettings` (Konfigurationsklasse)

- **Neue Property:**
  - `DefaultPageSize: int = 20` — Standard-Seitengröße für Infinity-List

---

## Datenbankmigrationen

Keine. Alle erforderlichen Spalten existieren bereits in `PlaylistEntry` und Medien-Entitäten.

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `pageNumber` (API-Parameter) | `>= 1` | 400 Bad Request |
| `pageSize` (API-Parameter) | `>= 1` und `<= 100` | 400 Bad Request |
| `playlistId` (Route-Parameter) | Muss existieren und Benutzer muss Eigentümer sein | 404 oder 403 Forbidden |

---

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `PlaylistSettings.DefaultPageSize` | `int` | `20` | Standard-Seitengröße für paginierte Einträge-Abrufe |

Falls konfigurierbar via `appsettings.json`:
```json
{
  "Playlists": {
    "DefaultPageSize": 20,
    "MaxPageSize": 100
  }
}
```

---

## Seiteneffekte und Risiken

- **`PlaylistDetailE2ETests.cs` muss angepasst werden:** Die Test-Klasse nutzt derzeit die alte `GetPlaylistEntriesAsync()` Methode und erwartet eine einfache `@foreach`-Liste. Mit der Einführung von `Virtualize<T>` und Lazy Loading müssen die Tests angepasst werden:
  - CSS-Selektoren müssen auf neue `Virtualize<T>` DOM-Struktur angepasst werden (alte `@foreach` Selektoren funktionieren nicht mehr).
  - Scroll-Simulation / Lazy-Load-Wartelogik muss implementiert oder angepasst werden für Virtual-Scrolling-Tests.
  - Initial-Load und Scroll-Lazy-Load Szenarien müssen getestet werden.

- **Bestehende E2E-Tests für Playlists könnten betroffen sein:** Falls andere Tests (`PlaylistsE2ETests`, `PlaylistEntriesE2ETests`) das Rendering von Playlist-Einträgen prüfen, könnten CSS-Selektoren oder Wartelogik angepasst werden müssen.

- **Performance-Anforderungen:** Die Sortierlogik lädt alle Einträge in RAM und sortiert sie vor Paginierung. Bei Playlists mit 100–300 Einträgen (gemäß Anforderung für E2E-Tests) ist dies nicht kritisch. Die Paginierung mit Server-seitiger Sortierung verhindert, dass alle Einträge in den Browser geladen werden. Für zukünftige Optimierungen könnte DB-seitiges ORDER BY erwogen werden (z. B. wenn es Performance-Probleme bei >10.000 Einträgen gibt).

- **Lizenz-Prüfung aktuell nicht implementiert:** Alle Inhalte haben `IsAccessible = true` als Platzhalter. Die DTO-Struktur ist für zukünftige Lizenz-Service-Integration vorbereitet. Lizenz-Prüfung wird in einem späteren Feature-Schritt hinzugefügt.

- **`GetPlaylistEntriesAsync()` bleibt erhalten:** Neue Methode `GetPlaylistEntriesPagedAsync()` ist ergänzend; alte Methode wird nicht gelöscht. Tests, die `GetPlaylistEntriesAsync()` mocken, funktionieren weiterhin.

---

## Umsetzungsreihenfolge

1. **Erweiterung von `MediaTypeHandler` und neue Hilfsmethoden in `PlaylistService`**
   - Voraussetzungen: `PlaylistService.cs`, Datenmodelle existieren (vorhanden)
   - Beschreibung:
     - Füge `LoadReleaseDateAsync` und `GetHierarchySequenceAsync` Properties zu `MediaTypeHandler` hinzu
     - Implementiere Handler-Instanzen für alle fünf Medientypen (Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection)
     - Implementiere private Hilfsmethoden `SortPlaylistEntriesByReleaseDateAsync()` und `BuildPlaylistEntriesSortKeyAsync()`

2. **Neue Methode `GetPlaylistEntriesPagedAsync()` in `PlaylistService`**
   - Voraussetzungen: Schritt 1 abgeschlossen; `PlaylistService` Basis-Klasse vorhanden
   - Beschreibung:
     - Implementiere Methode mit Berechtigungsprüfung, Orphan-Bereinigung, Titel-Laden, Sortierung (via Schritt 1), Paginierung, DTO-Konvertierung

3. **Neue Methode in `IPlaylistService` Interface**
   - Voraussetzungen: `IPlaylistService.cs` existiert (vorhanden)
   - Beschreibung: Füge Methodensignatur `GetPlaylistEntriesPagedAsync()` hinzu

4. **Neue DTO-Klasse `DtoPlaylistEntriesPagedResult` anlegen**
   - Voraussetzungen: DTO-Verzeichnis existiert (vorhanden)
   - Beschreibung: Neue Klasse mit Properties `Entries: DtoPlaylistEntry[]`, `TotalCount: int`, `HasNextPage: bool`

5. **Erweiterung von `DtoPlaylistEntry` DTO**
   - Voraussetzungen: `DtoPlaylistEntry.cs` existiert (vorhanden)
   - Beschreibung: Füge `IsAccessible: bool` Property hinzu (initialisiere mit `true`)

6. **Erweiterung von `PlaylistSettings` Konfigurationsklasse**
   - Voraussetzungen: `PlaylistSettings.cs` existiert (vorhanden)
   - Beschreibung: Füge `DefaultPageSize: int` Property mit Defaultwert 20 hinzu

7. **Neuer API-Endpoint in `PlaylistsController`**
   - Voraussetzungen: `PlaylistsController.cs` existiert; Schritte 2–3 abgeschlossen
   - Beschreibung:
     - Neue Endpoint-Methode `GetPlaylistEntriesPagedAsync()` mit Route `GET /api/playlists/{playlistId}/entries`
     - Query-Parameter: `pageNumber`, `pageSize`
     - Authentifizierung und Fehlerbehandlung

8. **Erweiterung von `PlaylistDetail.razor` mit Virtual Scrolling**
   - Voraussetzungen: `PlaylistDetail.razor` existiert; Schritte 2–7 abgeschlossen; `Virtualize<T>` ist bereits Teil des ASP.NET Core Blazor Frameworks (keine zusätzliche NuGet-Abhängigkeit erforderlich)
   - Beschreibung:
     - Füge Properties für Paginierungs-State und `Virtualize<T>` Referenz hinzu
     - Ersetze bestehende `@foreach` Loop durch Blazor `<Virtualize<DtoPlaylistEntry> ItemsProviderAsync="ItemsProviderAsync">`
     - Implementiere `ItemsProviderAsync(ItemsProviderRequest)` Delegate für Lazy-Loading
     - Bedingte Styling für ausgegrenzte Inhalte (opacity 0.5, graue Farbe, disabled Click-Handler für `IsAccessible == false`)

9. **Unit-Tests für Paginierung und Sortierung**
   - Voraussetzungen: Test-Framework (Xunit), `PlaylistServiceTestBase`, Datenmodelle vorhanden; Schritte 2–6 abgeschlossen
   - Beschreibung:
     - Neue Testklasse `PlaylistServiceTests_GetEntriesPaged.cs`
     - Tests für verschiedene Page Sizes, Page Numbers, `hasNextPage` Logik
     - Tests für Sortierung nach Erscheinungsdatum, Fallback-Ketten, Gemischte Szenarien
     - Tests für Ausgegrenzte Inhalte (`IsAccessible` Flag)

10. **Unit-Tests für API-Controller**
    - Voraussetzungen: `PlaylistsControllerTestBase`, Test-Framework vorhanden; Schritt 7 abgeschlossen
    - Beschreibung:
      - Neue Testmethoden in `PlaylistsControllerTests_Entries.cs` für neuen Endpoint
      - Tests für Parameter-Validierung (pageNumber, pageSize)
      - Tests für Authentifizierung und Autorisierung

11. **E2E-Tests für Virtual Scrolling und Lazy Loading**
    - Voraussetzungen: E2E-Test-Infrastruktur, WebApplicationFactory vorhanden; Schritte 2–8 abgeschlossen
    - Beschreibung:
      - Neue Testklasse oder erweitere `PlaylistDetailE2ETests.cs`
      - Tests für Seite 1 Laden
      - Tests für Scroll-Trigger und Seite 2+ Laden
      - Tests für `HasNextPage = false` (kein weiteres Laden)
      - Tests für Ausgegrenzte Inhalte Darstellung

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetEntriesPaged_ReturnsFirstPage` | `PlaylistServiceTests_GetEntriesPaged` | Erste Seite mit korrektem Page Size wird zurückgeliefert |
| `GetEntriesPaged_HasNextPageTrue_WhenMoreEntriesExist` | `PlaylistServiceTests_GetEntriesPaged` | `HasNextPage` ist `true` wenn mehr Einträge existieren |
| `GetEntriesPaged_HasNextPageFalse_WhenNoMoreEntries` | `PlaylistServiceTests_GetEntriesPaged` | `HasNextPage` ist `false` auf letzter Seite |
| `GetEntriesPaged_TotalCountIsAccurate` | `PlaylistServiceTests_GetEntriesPaged` | `TotalCount` nach Orphan-Bereinigung korrekt |
| `GetEntriesPaged_SortsByReleaseDate_WhenSortModeByReleaseDate` | `PlaylistServiceTests_GetEntriesPaged` | Einträge nach Erscheinungsdatum sortiert |
| `GetEntriesPaged_SortsWithFallbackToHierarchy_WhenReleaseDateMissing` | `PlaylistServiceTests_GetEntriesPaged` | Fallback auf Hierarchie (ParentMediaType, ParentMediaId, Episode-Nummer) |
| `GetEntriesPaged_SortsWithFallbackToAddedAt_WhenDateAndHierarchyMissing` | `PlaylistServiceTests_GetEntriesPaged` | Fallback auf AddedAt wenn Datum und Hierarchie fehlen |
| `GetEntriesPaged_SortsMixedMediaTypes_Correctly` | `PlaylistServiceTests_GetEntriesPaged` | Gemischte Medientypen (Movie + TVShow + Episode) korrekt sortiert |
| `GetEntriesPaged_IncludesInaccessibleEntries_WithIsAccessibleFalse` | `PlaylistServiceTests_GetEntriesPaged` | Ausgegrenzte Inhalte sind in Ergebnis sichtbar mit `IsAccessible = false` |
| `GetEntriesPaged_NotOwner_ThrowsPlaylistAccessDeniedException` | `PlaylistServiceTests_GetEntriesPaged` | Zugriff wird verweigert falls Benutzer nicht Eigentümer |
| `GetEntriesPaged_PlaylistNotFound_ThrowsKeyNotFoundException` | `PlaylistServiceTests_GetEntriesPaged` | Playlist-ID nicht gefunden wirft Exception |
| `GetEntriesPaged_EmptyPlaylist_ReturnsEmpty` | `PlaylistServiceTests_GetEntriesPaged` | Leere Playlist wird korrekt behandelt |
| `GetEntriesPaged_ValidatesPageNumber_GreaterThanZero` | `PlaylistsControllerTests_Entries` | pageNumber < 1 wird abgelehnt (400 Bad Request) |
| `GetEntriesPaged_ValidatesPageSize_WithinBounds` | `PlaylistsControllerTests_Entries` | pageSize > 100 oder < 1 wird abgelehnt |
| `GetEntriesPaged_Endpoint_ReturnsCorrectResponse` | `PlaylistsControllerTests_Entries` | API-Endpoint liefert `DtoPlaylistEntriesPagedResult` mit korrektem Content-Type |
| `PlaylistDetail_LoadsFirstPage_OnInitialize` | `PlaylistDetailE2ETests` | PlaylistDetail-Komponente lädt Seite 1 beim Init |
| `PlaylistDetail_LoadsNextPage_OnScrollNearEnd` | `PlaylistDetailE2ETests` | Virtual Scroll triggert Lazy Load von nächster Seite |
| `PlaylistDetail_StopsLoading_WhenHasNextPageFalse` | `PlaylistDetailE2ETests` | Laden wird gestoppt wenn keine Seiten mehr verfügbar |
| `PlaylistDetail_DisplaysInaccessibleEntries_WithReducedOpacity` | `PlaylistDetailE2ETests` | Ausgegrenzte Inhalte werden mit reduzierter Opacity angezeigt |
| `CreateTestPlaylistWithReleaseDatesAsync()` (Hilfsmethode) | `PlaylistServiceTestBase` oder `TestHelpers` | Erstellt Test-Playlist mit Einträgen verschiedener Medientypen und Erscheinungsdatum-Kombinationen |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistDetailTests` oder `PlaylistDetailE2ETests` (falls existiert) | Falls vorhanden: Muss auf neue Virtual-Scroll-Komponente in `PlaylistDetail.razor` angepasst werden; alte `@foreach` Selektoren funktionieren nicht mehr |
| `PlaylistsControllerTests_Entries` | Falls bestehende Tests den alten Endpoint `GET /api/playlists/{id}/entries` direkt testen: Diese Tests sollten weiterhin funktionieren (alter Endpoint bleibt erhalten oder wird als Wrapper um neue `GetPlaylistEntriesPagedAsync()` implementiert). Falls nicht: keine Anpassung nötig. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| **Pflicht** | Erste Seite einer Playlist wird beim Öffnen von PlaylistDetail angezeigt | `PlaylistDetailE2ETests::LoadsFirstPage_OnInit` | "Benutzer sieht sortierte, scrollable Einträge beim Öffnen (Seite 1 mit ~20 Einträgen)" | Unit-Tests können nicht zeigen, dass Komponente korrekt rendern und API aufrufen; E2E verifiziert kompletten Benutzerfluss von UI bis Backend; visuelle Ordnung und Komponenten-Struktur nur E2E sichtbar |
| **Pflicht** | Benutzer scrollt zum Ende der sichtbaren Einträge; nächste Seite wird asynchron geladen und Einträge werden angefügt | `PlaylistDetailE2ETests::LazyLoadsNextPage_OnScroll` | "Virtual Scrolling mit Lazy Loading funktioniert; keine Flicker, keine Blockierung" | Unit-Tests prüfen Service-Logik und DTO; E2E prüft tatsächliches Virtual-Scroll-Verhalten (Browser-seitig), DOM-Update, Timing; Scroll-Events können nicht unit-getestet werden |
| **Pflicht** | Ausgegrenzte Inhalte sind sichtbar aber visuell unterschieden (Opacity 0.5, graue Farbe, Button disabled) | `PlaylistDetailE2ETests::IsAccessibleFalse_StylesCorrectly` | "Ausgegrenzte Inhalte werden ausgegraut angezeigt, nicht startbar" | CSS-Styling und Event-Handling nur durch Browser-Rendering nachweisbar; Unit-Tests können nicht visuelle Unterschiede prüfen |
| **Wichtig** | Playlist mit 200–300 Einträgen wird ohne merkliche Verzögerung geladen und scrollbar angezeigt (Virtual Scrolling funktioniert) | `PlaylistDetailE2ETests::Performance_MediumLoad_200Entries` | "Infinity-List ist performant auch mit 200–300 Einträgen; kein Ruckeln beim Scrollen" | Virtualisierung und Render-Performance können nur im echten Browser gemessen werden; Unit-Tests können nicht zeigen, ob UI responsiv bleibt oder Scroll-Lag auftritt |

**Warum E2E-Tests erforderlich sind:** Virtual Scrolling ist eine Browser-Feature (DOM Virtualisierung, Scroll-Events, Viewport-Berechnung). Service-Unit-Tests prüfen nur Sortierung und Paginierung; sie zeigen nicht, dass die UI-Komponente korrekt `Virtualize<T>` nutzt, dass Lazy Loading funktioniert, dass DOM-Updates korrekt sind, oder dass Ausgegrenzte Inhalte visuell korrekt dargestellt werden.

**Betroffene bestehende E2E-Tests:** `PlaylistDetailE2ETests.cs` (gemäß Bestandsaufnahme vorhanden). Falls bereits Tests für die alte, nicht-paginierte Anzeige existieren, müssen diese angepasst werden:
- CSS-Selektoren müssen auf neue `Virtualize<T>` DOM-Struktur angepasst werden.
- Wartelogik muss für Virtual-Scroll-Lazy-Loading angepasst werden.
- Tests können nicht mehr statisch auf alle Einträge zugreifen; Lazy-Load-Logik muss simuliert werden.

---

## Offene Punkte

Keine. Alle offenen Punkte aus der vorherigen Anforderung wurden durch die Klarstellungen vollständig beantwortet und in den Plan eingearbeitet:

- ✅ **Virtual-Scrolling-Library:** Blazor `Virtualize<T>` (eingebaut, keine NuGet-Abhängigkeit)
- ✅ **Lizenz-Service:** `IsAccessible` vorerst `true` (Platzhalter)
- ✅ **Standard-Seitengröße:** 20 Default, 100 Maximum
- ✅ **Performance-Anforderungen:** E2E-Tests mit 100–300 Einträgen ausreichend (nicht 10.000+)
- ✅ **Betroffene Tests:** `PlaylistDetailE2ETests.cs` wird überprüft und angepasst

