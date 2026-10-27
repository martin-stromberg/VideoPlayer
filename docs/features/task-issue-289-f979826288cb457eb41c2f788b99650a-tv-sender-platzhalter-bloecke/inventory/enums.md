# Bestandsaufnahme: Enums

## `ProgramRuleType`

Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serie zur festen Sendezeit |
| `MoviePrimeTime = 1` | Film zur Primetime |
| `GenreRotation = 2` | Rotation durch Genre-/Format-Inhalte |
| `Repeat = 3` | Wiederholung bereits ausgewählten Materials |
| `HolidaySpecial = 4` | Sondersendung an Feiertagen |
| `Filler = 5` | Füllmaterial für Planlücken |

Bedeutung für die Anforderung: Clip-Füll-Slots tragen **`RuleType` der erzeugenden Regel**
(z. B. `MoviePrimeTime`), nicht `Filler`. In `ProgramGuideService.ToEntry` wird
`IsFiller = slot.RuleType == ProgramRuleType.Filler` gesetzt — Clip-Füll-Slots einer
Nicht-Füller-Regel sind darüber **nicht** als Füller erkennbar. Umgekehrt sind kurze Slots
einer `Filler`-Regel mit Material `MediaSourceClips` (Füller-„Clip-Quelle") per `RuleType ==
Filler` identifizierbar — fachlich ähnliche Clips, anderer Mechanismus (Offene Frage 2 in
requirement.md).

## `ProgramSlotStatus`

Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | Teil des wirksamen Sendeplans |
| `Displaced = 1` | Durch Plan-Neubewertung verdrängt; bleibt als Historie, belegt keine Sendezeit |

Bedeutung: Der Admin-Sendeplan (`GetScheduleAsync`) liefert **beide** Stati (Anzeige
„Verdrängt"); `ProgramGuideService` liefert nur `Scheduled`. Ob `Displaced`-Clip-Slots
gruppiert werden, ist eine offene Frage der Anforderung.

## `ProgramRuleMaterialType`

Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie |
| `Movie = 1` | Einzelner Film |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre (namensbasiert dedupliziert) |
| `Playlist = 4` | Benutzer-Playlist |
| `Trailer = 5` | Trailer geplanter Filme (globales Material, kein `MaterialId`) |
| `MediaSourceClips = 6` | Uneingeordnete Video-`MediaItem`s einer Quelle („Clip-Quelle", `MaterialId` = `MediaSources.Id`) |

## `ProgramRuleRecurrence`, `ProgramCursorContentType`, `ProgramScheduleContentScope`

Kontext-Enums (Wiederholungsmuster, Cursor-Inhaltstyp `MediaItem`/`Movie`/`TVShowEpisode`,
Planungs-Freigabebereich) — für die Clip-Erzeugung relevant (Clip-Items tragen
`ProgramCursorContentType.MediaItem`), von der Anzeige-Gruppierung selbst nicht betroffen.
