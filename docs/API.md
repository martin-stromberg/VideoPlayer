# VideoWebPlayer API

> **Dokumenttyp**: Technische Dokumentation  
> **Zielgruppe**: Backend-Entwickler, API-Integratoren  
> **Version**: 1.0  
> **Letzte Aktualisierung**: 2026-09-26

Diese Datei beschreibt den versionierten API-Vertrag des Web-Repositorys. Die DTOs liegen unter `VideoWebPlayer.Client/`.

## Basis und Authentifizierung

- Basis-URL lokal: `http://localhost:5000`, sofern `Host:Address` und `Host:Port` nicht anders konfiguriert sind.
- `GET /api/health` ist ohne Authentifizierung erreichbar.
- `POST /api/pairing/exchange` und `POST /api/pairing/bootstrap` sind ohne Authentifizierung erreichbar und dienen dem Geräte-Pairing (siehe Abschnitt [Pairing](#pairing)).
- `POST /api/auth/login`, `POST /api/auth/refresh` und `POST /api/auth/logout` benötigen den Header `X-API-Key: <GERÄTE_TOKEN>`. Als Geräte-Tokens gelten die in der Datenbank gespeicherten, nicht widerrufenen Tokens aus dem Pairing-Verfahren. Zusätzlich bleibt der statische Konfigurationstoken `Jwt:ApiToken:Maui` als Fallback für ältere App-Versionen akzeptiert.
- Alle übrigen API-Endpunkte benötigen `Authorization: Bearer <JWT_ACCESS_TOKEN>`, sofern sie nicht ausdrücklich als öffentlich dokumentiert sind.
- Bei **jedem** dieser Endpunkte kann der Anmeldenachweis alternativ als Abfrageparameter `?access_token=<JWT_ACCESS_TOKEN>` mitgegeben werden — der Server prüft zuerst den `Authorization`-Header und greift nur bei dessen Fehlen auf den Abfrageparameter zurück. Nötig ist das für Abrufe, die ein Browser ohne eigene Header auslöst (Bilder in `<img>`, Videoströme in `<video>`, SignalR über WebSockets); es funktioniert aber bei allen Endpunkten gleichermaßen, auch bei `POST`, `PUT`, `PATCH` und `DELETE`. Ein mitgegebener `Authorization`-Header hat immer Vorrang. Weil der Wert dabei in der URL steht und damit in Server- und Proxy-Protokollen landen kann, wird er nur verwendet, wo ein Header nicht möglich ist.
- Der API-Key ist ein Client-Gate und kein Ersatz für ein Benutzer-Secret oder die JWT-Autorisierung. Backendwerte sind als sensible Konfigurationswerte zu behandeln und dürfen nur aus kontrollierten Konfigurationsquellen kommen.
- JWT-Signaturschlüssel und produktive API-Tokens werden ausschließlich über User Secrets, Umgebungsvariablen oder ein Secret-Management-System gesetzt.
- Widerruf: Ein in der Admin-Oberfläche (`/admin/devices`) widerrufenes Geräte-Token wird beim Gate sofort mit `401` abgelehnt. Bereits ausgestellte Benutzer-JWTs bleiben bis zu ihrem Ablauf (12 Stunden) gültig.

## Standardstatuscodes

| Status | Bedeutung |
|--------|-----------|
| `200 OK` | JSON- oder Binärantwort erfolgreich. |
| `204 No Content` | Mutation erfolgreich, keine Antwortnutzlast. |
| `400 Bad Request` | Ungültiger Request, beispielsweise unbekannter `MediaType`. |
| `401 Unauthorized` | API-Key fehlt/ist falsch oder Bearer-Token fehlt/ist ungültig. |
| `403 Forbidden` | Benutzer ist angemeldet, besitzt aber keinen Zugriff auf die Quelle oder das Bild. |
| `404 Not Found` | Ressource existiert nicht oder ist für den Benutzer nicht erreichbar. |
| `409 Conflict` | Der Aufruf würde Daten verlieren oder widerspricht dem aktuellen Zustand; die Antwort nennt die erforderliche Bestätigung. |
| `429 Too Many Requests` | Die Client-IP ist wegen wiederholter Fehlversuche gesperrt. |
| `500 Internal Server Error` | Unerwarteter Serverfehler. |

Die Playlist-Endpunkte halten sich an diese Tabelle. Bei den Medien-Endpunkten (`GET /api/items/{type}/{id}`, `.../stream`, `.../download`) weicht das heutige Verhalten davon ab: Fehlt einem **angemeldeten** Anwender die Freischaltung, antworten sie mit `401 Unauthorized` statt mit `403 Forbidden`; eine unbekannte Kennung oder ein Titel ohne hinterlegte Videodatei ergibt in einem Teil der Fälle `500 Internal Server Error` statt `404 Not Found`. Ein Client sollte ein `401` von diesen Endpunkten daher nicht als „Sitzung abgelaufen“ deuten und keine Sitzungserneuerung auslösen. Die Korrektur auf `403` bzw. `404` ist als eigene Anforderung erfasst; bis dahin gilt das hier beschriebene Verhalten.

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

## Pairing

### POST /api/pairing/exchange

Öffentlicher Endpunkt ohne `X-API-Key`- und ohne `Bearer`-Anforderung. Löst einen kurzlebigen Einmal-Pairing-Code gegen ein Geräte-Token ein. Der Pairing-Code wird vom Administrator unter `/admin/devices` erzeugt (Standard: 8 Zeichen, 5 Minuten gültig, konfigurierbar über `Pairing:CodeLength` und `Pairing:CodeTtlMinutes`).

Die Übertragung des Geräte-Tokens wird auf Anwendungsebene geschützt, da der Server ohne TLS betrieben werden kann:

- Client und Server verwenden jeweils ein flüchtiges ECDH-Schlüsselpaar über der Kurve `nistP256` (P-256). Public Keys werden als SubjectPublicKeyInfo (DER, Base64-kodiert) übertragen.
- Der AES-256-Schlüssel wird aus dem ECDH Shared Secret via `ECDiffieHellman.DeriveKeyFromHmac` mit `HashAlgorithmName.SHA256` abgeleitet (Parameter `hmacKey`, `secretPrepend`, `secretAppend` jeweils `null`).
- Das Geräte-Token wird mit AES-256-GCM verschlüsselt. `encryptedToken` enthält Base64(`nonce` ‖ `ciphertext` ‖ `tag`) mit einer 12-Byte-Nonce und einem 16-Byte-Tag.

Die verbindlichen JSON-Feldnamen sind camelCase.

Request:

```json
{
  "code": "<PAIRING_CODE>",
  "clientPublicKey": "<BASE64_SPKI_P256>",
  "deviceName": "<OPTIONALER_GERAETENAME>"
}
```

| Feld | Pflicht | Regel |
|------|---------|-------|
| `code` | Ja | Nicht leer. |
| `clientPublicKey` | Ja | Als SubjectPublicKeyInfo importierbarer ECDH-Schlüssel, Kurve `nistP256`. |
| `deviceName` | Nein | Maximal 200 Zeichen; leer → Server-Default. |

Antwort:

```json
{
  "serverPublicKey": "<BASE64_SPKI_P256>",
  "encryptedToken": "<BASE64_NONCE_CIPHERTEXT_TAG>"
}
```

Fehlercodes:

| Status | Bedeutung |
|--------|-----------|
| `400 Bad Request` | Formatfehler im Request (fehlender/leerer `code`, nicht importierbarer oder falscher Kurven-Typ bei `clientPublicKey`, `deviceName` länger als 200 Zeichen). Formatfehler werden nicht als Fehlversuch gezählt. |
| `401 Unauthorized` | Pairing-Code unbekannt, abgelaufen oder bereits verbraucht. Jeder Fehlversuch zählt für die Client-IP. |
| `429 Too Many Requests` | Die Client-IP ist gesperrt (Schwelle: 5 Fehlversuche, geteilt mit dem Web-Login). |

Hinweise:

- Der Pairing-Code ist ein Einmal-Code und wird atomar verbraucht; eine zweite Verwendung schlägt fehl.
- Der Server speichert das Geräte-Token ausschließlich als SHA-256-Hash; der Klartext verlässt den Server nur verschlüsselt in dieser Response.
- `code` und `clientPublicKey` werden unverschlüsselt über HTTP übertragen; das ist unbedenklich, weil der Code nur einmalig und kurzlebig ist und keine wiederverwendbaren Secrets enthält.
- Gesperrte IPs werden unter `/admin/security` angezeigt und können dort entsperrt werden.

### POST /api/pairing/bootstrap

Öffentlicher Endpunkt ohne `X-API-Key`- und ohne `Bearer`-Anforderung — analog zum Exchange. Löst ein Bootstrap-Ticket gegen ein verschlüsseltes Paket aus **Geräte-Token, Benutzer-JWT und Refresh-Token** ein. Der QR-Bootstrap ist der Self-Service-Onboarding-Weg: Ein angemeldeter Benutzer erzeugt das Ticket auf der Profilseite unter `Profil` > `Geräte` (Route `/Account/Manage/Devices`), die App scannt den QR-Code oder der Anwender gibt den angezeigten Kurzcode ein.

Ticket-Eigenschaften:

- Das Ticket ist ein langes Geheimnis (192 Bit, Base64url ohne Padding) und wird im QR-Code als URL der Form `https://<server>/pairing?t=<ticket>` kodiert. Der 8-stellige Kurzcode ist nur ein Alias und löst denselben Datensatz auf.
- Standard: 5 Minuten gültig (`Pairing:BootstrapTicketTtlMinutes`), Einmal-Ticket, atomarer Verbrauch wie beim Exchange.
- Ticket-Erzeugung: alle angemeldeten Benutzer, Ratenlimit pro Benutzer (`Pairing:BootstrapMaxTicketsPerHour`, Standard 10/Stunde), optional auf Administratoren beschränkbar (`Pairing:BootstrapAdminOnly`, Standard `false`).

Die Verschlüsselung ist identisch zum Exchange (ECDH `nistP256`, `DeriveKeyFromHmac` SHA-256, AES-256-GCM, `encryptedPayload` = Base64(`nonce` ‖ `ciphertext` ‖ `tag`), 12-Byte-Nonce, 16-Byte-Tag) — derselbe Entschlüsselungs-Codepfad funktioniert für beide Endpunkte. Der Klartext ist ein JSON-Objekt.

Request:

```json
{
  "ticket": "<TICKET_ODER_KURZCODE>",
  "clientPublicKey": "<BASE64_SPKI_P256>",
  "deviceName": "<OPTIONALER_GERAETENAME>"
}
```

| Feld | Pflicht | Regel |
|------|---------|-------|
| `ticket` | Ja | Nicht leer; langes Ticket aus dem QR-Code oder 8-stelliger Kurzcode. |
| `clientPublicKey` | Ja | Als SubjectPublicKeyInfo importierbarer ECDH-Schlüssel, Kurve `nistP256`. |
| `deviceName` | Nein | Maximal 200 Zeichen; leer → Server-Default. |

Antwort:

```json
{
  "serverPublicKey": "<BASE64_SPKI_P256>",
  "encryptedPayload": "<BASE64_NONCE_CIPHERTEXT_TAG>"
}
```

Entschlüsselter Inhalt von `encryptedPayload` (camelCase-JSON):

```json
{
  "deviceToken": "<GERAETE_TOKEN>",
  "token": "<JWT_ACCESS_TOKEN>",
  "expires": "2026-09-24T15:00:00Z",
  "refreshToken": "<REFRESH_TOKEN>"
}
```

| Feld | Bedeutung |
|------|-----------|
| `deviceToken` | Gate-Key für `X-API-Key` (z. B. für `/api/auth/refresh`, `/api/auth/logout` und alle Maui-Endpunkte). |
| `token` / `expires` | JWT-Benutzersitzung (12 h) inkl. Ablaufzeitpunkt (UTC). |
| `refreshToken` | Refresh-Token für `POST /api/auth/refresh` (Rotation, Einmalverwendung). |

Fehlercodes:

| Status | Bedeutung |
|--------|-----------|
| `400 Bad Request` | Formatfehler im Request (fehlender/leerer `ticket`, nicht importierbarer oder falscher Kurven-Typ bei `clientPublicKey`, `deviceName` länger als 200 Zeichen). Fehlertext: `Ungültiger Bootstrap-Request.` Formatfehler verbrauchen das Ticket nicht und zählen nicht als Fehlversuch. |
| `401 Unauthorized` | Ticket unbekannt, abgelaufen oder bereits verbraucht. Fehlertext: `Ungültiges oder abgelaufenes Pairing-Ticket.` Jeder Fehlversuch zählt für die Client-IP. |
| `429 Too Many Requests` | Die Client-IP ist gesperrt (Schwelle: 5 Fehlversuche, geteilt mit dem Web-Login und dem Exchange). |

Hinweise:

- Geschützt ist ausschließlich die **Antwort**: `encryptedPayload` (Geräte-Token, JWT, Refresh-Token) ist mit AES-256-GCM über dem ECDH-Schlüssel verschlüsselt. `ticket` (bzw. der Kurzcode), `clientPublicKey` und `deviceName` werden dagegen **unverschlüsselt** übertragen — das Ticket steht im Klartext in der QR-Nutzlast (`?t=<ticket>`), der Kurzcode wird auf der Profilseite im Klartext angezeigt, und beides geht unverschlüsselt im Request-Body an den Server zurück (siehe Request-Block oben).
- Folge in einem Netz ohne TLS: Ein mitgelesenes Ticket lässt sich innerhalb seiner Gültigkeit einlösen und ergibt dann nicht nur ein Geräte-Token, sondern eine vollständige Benutzersitzung (JWT und Refresh-Token). Das Zeitfenster ist mit Einmalverwendung und Standard-Gültigkeit von 5 Minuten (`Pairing:BootstrapTicketTtlMinutes`) knapp gehalten; für den Bootstrap wird TLS dennoch ausdrücklich empfohlen, anders als beim Exchange, wo nur ein Geräte-Token auf dem Spiel steht.
- Gespeichert werden Ticket, Kurzcode, Geräte-Token und Refresh-Token ausschließlich als SHA-256-Hash; die Klartexte liegen nie in der Datenbank.
- Ein bereits verbrauchtes oder abgelaufenes Ticket kann nicht erneut eingelöst werden; der Anwender erzeugt dann auf der Profilseite ein neues Ticket.

### POST /api/auth/refresh

Erneuert die Benutzersitzung ohne Passwort. Erfordert den Header `X-API-Key` mit einem gültigen Geräte-Token (oder dem statischen `Jwt:ApiToken:Maui`-Fallback).

Header:

```http
X-API-Key: <GERAETE_TOKEN>
Content-Type: application/json
```

Request:

```json
{
  "refreshToken": "<REFRESH_TOKEN>"
}
```

Antwort:

```json
{
  "token": "<JWT_ACCESS_TOKEN>",
  "expires": "2026-09-24T15:00:00Z",
  "refreshToken": "<NEUER_REFRESH_TOKEN>"
}
```

Fehlercodes:

| Status | Bedeutung |
|--------|-----------|
| `400 Bad Request` | `refreshToken` fehlt oder ist leer. Fehlertext: `Ungültiger Refresh-Request.` |
| `401 Unauthorized` | Kein/ungültiger `X-API-Key`, oder Refresh-Token unbekannt, abgelaufen, widerrufen bzw. das gekoppelte Gerät wurde widerrufen. Fehlertext: `Ungültiger oder abgelaufener Refresh-Token.` |

Hinweise:

- Rotation: Der vorgelegte Refresh-Token wird atomar widerrufen und durch einen neuen ersetzt. Ein bereits rotierter Token darf nicht erneut vorgelegt werden — dann wird die komplette Token-Familie des Geräts gesperrt (Reuse-Detection).
- Standard-Lebensdauer: 30 Tage (`Auth:RefreshTokenTtlDays`). Es wird nur der SHA-256-Hash gespeichert.
- Widerruf eines Geräts (`/admin/devices`) sperrt sofort alle Refresh-Tokens des Geräts; die Refresh-Route verweigert außerdem Tokens, deren Gerät nicht mehr aktiv ist.

### POST /api/auth/logout

Widerruft einen Refresh-Token (Logout auf dem Gerät). Idempotent — liefert immer `200 OK`, auch bei unbekanntem Token. Erfordert `X-API-Key` wie `/api/auth/refresh`.

Request:

```json
{
  "refreshToken": "<REFRESH_TOKEN>"
}
```

Antwort: `200 OK` ohne Body.

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
Playlists anderer). Optional `?genreId=<id>` als Filter: Es werden nur Playlists geliefert, die dieses
Genre führen.

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

Query:

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `confirmContinueWatchingRemoval` | `bool` | Standard `false`. Bestätigt das Entfernen, obwohl ein Weiterschauen-Eintrag **dieser** Playlist noch auf den Titel verweist. Ohne Bezug wirkungslos. |

Antwort: `204 No Content`.

- `400 Bad Request`, wenn `mediaType` keinem unterstützten Medientyp entspricht.
- `409 Conflict` mit `DtoRemovePlaylistEntryConflictResponse`
  (`{ "isContinueWatchingConfirmationRequired": true }`), wenn ein Weiterschauen-Eintrag dieser
  Playlist auf den Titel verweist und `confirmContinueWatchingRemoval` nicht `true` ist. Es wird
  dabei nichts entfernt; der Client fragt den Anwender und wiederholt den Aufruf mit
  `confirmContinueWatchingRemoval=true`. Nach der Bestätigung wird der Weiterschauen-Eintrag durch
  den nächsten verfügbaren Titel derselben Playlist ersetzt oder entfernt.
- `404 Not Found`, wenn kein passender Eintrag in der Playlist vorhanden ist.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### GET /api/playlists/{id}/entries

Liefert alle Einträge einer Playlist als `DtoPlaylistEntry[]` (unsortiert). Einträge, deren
referenzierter Medieninhalt nicht mehr existiert, werden dabei still aus der Datenbank entfernt
und nicht in der Antwort aufgeführt.

- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört und nicht öffentlich ist.

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
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört und nicht öffentlich ist.

### PUT /api/playlists/{id}/entries/{entryId}/order

Setzt die manuelle Sortierposition (`SortOrder`) eines einzelnen Eintrags. Nur im Sortiermodus `Manual`
verwendbar; benachbarte Einträge werden **nicht** automatisch angepasst (dafür gibt es
`move-to-beginning` und `move-between`). Nur der Besitzer.

Request (`DtoReorderPlaylistEntryRequest`):

```json
{
  "newSortOrder": 3
}
```

Antwort: `200 OK` ohne Nutzlast.

- `400 Bad Request`, wenn `newSortOrder` negativ oder der Anfrage-Body leer ist.
- `409 Conflict`, wenn die Playlist nicht im Sortiermodus `Manual` ist.
- `404 Not Found`, wenn die Playlist oder der Eintrag nicht existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### POST /api/playlists/{id}/entries/batch-reorder

Setzt die Sortierpositionen mehrerer Einträge in einer atomaren Operation (alle oder keine). Nur im
Sortiermodus `Manual`; nur der Besitzer.

Request (`DtoBatchReorderPlaylistEntriesRequest`):

```json
{
  "reorderOperations": [
    { "entryId": 456, "newSortOrder": 0 },
    { "entryId": 457, "newSortOrder": 1 }
  ]
}
```

Innerhalb der Anfrage müssen sowohl die `entryId`- als auch die `newSortOrder`-Werte eindeutig sein; eine
Kollision mit einem nicht in der Anfrage enthaltenen Eintrag ist dagegen erlaubt.

Antwort: `DtoPlaylistEntry[]` der aktualisierten Einträge.

- `400 Bad Request` bei leerer Liste, doppelter `entryId` oder negativem `newSortOrder`.
- `409 Conflict` bei doppeltem `newSortOrder` innerhalb der Anfrage oder wenn die Playlist nicht im
  Sortiermodus `Manual` ist.
- `404 Not Found`, wenn die Playlist oder mindestens ein Eintrag nicht existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### GET /api/playlists/{id}/entries/max-sort-order

Liefert die höchste vergebene Sortierposition über die **gesamte** Playlist (nicht nur die geladene Seite)
für die Schnellaktion „An Ende“. Hilfsabfrage der Umsortierung und daher wie ein schreibender Zugriff
behandelt: nur der Besitzer.

Antwort (`DtoMaxSortOrderResult`):

```json
{
  "maxSortOrder": 12
}
```

`maxSortOrder` ist `null`, wenn kein Eintrag eine Sortierposition gesetzt hat (leere Playlist oder noch
nie im Sortiermodus `Manual`).

- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### POST /api/playlists/{id}/entries/{entryId}/move-to-beginning

Verschiebt einen Eintrag an die erste Position der manuellen Reihenfolge und schiebt dabei alle übrigen
Einträge atomar um eins nach hinten. Nur im Sortiermodus `Manual`; nur der Besitzer. Kein Request-Body.

Antwort: das aktualisierte `DtoPlaylistEntry` (`sortOrder = 0`).

- `400 Bad Request`, wenn der Eintrag keine Sortierposition gesetzt hat (der Endpunkt führt
  intern denselben Ablauf aus wie `move-between` mit Zielposition 0).
- `409 Conflict`, wenn die Playlist nicht im Sortiermodus `Manual` ist.
- `404 Not Found`, wenn die Playlist oder der Eintrag nicht existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### POST /api/playlists/{id}/entries/{entryId}/move-between

Verschiebt einen Eintrag auf eine beliebige Zielposition und schiebt dabei alle Einträge zwischen
bisheriger und Zielposition atomar um eins in die Gegenrichtung. Das ist der Endpunkt, den das Umsortieren
per Ziehen (Drag & Drop) verwendet — im Unterschied zu `.../order` kollidiert er nie mit der
Sortierposition des Zieleintrags. Nur im Sortiermodus `Manual`; nur der Besitzer.

Request (`DtoReorderPlaylistEntryRequest`): dieselbe Struktur wie bei `.../order`; `newSortOrder` ist die
Zielposition.

Antwort: `200 OK` ohne Nutzlast.

- `400 Bad Request`, wenn `newSortOrder` negativ ist, der Anfrage-Body leer ist oder der Eintrag
  keine Sortierposition gesetzt hat.
- `409 Conflict`, wenn die Playlist nicht im Sortiermodus `Manual` ist.
- `404 Not Found`, wenn die Playlist oder der Eintrag nicht existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### PATCH /api/playlists/{id}/sort-mode

Ändert den Sortiermodus einer Playlist. Nur der Besitzer.

Request (`DtoChangeSortModeRequest`):

```json
{
  "newSortMode": "Manual",
  "confirmLossOfManualOrder": false
}
```

- `ByReleaseDate` → `Manual`: Jedem Eintrag wird einmalig eine fortlaufende Sortierposition zugewiesen,
  die der zuletzt angezeigten Reihenfolge entspricht. Keine Bestätigung nötig.
- `Manual` → `ByReleaseDate`: Die manuelle Reihenfolge geht verloren, daher ist
  `confirmLossOfManualOrder: true` erforderlich.
- Ist der Zielmodus schon der aktuelle, ändert sich nichts.

Antwort: aktualisiertes `DtoPlaylist`.

- `400 Bad Request`, wenn `newSortMode` kein gültiger Modus ist oder der Anfrage-Body leer ist.
- `409 Conflict` mit `DtoChangeSortModeConflictResponse`
  (`{ "isLossOfDataConfirmationRequired": true }`), wenn von `Manual` nach `ByReleaseDate` ohne
  Bestätigung gewechselt wird. Es wird dabei nichts verändert.
- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### PUT /api/playlists/{id}/genres

Überschreibt die Genres einer Playlist mit genau den angegebenen Genre-IDs und setzt
`genresManuallyOverridden` auf `true`; die automatische Ableitung aus den Inhalten ruht danach. Nur der
Besitzer. Einen eigenen Lese-Endpunkt gibt es nicht — die Genres sind Teil von `DtoPlaylist`.

Request (`DtoSetPlaylistGenresRequest`):

```json
{
  "genreIds": [12, 47]
}
```

Unbekannte IDs werden stillschweigend ignoriert. Antwort: aktualisiertes `DtoPlaylist` mit
`genresManuallyOverridden: true`.

- `400 Bad Request`, wenn der Anfrage-Body leer ist.
- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### POST /api/playlists/{id}/genres/reset

Hebt eine manuelle Genre-Auswahl auf (`genresManuallyOverridden` wird `false`) und berechnet die Genres
sofort aus den aktuellen Inhalten neu. Nur der Besitzer. Kein Request-Body.

Antwort: aktualisiertes `DtoPlaylist`.

- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### POST /api/playlists/{id}/play

Startet die Wiedergabe einer Playlist. Lesender Zugriff: der Besitzer, oder jeder angemeldete Anwender,
solange die Playlist öffentlich ist. Die Endpunkte der Wiedergabe sind zustandslos — der Server merkt
sich keinen Wiedergabe-Kontext, der Client übergibt ihn bei jedem Aufruf. Nicht abspielbare
Sammel-Einträge (`TVShow`, `TVShowSeason`, `MovieCollection`) und Einträge ohne Freischaltung des
Anfragenden werden bei der Auswahl übersprungen.

Query:

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `entryId` | `long?` | Eintrag, an dem die Wiedergabe beginnen soll; ohne Angabe der erste abspielbare, zugängliche Eintrag. |

Antwort (`DtoPlaylistPlaybackStart`):

```json
{
  "playlistId": 1,
  "playlistName": "Meine Favoriten",
  "totalCount": 12,
  "currentPosition": 3,
  "currentEntryId": 458,
  "currentEntry": { "id": 458, "mediaType": "TVShowEpisode", "mediaId": 1001, "..." : "..." },
  "streamUrl": "/api/items/tvshowepisode/1001/stream",
  "mediaType": "episode",
  "mediaId": 1001
}
```

`streamUrl` enthält bewusst keinen `access_token`-Parameter; der Client hängt seinen eigenen
Anmeldenachweis selbst an.

- `400 Bad Request`, wenn die Playlist keinen abspielbaren, zugänglichen Eintrag enthält (ohne `entryId`)
  oder der angegebene `entryId` ein nicht abspielbarer Sammel-Eintrag ist.
- `403 Forbidden`, wenn die Playlist privat und fremd ist oder der angegebene `entryId` für den
  Anfragenden nicht freigeschaltet ist.
- `404 Not Found`, wenn die Playlist nicht existiert oder der `entryId` nicht zu ihr gehört.

### POST /api/playlists/{id}/play/next

Liefert den nächsten abspielbaren und zugänglichen Eintrag nach `currentEntryId` in der aktuell gültigen
Sortierung. Lesender Zugriff wie bei `.../play`.

Query:

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `currentEntryId` | `long` | Pflicht. Der gerade abgespielte Eintrag. |

Antwort: `DtoPlaylistNavigationResult` mit dem nächsten Eintrag und dessen Position.

- `204 No Content`, wenn es keinen weiteren abspielbaren, zugänglichen Eintrag gibt (Ende der Playlist).
- `400 Bad Request`, wenn `currentEntryId` nicht zu dieser Playlist gehört.
- `403 Forbidden`, wenn die Playlist privat und fremd ist.
- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.

### POST /api/playlists/{id}/play/previous

Wie `.../play/next`, jedoch rückwärts: der vorherige abspielbare und zugängliche Eintrag vor
`currentEntryId`. Parameter, Antwortformat und Fehlerantworten sind identisch; `204 No Content` bedeutet
hier, dass der Anfang der Playlist erreicht ist.

### POST /api/playlists/{id}/play/advance

Automatisches Weiterschalten, wenn ein Titel zu Ende läuft. Ermittelt denselben Eintrag wie
`.../play/next` und ist nur deshalb ein eigener Endpunkt, damit automatisches Weiterschalten und ein
manueller „Nächster“-Klick im Server-Protokoll unterscheidbar sind. Parameter, Antwortformat und
Fehlerantworten wie bei `.../play/next`.

### POST /api/playlists/{id}/cover/upload

Speichert eine hochgeladene Bilddatei als Cover der Playlist und ersetzt ein vorhandenes Cover
(hochgeladen oder erzeugt); das bisherige Bild wird gelöscht. Nur der Besitzer.

Request: `multipart/form-data` mit dem Feld `file` (die Bilddatei). Weitere Felder gibt es nicht — ein
Beschnitt findet serverseitig nicht statt.

Antwort (`DtoPlaylistCoverResult`):

```json
{
  "success": true,
  "message": "Bild erfolgreich hochgeladen.",
  "pictureId": 123
}
```

Geprüft werden in dieser Reihenfolge: Datei vorhanden und nicht leer, gemeldete Länge nicht größer als
`Playlists:MaxCoverImageSizeBytes` (noch vor dem Einlesen des Inhalts), dann der Inhalt selbst
(gemeldeter und tatsächlich erkannter MIME-Type in `Playlists:AllowedCoverImageFormats`, Pixelmaße gemäß
`Playlists:MaxCoverImageWidthPixels`/`MaxCoverImageHeightPixels`/`MaxCoverImageTotalPixels`, Bild
vollständig dekodierbar). Gespeichert wird das Originalformat mit dem **erkannten** MIME-Type.

- `400 Bad Request` bei jedem Verstoß gegen diese Prüfungen, mit deutschem Klartext als Antwortkörper
  (bewusst nicht im `DtoPlaylistCoverResult`-Format).
- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### POST /api/playlists/{id}/cover/regenerate

Erzeugt das Cover neu als Collage aus den Bildern der aktuellen Inhalte und ersetzt das bisherige Cover.
Nur der Besitzer. Kein Request-Body.

Query:

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `confirmReplaceUploadedCover` | `bool` | Standard `false`. Bestätigt das Ersetzen eines **hochgeladenen** Covers. |

Antwort (`DtoPlaylistCoverResult`): `success: true` mit `pictureId` des neuen Bildes. Enthält die Playlist
keine Inhalte mit Bildern, bleibt die Antwort `200 OK`, jedoch mit `success: false`,
`message: "Keine Bilder verfügbar."` und ohne `pictureId`; ein hochgeladenes Cover bleibt dann unverändert
und es wird auch nicht nachgefragt.

- `409 Conflict` mit `DtoRegeneratePlaylistCoverConflictResponse`
  (`{ "isUploadedCoverReplacementConfirmationRequired": true }`), wenn das aktuelle Cover hochgeladen wurde
  und `confirmReplaceUploadedCover` nicht `true` ist. Es wird dabei nichts verändert.
- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### POST /api/playlists/{id}/cover/preview

Erzeugt dieselbe Collage wie `cover/regenerate`, **speichert aber nichts** und liefert sie nur als
Bilddaten zurück (Vorschau im Bild-Panel der Detailseite). Deshalb ist auch für ein hochgeladenes Cover
keine Bestätigung nötig. Nur der Besitzer — auch bei einer öffentlichen Playlist antwortet der Endpunkt
für alle anderen mit `403`, weil die Vorschau Teil des Änderungsablaufs ist. Kein Request-Body.

Antwort (`DtoPlaylistCoverPreview`):

```json
{
  "success": true,
  "message": null,
  "contentType": "image/jpeg",
  "imageData": "/9j/4AAQSkZJRgABAQ..."
}
```

`imageData` ist der Base64-kodierte JPEG-Inhalt. Ohne verwendbare Bilder bleibt die Antwort `200 OK` mit
`success: false` und `message: "Keine Bilder verfügbar."`.

- `404 Not Found`, wenn keine Playlist mit dieser ID existiert.
- `403 Forbidden`, wenn die Playlist einem anderen Benutzer gehört.

### GET /api/playlists/{id}/cover

Liefert das Coverbild der Playlist (hochgeladen oder erzeugt) als Binärantwort mit dem gespeicherten
Content-Type. Lesender Zugriff: der Besitzer, oder jeder angemeldete Anwender, solange die Playlist
öffentlich ist. Da der Abruf typischerweise aus einem `<img>`-Element erfolgt, wird der Anmeldenachweis
hier üblicherweise als Abfrageparameter `access_token` mitgegeben (siehe
[Basis und Authentifizierung](#basis-und-authentifizierung)). Dieselbe Prüfung gilt beim Abruf desselben
Bildes über `GET /api/pictures/{id}`.

- `404 Not Found`, wenn die Playlist nicht existiert oder kein Cover gesetzt ist (der Client zeigt dann
  den Platzhalter).
- `403 Forbidden`, wenn die Playlist privat ist und einem anderen Benutzer gehört.

### DELETE /api/playlists/{id}/cover

Entfernt das Cover der Playlist und löscht das zugehörige Bild. Ist kein Cover gesetzt, ist der Aufruf
wirkungslos erfolgreich. Nur der Besitzer.

Antwort (`DtoPlaylistCoverResult`):

```json
{
  "success": true,
  "message": null,
  "pictureId": null
}
```

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

Der Test `VideoWebPlayer.Tests.ApiDocumentationContractTests` stellt sicher, dass diese Dokumentation die Kernrouten enthält — einschließlich aller Playlist-Routen und der Sitzungs-Endpunkte `POST /api/pairing/bootstrap`, `POST /api/auth/refresh` und `POST /api/auth/logout` — und dass der Laufzeitvertrag funktioniert: `GET /api/health`, `POST /api/auth/login`, ein authentifizierter `GET /api/items` sowie ein vollständiger Playlist-Durchlauf (anmelden, Playlist anlegen, Titel hinzufügen, Wiedergabe starten, nächsten Titel holen). Wird eine dieser Routen aus der Dokumentation entfernt, schlägt der Test fehl. Für lokale Prüfung:

```bash
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter ApiDocumentationContractTests
```
