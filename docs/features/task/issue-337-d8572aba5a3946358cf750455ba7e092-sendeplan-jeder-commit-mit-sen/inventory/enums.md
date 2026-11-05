# Enums — Bestandsaufnahme Issue #337

## `ProgramSlotStatus`
Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | Slot ist Teil des wirksamen Sendeplans. |
| `Displaced = 1` | Durch Plan-Neubewertung verdrängt; bleibt als Historie, blockiert keine Ersatz-Slots. |

Relevant, weil `Reconcile.SlotsDisplaced`/`SlotsCreated` und `Coverage.SlotsCreated` je `ExecutePassAsync`-Lauf entscheiden, ob `NotifyLiveScheduleChanged()` feuert.

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serien-Fixpunkt |
| `MoviePrimeTime = 1` | Film-Primetime |
| `GenreRotation = 2` | Genre-Rotation |
| `Repeat = 3` | Wiederholung |
| `HolidaySpecial = 4` | Feiertags-Special |
| `Filler = 5` | Füller |

## `ProgramRuleRecurrence`
Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | Täglich |
| `Weekly = 1` | Wöchentlich |
| `Once = 2` | Einmalig |
| `Holiday = 3` | Feiertag |
| `Weekday = 4` | Werktags |
| `Weekend = 5` | Wochenende |

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Serie |
| `Movie = 1` | Film |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre |
| `Playlist = 4` | Playlist |
| `Trailer = 5` | Trailer (System-Clip-Gruppe) |
| `MediaSourceClips = 6` | Clip-Quelle aus Medienquelle |

## `ProgramRuleValidityMode`
Datei: `VideoWebPlayer/Data/ProgramRuleValidityMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unrestricted = 0` | Unbegrenzt gültig |
| `OneTime = 1` | Einmalig |
| `AnnualWindow = 2` | Jährliches Fenster |
| `MonthlyWindow = 3` | Monatliches Fenster |
| `WeeklyWindow = 4` | Wöchentliches Fenster |
| `HolidayWindow = 5` | Feiertagsfenster |

## `ProgramScheduleContentScope`
Datei: `VideoWebPlayer/Data/ProgramScheduleContentScope.cs`

| Wert | Bedeutung |
|------|-----------|
| `SharedOnly = 0` | Nur freigegebene Inhalte |
| `AllContent = 1` | Alle Inhalte |

Änderung in `TVSenderAdmin.razor` löst über `SaveChannelSettingsAsync` + `NotifyScheduleChanged()` eine Plan-Neubewertung aus.

## `ProgramScheduleUnknownRatingMode`
Datei: `VideoWebPlayer/Data/ProgramScheduleUnknownRatingMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `RestrictToNightWindow = 0` | Ohne Freigabe nur im Nachtfenster |
| `Unrestricted = 1` | Ohne Freigabe überall planbar |

## `LiveStreamSyncAction`
Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` (im selben File, Z. 9 ff.)

| Wert | Bedeutung |
|------|-----------|
| `Unchanged` | Laufende Segmentierung passt bereits zum Plan. |
| `Started` | Segmentierung für die aktuelle Sendung (neu) gestartet. |
| `Stopped` | Segmentierung gestoppt (Sendelücke oder nicht auflösbares Medium). |
| `FfmpegUnavailable` | ffmpeg nicht auffindbar — keine Segmentierung möglich. |
| `MediaUnavailable` | Medium der aktuellen Sendung nicht auflösbar. |
| `InputUnplayable` | Medium aufgelöst, aber für ffmpeg undekodierbar — Slot wird bis zu seinem Ende übersprungen. |

Steuert Logging und `WakeAtUtc` im `LiveStreamWorker` — indirekt betroffen, weil `WaitLiveAsync` auch von `NotifyScheduleChanged` gespeist wird.

## `MediaTypeValues` (Konstanten, kein Enum)
Datei: `VideoWebPlayer.Client/Models/MediaTypeValues.cs`

String-Konstanten `Movie`, `TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection` — davon nutzt der Commit-Hook `TVShow`, `TVShowSeason` und `MovieCollection` für `PlaylistBackfillMarker.MediaType`.
