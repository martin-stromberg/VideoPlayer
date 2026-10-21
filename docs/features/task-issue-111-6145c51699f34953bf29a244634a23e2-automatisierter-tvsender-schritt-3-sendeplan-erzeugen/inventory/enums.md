# Enums – Bestandsaufnahme

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serie zu fester Sendezeit |
| `MoviePrimeTime = 1` | Film zur Primetime |
| `GenreRotation = 2` | Genre-/Formatrotation |
| `Repeat = 3` | Wiederholung zuvor ausgewählten Materials |
| `HolidaySpecial = 4` | Ausstrahlung an Feiertagen |
| `Filler = 5` | Platzhalter-/Füller-Material für Lücken |

## `ProgramRuleRecurrence`
Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | Täglich |
| `Weekly = 1` | Wöchentlich am konfigurierten Wochentag |
| `Once = 2` | Einmalig am konfigurierten Datum |
| `Holiday = 3` | An Feiertagen (deutscher Feiertagskalender) |

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie |
| `Movie = 1` | Einzelner Film |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre (über `MovieGenre`/`TVShowGenre`) |
| `Playlist = 4` | Benutzer-Playlist |

## `MediaType`
Datei: `VideoWebPlayer/Data/MediaType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie`, `TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection` | Referenzierbare Medientypen der `PlaylistEntry`s; abspielbar sind `Movie` und `TVShowEpisode` |

## `PlaylistSortMode`
Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `ByReleaseDate`, `Manual` | Playlist-Reihenfolge (für die Expansion von Playlist-Material) |
