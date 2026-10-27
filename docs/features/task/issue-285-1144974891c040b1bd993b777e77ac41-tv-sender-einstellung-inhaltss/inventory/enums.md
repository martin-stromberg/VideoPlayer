# Enums — Bestandsaufnahme

Für die Anforderung relevante Enums. Ein Enum für die zwei Spektrum-Optionen existiert
**nicht** — er müsste neu angelegt werden (Alternative laut Anforderung: Bool-Spalte).

## `ProgramRuleMaterialType`

Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie |
| `Movie = 1` | Einzelner Film |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre (namens-deduplizierte Vereinigung) |
| `Playlist = 4` | Benutzer-Playlist |
| `Trailer = 5` | „Trailer anstehender Filme" — globales Füller-Material ohne `MaterialId` |
| `MediaSourceClips = 6` | „Clip-Quelle" — unklassifizierte Videodateien einer Medienquelle |

Alle sieben Materialarten expandieren über den `AccessContext`
(`ExpandMaterialAsync`, `ProgramScheduleService.cs` Z. 1791).

## `ProgramRuleType`

Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serien-Sendezeit |
| `MoviePrimeTime = 1` | Film zur Primetime |
| `GenreRotation = 2` | Genre-/Formatrotation |
| `Repeat = 3` | Wiederholung |
| `HolidaySpecial = 4` | Feiertags-Special |
| `Filler = 5` | Platzhalter/Füller |

## `ProgramRuleRecurrence`

Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | Täglich |
| `Weekly = 1` | Wöchentlich (`DayOfWeek`) |
| `Once = 2` | Einmalig (`SingleDate`) |
| `Holiday = 3` | An Feiertagen (`HolidayFederalState` via `GermanHolidayCalendar`) |
| `Weekday = 4` | Werktags |
| `Weekend = 5` | Wochenende |

## `ProgramSlotStatus`

Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | geplant |
| `Displaced = 1` | verdrängt (durch `ReconcileAsync`) |

## `ProgramCursorContentType`

Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | Film |
| `TVShowEpisode = 1` | Serien-Episode |
| `PlaylistEntry = 2` | Playlist-Eintrag |
| `Trailer = 3` | Trailer-Clip |
| `MediaItem = 4` | freier Clip (`MediaItem`) |
