# Enums — Bestandsaufnahme

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie |
| `Movie = 1` | Einzelfilm |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre (nach Name über Quellen dedupliziert) |
| `Playlist = 4` | Benutzer-Playlist |

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs` — `SeriesFixedTime`, `MoviePrimeTime`, `GenreRotation`, `Repeat`, `HolidaySpecial`, `Filler`. Genre-Material erlaubt bei `GenreRotation`, `Repeat`, `HolidaySpecial`, `Filler` (laut `GetAllowedMaterialTypes`).

## `ProgramRuleRecurrence`
`Daily`, `Weekly`, `Once`, `Holiday` — unverändert.

## `ProgramCursorContentType`
`Movie`, `TVShowEpisode`, `PlaylistEntry` — Cursor-Inhaltstypen; Genre-Union ändert nichts daran (Movie/Episode).

## `ProgramSlotStatus`
`Scheduled`, `Displaced` — unverändert.
