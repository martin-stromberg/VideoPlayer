# Tasks: QR-Bootstrap — Gerätekopplung + Login per Einmal-Ticket

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Paket | `QRCoder` 1.8.0 in `VideoWebPlayer.csproj` aufnehmen | Offen | — |
| 2 | Datenmodell | Enum `PairingCodeKind` anlegen (`AdminCode = 0`, `BootstrapTicket = 1`, `DeviceInitiated = 2`) | Offen | — |
| 3 | Datenmodell | `PairingCode` um `Kind` und `TicketHash` erweitern | Offen | — |
| 4 | Datenmodell | Entität `RefreshToken` anlegen (`Id`, `TokenHash`, `UserId`, `DeviceId`, `CreatedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc`, `ReplacedByHash`) | Offen | — |
| 5 | Datenmodell | `RefreshTokenConfiguration` + `PairingCodeConfiguration`-Erweiterung (`TicketHash`, `Kind`-Index) | Offen | — |
| 6 | Datenmodell | `DbSet<RefreshToken>` in `ApplicationDbContext` | Offen | — |
| 7 | Datenmodell | EF-Migration `AddPairingBootstrapAndRefreshTokens` erzeugen | Offen | — |
| 8 | Logik | `IssuedDeviceToken` (`Token`, `DeviceId`); `IDeviceTokenService.IssueAsync`-Rückgabe umstellen | Offen | — |
| 9 | Logik | `IRefreshTokenService`/`RefreshTokenService` (`IssueAsync`, `RotateAsync` atomar + Reuse-Detection, `RevokeAsync`, `RevokeAllForDeviceAsync`; `Auth:RefreshTokenTtlDays` Default 30) | Offen | — |
| 10 | Logik | `DeviceTokenService`: `IRefreshTokenService` injizieren; `RevokeAsync` → `RevokeAllForDeviceAsync` | Offen | — |
| 11 | Logik | `PairingService.ExchangeAsync` + `GetActiveCodesAsync`: `Kind == AdminCode`-Filter | Offen | — |
| 12 | Vertrag / DTOs | `PairingBootstrapRequest` (`ticket`, `clientPublicKey`, `deviceName`), `PairingBootstrapResponse` (`serverPublicKey`, `encryptedPayload`), `PairingBootstrapPayload` (`deviceToken`, `token`, `expires`, `refreshToken`) | Offen | — |
| 13 | Vertrag / DTOs | `RefreshTokenRequest` (`refreshToken`), `RefreshTokenResponse` (`token`, `expires`, `refreshToken`) | Offen | — |
| 14 | Logik | `PairingCrypto`-Helfer (ECDH-Import + AES-256-GCM) in `Services/Security/`; `PairingService` darauf umstellen | Offen | — |
| 15 | Logik | `IPairingBootstrapService`/`PairingBootstrapService`: `CreateBootstrapTicketAsync` (Admin-Only, Rate-Limit `Pairing:BootstrapMaxTicketsPerHour` Default 10, TTL `Pairing:BootstrapTicketTtlMinutes` Default 5, Ticket Base64url(24B) + 8-Zeichen-Alias, beide Hashes) | Offen | — |
| 16 | Logik | `PairingBootstrapService.BootstrapAsync` (Validierung → `InvalidRequest`; Lookup `TicketHash`/`CodeHash` + `Kind`; atomarer Verbrauch; `UserManager`-Prüfung; Geräte-Token + JWT + Refresh-Token → verschlüsselter Payload) | Offen | — |
| 17 | Logik | `PairingController.Bootstrap` (`POST api/pairing/bootstrap`, öffentlich, IP-Block → 429, 400/401-Mapping) | Offen | — |
| 18 | Logik | `AuthController.Refresh` (`POST api/auth/refresh`, `MauiOnly`-Gate, Rotation → `RefreshTokenResponse`, 401) und `AuthController.Logout` (`POST api/auth/logout`, `MauiOnly`-Gate, idempotent 200) | Offen | — |
| 19 | Logik | DI: `IPairingBootstrapService`, `IRefreshTokenService` scoped registrieren | Offen | — |
| 20 | UI | `Components/Account/Pages/Manage/Devices.razor` (SSR-Formular, „Gerät koppeln" → QR-Data-URL + Kurzcode + Anleitung + „Gültig bis"; Loopback-Warnhinweis; Forbidden/RateLimited-Meldungen) | Offen | — |
| 21 | UI | `ManageNavMenu.razor`: Eintrag „Geräte" | Offen | — |
| 22 | Tests | `PairingTestDb`/`DeviceTokenServiceTests` an neue Signaturen anpassen (`IssuedDeviceToken`, `IRefreshTokenService`-Abhängigkeit) | Offen | — |
| 23 | Tests | `PairingServiceTests_BootstrapTicket` (Erzeugung, TTL-Config, Rate-Limit, Admin-Only) | Offen | — |
| 24 | Tests | `PairingServiceTests_Bootstrap` (Happy-Path-Entschlüsselung, Kurzcode-Alias, Kind-Grenzen beide Richtungen, TTL/Verbrauch/Race, 400-Pfade, gelöschter Benutzer) | Offen | — |
| 25 | Tests | `RefreshTokenServiceTests` (Issue, Rotation, Reuse-Detection, Gerätewiderruf, `RevokeAsync`/`RevokeAllForDeviceAsync`) | Offen | — |
| 26 | Tests | `PairingBootstrapContractTests` (öffentlich, 200-Feldnamen Roh-JSON, 400/401/429, Trio→Login+Refresh) | Offen | — |
| 27 | Tests | `AuthRefreshContractTests` (Gate-Pflicht, Rotation via HTTP, Reuse → 401, Logout → danach 401, Logout unbekannt → 200) | Offen | — |
| 28 | E2E-Tests | `ProfilePairingE2ETests`: Ticket per UI erzeugen → QR/Kurzcode/Gültigkeit sichtbar | Offen | — |
| 29 | E2E-Tests | `ProfilePairingE2ETests`: Gesamtfluss UI-Ticket → bootstrap → Trio → Login + Refresh | Offen | — |
| 30 | E2E-Tests | `ProfilePairingE2ETests`: Rate-Limit-Meldung auf der Profilseite | Offen | — |
| 31 | Dokumentation | `docs/API.md`: `POST /api/pairing/bootstrap` (Feldnamen, Krypto, Payload, Fehlercodes), `POST /api/auth/refresh`, `POST /api/auth/logout`, Widerruf-Verhalten | Offen | — |
| 32 | Dokumentation | `ApiDocumentationContractTests.ApiDocumentationContainsMauiRelevantRoutes` um die drei Routen erweitern | Offen | — |
| 33 | Dokumentation | `docs/help/geraete/` Benutzeranleitung „Gerät per QR koppeln"; `docs/help/einrichtung.md` ggf. | Offen | — |
| 34 | Dokumentation | `README.md`, `docs/RELEASE_NOTES.md`, `docs/GUIDE_Installation.md` (neue `Pairing:Bootstrap*`-/`Auth:RefreshTokenTtlDays`-Konfiguration) | Offen | — |
