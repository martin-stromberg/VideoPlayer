# Playlists – Geschäftslogik und Business Rules

## Übersicht

Dieses Dokument beschreibt die Validierungsregeln, Geschäftslogik und Entscheidungen, die beim Umgang mit Playlist-Einträgen gelten.

---

## BR-1: Duplikatsprüfung pro Playlist

**Regel:** Ein Medieninhalt darf in ein und derselben Playlist nur einmal vorkommen.

**Definition Duplikat:** Zwei `PlaylistEntry`-Reihen sind Duplikate, wenn sie die gleiche Kombination aus `(PlaylistId, MediaType, MediaId)` haben.

**Implementierung:**
- Composite Unique Constraint in der Datenbank auf Spalten `(PlaylistId, MediaType, MediaId)`
- Zusätzliche Validierung im Service vor dem Einfügen

**Fehlerbehandlung:** Versuch, einen bereits vorhandenen Eintrag hinzuzufügen → `InvalidOperationException` → HTTP 409 Conflict

**Beispiel:**
```
Playlist 1:
- Film: "The Matrix" (Movie, ID=100)

Versuch: Hinzufügen von Film "The Matrix" (Movie, ID=100) erneut
Ergebnis: Fehler 409 — "Medieninhalt bereits in dieser Playlist vorhanden."
```

---

## BR-2: Cascade-Duplikate bei Hinzufügen

**Regel:** Werden Duplikate bei Cascade-Hinzufügen erkannt, werden diese still übersprungen ohne Fehler.

**Hintergrund:** Wenn Benutzer eine Serie hinzufügen und einige Episoden bereits einzeln in der Playlist sind, soll die Operation nicht fehlschlagen, sondern nur die neuen Episoden hinzufügen.

**Implementierung:**
- Lade alle bestehenden Einträge als `HashSet<(MediaType, MediaId)>`
- Filtere Cascade-Kinder: `newCascadeEntries = cascadeEntries.Where(e => !existingKeys.Contains(e))`
- Speichere nur die neuen Einträge (Cascade-Duplikate ignorieren)

**Benutzer-Feedback:** Keine Fehlermeldung; der Top-Level-Eintrag wird zurückgegeben

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

Benutzer sieht nur: "Serie A wurde hinzugefügt" (keine Details über übersprungene Duplikate)
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

Dies ermöglicht später (in späteren Schritten) Zuordnung und Gruppierung in der UI.

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

**Implementierung:** `ValidateMediaType()` wirft `InvalidOperationException` für ungültige Typen

**Fehlerbehandlung:** HTTP 400 Bad Request

**Zusammenhang:** Diese Typen entsprechen den Datenmodellen in `VideoWebPlayer.Data` (Movie, TVShow, etc.)

**Beispiel:**
```
Request: POST /api/playlists/1/entries
Body: { "mediaType": "Book", "mediaId": 100 }

Validierung schlägt fehl: "Book" ist nicht in der Liste gültiger Typen
Antwort: HTTP 400 Bad Request — "Ungültiger Medientyp."
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

**Auswirkung:** Die Cascade-Liste ist nicht flach, sondern enthält die gesamte Hierarchie. Dies ermöglicht später (Schritt 3) Sortierung und Gruppierung.

---

## BR-12: Titel-Lookup beim Read

**Regel:** Beim Abrufen der Einträge werden die aktuellen Titel geladen.

**Grund:** Medieinhalte können aktualisiert werden (z. B. Titel-Änderung). Der DTO soll immer den aktuellen Titel enthalten.

**Implementierung:** `GetPlaylistEntriesAsync()` lädt Titel via `GetMediaTitlesAsync()` für jeden Medientyp

**Performance-Optimierung:** Batch-Lookup pro Medientyp, nicht per Entry

**Auswirkung auf Responsivität:** GET-Anfragen können potenziell langsam sein, wenn viele unterschiedliche Medientypen vorhanden sind (bis zu 5 separate DB-Queries)

---

## Zusammenfassung der Validierungsregeln

| Regel | Prüfpunkt | Fehler | HTTP-Status |
|-------|-----------|--------|-------------|
| BR-1: Keine Duplikate | Vor Insert | "... bereits in dieser Playlist vorhanden" | 409 Conflict |
| BR-2: Cascade-Duplikate OK | Bei Cascade | Still übersprungen | Keine |
| BR-3: Cascade-Abfrage | Konfiguriert pro Type | Keine (oder Empty-List) | Keine |
| BR-4: Berechtigung | Vor jeder Op. | `PlaylistAccessDeniedException` | 403 Forbidden |
| BR-5: Medieninhalt existiert | Vor Insert | `KeyNotFoundException` | 404 Not Found |
| BR-6: MediaType gültig | Bei Validierung | "Ungültiger Medientyp" | 400 Bad Request |
| BR-7: Verwaiste Bereinigung | Bei Get | Still gelöscht | Keine |
| BR-8: MediaId > 0 | Bei Validierung | "MediaId muss groesser als 0 sein" | 400 Bad Request |
| BR-9: MaxItemCount | Vor Insert (optional) | "... maximale Anzahl ... erreicht" | 400 Bad Request |
| BR-10: Remove-Eintrag vorhanden | Vor Delete | `KeyNotFoundException` | 404 Not Found |

---

## Designentscheidungen

### Warum Cascade-Duplikate still übersprungen?

**Entscheidung:** Cascade-Duplikate werden übersprungen ohne Fehler.

**Alternativen:**
1. Abbrechen und 409 zurückgeben (würde benutzer blockieren)
2. Alle Einträge mit detaillierter Response zurückgeben (komplexer für Client)

**Gewählte Lösung:** Still überspringen. Begründung:
- Einfacher zu implementieren
- User-Experience besser: Operation erfolgt, nicht blockiert
- Der Top-Level-Eintrag wird immer zurückgegeben (Erfolgs-Bestätigung)

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
- Später (Schritt 3): Einfach gruppieren nach Parent-Referenz

---

## Konfigurationsparameter

| Parameter | Typ | Standard | Beschreibung |
|-----------|-----|---------|--------------|
| `Playlists:MaxPlaylistItemCount` | `int?` | `null` (unbegrenzt) | Maximale Einträge pro Playlist (Enforcement noch nicht implementiert) |

Dieser Parameter wird in `PlaylistSettings` gelesen. Falls `null`, gibt es keine Prüfung.
