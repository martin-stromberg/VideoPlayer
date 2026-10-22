# Enums

## `ProgramRuleRecurrence`
Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | täglich |
| `Weekly = 1` | wöchentlich am `DayOfWeek` |
| `Once = 2` | einmalig am `SingleDate` |
| `Holiday = 3` | an Feiertagen (deutscher Kalender + Bundesland) |

Persistiert als `int` in `ProgramRules.Recurrence` — neue Werte werden angehängt
(`Weekday = 4`, `Weekend = 5`), bestehende Werte bleiben unverändert.

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serie zur festen Sendezeit |
| `MoviePrimeTime = 1` | Film zur Primetime |
| `GenreRotation = 2` | Genre-/Formatrotation (einzige Art mit Genre-Material als Primärzweck) |
| `Repeat = 3` | Wiederholung |
| `HolidaySpecial = 4` | Feiertags-Special |
| `Filler = 5` | Füller (keine Occurrences — nur `FillGaps`) |

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

`TVShow=0`, `Movie=1`, `MovieCollection=2`, `Genre=3`, `Playlist=4`, `Trailer=5`,
`MediaSourceClips=6`. Genre-Material ist für `GenreRotation`, `Repeat`, `HolidaySpecial`
und `Filler` zulässig (`GetAllowedMaterialTypes`).

## `ProgramCursorContentType`
Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

`Movie=0`, `TVShowEpisode=1`, `PlaylistEntry=2`, `Trailer=3`, `MediaItem=4` — typisiert
`ProgramRuleCursor.LastContentId`.
