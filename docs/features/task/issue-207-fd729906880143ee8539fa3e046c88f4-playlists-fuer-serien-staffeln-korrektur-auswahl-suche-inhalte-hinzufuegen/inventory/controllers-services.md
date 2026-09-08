# Controller und Service-Logik

## ItemsController

**Datei:** `VideoWebPlayer/Controllers/ItemsController.cs`

### Methode: `Get` (Zeilen 116–226)
**HTTP:** GET /api/items

Ruft Media-Einträge mit optionalen Filtern ab. **KRITISCH:** Aktuell nur MovieCollections und TVShows, nicht Movies/Seasons/Episodes!

| Parameter | Typ | Beschreibung |
|---|---|---|
| mediaSourceId | long? | Optionale Media-Source-ID zum Filtern |
| page | int | 0-basierte Seite (Standard: 0) |
| size | int | Einträge pro Seite (Standard: 30) |
| search | string | Optionale Name-Suche |
| genreId | long? | Optionales Genre-Filter |

**Rückgabe:** `ActionResult<List<MediaEntryDto>>`

**Ablauf:**
1. Benutzer-Autorisierung prüfen
2. MovieCollections abfragen:
   - Filtern nach mediaSourceId falls angegeben
   - Name-Suche anwenden falls search nicht null
   - Genre-Filter anwenden falls genreId nicht null
   - Auf Benutzer-Zugriff prüfen (MediaSourceUsers + UnlockedMediaService)
   - In MediaEntryDto konvertieren mit Type="Movie" (HINWEIS: das ist falsch benannt; sollte "MovieCollection" sein)
3. TVShows abfragen (gleiche Filter-Logik)
   - Type="TVShow" setzen
4. Beide Listen zusammenführen und nach Title sortieren
5. Paging anwenden (skip/take) und zurückgeben

**Probleme / Lücken:**
- Type für MovieCollections wird als "Movie" gesetzt statt "MovieCollection" – Semantik-Fehler
- Movies werden nicht berücksichtigt (kein Query in der Datenbank)
- TVShowSeasons werden nicht berücksichtigt
- TVShowEpisodes werden nicht berücksichtigt
- UnlockedMediaService wird nur für Collections/Shows genutzt, nicht für die fehlenden Typen

**Zugriffskontrolle:**
```csharp
var mediaSourceIds = await _db.MediaSourceUsers
    .Where(msu => msu.UserId == CurrentUser.Id)
    .Select(msu => msu.MediaSourceId)
    .ToArrayAsync();

var unlockedMovieCollectionIds = await _unlockedMediaService.GetUnlockedMovieCollectionIdsForUserAsync(CurrentUser.Id);
var unlockedTVShowIds = await _unlockedMediaService.GetUnlockedTVShowIdsForUserAsync(CurrentUser.Id);

// Filter:
.Where(m => mediaSourceIds.Contains(m.MediaSourceId) || unlockedMovieCollectionIds.Contains(m.Id))
```

## PlaylistsController

**Datei:** `VideoWebPlayer/Controllers/PlaylistsController.cs`

### Methode: `AddMediaToPlaylist` (Zeilen 219–227)
**HTTP:** POST /api/playlists/{id}/entries

| Parameter | Typ | Beschreibung |
|---|---|---|
| id | long | Playlist-ID |
| request | DtoAddMediaToPlaylistRequest | MediaType + MediaId |

**Ablauf:**
1. Validierung des Request-Body
2. Ruf `_playlistService.AddMediaToPlaylistAsync` auf
3. Gebe DtoPlaylistAddResult zurück

**Exception-Handling (zentralisiert in ExecuteAsync):**
- KeyNotFoundException → 404 NotFound
- UnauthorizedAccessException → 401 Unauthorized
- PlaylistAccessDeniedException → 403 Forbidden
- ArgumentException → 400 BadRequest
- InvalidOperationException → 400 BadRequest (oder Custom-Map)
- Exception → 500 Internal Server Error

### Methode: `RemoveMediaFromPlaylist` (Zeilen 236–244)
**HTTP:** DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}

### Methode: `GetPlaylistEntriesPaged` (Zeilen 268–285)
**HTTP:** GET /api/playlists/{id}/entries/paged

Mit Paging und Sortierung nach SortMode.

## PlaylistService

**Datei:** `VideoWebPlayer/Services/PlaylistService.cs`

### Methode: `AddMediaToPlaylistAsync` (Zeilen 139–166)
**Signatur:**
```csharp
public async Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(
    long playlistId, 
    string userId, 
    string mediaType, 
    long mediaId, 
    CancellationToken cancellationToken = default)
```

**Sichtbarkeit:** public

**Ablauf:**
1. Playlist validieren (Eigentumscheck via `GetOwnedPlaylistAsync`)
2. mediaType mit `MediaHierarchyRegistry.ParseMediaType` parsen → normalisieren
3. mediaId validieren (> 0)
4. `GetMediaTitleAsync` aufrufen (prüft, ob Medium existiert)
5. `BuildEntriesToAddAsync` aufrufen:
   - Top-Level-Eintrag erstellen
   - Cascade-Einträge ermitteln (z.B. Episoden bei TVShow-Add)
   - Duplikate überspringen
6. SortOrder zuweisen (wenn Manual-Mode):
   - `_reorderService.GetMaxSortOrderAsync` hole aktuelles Max
   - Inkrementelle SortOrders für neue Einträge
7. Einträge in DB speichern
8. `BuildAddResultAsync` aufrufen → DtoPlaylistAddResult mit Nachricht/Anzahl

**Kaskaden-Auflösung:**
- TVShow → alle Episoden hinzufügen
- TVShowSeason → alle Episoden dieser Staffel hinzufügen
- Movie/MovieCollection/TVShowEpisode → kein Cascade

Implementiert via `GetCascadeMediaIdsAsync` → parst MediaType und ruft entsprechende DB-Query auf.

**Duplikat-Handling:**
- `LoadExistingMediaRefsAsync` lädt alle (MediaType, MediaId) Paare der Playlist
- Vergleich mit HashSet; Duplikate werden übersprungen (Zähler in Result)

**Zugriffsprüfung:**
- Wird NICHT beim Hinzufügen geprüft
- Wird später von `GetPlaylistEntriesAsync`/`GetPlaylistEntriesPagedAsync` via `PlaylistEntryAccessResolver` geprüft
- Bei Anzeige wird IsAccessible pro Eintrag gesetzt

### Methode: `GetPlaylistEntriesAsync` (Zeilen 215–222)
**Signatur:**
```csharp
public async Task<DtoPlaylistEntry[]> GetPlaylistEntriesAsync(
    long playlistId, 
    string userId, 
    CancellationToken cancellationToken = default)
```

**Ablauf:**
1. Playlist laden (Eigentumscheck)
2. Alle gültigen PlaylistEntries laden
3. Nach SortMode sortieren
4. Für jeden Eintrag einen DTO erstellen (mit ResolvedPictureId, IsAccessible, etc.)
5. Array zurückgeben

### Methode: `GetPlaylistEntriesPagedAsync` (analog)
Wie GetPlaylistEntriesAsync aber mit Paging (page/size).

## PlaylistEntryAccessResolver

**Datei:** `VideoWebPlayer/Services/PlaylistEntryAccessResolver.cs`

Hilfsklasse, die `PlaylistEntry` → `DtoPlaylistEntry` mit Access-Prüfung konvertiert.

**Aufgaben:**
- ResolvedPictureId auflösen (sucht Poster/Banner/Fanart der referenzierten Media)
- IsAccessible bestimmen:
  - Liest MediaSourceId des referenzierten Media
  - Prüft `MediaSourceUsers` für Benutzer
  - Prüft `IUnlockedMediaService` für Individual-Unlock
  - Nutzt `IsAccessible(hasSourceAccess, isUnlocked)`

## GetMediaTitleAsync (privat in PlaylistService)

Prüft, ob ein Medium mit gegebener Type/ID existiert; throws KeyNotFoundException wenn nicht.

Unterstützt alle 5 Medientypen via Switch-Statement:
```csharp
case MediaType.Movie => 
    await _db.Movies.FirstOrDefaultAsync(m => m.Id == mediaId)
case MediaType.TVShow =>
    await _db.TVShows.FirstOrDefaultAsync(ts => ts.Id == mediaId)
// usw.
```

**Wichtig:** Diese Methode PRÜFT NICHT die Zugriffs-berechtigung des Benutzers – nur die Existenz!

## MediaHierarchyRegistry

**Datei:** (vermutlich `VideoWebPlayer/Services/` oder `VideoWebPlayer/Data/`)

Enthält `ParseMediaType(string)` → `MediaType` Enum.

Unterstützt String-zu-Enum-Konvertierung für alle 5 Typen.

## IPlaylistApiClient (Partielle Implementierung)

**Datei:** `VideoWebPlayer.Client/VideoWebPlayerClient.Playlists.cs` (vermutlich)

Definiert Client-seitige Playlist-Operationen.

Bekannte Methoden:
- `AddMediaToPlaylistAsync(playlistId, DtoAddMediaToPlaylistRequest)`
- `RemoveMediaFromPlaylistAsync(playlistId, mediaType, mediaId)`
- `RequestPlaylistEntriesPagedAsync(playlistId, pageNumber, pageSize)`
- `RequestMaxSortOrderAsync(playlistId)`

**LÜCKE:** Keine Suchmethode für Media-Suche vorhanden. Müsste hinzugefügt werden oder über bestehenden `ItemsController.Get` abgewickelt werden.

## Zusammenfassung der Erweiterungsaufgaben

| Komponente | Erweiterungsaufgabe |
|---|---|
| ItemsController.Get | Unterstützung für Movies, TVShowSeasons, TVShowEpisodes hinzufügen |
| ItemsController.Get | Type-Feld korrigieren (MovieCollection statt "Movie") |
| IUnlockedMediaService | Ggf. neue Methods für Movies/TVShowSeasons – bereits vorhanden? |
| VideoWebPlayerClient | Ggf. neue Suchmethode hinzufügen oder bestehende nutzen |
| PlaylistEntriesList.razor | Durch MediaSearchSelector ersetzen (neu zu erstellen) |
| PlaylistService | Keine Änderung erforderlich – akzeptiert bereits alle 5 Typen |
