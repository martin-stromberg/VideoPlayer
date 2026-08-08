# EF Core DbContext und Datenmodell

## DbContext

`VideoWebPlayer/Data/ApplicationDbContext.cs` ist der zentrale EF Core Kontext und erbt von `IdentityDbContext<ApplicationUser>`. Damit liegen Identity-Tabellen und fachliche Tabellen in derselben SQLite-Datenbank.

Registrierung:

- `ServiceCollectionExtensions.cs` liest `ConnectionStrings:DefaultConnection`.
- Fallback ist `Data Source=app.db`.
- `appsettings.json` setzt `Data Source=Data/WebVideoPlayer.db`.
- `WebApplicationExtensions.MigrateDatabase()` fuehrt beim Start `db.Database.Migrate()` aus.

Konfigurationen werden per `modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly)` geladen.

## DbSets

Fachliche Tabellen im Kontext:

- `MediaSources`
- `MediaSourceUsers`
- `MediaCollections`
- `MediaItems`
- `MovieCollections`
- `Movies`
- `TVShows`
- `TVShowSeasons`
- `TVShowEpisodes`
- `MovieMediaItems`
- `TVShowEpisodeMediaItems`
- `Pictures`
- `MediaSourceIcons`
- `Setups`
- `RecentEntries`
- `FavoriteEntries`
- `Genres`
- `GenreNames`
- `MovieGenres`
- `TVShowGenres`
- `ContinueWatchingEntries`
- `BlockedLoginIps`

Identity-Tabellen kommen aus `IdentityDbContext<ApplicationUser>` hinzu, inklusive Benutzer, Claims, Logins, Tokens und Rollen-/RoleClaim-Tabellen je nach Identity-Schema. Rollen werden fachlich offenbar nicht fuer Admins genutzt.

## ApplicationUser

`ApplicationUser` erweitert `IdentityUser`:

- `Sources`: serialisierte Liste zugreifbarer Quellen.
- `IsAdmin`: boolescher Administratorstatus.

Restore-Anforderung:

- Das ausfuehrende Admin-Konto muss ueber `ApplicationUser.Id` oder Name/E-Mail vor dem Restore identifiziert werden.
- Ist der User im Backup vorhanden, sollen die Werte aus dem Backup uebernommen werden.
- Ist der User nicht im Backup vorhanden, muss er nach dem Loeschen wieder eingefuegt werden, ohne Quellenzuweisung. Praktisch bedeutet das mindestens `Sources = ""` und `IsAdmin = true`, damit der ausfuehrende Administrator weiter administrieren kann.

## Setup und Programmeinstellungen

`Setup` enthaelt aktuell:

- `DataVersion`
- `GenresChanged`
- `ScanProcessIntervalMinutes`
- `MediaCollectionScanIntervalDays`

Backup-Einstellungen koennen entweder in `Setup` erweitert werden oder in einer eigenen Backup-Konfigurationsdatei bzw. Backup-Optionsbindung bleiben. Fuer Wiederverwendbarkeit der Bibliothek ist eine optionsbasierte Konfiguration besser; falls Admins Einstellungen aendern sollen, braucht das Hostprojekt jedoch persistente Werte.

## Datenzugriffsbesonderheiten

`ApplicationDbContext` enthaelt fachliche Hilfsmethoden, die direkt speichern und Events publizieren:

- `AddMediaSourceAsync`
- `UpdateMediaSourceAsync`
- `DeleteMediaSourceAsync`
- mehrere `Ensure...ExistsAsync` Methoden

Ein Restore sollte diese Methoden nicht verwenden, weil sie Events ausloesen und teilweise inkrementelle Seiteneffekte haben. Fuer Restore ist ein kontrollierter Low-Level-Import ueber DbSets mit deaktivierten Hintergrundprozessen sinnvoller.

## Backup-Umfang

Minimal gemaess Anforderung: Datenbankdaten. Dafuer muessen alle Identity- und fachlichen Tabellen exportiert werden.

Moegliche Zusatzartefakte, die fachlich relevant sein koennen:

- Genre-Icons werden in `wwwroot/images/genres` als Dateien abgelegt.
- Demo-/Standarddaten liegen unter `DemoDataSets` bzw. `StandardSources.json` und sind Build-/Seed-Artefakte, aber nicht zwingend Laufzeitdaten.
- Serilog-Logs unter `Logs` sollten nicht Teil eines Datenbackups sein.

Der offene Punkt aus `requirement.md` zum Backupumfang sollte in der Planung explizit entschieden werden.
