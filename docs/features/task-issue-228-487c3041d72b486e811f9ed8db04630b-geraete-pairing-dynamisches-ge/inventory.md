# Bestandsaufnahme: Geräte-Pairing mit dynamischem Geräte-Token

Analysiert wurden die ASP.NET-Core-Server-Komponente `VideoWebPlayer`, das geteilte Client-Projekt `VideoWebPlayer.Client` und die Testsuite `VideoWebPlayer.Tests` bezogen auf die Anforderung in [`requirement.md`](requirement.md) (Issue #228: Umstellung des `X-API-Key`-Gates von einem statischen `Jwt:ApiToken:Maui`-Token auf individuelle, widerrufbare Geräte-Tokens mit Einmal-Pairing-Code und `POST api/pairing/exchange`).

## Zusammenfassung

- **Pairing-/Geräte-Funktionalität existiert noch nicht.** Keine Entitäten `PairedDevice`/`PairingCode`, kein `PairingController`, keine Pairing-DTOs, keine `Pairing`-Konfigurationssektion, kein `Microsoft.AspNetCore.RateLimiting`.
- **Das zu ändernde Gate ist klein und klar abgegrenzt:** `ApiTokenCheckAttribute` (`VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs`) prüft `X-API-Key` ausschließlich gegen `IConfiguration`-Werte; `MauiOnly` ist nur auf `AuthController.Login` (`POST api/auth/login`) gesetzt. Für DB-Geräte-Tokens muss das Attribut zusätzliche Dienste aus `RequestServices` beziehen.
- **Infrastruktur-Muster sind vorhanden:** EF-Core-Konventionen (`IEntityTypeConfiguration<T>` + `ApplyConfigurationsFromAssembly`, `*AtUtc`-Timestamps, automatische Migration via `MigrateDatabase()`), Bruteforce-/Sperr-Muster `ILoginIpBlockService`/`LoginIpBlockService` (Singleton + `ConcurrentDictionary` + `BlockedLoginIps`-Tabelle), Admin-UI-Muster (`Security.razor` + Kachel in `AdminIndex.razor`), HostedService-Muster (`MediaSourceScanService`, `ContinueWatchingWorker`), DTO-Konvention in `VideoWebPlayer.Client/Models/`.
- **Zu beachtende Stolpersteine:** Produktions-Pflichtcheck auf `Jwt:ApiToken:Maui` in `ServiceCollectionExtensions.cs` Zeile 58; `SECRETS_MANAGEMENT.md` führt den Maui-Token aktuell gar nicht; `AuthController.ImpersonateRequest` existiert doppelt (globale Klasse in `AuthController.cs` und `VideoWebPlayer.Client/Models`); die Gate-Attribute liegen im globalen Namespace; der `Integration`-Testfilter matcht keinen Test.
- **Test-Ausgangszustand:** Alle 356 ausgeführten Tests erfolgreich (303 Unit/Nicht-E2E + 53 E2E), 0 fehlgeschlagen; `Category=Integration` matcht 0 Tests. Nachweis: [inventory/tests.md](inventory/tests.md) mit TRX-Reports und Konsolenlogs unter `inventory/test-results/`.

## Details

- [Datenmodell](inventory/models.md) — `ApplicationDbContext` (DbSet-Region, `OnModelCreating`), Stilvorbilder `BlockedLoginIp`/`UpdateSettings`/`Setup`, `ApplicationUser`, vorhandene Client-DTOs
- [Logik, Services und Controller](inventory/logic.md) — `ApiTokenCheckAttribute`, `BearerTokenCheckAttribute`, `ConnectionCheckAttribute`, `AuthController`, `ApiBaseController`, `HealthController`, `LoginIpBlockService`, `InternalConnectionService`, `WhitelistIpMiddleware`, `UdpDiscoveryListener`, `AuthService`/`AuthorizationTokenService`, `VideoWebPlayerClient`, `ServiceCollectionExtensions`, `WebApplicationExtensions`, `Program.cs`, HostedService-/Event-Muster
- [Enums](inventory/enums.md) — `ApiTokenScope` (`AnyClient`, `MauiOnly`)
- [Interfaces](inventory/interfaces.md) — `ILoginIpBlockService`, `IAuthService`, Interface-Konventionen
- [Admin-UI](inventory/ui.md) — `Security.razor`, `AdminIndex.razor`, `NavMenu.razor` (Security-Badge)
- [Konfiguration und Secrets](inventory/configuration.md) — `Jwt:*`-Schlüssel, Produktions-Pflichtcheck, fehlende `Pairing`-Sektion, kein Rate-Limiting-Paket
- [Bestehende Dokumentation](inventory/documentation.md) — `docs/API.md`, `SECRETS_MANAGEMENT.md`, `GUIDE_Installation.md`, `docs/help/`, `issue.md`
- [Tests](inventory/tests.md) — Test-Ausgangszustand (356/356 bestanden), anforderungsrelevante Testklassen, Hilfsmethoden, Testlücken
