# Datenmodell — Bestandsaufnahme

Bezug: Anforderung „Geräte-Pairing mit dynamischem Geräte-Token" (`../requirement.md`).

Die in der Anforderung genannten neuen Entitäten `PairedDevice` und `PairingCode` sowie die Konfigurationsklassen `PairedDeviceConfiguration` und `PairingCodeConfiguration` **existieren nicht** im Code (verifiziert per Volltextsuche nach `Pairing`, `PairedDevice`, `DeviceToken`). Nachfolgend die vorhandenen, für die Anforderung relevanten Datenmodell-Artefakte.

## `ApplicationDbContext`

Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`

- `partial class`, erbt von `IdentityDbContext<ApplicationUser>` (Zeile 14).
- Konstruktor benötigt `DbContextOptions<ApplicationDbContext>` und `EventManager` (Zeile 24–28); der `EventManager` ist damit Pflicht-Abhängigkeit auch in Tests.
- DbSet-Region: Zeilen 29–154 (`#region DbSet Properties` … `#endregion`).
- `OnModelCreating` (Zeile 646–651) ruft `modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly)` auf — neue `IEntityTypeConfiguration<T>`-Klassen unter `VideoWebPlayer/Data/Configurations/` werden automatisch eingebunden.
- Registrierung: `services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString))` in `ServiceCollectionExtensions.cs` Zeile 157; ConnectionString `DefaultConnection`, Fallback `Data Source=app.db`.
- Migrationen liegen unter `VideoWebPlayer/Migrations/` (55 Dateien inkl. `ApplicationDbContextModelSnapshot.cs`); sie werden beim Start über `WebApplicationExtensions.MigrateDatabase()` (`VideoWebPlayer/Extensions/WebApplicationExtensions.cs`, Zeilen 23–32, `db.Database.Migrate()`) angewendet.
- Enthält zusätzlich domänenspezifische Methoden (`AddMediaSourceAsync`, `UpdateMediaSourceAsync`, `DeleteMediaSourceAsync`, `EnsureMediaCollectionExistsAsync`, `EnsureMediaItemExistsAsync`, `EnsureMovieCollectionExistsAsync`, `EnsureMovieExistsAsync`, `EnsureTVShowExistsAsync`, `EnsureTVShowSeasonExistsAsync`, `EnsureTVShowEpisodeExistsAsync`, `GetRelationsForMediaItemAsync`) und die nested Klasse `MediaItemRelationResult`.

Vorhandene DbSets (Zeilen 33–153): `MediaSources`, `MediaSourceUsers`, `MediaCollections`, `MediaItems`, `MovieCollections`, `Movies`, `TVShows`, `TVShowSeasons`, `TVShowEpisodes`, `MovieMediaItems`, `TVShowEpisodeMediaItems`, `Pictures`, `MediaSourceIcons`, `Setups`, `RecentEntries`, `FavoriteEntries`, `UnlockedMediaEntries`, `Genres`, `GenreNames`, `MovieGenres`, `TVShowGenres`, `Actors`, `MovieActors`, `TVShowEpisodeActors`, `ContinueWatchingEntries`, `WatchedEntries`, `BlockedLoginIps` (Zeile 141), `BackupSettings`, `BackupOperationHistories`, `UpdateSettings`.

## `BlockedLoginIp` (Stilvorbild für persistente Sicherheits-Entität)

Datei: `VideoWebPlayer/Data/BlockedLoginIp.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Ip` | `string` | Gesperrte IP-Adresse; Primärschlüssel (per Konfiguration) |
| `BlockedAtUtc` | `DateTime` | Zeitpunkt der Sperrung (UTC) |
| `Failures` | `int` | Anzahl registrierter Fehlversuche |

Konfiguration: `BlockedLoginIpConfiguration` (`VideoWebPlayer/Data/Configurations/BlockedLoginIpConfiguration.cs`) — `HasKey(x => x.Ip)`, `Ip` mit `HasMaxLength(64)`, Index auf `BlockedAtUtc`. Wird genutzt von `LoginIpBlockService` und der Admin-Seite `Security.razor`.

## `UpdateSettings` (Stilvorbild für `*AtUtc`-Timestamps / Singleton-Einstellungszeile)

Datei: `VideoWebPlayer/Data/UpdateSettings.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Singleton-ID, Default `1` |
| `AutomaticChecksEnabled` | `bool` | Update-Prüfung aktiv (Default `true`) |
| `CheckIntervalMinutes` | `int` | Intervall (Default 360) |
| `AllowPrereleaseUpdates` | `bool` | Prerelease zulassen |
| `AutomaticInstallationEnabled` | `bool` | Auto-Installation |
| `AutomaticDownloadEnabled` | `bool` | Auto-Download (Default `true`) |
| `ServiceName` | `string?` | Dienstname für Updater |
| `CreateBackupBeforeInstallation` | `bool` | Backup vor Installation (Default `true`) |
| `CancelInstallationOnBackupFailure` | `bool` | Abbruch bei Backup-Fehler (Default `true`) |
| `UpdateBackupPath` | `string` | Backup-Pfad (Default `"Backups"`) |
| `RetainedUpdateBackupCount` | `int` | Aufbewahrung (Default 5) |
| `UpdatedAtUtc` | `DateTime` | Letzter Änderungszeitpunkt, Default `DateTime.UtcNow` |

Konfiguration: `UpdateSettingsConfiguration` (`VideoWebPlayer/Data/Configurations/UpdateSettingsConfiguration.cs`) — `HasKey(x => x.Id)`, Längenbegrenzungen für `ServiceName` (200) und `UpdateBackupPath` (1024).

## `Setup` (DB-basierte Programmeinstellungen)

Datei: `VideoWebPlayer/Data/Setup.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Primärschlüssel |
| `DataVersion` | `int` | Datenversionsstand |
| `GenresChanged` | `bool` | Änderungsflag für Genres |
| `ApplicationTitle` | `string` | Anwendungstitel (Default `"Martins Videosammlung"`) |
| `ScanProcessIntervalMinutes` | `int` | Scan-Intervall (Default 60) |
| `MediaCollectionScanIntervalDays` | `int` | Rescan-Intervall (Default 7) |
| `ContinueWatchingEndThresholdSeconds` | `int` | Schwelle Weiterschauen (Default 30) |
| `ActorCollectionThresholdPercent` | `int` | Schwellenwert Schauspieler-Collections (Default 50) |

Keine eigene `IEntityTypeConfiguration`; Zugriff über `ProgramSettingsService` (`GetOrCreateSetupAsync`). Relevant als Alternative für DB-basierte Pairing-Einstellungen (siehe Anforderung, Abschnitt „Konfiguration").

## `ApplicationUser`

Datei: `VideoWebPlayer/Data/ApplicationUser.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sources` | `string` | Serialisierte Liste zugänglicher Quellen |
| `IsAdmin` | `bool` | Administrator-Flag; Grundlage des Claims `"IsAdmin"` und der Policy `"AdminOnly"` |

Erbt `IdentityUser` (string-`Id`). Relevant für ein optionales `CreatedByUserId` auf `PairingCode`.

## Bestehende DTOs im geteilten Client-Projekt

Verzeichnis: `VideoWebPlayer.Client/Models/` — vorhandene Dateien: `AuthenticationRequest.cs`, `AuthorizationToken.cs`, `ContinueWatchingDto.cs`, `ContinueWatchingMutationResult.cs`, `DtoActor.cs`, `DtoMovie.cs`, `DtoRecentEntry.cs`, `DtoSource.cs`, `FavoriteEntry.cs`, `ImpersonateRequest.cs`, `MediaEntryDto.cs`, `UnlockedUsersRequest.cs`.

| DTO | Datei | Eigenschaften |
|-----|-------|---------------|
| `AuthenticationRequest` | `VideoWebPlayer.Client/Models/AuthenticationRequest.cs` | `Email` (string), `Password` (string) |
| `AuthorizationToken` | `VideoWebPlayer.Client/Models/AuthorizationToken.cs` | `token` (string, kleingeschrieben), `expires` (DateTime) |

Hinweis: Es gibt zusätzlich eine zweite `ImpersonateRequest`-Klasse als Top-Level-Klasse am Ende von `VideoWebPlayer/Controllers/AuthController.cs` (Zeilen 82–88, globaler Namespace) neben `VideoWebPlayer.Client/Models/ImpersonateRequest.cs`.

DTO-Stil: schlichte Klassen mit get/set-Properties, Namespace `VideoWebPlayer.Client.Models`. Neue Pairing-DTOs (`PairingExchangeRequest`, `PairingExchangeResponse`) sind hier einzuordnen — sie existieren noch nicht.

## Weitere Konventionen

- Entity-Konfigurationen liegen in `VideoWebPlayer/Data/Configurations/` und implementieren `IEntityTypeConfiguration<T>` (`public sealed class …Configuration`), automatisch via `ApplyConfigurationsFromAssembly` gebunden.
- UTC-Timestamps folgen dem Muster `*AtUtc` bzw. teils `CreatedAt`/`UpdatedAt` (ohne `Utc`-Suffix, z. B. `MediaSource.CreatedAt`, `WatchedEntry.WatchedAt`, `ContinueWatchingEntry.UpdatedAt`).
- Das `VideoWebPlayer.csproj` enthält eine `<ItemGroup>` mit `<DbSet Include="…" />`-Einträgen (nur `MediaSource`, `MediaCollection`, `MediaItem` — wirkt nicht vollständig gepflegt).
