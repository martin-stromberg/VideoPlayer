# Datenmodell — Bestandsaufnahme

## `Playlist` (Muster für benannte Stammdaten-Entity)
Datei: `VideoWebPlayer/Data/Playlist.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK (`[Key]`) |
| `UserId` | `string` (required) | FK auf `ApplicationUser`, Cascade-Delete |
| `Name` | `string` (required, `[MaxLength(255)]`, NOCASE-Collation) | Anzeigename; Unique-Index `(UserId, Name)` |
| `Description` | `string?` (`[MaxLength(2000)]`) | optionale Beschreibung |
| `SortMode` | `PlaylistSortMode` | Enum-Spalte mit `HasDefaultValue` |
| `CreatedAt`/`UpdatedAt` | `DateTime` | Zeitstempel, `IsRequired` in Konfiguration |
| `IsPublic` | `bool` | öffentliche Playlist, nur Admin-Eigentümer setzbar |
| `CoverPictureId`/`CoverPicture` | `long?`/`Picture?` | optionale FK-Navigation ohne OnDelete-Angabe |
| `PlaylistEntries` | `ICollection<PlaylistEntry>` | Navigation (Cascade) |

Konfiguration: `Data/Configurations/PlaylistConfiguration.cs` — Muster für `IEntityTypeConfiguration<T>`.

## `PlaylistEntry` (Muster für polymorphe Medienreferenz)
Datei: `VideoWebPlayer/Data/PlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `PlaylistId`/`Playlist` | `long`/Navigation | Cascade-Delete |
| `MediaType` | `string` (required) | polymorpher Typverweis, Werte aus `MediaTypeValues` (`Movie`, `TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection`) — **kein** EF-Fremdschlüssel |
| `MediaId` | `long` | Id der referenzierten Entität — **kein** EF-Fremdschlüssel |
| `ParentMediaType`/`ParentMediaId` | `string?`/`long?` | optionale Container-Referenz |
| `AddedAt` | `DateTime` | Zeitstempel |
| `SortOrder` | `long?` | manuelle Reihenfolge |

Indizes: Unique `(PlaylistId, MediaType, MediaId)`; `IX_PlaylistEntries_MediaType_MediaId`.

## `MediaItem` (Ergebnis Schritt 1 — vorhanden)
Datei: `VideoWebPlayer/Data/MediaItem.cs` (erbt `MediaEntry`: `Id`, `Name`, `Path`, `CreatedAt`, `ClassifiedAt`, `Changed`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaCollectionId`/`MediaCollection` | `long`/Navigation | Ablageort |
| `Duration` | `TimeSpan?` | per ffprobe ermittelte Laufzeit, `null` = unbekannt |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt der letzten Probe |

## `Genre` (medienquellenbezogen — relevant für Materialart „Genre")
Datei: `VideoWebPlayer/Data/Genre.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `Name` | `string` | Genre-Name (über Quellen hinweg doppelt möglich; UI dedupliziert per Name) |
| `MediaSourceId`/`MediaSource` | `long`/Navigation | Genre gehört zu genau einer Quelle |
| `StartDate`/`EndDate` | `DateTime?` | saisonale Sichtbarkeit |
| `IsHidden` | `bool` | Ausblendung |
| `AlternateNames` | `ICollection<GenreName>` | alternative Namen |
| `MovieGenres`/`TVShowGenres` | `ICollection<…>` | Verknüpfungen |

## `MediaSourceUser` / `UnlockedMediaEntry` (Sichtbarkeits-/Freigabemodell)
Dateien: `VideoWebPlayer/Data/MediaSourceUser.cs`, `UnlockedMediaEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceUser.MediaSourceId`/`UserId` | `long`/`string` | regulärer Quellenzugriff je Benutzer |
| `UnlockedMediaEntry.MovieCollectionId`/`TVShowId` | `long?` | einzeln freigeschaltete Einträge |

## `Setup` (Single-Row-Einstellungstabelle)
Datei: `VideoWebPlayer/Data/Setup.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | PK (genau eine Zeile) |
| `ApplicationTitle`, `ScanProcessIntervalMinutes`, `MediaCollectionScanIntervalDays`, `ContinueWatchingEndThresholdSeconds`, `ActorCollectionThresholdPercent`, `MdnsAdvertisementEnabled`, `DiscoveryPublicBaseUrl`, `PlaylistBackfillLastSweepAt` | div. | bestehende Programmeinstellungen |

Neue Sender-Einstellung `HolidayFederalState` wird hier ergänzt (Spaltentyp `string?`).

## `ApplicationDbContext`
Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`

- `IdentityDbContext<ApplicationUser>`; DbSets für alle Entities; `OnModelCreating` ruft `ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly)` auf — neue Konfigurationen werden automatisch gefunden.
- `Database.IsRelational()`-Pfad registriert die SQLite-Funktion `lower_invariant` (`AppDbFunctions.LowerInvariant`).
- `DeleteMediaSourceAsync` löscht Quellbestände per `ExecuteDeleteAsync` — polymorphe Referenzen ohne EF-FK (wie `PlaylistEntry`) sind davon unberührt.

## `VideoWebPlayerBackupData` (Backup-Registrierung)
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `GetTables()` ermittelt Tabellen/Spalten automatisch aus dem EF-Modell — neue Tabellen sind automatisch im Backup enthalten.
- `OptionalRestoreTables`: Tabellen, die in Alt-Backups fehlen dürfen (u. a. `Playlists`, `PlaylistEntries`, `PairedDevices`, `RefreshTokens`).
- `OptionalRestoreColumns`: Spalten, die in Alt-Backups fehlen dürfen (Format `{Tabelle}.{Spalte}`, z. B. `Setups.MdnsAdvertisementEnabled`, `MediaItems.Duration`).
- `OptionalRestoreBoolDefaults`/`LongDefaults`/`IntDefaults`: Ersatzwerte beim Restore für fehlende NOT-NULL-Spalten.
- `IgnoredRestoreColumns`: bekannte Alt-Backup-Spalten, die ignoriert werden.

## Fehlende Entities (Nachweis)

Volltextsuche über `VideoWebPlayer/` und `VideoWebPlayer.Client/`: keine Treffer für `TVChannel`, `ProgramRule`, `ScheduleRule`, `ScheduleSlot`, `TVChannels`, `ProgramRules`. Die Tabellen/Entities für diesen Schritt müssen neu angelegt werden.
