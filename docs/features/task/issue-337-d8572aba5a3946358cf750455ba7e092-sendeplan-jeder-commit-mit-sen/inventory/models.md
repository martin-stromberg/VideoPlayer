# Datenmodell — Bestandsaufnahme Issue #337

Betroffen sind die sendeplan-relevanten Entitäten, deren Commits `IProgramScheduleSignal.NotifyScheduleChanged()` auslösen, sowie der `PlaylistBackfillMarker`, über dessen Schreiben der zentrale `SaveChanges`-Hook das Signal feuert.

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | Fremdschlüssel auf den (einzigen) `TVChannel` |
| `Name` | `string` (required) | Anzeigename der Regel |
| `RuleType` | `ProgramRuleType` | Regelart (Serie, Film, Genre-Rotation, Füller u. a.) |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster (täglich, wöchentlich, einmalig, Feiertag u. a.) |
| `DayOfWeek` | `DayOfWeek?` | Wochentag bei Weekly-Regeln |
| `SingleDate` | `DateOnly?` | Datum bei Once-Regeln |
| `TimeOfDay` | `TimeOnly` | Sendezeit |
| `EpisodeCount` | `int?` | Episoden je Sendeplatz |
| `SlotDuration` | `TimeSpan?` | Fixe Slot-Dauer |
| `MaterialType` | `ProgramRuleMaterialType` | Art des Quellmaterials |
| `MaterialId` | `long` | Id des Quellmaterials (Show, Film, Genre, Playlist, Clip-Gruppe u. a.) |
| `Genres` / `MediaSourceFilters` / `ClipGroups` | `ICollection<...>` | Verknüpfungstabellen (Genres, Quellfilter, Clip-Gruppen) |
| `PlaceholderGrace` | `TimeSpan` | Kulanzzeit für Platzhalter |
| `Priority` | `int` | Verdrängungspriorität |
| `IsActive` | `bool` | Regel aktiv/inaktiv |
| `ValidityMode` + `ValidFrom`/`ValidTo`/`ValidFromDay`/`ValidToDay`/`ValidWeekdays`/`ValidHolidayAnchor`/`ValidHolidayBeforeDays`/`ValidHolidayAfterDays` | `ProgramRuleValidityMode`, `DateOnly?`, `int?`, `ProgramRuleHolidayAnchor?` | Gültigkeitsfenster der Regel |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

CRUD auf dieser Entität läuft über `ProgramRuleService`; jede Mutation endet in `NotifyScheduleChanged()` (vgl. `logic.md`).

## `ProgramClipGroup` / `ProgramClipGroupItem`
Dateien: `VideoWebPlayer/Data/ProgramClipGroup.cs`, `VideoWebPlayer/Data/ProgramClipGroupItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProgramClipGroup.Id` | `long` | Primärschlüssel |
| `ProgramClipGroup.Name` | `string` (required) | Gruppenname; reservierter Systemname „Trailer“ (`TrailerGroupName`, `NameMaxLength`) |
| `ProgramClipGroup.IsSystem` | `bool` | System-Gruppen sind gesperrt (kein Rename/Delete/Remove) |
| `ProgramClipGroup.CreatedAt`/`UpdatedAt` | `DateTime` | Zeitstempel |
| `ProgramClipGroupItem.ProgramClipGroupId` | `long` | Fremdschlüssel auf die Gruppe |
| `ProgramClipGroupItem.MediaItemId` | `long` | Fremdschlüssel auf das `MediaItem` (Clip) |

CRUD läuft über `ProgramClipGroupService`; jede Mutation endet in `NotifyScheduleChanged()`.

## `PlaylistBackfillMarker`
Datei: `VideoWebPlayer/Data/PlaylistBackfillMarker.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `MediaType` | `string` (required) | `TVShow`, `TVShowSeason` oder `MovieCollection` (`MediaTypeValues`) |
| `MediaId` | `long` | Id des markierten Sammel-Mediums |
| `MarkedAt` | `DateTime` | Zeitpunkt der letzten Markierung (UTC) |
| `Version` | `long` | Zähler, wird je Re-Mark inkrementiert (Unique-Index auf `MediaType`+`MediaId`, Upsert `ON CONFLICT DO UPDATE`) |

Der `SaveChanges`-Hook (`ApplicationDbContext.PlaylistBackfillMarking.cs`) feuert `NotifyScheduleChanged()` genau dann, wenn in demselben Commit tatsächlich Marker geschrieben wurden — also bei neuen oder umgehängten `Movie`-, `TVShowSeason`- und `TVShowEpisode`-Entitäten.

## `Movie` / `TVShowSeason` / `TVShowEpisode` (relevante Fremdschlüssel des Hooks)
Dateien: `VideoWebPlayer/Data/Movie.cs`, `VideoWebPlayer/Data/TVShowSeason.cs`, `VideoWebPlayer/Data/TVShowEpisode.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Movie.MovieCollectionId` | `long?` | Beim Hinzufügen/Ändern markiert der Hook die (neue) Filmsammlung |
| `TVShowSeason.TVShowId` | `long` | Beim Hinzufügen/Ändern markiert der Hook die Serie |
| `TVShowEpisode.TVShowSeasonId` | `long` | Beim Hinzufügen/Ändern markiert der Hook Season und Serie |

Nur `EntityState.Added`-/`Modified`-Einträge mit gesetztem, nicht-temporärem Fremdschlüssel lösen eine Markierung aus (`TryGetChangedForeignKey`).

## `MediaItem` (sendeplan-relevante Teilfelder)
Datei: `VideoWebPlayer/Data/MediaItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Duration` | `TimeSpan?` | Erst gesetzte Laufzeit macht das Item planbar — `MediaDurationProbeService.ProbePendingAsync` feuert danach `NotifyScheduleChanged()` |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt des letzten Probe-Versuchs |
| `DurationProbeAttempts` | `int` | Versuchszähler (Retry-/Abbruchlogik) |

## `ProgramSlot` (Ergebnis des gebündelten Laufs)
Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | Fremdschlüssel auf den `TVChannel` |
| `Start` / `End` | `DateTime` | Sendefenster |
| `MediaItemId` | `long` | Gesendetes Medium |
| `MovieId` / `TVShowEpisodeId` | `long?` | Inhaltsbezug |
| `ProgramRuleId` | `long?` | Erzeugende Regel |
| `RuleType` | `ProgramRuleType` | Denormalisierte Regelart |
| `Status` | `ProgramSlotStatus` | `Scheduled` oder `Displaced` (Verdrängungshistorie) |
| `IsClipFill` | `bool` | Clip-Füller |
| `CreatedAt` | `DateTime` | Zeitstempel |

Die Zahl verdrängter/erzeugter Slots je `ExecutePassAsync`-Lauf entscheidet, ob der `ProgramScheduleWorker` anschließend `NotifyLiveScheduleChanged()` feuert (kein Eingabe-Commit-Signal, bleibt unmittelbar).

## `Setup` (relevante Schalter)
Datei: `VideoWebPlayer/Data/Setup.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TvSenderEnabled` | `bool` | Feature-Flag; wird je Worker-Iteration gelesen — Signalweckung bleibt auch bei ausgeschaltetem Flag bestehen |
| `ProgramScheduleIntervalMinutes` | `int` (Default 15) | Generierungsintervall des `ProgramScheduleWorker`, gelesen via `ProgramSettingsService.GetProgramScheduleIntervalAsync` |
