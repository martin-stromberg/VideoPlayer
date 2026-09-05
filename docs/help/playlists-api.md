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
```

**Fehlerantworten:**

| HTTP-Status | Grund | Beispiel-Body |
|-------------|-------|---------------|
| 400 Bad Request | Ungültiger `mediaType` oder `mediaId <= 0` | `"Ungültiger Medientyp."` |
| 404 Not Found | Playlist nicht gefunden oder Medieninhalt nicht vorhanden | `"Medieninhalt wurde nicht gefunden."` |
| 409 Conflict | Medieninhalt existiert bereits in dieser Playlist | `"Medieninhalt bereits in dieser Playlist vorhanden."` |
| 403 Forbidden | Benutzer ist nicht der Besitzer der Playlist | `"Sie haben keinen Zugriff auf diese Playlist."` |
| 401 Unauthorized | Fehlende oder ungültige Authentifizierung | `"Unauthorized"` |

**Besonderheit – Cascade-Logik:**

Wenn der `mediaType` einer Sammlung entspricht (Serie, Staffel oder Filmsammlung), werden automatisch alle zugehörigen Kind-Inhalte hinzugefügt:

- `TVShow` → alle Staffeln und Episoden
- `TVShowSeason` → alle Episoden
- `MovieCollection` → alle Filme

Die Antwort enthält nur den Top-Level-Eintrag. Cascade-Duplikate werden still übersprungen.

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
| Duplikat | Eintrag `(PlaylistId, MediaType, MediaId)` darf nur einmal existieren | 409 Conflict |
| Berechtigung | Anfragender Benutzer muss Besitzer der Playlist sein | 403 Forbidden |

---

## Besonderheiten

### Cascade-Duplikate bei Hinzufügen

Werden Duplikate beim Hinzufügen einer Sammlung erkannt, werden diese still übersprungen:

**Beispiel:**
- Playlist enthält bereits Episode 5 von Season 1 (hinzugefügt einzeln)
- Benutzer fügt Season 1 hinzu
- Resultat: Season 1 und Episode 1–4 werden hinzugefügt, Episode 5 wird übersprungen (existiert bereits)
- Kein Fehler wird angezeigt

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
