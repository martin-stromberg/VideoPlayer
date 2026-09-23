# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Neue Klassen / Objekte

- [x] `PairedDevice` (Datenmodellklasse) — angelegt in `VideoWebPlayer/Data/PairedDevice.cs` mit allen Feldern: `Id` (int), `Name` (string), `TokenHash` (string), `IssuedAtUtc` (DateTime), `LastUsedAtUtc` (DateTime?), `RevokedAtUtc` (DateTime?), `CreatedByUserId` (string?)
- [x] `PairingCode` (Datenmodellklasse) — angelegt in `VideoWebPlayer/Data/PairingCode.cs` mit allen Feldern: `Id`, `CodeHash`, `CreatedAtUtc`, `ExpiresAtUtc`, `ConsumedAtUtc` (DateTime?), `CreatedByUserId` (string?)
- [x] `PairedDeviceConfiguration` (`IEntityTypeConfiguration<PairedDevice>`) — angelegt in `VideoWebPlayer/Data/Configurations/PairedDeviceConfiguration.cs`: Schlüssel `Id`, `HasMaxLength` 200/128/64, Unique-Index `TokenHash`, Index `RevokedAtUtc`
- [x] `PairingCodeConfiguration` (`IEntityTypeConfiguration<PairingCode>`) — angelegt in `VideoWebPlayer/Data/Configurations/PairingCodeConfiguration.cs`: Schlüssel `Id`, `HasMaxLength` 128/64, Index `CodeHash`, Index `ExpiresAtUtc`
- [x] `IDeviceTokenService` (Interface) — vorhanden in `VideoWebPlayer/Services/DeviceTokenService.cs` mit `IssueAsync`, `IsValidDeviceTokenAsync`, `RevokeAsync`, `GetDevicesAsync`
- [x] `DeviceTokenService` (`internal sealed`, scoped) — implementiert: Base64Url-Zufallstoken (32 Byte), SHA-256-`TokenHash`, `LastUsedAtUtc`-Update, `RevokedAtUtc`-Widerruf, `ApplicationDbContext`-Zugriff
- [x] `IPairingService` (Interface) — vorhanden in `VideoWebPlayer/Services/PairingService.cs` mit `CreatePairingCodeAsync`, `ExchangeAsync`, `GetActiveCodesAsync`
- [x] `PairingService` (`internal sealed`, scoped) — implementiert: Code-Generierung (Alphabet ohne `0/O/1/I/L`, `RandomNumberGenerator`, `Pairing:CodeLength`/`CodeTtlMinutes` via `IConfiguration`), SHA-256-`CodeHash`, atomarer Verbrauch via `ExecuteUpdateAsync`, ECDH `nistP256`/SPKI, `DeriveKeyFromHmac` (SHA-256), AES-256-GCM mit 12-Byte-Nonce (Base64 `nonce‖ciphertext‖tag`)
- [x] `PairingExchangeResult` (Ergebnisobjekt) — vorhanden in `VideoWebPlayer/Services/PairingService.cs` inkl. `PairingExchangeErrorKind` (`InvalidRequest` → 400, `InvalidCode` → 401)
- [x] `PairingController` — angelegt in `VideoWebPlayer/Controllers/PairingController.cs`: `[ApiController]`, Route `api/pairing`, `POST exchange`, erbt `ApiBaseController`, keine Gate-Attribute, `ILoginIpBlockService.IsBlocked` → 429, `RegisterFailure`/`RegisterSuccess`
- [x] `PairingExchangeRequest` (DTO) — angelegt in `VideoWebPlayer.Client/Models/PairingExchangeRequest.cs` (`Code`, `ClientPublicKey`, `DeviceName?`)
- [x] `PairingExchangeResponse` (DTO) — angelegt in `VideoWebPlayer.Client/Models/PairingExchangeResponse.cs` (`ServerPublicKey`, `EncryptedToken`)
- [x] `Devices.razor` (Razor-Komponente) — angelegt in `VideoWebPlayer/Components/Pages/Admin/Devices.razor`: `/admin/devices`, `InteractiveServer`, `IsAdmin`-Claim-Prüfung („Nicht autorisiert."), Code-Erzeugung mit TTL-Anzeige, Liste aktiver Codes (nur Metadaten), Geräteliste mit Status und Widerruf-Aktion, `admin-*`-CSS

### Änderungen an bestehenden Klassen

- [x] `ApplicationDbContext` — `DbSet<PairedDevice> PairedDevices` (Zeile 157) und `DbSet<PairingCode> PairingCodes` (Zeile 161) ergänzt; `OnModelCreating` unverändert
- [x] `ApiTokenCheckAttribute` — auf `OnActionExecutionAsync` umgestellt (`VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs`, Zeile 28): synchroner Config-Token-Pfad zuerst, bei `MauiOnly` + Nicht-Match `IDeviceTokenService` via `RequestServices.GetService` (fehlender Service → `Unauthorized`), `AnyClient` ohne DB-Zugriff, Logs ohne Token-Wert; `ApiTokenScope` unverändert
- [x] `ServiceCollectionExtensions` — `AddScoped<IDeviceTokenService, DeviceTokenService>()` und `AddScoped<IPairingService, PairingService>()` (Zeilen 77–78); Produktions-Pflichtcheck `Jwt:ApiToken:Maui` unverändert (Zeilen 50–58)
- [x] `AdminIndex.razor` — Kachel „Geraete" für `/admin/devices` eingereiht (Zeilen 56–60)
- [x] `AuthController` — keine Änderung nötig; `[ApiTokenCheck(ApiTokenScope.MauiOnly)]` auf `Login` besteht

### Datenbankmigration

- [x] Migration `AddDevicePairing` — vorhanden (`VideoWebPlayer/Migrations/20260923074815_AddDevicePairing.cs` + Designer): Tabellen `PairedDevices`/`PairingCodes` mit allen Spalten und Indizes (`IX_PairedDevices_TokenHash` unique, `IX_PairedDevices_RevokedAtUtc`, `IX_PairingCodes_CodeHash`, `IX_PairingCodes_ExpiresAtUtc`)

### Tests

- [x] `DeviceTokenServiceTests` — 6 Tests (`IssueAsync_StoresHashNotPlaintext`, `IssueAsync_EmptyDeviceName_UsesDefaultName`, `IsValidDeviceTokenAsync_ValidToken_SucceedsAndUpdatesLastUsed`, `..._RevokedToken_Fails`, `RevokeAsync_SetsRevokedAtUtc`, `GetDevicesAsync_ReturnsAll`)
- [x] `PairingServiceTests` — 11 Tests aufgeteilt in `PairingServiceTests_CodeCreation` (Code-Erzeugung/Hash/TTL), `PairingServiceTests_Exchange` (Erfolg mit ECDH-Roundtrip, unbekannter/abgelaufener/verbrauchter Code, `ConcurrentConsume_OnlyOneSucceeds`, `InvalidPublicKey`, `NonP256PublicKey`, `EmptyCode`, `DeviceNameTooLong`) und `PairingServiceTests_ActiveCodes` (nur aktive Metadaten)
- [x] `ApiTokenConfigurationTests` erweitert — `MauiOnly_AcceptsDeviceToken`, `MauiOnly_RejectsRevokedDeviceToken`, `MauiOnly_MauiConfigTokenStillAccepted`, `AnyClient_DoesNotTouchDeviceTokens` (mit `RecordingDeviceTokenService`, Aufrufzähler = 0)
- [x] `ApiTokenCheckAttribute_InvalidTokenLogDoesNotIncludeHeaderValue` — auf `OnActionExecutionAsync` umgestellt (Zeile 73)
- [x] `PairingExchangeContractTests` — 8 Tests in `..._Flow` (200 ohne Auth, 401, 429, entschlüsseltes Token → Login 200), `..._Validation` (400 bei fehlendem/leerem `code`, `deviceName` > 200, falscher Kurve) und `..._JsonContract` (verbindliche camelCase-Feldnamen via Roh-JSON)
- [x] `ApiDocumentationContractTests.ApiDocumentationContainsMauiRelevantRoutes` — `POST /api/pairing/exchange` in Pflicht-Routenliste (Zeile 54)
- [x] `DevicePairingE2ETests` — 5 Pflicht-E2E-Tests (`Admin_Creates_Pairing_Code_And_Sees_Code_With_Ttl`, `Pairing_Flow_UiCode_Exchange_Login_Succeeds`, `Revoked_Device_Token_Login_Fails`, `Invalid_Codes_Block_Ip_And_Show_In_Security`, `NonAdmin_Sees_NotAuthorized_On_Devices_Page`)

### Dokumentation

- [x] `docs/API.md` — Abschnitt „Pairing" mit `POST /api/pairing/exchange`, verbindlichen JSON-Feldnamen, Krypto-Parametern (ECDH P-256, AES-256-GCM, `nonce‖ciphertext‖tag`), Fehlercodes 400/401/429, geänderter `X-API-Key`-Semantik (Geräte-Tokens + `Jwt:ApiToken:Maui`-Fallback) und Widerruf-Verhalten (JWT-Restlaufzeit 12 h)
- [x] `docs/GUIDE_Installation.md` — Abschnitt „Geräte-Pairing" mit Admin-Ablauf und `Pairing:CodeLength`/`Pairing:CodeTtlMinutes`-Konfiguration
- [x] `docs/help/einrichtung.md` — Kachel „Geräte" + Hilfeabschnitt zum Pairing-Ablauf inkl. Widerruf-/JWT-12h-Hinweis
- [x] `docs/SECRETS_MANAGEMENT.md` — `Jwt:ApiToken:Maui` als Fallback zum Geräte-Token-Verfahren dokumentiert

## Hinweise

- Die geplanten Testklassen `PairingServiceTests`/`PairingExchangeContractTests` wurden jeweils auf drei Dateien aufgeteilt (`..._CodeCreation`/`_Exchange`/`_ActiveCodes` bzw. `..._Flow`/`_Validation`/`_JsonContract`); alle geplanten Testmethoden sind vorhanden — keine Lücke, nur Aufteilung.
- `dotnet build VideoPlayer.sln -c Release`: 0 Warnungen, 0 Fehler.
- Bewusste Nicht-Umsetzung laut Plan: kein Aufräum-HostedService für abgelaufene Pairing-Codes (explizit als optional entschieden), kein serverseitiger JWT-Widerruf.
