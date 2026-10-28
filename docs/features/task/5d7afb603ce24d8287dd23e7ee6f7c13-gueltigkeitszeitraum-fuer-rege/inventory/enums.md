# Enums — Bestandsaufnahme

**Es existiert kein `ProgramRuleValidityMode`** — ein Gültigkeitsmodus (uneingeschränkt / einmaliger Zeitraum / jährlich wiederkehrend) wäre neu anzulegen. Bestehende Enums im Umfeld:

## `ProgramRuleRecurrence`
Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | Jeden Tag |
| `Weekly = 1` | Wöchentlich am `DayOfWeek` |
| `Once = 2` | Einmalig am `SingleDate` |
| `Holiday = 3` | An Feiertagen (`GermanHolidayCalendar`) |
| `Weekday = 4` | Werktags (Mo–Fr) |
| `Weekend = 5` | Wochenende (Sa+So) |

Der Sende-Rhythmus — fachlich getrennt von der angeforderten Gültigkeit (Anforderung: Gültigkeit ist zusätzliche Dimension, kein neuer `Recurrence`-Wert). Ausgewertet ausschließlich in `ProgramScheduleService.BuildOccurrences` (Z. 1626–1635).

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serien-Sendezeit |
| `MoviePrimeTime = 1` | Film zur Primetime |
| `GenreRotation = 2` | Genre-/Formatrotation |
| `Repeat = 3` | Wiederholung |
| `HolidaySpecial = 4` | Feiertags-Special |
| `Filler = 5` | Platzhalter/Füller — hat keine eigenen Occurrences, sondern füllt Lücken (`FillGaps`) |

Offene Frage 8 der Anforderung: Gilt die Gültigkeit auch für `Filler`-Regeln? (Annahme dort: ja.)

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie |
| `Movie = 1` | Einzelfilm |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre |
| `Playlist = 4` | Benutzer-Playlist |
| `Trailer = 5` | Trailer anstehender Filme (kein `MaterialId`) |
| `MediaSourceClips = 6` | Unklassifizierte Videos einer Quelle |

## `ProgramSlotStatus`
Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | Teil des wirksamen Sendeplans |
| `Displaced = 1` | Verdrängt durch Neu-Bewertung; Historie, belegt keine Sendezeit — Zielstatus der Slots einer gelöschten/ungültigen Regel nach dem nächsten `ReconcileAsync` |

## `ProgramCursorContentType`
Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0`, `TVShowEpisode = 1`, `PlaylistEntry = 2`, `Trailer = 3`, `MediaItem = 4` | Inhaltstyp des `ProgramRuleCursor`-Fortsetzungsstands |

Hinweis: `EnumContractTests` (VideoWebPlayer.Tests) pinnen nur `RecentEntryType`, `ManualBackupJobStatus`, `RestoreBackupJobStatus`, `FfmpegToolDownloadJobStatus`, `PairingCodeKind` — ProgramRule-Enums sind dort **nicht** abgedeckt.
