← [Zurück zur Übersicht](index.md)

# Geräte — API

## Übersicht

Das Feature exponiert **zwei** öffentliche Endpunkte, die weder `X-API-Key` noch `Authorization: Bearer` verlangen:

| Endpunkt | Zweck |
|----------|-------|
| `POST /api/pairing/exchange` | Einmal-Pairing-Code (Administrator) gegen ein Geräte-Token einlösen |
| `POST /api/pairing/bootstrap` | Einmal-Ticket des QR-Bootstraps (oder dessen 8-stelligen Kurzcode) gegen Geräte-Token, Benutzersitzung und Erneuerungsnachweis einlösen |

Daneben ändert sich die Semantik des `X-API-Key`-Gates: Im Scope `MauiOnly` werden neben dem Konfigurationstoken `Jwt:ApiToken:Maui` auch in der Datenbank gespeicherte, nicht widerrufene Geräte-Tokens akzeptiert. Zum Scope `MauiOnly` gehören drei Endpunkte:

| Endpunkt | Zweck |
|----------|-------|
| `POST /api/auth/login` | Anmeldung eines Anwenders auf dem Gerät (Anwenderwechsel) |
| `POST /api/auth/refresh` | Sitzung erneuern, Erneuerungsnachweis rotieren |
| `POST /api/auth/logout` | Erneuerungsnachweis serverseitig sperren (Abmelden) |

`POST /api/auth/impersonate` liegt dagegen im Scope `AnyClient` und verlangt zusätzlich `Authorization: Bearer`.

## Authentifizierung

`POST /api/pairing/exchange` und `POST /api/pairing/bootstrap` erfordern weder `X-API-Key` noch `Authorization: Bearer`. Schutz erfolgt über den Einmal-Code bzw. das Einmal-Ticket, dessen kurze Gültigkeit und die IP-Sperre.

Für alle `MauiOnly`-Endpunkte gilt als `X-API-Key` entweder ein Geräte-Token (ausgestellt über Exchange oder Bootstrap, nicht widerrufen) oder der Fallback-Konfigurationstoken `Jwt:ApiToken:Maui`.

### Anmeldenachweis als Abfrageparameter

Jeder Endpunkt mit `BearerTokenCheck` akzeptiert den Anmeldenachweis alternativ als Abfrageparameter `access_token`, wenn keine `Authorization`-Kopfzeile mitgeschickt wird. Das ist für Aufrufe gedacht, die der Browser oder ein Player selbst absetzt und bei denen sich keine Kopfzeile setzen lässt — Bilder und Videoströme:

```http
GET /api/playlists/42/cover?access_token=<JWT>
GET /api/items/movie/17/stream?access_token=<JWT>
```

Der Wert ist derselbe Anmeldenachweis wie in `Authorization: Bearer <JWT>`; fehlt beides, antwortet der Endpunkt mit `401`. Für die Playlist-Endpunkte im Einzelnen siehe [Playlists — API](../playlists-api.md).

## Rate Limiting

Fehlgeschlagene Einlöseversuche (`401`) werden für beide Pairing-Endpunkte pro Client-IP gezählt. Ab 5 Fehlversuchen wird die IP gesperrt und der Endpunkt antwortet mit `429`. Der Zähler ist mit dem Web-Login geteilt (`ILoginIpBlockService`); ein erfolgreiches Einlösen setzt ihn zurück. Formatfehler (`400`) zählen nicht als Fehlversuch.

Zusätzlich begrenzt der Server die Zahl der Bootstrap-Tickets **pro Anwender und Stunde** (`Pairing:BootstrapMaxTicketsPerHour`, Standard `10`). Diese Grenze greift beim Erzeugen des Tickets auf der Profilseite, nicht beim Einlösen.

## Endpunkte / Methoden

### `POST` / `api/pairing/exchange`

**Beschreibung:** Löst einen Pairing-Code gegen ein verschlüsseltes Geräte-Token ein. Client und Server verwenden flüchtige ECDH-Schlüsselpaare über `nistP256`; der AES-256-Schlüssel wird via `ECDiffieHellman.DeriveKeyFromHmac` (SHA-256, alle optionalen Parameter `null`) abgeleitet; das Token wird mit AES-256-GCM verschlüsselt.

**Parameter (Request-Body, JSON, camelCase verbindlich):**

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `code` | string | Ja | Einmal-Pairing-Code, wie in der Admin-UI angezeigt. |
| `clientPublicKey` | string | Ja | ECDH-PublicKey des Clients, Base64-kodiertes SubjectPublicKeyInfo, Kurve `nistP256`. |
| `deviceName` | string? | Nein | Anzeigename des Geräts, maximal 200 Zeichen; leer → Server-Default. |

**Rückgabe (`200 OK`, JSON):**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `serverPublicKey` | string | ECDH-PublicKey des Servers, Base64-SPKI (`nistP256`). |
| `encryptedToken` | string | Base64(`nonce` ‖ `ciphertext` ‖ `tag`), AES-256-GCM, 12-Byte-Nonce, 16-Byte-Tag. |

**Beispiel:**

```http
POST /api/pairing/exchange
Content-Type: application/json

{
  "code": "XK7M2PQ9",
  "clientPublicKey": "<BASE64_SPKI_P256>",
  "deviceName": "TV Wohnzimmer"
}
```

```json
{
  "serverPublicKey": "<BASE64_SPKI_P256>",
  "encryptedToken": "<BASE64_NONCE_CIPHERTEXT_TAG>"
}
```

**Fehler:**

| Code | Ursache |
|------|---------|
| `400` | Formatfehler: `code`/`clientPublicKey` fehlt oder ist leer, Schlüssel nicht importierbar oder falsche Kurve, `deviceName` > 200 Zeichen. Zählt nicht als Fehlversuch. |
| `401` | Pairing-Code unbekannt, abgelaufen oder bereits verbraucht (generische Meldung, kein Orakel). Zählt als Fehlversuch. |
| `429` | Client-IP wegen wiederholter Fehlversuche gesperrt (Schwelle 5, geteilt mit dem Web-Login). |

### `POST` / `api/pairing/bootstrap`

**Beschreibung:** Löst ein Bootstrap-Ticket ein. Krypto identisch zum Exchange (ECDH `nistP256`, AES-256-GCM); verschlüsselt wird hier aber nicht nur das Geräte-Token, sondern ein ganzes Paket aus Geräte-Token, Benutzersitzung und Erneuerungsnachweis. Das lange Ticket aus dem QR-Code und der 8-stellige Kurzcode lösen denselben Datensatz auf — Gültigkeit und Einmaligkeit gelten gemeinsam.

**Parameter (Request-Body, JSON, camelCase verbindlich):**

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `ticket` | string | Ja | Das Ticket aus dem QR-Code oder der 8-stellige Kurzcode. |
| `clientPublicKey` | string | Ja | ECDH-PublicKey des Clients, Base64-SPKI, Kurve `nistP256`. |
| `deviceName` | string? | Nein | Anzeigename des Geräts, maximal 200 Zeichen; leer → Server-Default. |

**Rückgabe (`200 OK`, JSON):**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `serverPublicKey` | string | ECDH-PublicKey des Servers, Base64-SPKI (`nistP256`). |
| `encryptedPayload` | string | Base64(`nonce` ‖ `ciphertext` ‖ `tag`), AES-256-GCM. |

**Entschlüsselter Inhalt von `encryptedPayload` (JSON, camelCase):**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `deviceToken` | string | Das Geräte-Token (Gate-Key für `X-API-Key`). |
| `token` | string | Anmeldenachweis (JWT) des Anwenders, der das Ticket erzeugt hat. |
| `expires` | DateTime | Ablaufzeitpunkt des Anmeldenachweises (12 Stunden nach Ausstellung). |
| `refreshToken` | string | Erneuerungsnachweis für `POST /api/auth/refresh`. |

**Fehler:**

| Code | Ursache |
|------|---------|
| `400` | Formatfehler: `ticket`/`clientPublicKey` fehlt, Schlüssel nicht importierbar oder falsche Kurve, `deviceName` > 200 Zeichen. Zählt nicht als Fehlversuch. |
| `401` | Ticket unbekannt, abgelaufen oder bereits eingelöst; auch, wenn das erstellende Konto nicht mehr existiert. Zählt als Fehlversuch. |
| `429` | Client-IP wegen wiederholter Fehlversuche gesperrt. |

### `POST` / `api/auth/refresh`

**Beschreibung:** Erneuert die Sitzung eines Geräts und rotiert dabei den Erneuerungsnachweis: Der vorgelegte Nachweis wird in derselben Operation gesperrt und durch einen neuen ersetzt. Der neue Nachweis wird **nur hier** im Klartext zurückgegeben.

**Kopfzeile:** `X-API-Key` (Geräte-Token oder `Jwt:ApiToken:Maui`).

**Parameter (Request-Body, JSON):**

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `refreshToken` | string | Ja | Der aktuell gültige Erneuerungsnachweis. |

**Rückgabe (`200 OK`, JSON):**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `token` | string | Neuer Anmeldenachweis (JWT), 12 Stunden gültig. |
| `expires` | DateTime | Ablaufzeitpunkt des Anmeldenachweises. |
| `refreshToken` | string | Der rotierte Erneuerungsnachweis; der alte ist ab sofort ungültig. |

**Fehler:**

| Code | Ursache |
|------|---------|
| `400` | Kein Body oder `refreshToken` leer. |
| `401` | Nachweis unbekannt, abgelaufen, bereits rotiert oder gesperrt; Gerät widerrufen; Konto nicht mehr vorhanden. Ein erneut vorgelegter, bereits rotierter Nachweis sperrt zusätzlich alle übrigen Nachweise dieses Anwenders auf diesem Gerät. |
| `401` | `X-API-Key` fehlt oder ist weder ein gültiges Geräte-Token noch `Jwt:ApiToken:Maui`. |

### `POST` / `api/auth/logout`

**Beschreibung:** Sperrt den vorgelegten Erneuerungsnachweis (Abmelden auf dem Gerät). Idempotent: Ein unbekannter oder bereits gesperrter Nachweis führt ebenso zu `200 OK` wie ein gültiger; der Endpunkt gibt darüber keine Auskunft.

**Kopfzeile:** `X-API-Key` (Geräte-Token oder `Jwt:ApiToken:Maui`).

**Parameter (Request-Body, JSON):**

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `refreshToken` | string | Nein | Der zu sperrende Erneuerungsnachweis; fehlt er, passiert serverseitig nichts. |

**Rückgabe:** `200 OK` ohne Inhalt.

**Fehler:**

| Code | Ursache |
|------|---------|
| `401` | `X-API-Key` fehlt oder ist ungültig — insbesondere nach dem Widerruf des Geräts. |

## Weitere betroffene Schnittstelle

### `X-API-Key` bei `MauiOnly`-Endpunkten

`POST /api/auth/login`, `POST /api/auth/refresh` und `POST /api/auth/logout` akzeptieren als `X-API-Key` zusätzlich zum Konfigurationstoken `Jwt:ApiToken:Maui` jedes gespeicherte, nicht widerrufene Geräte-Token. Bei einem Treffer wird `PairedDevice.LastUsedAtUtc` aktualisiert. Ein Widerruf in der Admin-UI führt sofort zu `401` für Folgeanfragen; bereits ausgestellte Benutzer-JWTs bleiben bis zum Ablauf (12 Stunden) gültig.

Siehe auch den Gesamtvertrag in [API.md](../../API.md) sowie die [Client-Bibliothek](client-bibliothek.md), die alle hier beschriebenen Aufrufe bereits fertig kapselt.
