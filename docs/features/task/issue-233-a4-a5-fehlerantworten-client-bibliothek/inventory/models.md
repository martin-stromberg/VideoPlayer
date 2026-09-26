# Bestandsaufnahme: Datenmodelle (DTOs) und Exceptions

## A5 — Pairing und Authentifizierung (DTOs vorhanden)

### `PairingExchangeRequest`
Datei: `VideoWebPlayer.Client/Models/PairingExchangeRequest.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Code` | string | One-Time Pairing Code |
| `ClientPublicKey` | string | ECDH-Öffentlicher Schlüssel (nistP256) als Base64 SubjectPublicKeyInfo |
| `DeviceName` | string? | Optionaler Gerätename (max. 200 Zeichen) |

### `PairingExchangeResponse`
Datei: `VideoWebPlayer.Client/Models/PairingExchangeResponse.cs`

Vorhanden, enthält Token-Information nach Austausch.

### `PairingBootstrapRequest`
Datei: `VideoWebPlayer.Client/Models/PairingBootstrapRequest.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Ticket` | string | Bootstrap-Ticket-Hash |
| `ClientPublicKey` | string | ECDH-Öffentlicher Schlüssel für diese Session |
| `DeviceName` | string? | Gerätename |

### `PairingBootstrapResponse`
Datei: `VideoWebPlayer.Client/Models/PairingBootstrapResponse.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `ServerPublicKey` | string | Server-Öffentlicher Schlüssel (ECDH) |
| `EncryptedPayload` | string | AES-GCM verschlüsselte Payload |

Decrypted-Inhalt siehe `PairingBootstrapPayload`.

### `PairingBootstrapPayload`
Datei: `VideoWebPlayer.Client/Models/PairingBootstrapPayload.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `DeviceToken` | string | Geräte-Token |
| `Token` | string | JWT Zugangstoken |
| `Expires` | DateTime | Token-Ablauf (UTC) |
| `RefreshToken` | string | Sitzungserneuerungs-Token |

### `RefreshTokenRequest`
Datei: `VideoWebPlayer.Client/Models/RefreshTokenRequest.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `RefreshToken` | string | Ablaufender oder zu erneuernder Token |

### `RefreshTokenResponse`
Datei: `VideoWebPlayer.Client/Models/RefreshTokenResponse.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Token` | string | Neues JWT Zugangstoken |
| `Expires` | DateTime | Ablauf des neuen Tokens (UTC) |
| `RefreshToken` | string | Neuer Refresh-Token (Rotation — alter Token wird widerrufen) |

---

## A4 — Fehlerbehandlung (Exceptions)

### `RecordNotFoundException`
Datei: `VideoWebPlayer/Controllers/Exceptions/RecordNotFoundException.cs`

**Zweck:** Geworfen wenn ein Datensatz nicht existiert (unbekannte ID oder fehlende Videodatei).

**Heute:** Fällt in generischen Exception-Handler → `StatusCode(500)` ❌

**Nach A4:** Sollte auf `NotFound(404)` abgebildet werden ✓

### `UnauthorizedAccessException` (BCL)
**Zweck:** Geworfen wenn ein angemeldeter Benutzer keine Berechtigung für eine Ressource hat.

**Heute:** Abgebildet auf `Unauthorized(401)` ❌

**Nach A4:** Sollte auf `Forbid(403)` abgebildet werden ✓

### `PlaylistAccessDeniedException` (Vorbild)
Datei: `VideoWebPlayer/Services/Exceptions/PlaylistAccessDeniedException.cs`

**Zweck:** Geworfen wenn Playlist-Zugriff verweigert (Vorbild für A4).

**Mapping:** `PlaylistsController.ExecuteAsync` (Zeile 71-74) bildet auf `Forbid()` = 403 ab ✓

**Status:** Dieses Muster wird für `ItemsController` in A4 nachgebildet.

---

## Vorhandene Hilfklassen für Tests

### `PairingTestDb`
Datei: `VideoWebPlayer.Tests/Helpers/PairingTestDb.cs`

Stellt Test-Datenbank mit Pairing-Setup bereit (für A5-Tests nutzbar).

### `PairingCryptoHelper`
Datei: `VideoWebPlayer.Tests/Helpers/PairingCryptoHelper.cs`

Crypto-Hilfsmethoden für ECDH und AES-GCM Verschlüsselung in Tests.

### `PairingWebApplicationFactory`
Datei: `VideoWebPlayer.Tests/Helpers/PairingWebApplicationFactory.cs`

WebApplicationFactory mit Pairing-Setup für E2E-Tests.

### `PairingExchangeContractTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PairingExchangeContractTestBase.cs`

Basis für Vertragstest zu Pairing-Endpunkten.
