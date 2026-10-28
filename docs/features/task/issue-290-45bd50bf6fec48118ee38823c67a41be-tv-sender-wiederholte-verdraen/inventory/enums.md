# Enums — Bestandsaufnahme (Issue #290)

## `ProgramSlotStatus`
Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | Teil des wirksamen Sendeplans; belegt Sendezeit |
| `Displaced = 1` | Durch Neubewertung invalidiert; nur Historie, blockiert nichts, bleibt als Zeile erhalten (`DeleteDisplacedSlotsAsync` räumt manuell) |

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serie zur festen Sendezeit |
| `MoviePrimeTime = 1` | Film zur Primetime |
| `GenreRotation = 2` | Rotation durch Genre-/Formatinhalte |
| `Repeat = 3` | Wiederholung zuvor ausgewählten Materials |
| `HolidaySpecial = 4` | Ausstrahlung an Feiertagen |
| `Filler = 5` | Füllmaterial für Lücken — wird in `ReconcileWindow` separat (einzeln, nicht airing-gruppiert) validiert und verliert Eviction immer |

## `ProgramRuleRecurrence`
Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | Jeden Tag |
| `Weekly = 1` | Wöchentlich am `DayOfWeek` |
| `Once = 2` | Einmalig am `SingleDate` |
| `Holiday = 3` | An Feiertagen (`GermanHolidayCalendar`, bundeslandabhängig) |
| `Weekday = 4` | Montag–Freitag |
| `Weekend = 5` | Samstag/Sonntag |

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie (`MaterialId` → `TVShows`) |
| `Movie = 1` | Einzelner Film |
| `MovieCollection = 2` | Film-Sammlung |
| `Genre = 3` | Genre-Union über `ProgramRuleGenres` (+`ProgramRuleMediaSources`-Filter) |
| `Playlist = 4` | Benutzer-Playlist |
| `Trailer = 5` | Trailer anstehender Filme — globales Material ohne `MaterialId`; nur Füller-Regeln |
| `MediaSourceClips = 6` | Unklassifizierte Video-`MediaItem`s einer Quelle (`MaterialId` → `MediaSources`); nur Füller-Regeln |

## `ProgramCursorContentType`
Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | Cursor verweist auf `Movie` |
| `TVShowEpisode = 1` | Auf `TVShowEpisode` |
| `PlaylistEntry = 2` | Auf `PlaylistEntry` |
| `Trailer = 3` | Auf Trailer-`MediaItem` |
| `MediaItem = 4` | Auf rohes `MediaItem` (Clip-Quelle) |

Steuert zugleich die Ring-Ordnung: `PreservesOrder` gilt nur für `TVShowEpisode`/`PlaylistEntry`; `IsUnorderedRing` für reine `Movie`/`MediaItem`-Ringe (`MixUnorderedRing`).

## `ProgramScheduleContentScope`
Datei: `VideoWebPlayer/Data/ProgramScheduleContentScope.cs`

| Wert | Bedeutung |
|------|-----------|
| `SharedOnly = 0` | Nur für alle Nicht-Admin-Benutzer freigegebene Inhalte sind planbar (Default/Fallback) |
| `AllContent = 1` | Freigabeprüfung aufgehoben — jede Expansion ist planbar |

## `ProgramScheduleUnknownRatingMode`
Datei: `VideoWebPlayer/Data/ProgramScheduleUnknownRatingMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `RestrictToNightWindow = 0` | Material ohne bekannte Freigabe nur im FSK-18-Nachtfenster (Default/Fallback) |
| `Unrestricted = 1` | Unbekannte Freigabe jederzeit sendefähig |
