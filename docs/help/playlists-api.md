# Playlists – API-Dokumentation

## Übersicht

Die Playlist-API bietet Endpunkte zur Verwaltung von Benutzer-Playlists und deren Medieninhalte. Alle Operationen erfordern Authentifizierung via Bearer Token und unterliegen Berechtigungsprüfung: Nur der Besitzer einer Playlist darf diese ändern. Ab Entwicklungsschritt 11 können Administratoren eigene Playlists als **öffentlich** kennzeichnen; eine öffentliche Playlist dürfen alle Anwender **lesen** (ansehen, abspielen), ändern darf sie weiterhin nur der Besitzer (siehe „Berechtigungen je Endpunkt" und „Endpunkte für öffentliche Playlists").

## Authentifizierung

Alle Endpunkte erfordern einen gültigen Bearer Token im `Authorization`-Header:

```
Authorization: Bearer {token}
```

Fehlerhafte oder fehlende Authentifizierung führt zu HTTP 401 (Unauthorized).

## Berechtigungen je Endpunkt

Lesend = Besitzer **oder** beliebiger Anwender, solange die Playlist öffentlich ist. Schreibend = ausschließlich
der Besitzer, unabhängig davon, ob die Playlist öffentlich ist und ob der Anfragende Administrator ist.
Verstöße antworten mit `403 Forbidden` (nicht angemeldet: `401`, unbekannte Playlist: `404`); die Prüfung
erfolgt serverseitig im `PlaylistService`, nicht (nur) in der Oberfläche.

| Endpunkt | Zuordnung | Wer darf |
|----------|-----------|----------|
| `GET /api/playlists` | lesend (eigene) | jeder Angemeldete — liefert nur die **eigenen** Playlists |
| `GET /api/playlists/public` | lesend | jeder Angemeldete — alle öffentlichen Playlists |
| `GET /api/playlists/{id}` | lesend | Besitzer; andere nur bei öffentlicher Playlist |
| `GET /api/playlists/{id}/entries`, `/entries/paged` | lesend | Besitzer; andere nur bei öffentlicher Playlist |
| `POST /api/playlists/{id}/play`, `/play/next`, `/play/previous`, `/play/advance` | lesend | Besitzer; andere nur bei öffentlicher Playlist (Freischaltung je Anfragendem) |
| `GET /api/playlists/{id}/cover` | lesend | Besitzer; andere nur bei öffentlicher Playlist |
| `POST /api/playlists` | eigene Playlist anlegen | jeder Angemeldete |
| `PUT /api/playlists/{id}` (Umbenennen) | schreibend | nur Besitzer |
| `DELETE /api/playlists/{id}` | schreibend | nur Besitzer |
| `POST /api/playlists/{id}/entries`, `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}` | schreibend | nur Besitzer |
| `PUT /api/playlists/{id}/entries/{entryId}/order`, `POST .../entries/batch-reorder`, `POST .../entries/{entryId}/move-to-beginning`, `POST .../entries/{entryId}/move-between` | schreibend | nur Besitzer |
| `GET /api/playlists/{id}/entries/max-sort-order` | Hilfsabfrage der Umsortierung, daher wie schreibend | nur Besitzer |
| `PATCH /api/playlists/{id}/sort-mode` | schreibend | nur Besitzer |
| `PUT /api/playlists/{id}/genres`, `POST /api/playlists/{id}/genres/reset` | schreibend | nur Besitzer |
| `POST /api/playlists/{id}/cover/upload`, `POST .../cover/regenerate`, `DELETE .../cover` | schreibend | nur Besitzer |
| `PUT /api/playlists/{id}/public` | schreibend (Kennzeichnung) | nur Besitzer **und** Administrator |

## Medien-Suche für die Auswahl-Oberfläche

Die Namenssuche, mit der die Playlist-Detailseite Medieninhalte zum Hinzufügen anbietet (siehe
`playlists.md`, Abschnitt „Hinzufügen"), nutzt keinen playlist-spezifischen Endpunkt, sondern den
bestehenden `GET /api/items`-Endpunkt (`search`-Parameter, zusätzlich `includeIndividualMediaTypes=true`,
siehe unten). Dieser Endpunkt durchsucht alle fünf Medientypen (`Movie`, `TVShow`, `TVShowSeason`,
`TVShowEpisode`, `MovieCollection`) nach passenden Namen und wendet dabei dieselbe Zugriffskontrolle
an wie andernorts (regulärer Mediaquellen-Zugriff oder individuelle Freischaltung der übergeordneten
Filmsammlung bzw. Serie für Filme, Staffeln und Episoden) — nicht zugängliche Inhalte erscheinen
nicht in den Suchergebnissen. Das Ergebnis ist eine `List<MediaEntryDto>` mit `Type`, `Id`, `Title`
und `PictureId` je Treffer. Der `Type`-Wert einer Filmsammlung lautet dabei korrekt
`"MovieCollection"` (zuvor fälschlich `"Movie"`).

**Case-insensitive Suche:** Der `search`-Parameter vergleicht ohne Rücksicht auf Groß-/Kleinschreibung
(z. B. findet die Suche nach `"breaking bad"` oder `"BREAKING"` einen Eintrag mit dem Namen
„Breaking Bad"). Die Faltung erfolgt Unicode-korrekt und kulturunabhängig (`ToLowerInvariant()`
über eine als SQLite-Funktion registrierte `AppDbFunctions.LowerInvariant()`), sodass auch
deutsche Umlaute und ß unabhängig von Groß-/Kleinschreibung gefunden werden (z. B. findet
`"mörder"` auch „MÖRDER"). Die `LIKE`-Sonderzeichen `%` und `_` im Suchbegriff werden escaped und
dadurch literal statt als Wildcard gesucht.

**Parameter `includeIndividualMediaTypes` (boolean, Standard: `false`):** Steuert, ob neben
`MovieCollection` und `TVShow` zusätzlich die drei einzelnen Medientypen `Movie`, `TVShowSeason`
und `TVShowEpisode` im Ergebnis enthalten sind. Die Playlist-Medienauswahl setzt diesen Parameter auf
`true`, um alle fünf Medientypen zu durchsuchen. Wird der Endpunkt dagegen zum Durchsuchen einer
einzelnen Medienquelle verwendet (`mediaSourceId` gesetzt, Detailseite einer Medienquelle), bleibt
der Parameter auf seinem Standardwert `false`, sodass weiterhin nur `MovieCollection`- und
`TVShow`-Einträge geliefert werden — das bisherige Verhalten des Quellen-Browsings bleibt dadurch
unverändert.

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

Entfernt einen spezifischen Medieninhalt aus einer Playlist. Existiert in der Weiterschauen-Liste noch ein
Eintrag mit Bezug zu genau dieser Playlist, der auf diesen Titel verweist, muss der Aufrufer das Entfernen
über `confirmContinueWatchingRemoval=true` ausdrücklich bestätigen (siehe Fehlerantworten) - andernfalls
wird nichts verändert. Nach bestätigtem Entfernen wird der betroffene Weiterschauen-Eintrag durch den
nächsten verfügbaren Titel dieser Playlist ersetzt, oder entfernt, falls keiner existiert (siehe
`docs/help/weiterschauen/business-rules.md`).

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `mediaType` | Route | string | Ja | Medientyp des zu entfernenden Eintrags |
| `mediaId` | Route | long | Ja | ID des Medieninhalts |
| `confirmContinueWatchingRemoval` | Query | bool | Nein (Standard `false`) | Bestätigt das Entfernen trotz bestehendem Weiterschauen-Bezug |

**Erfolgreiche Antwort (HTTP 204):**

Keine Antwort-Body. Der Eintrag wurde entfernt.

**Fehlerantworten:**

| HTTP-Status | Grund | Response-Body |
|-------------|-------|----------------|
| 404 Not Found | Eintrag nicht in dieser Playlist oder Playlist nicht gefunden | — |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist | — |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung | — |
| 409 Conflict | Ein Weiterschauen-Eintrag mit Bezug zu dieser Playlist referenziert diesen Titel und `confirmContinueWatchingRemoval` wurde nicht als `true` übergeben | `DtoRemovePlaylistEntryConflictResponse` (siehe [DTO-Modelle](#dto-modelle)), z. B. `{ "isContinueWatchingConfirmationRequired": true }` |

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

## Endpunkte für die Playlist-Wiedergabe

Diese Endpunkte sind zustandslos: Der Server speichert keinen Wiedergabe-Kontext zwischen
Aufrufen. Der Client (Video-Player) übergibt bei jedem Navigations-Aufruf die aktuell abgespielte
`currentEntryId` explizit und hält den Kontext (Playlist-ID, aktuelle Position, Gesamtanzahl,
Playlist-Name) selbst client-seitig, u. a. über den URL-Abfrageparameter `entryId` für
Browser-Reload-Resilienz.

Alle vier Endpunkte überspringen bei der Titelauswahl automatisch nicht-abspielbare Sammel-Einträge
(`TVShow`, `TVShowSeason`, `MovieCollection`) sowie Einträge, auf die der anfragende Benutzer
keinen Zugriff hat (dieselbe `hasSourceAccess OR isUnlocked`-Regel wie bei den Lese-Endpunkten,
siehe Hinweis zu `IsAccessible` weiter unten). Nur `Movie` und `TVShowEpisode` sind direkt
abspielbar.

### `POST /api/playlists/{id}/play` — Wiedergabe starten

Startet die Wiedergabe einer Playlist an einem bestimmten Eintrag oder, falls keiner angegeben
ist, am ersten abspielbaren und zugänglichen Eintrag in der aktuell gültigen Sortierreihenfolge.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `entryId` | Query | long | Nein | ID des Eintrags, an dem die Wiedergabe beginnen soll; ohne Angabe wird der erste abspielbare, zugängliche Eintrag verwendet |

**Erfolgreiche Antwort (HTTP 200):**

```json
{
  "playlistId": 1,
  "playlistName": "Meine Favoriten",
  "totalCount": 12,
  "currentPosition": 3,
  "currentEntryId": 458,
  "currentEntry": { "id": 458, "mediaType": "TVShowEpisode", "mediaId": 1001, "mediaTitle": "Pilot", "...": "..." },
  "streamUrl": "/api/items/tvshowepisode/1001/stream",
  "mediaType": "episode",
  "mediaId": 1001
}
```

Die Antwort ist ein `DtoPlaylistPlaybackStart`-Objekt (siehe [DTO-Modelle](#dto-modelle)).
`streamUrl` enthält bewusst keinen `access_token`-Abfrageparameter — der Client hängt seinen
eigenen, bereits bekannten Bearer-Token selbst an, bevor er die URL an den Video-Player übergibt
(analog zur bestehenden Film-/Episoden-Wiedergabe).

**Fehlerantworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 400 Bad Request | Playlist enthält keinen einzigen abspielbaren und zugänglichen Eintrag (nur wenn kein `entryId` angegeben wurde), oder der explizit angegebene `entryId` verweist auf einen nicht abspielbaren Sammel-Eintrag (`TVShow`, `TVShowSeason`, `MovieCollection`) |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist, oder der explizit angegebene `entryId` ist nicht zugänglich |
| 404 Not Found | Playlist nicht gefunden, oder der explizit angegebene `entryId` gehört nicht zu dieser Playlist |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

### `POST /api/playlists/{id}/play/next` — Zum nächsten Titel navigieren

Ermittelt den nächsten abspielbaren und zugänglichen Eintrag nach `currentEntryId` in der aktuell
gültigen Sortierreihenfolge.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `currentEntryId` | Query | long | Ja | ID des aktuell abgespielten Eintrags |

**Erfolgreiche Antwort (HTTP 200):** Ein `DtoPlaylistNavigationResult`-Objekt mit dem nächsten
Eintrag und dessen tatsächlicher Position in der aktuell gültigen Sortierreihenfolge (siehe
[DTO-Modelle](#dto-modelle)).

**Erfolgreiche Antwort (HTTP 204 No Content):** Kein weiterer abspielbarer, zugänglicher Eintrag
vorhanden (Ende der Playlist erreicht).

**Fehlerantworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 400 Bad Request | `currentEntryId` gehört nicht zu dieser Playlist |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
| 404 Not Found | Playlist nicht gefunden |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

### `POST /api/playlists/{id}/play/previous` — Zum vorherigen Titel navigieren

Analog zu `POST /api/playlists/{id}/play/next`, jedoch rückwärts: Ermittelt den vorherigen
abspielbaren und zugänglichen Eintrag vor `currentEntryId`.

**Parameter:** wie bei `.../play/next`.

**Erfolgreiche Antwort (HTTP 200):** Ein `DtoPlaylistNavigationResult`-Objekt mit dem vorherigen
Eintrag und dessen tatsächlicher Position (siehe [DTO-Modelle](#dto-modelle)).

**Erfolgreiche Antwort (HTTP 204 No Content):** Kein vorheriger abspielbarer, zugänglicher Eintrag
vorhanden (Anfang der Playlist erreicht).

**Fehlerantworten:** wie bei `.../play/next`.

---

### `POST /api/playlists/{id}/play/advance` — Automatisches Weiterschalten

Wird vom Video-Player aufgerufen, wenn ein Titel automatisch zu Ende geht. Entspricht inhaltlich
`.../play/next` (liefert intern denselben nächsten Eintrag), ist aber als eigener Endpunkt
ausgeführt, um Auto-Advance-Aufrufe von manuellen „Nächster"-Klicks im Server-Log unterscheiden zu
können.

**Parameter:** wie bei `.../play/next`.

**Erfolgreiche Antwort (HTTP 200):** Ein `DtoPlaylistNavigationResult`-Objekt mit dem nächsten
Eintrag und dessen tatsächlicher Position (siehe [DTO-Modelle](#dto-modelle)).

**Erfolgreiche Antwort (HTTP 204 No Content):** Ende der Playlist erreicht; der Client zeigt
daraufhin „Ende der Playlist erreicht." an und stoppt die Wiedergabe.

**Fehlerantworten:** wie bei `.../play/next`.

---

## Endpunkte für Playlist-Genres

Ab Entwicklungsschritt 9 führt jede Playlist Genres: standardmäßig automatisch aus den Genres der
enthaltenen Titel abgeleitet und bei jeder Änderung der Playlist-Inhalte (Hinzufügen, Entfernen,
automatische Nachlieferung) neu berechnet, bis der Besitzer sie manuell überschreibt (siehe
`playlists-business-rules.md`, BR-20 und BR-21, für das vollständige fachliche Verhalten). Es gibt
dafür keinen eigenen Lese-Endpunkt — die Genres sind Teil des `DtoPlaylist`-Objekts, das
`GET /api/playlists` und `GET /api/playlists/{id}` bereits liefern (siehe [DTO-Modelle](#dto-modelle)).

### `GET /api/playlists?genreId={genreId}` — Playlists nach Genre filtern

Der bestehende Endpunkt zum Abrufen aller Playlists des angemeldeten Benutzers akzeptiert
zusätzlich einen optionalen `genreId`-Parameter, um nach Genre zu filtern und zu suchen — genau wie
bei anderen Inhaltstypen (siehe `GET /api/items`).

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `genreId` | Query | long | Nein | Nur Playlists zurückgeben, die dieses Genre führen |

**Erfolgreiche Antwort (HTTP 200):** Array von `DtoPlaylist`-Objekten. Ohne `genreId` unverändert
alle Playlists des Benutzers. Mit `genreId` nur die Playlists, die dieses Genre in irgendeiner ihrer
`PlaylistGenre`-Zeilen führen — nicht nur die tatsächlich angezeigten (siehe Hinweis zu `genres`
unten), sodass die Filterung auch dann korrekt greift, wenn ein Genre selten ist und deshalb nicht
unter den angezeigten Top-Genres einer Playlist auftaucht.

---

### `PUT /api/playlists/{id}/genres` — Genres manuell überschreiben

Überschreibt die Genres einer Playlist mit genau den angegebenen Genre-IDs und setzt
`genresManuallyOverridden` auf `true`. Ab diesem Zeitpunkt werden die Genres dieser Playlist nicht
mehr automatisch aus ihren Inhalten neu berechnet, bis `POST /api/playlists/{id}/genres/reset`
(siehe unten) aufgerufen wird.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |
| `genreIds` | Body | Array\<long\> | Ja | Genre-IDs, die die Playlist ab jetzt führen soll (ersetzt die bisherige Auswahl vollständig; unbekannte IDs werden stillschweigend ignoriert) |

**Request-Body:**

```json
{
  "genreIds": [12, 47]
}
```

**Erfolgreiche Antwort (HTTP 200):** Das aktualisierte `DtoPlaylist`-Objekt mit
`genresManuallyOverridden: true`.

**Fehlerantworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 404 Not Found | Playlist nicht gefunden |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

### `POST /api/playlists/{id}/genres/reset` — Genres auf automatische Ableitung zurücksetzen

Hebt eine zuvor gesetzte manuelle Genre-Auswahl auf (`genresManuallyOverridden` wird `false`) und
berechnet die Genres sofort neu aus den aktuell in der Playlist enthaltenen Titeln.

**Parameter:**

| Name | Position | Typ | Erforderlich | Beschreibung |
|------|----------|-----|-------------|--------------|
| `id` | Route | long | Ja | Playlist-ID |

**Erfolgreiche Antwort (HTTP 200):** Das aktualisierte `DtoPlaylist`-Objekt mit
`genresManuallyOverridden: false` und den neu abgeleiteten Genres.

**Fehlerantworten:** wie bei `PUT /api/playlists/{id}/genres`.

---

## Endpunkte für die Playlist-Abbildung (Cover)

Ab Entwicklungsschritt 10 kann jede Playlist ein Coverbild besitzen — hochgeladen vom Besitzer oder
automatisch als Collage aus den Poster-Bildern der enthaltenen Inhalte erzeugt. Welche Bilder in
welcher Reihenfolge in die Collage einfließen und warum die Neuerzeugung ausschließlich manuell
ausgelöst wird, beschreibt `playlists-business-rules.md` (BR-23, BR-24, BR-25). Ob eine Playlist
aktuell ein Cover besitzt, ist am `coverPictureId`-Feld des `DtoPlaylist`-Objekts erkennbar (siehe
[DTO-Modelle](#dto-modelle)).

Die drei ändernden Endpunkte (`upload`, `regenerate`, `DELETE`) prüfen wie alle übrigen
Playlist-Operationen die Besitzer-Berechtigung (`Playlist.UserId == CurrentUser.Id`). Der
Lese-Endpunkt `GET /api/playlists/{id}/cover` steht dagegen — analog zu
`GET /api/pictures/{id}` — jedem angemeldeten Benutzer offen, da das Bild selbst keine
schützenswerten Daten enthält.

### `POST /api/playlists/{id}/cover/upload` — Eigenes Coverbild hochladen

Speichert eine hochgeladene Bilddatei als Cover der Playlist und ersetzt dabei ein vorhandenes
Cover (hochgeladen oder generiert); das bisherige Bild wird gelöscht.

**Parameter:**

|| Name | Position | Typ | Erforderlich | Beschreibung |
||------|----------|-----|-------------|--------------|
|| `id` | Route | long | Ja | Playlist-ID |
|| `file` | Body (`multipart/form-data`) | IFormFile | Ja | Die hochzuladende Bilddatei |

**Serverseitige Prüfungen (in dieser Reihenfolge):**

1. `file` fehlt oder ist leer → HTTP 400 `"Es wurde keine Datei ausgewaehlt."`
2. `file.Length` größer als `Playlists:MaxCoverImageSizeBytes` → HTTP 400 `"Die Datei ist zu gross.
   Maximal erlaubt sind {N} Bytes."` — diese Vorab-Prüfung erfolgt bereits an der gemeldeten Länge,
   bevor der Inhalt überhaupt in den Speicher gelesen wird
3. Validierung des Dateiinhalts über `PlaylistCoverValidator` (siehe
   `playlists-business-rules.md`, BR-22): gemeldeter MIME-Type in `Playlists:AllowedCoverImageFormats`,
   Dateigröße ≤ `Playlists:MaxCoverImageSizeBytes`, das **tatsächlich im Inhalt erkannte** Bildformat
   (nicht der vom Client gemeldete MIME-Type) ebenfalls in `Playlists:AllowedCoverImageFormats`,
   Pixelmaße gemäß `Playlists:MaxCoverImageWidthPixels`, `Playlists:MaxCoverImageHeightPixels` und
   `Playlists:MaxCoverImageTotalPixels` (geprüft am Bildkopf, **bevor** das Bild dekodiert wird), und
   das Bild muss sich vollständig dekodieren lassen (abgeschnittene oder beschädigte Dateien werden
   abgelehnt). Jeder Verstoß liefert HTTP 400 mit einer deutschen Klartext-Meldung als
   Body — bewusst **nicht** im `DtoPlaylistCoverResult`-Format, sondern über dasselbe generische
   Fehler-Mapping (`InvalidOperationException` → HTTP 400) wie die übrigen Playlist-Endpunkte.

**Erfolgreiche Antwort (HTTP 200):**

```json
{
  "success": true,
  "message": "Bild erfolgreich hochgeladen.",
  "pictureId": 123
}
```

Die Antwort ist ein `DtoPlaylistCoverResult`-Objekt (siehe [DTO-Modelle](#dto-modelle)). Das Bild
wird unverändert in seinem Originalformat gespeichert (`ContentType` = der aus dem Bildinhalt
erkannte MIME-Type — bei abweichender Client-Angabe, z. B. ein PNG mit `Content-Type: image/jpeg`,
wird also `image/png` gespeichert und später ausgeliefert; `Width`/`Height` werden aus dem Bild ermittelt; `Picture.Type = "cover"`;
`IsGeneratedBackground = false`; `Playlist.CoverPictureIsUserUploaded = true`).

**Fehlerantworten:**

|| HTTP-Status | Grund | Response-Body |
||-------------|-------|----------------|
|| 400 Bad Request | Keine Datei, zu groß (Bytes oder Pixel), Format nicht erlaubt (auch das tatsächlich erkannte), kein gültiges Bild oder beschädigte/unvollständige Datei | Klartext-Fehlermeldung |
|| 404 Not Found | Playlist nicht gefunden | Fehlertext |
|| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist | Fehlertext |
|| 401 Unauthorized | Fehlende oder ungültige Authentifizierung | Fehlertext |

Es wird bewusst **kein** `[RequestSizeLimit]`-Attribut verwendet: Die Größengrenze ist zur
Laufzeit konfigurierbar (`Playlists:MaxCoverImageSizeBytes`) und wird daher im Code geprüft, statt
als Kompilierzeit-Attributwert von der Konfiguration abzuweichen.

---

### `POST /api/playlists/{id}/cover/regenerate` — Cover automatisch neu erzeugen

Erzeugt das Cover der Playlist neu als Collage aus den Poster-Bildern ihrer aktuellen Inhalte
(die „Cover neu erzeugen"-Aktion der Oberfläche) und ersetzt dabei ein vorhandenes Cover.
Ein zuvor **hochgeladenes** Cover (`Playlist.CoverPictureIsUserUploaded = true`) hat Vorrang und wird
nur mit ausdrücklicher Bestätigung ersetzt: Ohne `confirmReplaceUploadedCover=true` antwortet der
Endpunkt mit HTTP 409 Conflict (`DtoRegeneratePlaylistCoverConflictResponse`) und ändert nichts; die
Oberfläche zeigt daraufhin einen Bestätigungsdialog und wiederholt den Aufruf mit
`confirmReplaceUploadedCover=true` (gleiches Muster wie beim Sortiermodus-Wechsel und beim Entfernen
eines Eintrags mit Weiterschauen-Bezug). Die Bestätigung wird erst verlangt, wenn tatsächlich eine
Collage erzeugt werden könnte — enthält die Playlist keine Inhalte mit Bildern, bleibt die Antwort
`success: false` (HTTP 200) ohne Rückfrage, und ein hochgeladenes Cover bleibt unverändert. Ein
erzeugtes oder fehlendes Cover wird ohne Rückfrage ersetzt.
Es findet **keine** automatische Neuerzeugung bei Inhaltsänderungen statt (siehe BR-25).

**Parameter:**

|| Name | Position | Typ | Erforderlich | Beschreibung |
||------|----------|-----|-------------|--------------|
|| `id` | Route | long | Ja | Playlist-ID |
|| `confirmReplaceUploadedCover` | Query | bool | Nein (Standard `false`) | Bestätigt das Ersetzen eines hochgeladenen Covers |

**Erfolgreiche Antwort (HTTP 200):**

```json
{
  "success": true,
  "message": "Cover neu erzeugt.",
  "pictureId": 124
}
```

Enthält die Playlist keine Inhalte mit Bildern (oder schlägt die Collage-Erzeugung fehl), bleibt
die Antwort ebenfalls HTTP 200, jedoch mit `success: false` und ohne `pictureId`:

```json
{
  "success": false,
  "message": "Keine Bilder verfuegbar.",
  "pictureId": null
}
```

Das erzeugte Bild wird als JPEG (`ContentType = "image/jpeg"`, `IsGeneratedBackground = true`,
`Picture.Type = "cover"`) in den über `Playlists:GeneratedCoverWidthPixels`,
`Playlists:GeneratedCoverHeightPixels` und `Playlists:GeneratedCoverJpegQuality` konfigurierten
Abmessungen gespeichert; `Playlist.CoverPictureIsUserUploaded` wird `false`.

**Bestätigung erforderlich (HTTP 409 Conflict):**

```json
{
  "isUploadedCoverReplacementConfirmationRequired": true
}
```

**Fehlerantworten:**

|| HTTP-Status | Grund |
||-------------|-------|
|| 409 Conflict | Das aktuelle Cover ist hochgeladen und `confirmReplaceUploadedCover` ist nicht `true` |
|| 404 Not Found | Playlist nicht gefunden |
|| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
|| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

### `GET /api/playlists/{id}/cover` — Coverbild abrufen

Liefert das aktuelle Coverbild der Playlist (hochgeladen oder generiert) als Bilddaten. **Lesender Zugriff
(ab Schritt 11):** der Besitzer, oder jeder angemeldete Benutzer, solange die Playlist öffentlich ist (die
Kacheln der öffentlichen Übersicht zeigen das Bild). Für eine private Playlist eines anderen Anwenders antwortet
der Endpunkt mit `403` — ein generiertes Cover ist eine Collage der Inhalte der Playlist; nur das Ändern des
Covers erfordert stets Besitz.

**Parameter:**

|| Name | Position | Typ | Erforderlich | Beschreibung |
||------|----------|-----|-------------|--------------|
|| `id` | Route | long | Ja | Playlist-ID |

**Erfolgreiche Antwort (HTTP 200):** Die Bilddaten mit dem gespeicherten `ContentType` (bei einem
generierten Cover `image/jpeg`, bei einem hochgeladenen das Upload-Format). Da es sich um eine
Bildauslieferung handelt, wird sie typischerweise aus einem `<img>`-Element angesprochen; die
mitgelieferte Oberfläche hängt dabei — wie bei anderen Bild-/Stream-URLs — den Bearer-Token als
`access_token`-Query-Parameter an.

**Fehlerantworten:**

|| HTTP-Status | Grund |
||-------------|-------|
|| 404 Not Found | Playlist nicht gefunden oder besitzt kein Cover (der Client zeigt dann den Platzhalter) |
|| 403 Forbidden | Playlist ist privat und gehört einem anderen Benutzer |
|| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

### `DELETE /api/playlists/{id}/cover` — Cover entfernen

Entfernt das Cover der Playlist und löscht das zugehörige Bild. Ist kein Cover gesetzt, ist der
Aufruf wirkungslos erfolgreich.

**Parameter:**

|| Name | Position | Typ | Erforderlich | Beschreibung |
||------|----------|-----|-------------|--------------|
|| `id` | Route | long | Ja | Playlist-ID |

**Erfolgreiche Antwort (HTTP 200):**

```json
{
  "success": true,
  "message": null,
  "pictureId": null
}
```

**Fehlerantworten:**

|| HTTP-Status | Grund |
||-------------|-------|
|| 404 Not Found | Playlist nicht gefunden |
|| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist |
|| 401 Unauthorized | Fehlende oder ungültige Authentifizierung |

---

## Endpunkte für öffentliche Playlists

### `GET /api/playlists/public` — Öffentliche Playlists abrufen

Liefert alle Playlists, die als öffentlich gekennzeichnet sind, als `DtoPlaylist[]` (Quelle der getrennten
Übersicht „Öffentliche Playlists"). Optional mit `?genreId={genreId}` auf ein Genre beschränkt (wie
`GET /api/playlists`). Enthält auch die eigenen öffentlichen Playlists des Anfragenden
(`isOwner = true`); private Playlists — auch eigene — erscheinen nie. Die DTOs sind auf den Anfragenden
zugeschnitten (siehe `DtoPlaylist`), enthalten keine Benutzer-ID und keine E-Mail-Adresse des Besitzers.

**Antworten:** `200 OK` (`DtoPlaylist[]`), `401 Unauthorized`.

### `PUT /api/playlists/{id}/public` — Kennzeichnung „öffentlich" setzen oder entfernen

Setzt oder entfernt die Kennzeichnung. Nur ein **Administrator, der Besitzer der Playlist ist**, darf das.
Der Administrator-Status wird aus dem Benutzerdatensatz in der Datenbank gelesen (nicht aus dem Token-Claim),
so dass ein entzogenes Administratorrecht sofort wirkt.

**Request-Body (`DtoSetPlaylistPublicRequest`):**

```json
{ "isPublic": true }
```

Das Entfernen (`false`) entzieht allen anderen Anwendern sofort den Zugriff und löst deren
Weiterschauen-Einträge mit Bezug zu dieser Playlist vom Playlist-Bezug (siehe `weiterschauen/business-rules.md`).
Das erneute Setzen des bereits vorhandenen Werts ist ein wirkungsloser Erfolg.

**Antworten:**

| HTTP-Status | Grund |
|-------------|-------|
| 200 OK | Aktualisierte Playlist (`DtoPlaylist`) |
| 400 Bad Request | Leerer Request-Body |
| 401 Unauthorized | Nicht angemeldet |
| 403 Forbidden | Anfragender ist kein Administrator, oder nicht der Besitzer (auch ein Administrator, der nicht Besitzer ist) |
| 404 Not Found | Playlist existiert nicht |

---

## DTO-Modelle

### `DtoPlaylist`

Repräsentiert eine Playlist selbst (nicht ihre Einträge). Wird von `GET /api/playlists`,
`GET /api/playlists/{id}`, `POST /api/playlists`, `PUT /api/playlists/{id}`,
`PATCH /api/playlists/{id}/sort-mode`, `PUT /api/playlists/{id}/genres` und
`POST /api/playlists/{id}/genres/reset` zurückgegeben.

```csharp
public class DtoPlaylist
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string SortMode { get; set; }                 // "ByReleaseDate" oder "Manual"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DtoGenreOption[] Genres { get; set; }          // angezeigte Genres, siehe Hinweis unten
    public long[] AllGenreIds { get; set; }               // alle zugeordneten Genre-IDs, ungekuerzt
    public bool GenresManuallyOverridden { get; set; }    // true, wenn der Besitzer die Genres manuell gesetzt hat
    public long? CoverPictureId { get; set; }             // Bild-ID des Covers, siehe Hinweis unten
    public bool CoverPictureIsUserUploaded { get; set; }  // true = hochgeladen, false = generierte Collage
    public bool IsPublic { get; set; }                    // Kennzeichnung "öffentlich" (Schritt 11)
    public bool IsOwner { get; set; }                     // gehört die Playlist dem Anfragenden? (Schritt 11)
}
```

**Hinweis zu `isPublic`/`isOwner` (Schritt 11):** `isOwner` ist relativ zum Anfragenden und bestimmt, ob die Oberfläche
Bearbeitungsmöglichkeiten anbietet (nur für den Besitzer). Es ist keine Sicherheitsgrenze — der Server lehnt jede
Änderung durch Nicht-Besitzer unabhängig davon ab. Für einen **Betrachter** (`isOwner = false`) wird das DTO auf das
Nötige zum Ansehen und Abspielen reduziert: `allGenreIds` ist leer, `genresManuallyOverridden` und
`coverPictureIsUserUploaded` sind `false`; `genres`, `name`, `description`, `coverPictureId` usw. bleiben sichtbar. Das
DTO enthält nie die Benutzer-ID oder E-Mail-Adresse des Besitzers.

**Hinweis zu `coverPictureId`/`coverPictureIsUserUploaded`:** `coverPictureId` verweist auf das
aktuelle Coverbild der Playlist (`null` = kein Cover gesetzt; die Oberfläche zeigt dann den
generischen Platzhalter). Die eigentlichen Bilddaten liefert `GET /api/playlists/{id}/cover` (siehe
Abschnitt „Endpunkte für die Playlist-Abbildung (Cover)") — nicht `GET /api/pictures/{id}`.
`coverPictureIsUserUploaded` unterscheidet, ob das Bild vom Besitzer hochgeladen (`true`) oder als
Collage erzeugt (`false`) wurde; die Oberfläche nutzt `coverPictureId` zusätzlich als
Cache-Buster (`?v={coverPictureId}`), damit der Browser nach einem Upload oder einer Neuerzeugung
das neue Bild lädt statt eine gecachte Antwort desselben Endpunkts wiederzuverwenden.

**Hinweis zu `genres`:** Nach Häufigkeit absteigend sortiert (wie viele unterschiedliche Titel der
Playlist dieses Genre tragen; bei einer manuellen Auswahl stattdessen alphabetisch, da eine
Häufigkeit hier keinen Sinn ergibt) und auf die ersten fünf Einträge begrenzt, damit die Anzeige
übersichtlich bleibt. Solange `genresManuallyOverridden` `false` ist, werden diese Genres
automatisch aus den Genres der enthaltenen Titel abgeleitet und bei jeder Änderung der
Playlist-Inhalte neu berechnet (siehe `playlists-business-rules.md`, BR-20). `Movie`- und
`TVShow`-Einträge tragen ihre eigenen Genres bei; `TVShowSeason`- und `TVShowEpisode`-Einträge die
Genres ihrer übergeordneten Serie; `MovieCollection`-Einträge die kombinierten Genres ihrer
enthaltenen Filme.

**Hinweis zu `allGenreIds`:** Enthält jedes der Playlist zugeordnete Genre, nicht nur die oben
gekürzt angezeigte Teilmenge. Wird von der Oberfläche verwendet, um den
Genre-Überschreiben-Dialog mit der tatsächlich vollständigen aktuellen Auswahl vorzubelegen;
`GET /api/playlists?genreId=…` (siehe oben) filtert ebenfalls gegen die vollständige Menge, nicht
nur gegen `genres`.

### `DtoGenreOption`

Ein einzelnes, auswählbares Genre (auch für andere Inhaltstypen verwendet, siehe `GET /api/items/genres`).

```csharp
public class DtoGenreOption
{
    public long Id { get; set; }
    public string Name { get; set; }
}
```

### `DtoSetPlaylistGenresRequest`

Request-Format von `PUT /api/playlists/{id}/genres`.

```csharp
public class DtoSetPlaylistGenresRequest
{
    public long[] GenreIds { get; set; }
}
```

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

### `DtoRemovePlaylistEntryConflictResponse`

Response-Body bei `HTTP 409 Conflict` von `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}`, wenn
das Entfernen einen Weiterschauen-Eintrag mit Bezug zu dieser Playlist betreffen würde und nicht per
`confirmContinueWatchingRemoval=true` bestätigt wurde.

```csharp
public class DtoRemovePlaylistEntryConflictResponse
{
    public bool IsContinueWatchingConfirmationRequired { get; set; }  // stets true, wenn dieser Response-Typ zurueckgegeben wird
}
```

### `DtoRegeneratePlaylistCoverConflictResponse`

Response-Body bei `HTTP 409 Conflict` von `POST /api/playlists/{id}/cover/regenerate`, wenn das
Neuerzeugen ein hochgeladenes Cover ersetzen würde und nicht per `confirmReplaceUploadedCover=true`
bestätigt wurde.

```csharp
public class DtoRegeneratePlaylistCoverConflictResponse
{
    public bool IsUploadedCoverReplacementConfirmationRequired { get; set; }  // stets true, wenn dieser Response-Typ zurueckgegeben wird
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

### `DtoPlaylistPlaybackStart`

Response-Format von `POST /api/playlists/{id}/play`.

```csharp
public class DtoPlaylistPlaybackStart
{
    public long PlaylistId { get; set; }
    public string PlaylistName { get; set; }
    public int TotalCount { get; set; }            // Gesamtzahl aller (nicht verwaisten) Eintraege der Playlist
    public int CurrentPosition { get; set; }        // 1-basierte Position des Starttitels in der Sortierreihenfolge
    public long CurrentEntryId { get; set; }
    public DtoPlaylistEntry CurrentEntry { get; set; }
    public string StreamUrl { get; set; }           // ohne access_token-Parameter, siehe Hinweis oben
    public string MediaType { get; set; }           // "movie" oder "episode" (Video-Player-Konvention)
    public long MediaId { get; set; }
}
```

### `DtoPlaylistNavigationResult`

Response-Format von `POST /api/playlists/{id}/play/next`, `.../play/previous` und `.../play/advance`
bei HTTP 200.

```csharp
public class DtoPlaylistNavigationResult
{
    public DtoPlaylistEntry Entry { get; set; }
    public int Position { get; set; }  // 1-basierte Position von Entry in der aktuell gueltigen Sortierreihenfolge
}
```

**Hinweis zu `Position`:** Wird serverseitig ermittelt und entspricht der tatsächlichen Position
von `Entry` in der vollständigen (auch nicht-abspielbare und gesperrte Einträge zählenden)
Sortierreihenfolge — nicht der Position des vorherigen Eintrags plus/minus eins. Das ist relevant,
weil die Navigation dabei einen oder mehrere nicht-abspielbare Sammel-Einträge (`TVShow`,
`TVShowSeason`, `MovieCollection`) oder für den Benutzer gesperrte Einträge überspringen kann.

### `DtoPlaylistCoverResult`

Response-Format der drei ändernden Cover-Endpunkte `POST /api/playlists/{id}/cover/upload`,
`POST /api/playlists/{id}/cover/regenerate` und `DELETE /api/playlists/{id}/cover`.

```csharp
public class DtoPlaylistCoverResult
{
    public bool Success { get; set; }       // false nur bei Regenerierung ohne verfuegbare Quellbilder
    public string? Message { get; set; }    // fuer die Anzeige aufbereiteter Ergebnistext
    public long? PictureId { get; set; }    // ID des neuen Cover-Bildes; null bei Misserfolg oder nach Delete
}
```

**Hinweis:** Ein `success: false` bei der Regenerierung ist **kein** HTTP-Fehler — die Antwort
bleibt `200 OK`. Echte Fehler (Validierung, Berechtigung, nicht gefunden) werden wie bei den
übrigen Endpunkten über HTTP-Statuscodes mit Klartext-/Fehler-Body gemeldet, nicht über dieses
DTO.

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
| Cover-Upload `file` | Datei vorhanden und nicht leer; MIME-Type in `Playlists:AllowedCoverImageFormats`; Größe ≤ `Playlists:MaxCoverImageSizeBytes`; tatsächlich erkanntes Bildformat in `Playlists:AllowedCoverImageFormats`; Breite/Höhe/Gesamtpixel innerhalb `Playlists:MaxCoverImageWidthPixels`/`…HeightPixels`/`…TotalPixels`; Inhalt dekodiert vollständig als echtes Bild | 400 Bad Request (Klartext-Meldung) |

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
    "MaxPageSize": 100,
    "BackfillIntervalMinutes": 15,
    "BackfillBatchSize": 25,
    "AllowedCoverImageFormats": "image/jpeg,image/png,image/webp",
    "MaxCoverImageSizeBytes": 5242880,
    "MaxCoverImageWidthPixels": 4096,
    "MaxCoverImageHeightPixels": 4096,
    "MaxCoverImageTotalPixels": 16777216,
    "GeneratedCoverWidthPixels": 1600,
    "GeneratedCoverHeightPixels": 520,
    "GeneratedCoverJpegQuality": 85
  }
}
```

- `MaxPlaylistItemCount`: `null` (Standard) bedeutet keine Beschränkung. Bei Überschreitung wird HTTP 400 zurückgegeben. Wird eine Playlist durch die automatische Nachlieferung (siehe unten) nachbefüllt, gilt dasselbe Limit; ist es bereits erreicht, liefert der Hintergrundprozess für diese Playlist einfach nichts nach (kein Fehler).
- `DefaultPageSize`: Seitengröße, die `GET /api/playlists/{id}/entries/paged` verwendet, wenn kein `pageSize`-Parameter übergeben wird (Standard: `20`).
- `MaxPageSize`: obere Grenze für den `pageSize`-Parameter von `GET /api/playlists/{id}/entries/paged` (Standard: `100`); größere Werte führen zu HTTP 400.
- `BackfillIntervalMinutes`: Zeitabstand in Minuten, in dem der Hintergrundprozess prüft, ob Playlists mit vollständig enthaltenen Serien/Staffeln/Filmsammlungen neue Inhalte (neue Staffel, neue Episode, neuer Film) nachgeliefert bekommen sollen (Standard: `15`). Werte kleiner 1 werden wie `1` behandelt.
- `BackfillBatchSize`: wie viele Playlists der Hintergrundprozess pro Durchlauf höchstens prüft (Standard: `25`), damit ein einzelner Durchlauf kurz bleibt; alle betroffenen Playlists werden über mehrere Durchläufe reihum abgedeckt. Werte kleiner 1 werden wie `1` behandelt.
- `AllowedCoverImageFormats`: kommagetrennte Liste der für `POST /api/playlists/{id}/cover/upload` akzeptierten MIME-Types (Standard: `image/jpeg,image/png,image/webp`); Vergleich erfolgt case-insensitiv.
- `MaxCoverImageSizeBytes`: maximale Dateigröße eines Cover-Uploads in Bytes (Standard: `5242880` = 5 MB); wird im Controller bereits vor dem Einlesen des Inhalts und erneut im `PlaylistCoverValidator` geprüft.
- `MaxCoverImageWidthPixels` / `MaxCoverImageHeightPixels` / `MaxCoverImageTotalPixels`: maximale Breite, Höhe und Gesamtzahl der Bildpunkte (Breite × Höhe) eines Cover-Uploads (Standard: `4096` / `4096` / `16777216`); geprüft am Bildkopf, bevor das Bild dekodiert wird (Schutz vor „Decompression Bombs"). Ein Wert ≤ `0` schaltet die jeweilige Prüfung ab. Die vollständige Dekodierung zur Integritätsprüfung benötigt kurzzeitig rund 4 Byte je Bildpunkt Arbeitsspeicher.
- `GeneratedCoverWidthPixels` / `GeneratedCoverHeightPixels`: Zielabmessungen der automatisch erzeugten Cover-Collage in Pixeln (Standard: `1600` × `520`); werden auch als `Width`/`Height` der gespeicherten `Picture`-Zeile übernommen.
- `GeneratedCoverJpegQuality`: JPEG-Qualität (0–100) der erzeugten Collage (Standard: `85`).

Es gibt für die automatische Nachlieferung keinen eigenen REST-Endpunkt - der Mechanismus läuft ausschließlich als Hintergrundprozess (`PlaylistBackfillWorker`) und verändert Playlist-Einträge über dieselben Tabellen, die auch `POST /api/playlists/{id}/entries` verwendet. Details zum fachlichen Verhalten siehe `playlists-business-rules.md`, BR-18 und BR-19.
