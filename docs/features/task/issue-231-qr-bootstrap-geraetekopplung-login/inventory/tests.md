# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-09-24 ~12:20–12:25 CEST (+02:00)
- Branch und Commit-ID: `task/issue-231-qr-bootstrap-geraetekopplung-login` @ `a7c2d17a889a1b682816aa545044af52a7ac2e07`
- Uncommittete Änderungen im getesteten Stand: nur neue Artefakte unter `docs/features/task/issue-231-qr-bootstrap-geraetekopplung-login/` (kein Produktivcode/Test-Code geändert)
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK `10.0.401`, `dotnet-ef` 10.0.10, xUnit.v3 + Microsoft.Playwright 1.49.0 (Chromium vorhanden — E2E lief)
- Ermittelte Testsuiten und Quellen der Testbefehle: `VideoWebPlayer.Tests` (einzige Test-Suite; Befehl aus `run-tests`-Konvention `dotnet test`), E2E-Kategorie via `Trait("Category","E2E")`

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build --logger "console;verbosity=normal"` (nach `dotnet build VideoPlayer.sln -c Release`, 0 Fehler) | Repo-Root | 0 | 407 | 0 | 0 | [baseline-dotnet-test.log](test-results/baseline-dotnet-test.log) |

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf war vollständig erfolgreich (407/407).

### Testlücken und Ausführungsprobleme

Keine. Alle Tests inkl. der Playwright-E2E-Suite liefen in dieser Umgebung durch.

## Testklassen (relevant für diese Anforderung)

### `PairingServiceTests_CodeCreation` / `_Exchange` / `_ActiveCodes`
- `CreatePairingCodeAsync_ReturnsCodeAndPersistsHash` — Länge/Alphabet/Hash/`CreatedByUserId`/TTL
- `CreatePairingCodeAsync_ConfiguredCodeLength*` — Config-Klemmung
- `ExchangeAsync_ValidRequest_ReturnsDecryptableTokenAndPersistsDevice` — ECDH-Roundtrip via `PairingCryptoHelper.DecryptToken`
- `ExchangeAsync_UnknownCode_Fails`, `_ExpiredCode_Fails`, `_AlreadyConsumed_Fails`, `_ConcurrentConsume_OnlyOneSucceeds`, `_ExplicitParametersPublicKey_Fails`, `_DeviceNameWithPaddingWithinTrimmedLimit_Succeeds`
- `GetActiveCodesAsync_*` — nur aktive, keine Secrets

### `DeviceTokenServiceTests`
- Issue/Hash, Validierung inkl. `LastUsedAtUtc`, Widerruf, Rename, Auflistung, Fallback-Namen

### `PairingExchangeContractTests_Flow` / `_Validation` / `_JsonContract`
- Öffentlicher Endpunkt, 401/429/400-Mapping, verbindliche camelCase-Feldnamen via Roh-JSON, entschlüsseltes Token → `POST /api/auth/login` → 200

### `DevicePairingE2ETests` (Playwright, `Trait Category=E2E`)
- Admin-Code-UI, Gesamtfluss Code→Exchange→Login, Widerruf, IP-Sperre sichtbar in `/admin/security`, Non-Admin-Abweisung

### `ApiDocumentationContractTests`
- `ApiDocumentationContainsMauiRelevantRoutes` — pinnt Routenliste gegen `docs/API.md` (neue Routen müssen dort ergänzt werden: `POST /api/pairing/bootstrap`, `POST /api/auth/refresh`, `POST /api/auth/logout`)

### `LoginIpBlockServiceTests`, `ApiTokenConfigurationTests`
- Sperr-/Unblock-Semantik, Geräte-Token-Gate (`MauiOnly`)

## Hilfsmethoden

### `PairingWebApplicationFactory` (`Helpers/`)
- `CreateTempDbPath(prefix)`, `Create(dbPath, configure?)` — Temp-SQLite, generierter `Jwt:Key`, Test-ApiTokens, `HttpsPort=null`, Environment `Testing`

### `PairingTestDb` (`Helpers/`)
- `CreateAsync(dbName, ct)` — shared In-Memory-SQLite + `ApplicationDbContext` (EnsureCreated) + `ServiceProvider`; `CreatePairingService(db)` — Service mit leerer `IConfiguration`; `CreateScope()`

### `PairingCryptoHelper` (`Helpers/`)
- `DecryptToken(clientKey, serverPublicKeyBase64, encryptedTokenBase64)` — Client-Gegenstück des ECDH/AES-GCM-Kanals; **Ciphertext-Layout muss für `bootstrap` identisch bleiben**, damit dieselbe Routine greift

### `PairingExchangeContractTestBase` (`Helpers/`)
- `SendExchangeAsync(rawJson, remoteIp)` via `Factory.Server.SendAsync`, `CreatePairingCodeAsync` via DI-Scope, `CreateUserAsync(password)`, `UnblockIp(ip)` — wiederverwendbares Muster für Bootstrap-Contract-Tests
