# Playlists – Geschäftslogik und Business Rules

## Übersicht

Dieses Dokument beschreibt die Validierungsregeln, Geschäftslogik und Entscheidungen, die beim Umgang mit Playlist-Einträgen gelten.

---

## BR-1: Duplikatsprüfung pro Playlist

**Regel:** Ein Medieninhalt darf in ein und derselben Playlist nur einmal vorkommen.

**Definition Duplikat:** Zwei `PlaylistEntry`-Reihen sind Duplikate, wenn sie die gleiche Kombination aus `(PlaylistId, MediaType, MediaId)` haben (nach Normalisierung des `MediaType`, siehe BR-6).

**Implementierung:**
- Composite Unique Constraint in der Datenbank auf Spalten `(PlaylistId, MediaType, MediaId)`
- Zusätzliche Validierung im Service vor dem Einfügen: Lade alle bestehenden Einträge als `HashSet<(MediaType, MediaId)>` und prüfe den (normalisierten) Top-Level-Eintrag dagegen

**Fehlerbehandlung:** Versuch, einen bereits vorhandenen Eintrag hinzuzufügen → Eintrag wird übersprungen und in `SkippedDuplicateCount` gezählt; es wird **keine Exception geworfen** und **kein HTTP 409** zurückgegeben. Die Antwort bleibt `HTTP 200 OK` (siehe BR-2).

**Beispiel:**
```
Playlist 1:
- Film: "The Matrix" (Movie, ID=100)

Versuch: Hinzufügen von Film "The Matrix" (Movie, ID=100) erneut
Ergebnis: HTTP 200 OK — AddedEntries=[], SkippedDuplicateCount=1,
          Message="Alle 1 Titel waren bereits vorhanden."
```

---

## BR-2: Einheitliches Duplikat-Handling (Top-Level und Cascade)

**Regel:** Werden Duplikate beim Hinzufügen erkannt — egal ob der angeforderte Top-Level-Eintrag selbst oder ein Cascade-Kind —, werden diese übersprungen ohne Fehler. Die Anfrage schlägt in keinem Fall wegen eines Duplikats fehl.

**Hintergrund:** Wenn Benutzer eine Serie hinzufügen und einige Episoden bereits einzeln in der Playlist sind (oder die ganze Serie erneut hinzugefügt wird), soll die Operation nicht fehlschlagen, sondern nur die neuen Einträge hinzufügen und dem Benutzer eine verständliche Zusammenfassung liefern.

**Implementierung:**
- Lade alle bestehenden Einträge als `HashSet<(MediaType, MediaId)>`
- Prüfe den Top-Level-Eintrag gegen dieses Set: Duplikat → `SkippedDuplicateCount++`, sonst neuer Eintrag
- Filtere Cascade-Kinder genauso: Duplikat → `SkippedDuplicateCount++`, sonst neuer Eintrag
- Speichere nur die neuen Einträge (`AddedEntries`) in einem Rutsch

**Benutzer-Feedback:** Kein Fehler; `DtoPlaylistAddResult.Message` beschreibt das Ergebnis in Textform:
- `entriesToAdd.Count > 0 && SkippedDuplicateCount > 0`: `"{N} Titel hinzugefügt, {M} bereits vorhanden und übersprungen."`
- `entriesToAdd.Count > 0 && SkippedDuplicateCount == 0`: `"{N} Titel hinzugefügt."`
- `entriesToAdd.Count == 0`: `"Alle {M} Titel waren bereits vorhanden."`

**Beispiel:**
```
Playlist:
- Episode 1 der Serie A (TVShowEpisode, ID=1001)
- Episode 2 der Serie A (TVShowEpisode, ID=1002)

Benutzer: Hinzufügen von Serie A (TVShow, ID=100)

Cascade-Ergebnis:
- Serie A (TVShow, ID=100) — neu hinzugefügt
- Staffel 1 (TVShowSeason, ID=200) — neu hinzugefügt
- Episode 1 (TVShowEpisode, ID=1001) — Duplikat, übersprungen
- Episode 2 (TVShowEpisode, ID=1002) — Duplikat, übersprungen
- Episode 3 (TVShowEpisode, ID=1003) — neu hinzugefügt

Benutzer sieht: HTTP 200 OK, Message="3 Titel hinzugefügt, 2 bereits vorhanden und übersprungen."
```

---

## BR-3: Cascade-Abfrage für Sammelwerke

**Regel:** Beim Hinzufügen von Sammelwerken werden automatisch alle Kind-Einträge mitaufgenommen.

**Gültige Kaskaden:**

| Top-Level Typ | Cascade-Kinder |
|---------------|----------------|
| `TVShow` (Serie) | Alle `TVShowSeason` + alle deren `TVShowEpisode` |
| `TVShowSeason` (Staffel) | Alle `TVShowEpisode` dieser Staffel |
| `MovieCollection` (Filmsammlung) | Alle `Movie` dieser Sammlung |
| `Movie` (Film) | Keine (Blattknoten) |
| `TVShowEpisode` (Episode) | Keine (Blattknoten) |

**Implementierung:** `MediaTypeHandler` pro Typ mit `LoadCascadeChildrenAsync`-Funktion

**Auswirkung auf Parent-Referenzen:**

Cascade-Einträge erhalten eine Parent-Referenz zu ihrem Top-Level-Eintrag:

```
PlaylistEntry:
- MediaType: TVShowSeason
- MediaId: 200
- ParentMediaType: TVShow
- ParentMediaId: 100
```

Dies wird für die Fallback-Sortierung nach Hierarchie genutzt, siehe BR-13.

**Beispiel:**
```
Benutzer: Hinzufügen von Staffel 1 der Serie A (TVShowSeason, ID=200)

Abfrage:
- Lade alle Episoden für Staffel 200 aus der DB
- Beispiel: Episode 1 (ID=1001), Episode 2 (ID=1002), Episode 3 (ID=1003)

Eingefügte Einträge:
- PlaylistEntry(mediaType=TVShowSeason, mediaId=200, parentMediaType=null, parentMediaId=null)
- PlaylistEntry(mediaType=TVShowEpisode, mediaId=1001, parentMediaType=TVShowSeason, parentMediaId=200)
- PlaylistEntry(mediaType=TVShowEpisode, mediaId=1002, parentMediaType=TVShowSeason, parentMediaId=200)
- PlaylistEntry(mediatype=TVShowEpisode, mediaId=1003, parentMediaType=TVShowSeason, parentMediaId=200)
```

---

## BR-4: Berechtigungsprüfung — Nur Besitzer (schreibend), Besitzer oder öffentlich (lesend)

**Regel:** Nur der Besitzer einer Playlist darf sie ändern (umbenennen, Inhalt ändern, umsortieren, Bild, Genres,
Sortiermodus, löschen). **Lesend** darf zugreifen, wer Besitzer ist — oder jeder Anwender, solange die Playlist
öffentlich gekennzeichnet ist (siehe BR-27/BR-28, seit Entwicklungsschritt 11).

**Implementierung:**
- Schreibende Operationen: `PlaylistService.GetOwnedPlaylistAsync` prüft `playlist.UserId == currentUser.Id`
- Lesende Operationen: `GetReadablePlaylistAsync`/`EnsureReadable` prüfen `playlist.UserId == currentUser.Id || playlist.IsPublic`
- Falls nicht → `PlaylistAccessDeniedException`

**Fehlerbehandlung:** HTTP 403 Forbidden

**Scope:** Alle Operationen (Add, Remove, Get, Play, Cover, …) — die Zuordnung je Endpunkt steht in `playlists-api.md`
(„Berechtigungen je Endpunkt")

**Beispiel:**
```
Benutzer A hat Playlist mit ID=1
Benutzer B versucht: POST /api/playlists/1/entries

Server prüft: playlist.UserId (= A) != currentUser.Id (= B)
Antwort: HTTP 403 Forbidden — "Sie haben keinen Zugriff auf diese Playlist."
```

---

## BR-5: Medieninhalt muss existieren

**Regel:** Der Medieninhalt, der hinzugefügt werden soll, muss in der Datenbank existieren.

**Implementierung:**
- Vor dem Hinzufügen: Prüfe via `CheckMediaExistsAsync()` ob der Medieninhalt existiert
- Die Prüfung erfolgt via `MediaTypeHandler.LoadTitlesAsync()` — wenn kein Titel geladen werden kann, existiert der Medieninhalt nicht

**Fehlerbehandlung:** HTTP 404 Not Found — "Medieninhalt wurde nicht gefunden."

**Auswirkung auf Cascade:**
- Falls Top-Level existiert, aber Cascade-Kinder nicht: Keine Fehlermeldung; Cascade-Kinder werden einfach nicht gefunden
- Falls ein Cascade-Kind gelöscht wird, nachdem die Serie hinzugefügt wurde: Das Kind wird beim nächsten Laden der Playlist still entfernt (siehe BR-7)

---

## BR-6: MediaType-Validierung

**Regel:** Der `mediaType` muss einer der 5 unterstützten Typen sein.

**Gültige Typen:**
1. `Movie`
2. `TVShow`
3. `TVShowSeason`
4. `TVShowEpisode`
5. `MovieCollection`

**Implementierung:** `ParseMediaType()` wirft `InvalidOperationException` für ungültige Typen (case-insensitives Parsing des `MediaType`-Enums)

**Fehlerbehandlung:** HTTP 400 Bad Request

**Zusammenhang:** Diese Typen entsprechen den Datenmodellen in `VideoWebPlayer.Data` (Movie, TVShow, etc.)

**MediaType-Normalisierung:** Der geparste `mediaType` wird über `parsedMediaType.ToString()` auf seine kanonische Schreibweise normalisiert (z. B. `"movie"` oder `"MOVIE"` → `"Movie"`), bevor er gespeichert oder mit vorhandenen Einträgen verglichen wird. Dadurch werden unterschiedliche Schreibweisen desselben Medientyps zuverlässig als Duplikat erkannt (siehe BR-1). Dieselbe Normalisierung wird auch beim Entfernen eines Eintrags (`RemoveMediaFromPlaylistAsync`) angewendet, damit ein Eintrag unabhängig von der Schreibweise des übergebenen `mediaType` gefunden wird.

**Beispiel:**
```
Request: POST /api/playlists/1/entries
Body: { "mediaType": "Book", "mediaId": 100 }

Validierung schlägt fehl: "Book" ist nicht in der Liste gültiger Typen
Antwort: HTTP 400 Bad Request — "Ungültiger Medientyp."

Request: POST /api/playlists/1/entries (zweiter Aufruf mit anderer Schreibweise)
Body: { "mediaType": "movie", "mediaId": 42 }   // zuvor bereits mit "Movie" hinzugefügt

Ergebnis: HTTP 200 OK — SkippedDuplicateCount=1 (als Duplikat von "Movie" erkannt)
```

---

## BR-7: Automatische Bereinigung verwaister Einträge

**Regel:** Einträge, deren Medieninhalt gelöscht wurde, werden automatisch entfernt.

**Definition "Verwaist":** Ein `PlaylistEntry` ist verwaist, wenn der Medieninhalt (Film, Episode, Staffel, etc.) nicht mehr in der Datenbank existiert.

**Implementierung:**
- In `GetPlaylistEntriesAsync()`:
  1. Lade alle Einträge
  2. Lade alle Titel via `GetMediaTitlesAsync()` — liefert null, wenn Medieninhalt nicht existiert
  3. Identifiziere Einträge mit null-Titeln als verwaist
  4. Lösche verwaiste Einträge aus DB
  5. Gebe nur noch vorhandene Einträge an Client zurück

**Timing:** Bereinigung erfolgt beim nächsten Aufruf von `GET /api/playlists/{id}/entries`

**Benutzer-Feedback:** Keine Meldung; Einträge verschwinden still

**Performance-Überlegung:** Read-Operation kann Write-Operationen (Löschungen) auslösen, ist aber akzeptabel da Medienlöschungen selten sind

**Beispiel:**
```
Playlist:
- Episode 1 der Serie A (existiert noch)
- Episode 2 der Serie A (wurde gelöscht aus Datenbank)
- Film X (existiert noch)

Client: GET /api/playlists/1/entries

Server:
1. Lädt alle 3 Einträge
2. Lädt Titel:
   - Episode 1 → "Pilot" (gefunden)
   - Episode 2 → null (nicht gefunden, verwaist)
   - Film X → "Action Movie" (gefunden)
3. Löscht Episode 2 aus DB (stille Bereinigung)
4. Gibt nur Episode 1 und Film X an Client zurück

Benutzer sieht: Episode 2 ist verschwunden (keine Fehlermeldung)
```

---

## BR-8: MediaId muss > 0 sein

**Regel:** Die ID eines Medieninhalts muss positiv sein.

**Implementierung:** `AddMediaToPlaylistAsync()` prüft `if (mediaId <= 0)`

**Fehlerbehandlung:** HTTP 400 Bad Request

**Grund:** IDs sind Datenbankschlüssel und müssen > 0 sein

---

## BR-9: Maximale Anzahl Einträge (optional, konfigurierbar)

**Regel:** Die Anzahl der Einträge pro Playlist kann optional begrenzt werden.

**Konfiguration:**
```json
{
  "Playlists": {
    "MaxPlaylistItemCount": 1000
  }
}
```

- `null` (Standard): Keine Obergrenze
- Zahlwert > 0: Maximale Anzahl Einträge pro Playlist

**Implementierung:**
- In `AddMediaToPlaylistAsync()`: Vor dem Speichern wird die Grenze geprüft
- Berechnung: `existingKeys.Count + newEntryCount <= maxItemCount`
- `newEntryCount = 1 (Top-Level) + newCascadeEntries.Count`

**Fehlerbehandlung:** HTTP 400 Bad Request — "Die maximale Anzahl an Playlist-Einträgen wurde erreicht."

**Auswirkung auf die automatische Nachlieferung (BR-18):** Der Hintergrundprozess `PlaylistBackfillService`
prüft dieselbe Grenze, weicht aber im Verhalten bewusst von der manuellen Add-Operation ab: Statt den
gesamten Nachlieferungs-Versuch für eine Playlist abzulehnen, füllt er nur so viele Titel nach, wie noch
Platz ist (`MaxPlaylistItemCount - vorhandene Eintragsanzahl`), und liefert den Rest nicht nach - ohne
Fehler, da ein Hintergrundprozess niemandem eine Fehlermeldung anzeigen kann. Ist bereits kein Platz mehr
vorhanden, liefert der Durchlauf für diese Playlist gar nichts nach.

---

## BR-10: Entfernen ist idempotent (zur Klärung)

**Regel (aktuell implementiert):** Versuch, einen nicht vorhandenen Eintrag zu entfernen, führt zu 404 Not Found.

**Alternative (nicht implementiert):** Könnten 204 No Content zurückgeben (idempotent).

**Aktuell**: Der Service wirft `KeyNotFoundException`, die zu 404 mapped wird.

**Begründung:** Explizites Feedback ist besser; Debugging ist einfacher, wenn ein fehlgeschlagener Remove erkannt wird.

---

## BR-11: Top-Level-Eintrag wird auch bei Cascade hinzugefügt

**Regel:** Beim Hinzufügen einer Sammlung (Serie, Staffel, Sammlung) wird nicht nur die Sammlung selbst, sondern auch alle Kind-Einträge hinzugefügt.

**Beispiel:**
```
Benutzer: Hinzufügen von Serie A (TVShow, ID=100)

Resultat in Playlist:
✓ Serie A selbst (TVShow, ID=100)
✓ Staffel 1 (TVShowSeason, ID=200)
✓ Staffel 2 (TVShowSeason, ID=201)
✓ Episode 1 von Staffel 1 (TVShowEpisode, ID=1001)
✓ Episode 2 von Staffel 1 (TVShowEpisode, ID=1002)
... etc.
```

**Auswirkung:** Die Cascade-Liste ist nicht flach, sondern enthält die gesamte Hierarchie. Dies ermöglicht die Fallback-Sortierung nach Hierarchie (siehe BR-13).

---

## BR-12: Titel-Lookup beim Read

**Regel:** Beim Abrufen der Einträge werden die aktuellen Titel geladen.

**Grund:** Medieinhalte können aktualisiert werden (z. B. Titel-Änderung). Der DTO soll immer den aktuellen Titel enthalten.

**Implementierung:** `GetPlaylistEntriesAsync()` lädt Titel via `GetMediaTitlesAsync()` für jeden Medientyp

**Performance-Optimierung:** Batch-Lookup pro Medientyp, nicht per Entry

**Auswirkung auf Responsivität:** GET-Anfragen können potenziell langsam sein, wenn viele unterschiedliche Medientypen vorhanden sind (bis zu 5 separate DB-Queries)

---

## BR-13: Automatische Sortierung mit Fallback-Kette

**Regel:** Im Sortiermodus `ByReleaseDate` (Standard) werden die Einträge einer Playlist beim
Abruf über `GetPlaylistEntriesPagedAsync()` in folgender Reihenfolge sortiert, wobei jede Stufe
nur dann greift, wenn die vorherige Stufe keinen eindeutigen Wert liefert:

1. **Erscheinungsdatum** des referenzierten Medieninhalts (aufsteigend), ermittelt über
   `MediaTypeHandler.LoadReleaseDateAsync`:
   - `Movie`, `MovieCollection`: `ReleaseDate ?? PremieredAt`
   - `TVShowEpisode`: `ReleaseDate ?? PremieredAt ?? ` Erscheinungsdatum der Serie
   - `TVShowSeason`: `PremieredAt ?? ` Erscheinungsdatum der ersten Episode `?? ` Erscheinungsdatum der Serie
   - `TVShow`: `PremieredAt ?? ` Erscheinungsdatum der ersten Episode der ersten Staffel
2. **Hierarchie** (`ParentId`, dann `SequenceNumber`), ermittelt über
   `MediaTypeHandler.GetHierarchySequenceAsync`, falls kein Erscheinungsdatum vorhanden ist:
   - `TVShowEpisode`: `ParentId` = `TVShowSeasonId`, `SequenceNumber` = Episodennummer
   - `TVShowSeason`: `ParentId` = `TVShowId`, `SequenceNumber` = fortlaufende Position der Staffel
     innerhalb ihrer Serie (aufsteigend nach `Id`)
   - `Movie`, `TVShow`, `MovieCollection`: keine Hierarchie (`null`, `null`)
3. **`AddedAt`** (aufsteigend) als letzter Fallback, falls weder Erscheinungsdatum noch Hierarchie
   einen Wert liefern.

Im Sortiermodus `Manual` entfällt diese Kette; es wird ausschließlich nach `AddedAt` sortiert.

**Begründung für die Einbeziehung von `ParentId`:** Ohne `ParentId` in der Sortierung würden z. B.
gleich nummerierte Staffeln unterschiedlicher Serien (beide ohne Erscheinungsdatum) allein nach
`SequenceNumber` vermischt sortiert. Durch die Voranstellung von `ParentId` bleiben Einträge
derselben übergeordneten Serie/Staffel zusammenhängend sortiert.

**Implementierung:** `PlaylistService.SortPlaylistEntriesByReleaseDateAsync()`,
`PlaylistService.BuildPlaylistEntriesSortKey()`

**Beispiel:**
```
Playlist (SortMode = ByReleaseDate):
- Episode "Pilot" (TVShowEpisode, kein eigenes Datum, Serie "Show A" hat PremieredAt 2020-01-01)
- Film "Der Film" (Movie, ReleaseDate 2019-05-01)
- Episode "Finale" (TVShowEpisode, kein eigenes Datum, gleiche Serie, Episodennummer 10)

Ergebnis-Reihenfolge:
1. Film "Der Film" (2019-05-01)
2. Episode "Pilot" (Fallback: Serien-Datum 2020-01-01, Sequenz 1)
3. Episode "Finale" (Fallback: Serien-Datum 2020-01-01, Sequenz 10)
```

---

## BR-15: Case-insensitive, Unicode-korrekte Namenssuche in der Medienauswahl

**Regel:** Die Namenssuche bei `GET /api/items?search={term}` erfolgt unabhängig von Groß-/Kleinschreibung, einschließlich deutscher Umlaute (Ä/Ö/Ü/ß). Zeichen mit Sonderbedeutung in SQL-`LIKE`-Mustern (`%`, `_`) im Suchbegriff werden literal gesucht statt als Wildcard interpretiert.

**Implementierung:**
- Alle fünf `Get*EntriesAsync()`-Methoden nutzen die gemeinsame Hilfsmethode `ApplySearchFilter<T>()`, die den Suchbegriff kulturunabhängig kleinschreibt (`ToLowerInvariant()`), die `LIKE`-Sonderzeichen `\`, `%` und `_` escaped (Backslash zuerst) und den Vergleich über eine als SQLite-Funktion registrierte, ebenfalls kulturunabhängige Faltung des Medientitels durchführt:
  ```csharp
  var lowered = search.ToLowerInvariant();
  var escaped = lowered.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
  return query.Where(e => EF.Functions.Like(AppDbFunctions.LowerInvariant(e.Name), $"%{escaped}%", "\\"));
  ```
- `AppDbFunctions.LowerInvariant()` ist über `HasDbFunction`/`CreateFunction` als SQLite-Funktion `lower_invariant` registriert und ruft serverseitig `string.ToLowerInvariant()` auf. Das ist bewusst nicht SQLite's eingebautes `lower()`, das Ä/Ö/Ü/ß nicht faltet (ASCII-only) und `ToLower()` (kultursensitiv, z. B. `tr-TR`-Sonderfälle bei „I"/„ı"), sondern eine Unicode-korrekte, kulturunabhängige Faltung auf beiden Seiten des Vergleichs.
- Auf SQLite-Ebene wird dies zu einer case-insensitiven, Ä/Ö/Ü/ß-korrekten `LIKE`-Abfrage mit explizitem Escape-Zeichen übersetzt

**Auswirkung auf Playlist-Suche:** Benutzer können einen Suchbegriff in beliebiger Groß-/Kleinschreibung eingeben und finden weiterhin Medieninhalte mit unterschiedlicher Schreibweise, auch bei Umlauten:
- Suche nach „breaking bad" findet „Breaking Bad"
- Suche nach „BREAKING" findet „Breaking Bad"
- Suche nach „Breaking Bad" findet ebenfalls alle Treffer
- Suche nach „mörder" findet „Mörder" ebenso wie „MÖRDER"
- Suche nach einem Titel mit `%` oder `_` findet diesen literal, ohne dass die Zeichen als Wildcard wirken

**Beispiel:**
```
Datenbank enthält: "The Office", "FRIENDS", "The Crown", "breaking bad"

Suche nach "the":
- Ergebnis: "The Office", "The Crown"

Suche nach "BREAKING":
- Ergebnis: "breaking bad"

Suche nach "friends":
- Ergebnis: "FRIENDS"
```

---

## BR-16: Opt-in-Parameter für 5 Medientypen (Playlist-Suche vs. Quellen-Browsing)

**Regel:** Der Query-Parameter `includeIndividualMediaTypes` (bool, Standard: `false`) steuert, ob die Suche alle 5 oder nur 2 Medientypen durchsucht.

**Anwendungsfälle:**

| Szenario | Parameter | Medientypen | Zweck |
|----------|-----------|------------|-------|
| Playlist-Medienauswahl (neue Oberfläche) | `includeIndividualMediaTypes=true` | 5: Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection | Benutzer soll alle verfügbaren Medientypen für die Playlist auswählen können |
| Quellen-Browsing (bestehende Oberfläche) | Nicht gesetzt (Default `false`) | 2: TVShow, MovieCollection | Detailseite einer Medienquelle zeigt nur Sammlungen (nicht einzelne Filme/Episoden) → keine Redundanz |

**Implementierung:**
- `ItemsController.Get()` prüft den Wert von `includeIndividualMediaTypes`
- Nur falls `includeIndividualMediaTypes == true`: Rufe `GetMovieEntriesAsync()`, `GetSeasonEntriesAsync()`, `GetEpisodeEntriesAsync()` auf
- Falls `includeIndividualMediaTypes == false` (oder nicht gesetzt): Rufe nur `GetMovieCollectionEntriesAsync()` und `GetTVShowEntriesAsync()` auf
- Alle fünf Methoden werden für die genannten Typen implementiert; einzelne werden via Opt-in-Parameter selegktiv verwendet

**Regression-Schutz:**
- Bestehende Aufrufer von `GET /api/items` (z. B. Quellen-Browsing), die den Parameter nicht setzen, erhalten weiterhin nur 2 Medientypen
- Dies verhindert, dass die Detail-Seite einer Medienquelle plötzlich redundante Einträge enthält (z. B. einzelne Filme zusätzlich zur Sammlung)

**Beispiel:**
```
GET /api/items?search=breaking&includeIndividualMediaTypes=true
→ Ergebnisse: Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection (bis zu 5 Typen)

GET /api/items?mediaSourceId=123&includeIndividualMediaTypes=false
→ Ergebnisse: TVShow, MovieCollection (nur 2 Typen, kein Regression)

GET /api/items?mediaSourceId=123
(Parameter nicht gesetzt, Default false)
→ Ergebnisse: TVShow, MovieCollection (nur 2 Typen)
```

---

## BR-14: Validierung der Paginierungsparameter

**Regel:** `GET /api/playlists/{id}/entries/paged` validiert `pageNumber` und `pageSize`, bevor
die Playlist-Berechtigung geprüft wird.

**Implementierung (`PlaylistsController.GetPlaylistEntriesPaged`):**
- `pageNumber < 1` → HTTP 400 Bad Request ("pageNumber muss größer oder gleich 1 sein.")
- `pageSize` (nach Anwendung des Standardwerts `Playlists:DefaultPageSize`) `< 1` oder
  `> Playlists:MaxPageSize` → HTTP 400 Bad Request ("pageSize muss zwischen 1 und {MaxPageSize} liegen.")
- Wird kein `pageSize`-Query-Parameter übergeben, wird `Playlists:DefaultPageSize` (Standard: `20`) verwendet.

**Beispiel:**
```
GET /api/playlists/1/entries/paged?pageNumber=0
→ 400 Bad Request: "pageNumber muss größer oder gleich 1 sein."

GET /api/playlists/1/entries/paged?pageSize=500
→ 400 Bad Request: "pageSize muss zwischen 1 und 100 liegen."
```

---

## BR-17: Sicherheitsabfrage beim Entfernen eines Titels mit Weiterschauen-Bezug

**Regel:** `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}` prüft vor dem Entfernen, ob ein
`ContinueWatchingEntry` mit `PlaylistId` gleich dieser Playlist auf exakt dieses Video verweist. Ist das der
Fall und wurde nicht per `confirmContinueWatchingRemoval=true` bestätigt, wird der Eintrag nicht entfernt.

**Implementierung (`PlaylistService.RemoveMediaFromPlaylistAsync`):**
- Kein passender Weiterschauen-Eintrag → Entfernen wie bisher, keine Bestätigung nötig
- Passender Eintrag vorhanden, `confirmContinueWatchingRemoval` nicht `true` → `ContinueWatchingConfirmationRequiredException`
- Passender Eintrag vorhanden und bestätigt → Eintrag wird entfernt; der betroffene Weiterschauen-Eintrag wird
  durch den nächsten verfügbaren Titel der Playlist ersetzt oder entfernt (siehe
  `docs/help/weiterschauen/business-rules.md`, Abschnitt "Sicherheitsabfrage beim Entfernen eines Titels mit
  Weiterschauen-Bezug", für das vollständige Verhalten inkl. des stillen Falls und der Kollisionsauflösung)

**Fehlerbehandlung:** HTTP 409 Conflict — `DtoRemovePlaylistEntryConflictResponse.IsContinueWatchingConfirmationRequired = true`

**Begründung:** Ohne Warnung würde das Entfernen eines Titels aus einer Playlist einen zugehörigen
Weiterschauen-Fortschritt überraschend verändern (Ersetzen oder Verschwinden), ohne dass der Anwender das
beabsichtigt oder bemerkt hat.

---

## BR-18: Automatische Nachlieferung neuer Inhalte für Sammel-Einträge

**Regel:** Enthält eine Playlist einen Sammel-Eintrag (`TVShow`, `TVShowSeason` oder `MovieCollection`),
werden später hinzukommende Kind-Inhalte dieses Sammel-Eintrags (neue Staffel einer Serie, neue Episode
einer Staffel, neuer Film einer Filmsammlung) automatisch in die Playlist aufgenommen, ohne dass der
Anwender die Serie/Sammlung erneut hinzufügen muss.

**Implementierung (`PlaylistBackfillService` + `PlaylistBackfillWorker`):**
- `PlaylistBackfillWorker` (Hintergrunddienst) ruft periodisch (`Playlists:BackfillIntervalMinutes`,
  Standard 15 Minuten) `PlaylistBackfillService.RunBatchAsync()` für einen begrenzten Ausschnitt der
  Playlists auf (`Playlists:BackfillBatchSize`, Standard 25 Playlists pro Durchlauf); über mehrere
  Durchläufe hinweg werden so reihum (round-robin) alle Playlists mit einem Sammel-Eintrag abgedeckt
- Für jeden in der Playlist vorhandenen Sammel-Eintrag wird dieselbe Cascade-Abfrage genutzt wie beim
  manuellen Hinzufügen (siehe BR-3, `MediaTypeHandler.LoadCascadeChildrenAsync`), um die *aktuell*
  existierenden Kind-Inhalte zu ermitteln
- Kind-Inhalte, die bereits als `PlaylistEntry` vorhanden sind, werden übersprungen (kein Duplikat, siehe BR-1)
- Kind-Inhalte, die der Anwender zuvor bewusst einzeln aus der Playlist entfernt hat, werden ebenfalls
  übersprungen (siehe BR-19)
- Verbleibende neue Kind-Inhalte werden als `PlaylistEntry` mit `ParentMediaType`/`ParentMediaId` auf den
  jeweiligen Sammel-Eintrag hinzugefügt

**Sortierung neu nachgelieferter Titel:**
- Sortiermodus `ByReleaseDate` (Standard): `SortOrder = null`; die Einordnung nach Erscheinungsdatum
  erfolgt wie gewohnt beim Lesen (siehe BR-13)
- Sortiermodus `Manual`: Die neuen Titel werden ans Ende der bisherigen manuellen Reihenfolge angehängt
  (aufsteigende `SortOrder`-Werte ab dem bisherigen Maximum), damit die vom Anwender festgelegte
  Reihenfolge erhalten bleibt. Dieselbe Zuweisungslogik (`PlaylistEntryReorderService.AssignSortOrderForNewEntriesAsync`)
  wird auch beim manuellen Hinzufügen verwendet, damit beide Wege nicht auseinanderlaufen können.

**Betriebssicherheit:** Der Durchlauf nutzt denselben `IBackgroundProcessingGate` wie andere
Hintergrundprozesse (z. B. der Medienquellen-Scan), um sich mit einem laufenden Backup zu koordinieren,
und verarbeitet je Durchlauf nur eine begrenzte Anzahl Playlists (s. o.), damit der laufende Betrieb nicht
spürbar beeinträchtigt wird.

**Beispiel:**
```
Playlist "Meine Serien" enthält Sammel-Eintrag Serie "Show A" (Staffel 1+2 bereits vollständig enthalten)

Im Medienbestand wird Staffel 3 von "Show A" mit 8 Episoden neu erkannt (z. B. durch den Medienquellen-Scan)

Nächster Backfill-Durchlauf:
- Sammel-Eintrag "Show A" wird geprüft → Cascade liefert jetzt auch Staffel 3 + deren 8 Episoden
- Staffel 1+2 und deren Episoden: bereits vorhanden, übersprungen
- Staffel 3 + 8 Episoden: neu, werden hinzugefügt

Ergebnis: "Meine Serien" enthält jetzt auch Staffel 3 mit allen 8 Episoden, ohne dass der Anwender
etwas tun musste.
```

**Konfiguration:** siehe Abschnitt „Konfigurationsparameter" unten (`BackfillIntervalMinutes`, `BackfillBatchSize`).

---

## BR-19: Bewusst entfernte Titel werden von der automatischen Nachlieferung nicht erneut aufgenommen

**Regel:** Entfernt der Anwender einen einzelnen Titel bewusst aus einer Playlist (siehe „Entfernen" oben),
liefert die automatische Nachlieferung (BR-18) genau diesen Titel für diese Playlist nicht erneut nach -
auch wenn er weiterhin (oder erneut) ein Kind-Inhalt eines in der Playlist enthaltenen Sammel-Eintrags ist.

**Implementierung (`PlaylistEntryExclusion`):**
- `RemoveMediaFromPlaylistAsync()` legt bei jedem Entfernen zusätzlich zum Löschen des `PlaylistEntry`
  einen `PlaylistEntryExclusion`-Datensatz an (Schlüssel: `PlaylistId` + `MediaType` + `MediaId`, analog
  zum Duplikat-Schlüssel aus BR-1) - unabhängig davon, ob das Entfernen über die reguläre Löschung oder
  über die Sicherheitsabfrage bei Weiterschauen-Bezug (BR-17) erfolgte
- `PlaylistBackfillService` schließt beim Ermitteln nachzuliefernder Kind-Inhalte jeden Eintrag aus, für
  den ein passender `PlaylistEntryExclusion`-Datensatz existiert

**Aufhebung der Ausschluss-Markierung beim manuellen Wieder-Hinzufügen:** Fügt der Anwender denselben Titel
später erneut manuell hinzu — entweder direkt (`AddMediaToPlaylistAsync()` für genau diesen Titel) oder
indirekt als Kind-Inhalt eines erneut hinzugefügten Sammel-Eintrags (z. B. die ganze Serie erneut
hinzufügen, nachdem zuvor nur eine einzelne Episode entfernt wurde) — wird die zugehörige
`PlaylistEntryExclusion` gelöscht. Begründung: Ein manuelles Hinzufügen ist eine eindeutige, bewusste
Einschluss-Entscheidung des Anwenders, die die frühere Entfernung aufhebt; ohne diese Aufhebung würde ein
später aus einer Sammlung nachgelieferter Titel für den Anwender ohne erkennbaren Grund dauerhaft
unsichtbar bleiben, obwohl er ihn gerade aktiv wieder hinzugefügt hat.

**Löschung der Playlist:** Wird eine Playlist gelöscht, werden auch ihre `PlaylistEntryExclusion`-Datensätze
automatisch mitgelöscht (Fremdschlüssel mit `ON DELETE CASCADE`, analog zu `PlaylistEntry`).

**Beispiel:**
```
Playlist "Meine Serien" enthält Serie "Show A" vollständig (Staffel 1, 2 Episoden je Staffel)

Anwender entfernt Episode 2 von Staffel 1 einzeln aus der Playlist
→ PlaylistEntryExclusion(PlaylistId, MediaType=TVShowEpisode, MediaId=<Episode 2>) wird angelegt

Staffel 1 wird im Medienbestand erneut gescannt (z. B. nach einer Reorganisation der Dateien),
Episode 2 bleibt dabei unverändert vorhanden

Nächster Backfill-Durchlauf: Episode 2 wird NICHT erneut hinzugefügt (Ausschluss greift)

Anwender fügt später die ganze Serie "Show A" erneut manuell hinzu
→ Episode 2 wird jetzt wieder hinzugefügt (expliziter manueller Einschluss),
  die PlaylistEntryExclusion wird dabei gelöscht

Ab jetzt würde ein erneutes Entfernen von Episode 2 wieder eine neue Ausschluss-Markierung anlegen.
```

---

## BR-20: Automatische Genre-Ableitung aus dem Playlist-Inhalt

**Regel:** Solange `Playlist.GenresManuallyOverridden` nicht gesetzt ist, werden die Genres einer
Playlist automatisch aus den Genres der aktuell enthaltenen Titel abgeleitet und bei jeder Änderung
des Playlist-Inhalts (manuelles Hinzufügen, manuelles Entfernen, automatische Nachlieferung gemäß
BR-18, stille Bereinigung verwaister Einträge gemäß BR-7) neu berechnet.

**Quellen je Medientyp:**

| Playlist-Eintragstyp | Genre-Quelle |
|-----------------------|--------------|
| `Movie` | eigene `MovieGenre`-Zeilen |
| `TVShow` | eigene `TVShowGenre`-Zeilen |
| `TVShowSeason` | `TVShowGenre`-Zeilen der übergeordneten Serie (Staffeln haben selbst keine Genre-Zuordnung) |
| `TVShowEpisode` | `TVShowGenre`-Zeilen der übergeordneten Serie (über die Staffel) |
| `MovieCollection` | kombinierte `MovieGenre`-Zeilen der enthaltenen Filme (`MediaHierarchyRegistry`-Cascade, dieselbe Abfrage wie bei BR-3) |

**Implementierung (`PlaylistGenreService.RecomputeGenresAsync`):**
- Für jeden Playlist-Eintrag werden die oben genannten zugrunde liegenden Film-/Serien-IDs
  ermittelt und zu je einer Menge distinkter Film- bzw. Serien-IDs zusammengeführt (Mehrfachnennung
  desselben Films/derselben Serie — z. B. eine Serie plus mehrere ihrer Episoden als eigene
  Einträge — zählt dabei nur einmal)
- Je Genre wird gezählt, bei wie vielen distinkten Filmen/Serien es vorkommt (`PlaylistGenre.Count`)
  — diese Häufigkeit bestimmt die Anzeigereihenfolge (siehe unten)
- Die bisherigen `PlaylistGenre`-Zeilen der Playlist werden vollständig durch die neu berechneten
  ersetzt (kein inkrementelles Diffing) — eine bewusst einfache, für die hier relevanten
  Playlist-Größen nicht unverhältnismäßig ineffiziente Lösung
- Aufgerufen von `PlaylistService` nach `AddMediaToPlaylistAsync`, `RemoveMediaFromPlaylistAsync`
  und der stillen Bereinigung verwaister Einträge, sowie von `PlaylistBackfillService` nach jedem
  Nachlieferungs-Durchlauf, der tatsächlich Einträge hinzugefügt hat

**Anzeige-Begrenzung:** `GET /api/playlists` und `GET /api/playlists/{id}` liefern in `genres` nur
die ersten `Playlist.MaxDisplayedGenres` (5) Einträge, sortiert nach `Count` absteigend (bei
Gleichstand nach Genre-Name aufsteigend). Gespeichert und für die Filterung (siehe BR-21) nutzbar
bleiben jedoch **alle** abgeleiteten Genres, unabhängig von dieser Anzeige-Begrenzung — sie ist
eine reine Darstellungsentscheidung.

**Beispiel:**
```
Playlist "Serienabend":
- Film "Film A" (Movie, Genres: Action, Drama)
- Film "Film B" (Movie, Genres: Action, Komödie)
- Filmsammlung "Trilogie" (MovieCollection, enthaltene Filme mit Genres: Fantasy, Fantasy+Abenteuer)

Genre-Zählung: Action=2, Drama=1, Komödie=1, Fantasy=2, Abenteuer=1

Angezeigte Genres (Count absteigend, Name als Tie-Break): Action, Fantasy, Abenteuer, Drama, Komödie
(alle 5 vorhandenen Genres, da nicht mehr als 5 abgeleitet wurden)
```

---

## BR-21: Manuelles Überschreiben und Zurücksetzen der Playlist-Genres

**Regel:** Der Besitzer einer Playlist kann die automatisch abgeleiteten Genres (BR-20) jederzeit
durch eine eigene Auswahl ersetzen. Ab diesem Zeitpunkt greift BR-20 nicht mehr für diese Playlist,
bis die Auswahl explizit wieder zurückgesetzt wird.

**Implementierung (`PlaylistGenreService.SetManualGenresAsync` / `ResetGenresAsync`):**
- `PUT /api/playlists/{id}/genres` (Body: `{ "genreIds": [...] }`) ersetzt die vorhandenen
  `PlaylistGenre`-Zeilen der Playlist vollständig durch die angegebenen Genre-IDs (unbekannte IDs
  werden stillschweigend ignoriert) und setzt `Playlist.GenresManuallyOverridden = true`
- Eine manuell überschriebene Auswahl trägt keine aussagekräftige Häufigkeit; jede Zeile erhält
  `Count = 1`, sodass die Anzeige-Begrenzung (BR-20) bei mehr als 5 gewählten Genres alphabetisch
  statt nach Häufigkeit kürzt
- `POST /api/playlists/{id}/genres/reset` setzt `Playlist.GenresManuallyOverridden = false` und
  berechnet die Genres sofort aus dem aktuellen Playlist-Inhalt neu (dieselbe Logik wie BR-20)
- Beide Endpunkte prüfen dieselbe Besitzer-Berechtigung wie alle übrigen Playlist-Operationen (siehe
  BR-4)

**Fehlerbehandlung:** HTTP 403 Forbidden bei fehlender Berechtigung, HTTP 404 Not Found bei
unbekannter Playlist-ID (identisch zu den übrigen Playlist-Endpunkten).

**Beispiel:**
```
Playlist "Serienabend" hat automatisch abgeleitete Genres: Action, Fantasy, Abenteuer, Drama, Komödie

Besitzer ruft PUT /api/playlists/1/genres mit genreIds=[5] auf (Genre 5 = "Handverlesen")
→ Playlist führt jetzt nur noch "Handverlesen", GenresManuallyOverridden = true

Besitzer fügt einen weiteren Film mit Genre "Thriller" hinzu
→ Genres bleiben unverändert bei "Handverlesen" (BR-20 greift nicht mehr)

Besitzer ruft POST /api/playlists/1/genres/reset auf
→ Genres werden neu aus dem aktuellen Inhalt abgeleitet (jetzt inkl. "Thriller"),
  GenresManuallyOverridden = false
```

---

## BR-22: Upload-Validierung für Cover-Bilder

**Regel:** Ein über `POST /api/playlists/{id}/cover/upload` hochgeladenes Cover-Bild wird in
mehreren Stufen geprüft: Datei vorhanden und nicht leer, Größe innerhalb der Grenze, gemeldeter
**und** tatsächlich im Inhalt erkannter Format-Typ erlaubt, Pixelmaße innerhalb der Grenzen (am
Bildkopf, vor der Dekodierung) und Bild vollständig dekodierbar (keine abgeschnittene oder
beschädigte Datei).

**Bedingungen / Implementierung (`PlaylistsController.UploadPlaylistCover` +
`PlaylistCoverValidator.ValidateUploadAsync`):**

- Keine oder leere Datei → `"Es wurde keine Datei ausgewählt."` (HTTP 400, direkt im Controller)
- `file.Length > Playlists:MaxCoverImageSizeBytes` → `"Die Datei ist zu groß. Maximal erlaubt sind
  {N} Bytes."` (HTTP 400, direkt im Controller) — diese Prüfung erfolgt an der gemeldeten
  Dateilänge **bevor** der Inhalt in den Speicher gelesen wird, damit eine übergroße Datei nie
  gepuffert wird; bewusst wird dafür kein `[RequestSizeLimit]`-Attribut verwendet, damit die Grenze
  nicht als Kompilierzeit-Konstante von der Laufzeit-Konfiguration abweichen kann
- Im Service erneut, diesmal am tatsächlichen Inhalt:
  - `contentType` muss (case-insensitiv, getrimmt) in der kommagetrennten Liste
    `Playlists:AllowedCoverImageFormats` enthalten sein → sonst `"Format {X} wird nicht
    unterstützt. Erlaubte Formate: {Liste}."` (`{X}` ist ein Kurzname wie „BMP" bzw. bei
    unbekannten MIME-Types der Teil nach dem `/` in Großbuchstaben)
  - Dateigröße ≤ `Playlists:MaxCoverImageSizeBytes` → sonst `"Datei zu groß, max. {N} MB erlaubt."`
  - `Image.Identify` (SixLabors.ImageSharp, liest nur den Bildkopf) muss die Datei erkennen →
    sonst `"Datei ist kein gültiges Bild."`
  - Das dabei **tatsächlich erkannte** Format (`DecodedImageFormat.DefaultMimeType`, nicht der vom
    Client gemeldete Content-Type) muss ebenfalls in `Playlists:AllowedCoverImageFormats` stehen →
    sonst dieselbe `"Format {X} wird nicht unterstützt. …"`-Meldung (z. B. für ein GIF, das als
    `image/png` hochgeladen wird). Weicht der gemeldete Typ vom erkannten ab, aber beide sind
    erlaubt (z. B. PNG als `image/jpeg`), wird die Datei akzeptiert und mit dem **erkannten**
    `ContentType` gespeichert
  - Pixelgrenzen, ebenfalls nur anhand des Bildkopfs und **vor** jeder Dekodierung (damit ein
    „Decompression Bomb"-Bild nie dekodiert wird): Breite ≤ `Playlists:MaxCoverImageWidthPixels`,
    Höhe ≤ `Playlists:MaxCoverImageHeightPixels`, Breite × Höhe ≤ `Playlists:MaxCoverImageTotalPixels`
    (Standard 4096 / 4096 / 16777216; ein Wert ≤ 0 schaltet die jeweilige Prüfung ab) → sonst
    `"Bild zu groß (B x H Pixel). Erlaubt sind höchstens … Bitte verkleinern Sie das Bild."`
  - Vollständige, strikte Dekodierung des Bildes (erster Frame, ohne Metadaten) → sonst
    `"Datei ist beschädigt oder unvollständig und kann nicht als Bild gelesen werden."`. Da
    ImageSharps JPEG-Decoder abgeschnittene oder in den Bilddaten zerstörte JPEGs stillschweigend
    als teilweise graues Bild „dekodiert", prüft zusätzlich `JpegIntegrityChecker` (vor der
    Dekodierung, ohne Pixel zu rekonstruieren) die JPEG-Struktur: fehlender Abschluss-Marker
    (EOI) = abgeschnitten; bei Baseline-/Extended-Sequential-Huffman-JPEGs (praktisch alle
    Kamera- und Web-JPEGs) werden die Huffman-kodierten Bilddaten Symbol für Symbol auf gültige
    Codes, gültige Koeffizientenpositionen, korrekte Anzahl der MCUs und Restart-Marker geprüft.
    Progressive/arithmetisch kodierte JPEGs werden nur auf Vollständigkeit der Marker-Struktur
    geprüft; im Zweifel gilt die Datei als gültig, ein legales Bild wird nicht abgelehnt. Der
    PNG-Decoder schlägt bei unvollständigen/beschädigten Daten selbst fehl. Der WebP-Decoder tut
    das bei einer abgeschnittenen Datei NICHT (schon ein fehlendes Byte wird stillschweigend
    akzeptiert); deshalb wird bei WebP die tatsächliche Dateilänge gegen die im RIFF-Kopf
    angegebene Länge geprüft - ist die Datei kürzer, gilt sie als unvollständig. Zerstörte
    Bilddaten innerhalb einer vollständig langen WebP-Datei werden von dieser Prüfung nicht
    erkannt, sondern nur, soweit der Decoder selbst daran scheitert. Ein Kopf mit Breite oder Höhe
    ≤ 0 (etwa durch Überlauf) gilt als „kein gültiges Bild"
  - Die bei der Prüfung ermittelten Abmessungen werden als `Width`/`Height` der `Picture`-Zeile
    gespeichert
  - Speicherbedarf: erst die (billige) Header-/Pixelprüfung, dann die Volldekodierung mit rund
    4 Byte je Bildpunkt (bei der Standardgrenze 4096 × 4096 ≈ 64 MB kurzzeitig); jede von
    ImageSharp beim Lesen geworfene Ausnahme wird zu einer sauberen Ablehnung (HTTP 400), nie zu
    einem 500

**Fehlerbehandlung:** Validierungsverletzungen im Service werden als `InvalidOperationException`
geworfen und über dasselbe generische Mapping wie die übrigen Playlist-Endpunkte zu HTTP 400 mit
der Klartext-Meldung als Body — bewusst nicht als `DtoPlaylistCoverResult`, damit kein zweiter,
endpunktspezifischer Fehlervertrag entsteht.

**Beispiel:**
```
Upload einer 10-MB-BMP-Datei bei Standardkonfiguration (5 MB, JPEG/PNG/WebP):
→ HTTP 400 "Format BMP wird nicht unterstützt. Erlaubte Formate: JPEG, PNG, WebP."

Upload einer 10-MB-JPEG-Datei:
→ Controller-Vorabprüfung: HTTP 400 "Die Datei ist zu groß. Maximal erlaubt sind 5242880 Bytes."

Upload einer GIF-Datei mit Content-Type image/png:
→ HTTP 400 "Format GIF wird nicht unterstützt. Erlaubte Formate: JPEG, PNG, WebP."

Upload einer wenige Dutzend Byte großen PNG-Datei mit Kopfdaten 30000 x 30000 Pixel:
→ HTTP 400 "Bild zu groß (30000 x 30000 Pixel). Erlaubt sind höchstens 4096 x 4096 Pixel. …"
  (ohne dass das Bild dekodiert wird)

Upload eines nach einem Drittel abgeschnittenen JPEG:
→ HTTP 400 "Datei ist beschädigt oder unvollständig und kann nicht als Bild gelesen werden."
```

---

## BR-23: Prioritätsreihenfolge der Medientypen bei der Collage-Erzeugung

**Regel:** Die automatisch erzeugte Cover-Collage besteht aus höchstens
`PlaylistCoverImageGenerator.MaxImages` (5) Poster-Bildern der Playlist-Einträge. Die Auswahl folgt
einer festen Priorität **pro Medientyp**, nicht der Reihenfolge der Einträge in der Playlist:

1. `TVShow` und `TVShowSeason` (Stufe 0) — eine Staffel besitzt kein eigenes Poster und löst daher
   auf das `PosterPictureId` ihrer übergeordneten Serie auf
2. `TVShowEpisode` (Stufe 1)
3. `MovieCollection` (Stufe 2)
4. `Movie` (Stufe 3)

Innerhalb derselben Stufe entscheidet die Hinzufügereihenfolge (`AddedAt`, dann `Id`): Der früher
hinzugefügte Eintrag wird zuerst berücksichtigt. Ein Eintrag einer späteren Stufe kann einen
Eintrag einer früheren Stufe dagegen nie überholen — auch dann nicht, wenn er früher zur Playlist
hinzugefügt wurde. Bereits verwendete Bild-IDs werden nicht doppelt in die Collage aufgenommen
(z. B. Staffel und Serie mit demselben Poster).

**Implementierung (`PlaylistCoverImageGenerator`):**
- `CollectOrderedPictureIdsAsync()` lädt alle `PlaylistEntry` der Playlist (nach `AddedAt`
  sortiert), weist jedem über `GetMediaTypePriority()` eine Stufe zu und sortiert stabil danach
- `BuildPosterLookupAsync()` löst pro Medientyp gebündelt die `PosterPictureId` auf (für
  `TVShowSeason` über den Umweg `TVShowSeason.TVShowId` → `TVShow.PosterPictureId`)
- Die ersten `MaxImages` verfügbaren, deduplizierten Bilder werden geladen und per
  `HomeBackgroundImageGenerator.Compose()` (Überlappungsbreite 32 px) zur Collage komponiert —
  Abmessungen und JPEG-Qualität aus `Playlists:GeneratedCover*` (siehe Konfigurationsparameter)

**Beispiel:**
```
Playlist-Einträge (Hinzufügereihenfolge): Film A, Serie B, Episode C, Sammlung D, Film E, Film F
Aufgeloeste Bilder in der Collage (max. 5):
1. Serie-B-Poster      (Stufe 0)
2. Episode-C-Poster    (Stufe 1)
3. Sammlung-D-Poster   (Stufe 2)
4. Film-A-Poster       (Stufe 3, zuerst hinzugefügt)
5. Film-E-Poster       (Stufe 3)
Film F wird nicht mehr berücksichtigt (Maximum erreicht).
```

---

## BR-24: Hochgeladenes Cover hat Vorrang vor dem erzeugten

**Regel:** Ein vom Besitzer hochgeladenes Cover (`Playlist.CoverPictureIsUserUploaded = true`) wird
nie ohne dessen ausdrückliche Aktion ersetzt — insbesondere gibt es keinen automatischen Prozess,
der es durch eine Collage überschreiben könnte. Ein erneuter Upload ersetzt es bewusst; eine
explizit ausgelöste Neuerzeugung (BR-25) ersetzt es nur nach ausdrücklicher Bestätigung
(Sicherheitsabfrage, siehe unten), damit es nicht durch einen versehentlichen Klick unwiderruflich
verloren geht.

**Implementierung:**
- `PlaylistService.SetPlaylistCoverAsync()` speichert ein hochgeladenes Bild mit
  `IsGeneratedBackground = false` und setzt `CoverPictureIsUserUploaded = true`
- `PlaylistService.GeneratePlaylistCoverAsync()` speichert die Collage mit
  `IsGeneratedBackground = true` und setzt `CoverPictureIsUserUploaded = false`. Ist das aktuelle
  Cover hochgeladen und die Collage könnte erzeugt werden, wird ohne Parameter
  `confirmReplaceUploadedCover = true` eine `UploadedCoverReplacementConfirmationRequiredException`
  geworfen und nichts geändert; `PlaylistsController.RegeneratePlaylistCover` übersetzt sie in
  HTTP 409 Conflict mit `DtoRegeneratePlaylistCoverConflictResponse`, die Oberfläche
  (`PlaylistCoverRegenerateConfirmationDialog`, „Hochgeladenes Bild ersetzen") zeigt einen Dialog und
  ruft bei „Ja, ersetzen" erneut mit Bestätigung auf — dasselbe Muster wie beim Sortiermodus-Wechsel
  (`ManualSortOrderConfirmationRequiredException`) und beim Entfernen mit Weiterschauen-Bezug
  (`ContinueWatchingConfirmationRequiredException`). Ein generiertes oder fehlendes Cover wird ohne
  Rückfrage ersetzt; ist keine Collage erzeugbar (keine Quellbilder), bleibt ein hochgeladenes Cover
  ohne Rückfrage unverändert (`Success = false`)
- `ReplaceCoverPictureAsync()` (gemeinsame Hilfsmethode) fügt die neue `Picture`-Zeile hinzu, setzt
  die `CoverPicture`-Navigation der Playlist (EF Core vergibt dabei die neue Fremdschlüssel-ID
  selbst) und löscht die zuvor referenzierte `Picture`-Zeile — alles innerhalb **eines**
  `SaveChangesAsync`-Aufrufs, sodass Einfügen, Referenz-Umsetzen und Löschen atomar sind und kein
  verwaistes Bild zurückbleiben kann
- Backup-Export: Da `Picture`-Zeilen mit `IsGeneratedBackground = true` grundsätzlich nicht
  exportiert werden (siehe `VideoWebPlayerBackupData`), wird `Playlist.CoverPictureId` beim Export
  nur dann mitgeschrieben, wenn `CoverPictureIsUserUploaded = 1` — sonst `NULL`, damit beim
  Wiederherstellen keine verwaiste Fremdschlüssel-Referenz entsteht. Ein wiederhergestelltes,
  zuvor generiertes Cover zeigt daher den Platzhalter, bis es erneut manuell erzeugt wird

**Beispiel:**
```
Playlist hat generiertes Cover (CoverPictureIsUserUploaded = false)
→ Besitzer lädt eigenes Bild hoch: alte Picture-Zeile wird gelöscht,
  CoverPictureId zeigt auf das neue Bild, CoverPictureIsUserUploaded = true

Besitzer löst anschließend "Cover neu erzeugen" aus:
→ Server antwortet mit 409 Conflict, das hochgeladene Bild bleibt unverändert;
  die Oberfläche fragt nach ("Hochgeladenes Bild ersetzen")
→ Erst nach "Ja, ersetzen" (Wiederholung mit confirmReplaceUploadedCover=true):
  hochgeladenes Bild wird gelöscht, neue Collage ersetzt es,
  CoverPictureIsUserUploaded = false
```

---

## BR-25: „Cover neu erzeugen" ist ausschließlich eine manuelle Aktion

**Regel:** Die Cover-Collage wird nur dann (neu) erzeugt, wenn der Besitzer es ausdrücklich
anstößt (`POST /api/playlists/{id}/cover/regenerate`, in der Oberfläche die Schaltfläche „Cover neu
erzeugen"). Inhaltsänderungen — manuelles Hinzufügen/Entfernen, automatische Nachlieferung (BR-18),
stille Bereinigung verwaister Einträge (BR-7) — lösen keine Neuerzeugung aus; auch beim Anlegen
einer Playlist wird kein Cover automatisch erzeugt.

**Begründung / Umsetzung:**
- Anders als die Genre-Ableitung (BR-20), die bei jeder Inhaltsänderung automatisch neu berechnet
  wird, verändert `AddMediaToPlaylistAsync`/`RemoveMediaFromPlaylistAsync` das Cover nie — ein
  bewusst hochgeladenes Bild (BR-24) darf nicht bei der nächsten beliebigen Inhaltsänderung
  unbemerkt durch eine Collage ersetzt werden, und temporäre Änderungen sollen nicht jedes Mal
  eine Neuerzeugung auslösen
- Kann keine Collage erzeugt werden (keine Quellbilder vorhanden oder Erzeugung fehlgeschlagen),
  liefert `PlaylistCoverImageGenerator.GeneratePlaylistCoverAsync()` `null` zurück; der Endpunkt
  antwortet dann mit `DtoPlaylistCoverResult { Success = false, Message = "Keine Bilder
  verfügbar." }` und das bisherige Cover (oder der Platzhalter) bleibt unverändert — ein Fehler
  wird wie bei den übrigen Bild-Generatoren geloggt statt an den Anwender weitergereicht

---

## BR-26: Aufräumen von Cover-Bildern

**Regel:** Ein Cover-Bild (`Picture` mit `Type = "cover"` und `PlaylistId` auf die Playlist) bleibt
nie verwaist zurück: Beim Ersetzen (Upload oder Regenerierung) wird das bisherige Bild gelöscht
(BR-24), beim expliziten Entfernen (`DELETE /api/playlists/{id}/cover` →
`PlaylistService.DeletePlaylistCoverAsync()`) werden `CoverPictureId`/`CoverPictureIsUserUploaded`
zurückgesetzt und die `Picture`-Zeile gelöscht, und `PlaylistService.DeletePlaylistAsync()` löscht
das referenzierte Cover-Bild beim Löschen der Playlist mit.

**Implementierung:**
- `Playlist.CoverPictureId` ist ein optionaler Fremdschlüssel auf `Pictures` (ohne kaskadiertes
  Löschverhalten); das Aufräumen erfolgt daher bewusst im Service statt über einen DB-Cascade
- `Picture.PlaylistId` ist der Rückverweis auf die Playlist (analog zu `Picture.EpisodeId` bei
  generierten Episoden-Hintergrundbildern); auf die `Pictures`-Tabelle liegt ein Index auf
  `(PlaylistId, IsGeneratedBackground)`
- Fehlt beim Löschen das referenzierte Bild bereits in der Datenbank, schlägt der Vorgang nicht
  fehl — die Referenz wird trotzdem zurückgesetzt

---

## BR-27: Kennzeichnung „öffentlich" — nur Administratoren, nur eigene Playlists

**Regel:** `Playlist.IsPublic` kann ausschließlich von einem Administrator gesetzt und wieder entfernt werden, und
nur bei Playlists, die dem Administrator selbst gehören. Regulären Anwendern wird die Möglichkeit nicht angeboten
(Button nicht gerendert) und vom Server mit 403 abgelehnt; ihre Playlists bleiben stets privat.

**Auslegung („darf ein Administrator die Kennzeichnung auch bei Playlists anderer setzen?"):** Nein. Begründung:
Die Anforderung verlangt „nur der Besitzer darf sie umbenennen, ihren Inhalt ändern, … löschen" und „ihre Playlists
bleiben stets privat". Um fremde Playlists zu veröffentlichen, müsste ein Administrator sie sehen können — das
widerspricht der Privatsphäre der Anwender. Die konservative Lesart ist daher: eigene Playlists.

**Implementierung:** `PlaylistService.SetPlaylistPublicAsync(playlistId, userId, requesterIsAdmin, isPublic)` —
zuerst Besitzprüfung, dann Administratorprüfung, beides als `PlaylistAccessDeniedException` (403). Der
Controller liest den Administrator-Status aus dem Benutzerdatensatz (`CurrentUser.IsAdmin`), nicht aus dem
Token-Claim (Wirkung eines entzogenen Rechts sofort). Das Entfernen der Kennzeichnung erfordert ebenfalls
Administratorrechte („setzen und wieder entfernen"). Bekannte Randfolge: Verliert ein Besitzer seine
Administratorrechte, während seine Playlist öffentlich ist, kann er die Kennzeichnung selbst nicht mehr
entfernen (die Oberfläche zur Rechteverwaltung kann derzeit keine Rechte entziehen).

---

## BR-28: Öffentliche Playlists sind ausschließlich lesend; Sichtbarkeit für Betrachter

**Regel:** Für alle außer dem Besitzer ist eine öffentliche Playlist read-only. Serverseitig erzwungen: jede
schreibende Operation (BR-4) wirft für Nicht-Besitzer `PlaylistAccessDeniedException` (403) — auch für
Administratoren. In der Oberfläche werden Bearbeitungsmöglichkeiten für Nicht-Besitzer gar nicht gerendert
(Komponenten `PlaylistDetail`/`PlaylistEntriesList` mit `IsOwner`/`IsReadOnly`).

**Datenschutz:** Ein Betrachter sieht nur, was zum Ansehen/Abspielen nötig ist. `DtoPlaylist` enthält nie die
Benutzer-ID/E-Mail des Besitzers; für Betrachter sind `AllGenreIds`, `GenresManuallyOverridden` und
`CoverPictureIsUserUploaded` zurückgesetzt; Ausschluss-Einträge (BR-19) sind nicht Teil irgendeines DTO. In der
öffentlichen Übersicht wird kein Besitzer angezeigt. Das Cover einer *privaten* fremden Playlist wird nicht mehr
ausgeliefert (403) — weder über `GET /api/playlists/{id}/cover` noch über `GET /api/pictures/{id}`.

---

## BR-29: Freischaltung wird je Betrachter aufgelöst

**Regel:** Ob ein Eintrag zugänglich (`IsAccessible`) und abspielbar ist, wird immer für den **Anfragenden**
bestimmt (regulärer Quellenzugriff oder dessen Einzelfreischaltung), nie für den Besitzer. Nicht zugängliche Titel
sind für den Betrachter abgeblendet, nicht abspielbar (`StartPlaylistAsync` mit diesem Eintrag → 403) und werden
beim Weiterschalten übersprungen. Das gilt auch für die Ersatztitel-Suche der Weiterschauen-Auflösung (BR-31):
Ein Anwender wird nie auf einen Titel umgehängt, den nur der Besitzer freigeschaltet hat.

---

## BR-30: Lesender Zugriff eines Betrachters verändert die Playlist nicht (Waisen-Bereinigung)

**Regel:** Die stille Bereinigung verwaister Einträge (BR-7) — samt Genre-Neuberechnung und Weiterschauen-Ersetzung —
ist eine Nebenwirkung, die nur ein Zugriff des **Besitzers** auslöst (`LoadValidPlaylistEntriesAsync(playlist,
removeOrphans: isOwner)`). Greift ein anderer Anwender lesend zu, werden verwaiste Einträge lediglich nicht
angezeigt (und nicht gezählt); die Playlist des Besitzers bleibt unverändert. Begründung: „ohne die Playlist selbst
oder die Fortschritte anderer zu verändern".

---

## BR-31: Auswirkungen auf die Weiterschauen-Einträge anderer Anwender

Bei einer öffentlichen Playlist besitzen auch Betrachter Weiterschauen-Einträge mit `PlaylistId` (die Einträge
gehören dem Betrachter, `ContinueWatchingEntry.UserId` = Betrachter). Was mit ihnen passiert, wenn der Besitzer ändert:

| Ereignis | Eintrag des Besitzers | Einträge anderer Anwender |
|----------|----------------------|---------------------------|
| Titel entfernen (`RemoveMediaFromPlaylistAsync`) | Sicherheitsabfrage (409), dann ersetzen/entfernen | **keine** Abfrage; still: ersetzen durch den nächsten für **diesen** Anwender zugänglichen Titel (Position zurückgesetzt) oder entfernen; bei Kollision mit vorhandenem Eintrag für den Ersatztitel bleibt dieser und der ersetzte entfällt |
| Titel verschwindet aus der Bibliothek (Waisen-Bereinigung, Quellenlöschung) | ersetzen/entfernen ohne Abfrage | dieselbe stille Regel für jeden betroffenen Anwender |
| Playlist löschen | Bezug entfällt (`ON DELETE SET NULL`), Duplikat-Auflösung | für **alle** Anwender: Bezug entfällt; existiert ein Eintrag ohne Playlist-Bezug für dasselbe Video, wird der gebundene entfernt (kein UNIQUE-Fehler) |
| Kennzeichnung entfernen | bleibt an der Playlist gebunden | wird zu einem Eintrag ohne Playlist-Bezug (Position bleibt), bei vorhandenem Eintrag ohne Bezug entfällt der gebundene; in derselben Transaktion wie das Entfernen der Kennzeichnung |
| Benutzerkonto des Besitzers löschen | Playlists entfallen | Bezug entfällt wie beim Löschen; Duplikate (auch zwischen mehreren Playlists des Besitzers) werden vorab aufgelöst |

Die Sicherheitsabfrage (409) erscheint ausschließlich, wenn ein **eigener** Eintrag des Besitzers betroffen ist;
Einträge anderer lösen sie nicht aus und werden in der Antwort nicht erwähnt. Zusätzlich zeigt die
Weiterschauen-Liste nur dann Playlist-Name und `PlaylistEntryId` (Deep-Link `?entryId=`), wenn der Anwender die Playlist
lesen darf (Besitzer oder öffentlich) — ein veralteter Eintrag verrät den Namen einer für ihn privaten Playlist nicht.
Fortschritt melden (`ContinueWatchingService.ValidatePlaylistAccessAsync`) darf, wer die Playlist lesen darf.

---

## Zusammenfassung der Validierungsregeln

| Regel | Prüfpunkt | Fehler | HTTP-Status |
|-------|-----------|--------|-------------|
| BR-1: Keine Duplikate | Vor Insert | Übersprungen, `SkippedDuplicateCount` erhöht | Keine — 200 OK |
| BR-2: Einheitliches Duplikat-Handling (Top-Level + Cascade) | Bei Insert | Übersprungen, `Message` beschreibt Ergebnis | Keine — 200 OK |
| BR-3: Cascade-Abfrage | Konfiguriert pro Type | Keine (oder Empty-List) | Keine |
| BR-4: Berechtigung | Vor jeder Op. | `PlaylistAccessDeniedException` | 403 Forbidden |
| BR-5: Medieninhalt existiert | Vor Insert | `KeyNotFoundException` | 404 Not Found |
| BR-6: MediaType gültig und normalisiert | Bei Validierung | "Ungültiger Medientyp" | 400 Bad Request |
| BR-7: Verwaiste Bereinigung | Bei Get | Still gelöscht | Keine |
| BR-8: MediaId > 0 | Bei Validierung | "MediaId muss größer als 0 sein" | 400 Bad Request |
| BR-9: MaxItemCount | Vor Insert (optional) | "... maximale Anzahl ... erreicht" | 400 Bad Request |
| BR-10: Remove-Eintrag vorhanden | Vor Delete | `KeyNotFoundException` | 404 Not Found |
| BR-13: Sortierung mit Fallback-Kette | Bei paginiertem Get | Keine (deterministische Sortierung) | Keine |
| BR-14: Paginierungsparameter gültig | Vor Berechtigungsprüfung | "pageNumber ..." / "pageSize ..." | 400 Bad Request |
| BR-15: Case-insensitive Namenssuche | Immer aktiv in Get-Methoden | Keine | Keine — 200 OK |
| BR-16: Opt-in für 5 Medientypen | Parameter gesteuert | Regression-Schutz für Quellen-Browsing | Keine — 200 OK |
| BR-17: Sicherheitsabfrage bei Weiterschauen-Bezug | Vor Delete | `ContinueWatchingConfirmationRequiredException` | 409 Conflict |
| BR-18: Automatische Nachlieferung neuer Inhalte | Periodischer Hintergrundprozess | Keine (still nachgeliefert, MaxItemCount begrenzt) | Keine |
| BR-19: Bewusst entfernte Titel nicht erneut aufnehmen | Bei Nachlieferung | Keine (still übersprungen) | Keine |
| BR-20: Automatische Genre-Ableitung | Bei jeder Inhaltsänderung | Keine (still neu berechnet) | Keine |
| BR-21: Manuelles Genre-Überschreiben/Zurücksetzen | Bei `PUT`/`POST .../genres[/reset]` | `PlaylistAccessDeniedException` bei Fremdzugriff | 403 Forbidden / 404 Not Found |
| BR-22: Cover-Upload-Validierung | Bei `POST .../cover/upload` | `InvalidOperationException` bzw. direkte Ablehnung im Controller | 400 Bad Request |
| BR-23: Medientyp-Priorität der Collage | Bei `POST .../cover/regenerate` | Keine (deterministische Auswahl) | Keine — 200 OK (ggf. `success: false`) |
| BR-24: Vorrang hochgeladenes Cover | Bei Upload/Regenerierung | `UploadedCoverReplacementConfirmationRequiredException`, wenn ein hochgeladenes Cover ohne Bestätigung ersetzt würde | 409 Conflict (`DtoRegeneratePlaylistCoverConflictResponse`) |
| BR-25: Neuerzeugung nur manuell | Nie automatisch | Keine | Keine |
| BR-26: Cover-Bild-Aufräumung | Bei Ersetzen/Entfernen/Playlist-Löschung | Keine (tolerantes Verhalten) | Keine |

---

## Designentscheidungen

### Warum Duplikate (Top-Level und Cascade) übersprungen statt abgelehnt?

**Entscheidung:** Alle Duplikate — sowohl der angeforderte Top-Level-Eintrag als auch Cascade-Kinder — werden übersprungen ohne Fehler; die Response beschreibt das Ergebnis in `Message` und `SkippedDuplicateCount`.

**Alternativen:**
1. Abbrechen und 409 zurückgeben (würde Benutzer blockieren, uneinheitlich zum bisherigen Cascade-Verhalten)
2. Alle Einträge mit detaillierter Response zurückgeben (komplexer für Client)

**Gewählte Lösung:** Übersprungen mit Rückmeldung. Begründung:
- Einheitliches Verhalten für Top-Level und Cascade (statt bisher inkonsistent: Top-Level warf 409, Cascade wurde still übersprungen)
- Einfacher zu implementieren
- User-Experience besser: Operation erfolgt, nicht blockiert
- `AddedEntries` und `Message` geben dem Benutzer transparent Auskunft über das Ergebnis

### Warum Verwaiste Einträge beim Read bereinigen?

**Entscheidung:** Bereinigung erfolgt in `GetPlaylistEntriesAsync()`.

**Alternativen:**
1. Scheduled Job alle Stunden
2. Trigger in der Datenbank
3. Lazy-Loading beim Client

**Gewählte Lösung:** On-Read-Bereinigung. Begründung:
- Einfacher zu implementieren und zu testen
- Garantiert, dass Liste immer aktuell ist
- Performance: Medienlöschungen sind selten, daher selten Write bei Read

### Warum Top-Level-Eintrag immer hinzufügen?

**Entscheidung:** Auch bei Cascade wird der Top-Level-Eintrag selbst hinzugefügt.

**Beispiel:**
```
Benutzer: Hinzufügen Serie A
Resultat: Serie A PLUS alle Staffeln und Episoden
```

**Grund:**
- Konsistenz: Alles, was hinzugefügt wird, kann auch einzeln entfernt werden
- UI einfacher: Keine speziellen Regeln für "Sammlung ohne Eintrag"
- Ermöglicht die Fallback-Sortierung nach Hierarchie über die Parent-Referenz (siehe BR-13)

---

## Konfigurationsparameter

| Parameter | Typ | Standard | Beschreibung |
|-----------|-----|---------|--------------|
| `Playlists:MaxPlaylistItemCount` | `int?` | `null` (unbegrenzt) | Maximale Einträge pro Playlist; wird sowohl beim manuellen Hinzufügen (BR-9) als auch bei der automatischen Nachlieferung (BR-18) durchgesetzt |
| `Playlists:DefaultPageSize` | `int` | `20` | Seitengröße für `GET /api/playlists/{id}/entries/paged`, wenn kein `pageSize`-Parameter übergeben wird |
| `Playlists:MaxPageSize` | `int` | `100` | Obere Grenze für den `pageSize`-Parameter von `GET /api/playlists/{id}/entries/paged` |
| `Playlists:BackfillIntervalMinutes` | `int` | `15` | Zeitabstand zwischen zwei Nachlieferungs-Durchläufen (BR-18); Werte < 1 werden wie 1 behandelt |
| `Playlists:BackfillBatchSize` | `int` | `25` | Anzahl Playlists, die pro Nachlieferungs-Durchlauf höchstens geprüft werden (BR-18); Werte < 1 werden wie 1 behandelt |
| `Playlists:AllowedCoverImageFormats` | `string` | `"image/jpeg,image/png,image/webp"` | Kommagetrennte Liste erlaubter MIME-Types für den Cover-Upload (BR-22); Vergleich case-insensitiv |
| `Playlists:MaxCoverImageSizeBytes` | `long` | `5242880` (5 MB) | Maximale Dateigröße eines Cover-Uploads (BR-22); geprüft im Controller an der gemeldeten Länge und erneut im Validator |
| `Playlists:MaxCoverImageWidthPixels` | `int` | `4096` | Maximale Breite eines hochgeladenen Cover-Bildes in Pixeln (BR-22); geprüft am Bildkopf vor der Dekodierung; ≤ 0 schaltet die Prüfung ab |
| `Playlists:MaxCoverImageHeightPixels` | `int` | `4096` | Maximale Höhe eines hochgeladenen Cover-Bildes in Pixeln (BR-22); geprüft am Bildkopf vor der Dekodierung; ≤ 0 schaltet die Prüfung ab |
| `Playlists:MaxCoverImageTotalPixels` | `long` | `16777216` (4096 × 4096) | Maximale Gesamtpixelzahl (Breite × Höhe) eines hochgeladenen Cover-Bildes (BR-22); begrenzt den Speicherbedarf der Volldekodierung; ≤ 0 schaltet die Prüfung ab |
| `Playlists:GeneratedCoverWidthPixels` | `int` | `1600` | Zielbreite der automatisch erzeugten Cover-Collage (BR-23) |
| `Playlists:GeneratedCoverHeightPixels` | `int` | `520` | Zielhöhe der automatisch erzeugten Cover-Collage (BR-23) |
| `Playlists:GeneratedCoverJpegQuality` | `int` | `85` | JPEG-Qualität (0–100) der erzeugten Collage (BR-23) |

Diese Parameter werden in `PlaylistSettings` gelesen. Falls `MaxPlaylistItemCount` `null` ist,
gibt es keine Prüfung der maximalen Eintragsanzahl.

**Nicht konfigurierbar:** Die Anzahl der angezeigten Playlist-Genres (BR-20) ist als Konstante
`Playlist.MaxDisplayedGenres = 5` im Code fest hinterlegt, analog zu anderen „höchstens N"-Grenzen
in dieser Anwendung, statt über `PlaylistSettings` konfigurierbar zu sein — sie betrifft nur die
Darstellung, nicht die zugrunde liegende Ableitung oder Filterbarkeit (siehe BR-20).
