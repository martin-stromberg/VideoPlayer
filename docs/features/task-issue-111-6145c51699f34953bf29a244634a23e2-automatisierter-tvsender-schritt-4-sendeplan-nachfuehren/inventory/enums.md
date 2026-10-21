# Enums — Bestandsaufnahme

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serien-Sendezeit (Material: `TVShow`) |
| `MoviePrimeTime = 1` | Film zur Primetime (Material: `Movie`, `MovieCollection`) |
| `GenreRotation = 2` | Genre-/Formatrotation (Material: `Genre`) |
| `Repeat = 3` | Wiederholung (alle Materialarten) |
| `HolidaySpecial = 4` | Feiertags-Special (alle Materialarten) |
| `Filler = 5` | Platzhalter/Füller (Material: `Movie`, `MovieCollection`, `Genre`, `Playlist`) |

## `ProgramRuleRecurrence`
Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs` — `Daily`, `Weekly`, `Once`, `Holiday`.

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs` — `TVShow`, `Movie`, `MovieCollection`, `Genre`, `Playlist`.

## `ProgramCursorContentType`
Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | Cursor-Element ist ein `Movie` |
| `TVShowEpisode = 1` | Cursor-Element ist eine `TVShowEpisode` |
| `PlaylistEntry = 2` | Cursor-Element ist ein `PlaylistEntry` |

## Fehlend (neu anzulegen)
Ein Sendeplatz-Status-Enum (z. B. `ProgramSlotStatus` mit `Scheduled = 0`, `Displaced = 1`) existiert noch nicht.
