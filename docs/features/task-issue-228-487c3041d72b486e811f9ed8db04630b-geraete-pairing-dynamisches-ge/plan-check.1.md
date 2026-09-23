# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Admin erzeugt in der Web-UI kurzlebigen Einmal-Pairing-Code (TTL-Anzeige, nur Metadaten in Liste) | `Devices.razor` unter `/admin/devices`, `IPairingService.CreatePairingCodeAsync`, `PairingCode`-Entität mit `CodeHash`/`ExpiresAtUtc`/`ConsumedAtUtc` (Schritte 1–2, 5, 9; Programmablauf „Pairing-Code erzeugen") | E2E-Szenario 1 (`DevicePairingE2ETests`), `CreatePairingCodeAsync_ReturnsCodeAndPersistsHash` | Abgedeckt |
| Client löst Code über neuen öffentlichen Endpunkt `POST api/pairing/exchange` gegen Geräte-Token ein | `PairingController` ohne `ApiTokenCheck`/`BearerTokenCheck`, DTOs in `VideoWebPlayer.Client/Models/` (Schritte 3, 7; Programmablauf „Pairing-Exchange") | `PairingExchangeContractTests` (`..._RequiresNoAuth_Returns200OnValidCode`, `..._Returns401OnInvalidCode`), E2E-Gesamtfluss (Szenario 2) | Abgedeckt |
| Token-Austausch per ECDH + symmetrischer Verschlüsselung geschützt (HTTP ohne TLS) | Verbindlicher Krypto-Vertrag: ECDH `nistP256`, SPKI/Base64, Schlüsselableitung via SHA-256-HMAC, AES-256-GCM mit 12-Byte-Nonce (Designentscheidung „Krypto-Vertrag", Schritt 5) | `ExchangeAsync_ValidRequest_ReturnsDecryptableTokenAndPersistsDevice` (Roundtrip-Entschlüsselung), `Exchange_ResponseDecryptsToWorkingLoginToken` | Abgedeckt |
| `ApiTokenCheckAttribute` akzeptiert bei `MauiOnly` zusätzlich gespeicherte, nicht widerrufene Geräte-Tokens (Hash-Vergleich, `LastUsedAtUtc`-Pflege) | Umstellung auf `OnActionExecutionAsync`, `IDeviceTokenService` aus `RequestServices`, `SHA-256`-Vergleich mit `RevokedAtUtc == null` (Schritt 8; Programmablauf „Request-Gate") | `ApiTokenCheckAttribute_MauiOnly_AcceptsDeviceToken`, `..._MauiConfigTokenStillAccepted`, `IsValidDeviceTokenAsync_ValidToken_SucceedsAndUpdatesLastUsed` | Abgedeckt |
| `AnyClient`-Verhalten bleibt unverändert (kein DB-Zugriff) | Config-Pfad bleibt Vorab-Check; DB-Prüfung nur bei `MauiOnly` (Schritt 8, Seiteneffekte) | `..._AnyClient_DoesNotTouchDeviceTokens` | Abgedeckt |
| Widerruf: `RevokedAtUtc` setzen, Token danach abgelehnt; Geräteliste in Admin-UI | `IDeviceTokenService.RevokeAsync`/`GetDevicesAsync`, Widerruf-Aktion in `Devices.razor` (Schritte 4, 9; Programmablauf „Gerät widerrufen") | `..._RevokedToken_Fails`, `RevokeAsync_SetsRevokedAtUtc`, E2E-Szenario 3 (Widerruf → Login `401`) | Abgedeckt |
| Rate-Limiting/Bruteforce-Schutz auf `exchange` pro IP (persistente Sperre) | Wiederverwendung `ILoginIpBlockService` (`IsBlocked` → `429`, `RegisterFailure` bei ungültigem Code, `RegisterSuccess` bei Erfolg) (Designentscheidung, Schritt 7, Validierungsregeln) | `..._Returns429AfterRepeatedFailures`, E2E-Szenario 4 (Sperre in `/admin/security` sichtbar) | Abgedeckt |
| Code-Validierung: nicht abgelaufen, nicht verwendet, atomarer Einmalverbrauch (Race-Schutz) | Lookup per `CodeHash`, `ExpiresAtUtc`/`ConsumedAtUtc`-Prüfung, `ExecuteUpdateAsync` mit `WHERE ConsumedAtUtc IS NULL` (Schritt 5, Programmablauf Punkt 4–5) | `..._ExpiredCode_Fails`, `..._ConsumedCode_Fails`, `ExchangeAsync_ConcurrentConsume_OnlyOneSucceeds`, `ExchangeAsync_UnknownCode_Fails` | Abgedeckt |
| Klartext-Geheimnisse nicht persistiert (Token-/Code-Hash, flüchtiger Server-Key) | `TokenHash`/`CodeHash` = SHA-256, Klartext nur einmalig in Response/UI (Designentscheidungen „Token-Speicherung"/„Pairing-Code-Speicherung") | `IssueAsync_StoresHashNotPlaintext`, `CreatePairingCodeAsync_ReturnsCodeAndPersistsHash` („Klartext nicht in DB") | Abgedeckt |
| Neues Datenmodell + EF-Migration (`PairedDevice`, `PairingCode`, Konfigurationen, DbSets, `AddDevicePairing`) | Schritte 1–2, Abschnitt „Datenbankmigrationen"; automatische Anwendung via `MigrateDatabase()` | Indirekt über In-Memory-SQLite-Service-Tests (`PairingServiceTests`/`DeviceTokenServiceTests` nach `ApplicationDbContextTests`-Muster) | Abgedeckt |
| Generische Fehlermeldung bei ungültigem Code (kein Orakel), kein Token-Wert im Log | `401` generisch + `RegisterFailure`; Log ohne Token-Wert (Programmablauf Punkt 4, Schritt 8) | `..._Returns401OnInvalidCode`; Bestandstest `InvalidTokenLogDoesNotIncludeHeaderValue` wird auf Async-Pipeline angepasst | Abgedeckt |
| `Jwt:ApiToken:Maui`-Fallback bzw. Produktions-Pflichtcheck entschieden | Bleibt als Fallback; Pflichtcheck `ServiceCollectionExtensions.cs` Zeile 54–59 unverändert (Designentscheidung, Seiteneffekte) | Bestandstests `AddVideoWebPlayerServices_ProductionRequiresMauiApiToken`, `MauiContract_RuntimeLoginHealthAndAuthenticatedRead_Succeeds` bleiben gültig (benannt) | Abgedeckt |
| Admin-UI nur für Administratoren (`IsAdmin`-Claim, „Nicht autorisiert." für Nicht-Admins) | Claim-Prüfung in `OnInitializedAsync` nach `Security.razor`-Muster (Programmablauf Punkt 2, Schritt 9) | **Kein Test geplant** — weder Unit/Integration noch E2E für Nicht-Admin-Zugriff auf `/admin/devices` | Lücke |
| Request-Validierung `DeviceName` (optional, ≤ 200 → `400` oder gekürzt) | Validierungsregel im Plan definiert (Abschnitt „Validierungsregeln") | **Kein Test geplant** für Überschreitung/Kürzung | Lücke |
| Dokumentation: `API.md`, `GUIDE_Installation.md`, `docs/help`, `SECRETS_MANAGEMENT.md` | Schritt 12 inkl. Widerruf-/JWT-Restlaufzeit-Hinweis und `Jwt:ApiToken:Maui`-Fallback-Rolle | `ApiDocumentationContainsMauiRelevantRoutes` wird um `POST /api/pairing/exchange` erweitert | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] Berechtigungs-/Sichtbarkeitstest für `/admin/devices`: Nicht-Admin (bzw. nicht authentifizierter Aufruf) sieht „Nicht autorisiert." und kann weder Pairing-Code erzeugen noch Geräte auflisten/widerrufen. Da die Regel über die UI erreichbar ist, ist mindestens ein E2E-Szenario erforderlich (Akzeptanzkriterium: Admin erzeugt Pairing-Code in der Web-UI; Programmablauf Punkt 2).
- [ ] Negativtest für die im Plan definierte Validierung `PairingExchangeRequest.DeviceName` (> 200 Zeichen → `400` oder serverseitige Kürzung) in `PairingServiceTests` oder `PairingExchangeContractTests`.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Admin meldet sich an, öffnet `/admin/devices`, erzeugt Pairing-Code; Code + TTL einmalig sichtbar | `DevicePairingE2ETests` Szenario 1 | Abgedeckt |
| Gesamtfluss: UI-Code → ECDH-Exchange → entschlüsseltes Token → `POST /api/auth/login` mit `X-API-Key` erfolgreich | `DevicePairingE2ETests` Szenario 2 | Abgedeckt |
| Admin widerruft Gerät → Login mit Geräte-Token → `401` | `DevicePairingE2ETests` Szenario 3 | Abgedeckt |
| Ungültiger Code → Fehler; IP nach wiederholten Fehlversuchen gesperrt und in `/admin/security` sichtbar | `DevicePairingE2ETests` Szenario 4 | Abgedeckt |
| Nicht-Admin ruft `/admin/devices` auf → „Nicht autorisiert.", keine Aktionen möglich | Nicht geplant | Lücke |
| App-seitiger Pairing-Clientfluss (Eingabe des Codes in der App) | — | Nicht erforderlich mit Begründung: App liegt im separaten Repository `martin-stromberg/VideoPlayer-App`; der verbindliche JSON-Vertrag ist serverseitig festgelegt und per E2E-Gesamtfluss über Test-HttpClient nachgewiesen. |

## Fehlende oder unvollständige Planbestandteile

- [ ] Kein Testschritt/-nachweis für die Admin-Berechtigung auf `Devices.razor` (siehe Testlücken) — als E2E-Szenario in `DevicePairingE2ETests` ergänzen (z. B. Benutzer ohne `IsAdmin`-Claim via Seeding).
- [ ] Kein Testnachweis für die `DeviceName`-Validierungsregel — konkreten Negativtest in den Tests-Abschnitt aufnehmen.

## Hinweise

- `inventory.md` verlinkt `inventory/configuration.md`; diese Datei existiert nicht im Feature-Verzeichnis. Die Konfigurationsfakten (`Jwt:*`-Schlüssel, Produktions-Pflichtcheck, fehlende `Pairing`-Sektion, kein `Microsoft.AspNetCore.RateLimiting`) sind über `inventory/logic.md` bzw. den Plan selbst abgedeckt, sodass dem Plan keine Umsetzungslücke entsteht — die Bestandsaufnahme sollte dennoch nachgezogen werden (fehlende Datei ergänzen oder Link korrigieren).
- Der Plan definiert `GetActiveCodesAsync` (Metadaten aktiver Codes für die UI) ohne eigenen Service-Test; der bestehende Test `CreatePairingCodeAsync_ReturnsCodeAndPersistsHash` deckt Persistierung/Hashing ab, nicht jedoch die Filterung (abgelaufene/verbrauchte Codes). Bei Nachplanung empfohlen.
- E2E-Szenario 4 kombiniert Endpunkt-Fehlerfall und Admin-UI-Sichtbarkeit — beim Umsetzen sicherstellen, dass beide Teilnachweise (HTTP-Fehler + Sichtbarkeit in `/admin/security`) im selben Test tatsächlich geprüft werden.
