# Tests: Ausgangszustand und Testlandschaft (issue-271)

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-10-27, ~13:05 (+0100, W. Europe Standard Time)
- **Branch und Commit-ID:** `task/issue-271-916652e6d8fc452b8b69fc000f3d91bf-tv-sender-altersgerechte-sende` @ `f8ae06501fb86f55543fe83431b6e498f6640a34`
- **Uncommittete Änderungen im getesteten Stand:** nur untracked `docs/features/task/issue-271-…/` (requirement.md, inventory-Arbeitsartefakte) — kein Produktivcode, keine Teständerungen
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (Git Bash), .NET SDK `10.0.402`, Debug-Build `net10.0`; xUnit + Playwright-E2E im selben Projekt `VideoWebPlayer.Tests` (Playwright-Browser vorhanden — E2E liefen real)
- **Ermittelte Testsuiten und Quellen der Testbefehle:** `AGENTS.md` §7 — Build `dotnet build VideoPlayer.sln` (Debug) + volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`. Zusätzlich existiert `tools/MarkdownLinkCheck.Tests` in der Solution (läuft nicht in `VideoWebPlayer.Tests`; für diese Anforderung nicht relevant).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build (Debug) | `dotnet build VideoPlayer.sln -c Debug` | Repo-Root | 0 (140 Warnungen, 0 Fehler) | — | — | — | [build-debug.log](test-results/build-debug.log) |
| Volle Testsuite | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` | Repo-Root | 0 | 2526 | 0 | 2 | [test-run-1.log](test-results/test-run-1.log) |

Dauer Testsuite: ~5 m 30 s (inkl. Playwright-E2E).

### Nachgewiesene bestehende Testfehler

**Keine.** Der Ausgangslauf ist komplett grün — 0 fehlgeschlagene Tests. Alles, was bei der Umsetzung rot wird, ist neu eingeführt und zu untersuchen.

### Testlücken und Ausführungsprobleme

Übersprungene Tests (2, beide beabsichtigt, keine Ausführungsprobleme):

| Test-ID | Grund |
|---------|-------|
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | `Assert.SkipWhen(OperatingSystem.IsWindows())` — Unix-only Test, auf Windows beabsichtigt übersprungen |
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | `[Fact(Skip = …)]` — dokumentiert bekannten Defekt: `api/continue-watching` antwortet 500 bei Film ohne Filmsammlung (`ContinueWatchingService.Create` ohne Null-Prüfung); laut Kommentar im Code in jenem Lauf bewusst nicht behoben. **Kein Bezug zu issue-271.** |

Sonstige Hinweise: Playwright-E2E liefen ohne Flackern durch; `tools/MarkdownLinkCheck.Tests` wurde nicht ausgeführt (gehört nicht zur Kern-Testsuite).

## Testklassen (relevante Bereiche)

### `ProgramScheduleServiceTests_Generation` (`ProgramScheduleServiceTests_Generation.cs`, 327 Zeilen)
- `EnsureCoverage_DailyMovieRule_SchedulesAtTimeOfDayEachDay`, `…WeeklyRule_OnlyOnDayOfWeek`, `…Once_OnlyOnSingleDate`, `…Holiday_OnlyOnHoliday`, `…WeekdayRule_OnlyMondayToFriday`, `…WeekendRule_OnlySaturdaySunday` — Occurrence-Bildung pro `Recurrence`
- `…SeriesRule_AdvancesEpisodesInAirOrder`, `…SeriesExhausted_WrapsAround` — Cursor/Ring
- `…SlotDuration_FillsUntilTarget`, `…PlanIsGapless_WhenFillerExists`, `…OrphanedMaterial_ProducesNothing`, `…SpanningOccurrence_KeepsTrueStart`, `…SecondRun_ExtendsNotDuplicates` — Kernverhalten des Runs

### `ProgramScheduleServiceTests_Boundary` (356 Zeilen)
- Overshoot/Shift: `…AdjacentWindows_OvershootCompletesAndShiftsSuccessor`, `Reconcile_PartialOccurrence_OvershootingTailStaysScheduled_SuccessorShifts`, `Reconcile_ShiftedAndOvershootingAirings_StayStableAcrossRuns`, `…OversizedHeadItem_AirsAndOvershootsSuccessor`, `…ShortRemainder_OvershootingItem_Completes`, `…RemainderBelowMinRemain_PlansFiller`, `…HardBound_ItemCannotComplete_DefersToNextOccurrence` — «a started item always completes» (Grenzregel, auf die Fenster-Regel aufsetzt)
- `ProgramScheduleOptions_MinStartRemain/MaxOccurrenceLookback_Normalizes`, `DeleteDisplacedSlots_*`, `ResetSchedule_*`

### `ProgramScheduleServiceTests_Reconcile` (676 Zeilen)
- Displacement/Stabilität: `Reconcile_NoChanges_LeavesEverythingScheduled`, `…DeactivatedRule_…`, `…DeletedRule_…`, `…ChangedTimeOfDay_ReplansAtNewTime`, `…DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip`, `…DeletedMediaItem_…`, `…PriorityRaised_EvictsLowerPriorityAiring`, `…NewEpisodeInserted_ReplansInAirOrder`, LongAiring-Familie (`…LongAiring_StaysScheduled`, `…BeyondTwoDays`, `…MultiPart`, `…Shifted`, `…PartiallyMaterialized`, `…ChangedRule_StillDisplaced`, `…BeyondMaxLookback_Displaced`), `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap`, `GetSchedule_ReturnsUpcomingSlotsWithStatus`

### `ProgramScheduleServiceTests_Access` (110 Zeilen)
- `…ItemWithoutDuration_IsSkipped`, `…NotFullyReleased_IsExcluded`, `…UnlockedForAll_IsScheduled`, `…NoNonAdminUsers_EverythingSchedulable` — Freigabe-Filtration (Fenster-Logik hängt sich an dieselbe Stelle)

### `ProgramScheduleServiceTests_ContentScope` (252 Zeilen)
- `…AllContent_SchedulesUnreleasedMovieAndEpisode/ClipSourceItems/ClipGroupItems/Trailers`, `…SharedOnlyDefault_StillFiltersUnreleased`, `…ScopeSwitch_TakesEffectNextRun`, `Reconcile_SwitchBackToSharedOnly_DisplacesUnreleasedSlots`, `GetRuleDiagnoses_AllContent_ReportsNoAccessExclusions` — **Muster für senderweite Schedule-Einstellung (gleiche Ebene wie Jugendschutzfenster)**

### `ProgramScheduleServiceTests_Diagnosis` (238 Zeilen)
- `Diagnosis_SchedulableMovie_CountsSchedulable`, `…MovieWithoutMediaItem_CountsNoMediaItem`, `…UnprobedItem_CountsProbePending`, `…FailedProbeBelowMax_CountsProbePending`, `…ExhaustedProbe_CountsProbeExhausted`, `…UnsharedContent_CountsNoAccess`, `…DeletedMaterial_MarksMissing`, `…GenreResolvingToNothing_MarksMissing`, `…InactiveRule_StillDiagnosed`, `Diagnosis_NonAdmin_Throws` — Muster für neuen Fenster-Grund

### `ProgramScheduleServiceTests_Filler` (80 Zeilen)
- `…OnlyFillerRule_FillsEntireHorizon`, `…FillerItemTooLong_IsSkipped_NoFittingFiller_LeavesGap`

### `ProgramScheduleServiceTests_Conflicts` (69 Zeilen)
- `…HigherPriorityWins_Overlap`, `…EqualPriority_LowerRuleIdWins`

### `ProgramScheduleServiceTests_CursorSelection` (115 Zeilen)
- `SelectStrandCursor_NullStrandKeyDuplicates_ReturnsYoungest/TiedUpdatedAt_ReturnsHigherId`, `…PersistCursorsUpdatesWinner` — Cursor-Dedup

### `ProgramScheduleServiceTests_Interleaving` (255 Zeilen)
- `…GenreRule_InterleavesShows_StableSlotsAcrossDays`, `…Interleaving_EpisodeOrderPerShow`, `…SecondRun_ContinuesEachShowCursor`, `StrandOrderKey_IsDeterministic_AndNotAlphabetic`, `…LeadStrand_FollowsHashOrder_NotAlphabetical`, Reconcile-Interleaved-Tests — **Cursor-Verhalten über Stränge (für „kein dauerhaftes Verklemmen“ des Fensters relevant)**

### `ProgramScheduleServiceTests_Genres` (273 Zeilen)
- Genre-Expansion, Source-Filter, Hidden/Alternate/Season, `RuleLinks_CascadeOnRuleAndGenreDelete`

### `ProgramScheduleServiceTests_Trailer` (164 Zeilen), `…_ClipGroups` (485), `…_ClipSource` (222), `…_ClipFillPeriodBounds` (141)
- Trailer-Vorlauf, Clip-Ringe/Grace/IsClipFill — Bereiche ohne `IAgeRated`

### `AgeRatingNormalizerTests` (114 Zeilen)
- `Normalize_KnownValues_…` (Theory: FSK/MPAA/Länderpräfixe), `Normalize_EmptyInput_…`, `Normalize_UnknownValues_FallsBackToTrimmedRaw`, `ExtractFromXml_MpaaElementWinsOverCertification/FallsBackToCertification/NoRatingElement_…`, `CanonicalOptions_ContainsAllKnownStepsInDisplayOrder` — Normalisierung bleibt einzige Stufenquelle

### `MovieAgeRatingTests` / `TVShowAgeRatingTests`
- `LoadFromXml_SetsAgeRating_*` — NFO→Felder

### `LocalMediaSourceClassifierTests_AgeRating` / `MediaSourceClassifierAgeRatingBackfillTests`
- Persistenz beim Scan, Backfill-Regeln (`IsManuallyEdited`-Schutz)

### `ProgramSettingsServiceContentScopeTests` (74 Zeilen) / `ProgramSettingsServiceTests` / `…_SetupRace` / `…LiveStreamTests`
- Enum-Setting Roundtrip/Normalisierung/Race — **Muster für neue Einstellungen**

### E2E
- `TVSenderAdminE2ETests` (622 Zeilen): `Admin_CreateEditDeactivateDeleteRule`, `…ProbeReset_…`, `…GenreRule_MultiSelectAndSourceFilter`, `NonAdmin_SeesNotAuthorized`, `Admin_StandbyClip_PickAndPersist`, `Admin_ContentScope_SelectAndPersist`, `Admin_ContentScope_AllContent_SchedulesUnreleased` — UI-Einstellungen inkl. Select-Pattern
- `AgeRatingE2ETests` (245 Zeilen): `MovieNfoRating_ShowsBadgeOnMovieDetails`, `TvShowNfoRating_ShowsBadge_AndEpisodeInheritsIt`, `Admin_SetsAgeRating_ViaEditDropdown`
- `TVSenderAdminClipBlocksE2ETests`, `TVSenderAdminClipGroupsE2ETests`, `TVSenderAdminE2ETests_FfmpegDownload`, `ProgramGuideE2ETests`

### `TVSenderAdminTests*` (Components/)
- Unit-Tests der Razor-Seite: `TVSenderAdminTests_ContentScope`, `…_ClipBlocks`, `…_ClipGroups`, `…_Standby`, `…_FfmpegDownload`

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`, 343 Zeilen)
Echtes In-Memory-SQLite (`Data Source=file:{db}?mode=memory&cache=shared`, `EnsureCreated`, FK an) — **kein EF InMemory**; seedet `Setup { TvSenderEnabled = true }` + Trailer-Clipgruppe.

- `CreateAsync(dbName, ct)`, `CreateService(scheduleOptions?)` → `ProgramScheduleService` mit `ProgramRuleService`/`ProgramSettingsService`/`NullLogger`
- `CreateGuideService()` → `ProgramGuideService`
- `SeedTrailerClipGroupAsync`, `CreateClipGroupAsync(name)`, `AddClipToGroupAsync(groupId, mediaItemId)`
- `CreateSlotAsync(channelId, startUtc, endUtc, mediaItemId, movieId?, episodeId?, ruleId?, ruleType, status, isClipFill)` — direkter Slot-Seed
- `AddUserAsync(email, isAdmin)`, `EnsureSourceAsync(id)`, `GrantSourceAccessAsync(userId, sourceId)`
- `CreateMediaItemAsync(sourceId, duration)`, `SeedProbeItemAsync(name, duration, probedAt, attempts)` — Probe-State
- `CreateMovieAsync(name, sourceId, duration, collectionId, releaseDate)` — Movie+MediaItem-Link
- `CreateShowAsync(name, episodeCount, sourceId, duration)` — Show+Season+Episodes+Links
- `CreateGenreAsync(name, sourceId, isHidden, alternateNames, startDate, endDate)`, `LinkMovieGenreAsync`, `LinkShowGenreAsync`
- `CreateRuleAsync(channelId, name, type, recurrence, timeOfDay, materialType, materialId, priority?, episodeCount?, slotDuration?, dayOfWeek?, singleDate?, isActive?, genreIds?, mediaSourceIds?, clipGroupIds?, placeholderGrace?)` — direkter Rule-Seed ohne Service-Validierung
- `GetChannelAsync`, `SaveDiagAsync` (FK-Dump bei Fehler), `SlotsAsync`
- `AssertNoDisplacedAsync(fixture, summary, ct, context?)` — Reconcile-Stabilität
- `AssertGapless(slots, from, to)` — Lückenlosigkeit

**Lücke für Fenster-Tests:** `CreateMovieAsync`/`CreateShowAsync` setzen keine `AgeRating*`-Felder — für Fenster-Tests müssen Seeds `AgeRatingMinAge` setzen (entweder Helper-Parameter oder Nachschreiben der Entität — Helper-Änderung nur im Umsetzungsschritt).

### Playwright-E2E
- E2E-Helfer je Datei (App-Bootstrap, Login); gemeinsame Basis siehe `VideoWebPlayer.Tests/*E2ETests.cs` — Playwright-Browser sind installiert (Lauf grün).
