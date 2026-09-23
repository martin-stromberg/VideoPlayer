# Übersetzte Anforderung: Geräte-Pairing mit dynamischem Geräte-Token

## Fachliche Zusammenfassung

Das Client-Gate `X-API-Key` wird von einem statischen Konfigurationstoken (`Jwt:ApiToken:Maui`) auf individuelle, widerrufbare Geräte-Tokens umgestellt. Ein Administrator erzeugt in der Web-UI einen kurzlebigen Einmal-Pairing-Code; die Client-App löst diesen Code über einen neuen öffentlichen Endpunkt `POST api/pairing/exchange` gegen ein Geräte-Token ein. Der Token-Austausch wird wegen des HTTP-Betriebs ohne TLS auf Anwendungsebene per ECDH-Schlüsselaustausch plus symmetrischer Verschlüsselung geschützt. `ApiTokenCheckAttribute` akzeptiert im Scope `ApiTokenScope.MauiOnly` zusätzlich die in der Datenbank gespeicherten, nicht widerrufenen Geräte-Tokens.

## Betroffene Klassen und Komponenten

### Datenmodell (neu, `VideoWebPlayer/Data/`)

- `PairedDevice`: Entität für gekoppelte Geräte mit (mindestens) `Id`, `Name`, `TokenHash` (Ableitung: das Geräte-Token sollte gehasht und nicht im Klartext gespeichert werden), `IssuedAtUtc`, `LastUsedAtUtc`, `RevokedAtUtc` o. ä. für den Widerruf; Benennung und Feldliste folgen dem Projektstil (`*AtUtc`-Timestamps, vgl. `BlockedLoginIp`, `UpdateSettings`).
- `PairingCode`: Entität für Einmal-Pairing-Codes mit `Code` (bzw. Hash), `ExpiresAtUtc`, `ConsumedAtUtc`/`UsedAtUtc` für Einmalverwendung, ggf. `CreatedByUserId` des ausstellenden Administrators.
- `PairedDeviceConfiguration`, `PairingCodeConfiguration`: `IEntityTypeConfiguration<T>`-Klassen unter `VideoWebPlayer/Data/Configurations/` (Konvention wie `BlockedLoginIpConfiguration`).
- `ApplicationDbContext`: neue `DbSet<PairedDevice>` und `DbSet<PairingCode>` (`VideoWebPlayer/Data/ApplicationDbContext.cs`, DbSet-Region ca. Zeile 29–154).
- Neue EF-Core-Migration unter `VideoWebPlayer/Migrations/` (wird beim Start über `MigrateDatabase()` in `WebApplicationExtensions.cs` automatisch angewendet).

### Logikklassen / Services (neu, `VideoWebPlayer/Services/`)

- Pairing-Service (z. B. `IPairingService` / `PairingService`): Erzeugung kryptographisch zufälliger Pairing-Codes, Validierung (Ablauf, Einmalverwendung), Austausch gegen Geräte-Token, ECDH-Ableitung und Verschlüsselung des Tokens (`System.Security.Cryptography`).
- Geräte-Token-Service (z. B. `IDeviceTokenService` / `DeviceTokenService` oder Teil des Pairing-Service): Token-Erzeugung, Validierung für `ApiTokenCheckAttribute`, `LastUsedAtUtc`-Pflege, Widerruf, Auflistung für die Admin-UI.
- Rate-Limiting für den öffentlichen Endpunkt: Wiederverwendung des vorhandenen Musters `ILoginIpBlockService`/`LoginIpBlockService` (`VideoWebPlayer/Services/LoginIpBlockService.cs`, Singleton mit `ConcurrentDictionary` + Persistenz) oder Einführung von `Microsoft.AspNetCore.RateLimiting` — derzeit im Projekt nicht vorhanden (Annahme: Entscheidung in der Planung).
- Registrierung in `ServiceCollectionExtensions.AddVideoWebPlayerServices` (`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`).

### Interfaces / Enums

- Neue Service-Interfaces nach Projektkonvention (`I`-Präfix, vgl. `IFavoritesService`, `ILoginIpBlockService`).
- `ApiTokenScope` (`VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs`, Zeile 91–102) bleibt voraussichtlich unverändert; Geräte-Tokens gelten für `MauiOnly`.

### Controller / UI

- Neuer `PairingController` (`VideoWebPlayer/Controllers/`): `POST api/pairing/exchange` ohne `ApiTokenCheck` und ohne `BearerTokenCheck`, dafür rate-limited. Request: Pairing-Code + ECDH-PublicKey des Clients; Response: serverseitiger ECDH-PublicKey + verschlüsseltes Geräte-Token.
- Neue DTOs unter `VideoWebPlayer.Client/Models/` (z. B. `PairingExchangeRequest`, `PairingExchangeResponse`), damit der Vertrag zwischen Server- und Client-Repository geteilt werden kann.
- `ApiTokenCheckAttribute` (`ApiTokenCheckAttribute.cs`): Erweiterung um datenbankbasierte Geräte-Token-Prüfung für `MauiOnly`; das Attribut löst bisher nur `IConfiguration` auf und muss zusätzlich einen Service/`ApplicationDbContext` aus `RequestServices` beziehen.
- Admin-UI (`VideoWebPlayer/Components/Pages/Admin/`): Erweiterung von `Security.razor` (`/admin/security`) oder neue Seite (z. B. `Devices.razor`) mit: Pairing-Code erzeugen inkl. TTL-Anzeige, Liste gekoppelter Geräte, Widerruf-Aktion. Bei neuer Seite Kachel in `AdminIndex.razor` ergänzen.
- `AuthController.Login` (`VideoWebPlayer/Controllers/AuthController.cs`, Zeile 42, `[ApiTokenCheck(ApiTokenScope.MauiOnly)]`) bleibt der einzige MauiOnly-Endpunkt und akzeptiert künftig Geräte-Tokens.

### Tests (`VideoWebPlayer.Tests/`)

- Neue Tests für Pairing-Exchange (Erfolg, ungültiger/abgelaufener/doppelt verwendeter Code, Rate-Limit), Geräte-Token-Validierung und Widerruf.
- Anpassung/Erweiterung von `ApiTokenConfigurationTests.cs` und `ApiDocumentationContractTests.cs` (Vertragstest prüft `POST /api/auth/login`; der neue öffentliche Endpunkt ist in `docs/API.md` zu dokumentieren und ggf. im Vertragstest abzudecken).

### Dokumentation

- `docs/GUIDE_Installation.md`: Abschnitt Geräte-Pairing.
- `docs/help/` (u. a. `einrichtung.md`, ggf. neue Hilfeseite): Pairing-Ablauf für Administratoren.
- `docs/API.md`: öffentlicher Endpunkt `POST /api/pairing/exchange` und geänderte `X-API-Key`-Semantik.
- `docs/SECRETS_MANAGEMENT.md`: Rolle von `Jwt:ApiToken:Maui` als Fallback bzw. Entfall.

## Implementierungsansatz

- Pairing-Flow (serverseitig): Admin-UI ruft Pairing-Service → Code (ca. 8 Zeichen, TTL wenige Minuten) wird persistiert. `POST api/pairing/exchange` validiert Code (nicht abgelaufen, nicht verwendet), markiert ihn atomar als verbraucht, erzeugt serverseitiges flüchtiges ECDH-Schlüsselpaar, leitet aus Client-PublicKey das Shared Secret ab, generiert das Geräte-Token, speichert es (gehasht) als `PairedDevice` und antwortet mit Server-PublicKey + verschlüsseltem Token. Klartext-Token verlässt den Server nur verschlüsselt.
- Gate-Erweiterung: `ApiTokenCheckAttribute` prüft bei `MauiOnly` neben `Jwt:ApiToken:Maui` auch gespeicherte, nicht widerrufene Geräte-Tokens (Hash-Vergleich) und aktualisiert `LastUsedAtUtc`. `AnyClient`-Verhalten bleibt unverändert.
- Rate-Limiting/Bruteforce-Schutz auf `exchange` pro IP; Fehlversuche werden gezählt und führen zu temporärer/persistenter Sperre (Muster `LoginIpBlockService`/`BlockedLoginIp`). Abgelaufene Codes werden bei Validierung verworfen; ein Aufräumlauf (HostedService-Muster wie `MediaSourceScanService`/`ContinueWatchingWorker`) ist optional.
- Erweiterungspunkte: `ServiceCollectionExtensions` für DI-Registrierung, `ApplicationDbContext` + `IEntityTypeConfiguration<T>` für das Datenmodell, `UseVideoWebPlayer` bleibt unverändert (Controller werden über `MapControllers` eingebunden). Der Endpunkt darf weder `ApiTokenCheck` noch `BearerTokenCheck` tragen.
- Abhängigkeiten: Vertrag (Request/Response-Format, Kurven-/Verschlüsselungsparameter, Code-Format) muss mit dem App-Repository `martin-stromberg/VideoPlayer-App` abgestimmt sein; DTOs gehören in das geteilte Projekt `VideoWebPlayer.Client`.

## Konfiguration

- `Pairing:CodeLength`, `Pairing:CodeTtlMinutes`, `Pairing:MaxAttemptsPerIp`/Sperrparameter: Vorschlag als `appsettings`-Sektion `Pairing` mit Defaults im Code; alternativ DB-basiert über `Setup`/`ProgramSettingsService`, falls Admin-editierbar gewünscht (Annahme — in der Anforderung nicht spezifiziert).
- `Jwt:ApiToken:Maui`: bleibt vorerst als Fallback-Konfiguration bestehen oder entfällt (Anforderung lässt beides zu). Falls es entfällt, ist der Produktions-Pflichtcheck in `ServiceCollectionExtensions.cs` (Zeile 58) anzupassen; ein verpflichtender neuer Konfigurationswert ist nicht vorgesehen.

## Offene Fragen

- Fallback-Strategie: Bleibt `Jwt:ApiToken:Maui` als zusätzlich akzeptiertes Token für `MauiOnly` bestehen (Migration alter App-Versionen) oder wird es entfernt?
- ECDH-Parameter und Antwortformat: Kurve (z. B. P-256), Verschlüsselungsverfahren (z. B. AES-GCM mit Nonce), Serialisierung des PublicKey (SubjectPublicKeyInfo, Raw, Base64) — Festlegung gemeinsam mit dem App-Issue erforderlich.
- Geräte-Name: Übermittelt die App einen Anzeigenamen im Exchange-Request, oder vergibt der Admin den Namen nachträglich in der UI?
- `LastUsedAtUtc`: Bei jedem gated Request aktualisieren (Schreibzugriff pro Login) oder nur beim Pairing/erstem Login setzen?
- Rate-Limiting-Mechanismus: vorhandenes `ILoginIpBlockService`-Muster wiederverwenden/verallgemeinern oder ASP.NET-Core-Rate-Limiter neu einführen?
- Soll `exchange` zusätzlich durch `ConnectionCheck`/IP-Whitelist eingeschränkt werden, oder ist der Endpunkt bewusst für beliebige LAN-/WLAN-Clients offen (App ist zum Pairing-Zeitpunkt noch nicht authentifiziert, `WhitelistIpMiddleware` greift nur bei bereits authentifizierten Requests)?
- Code-Alphabet und Länge final festlegen (z. B. 8 Zeichen ohne verwechselbare Zeichen) und konkrete TTL (z. B. 5 Minuten).
- Reichweite des Geräte-Tokens: Aktuell deckt `MauiOnly` nur `POST api/auth/login` ab; ist der Scope für die geplante TV-App identisch, oder wird ein eigener Scope nötig?
- Verhalten bei Widerruf, während eine App-Session läuft: Bereits ausgestellte Benutzer-JWTs bleiben bis zum Ablauf gültig — ist das akzeptiert?
