# Datenmodell

## `Genre`
Datei: `VideoWebPlayer/Data/Genre.cs` (Encoding: ISO-8859/Windows-1252, CRLF)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` | Genre-Name (quellbezogen, nicht eindeutig) |
| `MediaSourceId` | `long` | Besitzende Medienquelle |
| `MediaSource` | `MediaSource` | Navigation |
| `StartDate` | `DateTime?` | Saisonaler Sichtbarkeitsbeginn (null = immer sichtbar) |
| `EndDate` | `DateTime?` | Saisonales Sichtbarkeitsende (null = immer sichtbar) |
| `AlternateNames` | `ICollection<GenreName>` | Alternativnamen → Umlenkung auf kanonische Namen (Scan: `GetOrCreateGenresAsync`; Expansion: `GenreCanonicalResolver`) |
| `MovieGenres` | `ICollection<MovieGenre>` | Movie-Verknüpfungen |
| `TVShowGenres` | `ICollection<TVShowGenre>` | TVShow-Verknüpfungen |
| `IsHidden` | `bool` | Ausgeblendet (Filter u. a. `SourceGenresController.GetGenresForSource`, `GenreCanonicalResolver.IsVisible`) |

## `GenreName`
Datei: `VideoWebPlayer/Data/GenreName.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel (`[Key]`) |
| `Name` | `string` | Alternativname (`[Required]`) |
| `GenreId` | `long` | Zugehörige Genre-Zeile (`[Required]`) |
| `Genre` | `Genre` | Navigation |

## `Movie` (relevante Ausschnitte)
| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `GenreNames` | `string` | Kommaseparierte Genre-Namen (Scan-Rohwert; `ReloadGenres` schreibt kanonische Namen zurück) |
| `MovieGenres` | `ICollection<MovieGenre>` | Verknüpfungstabelle zu `Genre` |
| `IsManuallyEdited` | `bool` | Überspringt `ReloadGenres` |

## `TVShow` (relevante Ausschnitte)
| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `GenreNames` | `string` | Analog `Movie.GenreNames` |
| `TVShowGenres` | `ICollection<TVShowGenre>` | Verknüpfungstabelle zu `Genre` |
| `IsManuallyEdited` | `bool` | Überspringt `ReloadGenres` |

## `Setup` (relevanter Ausschnitt)
| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `GenresChanged` | `bool` | Flag für `CheckReloadGenres` (wird von `GenreService.MarkGenresAsChangedAsync` gesetzt, in `CheckReloadGenres` verbraucht) |
| `DataVersion` | `int` | Versionszähler für `DataUpgradeManager` (`CurrentVersion = 8`) |

Hinweis: `DbContext`-Sets: `Genres`, `GenreNames`, `MovieGenres`, `TVShowGenres`
(`ApplicationDbContext.cs` Zeilen 134–148). Kein Unique-Index auf `Genre.Name`.
Backup: `VideoWebPlayerBackupData` kopiert Tabellen zeilenweise — kein eigener
Genre-Erzeugungspfad; keine Schemaänderung geplant, daher keine
`OptionalRestoreTables`/`OptionalRestoreColumns`-Anpassung nötig.
