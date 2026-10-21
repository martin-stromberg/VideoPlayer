# Enums und Konstanten — Bestandsaufnahme

## Vorhandene, für diesen Schritt relevante Enums/Konstanten

## `MediaTypeValues` (statische Konstantenklasse — polymorphe Medientypen)
Datei: `VideoWebPlayer.Client/Models/MediaTypeValues.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie` = `"Movie"` | einzelner Film |
| `TVShowEpisode` = `"TVShowEpisode"` | Serienepisode |
| `TVShowSeason` = `"TVShowSeason"` | Staffel |
| `TVShow` = `"TVShow"` | Serie |
| `MovieCollection` = `"MovieCollection"` | Filmsammlung |

Spiegelt `VideoWebPlayer.Data.MediaType`; an der JSON-Grenze und in `PlaylistEntry.MediaType` verwendet. `EnumContractTests` pinnt vertragliche Enums zwischen Client- und Server-Assembly (bisher `RecentEntryType`, `ManualBackupJobStatus`, `RestoreBackupJobStatus`, `PairingCodeKind`).

## `PlaylistSortMode`
Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs` — `Manual`, `ByReleaseDate` (Enum-Spalten werden als int persistiert, `HasDefaultValue` in Konfiguration).

## `MediaSourceType`
Datei: `VideoWebPlayer/Data/MediaSourceType.cs` — `Sftp` u. a.; Beispiel für `OptionalRestoreIntDefaults`-Eintrag.

## `RecentEntryType` / `ManualBackupJobStatus` / `RestoreBackupJobStatus` / `PairingCodeKind`
Vertraglich gepinnte Enums (`EnumContractTests`) — nicht betroffen, zeigen aber, dass neue enums, die eine Assembly-/API-Grenze überqueren, dort gepinnt werden sollten.

## Fehlende Enums (Nachweis)

Volltextsuche: keine Treffer für `ProgramRuleType`, `ProgramRuleRecurrence`, `ProgramRuleMaterialType`, `ScheduleRuleType`, `ScheduleRecurrence`. Die Enums für diesen Schritt müssen neu angelegt werden:

- `ProgramRuleType` — Regelart: `SeriesFixedTime`, `MoviePrimeTime`, `GenreRotation`, `Repeat`, `HolidaySpecial`, `Filler`
- `ProgramRuleRecurrence` — Wiederholungsmuster: `Daily`, `Weekly`, `Once`, `Holiday`
- `ProgramRuleMaterialType` — Materialart: `TVShow`, `Movie`, `MovieCollection`, `Genre`, `Playlist`
