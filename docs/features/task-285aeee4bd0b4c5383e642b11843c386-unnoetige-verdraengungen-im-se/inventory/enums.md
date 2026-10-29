# Enums – Bestandsaufnahme

## `ProgramSlotStatus`

Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | Teil des wirksamen Sendeplans; belegt Sendezeit |
| `Displaced = 1` | Durch Neu-Bewertung verdrängt; nur Historie, blockiert keine Sendezeit — Träger des gemeldeten Symptoms („Verdrängt"-Chip) und der `_invaderStarts`-Evidenz |

## `ProgramRuleType`

Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serien-Sendeplatz zu fester Zeit |
| `MoviePrimeTime = 1` | Film zur Primetime |
| `GenreRotation = 2` | Rotation durch Genre-/Formatinhalte |
| `Repeat = 3` | Wiederholung |
| `HolidaySpecial = 4` | Feiertagssendung |
| `Filler = 5` | Füllmaterial — beim Reconcile nicht gruppiert, sondern einzeln validiert |

## `ProgramCursorContentType`

Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | Film |
| `TVShowEpisode = 1` | Serienepisode |
| `PlaylistEntry = 2` | Playlist-Eintrag |
| `Trailer = 3` | Trailer-MediaItem (`MovieMediaItem.IsTrailer`) |
| `MediaItem = 4` | Roh-MediaItem (Clip-Quellen / Clip-Gruppen) |

Steuert `StrandState.PreservesOrder` (`TVShowEpisode`/`PlaylistEntry` → geordnet) und
`IsUnorderedRing` (`Movie`/`MediaItem` → ungeordnet, per `ItemOrderKey` gemischt).

## `ProgramRuleMaterialType`

Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Einzelne Serie |
| `Movie = 1` | Einzelner Film |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre-Material (interleaved Stränge möglich) |
| `Playlist = 4` | Benutzer-Playlist |
| `Trailer = 5` | Trailer kommender Filme (expandiert nach Materialisierung) |
| `MediaSourceClips = 6` | Unklassifizierte Videos einer Medienquelle |

## `ProgramRuleRecurrence`

Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | Täglich |
| `Weekly = 1` | Wöchentlich (`DayOfWeek`) |
| `Once = 2` | Einmalig (`SingleDate`) |
| `Holiday = 3` | An Feiertagen (`GermanHolidayCalendar`, Bundesland) |
| `Weekday = 4` | Montag–Freitag |
| `Weekend = 5` | Samstag/Sonntag |

## `ProgramRuleValidityMode`

Datei: `VideoWebPlayer/Data/ProgramRuleValidityMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unrestricted = 0` | Immer gültig |
| `OneTime = 1` | Einmaliges Zeitfenster (`ValidFrom`–`ValidTo`, real datiert); kann ablaufen → `PurgeExpiredRulesAsync` |
| `AnnualWindow = 2` | Jährlich wiederkehrendes Fenster (Monat-Tag) |
| `MonthlyWindow = 3` | Monatliches Fenster (`ValidFromDay`–`ValidToDay`) |
| `WeeklyWindow = 4` | Wochentags-Bitmasken-Fenster (`ValidWeekdays`) |
| `HolidayWindow = 5` | Fenster um Feiertags-Anker |

## `ProgramScheduleUnknownRatingMode`

Datei: `VideoWebPlayer/Data/ProgramScheduleUnknownRatingMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `RestrictToNightWindow = 0` | Unbekannte Freigabe nur im FSK-18-Nachtfenster sendbar (produktiver Default; Fallback-Wert) |
| `Unrestricted = 1` | Unbekannte Freigabe jederzeit sendbar (Test-Fixture-Default) |
