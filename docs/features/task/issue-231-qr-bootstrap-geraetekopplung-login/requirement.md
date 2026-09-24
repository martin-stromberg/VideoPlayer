# Übersetzte Anforderung: QR-Bootstrap — Gerätekopplung + Login per Einmal-Ticket aus der Profilseite

Quelle: GitHub-Issue `martin-stromberg/VideoPlayer` #231 (Schritt 5a des App-Projekts `martin-stromberg/VideoPlayer-App#17`).

## Fachliche Zusammenfassung

Selbstbedienungs-Onboarding: Ein eingeloggter Benutzer erzeugt im Profilbereich (`Account/Manage`) ein kurzlebiges Einmal-Ticket, das als QR-Code (`https://<server>/pairing?t=<ticket>`) und als 8-Zeichen-Kurzcode angezeigt wird. Die Client-App löst das Ticket über einen neuen öffentlichen Endpunkt `POST api/pairing/bootstrap` ein und erhält — über denselben ECDH-P-256-/AES-256-GCM-Kanal wie `api/pairing/exchange` — Geräte-Token **plus** JWT-Usersession **plus** Refresh-Token. Damit entfällt die Passwort-Eingabe auf dem Gerät. Zusätzlich wird ein klassischer Refresh-Token-Mechanismus mit Rotation eingeführt (`RefreshToken`-Entität, Refresh-Endpunkt, Widerruf bei Logout und Gerätewiderruf). Das `PairingCode`-Modell wird um eine Pairing-Art/Richtung (`Kind`) und ein längeres Ticket-Secret erweitert, damit später auch die Gegenrichtung (Gerät zeigt Code, z. B. TV) abbildbar ist — diese Richtung wird **nicht** implementiert.

## Festgelegte Entscheidungen (aus Issue/App-Projektplan, nicht zur Disposition)

- QR-Payload: `https://<server>/pairing?t=<ticket>`; Adresse aus `Request.Scheme` + `Request.Host`; keine Zugangsdaten im QR; localhost/127.0.0.1 → Hinweis statt still unbrauchbarer Adresse.
- Neuer öffentlicher Endpunkt `api/pairing/bootstrap` (gleiches anonymes Profil wie `exchange`: kein `X-API-Key`, kein Bearer); `api/pairing/exchange` bleibt unverändert.
- Antwort: ECDH P-256 + AES-256-GCM-verschlüsselter Payload mit Geräte-Token + JWT + Refresh-Token; Ciphertext-Layout kompatibel zu `exchange` (Base64(`nonce`‖`ciphertext`‖`tag`), `DeriveKeyFromHmac` SHA-256).
- Klassischer Refresh-Token mit Rotation: neue Entität (Hash-only-Speicherung, `UserId`, `DeviceId`, `CreatedAtUtc`/`ExpiresAtUtc`/`RevokedAtUtc`/`ReplacedByHash`), EF-Migration committed.
- `PairingCode` um `Kind`/Richtung erweitern (beide Richtungen vorsehen, nur Bootstrap-Richtung implementieren); Ticket-Secret ≥128 Bit (Base64url); 8-Zeichen-Kurzcode ist Alias/Lookup auf dasselbe Ticket — beide Eingabewege lösen dasselbe Ticket auf.
- Berechtigung: alle eingeloggten Benutzer + Rate-Limit je Benutzer + konfigurierbare Option „nur Administratoren" (Default: aus).
- Ticket: Single-Use, TTL ~5 Minuten, atomarer Verbrauch via `ExecuteUpdateAsync`, `CreatedByUserId` gesetzt.
- Fehler-Mapping: 400 (Format), 401 (ungültig/abgelaufen/verbraucht), 429 (Rate-Limit/IP-Sperre); Vertrag in `docs/API.md` dokumentieren.
- Profilseite: neuer Abschnitt „Geräte" mit Button „Gerät koppeln" → QR-Bild + 8-Zeichen-Text + Kurzanleitung + verbleibende Gültigkeit; QR-Rendering über gepinntes Paket (Version, Lizenz, .NET-Kompatibilität dokumentieren).

## Akzeptanzkriterien (aus Issue #231)

1. Profilseite zeigt „Gerät koppeln": QR + 8-Zeichen-Text + Kurzanleitung; Rate-Limit je Benutzer.
2. `api/pairing/bootstrap` verbraucht Ticket atomar, liefert verschlüsselt Geräte-Token + JWT + Refresh-Token; öffentlich.
3. Datenmodell trägt beide Pairing-Richtungen.
4. Refresh-Endpunkt mit Rotation.
5. Admin-Only-Option konfigurierbar; Vertrag dokumentiert (`docs/API.md`).

## Betroffene Klassen und Komponenten

- **Datenmodell:** `PairingCode` (neue Felder `Kind`, `TicketHash` o. ä. für das lange Secret); neue Entität `RefreshToken`; `PairedDevice` (Verknüpfung `DeviceId` des Refresh-Tokens); `ApplicationDbContext` (DbSets); `Data/Configurations/*` (EF-Konfigurationen); neue EF-Migration.
- **Logik/Services:** `PairingService` (neue Methoden `CreateBootstrapTicketAsync`, `BootstrapAsync`; `ExchangeAsync` auf `Kind`-Filter eingrenzen); neuer `RefreshTokenService` (Issue/Rotate/Revoke/RevokeForDevice); `DeviceTokenService.RevokeAsync` (Refresh-Tokens des Geräts mitwiderrufen); `AuthorizationTokenService` (JWT-Ausstellung für den Ticket-Ersteller); `ILoginIpBlockService` (Brute-Force-Schutz des Bootstrap-Endpunkts).
- **Interfaces:** `IPairingService` (erweitert), `IRefreshTokenService` (neu).
- **Enums:** `PairingCodeKind` (Bootstrap-/Exchange-/TV-Richtung), Fehler-Kind-Aufzählung um Rate-Limit-/Forbidden-Fälle.
- **Controller:** `PairingController` (neuer `POST api/pairing/bootstrap`), `AuthController` (neuer `POST api/auth/refresh`, `POST api/auth/logout` für Refresh-Widerruf).
- **DTOs (`VideoWebPlayer.Client/Models`):** `PairingBootstrapRequest`, `PairingBootstrapResponse`, `RefreshTokenRequest`, `RefreshTokenResponse` (separate Vertragskopie liegt im App-Repo — Feldnamen verbindlich).
- **UI:** neue Profilseite `Components/Account/Pages/Manage/Devices.razor` („Geräte", statisch gerendert nach Manage-Seiten-Muster) + `ManageNavMenu.razor`-Eintrag; QR-Rendering via QRCoder (`PngByteQRCode` → Data-URL).
- **Konfiguration:** `Pairing:BootstrapTicketTtlMinutes`, `Pairing:BootstrapAdminOnly`, `Pairing:BootstrapMaxTicketsPerHour`, `Auth:RefreshTokenTtlDays` (o. ä.; IConfiguration-Konvention, keine Options-Klasse vorhanden).
- **Tests:** neue Service-Tests (`PairingTestDb`-Muster), Contract-Tests (`PairingWebApplicationFactory`-Muster), E2E-Tests (Playwright nach `DevicePairingE2ETests`-Vorbild), Erweiterung `ApiDocumentationContractTests`.
- **Dokumentation:** `docs/API.md` (Bootstrap-/Refresh-/Logout-Vertrag inkl. Feldnamen und Fehlercodes), `docs/help/` (Gerät-koppeln-Anleitung), `docs/GUIDE_Installation.md`/`README.md`/`docs/RELEASE_NOTES.md` (Konfiguration), `docs/SECRETS_MANAGEMENT.md` falls nötig.

## Implementierungsansatz

- `PairingCode` erhält `Kind` (Enum `PairingCodeKind`, Default = bisheriger Admin-Code) und `TicketHash` (SHA-256 des langen Bootstrap-Secrets, nullable). Bootstrap-Tickets bekommen beide Hashes: `TicketHash` für das QR-Ticket, `CodeHash` für den 8-Zeichen-Alias — beide lösen denselben Datensatz auf. `ExchangeAsync` filtert zusätzlich auf `Kind == ExchangeCode`, damit Bootstrap-Kurzcodes den Admin-Flow nicht umgehen (Antwortvertrag unverändert).
- `PairingService.BootstrapAsync` spiegelt `ExchangeAsync`: Validierung (400), Lookup über `TicketHash`/`CodeHash` + `Kind == BootstrapTicket` (401), atomarer Verbrauch via `ExecuteUpdateAsync`, ECDH-Server-Schlüssel, AES-256-GCM über `DeriveKeyFromHmac`-Schlüssel; Payload = JSON `{deviceToken, token, expires, refreshToken}` (verbindliche camelCase-Feldnamen), verschlüsselt als Base64(nonce‖ct‖tag) im Response-Feld `encryptedPayload`.
- `RefreshTokenService`: Token = Base64url(32 B), SHA-256-Hash persistiert; Rotation beim Refresh-Endpunkt atomar (`ExecuteUpdateAsync` auf `RevokedAtUtc == null && ExpiresAtUtc > now`), `ReplacedByHash` auf den Nachfolger; Widerruf einzeln (Logout) und gesamt je `DeviceId` (Gerätewiderruf/-löschung); Refresh prüft zusätzlich, dass das zugehörige Gerät nicht widerrufen ist.
- Rate-Limit: Datenbankbasiert — Anzahl `PairingCodes` mit `Kind == BootstrapTicket`, `CreatedByUserId == user` und `CreatedAtUtc > now-1h` gegen `Pairing:BootstrapMaxTicketsPerHour` (Default 10); Überschreitung → Fehler in der Profilseite (HTTP-relevant nur bei API-Erzeugung; UI zeigt Meldung).
- Profilseite: neue Manage-Unterseite `Account/Manage/Devices` im statischen SSR-Formularmuster der Account-Seiten (EditForm/`method="post"`); Button „Gerät koppeln" → Ticket erzeugen → QR als PNG-Data-URL (QRCoder `PngByteQRCode`), 8-Zeichen-Code, Kurzanleitung, „Gültig bis"; Server-Adresse aus `HttpContext.Request.Scheme`/`Host`; bei localhost/Loopback Warnhinweis.
- Endpunkte: `POST api/pairing/bootstrap` ohne Attribute (öffentlich, mit `ILoginIpBlockService`-Prüfung → 429 wie `exchange`); `POST api/auth/refresh` und `POST api/auth/logout` mit `[ApiTokenCheck(MauiOnly)]` (Gerät besitzt dann bereits ein Gate-Token — konsistent zu `api/auth/login`).

## Konfiguration

Anwendungseinstellungen (`appsettings`/Umgebungsvariablen/User Secrets), bestehende `IConfiguration`-Konvention (`Pairing:*`-Präfix; Secrets nie in `appsettings.json`):

| Schlüssel | Standard | Zweck |
|-----------|----------|-------|
| `Pairing:BootstrapTicketTtlMinutes` | 5 | TTL des Bootstrap-Tickets |
| `Pairing:BootstrapMaxTicketsPerHour` | 10 | Rate-Limit je Benutzer |
| `Pairing:BootstrapAdminOnly` | `false` | Nur Administratoren dürfen Tickets erzeugen |
| `Auth:RefreshTokenTtlDays` | 30 | Lebensdauer der Refresh-Tokens |

## Offene Fragen

Keine — alle Entscheidungen sind im Issue/App-Projektplan festgelegt; Detailvarianten (Feldnamen, Konfigurationsschlüssel) werden im Umsetzungsplan verbindlich festgezurrt und in `docs/API.md` dokumentiert.
