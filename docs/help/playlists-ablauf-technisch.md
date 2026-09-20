# Playlists – Technischer Programmablauf

## Übersicht

Die Playlist-Verwaltung besteht aus sechs Hauptabläufen:
1. Hinzufügen von Medieninhalten (mit Cascade-Logik)
2. Entfernen von Medieninhalten
3. Abrufen aller Einträge (mit Bereinigung verwaister Einträge)
4. Mediensuche für die Playlist-Auswahl-Oberfläche (case-insensitive Namenssuche, Opt-in für 5 Medientypen, Regression-Schutz für Quellen-Browsing)
5. Abrufen einer sortierten, paginierten Seite von Einträgen (für die Infinity-List der Detailseite)
6. Playlist-Abbildung (Cover): Upload, Collagen-Regenerierung, Abruf und Löschen

Diese Dokumentation beschreibt den internen Ablauf auf Code-Ebene.

---

## Ablauf 1: Medieninhalt hinzufügen

### Schritt-für-Schritt

**Auslöser:** Client ruft `POST /api/playlists/{id}/entries` auf mit `{ mediaType, mediaId }`

1. **Controller-Validierung:**
   - `PlaylistsController.AddMediaToPlaylist()` empfängt Request
   - Benutzer wird aus Auth-Token extrahiert (`CurrentUser`)
   - Service-Methode wird aufgerufen

2. **Berechtigungsprüfung:**
   - `PlaylistService.AddMediaToPlaylistAsync()` ruft `GetOwnedPlaylistAsync()` auf
   - Diese prüft: Existiert die Playlist? Ist der Benutzer der Besitzer?
   - Falls nicht → `PlaylistAccessDeniedException` → HTTP 403

3. **MediaType-Validierung:**
   - `ValidateMediaType()` prüft: Ist `mediaType` einer der 5 gültigen Typen?
   - Gültige Typen: `Movie`, `TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection`
   - Falls ungültig → `InvalidOperationException` → HTTP 400

4. **Medieninhalt-Existenz prüfen:**
   - `CheckMediaExistsAsync()` prüft via `MediaTypeHandler` in der DB: Existiert der Medieninhalt?
   - Falls nicht → `KeyNotFoundException` → HTTP 404

5. **Bestehende Duplikate abfragen:**
   - Lade alle bestehenden `PlaylistEntry` für diese Playlist
   - Sammle die Schlüssel als `(normalizedMediaType, MediaId)` in einem `HashSet`
   - Normalisiere den `mediaType` zu seinem kanonischen Enum-Wert: `var normalizedMediaType = parsedMediaType.ToString()`
   - Prüfe: Existiert bereits ein Eintrag mit `(normalizedMediaType, mediaId)`?
   - Falls ja → Erhöhe `skippedDuplicateCount`, fahre fort (kein Fehler)

6. **Cascade-Kinder laden:**
   - `GetCascadeMediaIdsAsync()` prüft: Hat dieser `mediaType` Kind-Einträge?
   - Für `TVShow`: Lade alle zugehörigen `TVShowSeason`, dann alle zugehörigen `TVShowEpisode`
   - Für `TVShowSeason`: Lade alle zugehörigen `TVShowEpisode`
   - Für `MovieCollection`: Lade alle zugehörigen `Movie`
   - Für `Movie`, `TVShowEpisode`: Keine Kinder (leere Liste)
   - Rückgabe: Liste von `(type, id)` Tupeln

7. **Cascade-Duplikate filtern:**
   - Filtere die Cascade-Kinder: Für jeden Eintrag Cascade-Eintrag den `mediaType` normalisieren
   - Behalte nur diejenigen, die nicht im bestehenden `HashSet` (mit normalisierten Schlüsseln) sind
   - Für Duplikate: Erhöhe `skippedDuplicateCount`
   - Neue Cascade-Einträge: `newCascadeEntries` (nur noch nicht vorhandene)

8. **Maximale Anzahl prüfen:**
   - Falls `PlaylistSettings.MaxPlaylistItemCount` gesetzt ist (nicht null):
     - Berechne: Neue Einträge = 1 (Top-Level) + `newCascadeEntries.Count`
     - Prüfe: `existingKeys.Count + newEntryCount <= maxItemCount`?
     - Falls nicht → `InvalidOperationException` → HTTP 400

9. **Top-Level-Eintrag vorbereiten:**
   - Falls nicht als Duplikat übersprungen: Erstelle `PlaylistEntry` mit:
     - `PlaylistId = playlistId`
     - `MediaType = normalizedMediaType` (kanonischer Enum-Wert, nicht roher Input)
     - `MediaId = mediaId`
     - `ParentMediaType = null`
     - `ParentMediaId = null`
     - `AddedAt = DateTime.UtcNow`
   - Füge zu `entriesToAdd` Liste hinzu (noch nicht in DB)

10. **Cascade-Einträge vorbereiten:**
    - Für jeden neuen Eintrag in `newCascadeEntries` (nicht Duplikate):
      - Erstelle `PlaylistEntry` mit:
        - `MediaType = normalizedCascadeMediaType` (normalisiert)
        - `ParentMediaType = normalizedMediaType` (normalisiert)
        - `ParentMediaId = mediaId`
      - Füge zu `entriesToAdd` Liste hinzu

11. **Alle Einträge speichern:**
    - `await db.SaveChangesAsync()` — eine Transaktion speichert alle Einträge aus `entriesToAdd` atomar

12. **Benutzer-Nachricht bauen:**
    - Falls `entriesToAdd.Count > 0 && skippedDuplicateCount > 0`: `"{entriesToAdd.Count} Titel hinzugefügt, {skippedDuplicateCount} bereits vorhanden und übersprungen."`
    - Falls `entriesToAdd.Count > 0 && skippedDuplicateCount == 0`: `"{entriesToAdd.Count} Titel hinzugefügt."`
    - Falls `entriesToAdd.Count == 0`: `"Alle {skippedDuplicateCount} Titel waren bereits vorhanden."`

13. **DTOs bauen und Response zusammenstellen:**
    - `BuildAddResultAsync()` konvertiert alle neuen Einträge (`entriesToAdd`) über dieselbe
      `BuildEntryDtosAsync()`-Methode zu `DtoPlaylistEntry`, die auch die beiden Lese-Endpunkte
      (Ablauf 3 und 4) verwenden — inklusive Titel, aufgelöster Bild-ID (`ResolvedPictureId`) und
      echter Freischaltungsprüfung (`IsAccessible`) für den aktuellen Benutzer. Ein soeben
      hinzugefügter, nicht freigeschalteter Titel liefert also unmittelbar `IsAccessible: false`.
    - Baue neue Response: `DtoPlaylistAddResult`:
      - `TopLevelEntry`: Der neu hinzugefügte Top-Level-Eintrag (oder `null`, falls Duplikat)
      - `AddedEntries[]`: Alle neu hinzugefügten Einträge
      - `SkippedDuplicateCount`: Anzahl übersprungener Duplikate
      - `Message`: Benutzer-Nachricht aus Schritt 12
    - Rückgabe an Client (HTTP 200 OK)

### Beteiligte Klassen/Komponenten

| Klasse | Methode | Zweck |
|--------|---------|-------|
| `PlaylistsController` | `AddMediaToPlaylist()` | HTTP-Endpoint-Handler |
| `PlaylistService` | `AddMediaToPlaylistAsync()` | Geschäftslogik mit normalisierter Duplikat-Behandlung und Message-Generierung |
| `PlaylistService` | `GetOwnedPlaylistAsync()` | Berechtigung + Existenz |
| `PlaylistService` | `ValidateMediaType()` | Typ-Validierung (case-insensitiv) |
| `PlaylistService` | `ParseMediaType()` | Parsing und Normalisierung des MediaType-Enums |
| `PlaylistService` | `CheckMediaExistsAsync()` | Existenz-Prüfung |
| `PlaylistService` | `GetCascadeMediaIdsAsync()` | Cascade-Abfrage |
| `PlaylistService` | `GetMediaTitleAsync()` | Titel-Lookup |
| `PlaylistService` | `BuildAddResultAsync()` | Baut `DtoPlaylistAddResult` inkl. Message |
| `PlaylistService` | `BuildEntryDtosAsync()` | Entity → DTO Konvertierung (Titel, `ResolvedPictureId`, `IsAccessible`), gemeinsam mit Ablauf 3/4 |
| `ApplicationDbContext` | `PlaylistEntries` | DB-Zugriff |
| `PlaylistEntry` | — | Datenmodell mit normalisiertem `MediaType` |
| `DtoPlaylistEntry` | — | Client-Modell für einzelne Einträge |
| `DtoPlaylistAddResult` | — | Neue Client-Response-Struktur (TopLevelEntry, AddedEntries[], SkippedDuplicateCount, Message) |

### Diagramm

```mermaid
flowchart TD
    A[POST Request] --> B[ValidateAuth]
    B --> C[GetOwnedPlaylist]
    C -->|Not Owner| C1[403 Forbidden]
    C -->|Not Found| C2[404 Not Found]
    C -->|OK| D[ValidateMediaType]
    D -->|Invalid| D1[400 Bad Request]
    D -->|OK| E[NormalizeMediaType]
    E --> F[CheckMediaExists]
    F -->|Not Found| F1[404 Not Found]
    F -->|OK| G[LoadExistingKeys]
    G --> H{TopLevel Duplicate?}
    H -->|Yes| H1[skippedCount++]
    H -->|No| H2[AddToEntries]
    H1 --> I[GetCascadeMediaIds]
    H2 --> I
    I --> J[FilterNewCascade]
    J --> K{MaxItemCount?}
    K -->|Exceeded| K1[400 Bad Request]
    K -->|OK| L[CreateEntries]
    L --> M[SaveToDb]
    M --> N[LoadTitles]
    N --> O[BuildMessage]
    O --> P[BuildDtoPlaylistAddResult]
    P --> Q[200 OK]
```

---

## Ablauf 2: Medieninhalt entfernen

### Schritt-für-Schritt

**Auslöser:** Client ruft `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}` auf

1. **Berechtigungsprüfung:**
   - `PlaylistService.RemoveMediaFromPlaylistAsync()` ruft `GetOwnedPlaylistAsync()` auf
   - Falls nicht Besitzer → `PlaylistAccessDeniedException` → HTTP 403

2. **Eintrag suchen:**
   - Lade `PlaylistEntry` mit `PlaylistId = id`, `MediaType = mediaType`, `MediaId = mediaId`
   - Falls nicht gefunden → `KeyNotFoundException` → HTTP 404

3. **Löschen:**
   - Entferne Eintrag aus DbContext
   - `await db.SaveChangesAsync()`

4. **Antwort:**
   - HTTP 204 No Content (leerer Body)

### Beteiligte Klassen

| Klasse | Methode | Zweck |
|--------|---------|-------|
| `PlaylistsController` | `RemoveMediaFromPlaylist()` | HTTP-Endpoint-Handler |
| `PlaylistService` | `RemoveMediaFromPlaylistAsync()` | Geschäftslogik |
| `PlaylistService` | `GetOwnedPlaylistAsync()` | Berechtigung + Existenz |
| `ApplicationDbContext` | `PlaylistEntries` | DB-Zugriff |

---

## Ablauf 3: Alle Einträge abrufen (mit Bereinigung)

### Schritt-für-Schritt

**Auslöser:** Client ruft `GET /api/playlists/{id}/entries` auf

1. **Berechtigungsprüfung:**
   - `PlaylistService.GetPlaylistEntriesAsync()` ruft `GetOwnedPlaylistAsync()` auf
   - Falls nicht Besitzer → HTTP 403

2. **Alle Einträge laden:**
   - Lade alle `PlaylistEntry` mit `PlaylistId = id` aus DB
   - Keine Filterung, keine Sortierung (unsortiert in Einfüge-Reihenfolge; sortierte Anzeige nur
     über den paginierten Endpunkt, siehe Ablauf 4)

3. **Medien-IDs sammeln:**
   - Erstelle Dictionary `mediaIdsByType` mit den Schlüsseln (Medientypen)
   - Für jeden Eintrag:
     - Addiere `entry.MediaId` zur Liste für `entry.MediaType`
     - Falls `ParentMediaType` nicht null: Addiere `ParentMediaId` zur Liste für `ParentMediaType`

4. **Titel in Batch laden:**
   - Für jeden Medientyp: Rufe `GetMediaTitlesAsync()` auf
   - Diese lädt alle Titel für die gegebenen IDs auf einmal (Batch-Optimierung)
   - Rückgabe: Dictionary von ID → Titel für diesen Typ

5. **Verwaiste Einträge bereinigen:**
   - Iteriere über alle Einträge:
     - Prüfe: Existiert der Titel für `entry.MediaType` und `entry.MediaId`?
     - Falls nein (Titel ist null):
       - Markiere Eintrag als Verwaist
       - Füge zu `orphans` Liste hinzu
       - Überspringe zu nächstem Eintrag (nicht in `result` aufnehmen)
     - Falls ja: Behalte den Eintrag für die DTO-Konvertierung in Schritt 7

6. **Verwaiste Einträge löschen:**
   - Falls `orphans.Count > 0`:
     - `db.PlaylistEntries.RemoveRange(orphans)`
     - `await db.SaveChangesAsync()` — Löscht alle Einträge, deren Medieninhalt nicht mehr existiert

7. **DTOs bauen und zurückgeben:**
   - `BuildEntryDtosAsync()` konvertiert die verbleibenden (nicht verwaisten) Einträge zu
     `DtoPlaylistEntry`, inklusive aufgelöster Bild-ID (`ResolvedPictureId`) und echter
     Freischaltungsprüfung (`IsAccessible`) für den aktuellen Benutzer über `IUnlockedMediaService`
     — dieselbe Methode wie in Ablauf 1 und 4
   - HTTP 200 OK mit Array von `DtoPlaylistEntry`

### Beteiligte Klassen

| Klasse | Methode | Zweck |
|--------|---------|-------|
| `PlaylistsController` | `GetPlaylistEntries()` | HTTP-Endpoint-Handler |
| `PlaylistService` | `GetPlaylistEntriesAsync()` | Geschäftslogik + Bereinigung |
| `PlaylistService` | `GetOwnedPlaylistAsync()` | Berechtigung + Existenz |
| `PlaylistService` | `GetMediaTitlesAsync()` | Batch-Titel-Lookup |
| `PlaylistService` | `BuildEntryDtosAsync()` | Entity → DTO Konvertierung (Titel, `ResolvedPictureId`, `IsAccessible`), gemeinsam mit Ablauf 1/4 |
| `IUnlockedMediaService` | `GetUnlockedMovieCollectionIdsForUserAsync()` / `GetUnlockedTVShowIdsForUserAsync()` | Bulk-Freischaltungsprüfung für `IsAccessible` |
| `ApplicationDbContext` | `PlaylistEntries` | DB-Zugriff |

### Diagramm

```mermaid
flowchart TD
    A[GET Request] --> B[ValidateAuth]
    B --> C[GetOwnedPlaylist]
    C -->|Not Owner| C1[403 Forbidden]
    C -->|Not Found| C2[404 Not Found]
    C -->|OK| D[LoadAllEntries]
    D --> E[CollectMediaIds]
    E --> F[LoadTitlesBatch]
    F --> G[IterateEntries]
    G --> H{TitleExists?}
    H -->|No| I[AddToOrphans]
    H -->|Yes| J[AddToResult]
    I --> K{MoreEntries?}
    J --> K
    K -->|Yes| G
    K -->|No| L{HasOrphans?}
    L -->|Yes| M[DeleteOrphans]
    L -->|No| N[ReturnResult]
    M --> N
    N --> O[200 OK]
```

---

## Ablauf 4: Mediensuche für die Playlist-Auswahl-Oberfläche (case-insensitiv, multi-type)

### Schritt-für-Schritt

**Auslöser:** Client (`MediaSearchSelector.razor`) ruft `GET /api/items?search={term}&includeIndividualMediaTypes=true` auf, während Benutzer einen Suchbegriff eingibt

**Hinweis:** Dieser Ablauf dokumentiert auch das bestehende Quellen-Browsing-Verhalten, wenn `includeIndividualMediaTypes` nicht gesetzt ist oder den Standardwert `false` hat.

1. **Parameter-Validierung:**
   - `ItemsController.Get()` empfängt Query-Parameter:
     - `search` (string, optional): Suchbegriff für Namensabfrage
     - `includeIndividualMediaTypes` (bool, Default `false`): Steuert, ob die 5 Medientypen (Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection) durchsucht werden oder nur 2 (TVShow, MovieCollection)
     - `mediaSourceId` (long, optional): Falls gesetzt, wird Quellen-Browsing-Modus aktiviert (filtert nach Medienquelle)
     - `page`, `size`, `genreId`: Weitere Filterparameter

2. **MediaEntryFilter konstruieren:**
   - Konstruiere ein `MediaEntryFilter`-Record mit:
     - `Search = search` (unverändert; die Kleinschreibungs-Faltung erfolgt erst in Schritt 4, Unicode-korrekt über `ToLowerInvariant()`)
     - `IncludeIndividualMediaTypes = includeIndividualMediaTypes`
     - `MediaSourceId = mediaSourceId`
     - `Page = page`
     - `Size = size`

3. **Abfrage-Strategie bestimmen:**
   - Falls `includeIndividualMediaTypes == true` (Playlist-Suche):
     - Rufe alle fünf `Get*EntriesAsync()`-Methoden auf:
       - `GetMovieCollectionEntriesAsync(filter)`
       - `GetTVShowEntriesAsync(filter)`
       - `GetMovieEntriesAsync(filter)` ← nur bei `includeIndividualMediaTypes == true`
       - `GetSeasonEntriesAsync(filter)` ← nur bei `includeIndividualMediaTypes == true`
       - `GetEpisodeEntriesAsync(filter)` ← nur bei `includeIndividualMediaTypes == true`
   - Sonst (Quellen-Browsing oder Standard):
     - Rufe nur die zwei ursprünglichen Methoden auf:
       - `GetMovieCollectionEntriesAsync(filter)`
       - `GetTVShowEntriesAsync(filter)`

4. **Case-insensitive, Unicode-korrekte Namenssuche in jeder Methode:**
   - Jede `Get*EntriesAsync()`-Methode ruft für den Namensvergleich `ApplySearchFilter<T>()` auf, eine über alle fünf Methoden geteilte Hilfsmethode:
     - Der Suchbegriff wird mit `ToLowerInvariant()` (kulturunabhängig statt kultursensitiv `ToLower()`) kleingeschrieben.
     - Die LIKE-Sonderzeichen `\`, `%` und `_` im Suchbegriff werden escaped (Backslash zuerst), damit sie als literale Zeichen statt als Wildcards gesucht werden.
     - Der Vergleich erfolgt über `EF.Functions.Like(AppDbFunctions.LowerInvariant(e.Name), $"%{escaped}%", "\\")`. `AppDbFunctions.LowerInvariant` ist eine als SQLite-Funktion (`lower_invariant`) registrierte, serverseitig ausgeführte Faltung über `string.ToLowerInvariant()` — im Gegensatz zu SQLite's eingebautem `lower()` faltet sie auch Ä/Ö/Ü/ß korrekt zu ä/ö/ü/ß (und umgekehrt bei der Suche nach Großbuchstaben-Varianten).
     - Beispiel: Suche nach „breaking" findet „Breaking Bad", „BREAKING_BAD", etc.; Suche nach „mörder“ findet „Mörder“ ebenso wie „MÖRDER“; ein Suchbegriff mit `%` oder `_` (z. B. Dateinamen-artige Titel) wird literal gesucht statt als Wildcard interpretiert.
   - Falls kein `search` angegeben: Alle Einträge des Medientyps

5. **Zugriffskontrolle in jeder Methode:**
   - Jede Methode prüft zusätzlich Berechtigungen über `IUnlockedMediaService`
   - Nur Einträge, auf die der aktuelle Benutzer zugriff hat (regulärer Quellenzugriff oder individuelle Freischaltung), werden in die Ergebnisse aufgenommen
   - Nicht zugängliche Inhalte erscheinen nicht in den Suchergebnissen

6. **Quellenzugriff filtern (nur bei `mediaSourceId`):**
   - Falls `mediaSourceId` gesetzt ist: Filtere nach `.Where(e => e.MediaSource.Id == mediaSourceId)`
   - Dies ist das bestehende Quellen-Browsing-Verhalten (z. B. Browse einer Netflix-Quelle zeigt nur Inhalte aus Netflix)

7. **Paginierung:**
   - Alle Ergebnisse aus den `Get*EntriesAsync()`-Methoden werden kombiniert (konkateniert)
   - Wende `Skip((page - 1) * size).Take(size)` an
   - Rückgabe: Liste von `MediaEntryDto` mit den angeforderten Einträgen

8. **Rückgabe:**
   - HTTP 200 OK mit Array von `MediaEntryDto`:
     ```csharp
     public class MediaEntryDto
     {
         public string Type { get; set; }       // "Movie", "TVShow", etc.
         public long Id { get; set; }           // Medien-ID
         public string Title { get; set; }      // Titel
         public long? PictureId { get; set; }   // Bild-ID oder null
     }
     ```

### Beispiel 1: Playlist-Medienauswahl mit case-insensitiver Suche

```
Client-Request: GET /api/items?search=breaking&includeIndividualMediaTypes=true

Server:
1. Konstruiere MediaEntryFilter(Search="breaking", IncludeIndividualMediaTypes=true)
2. Rufe alle 5 Get*EntriesAsync-Methoden auf
3. GetMovieEntriesAsync:
   - Suche: WHERE lower_invariant(Name) LIKE '%breaking%' ESCAPE '\'
   - Ergebnis: "Breaking Bad" (Movie), "Breaking Point" (Movie)
4. GetTVShowEntriesAsync:
   - Ergebnis: "Breaking Bad" (TVShow)
5. GetSeasonEntriesAsync:
   - Ergebnis: Staffel 1 von "Breaking Bad" (wenn Titel enthält "breaking")
6. ... etc.
7. Kombiniere und paginiere
8. Rückgabe: 5 Einträge (2 Movies, 1 TVShow, ...)
```

### Beispiel 2: Quellen-Browsing ohne Opt-in (Regression-Schutz)

```
Client-Request: GET /api/items?mediaSourceId=123&includeIndividualMediaTypes=false

Server:
1. Konstruiere MediaEntryFilter(MediaSourceId=123, IncludeIndividualMediaTypes=false)
2. Rufe nur 2 Get*EntriesAsync-Methoden auf (NICHT GetMovieEntriesAsync, etc.)
   - GetMovieCollectionEntriesAsync: WHERE MediaSourceId = 123
   - GetTVShowEntriesAsync: WHERE MediaSourceId = 123
3. Rückgabe: Nur 2 Medientypen, z. B. [MovieCollection, TVShow]
   (Keine Movies, Seasons, Episodes → kein Regression zur alten Oberfläche)
```

### Beteiligte Klassen

| Klasse | Methode | Zweck |
|--------|---------|-------|
| `ItemsController` | `Get(mediaSourceId, search, includeIndividualMediaTypes, ...)` | HTTP-Endpoint-Handler, Koordination der Abfragen |
| `ItemsController` | `GetMovieCollectionEntriesAsync()` | Abfrage MovieCollections mit case-insensitiver Namenssuche |
| `ItemsController` | `GetTVShowEntriesAsync()` | Abfrage TVShows mit case-insensitiver Namenssuche |
| `ItemsController` | `GetMovieEntriesAsync()` | Abfrage Movies mit case-insensitiver Namenssuche (nur wenn `includeIndividualMediaTypes == true`) |
| `ItemsController` | `GetSeasonEntriesAsync()` | Abfrage TVShowSeasons mit case-insensitiver Namenssuche (nur wenn `includeIndividualMediaTypes == true`) |
| `ItemsController` | `GetEpisodeEntriesAsync()` | Abfrage TVShowEpisodes mit case-insensitiver Namenssuche (nur wenn `includeIndividualMediaTypes == true`) |
| `ItemsController` | `ApplySearchFilter<T>()` | Hilfsmethode für case-insensitive, Unicode-korrekte `EF.Functions.Like(...)`-Suche mit LIKE-Escaping, wird von allen 5 Methoden genutzt |
| `AppDbFunctions` | `LowerInvariant()` | Als SQLite-Funktion `lower_invariant` registrierte, kulturunabhängige Kleinschreibungs-Faltung (`ToLowerInvariant()`); faltet Ä/Ö/Ü/ß korrekt, im Gegensatz zu SQLite's eingebautem `lower()` |
| `MediaEntryFilter` (Record) | — | Filter-Parameter-Objekt mit Feldern: `Search`, `IncludeIndividualMediaTypes`, `MediaSourceId`, `Page`, `Size`, `GenreId` |
| `VideoWebPlayerClient` | `RequestItemsAsync()` | Client-Methode für Playlist-Suche, ruft `RequestItemsCoreAsync()` mit `includeIndividualMediaTypes: true` auf |
| `VideoWebPlayerClient` | `RequestSourceItems()` | Client-Methode für Quellen-Browsing, ruft `RequestItemsCoreAsync()` mit `includeIndividualMediaTypes: false` (oder setzt Parameter nicht) auf |
| `VideoWebPlayerClient` | `RequestItemsCoreAsync()` | Kern-Methode für API-Aufruf, übernimmt den `includeIndividualMediaTypes`-Parameter in den Query-String |
| `MediaSearchSelector.razor` | — | UI-Komponente für Medienauswahl, ruft `Client.RequestItemsAsync()` auf |
| `IUnlockedMediaService` | `GetUnlockedMediaIdsAsync()` | Prüft Zugriff des Benutzers auf Individual Media (Movies, Seasons, Episodes) über übergeordnete Sammlung/Serie |

### Diagramm

```mermaid
flowchart TD
    A[GET /api/items] --> B{includeIndividualMediaTypes?}
    B -->|false oder nicht gesetzt| C[Quellen-Browsing]
    B -->|true| D[Playlist-Suche]
    C --> E[GetMovieCollectionEntriesAsync]
    C --> F[GetTVShowEntriesAsync]
    D --> G[GetMovieCollectionEntriesAsync]
    D --> H[GetTVShowEntriesAsync]
    D --> I[GetMovieEntriesAsync]
    D --> J[GetSeasonEntriesAsync]
    D --> K[GetEpisodeEntriesAsync]
    E --> L[case-insensitive Filter]
    F --> L
    G --> L
    H --> L
    I --> L
    J --> L
    K --> L
    L --> M[Access Control via IUnlockedMediaService]
    M --> N[Combine & Paginate]
    N --> O[200 OK]
```

---

## Ablauf 5: Sortierte, paginierte Einträge abrufen (Infinity-List)

### Schritt-für-Schritt

**Auslöser:** Client (`PlaylistEntriesList.razor`, über einen `IntersectionObserver`-Sentinel am
Listenende) ruft
`GET /api/playlists/{id}/entries/paged?pageNumber=N&pageSize=M` auf, initial für Seite 1 und dann
erneut mit fortlaufend höherem `pageNumber`, sobald der Anwender in der Liste weiter nach unten
scrollt.

1. **Parameter-Validierung (vor der Berechtigungsprüfung):**
   - `PlaylistsController.GetPlaylistEntriesPaged()` löst `pageSize` auf: übergebener Wert oder,
     falls keiner angegeben, `PlaylistSettings.DefaultPageSize`
   - `pageNumber < 1` → HTTP 400
   - `pageSize < 1` oder `pageSize > PlaylistSettings.MaxPageSize` → HTTP 400

2. **Berechtigungsprüfung:**
   - `PlaylistService.GetPlaylistEntriesPagedAsync()` ruft `GetOwnedPlaylistAsync()` auf
   - Falls nicht Besitzer → HTTP 403; falls Playlist nicht gefunden → HTTP 404

3. **Gültige Einträge laden (ohne Titel-Auflösung):**
   - `LoadValidPlaylistEntriesAsync()` lädt alle `PlaylistEntry` der Playlist
   - Für jeden Medientyp wird über `MediaTypeHandler.LoadExistingIdsAsync()` geprüft, welche
     referenzierten Medien-IDs noch existieren (nur ID-Existenzprüfung, keine Titel werden dabei
     geladen)
   - Einträge, deren Medieninhalt nicht mehr existiert, werden als Waisen erkannt und aus der DB
     entfernt (`RemoveRange` + `SaveChangesAsync`), analog zu Ablauf 3

4. **Sortierung:**
   - Bei `Playlist.SortMode == ByReleaseDate`: `SortPlaylistEntriesByReleaseDateAsync()` ermittelt
     für alle gültigen Einträge Erscheinungsdatum (`LoadReleaseDateAsync`) und Hierarchie-Sequenz
     (`GetHierarchySequenceAsync`) pro Medientyp und sortiert nach der Fallback-Kette
     Erscheinungsdatum → `ParentId` → `SequenceNumber` → `AddedAt` (siehe `playlists-business-rules.md`, BR-13)
   - Bei `Manual`: einfache Sortierung nach `AddedAt`

5. **Seite ausschneiden:**
   - `totalCount = sortedEntries.Count`
   - `skip = (pageNumber - 1) * pageSize`
   - `pageEntries = sortedEntries.Skip(skip).Take(pageSize)`

6. **Titel nur für die aktuelle Seite auflösen:**
   - `LoadTitlesForMediaRefsAsync()` lädt die Medientitel ausschließlich für `pageEntries` (nicht
     für die gesamte Playlist)
   - Ebenso werden die Titel etwaiger Eltern-Einträge (`ParentMediaType`/`ParentMediaId`) nur für
     die aktuelle Seite aufgelöst
   - Dadurch skaliert der Aufwand pro Anfrage mit der Seitengröße, nicht mit der Gesamtgröße der
     Playlist

7. **DTOs bauen und zurückgeben:**
   - `BuildEntryDtosAsync()` konvertiert `pageEntries` (nur die aktuelle Seite) zu
     `DtoPlaylistEntry`, inklusive für die Seite aufgelöster Bild-IDs (`ResolvedPictureId`, mit
     Fallback Poster → Banner → Fanart pro Medientyp via `LoadPictureIdsForMediaRefsAsync()`) und
     echter Freischaltungsprüfung (`IsAccessible`) über `IUnlockedMediaService`
     (`LoadUnlockedMediaIdsAsync()`, siehe Hinweis in `playlists-api.md`) — beides skaliert mit der
     Seitengröße, nicht mit der Gesamtgröße der Playlist
   - Rückgabe: `DtoPlaylistEntriesPagedResult { Entries, TotalCount, HasNextPage, PageNumber, PageSize }`
     mit `HasNextPage = skip + pageSize < totalCount`

**Client-seitiges Nachladen (`PlaylistEntriesList.razor`):**
- `LoadInitialPageAsync()` lädt beim Öffnen der Seite die erste Seite (`PageSize = 20`) und setzt
  `allEntries`, `hasMorePages`
- Die Liste rendert normal im Seitenfluss (kein `Virtualize`, keine eigene Scrollbox); solange
  `hasMorePages` gilt, steht am Listenende ein Sentinel-`div`. `OnAfterRenderAsync()` verbindet
  darauf den `IntersectionObserver` (`window.observeBottom`, `wwwroot/js/scroll.js`), der
  `OnBottomVisible()` aufruft, sobald der Sentinel sichtbar wird; `LoadNextPageAsync()` hängt dann
  die nächste Seite an und verbindet den Observer danach neu (ein noch sichtbarer Sentinel meldet
  sich sonst nicht erneut)
- Eine `SemaphoreSlim` (`loadPageSemaphore`) verhindert, dass bei schnellem Scrollen mehrere
  überlappende Ladevorgänge gleichzeitig laufen
- **Fehlerverhalten:** Schlägt ein Ladevorgang fehl (initial oder beim Nachladen), wird
  `loadMoreFailed` gesetzt, die Fehlermeldung (`entriesStatusMessage`) angezeigt und der Observer
  **nicht** neu verbunden — ein frisch verbundener Observer meldet einen sichtbaren Sentinel sofort,
  was bei anhaltendem Fehler eine Endlosschleife an Wiederholungsversuchen ausgelöst hätte. Solange
  `loadMoreFailed` gesetzt ist, ignoriert `OnBottomVisible()` Observer-Meldungen; erneut geladen
  wird erst über die Schaltfläche „Erneut versuchen" (`RetryLoadNextPageAsync()`) oder durch ein
  vollständiges Neuladen der Liste (`LoadInitialPageAsync()`, z. B. nach Hinzufügen/Entfernen)
- Nach der letzten Seite (`hasMorePages == false`) wird der Observer getrennt
  (`disconnectMediaSourceBottomObserver`), ebenso beim Entfernen der Komponente

### Beteiligte Klassen/Komponenten

| Klasse | Methode | Zweck |
|--------|---------|-------|
| `PlaylistsController` | `GetPlaylistEntriesPaged()` | HTTP-Endpoint-Handler inkl. Parameter-Validierung |
| `PlaylistService` | `GetPlaylistEntriesPagedAsync()` | Geschäftslogik: Laden, Sortieren, Paginieren, Titel nur für die Seite auflösen |
| `PlaylistService` | `LoadValidPlaylistEntriesAsync()` | Laden aller Einträge + Bereinigung verwaister Einträge (ohne Titel-Auflösung) |
| `PlaylistService` | `SortPlaylistEntriesByReleaseDateAsync()` | Ermittelt Sortierschlüssel und sortiert die vollständige, gültige Eintragsliste |
| `PlaylistService` | `LoadTitlesForMediaRefsAsync()` | Titel-Auflösung, beschränkt auf die übergebenen Referenzen (z. B. nur die aktuelle Seite) |
| `PlaylistService` | `BuildEntryDtosAsync()` | Entity → DTO Konvertierung (Titel, `ResolvedPictureId`, `IsAccessible`), gemeinsam mit Ablauf 1/3 |
| `PlaylistService` | `LoadPictureIdsForMediaRefsAsync()` | Bild-ID-Auflösung (Poster → Banner → Fanart), beschränkt auf die übergebenen Referenzen |
| `PlaylistService` | `LoadUnlockedMediaIdsAsync()` | Bulk-Freischaltungsprüfung über `IUnlockedMediaService` für alle Einträge der Seite |
| `PlaylistEntriesList.razor` | `LoadInitialPageAsync()` | Lädt die erste Seite beim Öffnen/Neuladen der Playlist |
| `PlaylistEntriesList.razor` | `OnBottomVisible()` / `LoadNextPageAsync()` | Vom Sentinel-Observer aufgerufen; hängt die nächste Seite an (nicht nach einem Ladefehler) |
| `PlaylistEntriesList.razor` | `RetryLoadNextPageAsync()` | „Erneut versuchen" nach einem Ladefehler |

### Diagramm

```mermaid
flowchart TD
    A[GET .../entries/paged] --> B{pageNumber/pageSize gültig?}
    B -->|Nein| B1[400 Bad Request]
    B -->|Ja| C[GetOwnedPlaylist]
    C -->|Not Owner| C1[403 Forbidden]
    C -->|Not Found| C2[404 Not Found]
    C -->|OK| D[LoadValidEntries ohne Titel]
    D --> E{SortMode?}
    E -->|ByReleaseDate| F[SortByReleaseDateFallback]
    E -->|Manual| G[SortByAddedAt]
    F --> H[Skip/Take Seite]
    G --> H
    H --> I[LoadTitles nur für Seite]
    I --> J[BuildDtoPlaylistEntriesPagedResult]
    J --> K[200 OK]
```

---

## Ablauf 6: Playlist-Abbildung (Cover) — Upload, Regenerierung, Abruf, Löschen

Eine Playlist kann genau ein Cover besitzen: ein vom Besitzer hochgeladenes Bild
(`CoverPictureIsUserUploaded = true`) oder eine automatisch erzeugte Collage (`false`). Ist kein
Cover gesetzt (`CoverPictureId = null`), zeigen Übersicht und Detailseite den
`PlaylistCoverPlaceholder` (aus der Playlist-Id deterministisch abgeleiteter Farbverlauf mit
Playlist-Symbol). Alle mutierenden Endpunkte erfordern Besitz; der Abruf steht jedem
angemeldeten Benutzer offen.

### Ablauf 6a: Cover hochladen

**Auslöser:** `PlaylistDetail.razor` öffnet `PlaylistCoverUploadDialog.razor` über die
Upload-Schaltfläche; der Dialog ruft `IPlaylistApiClient.UploadPlaylistCoverAsync()` auf, das
`POST /api/playlists/{id}/cover/upload` als `multipart/form-data` sendet.

**Client-seitig (`PlaylistCoverUploadDialog.razor`):**
- Vorvalidierung nur für UX: gemeldeter `ContentType` gegen `AllowedCoverImageFormats`, Dateigröße
  gegen `MaxCoverImageSizeBytes` (ein fehlender ContentType wird nicht vorab abgelehnt — die
  maßgebliche Prüfung erfolgt serverseitig)
- Bildvorschau als Data-URL, Anzeige von Dateiname und -größe, `accept`-Attribut aus der
  konfigurierten Formatliste
- `OpenReadStream(maxAllowedSize: MaxCoverImageSizeBytes)` begrenzt die clientseitige
  Pufferung bereits beim Lesen
- Bei Erfolg: `OnUploaded`-Callback → `PlaylistDetail` schließt den Dialog und lädt die Playlist
  neu (`HandleCoverUploadedAsync` → `LoadPlaylistAsync`)

**Server-seitig (`PlaylistsController.UploadPlaylistCover`):**

1. `CheckLogedIn()` — Authentifizierung
2. `file is null || file.Length == 0` → HTTP 400 (`"Es wurde keine Datei ausgewählt."`)
3. `file.Length > MaxCoverImageSizeBytes` → HTTP 400 **vor** jedem Stream-Zugriff — ein übergroßer
   Upload wird nie in den Speicher gepuffert. Bewusst kein `[RequestSizeLimit]`: das Limit ist
   zur Laufzeit konfigurierbar und soll nicht mit einem Compile-Zeit-Attribut auseinanderlaufen.
4. Erst danach: Datei in `byte[]` puffern (`CopyToAsync` in `MemoryStream`)
5. `PlaylistService.SetPlaylistCoverAsync()`:
   - `GetOwnedPlaylistAsync()` → 403/404 bei Fremdzugriff bzw. nicht existierender Playlist
   - `PlaylistCoverValidator.ValidateUploadAsync()` prüft in dieser Reihenfolge:
     1. Gemeldeter `ContentType` in `AllowedCoverImageFormats`-Allowlist (Case-insensitive;
        leerer/unbekannter Typ → Fehler mit „Friendly Name", z. B. `"Format BMP wird nicht
        unterstützt. Erlaubte Formate: JPEG, PNG, WebP."`)
     2. `fileSize > MaxCoverImageSizeBytes` → `"Datei zu groß, max. X MB erlaubt."`
     3. Erkennung via ImageSharp `Image.Identify()` (nur Bildkopf; jede dabei geworfene Ausnahme →
        `"Datei ist kein gültiges Bild."`)
     4. Das **tatsächlich erkannte** Format (`DecodedImageFormat.DefaultMimeType`) muss ebenfalls in
        der Allowlist stehen (sonst dieselbe „Format … wird nicht unterstützt"-Meldung); der
        erkannte MIME-Type wird im Ergebnis mitgegeben und später als `Picture.ContentType`
        gespeichert
     5. Pixelgrenzen am Bildkopf, **vor** der Dekodierung: `MaxCoverImageWidthPixels`,
        `MaxCoverImageHeightPixels`, `MaxCoverImageTotalPixels` → `"Bild zu groß (B x H Pixel). …"`
     6. Bei JPEG: `JpegIntegrityChecker.Check()` (Marker-Struktur + Huffman-Scan-Konsistenz, ohne
        Pixelrekonstruktion), danach vollständige strikte Dekodierung (`Image.Load` mit
        `MaxFrames = 1`, `SkipMetadata = true`); schlägt eines davon fehl →
        `"Datei ist beschädigt oder unvollständig und kann nicht als Bild gelesen werden."`
   - Validierungsfehler → `InvalidOperationException` → HTTP 400 (generisches Fehler-Mapping,
     kein `DtoPlaylistCoverResult`)
   - Bei Erfolg tragen `Width`/`Height` des Ergebnisses die ermittelten Bildabmessungen
6. Neues `Picture` anlegen: `Type = "cover"`, `Data` = Upload-Bytes, `ContentType` = aus dem Bild
   erkannter MIME-Type (nicht der vom Client gemeldete), `Width`/`Height` aus der Validierung, `IsGeneratedBackground = false`,
   `PlaylistId` = Playlist-Id
7. `ReplaceCoverPictureAsync(playlist, newPicture, isUserUploaded: true)` — siehe unten
8. HTTP 200 mit `DtoPlaylistCoverResult { Success = true, Message = "Bild erfolgreich
   hochgeladen.", PictureId }`

### Ablauf 6b: Cover als Collage neu erzeugen

**Auslöser:** „Cover neu erzeugen"-Schaltfläche in `PlaylistDetail.razor` →
`RegenerateCoverAsync()` → `IPlaylistApiClient.RegeneratePlaylistCoverAsync()` →
`POST /api/playlists/{id}/cover/regenerate`.

1. `CheckLogedIn()`; `PlaylistService.GeneratePlaylistCoverAsync(id, userId,
   confirmReplaceUploadedCover)` → `GetOwnedPlaylistAsync()` (403/404). `confirmReplaceUploadedCover`
   stammt aus dem Query-Parameter gleichen Namens (Standard `false`)
2. `PlaylistCoverImageGenerator.GeneratePlaylistCoverAsync(playlistId)`:
   - `CollectOrderedPictureIdsAsync()`:
     - Lädt alle `PlaylistEntry` der Playlist, sortiert nach `AddedAt`, dann `Id`
     - Weist jedem Eintrag eine Prioritätsstufe zu (`GetMediaTypePriority`):
       `TVShow`/`TVShowSeason` = 0, `TVShowEpisode` = 1, `MovieCollection` = 2, `Movie` = 3,
       unbekannte Typen werden verworfen
     - Stabile Sortierung nach Stufe — innerhalb einer Stufe bleibt die
       Hinzufüge-Reihenfolge erhalten; die Reihenfolge in der Playlist kann also nie einen
       niedriger priorisierten Typ vor einen höher priorisierten ziehen
     - `BuildPosterLookupAsync()` löst pro Referenz die `PosterPictureId` auf; `TVShowSeason`
       erhält das Poster der Eltern-`TVShow` (Staffeln haben kein eigenes Poster)
     - Iteriert in Prioritätsreihenfolge und sammelt bis zu `MaxImages = 5`
       deduplizierte Bild-IDs
   - Lädt die `Data`-Bytes der gesammelten `Picture`-Zeilen in dieser Reihenfolge
   - Komponiert via `HomeBackgroundImageGenerator.Compose()` (Cross-Fade-Collage,
     `TransitionWidth = 32`) eine JPEG-Datei in den konfigurierten Abmessungen
     (`GeneratedCoverWidthPixels` × `GeneratedCoverHeightPixels`, Qualität
     `GeneratedCoverJpegQuality`)
   - Keine Bilder vorhanden oder Generierungsfehler → `null` (Fehler werden geloggt, nicht
     geworfen — konsistent mit den übrigen Bild-Generatoren)
3. Bei `null`: HTTP 200 mit `DtoPlaylistCoverResult { Success = false, Message = "Keine Bilder
   verfügbar." }` — das bestehende Cover bleibt **unverändert** (auch ein hochgeladenes)
4. **Sicherheitsabfrage:** Ist `Playlist.CoverPictureIsUserUploaded` gesetzt und
   `confirmReplaceUploadedCover` nicht `true`, wirft der Service (erst nach erfolgreicher
   Collage-Erzeugung, damit ohne Quellbilder keine sinnlose Rückfrage entsteht) eine
   `UploadedCoverReplacementConfirmationRequiredException`; nichts wird geändert. Der Controller
   antwortet mit HTTP 409 Conflict und `DtoRegeneratePlaylistCoverConflictResponse {
   IsUploadedCoverReplacementConfirmationRequired = true }` (Client: `HttpRequestException` mit
   `StatusCode = Conflict` → `PlaylistDetail` zeigt `PlaylistCoverRegenerateConfirmationDialog`;
   „Ja, ersetzen" wiederholt den Aufruf mit `confirmReplaceUploadedCover = true`)
5. Bei Erfolg: neues `Picture` mit `Type = "cover"`, `ContentType = "image/jpeg"`,
   `Width`/`Height` = konfigurierte Collage-Abmessungen, `IsGeneratedBackground = true`,
   `PlaylistId` = Playlist-Id; `ReplaceCoverPictureAsync(..., isUserUploaded: false)`
   ersetzt das bisherige Cover — ein hochgeladenes nur nach der Bestätigung aus Schritt 4 (siehe
   BR-24/BR-25 in `playlists-business-rules.md`)
6. HTTP 200 mit `{ Success = true, Message = "Cover neu erzeugt.", PictureId }`;
   `PlaylistDetail` lädt die Playlist neu; bei `Success = false` wird `result.Message` als
   `coverStatusMessage` angezeigt

**Wichtig:** Die Regenerierung läuft ausschließlich über diesen expliziten Pfad —
`AddMediaToPlaylistAsync`/`RemoveMediaFromPlaylistAsync` und die automatische
Backfill-Mechanik lassen das Cover bewusst unangetastet (kein automatisches Neu Erzeugen bei
Inhaltsänderungen, anders als die Genre-Ableitung aus Schritt 9).

### Ablauf 6c: Cover-Bild abrufen

**Auslöser:** `<img>`-Tags in `PlaylistsList.razor` (Kachel) und `PlaylistDetail.razor`
(Kopfbereich) mit `GET /api/playlists/{id}/cover`.

1. `CheckLogedIn()` — jeder angemeldete Benutzer darf das Bild sehen (Zugriffsniveau wie
   `PicturesController.GetPicture`; nur mutierende Aktionen erfordern Besitz)
2. `GetPlaylistCoverAsync()` lädt die Playlist `AsNoTracking`, liest `CoverPictureId` und lädt
   das `Picture` — `null`, wenn Playlist oder Cover fehlen
3. `picture is null || Data leer` → HTTP 404; sonst `File(picture.Data, picture.ContentType ??
   "image/jpeg")`

**Client-seitige Anzeige:**
- Die Bild-URL trägt `?access_token={token}` (ein `<img>`-Request kann keinen
  `Authorization`-Header senden) plus `&v={CoverPictureId}` als Cache-Buster — die Id ändert sich
  bei jedem Upload/jeder Regenerierung, sodass der Browser das neue Bild lädt statt eine
  zwischengespeicherte Antwort desselben `/cover`-Endpunkts wiederzuverwenden
- Der `PlaylistCoverPlaceholder` liegt immer **unter** dem `<img>`; `onerror` blendet das Bild
  aus, sodass bei Ladefehlern (oder wenn kein Cover gesetzt ist, dann wird das `<img>` gar nicht
  gerendert) der Platzhalter sichtbar bleibt — ohne dediziertes JS-Interop

### Ablauf 6d: Cover löschen

**Auslöser:** `IPlaylistApiClient.DeletePlaylistCoverAsync()` → `DELETE /api/playlists/{id}/cover`.

1. `CheckLogedIn()`; `DeletePlaylistCoverAsync()` → `GetOwnedPlaylistAsync()` (403/404)
2. Kein Cover gesetzt → No-Op, trotzdem HTTP 200 `{ Success = true }`
3. Sonst: `CoverPictureId = null`, `CoverPictureIsUserUploaded = false`, die `Picture`-Zeile wird
   entfernt — in einem `SaveChangesAsync`

### `ReplaceCoverPictureAsync` (gemeinsame Austausch-Logik)

Upload und Regenerierung enden beide hier:

```csharp
var oldPictureId = playlist.CoverPictureId;
await _db.Pictures.AddAsync(newPicture, cancellationToken);
playlist.CoverPicture = newPicture;                    // Navigation statt Id: EF löst die neue
playlist.CoverPictureIsUserUploaded = isUserUploaded;  // Id nach dem Insert selbst auf
if (oldPictureId.HasValue) { /* altes Picture laden und Remove() */ }
await _db.SaveChangesAsync(cancellationToken);
```

Insert des neuen Bildes, Update des Fremdschlüssels und Löschen des alten Bildes laufen in
**einer** Transaktion — bei zwei getrennten `SaveChangesAsync`-Aufrufen könnte ein Fehler des
zweiten Aufrufs das bereits committed neue Bild dauerhaft verweisen lassen (siehe
`VideoWebPlayer/Services/PlaylistService.cs`, XML-Doku zu `ReplaceCoverPictureAsync`).

### Beteiligte Klassen/Komponenten

| Klasse | Methode | Zweck |
|--------|---------|-------|
| `PlaylistsController` | `UploadPlaylistCover()` | Upload-Endpunkt inkl. Größen-Vorprüfung vor dem Puffern |
| `PlaylistsController` | `RegeneratePlaylistCover()` | Regenerierungs-Endpunkt, mappt `null` auf `Success = false` |
| `PlaylistsController` | `GetPlaylistCover()` | Bild-Auslieferung (`FileResult`) für alle angemeldeten Benutzer |
| `PlaylistsController` | `DeletePlaylistCover()` | Lösch-Endpunkt |
| `PlaylistService` | `SetPlaylistCoverAsync()` | Upload: Besitzprüfung, Validierung, `Picture`-Anlage |
| `PlaylistService` | `GeneratePlaylistCoverAsync()` | Regenerierung: Besitzprüfung, Generator-Aufruf, `Picture`-Anlage |
| `PlaylistService` | `GetPlaylistCoverAsync()` | Cover-Auflösung für den GET-Endpunkt (`AsNoTracking`) |
| `PlaylistService` | `DeletePlaylistCoverAsync()` | Referenz leeren + `Picture` löschen (No-Op ohne Cover) |
| `PlaylistService` | `ReplaceCoverPictureAsync()` | Atomarer Austausch: neues Bild einfügen, FK setzen, altes löschen |
| `PlaylistService` | `DeletePlaylistAsync()` | Löscht das Cover-`Picture` beim Löschen der Playlist mit (BR-26) |
| `PlaylistCoverValidator` | `ValidateUploadAsync()` | Allowlist-, Größen- und Echtheitsprüfung inkl. Bildabmessungen |
| `PlaylistCoverImageGenerator` | `GeneratePlaylistCoverAsync()` | Collagen-Erzeugung (JPEG-Bytes), persistiert nichts selbst |
| `PlaylistCoverImageGenerator` | `CollectOrderedPictureIdsAsync()` | Prioritäts- und Reihenfolgenlogik (max. 5 deduplizierte Poster-IDs) |
| `PlaylistCoverImageGenerator` | `BuildPosterLookupAsync()` | Bulk-Auflösung der `PosterPictureId` pro Referenz, Staffel-Fallback auf Serienposter |
| `HomeBackgroundImageGenerator` | `Compose()` | Wiederverwendeter Collagen-Renderer (Cross-Fade, `TransitionWidth = 32`) |
| `PlaylistCoverUploadDialog.razor` | — | Upload-Dialog: Vorvalidierung, Vorschau, `InputFile`, Upload via Client |
| `PlaylistDetail.razor` | `OpenUploadDialog()`/`RegenerateCoverAsync()`/`CoverImageUrl` | Kopfbereich: Cover-Anzeige, Upload- und Regenerieren-Aktionen, Statusmeldung |
| `PlaylistsList.razor` | `GetCoverImageUrl()` | Kachel-Cover-URL mit `access_token` + `v={CoverPictureId}` |
| `PlaylistCoverPlaceholder.razor` | — | Deterministischer Platzhalter (Farbverlauf aus `PlaylistId * 47 % 360`) |
| `IPlaylistApiClient`/`VideoWebPlayerClient` | `UploadPlaylistCoverAsync()`/`RegeneratePlaylistCoverAsync()`/`DeletePlaylistCoverAsync()` | Client-Methoden; Upload sendet `multipart/form-data` |
| `DtoPlaylistCoverResult` | — | Ergebnis-Typ (`Success`, `Message`, `PictureId`) |
| `PlaylistSettings` | — | `AllowedCoverImageFormats`, `MaxCoverImageSizeBytes`, `MaxCoverImageWidthPixels`, `MaxCoverImageHeightPixels`, `MaxCoverImageTotalPixels`, `GeneratedCoverWidthPixels`, `GeneratedCoverHeightPixels`, `GeneratedCoverJpegQuality` |

### Diagramm (Upload / Regenerierung)

```mermaid
flowchart TD
    A[POST cover/upload] --> B[CheckLogedIn]
    B --> C{file leer?}
    C -->|Ja| C1[400 keine Datei]
    C -->|Nein| D{file.Length größer max?}
    D -->|Ja| D1[400 zu groß - vor dem Puffern]
    D -->|Nein| E[Puffern zu byte-Array]
    E --> F[SetPlaylistCoverAsync]
    F --> G[GetOwnedPlaylistAsync]
    G -->|403/404| G1[Fehler]
    G -->|OK| H[PlaylistCoverValidator]
    H -->|ungültig| H1[400 InvalidOperationException]
    H -->|OK| I[Neues Picture IsGeneratedBackground=false]
    I --> J[ReplaceCoverPictureAsync]

    K[POST cover/regenerate] --> L[GeneratePlaylistCoverAsync]
    L --> G2[GetOwnedPlaylistAsync]
    G2 -->|403/404| G3[Fehler]
    G2 -->|OK| M[CollectOrderedPictureIds: Prioritaet TVShow/Season, Episode, Collection, Movie]
    M --> N{Bilder gefunden?}
    N -->|Nein| N1[200 Success=false - Cover bleibt]
    N -->|Ja| O[Compose JPEG-Collage]
    O --> P[Neues Picture IsGeneratedBackground=true]
    P --> J
    J --> Q[Ein SaveChangesAsync: Insert + FK + altes Bild löschen]
    Q --> R[200 Success=true + PictureId]
```

---

## Ablauf 7: Öffentliche Playlists (Schritt 11)

### 7a: Lesender Zugriff (Betrachter)

1. `PlaylistsController` liest den Anfragenden (`CurrentUser`) und ruft die Service-Methode mit dessen ID auf.
2. `PlaylistService.GetReadablePlaylistAsync` lädt die Playlist (`KeyNotFoundException` → 404) und prüft
   `UserId == userId || IsPublic` (sonst `PlaylistAccessDeniedException` → 403); zurückgegeben wird zusätzlich `isOwner`.
3. `LoadValidPlaylistEntriesAsync(playlist, removeOrphans: isOwner)`: verwaiste Einträge werden nur für den Besitzer
   entfernt (BR-30); Betrachter erhalten die gültigen Einträge.
4. `PlaylistEntryAccessResolver.ResolveAccessibilityAsync(entries, userId, …)` mit der ID des **Anfragenden** (BR-29);
   dieselbe ID wird von `StartPlaylistAsync`/`FindAdjacentPlayableEntryAsync` (Next/Previous/Advance) verwendet.
5. `ToDto(playlist, genres, requesterId)` schneidet das DTO auf den Anfragenden zu (`IsOwner`, BR-28).

### 7b: Schreibender Zugriff

Jede schreibende Methode beginnt mit `GetOwnedPlaylistAsync` (Besitzprüfung → 403 für alle Nicht-Besitzer).
`SetPlaylistPublicAsync` prüft zusätzlich den Administrator-Status (vom Controller aus `CurrentUser.IsAdmin`).

### 7c: Kennzeichnung entfernen

`SetPlaylistPublicAsync(…, isPublic: false)` setzt `IsPublic = false` am getrackten Entity und ruft
`ContinueWatchingService.DetachOtherUsersFromPlaylistAsync`, das für alle anderen Anwender (a) gebundene Einträge
entfernt, wenn bereits ein Eintrag ohne Playlist-Bezug für dasselbe Video existiert, und (b) sonst `PlaylistId = null`
setzt — alles in **einem** `SaveChangesAsync` mit der Playlist-Änderung. Danach werden die betroffenen Anwender per
SignalR benachrichtigt.

### 7d: Titel entfernen / Playlist löschen

- `RemoveMediaFromPlaylistAsync`: `ContinueWatchingService.GetUserIdsWithPlaylistBoundEntryAsync` liefert alle Anwender mit
  gebundenem Eintrag für den Titel. Ist der Besitzer darunter und nicht bestätigt → `ContinueWatchingConfirmationRequiredException`
  (409). Für jeden Anwender wird der nächste für **ihn** zugängliche Titel vor dem Entfernen bestimmt
  (`ResolveNextEntriesPerUserAsync`) und nach dem Entfernen über `ResolvePlaylistEntryRemovalAsync` angewendet.
- Dieselbe Auflösung gilt für die stillen Pfade (Waisen-Bereinigung des Besitzers, `ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync`).
- `DeletePlaylistAsync`: `ResolvePlaylistDeletionConflictsAsync(playlistId)` löst die UNIQUE-Konflikte für **alle** Anwender
  (nicht nur den Besitzer); `UserManagement.DeleteUser` ruft vor dem Löschen eines Kontos
  `ResolveDeletionConflictsForOwnedPlaylistsAsync` auf (Kaskade über die Playlists des Kontos).

### Beteiligte Klassen/Komponenten

`PlaylistsController` (`GET public`, `PUT {id}/public`), `PlaylistService`, `ContinueWatchingService`,
`PlaylistDetail`/`PlaylistEntriesList` (Nur-Lese-Modus), `PlaylistsList` (zusammengefasste Übersicht: lädt `GET /api/playlists` und `GET /api/playlists/public` parallel mit demselben Genre-Filter, führt sie clientseitig zusammen — eigene zuerst, fremde öffentliche danach, Duplikate über die Playlist-Id ausgeschlossen — und filtert nach „Alle/Eigene/Öffentliche"; Alias-Route `/playlists/public` wählt den Filter „Öffentliche" vor), `PlaylistTile` (mit Fremd-Symbol für fremde Playlists).

---

## Cascade-Logik im Detail

### MediaTypeHandler

Die Cascade-Logik wird durch `MediaTypeHandler` implementiert — ein Dictionary, das für jeden Medientyp definiert, wie Kinder geladen werden.

```csharp
private sealed class MediaTypeHandler
{
    public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, string>>> LoadTitlesAsync { get; init; }
    
    public Func<ApplicationDbContext, long, CancellationToken, Task<List<(string type, long id)>>>? LoadCascadeChildrenAsync { get; init; }
}
```

**Handler pro Typ:**

| Typ | `LoadTitlesAsync` | `LoadCascadeChildrenAsync` |
|-----|-------------------|---------------------------|
| `Movie` | Titel aus `Movies` | null (keine Kinder) |
| `TVShowEpisode` | Titel aus `TVShowEpisodes` | null |
| `TVShowSeason` | Titel aus `TVShowSeasons` | Alle Episoden dieser Staffel |
| `TVShow` | Titel aus `TVShows` | Alle Staffeln + Episoden dieser Serie |
| `MovieCollection` | Titel aus `MovieCollections` | Alle Filme dieser Sammlung |

### Beispiel: Series hinzufügen

```
Hinzufügen: TVShow ID=100

1. LoadCascadeChildrenAsync(db, 100):
   - Lade alle TVShowSeason mit ShowId=100
   - Beispiel: Season 1 (ID=200), Season 2 (ID=201)
   - Für jede Season: Lade alle TVShowEpisode
   - Season 1: Episode 1 (ID=1001), Episode 2 (ID=1002)
   - Season 2: Episode 3 (ID=1003), Episode 4 (ID=1004)

2. Cascade-Liste:
   (TVShow, 100)      ← Top-Level
   (TVShowSeason, 200)
   (TVShowSeason, 201)
   (TVShowEpisode, 1001)
   (TVShowEpisode, 1002)
   (TVShowEpisode, 1003)
   (TVShowEpisode, 1004)

3. Duplikat-Filter:
   - Behalte nur Einträge, die noch nicht im PlaylistEntry existieren
   - Falls z. B. Episode 1002 bereits vorhanden: Überspringe

4. Einträge erstellen:
   - PlaylistEntry(mediaType=TVShow, mediaId=100, parentMediaType=null, parentMediaId=null)
   - PlaylistEntry(mediaType=TVShowSeason, mediaId=200, parentMediaType=TVShow, parentMediaId=100)
   - ... etc.
```

---

## Datenbank-Transaktionen

Alle Schreib-Operationen verwenden implizite Transaktionen via Entity Framework Core:

- **`AddMediaToPlaylistAsync()`:** Eine Transaktion speichert Top-Level + Cascade-Einträge atomar
- **`RemoveMediaFromPlaylistAsync()`:** Eine Transaktion löscht einen Eintrag
- **`GetPlaylistEntriesAsync()`:** Separate Transaktionen für Lesevorgänge (keine Sperrungen) und Löschung verwaister Einträge (wenn vorhanden)
- **`ReplaceCoverPictureAsync()` (Upload/Regenerierung):** Eine Transaktion fügt das neue Cover-`Picture` ein, aktualisiert `Playlist.CoverPictureId`/`CoverPictureIsUserUploaded` und löscht das bisherige Bild (Ablauf 6)
- **`DeletePlaylistCoverAsync()` / `DeletePlaylistAsync()`:** Eine Transaktion leert die Cover-Referenz bzw. löscht Playlist samt Cover-`Picture`

Fehler während `SaveChangesAsync()` führen zu Rollback — alle oder keine Einträge werden gespeichert.

---

## Performance-Überlegungen

### Batch-Titel-Lookup

Statt jeden Titel einzeln zu laden, werden alle Titel pro Medientyp in einem Aufruf geladen:

```csharp
// Effizient: Ein Query pro Medientyp
var titlesByType = new Dictionary<string, Dictionary<long, string>>();
foreach (var (mediaType, mediaIds) in mediaIdsByType)
    titlesByType[mediaType] = await GetMediaTitlesAsync(mediaType, mediaIds, cancellationToken);
```

Ohne Batch-Optimierung würden 100 Einträge 100 separate Queries erfordern. Mit Batch-Optimierung: maximal 5 Queries (eine pro Medientyp).

### Orphan-Bereinigung bei Read

Verwaiste Einträge werden aktiv beim Abrufen der Liste gelöscht. Dies verhindert:
- Weiterwachsen der `PlaylistEntry`-Tabelle mit ungültigen Einträgen
- Debugging-Schwierigkeiten bei der Diagnose von Duplikaten

Nachteil: Der Read-Vorgang könnte bei vielen Waisen einen Write-Vorgang auslösen. Dies wird als akzeptabel erachtet, da Löschungen von Medieninhalten selten sind.

---

## Fehlerbehandlung

Alle echten Fehlerfälle führen zu expliziten HTTP-Status-Codes:

| Exception | Mapping | HTTP-Status |
|-----------|---------|------------|
| `PlaylistAccessDeniedException` | Direkt | 403 Forbidden |
| `KeyNotFoundException` | Direkt | 404 Not Found |
| `InvalidOperationException` (Max-Item-Limit überschritten) | `MapInvalidOperationException()` | 400 Bad Request |
| `InvalidOperationException` (ungültiger MediaType) | `MapInvalidOperationException()` | 400 Bad Request |
| Ungültige `pageNumber`/`pageSize` (kein Exception, direkte Prüfung im Controller) | Direkte `BadRequest()`-Rückgabe | 400 Bad Request |
| Cover-Upload: fehlende/übergroße Datei (direkte Prüfung im Controller, vor dem Puffern) | Direkte `BadRequest()`-Rückgabe | 400 Bad Request |
| Cover-Upload: `InvalidOperationException` aus `PlaylistCoverValidator` (Format, Echtheit) | `MapInvalidOperationException()` | 400 Bad Request |
| Cover-Regenerierung ohne Quellbilder | Kein Fehler: `DtoPlaylistCoverResult { Success = false }` | 200 OK |
| Andere Exceptions | Generischer Error | 500 Internal Server Error |

**Wichtig:** Duplikate führen **nicht** mehr zu einem Fehler. Sie werden übersprungen, gezählt und in der `DtoPlaylistAddResult`-Message beschrieben. Die Antwort bleibt immer HTTP 200 OK.
