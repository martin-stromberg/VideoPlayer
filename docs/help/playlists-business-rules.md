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
- `entriesToAdd.Count > 0 && SkippedDuplicateCount > 0`: `"{N} Titel hinzugefuegt, {M} bereits vorhanden und uebersprungen."`
- `entriesToAdd.Count > 0 && SkippedDuplicateCount == 0`: `"{N} Titel hinzugefuegt."`
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

Benutzer sieht: HTTP 200 OK, Message="3 Titel hinzugefuegt, 2 bereits vorhanden und uebersprungen."
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

## BR-4: Berechtigungsprüfung — Nur Besitzer

**Regel:** Nur der Besitzer einer Playlist darf deren Einträge verwalten.

**Implementierung:**
- Vor jeder Operation: Prüfe `playlist.UserId == currentUser.Id`
- Falls nicht → `PlaylistAccessDeniedException`

**Fehlerbehandlung:** HTTP 403 Forbidden

**Scope:** Alle Operationen (Add, Remove, Get)

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
Body: { "mediaType": "movie", "mediaId": 42 }   // zuvor bereits mit "Movie" hinzugefuegt

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

**Fehlerbehandlung:** HTTP 400 Bad Request — "Die maximale Anzahl an Playlist-Eintraegen wurde erreicht."

**Timing der Implementierung:** Diese Regel ist vorbereitet, aber enforcement erfolgt erst in späteren Schritten

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
- `pageNumber < 1` → HTTP 400 Bad Request ("pageNumber muss groesser oder gleich 1 sein.")
- `pageSize` (nach Anwendung des Standardwerts `Playlists:DefaultPageSize`) `< 1` oder
  `> Playlists:MaxPageSize` → HTTP 400 Bad Request ("pageSize muss zwischen 1 und {MaxPageSize} liegen.")
- Wird kein `pageSize`-Query-Parameter übergeben, wird `Playlists:DefaultPageSize` (Standard: `20`) verwendet.

**Beispiel:**
```
GET /api/playlists/1/entries/paged?pageNumber=0
→ 400 Bad Request: "pageNumber muss groesser oder gleich 1 sein."

GET /api/playlists/1/entries/paged?pageSize=500
→ 400 Bad Request: "pageSize muss zwischen 1 und 100 liegen."
```

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
| BR-8: MediaId > 0 | Bei Validierung | "MediaId muss groesser als 0 sein" | 400 Bad Request |
| BR-9: MaxItemCount | Vor Insert (optional) | "... maximale Anzahl ... erreicht" | 400 Bad Request |
| BR-10: Remove-Eintrag vorhanden | Vor Delete | `KeyNotFoundException` | 404 Not Found |
| BR-13: Sortierung mit Fallback-Kette | Bei paginiertem Get | Keine (deterministische Sortierung) | Keine |
| BR-14: Paginierungsparameter gültig | Vor Berechtigungsprüfung | "pageNumber ..." / "pageSize ..." | 400 Bad Request |
| BR-15: Case-insensitive Namenssuche | Immer aktiv in Get-Methoden | Keine | Keine — 200 OK |
| BR-16: Opt-in für 5 Medientypen | Parameter gesteuert | Regression-Schutz für Quellen-Browsing | Keine — 200 OK |

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
| `Playlists:MaxPlaylistItemCount` | `int?` | `null` (unbegrenzt) | Maximale Einträge pro Playlist (Enforcement noch nicht implementiert) |
| `Playlists:DefaultPageSize` | `int` | `20` | Seitengröße für `GET /api/playlists/{id}/entries/paged`, wenn kein `pageSize`-Parameter übergeben wird |
| `Playlists:MaxPageSize` | `int` | `100` | Obere Grenze für den `pageSize`-Parameter von `GET /api/playlists/{id}/entries/paged` |

Diese Parameter werden in `PlaylistSettings` gelesen. Falls `MaxPlaylistItemCount` `null` ist,
gibt es keine Prüfung der maximalen Eintragsanzahl.
