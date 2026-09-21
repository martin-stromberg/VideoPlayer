# VideoWebPlayer API

> **Dokumenttyp**: Technische Dokumentation  
> **Zielgruppe**: Backend-Entwickler, API-Integratoren  
> **Version**: 1.0  
> **Letzte Aktualisierung**: 2026-08-25

Diese Datei beschreibt den versionierten API-Vertrag des Web-Repositorys. Die DTOs liegen unter `VideoWebPlayer.Client/`.

## Basis und Authentifizierung

- Basis-URL lokal: `http://localhost:5000`, sofern `Host:Address` und `Host:Port` nicht anders konfiguriert sind.
- `GET /api/health` ist ohne Authentifizierung erreichbar.
- `POST /api/auth/login` benötigt den Header `X-API-Key: <CLIENT_API_TOKEN>`.
- Alle übrigen API-Endpunkte benötigen `Authorization: Bearer <JWT_ACCESS_TOKEN>`, sofern sie nicht ausdrücklich als öffentlich dokumentiert sind.
- Der API-Key ist ein Client-Gate und kein Ersatz für ein Benutzer-Secret oder die JWT-Autorisierung. Backendwerte sind als sensible Konfigurationswerte zu behandeln und dürfen nur aus kontrollierten Konfigurationsquellen kommen.
- JWT-Signaturschlüssel und produktive API-Tokens werden ausschließlich über User Secrets, Umgebungsvariablen oder ein Secret-Management-System gesetzt.

## Standardstatuscodes

| Status | Bedeutung |
|--------|-----------|
| `200 OK` | JSON- oder Binärantwort erfolgreich. |
| `204 No Content` | Mutation erfolgreich, keine Antwortnutzlast. |
| `400 Bad Request` | Ungültiger Request, beispielsweise unbekannter `MediaType`. |
| `401 Unauthorized` | API-Key fehlt/ist falsch oder Bearer-Token fehlt/ist ungültig. |
| `403 Forbidden` | Benutzer ist angemeldet, besitzt aber keinen Zugriff auf die Quelle oder das Bild. |
| `404 Not Found` | Ressource existiert nicht oder ist für den Benutzer nicht erreichbar. |
| `500 Internal Server Error` | Unerwarteter Serverfehler. |

## Health und Login

### GET /api/health

Prüft, ob der Webserver erreichbar ist.

Antwort:

```text
OK
```

### POST /api/auth/login

Authentifiziert einen Benutzer und liefert ein JWT.

Header:

```http
X-API-Key: <CLIENT_API_TOKEN>
Content-Type: application/json
```

Request:

```json
{
  "email": "user@example.invalid",
  "password": "<BENUTZER_PASSWORT>"
}
```

Antwort:

```json
{
  "token": "<JWT_ACCESS_TOKEN>",
  "expires": "2026-08-25T15:00:00Z"
}
```

## Quellen und Genres

### GET /api/Sources

Gibt die für den angemeldeten Benutzer freigeschalteten Medienquellen zurück.

Antwort: `DtoMediaSource[]`

```json
[
  {
    "id": 1,
    "name": "Private Library",
    "mediaSourceId": 0,
    "createdAt": "2026-08-25T10:00:00Z",
    "lastScannedAt": "2026-08-25T11:00:00Z",
    "iconPictureId": 12
  }
]
```

### GET /api/Sources/{id}

Liefert eine einzelne freigeschaltete Quelle. Gibt `403` zurück, wenn die Quelle dem Benutzer nicht zugeordnet ist.

### GET /api/SourceGenres

Liefert Genregruppen für alle freigeschalteten Quellen.

### GET /api/SourceGenres/{sourceId}

Liefert sichtbare Genres einer einzelnen Quelle.

Antwort: `SourceGenresDto`

```json
{
  "sourceId": 1,
  "sourceName": "Private Library",
  "genres": [
    { "id": 5, "name": "Drama", "iconUrl": "/images/genres/<ICON_DATEI>.png" }
  ]
}
```

### GET /api/sourceicons/{id}

Liefert ein hochgeladenes Quellen-Icon als Binärantwort. Der Benutzer muss Zugriff auf die Quelle besitzen, die dieses Icon verwendet.

## Medien und Bilder

### GET /api/items

Listet Medien-Einstiege aus Filmkollektionen und Serien.

Query:

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `mediaSourceId` | `long?` | Optionale Einschränkung auf eine Quelle. |
| `page` | `int` | Nullbasierte Seite, Standard `0`. |
| `size` | `int` | Seitengröße, Standard `30`. |
| `search` | `string?` | Optionaler Suchtext. |
| `genreId` | `long?` | Optionaler Genre-Filter. |

Antwort: `MediaEntryDto[]`

```json
[
  {
    "type": "Movie",
    "id": 42,
    "title": "Example Movie",
    "description": "",
    "url": "/moviecollection/42",
    "createdAt": "2026-08-25T10:00:00Z",
    "pictureId": 100,
    "itemCount": 1
  }
]
```

### GET /api/items/recent

Liefert zuletzt veröffentlichte oder zuletzt relevante Einträge als `DtoRecentEntry[]`.

### GET /api/items/genres

Liefert editierbare Genre-Optionen als `DtoGenreOption[]`.

### GET /api/items/{type}/{id}

Liefert Details zu einem Medieneintrag. Unterstützte `type`-Werte sind:

- `moviecollection`
- `movie`
- `tvshow`
- `tvshowseason`
- `tvshowepisode`

Antworten sind je nach Typ `DtoMovieCollection`, `DtoMovie`, `DtoTVShow`, `DtoTVShowSeason` oder `DtoTVShowEpisode`.

### GET /api/items/{type}/{id}/stream

Streamt eine Film- oder Episodendatei mit Range-Unterstützung. Unterstützte Stream-Typen sind `movie` und `tvshowepisode`; `tvshow` wird serverseitig auf `tvshowepisode` normalisiert.

Antworten:

- `video/mp4`, `video/x-matroska`, `video/x-msvideo`, `video/mpeg` oder `application/octet-stream`
- `400`, wenn Typ oder ID ungültig sind
- `404`, wenn kein Medienitem oder keine Datei gefunden wurde

### GET /api/items/{type}/{id}/download

Liefert dieselbe Datei als Download (`application/octet-stream`).

### GET /api/pictures/{id}

Liefert ein Poster-, Banner-, Fanart- oder Platzhalterbild als Binärantwort. Der Content-Type stammt aus dem gespeicherten Bild, ansonsten `image/jpg` bzw. `image/png` beim Platzhalter.

### GET /api/pictures/hero-background

Liefert das generierte Hero-Hintergrundbild aus der Weiterschauen-Liste oder einen Platzhalter.

### GET /api/episodes/{episodeId}/background-image

Liefert ein generiertes Episoden-Hintergrundbild. Die Antwort setzt `Cache-Control` und `ETag`; bei passendem `If-None-Match` kann `304` zurückgegeben werden.

## Favoriten

### GET /api/favorites

Liefert die Favoriten des aktuellen Benutzers als `DtoFavoriteEntry[]`.

### POST /api/favorites/toggle

Schaltet den Favoritenstatus eines `DtoMediaEntry` um.

Request:

```json
{
  "id": 42,
  "name": "Example Movie",
  "mediaSourceId": 1,
  "isFavorite": false
}
```

Antwort: `true`, wenn der Eintrag danach favorisiert ist, sonst `false`.

### POST /api/favorites/add

Browser- und Kompatibilitäts-Endpunkt zum Hinzufügen eines Favoriten.

### POST /api/favorites/remove

Entfernt einen Favoriten. Der aktuelle Client nutzt `RemoveFavoriteAsync(long favoriteId)` mit `{ "id": <id>, "userId": "anonymous" }`.

## Continue Watching

### GET /api/continue-watching

Liefert die Weiterschauen-Liste des aktuellen Benutzers als `ContinueWatchingDto[]`.

### POST /api/continue-watching/progress

Meldet Wiedergabefortschritt.

Request:

```json
{
  "mediaType": "movie",
  "mediaId": 42,
  "positionSeconds": 120,
  "durationSeconds": 5400
}
```

`mediaType` akzeptiert `movie`, `episode` und `tvshowepisode`.

Antwort: `204 No Content`.

### POST /api/continue-watching/hide

Blendet einen Eintrag aus.

Request:

```json
{
  "mediaType": "episode",
  "mediaId": 77
}
```

Antwort:

```json
{
  "status": "hidden",
  "message": "Eintrag wurde ausgeblendet."
}
```

### POST /api/continue-watching/skip

Überspringt einen Eintrag und ersetzt ihn gegebenenfalls durch den nächsten Titel.

Der Nachfolger hängt davon ab, ob der Eintrag an eine Playlist gebunden ist (`playlistId` im Rumpf):

- **mit `playlistId`:** der nächste abspielbare und für den anfragenden Anwender zugängliche Titel
  dieser Playlist in deren aktueller Sortierung (nicht abspielbare Sammel-Einträge und gesperrte Titel
  werden übersprungen; aus der Playlist entfernte Titel kommen nicht vor). Gibt es keinen, wird der
  Eintrag entfernt (`removed`).
- **ohne `playlistId`:** wie bisher die nächste Episode der Serie bzw. der nächste Film der Sammlung.

Antwortstatuswerte:

- `replaced`
- `removed`

## Playlists

Alle Playlist-Endpunkte sind benutzerbezogen: Sie wirken ausschließlich auf die Playlists des
aktuell authentifizierten Anwenders. Ausnahme sind öffentliche Playlists (von einem Administrator als
öffentlich gekennzeichnet): Sie dürfen alle Anwender **lesen** (Abruf, Einträge, Wiedergabe, Cover), ändern
darf sie weiterhin nur der Besitzer (`403 Forbidden` für jeden anderen, auch für Administratoren). Die
vollständige Zuordnung lesend/schreibend je Endpunkt steht in
[Playlists – API-Dokumentation](help/playlists-api.md).

### GET /api/playlists

Liefert alle Playlists des aktuellen Benutzers als `DtoPlaylist[]` (nur die eigenen, keine öffentlichen
Playlists anderer).

### GET /api/playlists/public

Liefert alle öffentlich gekennzeichneten Playlists als `DtoPlaylist[]`; optional `?genreId=` als Filter.

### PUT /api/playlists/{id}/public

Setzt oder entfernt die Kennzeichnung „öffentlich" (Request `{ "isPublic": true }`). Nur für Administratoren, die
Besitzer der Playlist sind; sonst `403 Forbidden`. Das Entfernen entzieht anderen Anwendern sofort den Zugriff.

### GET /api/playlists/{id}

Liefert eine einzelne Playlist als `DtoPlaylist`.

- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört und nicht öffentlich ist.

### POST /api/playlists

Erstellt eine neue Playlist.

Request (`DtoCreatePlaylistRequest`):

```json
{
  "name": "Serien-Marathon",
  "description": "Meine Lieblingsserien",
  "sortMode": "ByReleaseDate"
}
```

`sortMode` akzeptiert `ByReleaseDate` (Standard) oder `Manual`. `name` ist erforderlich
(max. 255 Zeichen, pro Benutzer eindeutig), `description` ist optional (max. 2000 Zeichen).

Antwort: `DtoPlaylist` der neu erstellten Playlist.

- `400 Bad Request` bei ungültigen Eingaben (z. B. leerer Name, zu lang).
- `409 Conflict`, wenn bereits eine Playlist mit demselben Namen existiert.

### PUT /api/playlists/{id}

Aktualisiert Name, Beschreibung und Sortiermodus einer bestehenden Playlist.

Request (`DtoUpdatePlaylistRequest`): identische Struktur wie beim Erstellen.

Antwort: aktualisiertes `DtoPlaylist`.

- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.
- `400 Bad Request` / `409 Conflict` analog zum Erstellen.

### DELETE /api/playlists/{id}

Löscht eine Playlist endgültig.

Antwort: `204 No Content`.

- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### POST /api/playlists/{id}/entries

Fügt einen Medieninhalt zur Playlist hinzu. Unterstützte `mediaType`-Werte: `Movie`,
`TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection` (case-insensitiv, wird beim Speichern
auf die kanonische Schreibweise normalisiert). Beim Hinzufügen von `TVShow`, `TVShowSeason` oder
`MovieCollection` werden alle zugehörigen Staffeln/Episoden bzw. Filme automatisch mit hinzugefügt
(Cascade-Logik). Bereits vorhandene Einträge — Top-Level oder Cascade-Kind — werden dabei
übersprungen statt einen Fehler auszulösen.

Request (`DtoAddMediaToPlaylistRequest`):

```json
{
  "mediaType": "Movie",
  "mediaId": 42
}
```

Antwort: `DtoPlaylistAddResult`:

```json
{
  "topLevelEntry": { "id": 456, "playlistId": 1, "mediaType": "Movie", "mediaId": 42, "..." : "..." },
  "addedEntries": [ { "id": 456, "playlistId": 1, "mediaType": "Movie", "mediaId": 42, "..." : "..." } ],
  "skippedDuplicateCount": 0,
  "message": "1 Titel hinzugefügt."
}
```

`topLevelEntry` ist `null`, wenn der angeforderte Eintrag bereits vorhanden war. `addedEntries`
enthält alle neu angelegten Einträge (Top-Level plus Cascade-Kinder). Duplikate — egal ob
Top-Level oder Cascade — führen **nicht** zu einem Fehler, sondern werden in
`skippedDuplicateCount` gezählt; die Antwort bleibt `200 OK`.

- `400 Bad Request`, wenn `mediaType` ungültig oder `mediaId` nicht größer als 0 ist.
- `404 Not Found`, wenn der Medieninhalt oder die Playlist nicht existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}

Entfernt einen Medieninhalt aus der Playlist. `mediaType` wird case-insensitiv verarbeitet und
vor dem Abgleich auf die kanonische Schreibweise normalisiert (z. B. `"movie"` findet denselben
Eintrag wie `"Movie"`).

Antwort: `204 No Content`.

- `400 Bad Request`, wenn `mediaType` keinem unterstützten Medientyp entspricht.
- `404 Not Found`, wenn kein passender Eintrag in der Playlist vorhanden ist.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### GET /api/playlists/{id}/entries

Liefert alle Einträge einer Playlist als `DtoPlaylistEntry[]` (unsortiert). Einträge, deren
referenzierter Medieninhalt nicht mehr existiert, werden dabei still aus der Datenbank entfernt
und nicht in der Antwort aufgeführt.

- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### GET /api/playlists/{id}/entries/paged

Liefert eine sortierte, paginierte Seite der Einträge einer Playlist als
`DtoPlaylistEntriesPagedResult`. Wird von der Playlist-Detailseite für das schrittweise Nachladen
beim Scrollen (Virtual Scrolling) verwendet. Verwaiste Einträge werden wie bei
`GET /api/playlists/{id}/entries` still bereinigt.

**Query-Parameter:**

| Name | Typ | Pflicht | Standard | Beschreibung |
|------|-----|---------|----------|--------------|
| `pageNumber` | int | Nein | `1` | 1-basierte Seitennummer, muss ≥ 1 sein |
| `pageSize` | int | Nein | `Playlists:DefaultPageSize` (20) | Anzahl Einträge pro Seite, muss zwischen 1 und `Playlists:MaxPageSize` (100) liegen |

**Sortierung:** Ist `Playlist.SortMode` auf `ByReleaseDate` gesetzt, werden die Einträge nach
Erscheinungsdatum des referenzierten Medieninhalts sortiert; fehlt dieses, wird auf
Hierarchie-Reihenfolge (übergeordnete Serie/Staffel, dann Episoden-/Staffelnummer) und zuletzt auf
den Zeitpunkt des Hinzufügens (`AddedAt`) zurückgefallen. Bei `Manual` wird nach `AddedAt`
sortiert.

Antwort (`DtoPlaylistEntriesPagedResult`):

```json
{
  "entries": [ { "id": 456, "playlistId": 1, "mediaType": "Movie", "mediaId": 42, "..." : "..." } ],
  "totalCount": 57,
  "hasNextPage": true,
  "pageNumber": 1,
  "pageSize": 20
}
```

- `400 Bad Request`, wenn `pageNumber < 1` oder `pageSize` außerhalb von `1..MaxPageSize` liegt.
- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

## SignalR

### GET /hubs/mediaupdate

SignalR-Hub für Medien-Updates. Clients verbinden sich mit:

```text
<BASE_URL>/hubs/mediaupdate
```

Der Hub ist kein REST-Endpunkt.

## Admininterne Endpunkte

Die folgenden Controller sind browser-/adminintern und nicht als allgemeine öffentliche API freigegeben:

- `admin/backups/api/*`
- `admin/updates/api/*`
- `api/admin/sources/*`
- `POST /api/auth/impersonate`
- Formular-, Razor- und Blazor-Komponentenrouten

Diese Endpunkte können zusätzliche Rollen, Browser-Kontext oder Admin-Rechte voraussetzen und dürfen nicht ohne gesonderte Abstimmung in externen Clients verwendet werden.

## Vertragscheck

Der Test `VideoWebPlayer.Tests.ApiDocumentationContractTests` stellt sicher, dass diese Dokumentation die Kernrouten enthält und dass der Laufzeitvertrag für `GET /api/health`, `POST /api/auth/login` und einen authentifizierten `GET /api/items` funktioniert. Für lokale Prüfung:

```bash
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter ApiDocumentationContractTests
```
