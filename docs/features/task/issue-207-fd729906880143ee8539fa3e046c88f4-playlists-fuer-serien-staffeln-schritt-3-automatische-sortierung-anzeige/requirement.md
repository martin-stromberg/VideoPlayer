# Anforderung – Schritt 3: Automatische Sortierung und performante Anzeige mit Infinity-List

**Ausgabedatum:** 2026-09-06  
**Gültig ab Schritt:** 3 (mehrstufiges Playlist-Rollout)

---

## Fachliche Zusammenfassung

Die Playlist-Detailseite (`/playlists/{id}`, `PlaylistDetail.razor`) wird um eine performante, scrollbare Anzeige der Playlist-Inhalte erweitert. Statt einer einfachen, unsortieren Liste (Schritt 2) wird eine virtuelle Liste (`Infinity-List` / `Virtual Scrolling`) implementiert, die auch bei sehr großen Playlistinhalten flüssig bedienbar bleibt und Einträge beim Scrollen seitenweise nachlädt. Im automatischen Sortiermodus (`PlaylistSortMode.ByReleaseDate`) werden alle Einträge chronologisch nach Erscheinungsdatum sortiert; für Einträge ohne Erscheinungsdatum erfolgt ein Fallback auf die Serie-, Staffel- und Episodenreihenfolge, danach auf das Hinzufügedatum (`AddedAt`). Einträge, für die der Benutzer keine Freischaltung besitzt, werden ausgegraut dargestellt, sind nicht startbar, bleiben aber Bestandteil der sortierten Liste sichtbar.

---

## Betroffene Klassen und Komponenten

### Datenmodellklassen (bestehend, ggf. Erweiterungen)

- **`PlaylistEntry`** (Entity, bestehend in `VideoWebPlayer.Data.Playlists`)
  - Bestehende Spalten sind für die Sortierlogik ausreichend
  - `ParentMediaType`, `ParentMediaId`: Ermöglichen die Fallback-Sortierung nach Hierarchie
  - `AddedAt`: Wird als finales Fallback für die Sortierung verwendet
  - _Annahme:_ Erscheinungsdatum-Daten werden über die verlinkten Medien-Entitäten (`Movie`, `TVShowEpisode`, etc.) bereitgestellt, nicht direkt in `PlaylistEntry` gespeichert

- **Medien-Entitäten** (bestehend: `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`)
  - Müssen Zugriff auf Erscheinungsdatum (z. B. `ReleaseDate`, `AirDate`) bieten
  - Müssen Zugriff auf Hierarchie-Informationen bereitstellen (Serie → Staffel → Episode)

### Logikklassen / Services

- **`PlaylistService`** (bestehend in `VideoWebPlayer.Services`)
  - Neue Methode: `GetPlaylistEntriesPagedAsync(playlistId, pageNumber, pageSize, cancellationToken)` oder ähnlich
  - Diese Methode liefert eine sortierte, paginierte Liste von Playlist-Einträgen mit allen erforderlichen Metadaten für die Anzeige
  - Sortierlogik:
    1. Wenn `Playlist.SortMode == PlaylistSortMode.ByReleaseDate` → Sortiere nach Erscheinungsdatum des Medieninhalts
    2. Falls Erscheinungsdatum fehlt → Fallback auf Hierarchie: `ParentMediaType`, `ParentMediaId`, dann Episode-Nummer (Sequenz)
    3. Falls auch Hierarchie nicht bestimmbar → Fallback auf `AddedAt` (Aufnahmezeit in die Playlist)
  - Erweiterte Methode: `GetMediaTitlesAsync()` (bestehend) könnte um Erscheinungsdatum-Daten erweitert werden
  - Integriert Berechtigung (`GetOwnedPlaylistAsync()`), Bereinigung verwaister Einträge (bestehend) und Sortierung

- **`MediaTypeHandler`** (bestehend in `PlaylistService`)
  - Erweiterung: `LoadReleaseDateAsync()` pro Medientyp, um Erscheinungsdatum zu laden
  - Erweiterung: `GetHierarchySequenceAsync()` pro Medientyp, um die Episode/Item-Sequenz innerhalb der Staffel/Sammlung zu ermitteln

### Interfaces

- Optional: **`IPlaylistSortingStrategy`** (neu, sofern Strategie-Pattern verwendet wird)
  - Definiert Sortierlogik pro Sortiermodus
  - Implementierung: `ByReleaseDateSortingStrategy` für den automatischen Sortiermodus

### UI-Komponenten / Razor-Komponenten (Blazor)

- **`PlaylistDetail.razor`** (bestehend, wird erweitert)
  - Nutzt neue paginierte `GetPlaylistEntriesPagedAsync()` statt `GetPlaylistEntriesAsync()`
  - Bindet Virtual-Scroll-Komponente ein (z. B. `Radzen.Blazor` oder `QuickGrid` mit Virtual Scrolling)

- **Neue Virtual-List-Komponente** (neu, sofern nicht vorhanden): z. B. `PlaylistVirtualList.razor` oder Konfiguration einer bestehenden Virtualisierungs-Komponente
  - Rendert Einträge mit Lazy Loading beim Scrollen
  - Zeigt Titelbild, Bezeichnung, Serie/Staffel/Sammlung-Zugehörigkeit
  - Visuell unterschiedliche Darstellung für ausgegrenzte Inhalte (z. B. `opacity: 0.5`, `color: gray`, Disabled-State)
  - Click-Handler nur für nicht ausgegrenzte Inhalte aktiviert

- **UI-Hilfsfunktionen** (neu oder bestehend)
  - Funktion zur Feststellung, ob Benutzer Zugriff auf einen Medieninhalt hat (z. B. via License-Service)
  - Funktion zur Formatierung von Metadaten (Serie/Staffel/Episode-Anzeige)

### Tests

- **Unit-Tests für Sortierlogik**
  - Test: Sortierung nach Erscheinungsdatum (alle Einträge haben Datum)
  - Test: Sortierung mit fehlenden Erscheinungsdaten (Fallback auf Hierarchie)
  - Test: Sortierung mit fehlenden Hierarchie-Daten (Fallback auf `AddedAt`)
  - Test: Gemischte Szenarien (einige mit Datum, einige ohne)

- **Unit-Tests für Paginierung**
  - Test: Korrekte Seite bei verschiedenen `pageNumber` und `pageSize`
  - Test: Korrekte Gesamtzahl bei Bereinigung verwaister Einträge

- **Integration-Tests**
  - Test: `GetPlaylistEntriesPagedAsync()` mit echten Datensätzen
  - Test: Ausgegrenzte Inhalte erscheinen in der sortierten Liste

---

## Implementierungsansatz

### 1. Erweiterung des Service-Layers

**`PlaylistService.GetPlaylistEntriesPagedAsync()`:**

```
Pseudocode:
┌─ GetPlaylistEntriesPagedAsync(playlistId, pageNumber, pageSize)
│
├─ 1. Berechtigungsprüfung: GetOwnedPlaylistAsync(playlistId)
│
├─ 2. Lade alle PlaylistEntry mit PlaylistId (sortieren erfolgt nach Schritt 4)
│
├─ 3. Lade Titel für alle Medientypen (bestehend: GetMediaTitlesAsync())
│
├─ 4. Bereinige verwaiste Einträge (bestehend)
│
├─ 5. Lade Erscheinungsdatum-Daten pro Medientyp
│     └─ MediaTypeHandler.LoadReleaseDataAsync(mediaType, mediaIds)
│
├─ 6. Lade Hierarchie-Sequenzen (Episode-Nummer in Staffel, etc.)
│     └─ MediaTypeHandler.GetHierarchySequenceAsync(mediaType, mediaIds)
│
├─ 7. Sortiere nach Sortiermodus:
│     Falls Playlist.SortMode == ByReleaseDate:
│       ├─ Primär: Nach Erscheinungsdatum (ascending)
│       ├─ Sekundär: Nach ParentMediaType (TVShow → TVShowSeason → TVShowEpisode)
│       ├─ Tertiär: Nach Episode-Sequenz in Staffel
│       └─ Final: Nach AddedAt (ascending)
│
├─ 8. Paginiere: skip = (pageNumber - 1) * pageSize, take = pageSize
│
├─ 9. Konvertiere zu DTO mit Lizenz-Status (ausgegraut ja/nein)
│
└─ Rückgabe: DtoPlaylistEntriesPagedResult { entries[], totalCount, hasNextPage }
```

### 2. Erweiterung des API-Layer

**Neuer oder verbesserter Endpoint:**
- `GET /api/playlists/{id}/entries?pageNumber=1&pageSize=20` (bereits bestehend: `GET /api/playlists/{id}/entries`)
  - Bestehender Endpoint wird erweitert oder neuer Endpoint wird hinzugefügt
  - Response: Array von `DtoPlaylistEntry` oder neues DTO `DtoPlaylistEntriesPagedResult`

### 3. Erweiterung des Data-Layer (MediaTypeHandler)

**Neue oder erweiterte Handler-Funktionen pro Medientyp:**

```csharp
// Laden von Erscheinungsdatum
LoadReleaseDateAsync(
    db: ApplicationDbContext,
    mediaIds: IReadOnlyCollection<long>,
    cancellationToken: CancellationToken
) → Task<Dictionary<long, DateTime?>>

// Laden von Hierarchie-Sequenzen (für Fallback-Sortierung)
GetHierarchySequenceAsync(
    db: ApplicationDbContext,
    mediaIds: IReadOnlyCollection<long>,
    cancellationToken: CancellationToken
) → Task<Dictionary<long, (long? parentId, int? sequenceNumber)>>
```

**Spezifische Handler:**

| Medientyp | `LoadReleaseDateAsync` | `GetHierarchySequenceAsync` |
|-----------|------------------------|---------------------------|
| `Movie` | Aus `Movie.ReleaseDate` | Keine Parent, Sequence = null |
| `TVShowEpisode` | Aus `TVShowEpisode.AirDate` oder `TVShow.ReleaseDate` | Parent = `TVShowSeason.Id`, Sequence = Episode-Nummer |
| `TVShowSeason` | Aus `TVShowSeason.AirDate` oder `TVShow.ReleaseDate` | Parent = `TVShow.Id`, Sequence = Staffel-Nummer |
| `TVShow` | Aus `TVShow.ReleaseDate` oder erstes Episode-Datum | Keine Parent, Sequence = null |
| `MovieCollection` | Aus `MovieCollection.CreatedAt` (oder ähnlich) | Keine Parent, Sequence = null |

### 4. Frontend / Razor-Komponente

**`PlaylistDetail.razor` Erweiterungen:**

- Nutzt `PlaylistService.GetPlaylistEntriesPagedAsync()` für Datenbeschaffung
- Integriert Virtual-Scrolling-Komponente (z. B. Radzen oder QuickGrid)
- Lädt initial erste Seite, bei Scroll-Ende nächste Seite
- Rendert pro Eintrag:
  - Titelbild (Cover-Art)
  - Titel
  - Medienzugehörigkeit (Serie → Staffel → Episode oder nur Filmsammlung)
  - Visueller Status für ausgegrenzte Inhalte (Opacity, Farbe, disabled Click-Handler)

**Lazy-Loading-Logik:**
```
Bei Initialization:
├─ Lade Seite 1 (pageSize z. B. 20)
│
Bei Scroll zum Seiten-Ende:
├─ Prüfe: hasNextPage?
├─ Falls ja: Lade nächste Seite asynchron
├─ Append Einträge zur bestehenden Liste
└─ Keine UI-Blockierung (Async/Await)
```

### 5. Ausgegrenzte Inhalte

**Anzeige ausgegrenzter Inhalte:**

- Service ruft einen Lizenz-Service auf (z. B. `ILicenseService` oder ähnlich), um zu prüfen, ob Benutzer Zugriff auf einen Medieninhalt hat
- DTO wird um Feld erweitert: `IsAccessible: bool` oder `LicenseStatus`
- Frontend rendert:
  - Ausgegrenzte Inhalte mit reduzierter Opacity (`opacity: 0.5` oder ähnlich)
  - Ggf. Icon oder Label "Nicht freigeschaltet" / "Nicht verfügbar"
  - Click-Handler deaktiviert (kein Start-Button oder Button disabled)
  - Text in grauer Farbe

---

## Konfiguration

**Neue Konfigurationsparameter (optional):**

```json
{
  "Playlists": {
    "MaxPlaylistItemCount": null,
    "DefaultPageSize": 20,
    "SortingStrategy": "ByReleaseDate"
  }
}
```

- `DefaultPageSize`: Standard-Seitengröße für Infinity-List (z. B. 20 Einträge pro Seite)
- Weitere Sortierstrategien könnten hier konfiguriert werden

**Berechtigungen & Lizenzprüfung:**
- Bestehender Lizenz-Service oder zu definierender Service mit Methode wie `bool HasAccessToMedia(userId, mediaType, mediaId)`

---

## Offene Fragen

1. **Erscheinungsdatum-Quellen:**
   - Woher stammen die Erscheinungsdatum-Daten für alle fünf Medientypen?
   - `Movie.ReleaseDate` ✓, `TVShowEpisode.AirDate` ✓, aber:
     - `TVShow`: Erstes Episode-Datum oder eigenes Feld?
     - `TVShowSeason`: Erstes Episode-Datum der Staffel oder eigenes Feld?
     - `MovieCollection`: Erstellungsdatum oder ähnlich?

2. **Lizenzprüfung / Ausgegrenzung:**
   - Existiert bereits ein Service zur Prüfung von Benutzer-Lizenzen?
   - Wie wird "Zugriff" auf einen Medieninhalt definiert (z. B. via Abonnement, einzelner Kauf)?
   - Sollen ausgegrenzte Inhalte auch in der Sortierung "mitlaufen" oder komplett ausgeblendet werden? (Anforderung sagt: sichtbar, aber ausgegraut)

3. **Virtual-Scrolling-Library:**
   - Welche Library wird für Virtual Scrolling verwendet?
   - `Radzen.Blazor` (https://blazor.radzen.com/datagrid)?
   - `Microsoft.AspNetCore.Components.QuickGrid` mit Virtual-Scroll-Erweiterung?
   - Eigene Komponente?

4. **Seitengröße / Performance:**
   - Standardseitengröße: 20, 50 oder 100 Einträge pro Ladevorgang?
   - Trade-off zwischen Speicherverbrauch und Ladefrequenz?

5. **Sortierung bei manuellen Modus (Schritt 4):**
   - Diese Anforderung betrifft nur automatischen Modus. Schritt 4 wird manuelle Sortierung (Drag & Drop) einführen.
   - Wird die `DtoPlaylistEntry` dann um ein Feld wie `SortOrder: int?` erweitert?

6. **Ausgegrenzte Inhalte und Kaskadenlogik:**
   - Wenn ein Benutzer keine Serie freischaltet, aber einzelne Episoden dieser Serie haben, wie wird das gehandhabt?
   - Sind diese Episoden dann auch ausgegraut?

7. **Performance bei großen Playlists:**
   - Test mit z. B. 10.000+ Einträgen erforderlich?
   - Ist Paginierung ausreichend oder ist zusätzliches Caching erforderlich?

8. **Fehlerbehandlung bei Datenfehlern:**
   - Was tun, wenn Erscheinungsdatum-Daten inkonsistent sind (z. B. Episode hat kein Datum, aber Serie auch nicht)?
   - Fallback-Kette ist definiert, aber Grenzzälle müssen getestet werden.

---

## Annahmen

1. **Medien-Datenmodelle** haben alle erforderlichen Felder für Erscheinungsdatum und Hierarchie-Informationen bereits vorhanden oder werden in separaten Schritten ergänzt.

2. **Lizenz-/Zugriffs-System** existiert bereits oder wird als Schnittstelle (`ILicenseService` oder ähnlich) zur Verfügung gestellt.

3. **Existing Endpoints** (`GET /api/playlists/{id}/entries`) können erweitert oder deprecated werden, ohne Breaking Changes zu verursachen (z. B. via neues Query-Parameter `?paged=true&pageNumber=1`).

4. **Virtual-Scrolling** wird auf der Client-Seite (Razor/Blazor) implementiert; Server liefert paginierte Daten.

5. **Performance-Anforderungen** sind moderat: Seiten mit 20–50 Einträgen sollten innerhalb von 200–500 ms geladen werden.

6. **Testdaten** mit mindestens 100–500 Playlist-Einträgen sind für Entwicklung und QA vorhanden oder werden bereitgestellt.
