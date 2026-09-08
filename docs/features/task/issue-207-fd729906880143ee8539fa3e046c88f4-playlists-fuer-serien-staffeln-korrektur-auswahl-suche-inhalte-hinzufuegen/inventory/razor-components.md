# Razor-Komponenten und UI

## PlaylistEntriesList.razor

**Datei:** `VideoWebPlayer/Components/Playlists/PlaylistEntriesList.razor`

Owns den "Inhalte"-Bereich der Playlist-Detail-Seite: Add-Entry-Formular, paginierte/virtualisierte Entry-Tabelle, Drag & Drop Reordering und Quick-Actions.

### Eingabe-Bereich (Zeilen 20–31)

**Aktueller Zustand (ZU ERSETZEN):**
```razor
<div class="admin-actions">
    <select class="form-control playlist-add-mediatype-select" @bind="newEntryMediaType">
        <option value="@MediaTypeValues.Movie">Film</option>
        <option value="@MediaTypeValues.TVShow">Serie</option>
        <option value="@MediaTypeValues.TVShowSeason">Staffel</option>
        <option value="@MediaTypeValues.TVShowEpisode">Episode</option>
        <option value="@MediaTypeValues.MovieCollection">Filmsammlung</option>
    </select>
    <input class="form-control playlist-add-mediaid-input" type="number" min="1" @bind="newEntryMediaId" placeholder="Medien-Id" />
    <button class="btn btn-primary playlist-add-entry-button" @onclick="AddEntryAsync">Hinzufuegen</button>
</div>
```

**Properties (Zeilen 114–116):**
- `newEntryMediaType` (string) – aktuell ausgewählter Medientyp
- `newEntryMediaId` (long) – manuell eingegebene Media-ID
- `newEntryMediaId` wird nach erfolgreichem Add auf 0 zurückgesetzt (Zeile 253)

### Einträge-Anzeige (Zeilen 33–102)

**Virtualisierte Tabelle:**
- Komponente: `<Virtualize>`
- ItemsProvider: `ItemsProviderAsync` (Zeilen 180–210)
- Infinites Scrolling mit Lade-Indikator
- Zeigt pro Eintrag:
  - Bild (mit Placeholder-Fallback)
  - Typ (MediaType als String)
  - Titel (MediaTitle)
  - Sammlung (ParentMediaTitle)
  - Hinzufügt-Datum
  - Aktionen (Entfernen, An Anfang/Ende – nur im Manual-Mode)

**Paging:**
- Konstante PageSize = 20
- Semaphore `loadPageSemaphore` zum Schutz vor Race-Conditions
- currentPageNumber, allEntries, hasMorePages, totalCount

### Add-Methode (Zeilen 243–269)

**`AddEntryAsync`:**
```csharp
private Task AddEntryAsync()
    => RunEntryActionAsync(async () =>
    {
        var result = await PlaylistClient.AddMediaToPlaylistAsync(PlaylistId, new DtoAddMediaToPlaylistRequest
        {
            MediaType = newEntryMediaType,
            MediaId = newEntryMediaId
        });
        entriesStatusMessage = result.Message;
        await LoadInitialPageAsync();
        newEntryMediaId = 0;
    }, "Fehler beim Hinzufuegen", ex => { ... });
```

**Ablauf:**
1. Rufe `PlaylistClient.AddMediaToPlaylistAsync` auf
2. Zeige Nachricht aus Result an
3. Reload erste Seite (via `LoadInitialPageAsync`)
4. Setze newEntryMediaId zurück
5. Spezielle Fehlerbehandlung für NotFound und Forbidden

**WICHTIG:** Mit der neuen MediaSearchSelector-Komponente würde diese Methode unverändert bleiben – nur die Art, wie newEntryMediaType/newEntryMediaId gesetzt werden, ändert sich.

### Hilfsmethoden

**`GetImageUrl` (Zeilen 278–281):**
```csharp
private string GetImageUrl(DtoPlaylistEntry entry)
    => entry.ResolvedPictureId.HasValue
        ? $"/api/pictures/{entry.ResolvedPictureId}?access_token={Client.AuthorizationToken}"
        : "/images/placeholder.png";
```

Nutzt `ResolvedPictureId` (server-seitig aufgelöst) nicht die rohe PictureId.

**`RunEntryActionAsync` (Zeilen 224–241):**
Wrapper für alle Entry-Aktionen (Add, Remove, Reorder). Setzt/verwaltet entriesStatusMessage/IsError.

**`RemoveEntryAsync`, `MoveEntryToBeginningAsync`, `MoveEntryToEndAsync`:**
Analog zu AddEntryAsync; rufen unterschiedliche PlaylistClient-Methoden auf.

### Drag & Drop Handling (Zeilen 320–365)

**`OnEntryDragStart`, `OnEntryDropAsync`, `OnEntryDragEnd`:**
- Merkt sich `draggedEntry` in OnEntryDragStart
- OnEntryDropAsync führt Reorder durch
- Browser-Kompatibilität via `wwwroot/js/playlistDragDrop.js`

**Nur im Manual-Mode aktiv** (draggable="@(IsManualMode ? \"true\" : \"false\")")

## MediaBox.razor

**Datei:** `VideoWebPlayer/Components/Shared/Media/MediaBox.razor`

Wiederverwendbare Komponente für Media-Element-Anzeige.

### Parameter

| Parameter | Typ | Beschreibung |
|---|---|---|
| Title | string | Titel (erforderlich) |
| Subtitle | string | Optionaler Untertitel (z.B. Serienname) |
| ImageUrl | string | URL des Poster-Bildes |
| LinkUrl | string | Navigations-Link |
| CardKey | string | Eindeutiger Schlüssel für JS-Interop |
| WatchedAt | DateTime | Nullable; Zeitstempel der letzten Ansicht |
| Actions | IReadOnlyList<MediaBoxAction> | Kontextmenü-Aktionen |
| **OnActionSelected** | EventCallback<string> | **Callback bei Action-Auswahl** |

### Struktur

**Visuell:**
- Poster-Bild mit Schnellplay-Symbol
- Titel und optionaler Untertitel im Overlay
- "Watched"-Indikator falls WatchedAt gesetzt

**Interaktivität:**
- Click auf Link navigiert zu LinkUrl (wenn Actions leer oder OnActionSelected nicht gebunden)
- Rechtsklick oder Long-Press öffnet Kontextmenü (wenn Actions + OnActionSelected vorhanden)
- Kontextmenü-Buttons rufen `OnActionSelected.InvokeAsync(actionKey)` auf

### Verwendung in Anforderung

MediaBox wird von MediaBaseEntryList.razor genutzt; kann von der neuen MediaSearchSelector-Komponente auch genutzt werden zur Such-Ergebnis-Anzeige.

**Wichtig:** OnActionSelected kann für Auswahl-Callbacks genutzt werden (statt Navigation).

## MediaBaseEntryList.razor

**Datei:** `VideoWebPlayer/Components/Shared/Media/MediaBaseEntryList.razor`

Zeigt eine Liste von MediaBaseEntry-Objekten als Media-Kacheln.

### Parameter

| Parameter | Typ | Beschreibung |
|---|---|---|
| Entries | List<MediaBaseEntry> | Anzuzeigende Einträge |
| AuthorizationToken | string | Token für Bild-URLs |
| WatchedAtByEntryKey | IReadOnlyDictionary<string, DateTime> | Watched-Status pro Eintrag |

### Logik

**GetImageUrl (Zeilen 17–26):**
Fallback-Reihenfolge: Poster → Banner → Fanart → Placeholder

**GetLinkUrl (Zeilen 28–40):**
Type-spezifisch:
- Movie → `/movie/{id}`
- TVShow → `/tvshow/{id}`
- TVShowSeason → `/tvshow/season/{id}`
- TVShowEpisode → `/tvshow/episode/{id}`
- MovieCollection → `/moviecollection/{id}`

**GetWatchedAt (Zeilen 42–45):**
Schaut in WatchedAtByEntryKey nach Key `{TypeName}:{Id}`.

## Actors.razor – Suchmuster-Vorbild

**Datei:** `VideoWebPlayer/Components/Pages/Actors/Actors.razor`

Demonstriert ein bewährtes Live-Such-Muster für Playlist-Suche.

### Suchmuster

**Input-Bindung (Zeile 17):**
```razor
<input type="text" class="form-control" @bind="searchTerm" @bind:event="oninput" @onkeyup="OnSearchChanged" placeholder="Schauspieler suchen..." />
```

- `@bind:event="oninput"` → Live-Suche bei jeder Eingabe
- `@onkeyup="OnSearchChanged"` → Handler aufgerufen

**Handler `OnSearchChanged` (Zeile 150+):**
```csharp
private async Task OnSearchChanged()
{
    await ResetAndLoadAsync(); // Reset offset, liste neu laden
}

private async Task ResetAndLoadAsync()
{
    offset = 0;
    actors = new List<ActorDto>();
    hasMore = true;
    await LoadActorsAsync();
}

private async Task LoadActorsAsync(bool append = false)
{
    if (isLoading) return;
    isLoading = true;
    try
    {
        var newActors = (await Client.RequestActorsAsync(
            searchTerm, sortMode, selectedFilter, offset, PageSize)).ToList();
        
        if (append)
            actors.AddRange(newActors);
        else
            actors = newActors;
        
        hasMore = newActors.Count == PageSize;
        offset += PageSize;
    }
    finally
    {
        isLoading = false;
    }
}
```

**Infinite Scroll (Zeile 60):**
```razor
<div @ref="sentinel" id="actors-sentinel" style="height: 1px; width: 1px; margin-top: 20px;"></div>
```

JS-Interop per `observeBottom` aufrufen → `OnBottomVisible` ruft `LoadActorsAsync(append: true)` auf.

**Lade-Indikator (Zeilen 38, 56–59):**
```razor
@if (isLoading && !actors.Any())
{
    <p>Lade Schauspieler...</p>
}
...
@if (isLoading)
{
    <p class="text-center mt-2">Weitere Schauspieler werden geladen...</p>
}
```

### Anwendbar auf MediaSearchSelector

Gleiche Struktur empfohlen:
- Input mit `@bind:event="oninput"`
- Lade-Indikator
- Such-Ergebnisse als MediaBox-Kacheln statt MediaBaseEntryList
- Infinite Scroll oder feste Ergebnis-Anzahl

## PlaylistForm.razor

**Datei:** `VideoWebPlayer/Components/Playlists/PlaylistForm.razor`

Formular zur Playlist-Erstellung/Bearbeitung. **Nicht direkt betroffen von Anforderung** – aber könnte zukünftig genutzt werden für Batch-Add-Operationen.

Relevante Binding-Felder:
- Name
- Description
- SortMode

## Zu erstellende Komponente: MediaSearchSelector.razor

**Vorgesehener Ort:** `VideoWebPlayer/Components/Playlists/MediaSearchSelector.razor`

**Parameter:**
- `EventCallback<(string mediaType, long mediaId)> OnMediaSelected` – Callback bei Auswahl

**Struktur (inspiriert von Actors.razor + MediaBox.razor):**
- Suchfeld mit `@bind="searchTerm"` + `@bind:event="oninput"`
- Lade-Indikator während Suche
- Ergebnis-Liste: MediaBox-Komponenten oder ähnlich
- OnMediaSelected-Callback aufrufen bei Auswahl (statt Navigation)

**Backend-Aufruf:**
- Nutzt `VideoWebPlayerClient` um `ItemsController.Get(search: searchTerm)` aufzurufen
- Alternativ: neue Methode auf IPlaylistApiClient?

**Zu beachten:**
- Alle 5 Medientypen werden gesucht und angezeigt
- Titelbild-Auflösung (ResolvedPictureId oder ähnlich)
- Zugriffsschutz-Prüfung (IsAccessible)
