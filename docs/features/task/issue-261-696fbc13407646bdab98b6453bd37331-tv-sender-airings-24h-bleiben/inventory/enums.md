# Enums

## `ProgramSlotStatus`

Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | Slot gehört zum wirksamen Plan; blockiert Airtime und dient als `invaderStarts`-/Overlapping-Evidenz |
| `Displaced = 1` | Von einer Re-Bewertung verdrängt; bleibt als Zeile zur Nachvollziehbarkeit bestehen (nie hard-deleted durch Reconcile), blockiert nichts mehr |

## `ProgramRuleType`

Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serie zur festen Zeit |
| `MoviePrimeTime = 1` | Film zur Primetime |
| `GenreRotation = 2` | Rotation durch Genre-Inhalte (Interleaving-Stränge) |
| `Repeat = 3` | Wiederholung |
| `HolidaySpecial = 4` | Feiertags-Sondersendung |
| `Filler = 5` | Lückenfüller — wird in `ReconcileAsync` einzeln geprüft, nicht als Airing |

## `ProgramRuleRecurrence`

Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | Täglich |
| `Weekly = 1` | Wöchentlich am `DayOfWeek` |
| `Once = 2` | Einmalig am `SingleDate` |
| `Holiday = 3` | An Feiertagen (`GermanHolidayCalendar`, `federalState`) |
| `Weekday = 4` | Mo–Fr |
| `Weekend = 5` | Sa/So |

## `ProgramRuleMaterialType`

Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie (`MaterialId` = TVShow) |
| `Movie = 1` | Einzelner Film |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre-Expansion; mehrere Serien → Interleaved-Stränge |
| `Playlist = 4` | Benutzer-Playlist |
| `Trailer = 5` | „Trailer kommender Filme" — globales Material ohne `MaterialId`-Bezug |
| `MediaSourceClips = 6` | Unklassifizierte Video-Items einer Quelle (`MaterialId` = MediaSource) |

## `ProgramCursorContentType`

Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | Cursor zeigt auf einen Film |
| `TVShowEpisode = 1` | Cursor zeigt auf eine Episode |
| `PlaylistEntry = 2` | Cursor zeigt auf einen Playlist-Eintrag |
| `Trailer = 3` | Cursor zeigt auf ein Trailer-MediaItem |
| `MediaItem = 4` | Cursor zeigt auf ein rohes MediaItem (Clip-Quelle) |
