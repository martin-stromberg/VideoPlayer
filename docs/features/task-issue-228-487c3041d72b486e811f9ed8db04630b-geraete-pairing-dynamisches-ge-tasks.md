# Tasks: Geräte-Pairing mit dynamischem Geräte-Token

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Entität `PairedDevice` anlegen (`VideoWebPlayer/Data/PairedDevice.cs`: `Id`, `Name`, `TokenHash`, `IssuedAtUtc`, `LastUsedAtUtc`, `RevokedAtUtc`, `CreatedByUserId`) | Offen | — |
| 2 | Datenmodell | Entität `PairingCode` anlegen (`VideoWebPlayer/Data/PairingCode.cs`: `Id`, `CodeHash`, `CreatedAtUtc`, `ExpiresAtUtc`, `ConsumedAtUtc`, `CreatedByUserId`) | Offen | — |
| 3 | Datenmodell | `PairedDeviceConfiguration` anlegen (`IEntityTypeConfiguration<PairedDevice>`: Unique-Index `TokenHash`, Index `RevokedAtUtc`, `HasMaxLength`) | Offen | — |
| 4 | Datenmodell | `PairingCodeConfiguration` anlegen (`IEntityTypeConfiguration<PairingCode>`: Index `CodeHash`, Index `ExpiresAtUtc`, `HasMaxLength`) | Offen | — |
| 5 | Datenmodell | `DbSet<PairedDevice>` und `DbSet<PairingCode>` in `ApplicationDbContext` ergänzen | Offen | — |
| 6 | Datenmodell | EF-Migration `AddDevicePairing` erzeugen (`dotnet ef migrations add`, `VideoWebPlayer/Migrations/`) | Offen | — |
| 7 | Vertrag / DTOs | `PairingExchangeRequest` in `VideoWebPlayer.Client/Models/` anlegen (`Code`, `ClientPublicKey`, `DeviceName?`; verbindliche JSON-Feldnamen camelCase: `code`, `clientPublicKey`, `deviceName`) | Offen | — |
| 8 | Vertrag / DTOs | `PairingExchangeResponse` in `VideoWebPlayer.Client/Models/` anlegen (`ServerPublicKey`, `EncryptedToken`; verbindliche JSON-Feldnamen: `serverPublicKey`, `encryptedToken`) | Offen | — |
| 9 | Logik | `IDeviceTokenService`/`DeviceTokenService` implementieren (`IssueAsync`, `IsValidDeviceTokenAsync` mit `LastUsedAtUtc`-Update, `RevokeAsync`, `GetDevicesAsync`; SHA-256-`TokenHash`, Base64Url-Zufallstoken) | Offen | — |
| 10 | Logik | `IPairingService`/`PairingService` implementieren (`CreatePairingCodeAsync`, `ExchangeAsync` mit atomarem Code-Verbrauch, ECDH P-256/SPKI, AES-256-GCM, `GetActiveCodesAsync`; `PairingExchangeResult`) | Offen | — |
| 11 | Konfiguration | `Pairing:CodeLength` (Default 8) und `Pairing:CodeTtlMinutes` (Default 5) via `IConfiguration` im `PairingService` lesen; Code-Alphabet als Konstante | Offen | — |
| 12 | Logik | Services in `ServiceCollectionExtensions.AddVideoWebPlayerServices` registrieren (`AddScoped<IDeviceTokenService, DeviceTokenService>()`, `AddScoped<IPairingService, PairingService>()`) | Offen | — |
| 13 | Logik | `PairingController` anlegen (`POST api/pairing/exchange`, keine Gate-Attribute, `ILoginIpBlockService`-Prüfung → 429, Validierung → 400/401, Response-Mapping) | Offen | — |
| 14 | Logik | `ApiTokenCheckAttribute` auf `OnActionExecutionAsync` umstellen und bei `MauiOnly` Geräte-Token-Prüfung über `IDeviceTokenService` aus `RequestServices` ergänzen (`AnyClient` unverändert) | Offen | — |
| 15 | UI | Admin-Seite `Devices.razor` erstellen (`/admin/devices`, `InteractiveServer`, `IsAdmin`-Check nach `Security.razor`-Muster, `admin-*`-CSS) | Offen | — |
| 16 | UI | `Devices.razor`: Pairing-Code-Erzeugung mit TTL-Anzeige und Liste aktiver Codes | Offen | — |
| 17 | UI | `Devices.razor`: Geräteliste (`Name`, `IssuedAtUtc`, `LastUsedAtUtc`, Status) mit Widerruf-Aktion | Offen | — |
| 18 | UI | Kachel „Geräte" (`/admin/devices`) in `AdminIndex.razor` ergänzen | Offen | — |
| 19 | Tests | `DeviceTokenServiceTests` anlegen (Issue/Hash, Validierung inkl. `LastUsedAtUtc`, Widerruf, Auflistung; In-Memory-SQLite nach `ApplicationDbContextTests`-Muster) | Offen | — |
| 20 | Tests | `PairingServiceTests` anlegen (Code-Erzeugung, Exchange-Erfolg mit ECDH-Roundtrip, unbekannter/abgelaufener/verbrauchter Code, Race beim Verbrauch, ungültiger PublicKey, PublicKey falscher Kurve ≠ `nistP256` (`ExchangeAsync_NonP256PublicKey_Fails`), leerer/fehlender `Code` → Formatfehler ohne Fehlversuchszählung (`ExchangeAsync_EmptyCode_Fails`), `DeviceName` > 200 Zeichen abgelehnt, `GetActiveCodesAsync` liefert nur aktive Codes als Metadaten ohne Hash/Klartext) | Offen | — |
| 21 | Tests | `ApiTokenConfigurationTests` erweitern (`MauiOnly` akzeptiert Geräte-Token, verworfenes Token → 401, Config-Fallback weiterhin gültig, `AnyClient` ohne DB-Zugriff) | Offen | — |
| 22 | Tests | `ApiTokenCheckAttribute_InvalidTokenLogDoesNotIncludeHeaderValue` auf `OnActionExecutionAsync`-Aufruf umstellen | Offen | — |
| 23 | Tests | `PairingExchangeContractTests` anlegen (`WebApplicationFactory<Program>`: öffentlicher Endpunkt ohne Auth, 401 bei ungültigem Code, 429 nach wiederholten Fehlversuchen, 400 bei `deviceName` > 200 Zeichen, 400 bei fehlendem/leerem `code` ohne Fehlversuchszählung/IP-Sperre (`Exchange_Returns400OnMissingOrEmptyCode`, 400-vs-401-Abgrenzung), 400 bei Client-PublicKey falscher Kurve, verbindliche camelCase-JSON-Feldnamen `code`/`clientPublicKey`/`deviceName`/`serverPublicKey`/`encryptedToken` via Roh-JSON geprüft (`Exchange_UsesBindingJsonFieldNames`), entschlüsseltes Token → `POST /api/auth/login` → 200) | Offen | — |
| 24 | Tests | `ApiDocumentationContractTests.ApiDocumentationContainsMauiRelevantRoutes` um `POST /api/pairing/exchange` erweitern | Offen | — |
| 25 | E2E-Tests | `DevicePairingE2ETests`: Admin-Login → `/admin/devices` → Pairing-Code erzeugen, Code + TTL sichtbar | Offen | — |
| 26 | E2E-Tests | `DevicePairingE2ETests`: Gesamtfluss Code aus UI → ECDH-Exchange per HttpClient → Login mit Geräte-Token erfolgreich | Offen | — |
| 27 | E2E-Tests | `DevicePairingE2ETests`: Widerruf in UI → Login mit Geräte-Token → 401 | Offen | — |
| 28 | E2E-Tests | `DevicePairingE2ETests`: ungültiger Code → Fehler; wiederholte Fehlversuche → IP-Sperre sichtbar in `/admin/security` (beide Teilnachweise im selben Test) | Offen | — |
| 29 | E2E-Tests | `DevicePairingE2ETests`: Benutzer ohne `IsAdmin`-Claim (Seeding-Muster `SeedRegularUserAsync` aus `BackupUploadE2ETests`) öffnet `/admin/devices` → „Nicht autorisiert.", keine Code-Erzeugung/Geräteliste/Widerruf-Aktionen gerendert | Offen | — |
| 30 | Dokumentation | `docs/API.md`: `POST /api/pairing/exchange` (inkl. verbindlicher JSON-Feldnamen und Krypto-Parameter), geänderte `X-API-Key`-Semantik (Geräte-Tokens + `Jwt:ApiToken:Maui`-Fallback) und Widerruf-Verhalten (ausgestellte Benutzer-JWTs bleiben bis zum Ablauf von 12 h gültig) dokumentieren | Offen | — |
| 31 | Dokumentation | `docs/GUIDE_Installation.md`: Abschnitt Geräte-Pairing inkl. `Pairing:`-Konfiguration ergänzen | Offen | — |
| 32 | Dokumentation | `docs/help/einrichtung.md` aktualisieren (Kachel „Geräte"); ggf. neue Hilfeseite zum Pairing-Ablauf inkl. Hinweis auf das Widerruf-Verhalten (JWT-Restlaufzeit 12 h) | Offen | — |
| 33 | Dokumentation | `docs/SECRETS_MANAGEMENT.md`: `Jwt:ApiToken:Maui` als Fallback zum Geräte-Token-Verfahren ergänzen | Offen | — |
