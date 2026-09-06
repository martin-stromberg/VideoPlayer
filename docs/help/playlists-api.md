# Playlists – API-Dokumentation

## Übersicht

Die Playlist-API bietet Endpunkte zur Verwaltung von Benutzer-Playlists und deren Medieninhalte. Alle Operationen erfordern Authentifizierung via Bearer Token und unterliegen Berechtigungsprüfung: Nur der Besitzer einer Playlist darf diese ändern.

## Authentifizierung

Alle Endpunkte erfordern einen gültigen Bearer Token im `Authorization`-Header:

```
Authorization: Bearer {token}
```

Fehlerhafte oder fehlende Authentifizierung führt zu HTTP 401 (Unauthorized).

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
    "addedAt": "2026-09-05T14:30:00Z"
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
      "addedAt": "2026-09-05T14:30:00Z"
    }
  ],
  "skippedDuplicateCount": 0,
  "message": "3 Titel hinzugefuegt."
}
```

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
    "addedAt": "2026-09-05T14:30:00Z"
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
    "addedAt": "2026-09-05T14:30:00Z"
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
}
```

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
    "MaxPlaylistItemCount": 1000
  }
}
```

Wenn der Wert `null` ist (Standard), gibt es keine Beschränkung. Bei Überschreitung wird HTTP 400 zurückgegeben.
