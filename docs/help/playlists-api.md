# Playlists – API-Dokumentation

## Übersicht

Die Playlist-API bietet Endpunkte zur Verwaltung von Benutzer-Playlists und deren Medieninhalte. Alle Operationen erfordern Authentifizierung via Bearer Token und unterliegen Berechtigungsprüfung: Nur der Besitzer einer Playlist darf diese ändern.

## Authentifizierung

Alle Endpunkte erfordern einen gültigen Bearer Token im `Authorization`-Header:

```
Authorization: Bearer {token}
```

Fehlerhafte oder fehlende Authentifizierung führt zu HTTP 401 (Unauthorized).

## Medien-Suche für die Auswahl-Oberfläche

Die Namenssuche, mit der die Playlist-Detailseite Medieninhalte zum Hinzufügen anbietet (siehe
`playlists.md`, Abschnitt „Hinzufügen"), nutzt keinen playlist-spezifischen Endpunkt, sondern den
bestehenden `GET /api/items`-Endpunkt (`search`-Parameter). Dieser Endpunkt durchsucht alle fünf
Medientypen (`Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`) nach passenden
Namen und wendet dabei dieselbe Zugriffskontrolle an wie andernorts (regulärer Mediaquellen-Zugriff
oder individuelle Freischaltung der übergeordneten Filmsammlung bzw. Serie für Filme, Staffeln und
Episoden) — nicht zugängliche Inhalte erscheinen nicht in den Suchergebnissen. Das Ergebnis ist eine
`List<MediaEntryDto>` mit `Type`, `Id`, `Title` und `PictureId` je Treffer. Der `Type`-Wert einer
Filmsammlung lautet dabei korrekt `"MovieCollection"` (zuvor fälschlich `"Movie"`).

## Endpunkte für Playlist-Einträge

### `POST /api/playlists/{id}/entries` — Medieninhalt hinzufügen

Fügt einen Medieninhalt (oder mehrere bei Cascade) zu einer Playlist hinzu.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `mediaType` | Body | string | Ja | Medientyp: `Movie`, `TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection` |
| `mediaId` | Body | long | Ja | ID des Medieninhalts (> 0) |

**Request-Body:**

```json
{
  "mediaType": "TVShow",
  "mediaId": 123
}
```

**Erfolgreiche Antwort (HTTP 200):**

```json
{
  "topLevelEntry": {
    "id": 456,
    "playlistId": 1,
    "mediaType": "TVShow",
    "mediaId": 123,
    "mediaTitle": "The Crown",
    "parentMediaType": null,
    "parentMediaId": null,
    "parentMediaTitle": null,
    "addedAt": "2026-09-05T14:30:00Z",
    "resolvedPictureId": 789,
    "isAccessible": false
  },
  "addedEntries": [
    {
      "id": 456,
      "playlistId": 1,
      "mediaType": "TVShow",
      "mediaId": 123,
      "mediaTitle": "The Crown",
      "parentMediaType": null,
      "parentMediaId": null,
      "parentMediaTitle": null,
      "addedAt": "2026-09-05T14:30:00Z",
      "resolvedPictureId": 789,
      "isAccessible": false
    }
  ],
  "skippedDuplicateCount": 0,
  "message": "3 Titel hinzugefuegt."
}
```

`resolvedPictureId` und `isAccessible` werden für `topLevelEntry` und `addedEntries` genauso
ermittelt wie für die Lese-Endpunkte weiter unten (siehe Hinweise zu `DtoPlaylistEntry` im
Abschnitt [DTO-Modelle](#dto-modelle)) — ein soeben hinzugefügter, noch nicht freigeschalteter
Titel liefert also unmittelbar `isAccessible: false`.

Die Antwort ist ein `DtoPlaylistAddResult`-Objekt (siehe [DTO-Modelle](#dto-modelle)). `topLevelEntry`
ist `null`, wenn der angeforderte Top-Level-Eintrag bereits als Duplikat übersprungen wurde.
`addedEntries` enthält alle tatsächlich neu angelegten Einträge (Top-Level plus Cascade-Kinder),
`skippedDuplicateCount` die Anzahl der dabei übersprungenen Duplikate, und `message` einen für die
UI aufbereiteten Text mit der Zusammenfassung des Ergebnisses.

**Duplikate führen nicht mehr zu einem Fehler:** Ist der angeforderte Medieninhalt (oder ein
Cascade-Kind) bereits in der Playlist vorhanden, wird der Eintrag übersprungen und in
`skippedDuplicateCount` gezählt; die Antwort bleibt weiterhin `HTTP 200 OK`. Sind ausnahmslos alle
betroffenen Einträge bereits vorhanden, ist `addedEntries` leer und `message` lautet z. B.
`"Alle 6 Titel waren bereits vorhanden."`.

**Fehlerantworten:**

| HTTP-Status | Grund | Beispiel-Body |
|-------------|-------|---------------|
| 400 Bad Request | Ungültiger `mediaType` oder `mediaId <= 0` | `"Ungültiger Medientyp."` |
| 404 Not Found | Playlist nicht gefunden oder Medieninhalt nicht vorhanden | `"Medieninhalt wurde nicht gefunden."` |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist | `"Sie haben keinen Zugriff auf diese Playlist."` |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung | `"Unauthorized"` |

**Besonderheit – Cascade-Logik:**

Wenn der `mediaType` einer Sammlung entspricht (Serie, Staffel oder Filmsammlung), werden automatisch alle zugehörigen Kind-Inhalte hinzugefügt:

- `TVShow` → alle Staffeln und Episoden
- `TVShowSeason` → alle Episoden
- `MovieCollection` → alle Filme

Die Antwort enthält alle neu hinzugefügten Einträge (Top-Level und Cascade-Kinder) in
`addedEntries`. Bereits vorhandene Cascade-Einträge werden übersprungen und in
`skippedDuplicateCount` mitgezählt.

**Besonderheit – MediaType-Normalisierung:**

Der übergebene `mediaType` wird beim Speichern auf seine kanonische Schreibweise normalisiert
(z. B. `"movie"` → `"Movie"`). Dadurch werden `"Movie"` und `"movie"` bei der Duplikatprüfung als
derselbe Medientyp erkannt.

---

### `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}` — Medieninhalt entfernen

Entfernt einen spezifischen Medieninhalt aus einer Playlist.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `mediaType` | Route | string | Ja | Medientyp des zu entfernenden Eintrags |
| `mediaId` | Route | long | Ja | ID des Medieninhalts |

**Erfolgreiche Antwort (HTTP 204):**

Keine Antwort-Body. Der Eintrag wurde entfernt.

**Fehlerantworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 404 Not Found | Eintrag nicht in dieser Playlist oder Playlist nicht gefunden |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

### `GET /api/playlists/{id}/entries` — Alle Einträge abrufen

Ruft alle Medieninhalte einer Playlist ab. Verwaiste Einträge (deren Medieninhalt gelöscht wurde) werden automatisch entfernt.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |

**Erfolgreiche Antwort (HTTP 200):**

```json
[
  {
    "id": 456,
    "playlistId": 1,
    "mediaType": "TVShow",
    "mediaId": 123,
    "mediaTitle": "The Crown",
    "parentMediaType": null,
    "parentMediaId": null,
    "parentMediaTitle": null,
    "addedAt": "2026-09-05T14:30:00Z",
    "resolvedPictureId": 789,
    "isAccessible": false
  },
  {
    "id": 457,
    "playlistId": 1,
    "mediaType": "TVShowSeason",
    "mediaId": 1234,
    "mediaTitle": "Season 1",
    "parentMediaType": "TVShow",
    "parentMediaId": 123,
    "parentMediaTitle": "The Crown",
    "addedAt": "2026-09-05T14:30:00Z",
    "resolvedPictureId": null,
    "isAccessible": false
  }
]
```

**Leere Playlist (HTTP 200):**

```json
[]
```

**Fehlerantworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 404 Not Found | Playlist nicht gefunden |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

### `GET /api/playlists/{id}/entries/paged` — Sortierte, paginierte Einträge abrufen

Ruft eine sortierte Seite der Medieninhalte einer Playlist ab. Wird von der Detailseite verwendet,
um Einträge beim Scrollen schrittweise nachzuladen (Virtual Scrolling), statt immer die komplette
Liste auf einmal zu übertragen. Verwaiste Einträge werden dabei wie beim unpaginierten Endpunkt
still bereinigt.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `pageNumber` | Query | int | Nein | 1-basierte Seitennummer (Standard: `1`); muss ≥ 1 sein |
| `pageSize` | Query | int | Nein | Anzahl Einträge pro Seite (Standard: `Playlists:DefaultPageSize`, `20`); muss zwischen 1 und `Playlists:MaxPageSize` (`100`) liegen |

**Sortierlogik:**

- Playlist-Sortiermodus `ByReleaseDate`: primär nach Erscheinungsdatum des referenzierten
  Medieninhalts (aufsteigend); fehlt dieses, Fallback auf die Hierarchie (übergeordnete
  Serie/Staffel, dann Episoden- bzw. Staffelnummer); fehlt auch das, Fallback auf `AddedAt`.
- Playlist-Sortiermodus `Manual`: nach `SortOrder` (aufsteigend, siehe unten); Einträge ohne
  `SortOrder` (z. B. aus einer Legacy-Datenlage) sortieren dabei vor allen Einträgen mit einem
  gesetzten Wert, Gleichstände (mehrere Einträge mit demselben `SortOrder`, siehe
  „Reihenfolge-Endpunkte" unten) werden zusätzlich nach `AddedAt` (aufsteigend) aufgelöst.

**Erfolgreiche Antwort (HTTP 200):**

```json
{
  "entries": [
    {
      "id": 456,
      "playlistId": 1,
      "mediaType": "TVShowEpisode",
      "mediaId": 1001,
      "mediaTitle": "Pilot",
      "parentMediaType": "TVShowSeason",
      "parentMediaId": 200,
      "parentMediaTitle": "Season 1",
      "addedAt": "2026-09-05T14:30:00Z",
      "resolvedPictureId": null,
      "isAccessible": true
    }
  ],
  "totalCount": 57,
  "hasNextPage": true,
  "pageNumber": 1,
  "pageSize": 20
}
```

Die Antwort ist ein `DtoPlaylistEntriesPagedResult`-Objekt (siehe [DTO-Modelle](#dto-modelle)).
`totalCount` ist die Gesamtzahl aller (nicht verwaisten) Einträge der Playlist, unabhängig von der
aktuellen Seite. `hasNextPage` gibt an, ob nach der aktuellen Seite noch weitere Einträge folgen.

**Fehlerantworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 400 Bad Request | `pageNumber < 1` oder `pageSize` außerhalb von `1..MaxPageSize` |
| 404 Not Found | Playlist nicht gefunden |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

## Endpunkte für den Sortiermodus und die manuelle Reihenfolge

### `PATCH /api/playlists/{id}/sort-mode` — Sortiermodus ändern

Ändert den Sortiermodus einer Playlist.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `newSortMode` | Body | string | Ja | Zielmodus: `ByReleaseDate` oder `Manual` |
| `confirmLossOfManualOrder` | Body | bool | Nein | Muss `true` sein, um von `Manual` auf `ByReleaseDate` zu wechseln (siehe unten) |

**Request-Body:**

```json
{
  "newSortMode": "Manual",
  "confirmLossOfManualOrder": false
}
```

**Verhalten je nach Wechselrichtung:**

- **`ByReleaseDate` → `Manual`**: Jedem (nicht verwaisten) Eintrag wird einmalig ein `SortOrder`
  zugewiesen, das der zuletzt gültigen `ByReleaseDate`-Anzeigereihenfolge entspricht (0-basiert,
  fortlaufend). Kein Bestätigungsparameter nötig.
- **`Manual` → `ByReleaseDate`**: Da dabei die gesamte manuelle Reihenfolge unwiederbringlich
  verloren geht (`SortOrder` wird für alle Einträge auf `null` zurückgesetzt), muss
  `confirmLossOfManualOrder: true` explizit mitgegeben werden. Fehlt dies oder ist es `false`,
  schlägt die Anfrage mit `HTTP 409 Conflict` fehl, ohne etwas zu verändern; der Client kann diesen
  Konflikt nutzen, um dem Anwender vor dem eigentlichen Aufruf eine Bestätigung einzuholen (so macht
  es die mitgelieferte Oberfläche: Sie ruft den Endpunkt zunächst ohne Bestätigung auf, zeigt bei
  409 einen Warndialog und wiederholt den Aufruf bei Bestätigung mit `confirmLossOfManualOrder: true`).
- Ist der Zielmodus bereits der aktuelle Modus, ändert sich nichts (keine erneute `SortOrder`-Zuweisung).

**Erfolgreiche Antwort (HTTP 200):** Das aktualisierte `DtoPlaylist`-Objekt.

**Fehlerantworten:**

| HTTP-Status | Grund | Response-Body |
|-------------|-------|----------------|
| 400 Bad Request | `newSortMode` ist kein gültiger Sortiermodus | Fehlertext |
| 409 Conflict | Wechsel von `Manual` zu `ByReleaseDate` ohne `confirmLossOfManualOrder: true` | `DtoChangeSortModeConflictResponse` (siehe [DTO-Modelle](#dto-modelle)), z. B. `{ "isLossOfDataConfirmationRequired": true }` |
| 404 Not Found | Playlist nicht gefunden | Fehlertext |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist | Fehlertext |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung | Fehlertext |

---

### `PUT /api/playlists/{id}/entries/{entryId}/order` — Einzelnen Eintrag umsortieren

Setzt den `SortOrder`-Wert eines einzelnen Eintrags. Nur im Sortiermodus `Manual` verwendbar. Wird
in einfachen Fällen genutzt, wenn nur ein einzelner Eintrag gesetzt werden soll; es findet keine
automatische Anpassung benachbarter Einträge statt. Die Drag-&-Drop-Oberfläche nutzt nicht diesen
Endpunkt direkt, sondern wird serverseitig durch spezialisierte Reorder-Logik verarbeitet (analog
zum Endpunkt `move-to-beginning` unten), um eine exakte Zielposition zu garantieren und Kollisionen
zu vermeiden.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `entryId` | Route | long | Ja | ID des umzusortierenden Eintrags |
| `newSortOrder` | Body | long | Ja | Neuer `SortOrder`-Wert; muss ≥ 0 sein |

**Erfolgreiche Antwort (HTTP 200):** Keine inhaltliche Antwort-Body-Auswertung nötig (Erfolg).

**Fehlerantworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 400 Bad Request | `newSortOrder < 0` |
| 404 Not Found | Playlist oder Eintrag nicht gefunden |
| 409 Conflict | Playlist ist nicht im Sortiermodus `Manual` |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

### `POST /api/playlists/{id}/entries/batch-reorder` — Mehrere Einträge atomar umsortieren

Setzt die `SortOrder`-Werte mehrerer Einträge in einer einzigen, atomaren Operation (entweder werden
alle Änderungen übernommen, oder keine). Nur im Sortiermodus `Manual` verwendbar. Von der
mitgelieferten Oberfläche derzeit nicht genutzt (sie reorganisiert einzelne Einträge nacheinander
über den Einzel-Endpunkt oben), steht aber als Teil des API-Vertrags für externe Konsumenten zur
Verfügung, die mehrere Positionsänderungen in einem Aufruf zusammenfassen möchten.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `reorderOperations` | Body | Array | Ja | Liste von `{ "entryId": long, "newSortOrder": long }` |

**Request-Body:**

```json
{
  "reorderOperations": [
    { "entryId": 456, "newSortOrder": 0 },
    { "entryId": 457, "newSortOrder": 1 }
  ]
}
```

**Erfolgreiche Antwort (HTTP 200):** Array der aktualisierten `DtoPlaylistEntry`-Objekte.

**Validierung:** Anders als beim Einzel-Endpunkt (siehe oben) müssen die `newSortOrder`-Werte
*innerhalb der Anfrage* eindeutig sein — mit einem bereits bestehenden, nicht in der Anfrage
enthaltenen Eintrag darf ein `newSortOrder`-Wert dagegen weiterhin kollidieren (siehe „Sortierlogik"
oben zum `AddedAt`-Fallback). Ebenso muss jede `entryId` innerhalb der Anfrage eindeutig sein.

**Fehlerantworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 400 Bad Request | Leere `reorderOperations`-Liste, doppelte `entryId` in der Anfrage, oder ein `newSortOrder < 0` |
| 404 Not Found | Playlist oder mindestens ein Eintrag nicht gefunden |
| 409 Conflict | Doppelter `newSortOrder`-Wert innerhalb der Anfrage, oder Playlist ist nicht im Sortiermodus `Manual` |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

### `POST /api/playlists/{id}/entries/{entryId}/move-to-beginning` — Eintrag an den Anfang verschieben

Verschiebt einen einzelnen Eintrag zuverlässig an die erste Position der manuellen Reihenfolge. Nur
im Sortiermodus `Manual` verwendbar. Verschiebt dazu serverseitig zunächst den `SortOrder`
sämtlicher anderer Einträge der Playlist um eins nach oben (atomar), bevor dem Ziel-Eintrag
`SortOrder = 0` zugewiesen wird — ein einfaches `newSortOrder: 0` über den Einzel-Endpunkt oben
würde stattdessen mit dem ohnehin an Position 0 stehenden Eintrag kollidieren und den `AddedAt`-
Gleichstand dabei zuverlässig verlieren, sodass der Eintrag nie sichtbar an erster Stelle landet.
Für die entsprechende „An Ende"-Schnellaktion existiert kein eigener Endpunkt: Dort genügt es, mit
`GET .../entries/max-sort-order` (siehe unten) die aktuell höchste `SortOrder` zu ermitteln und den
Eintrag per Einzel-Endpunkt auf `max + 1` zu setzen, da neue, größere Werte nie kollidieren können.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `entryId` | Route | long | Ja | ID des zu verschiebenden Eintrags |

**Erfolgreiche Antwort (HTTP 200):** Das aktualisierte `DtoPlaylistEntry`-Objekt (`SortOrder = 0`).

**Fehlerantworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 404 Not Found | Playlist oder Eintrag nicht gefunden |
| 409 Conflict | Playlist ist nicht im Sortiermodus `Manual` |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

### `GET /api/playlists/{id}/entries/max-sort-order` — Höchste manuelle Sortierposition abrufen

Ermittelt den aktuell höchsten `SortOrder`-Wert über die **gesamte** Playlist (nicht nur die gerade
geladene/virtualisierte Seite). Wird von der „An Ende"-Schnellaktion genutzt, damit ein Eintrag
zuverlässig ans tatsächliche Ende verschoben wird, statt nur ans Ende der aktuell im Client
geladenen Teilmenge.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |

**Erfolgreiche Antwort (HTTP 200):**

```json
{
  "maxSortOrder": 12
}
```

`maxSortOrder` ist `null`, wenn die Playlist keine Einträge mit gesetztem `SortOrder` hat (z. B.
eine leere Playlist, oder eine Playlist, die noch nie im Sortiermodus `Manual` war).

**Fehlerantworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 404 Not Found | Playlist nicht gefunden |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

## DTO-Modelle

### `DtoPlaylistEntry`

Repräsentiert einen Medieninhalt in einer Playlist.

```csharp
public class DtoPlaylistEntry
{
    public long Id { get; set; }
    public long PlaylistId { get; set; }
    public string MediaType { get; set; }          // "Movie", "TVShow", "TVShowSeason", "TVShowEpisode", "MovieCollection"
    public long MediaId { get; set; }
    public string MediaTitle { get; set; }         // Titel des Medieninhalts
    public string? ParentMediaType { get; set; }   // null für Top-Level, sonst "TVShow", "TVShowSeason" oder "MovieCollection"
    public long? ParentMediaId { get; set; }       // null für Top-Level
    public string? ParentMediaTitle { get; set; }  // null für Top-Level, sonst Titel der Sammlung
    public DateTime AddedAt { get; set; }          // UTC
    public long? ResolvedPictureId { get; set; }   // Bild-ID für die Anzeige, siehe Hinweis unten
    public bool IsAccessible { get; set; }         // echte Freischaltungsprüfung, siehe Hinweis unten
    public long? SortOrder { get; set; }           // manuelle Sortierposition, siehe Hinweis unten
}
```

**Hinweis zu `SortOrder`:** Nur im Sortiermodus `Manual` relevant. Bestimmt die Position des
Eintrags in der manuellen Reihenfolge (aufsteigend, kleinerer Wert = weiter vorne); `null` bedeutet,
dass dem Eintrag noch keine Position zugewiesen wurde (sortiert dann vor allen Einträgen mit einem
gesetzten Wert, siehe „Sortierlogik" oben). Wird beim Wechsel in den `Manual`-Modus für jeden
vorhandenen Eintrag anhand der damals aktuellen Anzeigereihenfolge einmalig gesetzt und danach über
die Reihenfolge-Endpunkte (siehe unten) verändert. Im Sortiermodus `ByReleaseDate` ist der Wert
irrelevant für die Anzeigereihenfolge, kann aber trotzdem einen zuvor gesetzten Wert enthalten (wird
beim Wechsel zurück zu `ByReleaseDate` auf `null` zurückgesetzt, siehe Abschnitt „Sortiermodus
ändern").

**Hinweis zu `ResolvedPictureId`:** Serverseitig bereits aufgelöste Bild-ID des referenzierten
Medieninhalts, die der Client direkt an `GET /api/pictures/{id}` übergeben kann. Es wird das
Poster-Bild verwendet; ist keines gesetzt, wird auf das Banner- und danach auf das Fanart-Bild
zurückgegriffen. Ist keines der drei vorhanden, ist der Wert `null` und der Client zeigt einen
Platzhalter an. Anders als `PosterPictureId` auf anderen DTOs (z. B. `DtoMovie`) kann dieser Wert
also bereits eine Banner- oder Fanart-ID sein, da der Fallback serverseitig erfolgt.

**Hinweis zu `IsAccessible`:** Gibt an, ob der aktuell angemeldete Benutzer Zugriff auf den
referenzierten Medieninhalt hat. Zugriff wird gewährt, wenn der Benutzer regulären Zugriff auf die
Mediaquelle hat ODER wenn der Eintrag für ihn individuell freigeschaltet wurde
(`hasSourceAccess OR isUnlocked`). Die individuelle Freischaltung erfolgt über denselben
Freischaltungsdienst wie bei Einzelfreischaltungen (siehe `einzelfreischaltungen.md`) und
berücksichtigt direkt nur die Medientypen `TVShow` und `MovieCollection` — für Filme wird die
Freischaltung über die übergeordnete Filmsammlung geprüft, für Episoden und Staffeln über die
übergeordnete Serie (Film → Filmsammlung, Episode → Staffel → Serie, Staffel → Serie). Dies gilt
unabhängig vom regulären Quellenzugriff, der für alle fünf Medientypen direkt anhand der jeweils
zugrunde liegenden Mediaquelle geprüft wird. Der Wert dient ausschließlich der Anzeige (siehe
`playlists.md`, Abschnitt „Zugriffsstatus in der Liste"); er verhindert nicht das Entfernen des
Eintrags aus der Playlist.

### `DtoAddMediaToPlaylistRequest`

Request-Format zum Hinzufügen eines Medieninhalts.

```csharp
public class DtoAddMediaToPlaylistRequest
{
    public string MediaType { get; set; }
    public long MediaId { get; set; }
}
```

### `DtoPlaylistAddResult`

Response-Format von `POST /api/playlists/{id}/entries`.

```csharp
public class DtoPlaylistAddResult
{
    public DtoPlaylistEntry? TopLevelEntry { get; set; }  // null, falls Top-Level-Eintrag ein Duplikat war
    public DtoPlaylistEntry[] AddedEntries { get; set; }  // alle tatsaechlich neu angelegten Eintraege
    public int SkippedDuplicateCount { get; set; }        // Anzahl uebersprungener Duplikate (Top-Level + Cascade)
    public string Message { get; set; }                   // Zusammenfassung fuer die Anzeige in der UI
}
```

### `DtoPlaylistEntriesPagedResult`

Response-Format von `GET /api/playlists/{id}/entries/paged`.

```csharp
public class DtoPlaylistEntriesPagedResult
{
    public DtoPlaylistEntry[] Entries { get; set; }  // Eintraege der aktuellen Seite, sortiert gemaess SortMode
    public int TotalCount { get; set; }              // Gesamtzahl aller (nicht verwaisten) Eintraege der Playlist
    public bool HasNextPage { get; set; }            // true, wenn nach dieser Seite weitere Eintraege folgen
    public int PageNumber { get; set; }              // angeforderte (1-basierte) Seitennummer
    public int PageSize { get; set; }                // tatsaechlich verwendete Seitengroesse
}
```

### `DtoChangeSortModeRequest`

Request-Format von `PATCH /api/playlists/{id}/sort-mode`.

```csharp
public class DtoChangeSortModeRequest
{
    public string NewSortMode { get; set; }
    public bool? ConfirmLossOfManualOrder { get; set; }
}
```

### `DtoChangeSortModeConflictResponse`

Response-Body bei `HTTP 409 Conflict` von `PATCH /api/playlists/{id}/sort-mode`, wenn ein Wechsel
von `Manual` zu `ByReleaseDate` ohne Bestätigung angefragt wurde.

```csharp
public class DtoChangeSortModeConflictResponse
{
    public bool IsLossOfDataConfirmationRequired { get; set; }  // stets true, wenn dieser Response-Typ zurueckgegeben wird
}
```

### `DtoReorderPlaylistEntryRequest`

Request-Format von `PUT /api/playlists/{id}/entries/{entryId}/order`.

```csharp
public class DtoReorderPlaylistEntryRequest
{
    public long NewSortOrder { get; set; }
}
```

### `DtoBatchReorderPlaylistEntriesRequest` / `DtoReorderOperation`

Request-Format von `POST /api/playlists/{id}/entries/batch-reorder`.

```csharp
public class DtoBatchReorderPlaylistEntriesRequest
{
    public List<DtoReorderOperation> ReorderOperations { get; set; }
}

public class DtoReorderOperation
{
    public long EntryId { get; set; }
    public long NewSortOrder { get; set; }
}
```

### `DtoMaxSortOrderResult`

Response-Format von `GET /api/playlists/{id}/entries/max-sort-order`.

```csharp
public class DtoMaxSortOrderResult
{
    public long? MaxSortOrder { get; set; }  // null, wenn kein Eintrag der Playlist einen SortOrder hat
}
```

---

## Medientypen

Die folgenden Medientypen werden unterstützt:

| Wert | Beschreibung |
|------|--------------|
| `Movie` | Ein einzelner Film |
| `TVShow` | Eine Fernsehserie (mit Staffeln und Episoden) |
| `TVShowSeason` | Eine Staffel einer Serie |
| `TVShowEpisode` | Eine Episode einer Staffel |
| `MovieCollection` | Eine Sammlung von Filmen |

---

## Validierung

| Feld | Regel | Fehler |
|------|-------|--------|
| `mediaType` | Muss einer der 5 unterstützten Typen sein | 400 Bad Request |
| `mediaId` | Muss > 0 sein | 400 Bad Request |
| Medieninhalt | Muss in der Datenbank existieren | 404 Not Found |
| Duplikat | Eintrag `(PlaylistId, MediaType, MediaId)` darf nur einmal existieren; wird beim Hinzufügen jedoch übersprungen statt einen Fehler auszulösen (siehe unten) | Kein Fehler — `HTTP 200 OK` mit `skippedDuplicateCount` |
| Berechtigung | Anfragender Benutzer muss Besitzer der Playlist sein | 403 Forbidden |

---

## Besonderheiten

### Duplikate bei Hinzufügen

Werden Duplikate beim Hinzufügen erkannt — egal ob der Top-Level-Eintrag selbst oder ein
Cascade-Kind —, werden diese übersprungen und in `skippedDuplicateCount` gezählt. Die Anfrage
schlägt dabei nie fehl; die Antwort bleibt `HTTP 200 OK`.

**Beispiel:**
- Playlist enthält bereits Episode 5 von Season 1 (hinzugefügt einzeln)
- Benutzer fügt Season 1 hinzu
- Resultat: Season 1 und Episode 1–4 werden hinzugefügt (`addedEntries`), Episode 5 wird
  übersprungen (`skippedDuplicateCount = 1`)
- `message`: `"5 Titel hinzugefuegt, 1 bereits vorhanden und uebersprungen."`

### Verwaiste Einträge

Werden Medieninhalte aus dem Bestand gelöscht (z. B. eine Serie), bleiben `PlaylistEntry`-Reihen bestehen, bis `GET /api/playlists/{id}/entries` aufgerufen wird. Bei diesem Aufruf werden verwaiste Einträge automatisch erkannt und gelöscht.

**Keine Benachrichtigung:** Der Benutzer erhält keine Meldung über die Löschung verwaister Einträge. Sie verschwinden still beim nächsten Laden der Playlist.

### Konfiguration

Optional kann die maximale Anzahl von Einträgen pro Playlist begrenzt werden:

```json
{
  "Playlists": {
    "MaxPlaylistItemCount": 1000,
    "DefaultPageSize": 20,
    "MaxPageSize": 100
  }
}
```

- `MaxPlaylistItemCount`: `null` (Standard) bedeutet keine Beschränkung. Bei Überschreitung wird HTTP 400 zurückgegeben.
- `DefaultPageSize`: Seitengröße, die `GET /api/playlists/{id}/entries/paged` verwendet, wenn kein `pageSize`-Parameter übergeben wird (Standard: `20`).
- `MaxPageSize`: obere Grenze für den `pageSize`-Parameter von `GET /api/playlists/{id}/entries/paged` (Standard: `100`); größere Werte führen zu HTTP 400.
