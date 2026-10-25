# Logik — Bestandsaufnahme

## `ProgramScheduleService`
Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (2185 Zeilen) — implementiert `IProgramScheduleService`; erzeugt den lückenlosen Sendeplan extend-only (`EnsureCoverageAsync`) und bewertet ihn neu (`ReconcileAsync`).

**Konstruktor (Z. 48–65):** `ApplicationDbContext`, `IProgramRuleService`, `ProgramSettingsService`, `ILogger`, optional `IOptions<MediaProbeOptions>` (`_maxProbeAttempts`) und `IOptions<ProgramScheduleOptions>` (`_trailerLead`, `_minStartRemain`, `_maxOccurrenceLookback`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` | public (Z. 68) | Extend-only-Lauf: `coverageStart = max(now, lastEnd)`, Occurrences bauen, `MaterializeOccurrence` je Vorkommen, dann `ExpandTrailerStatesAsync` + `FillGaps`, `PersistCursors`, speichern |
| `ReconcileAsync` | public (Z. 136) | Neu-Bewertung: `ReconcileWindow` mit Lookback, `AiringReproducible`-Validierung, Suffix-Verdrängung, `MaterializeOccurrenceEvicting`, `RewindCursor`, erneutes `FillGaps` |
| `GetScheduleAsync` | public (Z. 360) | Slots ab `fromUtc` für die Admin-Sendeplansicht |
| `GetRuleDiagnosesAsync` | public (Z. 369) | Pro-Regel-Diagnose (`ProgramRuleDiagnosis`); `RequireAdminAsync` |
| `DeleteDisplacedSlotsAsync` | public (Z. 405) | Löscht `Displaced`-Slots; `RequireAdminAsync` |
| `ResetScheduleAsync` | public (Z. 416) | Löscht alle Slots + Cursors; `RequireAdminAsync` |
| `RequireAdminAsync` | private (Z. 433) | Serverseitige Admin-Erzwingung (`UnauthorizedAccessException`/`ForbiddenAccessException`) |
| `GroupAirings` | private static (Z. 446) | Gruppiert Slots zu Ausstrahlungen (gleiche Regel, lückenlos) |
| `AiringReproducible` | private static (Z. 490/500) | Reproduzierbarkeitsprüfung: spielt `SelectAiringItems`/`SelectAiringItemsInterleaved` je Kandidaten-Occurrence-Start erneut und vergleicht slotweise (Memo über `ReproContext`) |
| `FlatAiringTailReproducible` | private static (Z. 514) | Rückwärts-Prefix-Hypothesen für Einzelstrang-Ausstrahlungen; nutzt `SelectAiringItems` mit `minStartRemain` |
| `InterleavedAiringTailReproducible` | private static (Z. 595) | Pendant für Multi-Strang-Regeln; nutzt `SelectAiringItemsInterleaved` |
| `FindItemIndex` | private static (Z. 741) | Slot→Element-Zuordnung: `MediaItemId`, sonst `MovieId`/`TVShowEpisodeId`-Content-Ref |
| `RewindCursor` | private static (Z. 756) | Cursor-Rücklauf auf Vorgänger des frühesten verdrängten Inhalts je Strang |
| `ReconcileWindow` (Klasse) | private (Z. 793) | Fenster-Zustand des Reconcile: `Reload` → `Rebuild` + `Validate` atomar |
| `MaterializeOccurrenceEvicting` | private (Z. 884) | Wie `MaterializeOccurrence`, liefert aber verlierende Bestands-Slots zurück (Reconcile) |
| `SelectItems` | private (Z. 1327) | Gemeinsamer Auswahl-Einstieg: flach (`SelectAiringItems`) oder interleaved (`SelectAiringItemsInterleaved`), beide mit `_minStartRemain` |
| `CommitTaken` | private static (Z. 1348) | Committet die Auswahl erst nach bestandener Kollisionsprüfung |
| `SelectAiringItems` | private static (Z. 1435) | **Startschwelle:** `bound.StartUtc - slotStart < minStartRemain` → Abbruch („the rest goes to fillers", Z. 1456); `b.Hard && slotStart + item.Duration > b.StartUtc` → Abbruch; begonnene Items laufen zu Ende (Soft-Bound-Overshoot); `EpisodeCount` > `SlotDuration` > 1 Item |
| `SelectAiringItemsInterleaved` | private static (Z. 1369) | Round-Robin über Stränge; dieselbe Bound-Logik je Strang (Z. 1404); `StrandStartOffset` für `EpisodeCount < Strangzahl` |
| `BoundStarts` / `BoundaryAfter` | private static (Z. 1475/1489) | Fenstergrenzen aus Occurrence-Starts; `Hard` = streng höhere Priorität |
| `MaterializeOccurrence` | private (Z. 1262) | `ShiftPastInvaders`, `SelectItems`, atomare Kollisionsprüfung (ganze Ausstrahlung verworfen bei Konflikt), Slots schreiben, `CommitTaken` |
| `ShiftPastInvaders` | private static (Z. 1107) | Start hinter den Ende einer überragenden Vorgänger-Ausstrahlung schieben |
| `FillGaps` | private (Z. 1497) | Füllt Lücken **zwischen** Ausstrahlungen über `ProgramRuleType.Filler`-Regeln; `Trailer`-Füller nur mit Vorlauf-Treffer (`FindLeadMatchOffset`, `_trailerLead`); Füller müssen in die Lücke passen, am Horizont-Ende darf der letzte überragen (Z. 1561); Restlücke → `summary.RemainingGapSeconds` |
| `GapSeconds` | private static (Z. 1590) | Restlücken-Sekunden für `RemainingGapSeconds` |
| `PersistCursors` | private (Z. 1608) | Schreibt je Strang eine `ProgramRuleCursor`-Zeile (NULL-`StrandKey` für flache Regeln) |
| `StrandOrderKey` | internal static (Z. 1074) | FNV-1a-Hash über `ruleId:strandKey` — prozess-stabile Strangreihenfolge |
| `SelectStrandCursor` | internal static (Z. 1092) | Cursor-Auswahl je Strang (jüngste `UpdatedAt`, dann höchste `Id`) |
| `StrandStartOffset` | private static (Z. 1102) | Startstrang je Ausstrahlungstag für `EpisodeCount < Stränge` |
| `BuildOccurrences` | private static (Z. 1228) | Vorkommen je Regel über Wiederholungsmuster (lokale Zeitzone, `GermanHolidayCalendar`) |
| `NextIndexFor` | private static (Z. 1219) | Ringindex hinter der Cursor-Position |
| `LoadAccessContextAsync` / `AccessContext` | private (Z. 1164/1150) | Publikums-Freigabe-Snapshot: planbar = für **alle** Nicht-Admins sichtbar |
| `ExpandMaterialAsync` | private (Z. 1641/1644) | Dispatch je `MaterialType` auf die `Expand*Async`-Methoden; leere Expansion → `OrphanedRules++` + Log |
| `ExpandShowAsync` | private (Z. 1738) | Episoden einer Serie in Air-Order |
| `ExpandMovieAsync` / `ExpandMovieCollectionAsync` | private (Z. 1782/1796) | Einzelfilm / Sammlung (Release-Reihenfolge) |
| `ExpandGenreAsync` | private (Z. 1827) | Genre-Union über `GenreCanonicalResolver` + Quell-Filter; Stränge `show:{id}` / `other` |
| `ExpandTrailersAsync` | private (Z. 1952) | **Trailer-Kandidatenmenge:** `MovieMediaItems` mit `IsTrailer`, Dauer erforderlich, Freigabe, Film muss im bedeckten Fenster ausgestrahlt werden (`airingsByMovie`, Start > `coverageStart`); Dedupe je `MediaItemId`, sortiert nach nächster Ausstrahlung |
| `ExpandTrailerStatesAsync` | private (Z. 1929) | Verzögerte Trailer-Expansion **nach** der Materialisierung (frisch geplante Filmausstrahlungen zählen) — Vorbild für eine verzögerte „Trailer"-Gruppen-Expansion |
| `FindLeadMatchOffset` | private static (Z. 1999) | Index des ersten Trailer-Items, dessen Film in `(fillAt, fillAt+lead]` ausgestrahlt wird |
| `TrailerMediaItemIdsAsync` | private (Z. 2012) | Alle Trailer-`MediaItemId`s (schließt Trailer-Slots aus der „Filmausstrahlung"-Menge aus) |
| `ExpandMediaSourceClipsAsync` | private (Z. 2024) | **Clip-Kandidatenmenge:** unklassifizierte Video-`MediaItem`s einer Quelle (kein `MovieMediaItem`-/`TVShowEpisodeMediaItem`-Link, `IsVideoFileName`), `Duration` Pflicht (`CountNoDuration`), `access.Allows(sourceId,…)`, `ProgramCursorContentType.MediaItem` |
| `ExpandPlaylistAsync` | private (Z. 2113) | Playlist-Material (SortMode, verwaiste Einträge still übersprungen) |
| `MovieMediaItemsAsync` / `EpisodeMediaItemsAsync` | private (Z. 1675/1688) | Repräsentative Media-Item-Links inkl. Probe-Status |
| `CountNoDuration` / `TryMovieItem` / `ToItem` | private (Z. 1704/1721/1765) | Skip-Buchhaltung (`SkippedNoDuration`, `ProbePending`, `ProbeExhausted`, `NoMediaItem`) und Item-Erzeugung mit Freigabeprüfung |
| `LoadMovieAiringsAsync` / `BuildMovieAiringsFromSlotsAsync` / `BuildMovieAiringsMap` | private (Z. 2075/2088/2098) | Film-Ausstrahlungsstarts je `MovieId` (Scheduled-Slots > `afterUtc`, Trailer-MediaItems ausgenommen) |

**Interne Typen:** `SchedulableItem` (Z. 958: `ContentType`, `ContentId`, `MediaItemId`, `MovieId`, `TVShowEpisodeId`, `Duration`, `StrandKey`), `StrandState` (Z. 975: `PeekAt`, `Commit`, `RewindTo`, `LastPlaced`), `RuleState` (Z. 995: `Items`, `Strands`, `IsInterleaved`, `SetItems` — Rebuild bei Trailer-Nachexpansion), `Occurrence` (Z. 1059), `Interval` (Z. 1061), `ReproContext` (Z. 467), `SummaryCounters` (Z. 1125: u. a. `RemainingGapSeconds`, `DroppedConflicts`, Diagnosezähler).

**Kein Mechanismus für In-Perioden-Auffüllung oder Karenzzeit vorhanden:** `SelectAiringItems`/`SelectAiringItemsInterleaved` kennen nur `_minStartRemain` (global, `ProgramSchedule:MinStartRemainSeconds`, Default 30 s); eingefügte Slots entstehen ausschließlich durch Titel-Auswahl und `FillGaps` zwischen Ausstrahlungen. Es gibt keine Gruppen-Expansion und keine Clip-Slots innerhalb einer Regel-Periode.

**Events:** publiziert keine; wird vom `ProgramScheduleWorker` auf Signal/Intervall aufgerufen.

## `ProgramRuleService`
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs` — implementiert `IProgramRuleService`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllowedMaterialTypes` | public (Z. 39) | Materialarten je `RuleType`; `Trailer`/`MediaSourceClips` nur für `Filler` |
| `GetOrCreateChannelAsync` | public (Z. 52) | Liefert den einzigen Sender, legt „TV-Sender" an |
| `RenameChannelAsync` | public (Z. 65) | Admin |
| `GetRulesAsync` | public (Z. 78) | Regeln inkl. `Genres`+`Genre` und `MediaSourceFilters`+`MediaSource`; Admin |
| `GetGenreMaterialOptionsAsync` | public (Z. 94) | Wählbare Genre-Optionen via `GenreCanonicalResolver.BuildOptions`; Admin |
| `CreateRuleAsync` | public (Z. 108) | Validierung → Insert → `ReplaceGenreLinksAsync` → `NotifyScheduleChanged`; Admin |
| `UpdateRuleAsync` | public (Z. 144) | dito auf Bestand |
| `MaterialIdFor` | private static (Z. 174) | Bei Genre: erste `GenreIds`-Id in `MaterialId` (Fallback-Pfad) |
| `ReplaceGenreLinksAsync` | private (Z. 181) | **Muster für Gruppen-Links:** ersetzt `ProgramRuleGenres`+`ProgramRuleMediaSources` nur bei Genre-Material, sonst löscht er sie |
| `SetRuleActiveAsync` / `DeleteRuleAsync` | public (Z. 198/213) | Admin; beide mit `NotifyScheduleChanged` |
| `RequireAdminAsync` | private (Z. 225) | Serverseitige Admin-Erzwingung |
| `ValidateRuleInputAsync` | private (Z. 234) | Name, Enums, Wiederholung, Länge/Folgenzahl, Materialart-Matrix, Material-Existenz (`Trailer` → immer `true`, `MediaSourceClips` → `MediaSources.AnyAsync`) |
| `ValidateGenreInputAsync` | private (Z. 291) | Genre-Ids ≥1, Existenz Genre/Quelle — Muster für Gruppen-Existenzprüfung |
| `ValidateChannelName` | private static (Z. 310) | Kanalname |

**Publizierte Signale:** `IProgramScheduleSignal.NotifyScheduleChanged()` nach jeder Regel-Änderung (Z. 139, 168, 208, 222). **Kein** Vorrat an Gruppen-Parametern in `ProgramRuleInput` und keine „Trailer"-Namensreservierung.

## `ProgramGuideService`
Datei: `VideoWebPlayer/Services/ProgramGuideService.cs` — rein lesende Programmauskunft.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetNowAsync` | public (Z. 37) | Laufender + nächster Slot (`Scheduled` only) |
| `GetUpcomingAsync` | public (Z. 68) | Slots im Vorschaufenster (≤ `MaxPreviewHours` = 12 h) |
| `LoadDetailsAsync` | private (Z. 102) | Lädt Filme/Episoden; **bare `MediaItem.Name`-Fallback** für Slots ohne Inhaltsreferenz (Z. 126–130) — von Clip-Slots wiederverwendbar |
| `ToEntry` | private static (Z. 135) | `IsFiller` aus `RuleType`, Titel-Fallback „Unbekannt" bzw. `MediaItem.Name` (Z. 177) |

## `ProgramScheduleWorker`
Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs` — `BackgroundService`; pro Intervall (`GetProgramScheduleIntervalAsync`, Default 15 min) bei `TvSenderEnabled` `ReconcileAsync` + `EnsureCoverageAsync`, unter `IBackgroundProcessingGate`; Aufwachen per `IProgramScheduleSignal` oder Intervall.

## `ProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` — `Channel<bool>` (bounded 1, DropWrite); `BeginScan` unterdrückt Signale während Medien-Scans (Scan-Ende weckt einmal).

## `ProgramSettingsService` (sender-relevante Methoden)
Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

- `GetTvSenderEnabledAsync` (Z. 195) / `UpdateTvSenderEnabledAsync` (Z. 206) / `IsTvSenderAccessGrantedAsync` (Z. 223) / `GetTvSenderAllowedUserIdsAsync` (Z. 238) / `UpdateTvSenderAllowedUserIdsAsync` (Z. 250)
- `GetHolidayFederalStateAsync` (Z. 317)
- `GetProgramScheduleHorizonAsync` (Z. 345, Default 48 h) / `GetProgramScheduleIntervalAsync` (Z. 357, Default 15 min) / `UpdateProgramScheduleSettingsAsync` (Z. 371)

## `ProgramGuideController`
Datei: `VideoWebPlayer/Controllers/ProgramGuideController.cs` — `api/program-guide`, `[BearerTokenCheck]`, Feature-Release-Check über `GetTvSenderEnabledAsync` (404 wenn aus), `GetNow` (Z. 55), `GetUpcoming` (Z. 78, `hours`-Query, Default 6).

## `TVSenderAdmin` (Blazor-Adminseite)
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (1742 Zeilen)

- Regel-Tabelle (Z. 125–164): Spalten u. a. Umfang (`DurationLabel`), Material (`MaterialLabel`), Planbar (`DiagnosisLabel`).
- Regel-Dialog `#rule-editor` (Z. 172–346): `rule-duration` (Minuten, Z. 239–240) als Muster für ein Karenzzeit-Feld; Material-Picker `#rule-material-picker` (Z. 263) mit `MediaSearchSelector`, `rule-material-playlist`, `rule-material-trailer`-Checkbox (Z. 280–286), `rule-material-clipsource`-Select (Z. 287–296); Genre-Mehrfachauswahl `#rule-material-genre` + Quell-Filter `#rule-material-sources` (Z. 303–333) als Muster für Gruppen-Mehrfachauswahl.
- `RuleEditModel` (Z. 1723): `SelectedGenreIds`/`SelectedMediaSourceIds`-`HashSet`s als Muster; `DurationMinutes` in Minuten.
- `SaveRuleAsync` (Z. 1523) baut `ProgramRuleInput` (Z. 1529–1549); `StartEdit` (Z. 1363) befüllt das Edit-Modell aus der Regel.
- `MaterialLabel` (Z. 1603/1619) inkl. „Clip-Quelle: …"/„Trailer anstehender Filme"; `RuleTypeLabel`, `RecurrenceLabel`, `DurationLabel` (Z. 1646–1676).
- `ReloadScheduleAsync` (Z. 1200) + `SlotCard` (Z. 1283): Karten-Aufbau mit `MediaItem.Name`-Fallback für bare Slots (Z. 1254–1258); `RuleName` (Z. 1301), `DiagnosisLabel` (Z. 1306).
- Kein UI für Clip-Gruppen oder Karenzzeit vorhanden.

## Optionen
- `ProgramScheduleOptions` (`VideoWebPlayer/Configuration/ProgramScheduleOptions.cs`): `TrailerLeadDays` (Default 7, `EffectiveTrailerLead`), `MinStartRemainSeconds` (Default 30 s, `EffectiveMinStartRemain`), `MaxOccurrenceLookbackDays` (Default 7, Clamp 30, `EffectiveMaxOccurrenceLookback`). `appsettings.json` setzt nur `ProgramSchedule:TrailerLeadDays = 7`.
- `MediaProbeOptions` (`VideoWebPlayer/Configuration/MediaProbeOptions.cs`): `MaxAttempts` = 10 (`EffectiveMaxAttempts`), Timeout-/Parallel-Parameter der Dauer-Messung.

## Hilfslogik
- `GenreCanonicalResolver` (`VideoWebPlayer/Services/GenreCanonicalResolver.cs`): `IsVisible`, `HasEffectiveAlternates`, `ResolveCanonicalNames`, `BuildOptions` — Auflösung kanonischer Genre-Namen.
- `GermanHolidayCalendar` (`VideoWebPlayer/Services/GermanHolidayCalendar.cs`): `IsHoliday` für `Recurrence.Holiday`.
- `ProgramGuideEntryText` (`VideoWebPlayer/Services/ProgramGuideEntryText.cs`): `SeasonNumberFromName`, `Subtitle`, `ShortSubtitle`, `FirstGenre`, `NoDescriptionText`.
- `VideoFileExtensions.IsVideoFileName` (`VideoWebPlayer/Utils/VideoFileExtensions.cs`): `.mp4/.avi/.mkv/.mpeg` — Clip-Filter.
- `VideoWebPlayerBackupData` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`): Restore-Pipeline mit `OptionalRestoreTables`/`OptionalRestoreColumns`/`IgnoredRestoreColumns` und Default-Injektion (s. [models.md](models.md)).

**DI-Registrierung** (`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`, Z. 241/263–277/328): `IProgramGuideApiClient`, `ProgramScheduleSignal`+`IProgramScheduleSignal` (Singleton), `ProgramSettingsService`, `IProgramRuleService`, `IProgramScheduleService`, `IProgramGuideService` (scoped), `ProgramScheduleWorker` (HostedService), `ProgramScheduleOptions`-Binding.
