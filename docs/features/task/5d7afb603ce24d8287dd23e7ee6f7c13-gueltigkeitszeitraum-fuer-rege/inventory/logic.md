# Logik — Bestandsaufnahme

## `ProgramRuleService`
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs`; Implementiert: `IProgramRuleService`; DI: `AddScoped` (`ServiceCollectionExtensions.cs` Z. 281)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllowedMaterialTypes(ruleType)` | public | Zulässige Materialarten je Regelart (Trailer/Clip-Quelle nur für `Filler`, nicht für `Repeat`/`HolidaySpecial`) |
| `GetOrCreateChannelAsync` | public | Liefert den einen Sender, legt ihn als „TV-Sender" an |
| `RenameChannelAsync` | public | Admin-geschützt (`RequireAdminAsync`) |
| `GetRulesAsync` (Z. 78) | public | Alle Regeln des Senders inkl. `Genres`, `MediaSourceFilters`, `ClipGroups` (eager), **`OrderBy(r => r.Name)`** — bisher einzige Sortierung; kein Gültigkeits-/Status-Nachsortieren |
| `GetGenreMaterialOptionsAsync` (Z. 96) | public | Genre-Optionen über `GenreCanonicalResolver.BuildOptions(..., includeOutOfSeason: true)` — Out-of-Season-Genres bleiben wählbar und werden im Label mit Fenster markiert |
| `CreateRuleAsync` (Z. 110) | public | `RequireAdminAsync` + `ValidateRuleInputAsync`, persistiert Regel, schreibt Genre-/Clip-Group-Links (`ReplaceGenreLinksAsync`, `ReplaceClipGroupLinksAsync`), feuert `_scheduleSignal?.NotifyScheduleChanged()` |
| `UpdateRuleAsync` (Z. 148) | public | Wie `CreateRuleAsync` auf Bestandsregel; `DayOfWeek`/`SingleDate` werden nur bei passendem `Recurrence` übernommen, sonst `null` |
| `SetRuleActiveAsync` (Z. 216) | public | Toggle `IsActive` + Signal |
| `DeleteRuleAsync` (Z. 231) | public | `_db.ProgramRules.Remove(rule)` + Signal — **einziger bestehender Löschpfad** einer Regel; kaskadiert `ProgramRuleCursor` und Join-Zeilen per FK |
| `RequireAdminAsync` | private | `UnauthorizedAccessException` (unbekannt) / `ForbiddenAccessException` (kein Admin) |
| `ValidateRuleInputAsync` (Z. 252) | private | Serverseitige Validierung: Name (Pflicht/Länge), `Enum.IsDefined` für `RuleType`/`Recurrence`/`MaterialType`, `DayOfWeek`-Pflicht bei `Weekly`, `SingleDate`-Pflicht bei `Once`, `EpisodeCount`-/`SlotDuration`-Pflichtlogik, Materialart↔Regelart, Clip-Gruppen-/Karenzzeit-Regeln, Genre-/Quellen-Existenz, Material-Existenz |
| `MaterialIdFor`, `ReplaceGenreLinksAsync`, `ReplaceClipGroupLinksAsync`, `ValidateChannelName` | private/static | Hilfen |

Publizierte Events: `IProgramScheduleSignal.NotifyScheduleChanged()` nach jeder Mutation (Ctor-Param, optional/`null` in Tests).
Abonnierte Events: keine.

### `ProgramRuleInput`
Datei: `VideoWebPlayer/Services/ProgramRuleInput.cs`

Create/Update-Record (Z. 13–29): `Name, RuleType, Recurrence, DayOfWeek?, SingleDate?, TimeOfDay, EpisodeCount?, SlotDuration?, MaterialType, MaterialId, Priority, IsActive, GenreIds?, MediaSourceIds?, ClipGroupIds?, PlaceholderGrace?` — **keine** Gültigkeitsfelder vorhanden.

## `ProgramScheduleService`
Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (3081 Zeilen); Implementiert: `IProgramScheduleService`; DI: `AddScoped` (`ServiceCollectionExtensions.cs` Z. 282)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` (Z. 75) | public | Extend-only-Ausbau: lädt Regeln **`Where(r.IsActive)`** (Z. 83) inkl. Genre-/Quell-/Clip-Group-Links; baut `RuleState`s, `BuildOccurrences`, `MaterializeOccurrence`, Clip-Fill, `FillGaps`, `PersistCursors` |
| `ReconcileAsync` (Z. 158) | public | Neu-Bewertung: lädt **alle** Regeln (Z. 161–167), `activeRules = rules.Where(IsActive)` (Z. 169); `ReconcileWindow` verdrängt ungültige Airings (`Status = Displaced`, nie gelöscht), rematerialisiert aus aktiven Regeln, `RewindCursor` pro verdrängter Regel |
| `GetScheduleAsync` (Z. 415) | public | Kommende Slots ab `fromUtc` (Scheduled + Displaced) |
| `GetRuleDiagnosesAsync` (Z. 424) | public | Pro-Regel-Planungsdiagnose: expandiert **jede** Regel (inkl. inaktiver, Z. 428ff) mit `BuildOccurrences` über 9 Tage (`DiagnosisOccurrenceHorizonDays`, Z. 33) für den Evaluationszeitpunkt des Jugendschutzfensters |
| `DeleteDisplacedSlotsAsync` (Z. 484) | public | Housekeeping: `ExecuteDeleteAsync` auf `Status == Displaced`; Admin-geschützt; Log-Ausgabe `[ProgramSchedule] {Deleted} verdrängte Sendeplaetze bereinigt.` |
| `ResetScheduleAsync` (Z. 495) | public | Löscht alle Slots + Cursors des Senders; Admin-geschützt |
| `BuildOccurrences` (Z. 1615) | private static | Kern der Regelplanung: iteriert lokale Tage (`TimeZoneInfo.Local`) von `coverageStart - lookback` bis `horizonEnd`; `applies`-Switch auswertet **nur `Recurrence`** (`Daily`/`Weekly`/`Once`/`Holiday` über `GermanHolidayCalendar.IsHoliday`/`Weekday`/`Weekend`) — **keine Gültigkeitsprüfung vorhanden**; erzeugt `Occurrence(rule, startUtc)` pro zutreffendem Tag |
| `MaterializeOccurrence` (Z. 1649) / `MaterializeOccurrenceEvicting` (Z. 1139) | private | Schreibt die Slots einer Ausstrahlung (atomar; Konflikt → Drop bzw. Evicts-Liste) |
| `ReconcileWindow` (Z. 1043) | private class | Lade-Fenster + Validierung: `Rebuild` ruft `BuildOccurrences(activeRules ohne Filler)`; `Validate` (Z. 1102) markiert ein Airing invalid, wenn `rule is null \|\| !rule.IsActive \|\| keine OccurrenceStarts \|\| !AiringReproducible` — hier würde eine ungültige Regel ihre Slots verlieren |
| `FillGaps` (Z. 1970) | private | Füllt Restlücken aus `RuleType.Filler`-Regeln der übergebenen `rules`-Liste (EnsureCoverage: nur aktive; Reconcile: `activeRules`) — Filler-Regeln haben keine eigenen Occurrences |
| `ExpandMaterialAsync` (Z. 2138) | private | Dispatch `MaterialType` → `ExpandShowAsync`/`ExpandMovieAsync`/`ExpandMovieCollectionAsync`/`ExpandGenreAsync`/`ExpandPlaylistAsync`/`ExpandTrailersAsync`/`ExpandMediaSourceClipsAsync`; zählt `OrphanedRules` |
| `ExpandGenreAsync` (Z. 2350) | private | Genre-Auflösung über `GenreCanonicalResolver.ResolveCanonicalNames(allRows, coverageStart)` — nutzt dort indirekt die saisonale Fenster-Mechanik |
| `PersistCursors`, `RewindCursor`, `RewindRing`, `NextIndexFor`, `SelectStrandCursor` | private(static) | Fortsetzungsstand-Semantik |
| `AiringReproducible` (Z. 581/630), `FlatAiringTailReproducible` (Z. 644), `InterleavedAiringTailReproducible` (Z. 769), `FindItemIndex`, `SlotMatchesItem`, `ClipMediaItemIds` | private static | Reproduzierbarkeits-Check geplanter Airings |
| `SelectItems`, `SelectAiringItems`, `SelectAiringItemsInterleaved`, `CommitTaken` | private(static) | Item-Auswahl pro Ausstrahlung (Jugendschutzfenster, Bound, Karenzzeit) |
| `BoundStarts`, `BoundaryAfter`, `ShiftPastInvaders`, `TailLimit`, `ChainLimit`, `PeriodStart` | private static | Zeit-/Konfliktgeometrie |
| `LoadAccessContextAsync` (Z. 1539), `LoadAgeRuleAsync` (Z. 1599), `LoadMovieAiringsAsync`, `TrailerMediaItemIdsAsync` | private | Snapshots: Publikumsfreigabe, Jugendschutz-Regelung, Film-Airings für Trailer |
| `InsertClipFill`, `FillAiringSegments`, `NextClipItem`, `BuildClipGroupStates`, `ExpandClipGroupStatesAsync`, `ExpandClipGroupItemsAsync`, `FilterClipCandidatesAsync`, `FindLeadMatchOffset` | private | Clip-Füllung |
| `RequireAdminAsync`, `GroupAirings`, `GapSeconds`, `StrandOrderKey`, `ItemOrderKey`, `Fnv1aHash`, `IsUnorderedRing`, `MixUnorderedRing`, `StrandStartOffset`, `CountNoDuration`, `ToMediaItemChoices`, `MovieMediaItemsAsync`, `EpisodeMediaItemsAsync`, `TryMovieItem`, `ToItem` | private(static)/internal static | Hilfen |

Interne Typen: `ReconcileWindow`, `RuleState`, `StrandState`, `ClipGroupState`, `Occurrence`, `Interval`, `SchedulableItem`, `SelectionResult`, `StrandPick`, `FlatSelection`, `InterleavedSelection`, `ReproContext`, `ClipFillContext`, `ClipCandidate`, `AccessContext`, `SummaryCounters`, `MediaItemRef`/`MediaItemVariant`/`MediaItemChoice`/`MediaItemLink`, `ProxyEpisode`.
Konstanten: `OccurrenceLookback` = 24 h (Z. 26), `DiagnosisOccurrenceHorizonDays` = 9 (Z. 33), `OtherStrandKey` = `"other"`.
Ctor-Abhängigkeiten: `ApplicationDbContext`, `IProgramRuleService`, `ProgramSettingsService`, `ILogger`, optional `IOptions<MediaProbeOptions>`/`IOptions<ProgramScheduleOptions>`.

## Saisonale Fenster-Mechanik (wiederverwendbarer Bestand)

### `SeasonalGenreWindow`
Datei: `VideoWebPlayer/Services/SeasonalGenreWindow.cs` (statisch)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetExpiry(DateTime? startDate, DateTime? endDate, DateTime nowUtc)` | public static | Exklusives UTC-Ablaufdatum des aktuell aktiven Fensters oder `null`. Semantik: **beide** Grenzen erforderlich; `end < start` ungültig; inklusives Tagesfenster; `while (end < nowUtc.Date) { start += 1J; end += 1J }` rollt das Fenster jahresweise nach vorn (dadurch auch über-Silvester-Fenster abbildbar); Schutz `Year == 9999`/`DateTime.MaxValue` |

Aufrufer: `GenreService.GetSeasonalGenresAsync` (`GenreService.cs` Z. 42–46), `SeasonalHomeService` (Z. 24). **Nicht** von `GenreCanonicalResolver.IsVisible` genutzt — dort steht eine parallele, gleichwertige Implementierung.

### `GenreCanonicalResolver`
Datei: `VideoWebPlayer/Services/GenreCanonicalResolver.cs` (`internal static`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsVisible(Genre, DateTime now)` | public static | `!IsHidden` && (keine Fensterdaten → sichtbar; beide Daten → rollierendes inklusives Jahresfenster: `while (end < date) { AddYears(1) }`, `start <= date <= end`) — **einzige Grenze allein = unbegrenzt** |
| `HasEffectiveAlternates`, `EffectiveAlternateNames` | public/internal static | Alternativnamen-Erkennung |
| `ResolveCanonicalNames(genres, now)` | public static | Genre-Id → kanonische Namen (rekursive Alternativnamen-Auflösung, zyklenfest); unsichtbare Zeilen ohne Alternativen tragen nichts bei |
| `BuildOptions(genres, now, includeOutOfSeason)` | public static | Auswahloptionen; `includeOutOfSeason: true` markiert Out-of-Season-Genres im Label `Name (dd.MM.–dd.MM.)` — UI-Muster für „aktuell ungültig, aber wählbar" |
| `OptionName`, `includeWindow`, `ResolveNames` | private | Hilfen |

Verwendung in `ProgramRuleService.GetGenreMaterialOptionsAsync` und `ProgramScheduleService.ExpandGenreAsync`.

### `GermanHolidayCalendar`
Datei: `VideoWebPlayer/Services/GermanHolidayCalendar.cs` (statisch)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetHolidays(int year, string? federalState)` | public static | Feste deutsche Feiertage (bundesweit + `DE-*`-Bundesländer) |
| `IsHoliday(DateOnly, string? federalState)` | (von `BuildOccurrences` genutzt) | Feiertagsprüfung pro Tag |

## Worker & Signal

### `ProgramScheduleWorker`
Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs`; `BackgroundService`, DI `AddHostedService` (`ServiceCollectionExtensions.cs` Z. 335)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` (Z. 45) | protected override | Startverzögerung 15 s; Schleife: Intervall via `ProgramSettingsService.GetProgramScheduleIntervalAsync` (Default 15 min), bei `GetTvSenderEnabledAsync == true` im `IBackgroundProcessingGate`-Scope `ReconcileAsync` **dann** `EnsureCoverageAsync` (Z. 71–72); danach `Task.WhenAny(Intervall-Delay, Signal)` — natürlicher Ort für eine regelmäßige Bereinigung abgelaufener Regeln; bei deaktiviertem Flag pausiert alles |

Ctor-Parameter: `IServiceScopeFactory`, `ILogger`, optional `IProgramScheduleSignal`, `TimeSpan? initialDelay`, `TimeProvider`.

### `ProgramScheduleSignal` / `IProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`; Singleton (`ServiceCollectionExtensions.cs` Z. 268–269)

| Methode | Zweck |
|---------|-------|
| `NotifyScheduleChanged()` | Aufwecken bei Regel-/Medienänderung (von `ProgramRuleService` u. a. nach jeder Mutation); während Scan (`BeginScan`) unterdrückt |
| `BeginScan()` | Scan-Scope; Ende weckt nur, wenn Notifications unterdrückt wurden |
| `WaitAsync(ct)` | Coalesced Warten des Workers |

## `VideoWebPlayerBackupData` (relevante Stellen)
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` — `OptionalRestoreTables`/`OptionalRestoreColumns`/Default-Arrays siehe [models.md](models.md); `ReadFromAsync` (Z. 274) restauriert tabellenweise und nutzt die Optional-Mechanik für Alt-Backups.

## `ProgramSettingsService` (relevante Methoden)
Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `GetTvSenderEnabledAsync` (Z. 195) | Feature-Flag aus `Setup` |
| `GetProgramScheduleHorizonAsync` (Z. 345) | Planungshorizont |
| `GetProgramScheduleIntervalAsync` (Z. 357) | Worker-Intervall (Default 15 min) |
| `GetHolidayFederalStateAsync` | Bundesland für `GermanHolidayCalendar` |
| `GetProgramScheduleContentScopeAsync`, `GetProgramScheduleAgeWindowsAsync`, `GetProgramScheduleUnknownRatingModeAsync` | Publikums-/Jugendschutz-Einstellungen |
