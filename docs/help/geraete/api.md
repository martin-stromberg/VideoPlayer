← [Zurück zur Übersicht](index.md)

# Geräte — API

## Übersicht

Das Feature exponiert einen einzigen öffentlichen Endpunkt `POST /api/pairing/exchange` zum Einlösen eines Einmal-Pairing-Codes gegen ein Geräte-Token. Daneben ändert sich die Semantik des `X-API-Key`-Gates: Im Scope `MauiOnly` (derzeit nur `POST /api/auth/login`) werden neben dem Konfigurationstoken `Jwt:ApiToken:Maui` auch in der Datenbank gespeicherte, nicht widerrufene Geräte-Tokens akzeptiert.

## Authentifizierung

`POST /api/pairing/exchange` erfordert weder `X-API-Key` noch `Authorization: Bearer`. Schutz erfolgt über den Einmal-Code, dessen kurze Gültigkeit und die IP-Sperre.

Für alle `MauiOnly`-Endpunkte gilt als `X-API-Key` entweder ein Geräte-Token (ausgestellt über den Pairing-Exchange, nicht widerrufen) oder der Fallback-Konfigurationstoken `Jwt:ApiToken:Maui`.

## Rate Limiting

Fehlgeschlagene Einlöseversuche (`401`) werden pro Client-IP gezählt. Ab 5 Fehlversuchen wird die IP gesperrt und der Endpunkt antwortet mit `429`. Der Zähler ist mit dem Web-Login geteilt (`ILoginIpBlockService`); ein erfolgreicher Exchange setzt ihn zurück. Formatfehler (`400`) zählen nicht als Fehlversuch.

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

## Weitere betroffene Schnittstelle

### `X-API-Key` bei `MauiOnly`-Endpunkten

`POST /api/auth/login` akzeptiert als `X-API-Key` zusätzlich zum Konfigurationstoken `Jwt:ApiToken:Maui` jedes gespeicherte, nicht widerrufene Geräte-Token. Ein Widerruf in der Admin-UI führt sofort zu `401` für Folgeanfragen; bereits ausgestellte Benutzer-JWTs bleiben bis zum Ablauf (12 Stunden) gültig.

Siehe auch den Gesamtvertrag in [API.md](../../API.md).
