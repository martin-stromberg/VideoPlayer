# Datenmodell — Bestandsaufnahme

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`; EF-Config: `VideoWebPlayer/Data/Configurations/ProgramRuleConfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | FK auf den Sender, `DeleteBehavior.Cascade` |
| `Name` | `string` (≤255, NOCASE) | Regelname; Unique-Index `(TVChannelId, Name)` |
| `RuleType` | `ProgramRuleType` | Regelart |
| `Recurrence`, `DayOfWeek`, `SingleDate`, `TimeOfDay` | Enum/`DateOnly`/`TimeOnly` | Wiederholungsmuster |
| `EpisodeCount` / `SlotDuration` | `int?` / `TimeSpan?` | Umfang der Ausstrahlung |
| `MaterialType` | `ProgramRuleMaterialType` | Materialart, required |
| `MaterialId` | `long` | Material-Referenz, required, **bewusst kein harter FK** (polymorph wie `PlaylistEntry.MediaType`/`MediaId`); bei `Genre` aktuell genau eine `Genre.Id` |
| `Priority`, `IsActive`, `CreatedAt`, `UpdatedAt` | `int`/`bool`/`DateTime` | Konfliktlösung, Aktiv-Flag, Zeitstempel |

Hinweis: Keine Navigation zu Genre-Listen vorhanden; `MaterialId` kann verwaist sein (Designentscheidung aus Schritt 2/3).

## `Genre`
Datei: `VideoWebPlayer/Data/Genre.cs` (**Achtung: Datei ist Windows-1252-codiert, nicht UTF-8** — enthält Byte `0xfc` („ü") in einem Kommentar; Edits müssen die Codierung erhalten oder die Datei verlustfrei überführen)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `Name` | `string` | Genre-Name (pro Quelle, nicht global unique) |
| `MediaSourceId` / `MediaSource` | `long` / `MediaSource` | Genres sind quellbezogen |
| `StartDate` / `EndDate` | `DateTime?` | Optionales saisonales Sichtbarkeitsfenster (jährlich wiederkehrend, siehe `GenreService.GetSeasonalGenresAsync`) |
| `AlternateNames` | `ICollection<GenreName>` | Umleitung auf kanonische Namen (Scan: `GetOrCreateGenresAsync`) |
| `MovieGenres` / `TVShowGenres` | `ICollection<MovieGenre>` / `ICollection<TVShowGenre>` | Inhalts-Verknüpfungen |
| `IsHidden` | `bool` | „Ausgeblendet"-Status |

## `GenreName`
Datei: `VideoWebPlayer/Data/GenreName.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `Name` | `string` | Alternativname |
| `GenreId` / `Genre` | `long` / `Genre` | Zugehöriges (i. d. R. verstecktes) Genre |

Scan-Semantik (`GetOrCreateGenresAsync`): Hat eine gefundene Genre-Zeile Alternativnamen (≠ Eigenname), wird **rekursiv** auf die Alternativnamen aufgelöst — unabhängig von `IsHidden`.

## `MovieGenre` / `TVShowGenre`
Dateien: `VideoWebPlayer/Data/MovieGenre.cs`, `VideoWebPlayer/Data/TVShowGenre.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MovieId`/`Movie` bzw. `TVShowId`/`TVShow` | `long` | Inhalts-Verknüpfung |
| `GenreId`/`Genre` | `long` | Genre-Verknüpfung (kann auf versteckte Alt-Genre-Zeilen zeigen) |

## `MediaSource` (Auszug)
Datei: `VideoWebPlayer/Data/MediaSource.cs` — erbt `Id`/`Name` von `MediaEntry`; `MediaSourceUsers` regelt Benutzerzugriff.

## `ProgramRuleCursor`
Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs` — Rotation je Regel über `LastContentType` (`ProgramCursorContentType.Movie`/`TVShowEpisode`/`PlaylistEntry`) + `LastContentId`; FK auf `ProgramRule` mit Kaskade.

## DbSets in `ApplicationDbContext`
`Genres`, `GenreNames`, `MovieGenres`, `TVShowGenres`, `ProgramRules`, `ProgramSlots`, `ProgramRuleCursors`, `MediaSources`, `MediaSourceUsers` u. a. vorhanden — **keine** `ProgramRule`-Join-Tabellen.

## Backup-Registrierung
`VideoWebPlayerBackupData.OptionalRestoreTables` enthält bereits `TVChannels`, `ProgramRules`, `ProgramSlots`, `ProgramRuleCursors`; neue Join-Tabellen müssen ergänzt werden. `OptionalRestoreColumns`/`OptionalRestoreIntDefaults` existieren für später ergänzte Spalten.
