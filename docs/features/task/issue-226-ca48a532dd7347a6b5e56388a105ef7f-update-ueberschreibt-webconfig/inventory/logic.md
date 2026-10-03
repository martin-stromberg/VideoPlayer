# Logik: Konfigurationspipeline, DI-Registrierung, Update-Services, Token-Gates

## `Program` (Top-Level-Statements)
Datei: `VideoWebPlayer/Program.cs`

| Schritt | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `WebApplication.CreateBuilder(args)` | 18 | Standard-Konfigurationsquellen in dieser Reihenfolge: `appsettings.json`, `appsettings.{Environment}.json`, User Secrets (nur Development), Umgebungsvariablen, Kommandozeile. **Keine eigenen `AddJsonFile`-Aufrufe vorhanden.** |
| `Directory.CreateDirectory(… "Logs")` | 21 | Log-Verzeichnis im Content-Root anlegen |
| `builder.Host.UseSerilog(…)` | 24–27 | Serilog aus `ctx.Configuration` lesen |
| `builder.AddVideoWebPlayerServices()` | 29 | Anwendungs-DI inkl. `Jwt:*`-Verbrauch (s. u.) |
| `builder.AddVideoWebPlayerAutoUpdate()` | 30 | msTools.Updater-Subsystem (s. u.) |
| `builder.WebHost.ConfigureKestrel(…)` | 34–41 | liest `Kestrel:Limits:MaxRequestBodySize` manuell, weil Kestrel `Limits` nicht aus der Konfiguration bindet; Parsing in `KestrelLimits.ParseMaxRequestBodySize` |
| `app.MigrateDatabase()`, `app.UseVideoWebPlayer()` | 45–46 | EF-Migrationen beim Start, Middleware-Pipeline |
| UDP-Discovery | 49–55 | übersprungen, wenn `Environment == "Testing"` (E2E-Tests) |

## `ServiceCollectionExtensions.AddVideoWebPlayerServices`
Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`

| Methode/Block | Zeilen | Kurzbeschreibung |
|---------------|--------|------------------|
| `Jwt:*`-Lesen | 48–52 | `Jwt:Key`, `Jwt:ApiToken:Web` ?? `Jwt:ApiToken`, `Jwt:ApiToken:Maui`, `Jwt:Issuer` ?? `"VideoWebPlayer"` — gelesen aus `builder.Configuration` |
| Produktions-Pflichtprüfung | 55–60 | `InvalidOperationException` bei fehlendem `Jwt:Key`, `Jwt:ApiToken(:Web)`, `Jwt:ApiToken:Maui` — **exakt das Laufzeit-Fehlerbild nach einem Update ohne Secrets** |
| `AddJwtBearer` | 92–124 | `TokenValidationParameters` mit `ValidIssuer`/`ValidAudience` = `issuer`, `IssuerSigningKey` aus Base64-`Jwt:Key`; `OnMessageReceived` akzeptiert `access_token`-Query für `/hubs/mediaupdate` |
| HttpClient `"Internal"` | 171–177 | setzt `X-API-Key`-Header aus `apiKey` |
| `SymmetricSecurityKey`-Singleton | 300–303 | nur registriert, wenn `Jwt:Key` gesetzt — konsumiert von `AuthorizationTokenService` und `BearerTokenCheckAttribute` |

## `AutoUpdateExtensions.AddVideoWebPlayerAutoUpdate`
Datei: `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs`

| Schritt | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `Configure<UpdateBackupOptions>` | 25 | bindet `AutoUpdate:Backup` |
| `AddSingleton<UpdateBackupCoordinator>` | 26 | Backup-vor-Update-Koordination |
| `AddHostedService<UpdateSettingsInitializer>` | 27 | wendet DB-Einstellungen beim Start auf `AutoUpdateOptions` an |
| `builder.UseAutoUpdate(cfg => …)` | 29–35 | liest `AutoUpdate:AllowPrereleaseUpdates`, erzeugt `AutoUpdateGithubSource` über `VideoWebPlayerUpdateSourceFactory`, setzt `UpdateUnitName` = `VideoWebPlayer-AutoUpdate`. **Anwendungsseitiger Eingriffspunkt:** Registrierungen vor diesem Aufruf schlagen die `TryAddSingleton`-Defaults der Bibliothek. |
| `AddHostedService<UpdateBackupEventBinder>` | 37 | verdrahtet `BeforeInstall`/`ErrorOccurred` |

## `UpdateSettingsService`
Datei: `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetDefaultSettings()` | public | Default-Werte aus `AutoUpdate:*`-Konfiguration (ohne DB) |
| `GetOrCreateAsync(ct)` | public | Singleton-DB-Zeile (`UpdateSettings`, Id=1) lesen/erzeugen |
| `UpdateAsync(update, ct)` | public | persistiert UI-Änderungen und wendet sie via `ApplyToRuntimeOptions` an |
| `ApplyToRuntimeOptionsAsync(ct)` | public | schreibt DB-Werte in die Singleton-`AutoUpdateOptions` (u. a. `ServiceName` — **nicht** `AppPoolName`) |
| `GetBackupOptionsAsync(ct)` | public | `UpdateBackupOptions` aus DB-Settings |
| `ApplyToRuntimeOptions(settings)` | private | setzt `Enabled`, `SourceCheck`, `AllowPrereleaseUpdates`, `EnableAutomaticInstallation/Download`, `ServiceName`, `Source` (neu via Factory) |
| `CreateDefaults()` | private | liest `AutoUpdate:Enabled`, `SourceCheck:Interval`, `AllowPrereleaseUpdates`, `EnableAutomaticInstallation/Download`, `ServiceName`, `Backup:*` aus `IConfiguration` |

## `UpdateSettingsInitializer`
Datei: `VideoWebPlayer/Services/Updates/UpdateSettingsInitializer.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartAsync` | public (IHostedService) | ruft `IUpdateSettingsService.ApplyToRuntimeOptionsAsync` beim Host-Start; Fehler wird geloggt und weitergeworfen |

## `UpdateBackupEventBinder`
Datei: `VideoWebPlayer/Services/Updates/UpdateBackupEventBinder.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartAsync`/`StopAsync` | public | abonniert/löst `IAutoUpdateEventAggregator.BeforeInstall` + `ErrorOccurred` |
| `OnBeforeInstall` | private | blockierender Backup-Aufruf via `UpdateBackupCoordinator.CreateBackupAsync`; bei Misserfolg `args.Cancel = true` |
| `OnErrorOccurred` | private | loggt Updater-Fehler mit Phase |

Abonnierte Events: `BeforeInstall`, `ErrorOccurred` (msTools.Updater)

## `UpdateBackupCoordinator`
Datei: `VideoWebPlayer/Services/Updates/UpdateBackupCoordinator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateBackupAsync(reason, ct)` | public | löst `IUpdateBackupService` in eigenem Scope auf; `false` nur, wenn Backup fehlschlägt/fehlt **und** `CancelInstallationOnFailure` |
| `ResolveTargetDirectory` | private | relativer Pfad → Content-Root |

## `UpdateAdminService`
Datei: `VideoWebPlayer/Services/Updates/UpdateAdminService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSnapshotAsync` | public | Settings + `IAutoUpdateOrchestrator.GetStatusAsync` + Defaults für `/admin/updates` |
| `UpdateSettingsAsync` | public | Delegat an `IUpdateSettingsService.UpdateAsync` |
| `CheckAsync` | public | manuelle Update-Prüfung (`IAutoUpdateCommandHandler.CheckAsync`), blockiert bei `IsBusy` |
| `InstallAsync` | public | Download (falls `UpdateAvailable`) + `InstallAsync(true, false)` |
| `IsBusy` | public static | `IsLocked` oder Zustand `Checking`/`Downloading`/`Installing` |
| `IsInstallable` | public static | `UpdateAvailable`/`ReadyToInstall` mit bekannter Version |

## `VideoWebPlayerUpdateSourceFactory`
Datei: `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateSourceFactory.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Create(bool includePrereleases)` | public | `AutoUpdateGithubSource` für `martin-stromberg/VideoPlayer`; optional Bearer-Token aus `AutoUpdate:GitHubToken` mit eigenem `HttpClient` |

## `ApiTokenCheckAttribute`
Datei: `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnActionExecutionAsync` | public | liest `X-API-Key`-Header (optional `Bearer `-Präfix), vergleicht gegen `Jwt:ApiToken`, `Jwt:ApiToken:Web`, `Jwt:ApiToken:Maui` (je nach `ApiTokenScope`; mehrere Tokens komma-/semikolon-getrennt möglich) — **zur Laufzeit aus `IConfiguration`, d. h. geänderte Konfigurationsquellen greifen ohne Neustart der Attributlogik**; `MauiOnly` akzeptiert zusätzlich `IDeviceTokenService`-Gerätetokens; sonst `401 UnauthorizedResult` |

Verwendet von: `AuthController` (Zeilen 58, 78–79, 98, 127) — u. a. `/api/auth/login` (`MauiOnly`).

## `BearerTokenCheckAttribute`
Datei: `VideoWebPlayer/Controllers/Attributes/BearerTokenCheckAttribute.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnActionExecuting` | public | validiert `Authorization: Bearer`-JWT gegen das registrierte `SymmetricSecurityKey`-Singleton und `Jwt:Issuer` ?? `"VideoWebPlayer"`; `401` bei fehlendem/ungültigem Token |

Verwendet von: `ActorsController`, `AdminSourcesController`, `AuthController`, `ContinueWatchingController`, `EpisodesController`, `FavoritesController`, `ItemsController`, `PicturesController`, `PlaylistsController`, `SourceGenresController`, `SourceIconsController`, `SourcesController`, `UnlockedMediaController`.

## `AuthorizationTokenService` / `AuthService`
Datei: `VideoWebPlayer/Services/Authentication/IAuthService.cs`

| Methode | Klasse | Kurzbeschreibung |
|---------|--------|------------------|
| `CreateToken(ApplicationUser)` | `AuthorizationTokenService` | signiert JWT (HmacSha256) mit injiziertem `SymmetricSecurityKey`; `Jwt:Issuer` ?? `"VideoWebPlayer"`; 12 h Gültigkeit — ein neuer `Jwt:Key` invalidiert alle bestehenden Tokens |
| `LoginAsync`/`ImpersonateAsync` | `AuthService` | Passwort-Login bzw. Admin-Impersonation → `CreateToken` |

## `KestrelLimits`
Datei: `VideoWebPlayer/Extensions/KestrelLimits.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ParseMaxRequestBodySize` | public (internal class) | `>0` → Limit, `<=0` → `null` (unbegrenzt), nicht-numerisch → `InvalidOperationException` |

## `WebApplicationExtensions`
Datei: `VideoWebPlayer/Extensions/WebApplicationExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `MigrateDatabase` | public | `db.Database.Migrate()` beim Start |
| `UseVideoWebPlayer` | public | Middleware-Pipeline (Host-Header-Prüfung, `UseWhitelistIp`, statische Dateien mit `no-cache` für css/js, Auth, `UseBackups`, `RestoreInProgressMiddleware`, `FirstUserRedirectMiddleware`, SignalR-Hub `/hubs/mediaupdate` mit JwtBearer) |

## `appsettings`-Dateien

| Datei | Relevanter Inhalt |
|-------|-------------------|
| `VideoWebPlayer/appsettings.json` | `ConnectionStrings:DefaultConnection` (`Data/WebVideoPlayer.db`), `Playlists`, `Backups`, `EpisodeBackgroundImage`, `Serilog`, `Logging`, `AutoUpdate` (Zeilen 77–94: `Enabled`, `EnableAutomaticDownload/Installation`, `ScheduledInstallTime`, `StopHostAfterScriptStart: true`, `DownloadPath: "Updates"`, `SourceCheck.Interval: 360`, `Backup:{Enabled,Path:"Backups",RetainedBackupCount:5,CancelInstallationOnFailure}`). **Kein `Jwt`-Abschnitt** (Secrets sind nie im Repo). **Kein `AutoUpdate:AppPoolName`.** |
| `VideoWebPlayer/appsettings.Production.json` | `Kestrel:Limits:MaxRequestBodySize: 0`, `Kestrel:Endpoints:Http:Url: http://*:5002`, Serilog/Logging-Level |
| `VideoWebPlayer/appsettings.Development.json` | `AutoUpdate.Enabled: false`, `EnableAutomaticInstallation: false` |

Alle drei landen per Web-SDK-Default im Publish-Output und damit im Release-Zip. Es existiert **keine** `appsettings.Local.json`/`secrets.json`-Einbindung; `.gitignore` enthält `secrets.json`-Einträge nur für andere Projekte (`MyVideoPlayer`, `VideoPlayer`, `Mediathek`, `MediaPlayer` — MAUI-Apps, nicht VideoWebPlayer).

## `IConfiguration`-Zugriffe auf `Jwt:*` — Gesamtübersicht

| Verbraucher | Schlüssel | Zeitpunkt |
|-------------|-----------|-----------|
| `ServiceCollectionExtensions` (48–52, 92–124, 171–177, 300–303) | `Jwt:Key`, `Jwt:ApiToken(:Web/:Maui)`, `Jwt:Issuer` | einmalig beim Start |
| `ApiTokenCheckAttribute` (71–77) | `Jwt:ApiToken(:Web/:Maui)` | pro Request |
| `BearerTokenCheckAttribute` (27) | `Jwt:Issuer` | pro Request |
| `AuthorizationTokenService.CreateToken` (63) | `Jwt:Issuer` | pro Token-Erzeugung |
