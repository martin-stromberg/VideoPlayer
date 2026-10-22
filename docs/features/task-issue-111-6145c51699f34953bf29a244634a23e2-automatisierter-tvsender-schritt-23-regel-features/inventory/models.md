# Datenmodell

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | FK auf `TVChannel` |
| `Name` | `string` | Regelname (max. 255) |
| `RuleType` | `ProgramRuleType` | SeriesFixedTime/MoviePrimeTime/GenreRotation/Repeat/HolidaySpecial/Filler |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster (persistiert als int) |
| `DayOfWeek` | `DayOfWeek?` | nur bei `Weekly` gesetzt |
| `SingleDate` | `DateOnly?` | nur bei `Once` gesetzt |
| `TimeOfDay` | `TimeOnly` | Sendezeit (lokal) |
| `EpisodeCount` | `int?` | Folgen pro Ausstrahlung (alternativ zu `SlotDuration`) |
| `SlotDuration` | `TimeSpan?` | Soll-Länge der Ausstrahlung |
| `MaterialType` | `ProgramRuleMaterialType` | TVShow/Movie/MovieCollection/Genre/Playlist/Trailer/MediaSourceClips |
| `MaterialId` | `long` | polymorphe Materialreferenz (bei Genre: erstes Genre, Rest in `Genres`) |
| `Genres` | `ICollection<ProgramRuleGenre>` | Genre-Mehrfachauswahl (nur Genre-Material) |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quell-Filter (nur Genre-Material) |
| `Priority` | `int` | Konfliktauflösung (höher gewinnt, Gleichstand: kleinere Id) |
| `IsActive` | `bool` | aktiv/inaktiv |

## `ProgramRuleCursor`
Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `ProgramRuleId` | `long` | FK auf `ProgramRule`, echte Cascade |
| `LastContentType` | `ProgramCursorContentType` | Art von `LastContentId` |
| `LastContentId` | `long` | zuletzt eingeplantes Inhaltselement der Regel-Expansion |
| `UpdatedAt` | `DateTime` | letzter Schreibzeitpunkt |

Konfiguration (`ProgramRuleCursorConfiguration.cs`): **Unique-Index auf `ProgramRuleId`
allein** — genau EIN Cursor je Regel. Kein Strang-Konzept vorhanden.

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

Weiche Referenzen ohne FK: `MediaItemId`, `MovieId?`, `TVShowEpisodeId?`, `ProgramRuleId?`,
`RuleType`, `Status` (`Scheduled`/`Displaced`), `Start`/`End` (UTC). Enthält KEINE Serien-Id —
die Strang-Zugehörigkeit eines Slots muss über `TVShowEpisodeId` → Episode → Show hergeleitet
werden (in `ReconcileAsync` über `state.Items`/`FindItemIndex`).

## Backup-Registrierung
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `OptionalRestoreTables` (Z. 23–45): enthält u. a. `ProgramRules`, `ProgramSlots`,
  `ProgramRuleCursors`, `ProgramRuleGenres`, `ProgramRuleMediaSources`.
- `OptionalRestoreColumns` (Z. 47–93): Format `{Tabelle}.{Spalte}`; Alt-Backups ohne die
  Spalte stellen wiederher. Neue `ProgramRuleCursors`-Spalte muss hier rein.
- `OptionalRestoreBoolDefaults`/`LongDefaults`/`IntDefaults` (Z. 103–136): füllen
  NOT-NULL-Spalten in Alt-Backups mit Defaults. Für eine nullable String-Spalte nicht nötig.
