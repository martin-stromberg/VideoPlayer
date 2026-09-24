# Bestandsaufnahme: QR-Bootstrap — Gerätekopplung + Login per Einmal-Ticket

Analyse des Pairing-/Auth-/UI-Stacks des Servers bezogen auf Issue #231 (QR-Bootstrap aus der Profilseite). Ergänzend liegt die verifizierte Fremd-Inventur aus dem App-Repo vor (`…/inventory/qr-bootstrap-server.md`); die dort genannten Fundstellen wurden gegen den aktuellen Code verifiziert und stimmen überein.

## Zusammenfassung

- **Pairing-Stack vollständig vorhanden:** `PairingCode` (CodeHash, TTL, Single-Use, `CreatedByUserId`), `PairingService` mit atomarem Verbrauch via `ExecuteUpdateAsync`, ECDH-P-256 + `DeriveKeyFromHmac` + AES-256-GCM, öffentlicher `PairingController` (`api/pairing/exchange`) mit `ILoginIpBlockService`-Schutz (429 ab 5 Fehlversuchen).
- **Fehlt für #231:** kein `Kind`/Richtungs-Feld am `PairingCode`, kein längeres Ticket-Secret (nur 8-Zeichen-Code), kein `api/pairing/bootstrap`, kein Refresh-Token (JWT läuft starr 12 h), keine Refresh-Endpunkte, kein Rate-Limit je Benutzer, keine benutzerseitige „Gerät koppeln"-Seite (nur Admin-Seite `/admin/devices`), kein QR-Paket.
- **Profilbereich existiert als SSR-Gerüst:** `Components/Account/Pages/Manage/*` (statisch gerenderte Identity-Scaffold-Seiten mit EditForm/`method="post"`, `ManageLayout` + `ManageNavMenu`); `AccountLayout` erzwingt bei fehlendem `HttpContext` einen Full-Reload → **keine `@rendermode InteractiveServer`-Seiten unter `Account/` möglich** (Reload-Schleife). Neue Profilseite muss dem SSR-Formularmuster folgen oder eine interaktive Insel einbetten.
- **JWT-Ausstellung ohne Passwort ist vorhanden:** `AuthorizationTokenService.CreateToken(user)` — Muster „Token ohne Passwort" wird von `ImpersonateAsync` gezeigt; für Bootstrap muss der `UserManager.FindByIdAsync(pairingCode.CreatedByUserId)`-Weg ergänzt werden.
- **Test-Infrastruktur vollständig:** `PairingWebApplicationFactory` (Temp-SQLite + Test-Settings), `PairingTestDb` (In-Memory-SQLite-Service-Tests), `PairingCryptoHelper` (Client-seitige Entschlüsselung), Playwright-E2E-Muster (`DevicePairingE2ETests`).
- **Test-Ausgangszustand:** `dotnet test` Release — **407/407 bestanden, 0 fehlgeschlagen, 0 übersprungen** (Nachweis: [tests.md](inventory/tests.md), Log `inventory/test-results/baseline-dotnet-test.log`).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [UI und Navigation](inventory/ui.md)
- [Tests](inventory/tests.md)

## Abgeleitete Arbeitspunkte (keine Planung, nur Befund)

| Befund | Konsequenz |
|--------|------------|
| `ExchangeAsync` sucht nur per `CodeHash` — ein Bootstrap-Kurzcode-Alias würde dort ebenfalls matchen | `Kind`-Filter im Lookup nötig, damit `exchange` vertraglich unverändert bleibt |
| `PairingService` hat keinen Zugriff auf `UserManager`/`AuthorizationTokenService` | Bootstrap-Pfad braucht neue Abhängigkeiten (beide scoped, kompatibel) |
| `DeviceTokenService.RevokeAsync` kennt keine Refresh-Tokens | Widerruf-Verknüpfung herstellen (Device → RefreshTokens) |
| `AccountLayout` forciert Reload bei `HttpContext == null` | Profilseite als SSR-Formularseite (Muster `Manage/Index.razor`) statt `InteractiveServer` |
| `Request.Host`-Vertrauen | QR-Adresse aus `Scheme`+`Host`; Loopback-Erkennung + Hinweis nötig |
| Kein QR-Paket | `QRCoder` 1.8.0 (MIT, 2026-04-04, keine Abhängigkeiten, `PngByteQRCode` ohne System.Drawing) geeignet |
