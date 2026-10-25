# Enums — Bestandsaufnahme

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serie zu fester Sendezeit |
| `MoviePrimeTime = 1` | Film zur Primetime |
| `GenreRotation = 2` | Genre-/Formatrotation |
| `Repeat = 3` | Wiederholung |
| `HolidaySpecial = 4` | Feiertags-Special |
| `Filler = 5` | Füller (`FillGaps`); trägt heute `Trailer`-/`MediaSourceClips`-Material |

Gespeichert in `ProgramRule.RuleType` und denormalisiert in `ProgramSlot.RuleType` (Füller-Erkennung nach Regel-Löschung; `FillGaps` und `ReconcileWindow.Rebuild` werten `Filler` aus).

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie |
| `Movie = 1` | Einzelfilm |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre (über `ProgramRuleGenre`-Joins) |
| `Playlist = 4` | Benutzer-Playlist |
| `Trailer = 5` | „Trailer anstehender Filme" — globales Material ohne `MaterialId` |
| `MediaSourceClips = 6` | Unklassifizierte Clip-`MediaItem`s einer Medienquelle (`MaterialId` = `MediaSources.Id`) |

Kein gruppenspezifischer Wert vorhanden; Clip-Gruppen sind laut Anforderung eine Zusatz-Zuordnung, keine neue Materialart.

## `ProgramRuleRecurrence`
Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | Täglich |
| `Weekly = 1` | Wöchentlich (`DayOfWeek` Pflicht) |
| `Once = 2` | Einmalig (`SingleDate` Pflicht) |
| `Holiday = 3` | An Feiertagen (`GermanHolidayCalendar` + `HolidayFederalState`) |
| `Weekday = 4` | Werktags (Mo–Fr) |
| `Weekend = 5` | Wochenende (Sa+So) |

## `ProgramSlotStatus`
Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | Belegt Sendezeit |
| `Displaced = 1` | Verdrängt, nur Nachvollziehbarkeit (Default-Restore-Wert `Scheduled` in `OptionalRestoreIntDefaults`) |

## `ProgramCursorContentType`
Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | `LastContentId` = `Movie.Id` |
| `TVShowEpisode = 1` | `TVShowEpisode.Id` |
| `PlaylistEntry = 2` | `PlaylistEntry.Id` |
| `Trailer = 3` | `MediaItem.Id` eines `MovieMediaItem`-Links mit `IsTrailer` |
| `MediaItem = 4` | `MediaItem.Id` eines Clip-Quellen-Items (`MediaSourceClips`) — für einen eventuellen Clip-Gruppen-Fortschritt wiederverwendbar |

## Vertraglich gepinnte Enums (Hinweis)
`VideoWebPlayer.Tests/EnumContractTests.cs` pinnt `RecentEntryType` (Client/Server-Gleichstand), `ManualBackupJobStatus`, `RestoreBackupJobStatus`, `PairingCodeKind`. **Keine** Pinning-Tests für die `ProgramRule*`/`ProgramSlot*`/`ProgramCursorContentType`-Enums vorhanden — neue Enum-Werte müssen dennoch als persistierte Zahlenwerte behandelt werden (DB-`int`-Spalten, Backup-Roundtrip).
