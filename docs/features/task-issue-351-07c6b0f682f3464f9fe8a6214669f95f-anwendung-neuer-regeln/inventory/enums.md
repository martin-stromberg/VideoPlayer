# Enums

## `ProgramSlotStatus`

Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | Teil des wirksamen Sendeplans; belegt Sendezeit |
| `Displaced = 1` | Von einer Neu-Bewertung verdrängt; nur Historie + Verschiebe-Evidenz (`_occupiedEvidence`), blockiert keine Belegung |

## `ProgramRuleType`

Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serien-Slot zur festen Sendezeit |
| `MoviePrimeTime = 1` | Primetime-Film |
| `GenreRotation = 2` | Genre-/Formatrotation (interleaved-Strang-Mechanik) |
| `Repeat = 3` | Wiederholung |
| `HolidaySpecial = 4` | Feiertags-Sonderprogramm |
| `Filler = 5` | Füllmaterial — verliert in `Evicts` immer; hat keine Occurrences (`BuildOccurrences` wird nur für `!= Filler` aufgerufen), füllt per `FillGaps` |

## `ProgramRuleRecurrence`

Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | täglich |
| `Weekly = 1` | wöchentlich an `DayOfWeek` |
| `Once = 2` | einmalig an `SingleDate` |
| `Holiday = 3` | an Feiertagen (`GermanHolidayCalendar`) |
| `Weekday = 4` | Mo–Fr |
| `Weekend = 5` | Sa/So |

## `ProgramRuleMaterialType`

Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | ganze Serie (geordneter Episoden-Strang) |
| `Movie = 1` | Einzelfilm |
| `MovieCollection = 2` | Filmsammlung → geordneter `collection:{id}`-Strang (`Sequenced`), Releasedatum-Reihenfolge |
| `Genre = 3` | Genre-Union → interleaved Stränge `show:{id}`/`collection:{id}`/`other` |
| `Playlist = 4` | Benutzer-Playlist (`ProgramCursorContentType.PlaylistEntry`) |
| `Trailer = 5` | Trailer kommender Filme (verzögerte Expansion über `airingsByMovie`) |
| `MediaSourceClips = 6` | unklassifizierte Clips einer Quelle |

## `ProgramRuleValidityMode`

Datei: `VideoWebPlayer/Data/ProgramRuleValidityMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unrestricted = 0` | immer gültig |
| `OneTime = 1` | einmaliger Zeitraum `ValidFrom`–`ValidTo` (einseitig offen erlaubt); einziger ablauffähiger Modus (`IsExpired`) |
| `AnnualWindow = 2` | jährlich wiederkehrendes Monat-Tag-Fenster (Wrap übers Jahr) |
| `MonthlyWindow = 3` | monatlich wiederkehrendes Tag-Fenster (Wrap übers Monatsende) |
| `WeeklyWindow = 4` | Wochentags-Bitmaske `ValidWeekdays` |
| `HolidayWindow = 5` | Tage um `ValidHolidayAnchor` ± before/after |

## `ProgramCursorContentType`

Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | `LastContentId` = `Movie.Id` (auch Sammlungsfilme) |
| `TVShowEpisode = 1` | `LastContentId` = `TVShowEpisode.Id` |
| `PlaylistEntry = 2` | `LastContentId` = `PlaylistEntry.Id` |
| `Trailer = 3` | `LastContentId` = Trailer-`MediaItemId` |
| `MediaItem = 4` | `LastContentId` = rohes `MediaItem` (Clip-Quelle/Clip-Gruppen) |

## `AiringInvalidReason`

Datei: `VideoWebPlayer/Services/AiringInvalidReason.cs` (`internal`, stufengeordnet — nur Diagnose)

| Wert | Bedeutung |
|------|-----------|
| `None` | gültig |
| `RuleMissing` / `RuleInactive` | Erzeuger-Regel weg/inaktiv |
| `NoOccurrenceAnchor` | keine Occurrence im geladenen Fenster |
| `OutsideOccurrenceWindow` / `ClipChainBroken` / `HeadPickInconsistent` / `RemainderNotReproducible` | **nur Stufenordnung** — Relikte der entfernten Re-Simulation, werden nie erzeugt |
| `TitlelessAiring` | Gruppe ohne Titel-Slot |
| `ItemNotInExpansion` | Inhalt nicht mehr expandiert |
| `StrandSequenceBroken` | Ring-Kontiguität geordneter Strang gebrochen |
| `AnchorWithoutInvaderEvidence` | verschobener Start ohne Invader-Beweis/außerhalb Headroom |
| `SlotSequenceMismatch` | Laufzeit- oder Altersfenster-Bruch eines Slots |

## Weitere (peripher relevant)

- `ProgramScheduleContentScope` (`VideoWebPlayer/Data/ProgramScheduleContentScope.cs`):
  `SharedOnly`/`AllContent` — steuert `LoadAccessContextAsync`.
- `ProgramScheduleUnknownRatingMode` (`VideoWebPlayer/Data/ProgramScheduleUnknownRatingMode.cs`):
  `RestrictToNightWindow`/`Unrestricted` — Altersfenster für unbewertetes Material.
- `ProgramRuleHolidayAnchor` — benannte Feiertagsanker für `HolidayWindow`.
