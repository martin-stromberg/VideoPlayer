# Test-Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-27, ~19:40–19:55 (lokale Zeit, Systemzeit Windows)
- Branch und Commit-ID: `task/issue-297-bb8b251bcf0e47d29671f5de7059ad0a-tv-sender-sendeplan-enthaelt-v` @ `35b2eeb591eede988bd23b2a6d8dc943595f4a8c`
- Uncommittete Änderungen im getesteten Stand: nur untracked Feature-Dokumente unter
  `docs/features/task/issue-297-...-tv-sender-sendeplan-enthaelt-v/` (`issue.md`,
  `requirement.md`, `todo.md` sowie die hier entstehende Inventar-Dokumentation);
  keine Produktivcode- oder Teständerungen.
- Testumgebung und Runtime-/SDK-Versionen: Windows (lokale Maschine), .NET SDK 10.0.402,
  xunit.v3 3.2.2, Microsoft.Playwright 1.62.0 (Chromium lokal installiert,
  `%LOCALAPPDATA%\ms-playwright\chromium-1248` u. a.).
- Ermittelte Testsuiten und Quellen der Testbefehle: Einzige Testprojekt-Datei ist
  `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`. Die CI
  (`.github/workflows/pr-staging-ci.yml`, Job `build-and-test`) baut Release und trennt in
  `--filter "Category!=E2E"` (Unit), `--filter "Category=Integration"` und
  `--filter "Category=E2E"` (Playwright). Im Code existiert ausschließlich das Trait
  `Category=E2E` (75 Klassen); `Category=Integration` trifft keine Tests. AGENTS.md nennt als
  DoD den vollständigen Lauf `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
  — daher wurde die komplette Suite (Unit + E2E) in Debug gefahren.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline (alle Tests, Debug) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug --logger "trx;LogFileName=baseline-all.trx"` | Repo-Wurzel | 0 | 2591 | 0 | 2 | [baseline-all.trx](test-results/baseline-all.trx), [baseline-all-console.log](test-results/baseline-all-console.log) |
| Build Debug | `dotnet build VideoPlayer.sln -c Debug` | Repo-Wurzel | 0 | – | – | – | Konsolenausgabe (0 Fehler, 140 Warnungen — allesamt BL0008-Razor-Warnungen, preexisting) |
| Build Release | `dotnet build VideoPlayer.sln -c Release` | Repo-Wurzel | 0 | – | – | – | Konsolenausgabe (0 Fehler, 140 Warnungen — identische BL0008-Warnungen) |

Gesamtdauer des Testlaufs: 5,9 Minuten (TRX `Counters total="2593" executed="2591" passed="2591" failed="0"`).

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf hat 0 fehlgeschlagene Tests.

### Testlücken und Ausführungsprobleme

- 2 übersprungene Tests, beide dokumentiert und nicht regressionsrelevant:
  - `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit`
    — `Assert.SkipWhen(OperatingSystem.IsWindows())`: läuft nur unter Unix (OS-bedingt).
  - `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
    — `[Fact(Skip = "...")]` am Test: dokumentierter Produktiv-Befund (HTTP 500 bei Film ohne
    Filmsammlung, `ContinueWatchingService.Create` ohne Null-Prüfung); bewusst deaktiviert.
- `Category=Integration` (CI-Filter) matcht keine Tests — das Trait existiert nicht im Code.
- Fachliche Testlücke zur Anforderung: Es existiert **kein Test**, der die Verteilung der
  Titel-Auswahl über Generierungsläufe prüft. Nächstliegende Absicherung ist lediglich
  `ProgramScheduleServiceTests_Interleaving.StrandOrderKey_IsDeterministic_AndNotAlphabetic`
  bzw. `EnsureCoverage_GenreRule_LeadStrand_FollowsHashOrder_NotAlphabetical` — beide prüfen
  nur die *Strang*-Reihenfolge bei Genre-Interleaving, nicht die Reihenfolge der Elemente
  innerhalb ungeordneter Ringe.

## Testklassen

Alle sendeplanbezogenen Testklassen laufen gegen echtes SQLite via
`ProgramScheduleTestDb` (in-memory shared, `EnsureCreated`), nicht gegen EF InMemory.

### `ProgramScheduleServiceTests_Generation` (`VideoWebPlayer.Tests/ProgramScheduleServiceTests_Generation.cs`, 13 Tests)
- `EnsureCoverage_DailyMovieRule_SchedulesAtTimeOfDayEachDay` — tägliche Occurrence-Zeiten.
- `EnsureCoverage_WeeklyRule_OnlyOnDayOfWeek` / `EnsureCoverage_Once_OnlyOnSingleDate` / `EnsureCoverage_Holiday_OnlyOnHoliday` / `EnsureCoverage_WeekdayRule_OnlyMondayToFriday` / `EnsureCoverage_WeekendRule_OnlySaturdaySunday` — Recurrence-Muster.
- `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder` — Episoden in Sendedreihenfolge (geordneter Strang).
- `EnsureCoverage_SeriesExhausted_WrapsAround` — Ring-Wrap bei erschöpfter Serie.
- `EnsureCoverage_SlotDuration_FillsUntilTarget` — SlotDuration-Befüllung.
- `EnsureCoverage_PlanIsGapless_WhenFillerExists` — Lückenlosigkeit mit Füller.
- `EnsureCoverage_OrphanedMaterial_ProducesNothing` — verwaistes Material.
- `EnsureCoverage_SpanningOccurrence_KeepsTrueStart` — übergreifende Occurrence.
- `EnsureCoverage_SecondRun_ExtendsNotDuplicates` — Extend-only über Läufe hinweg.

### `ProgramScheduleServiceTests_Genres` (`..._Genres.cs`, 10 Tests)
- `EnsureCoverage_MultiGenreRule_DeduplicatesUnion`, `EnsureCoverage_GenreSourceFilter_RestrictsSources`, `EnsureCoverage_GenreNoSourceFilter_IncludesAllSources`, `EnsureCoverage_HiddenGenreWithAlternate_ItemsRollUpToCanonical`, `EnsureCoverage_HiddenGenreWithoutAlternate_ProducesNothing`, `EnsureCoverage_OutOfSeasonGenre_ProducesNothing`, `EnsureCoverage_InSeasonGenre_IsScheduled`, `EnsureCoverage_LegacyGenreRuleWithoutJoins_FallsBackToMaterialId` — Genre-Auflösung/-Filterung.
- `EnsureCoverage_GenreExpansion_IncludesShowEpisodes` — Episoden in der Genre-Expansion.
- `RuleLinks_CascadeOnRuleAndGenreDelete` — FK-Kaskade der Joins (echtes SQLite).

### `ProgramScheduleServiceTests_Interleaving` (`..._Interleaving.cs`, 7 Tests)
- `EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays` — stabile Slot-Position je Serie, Führungsserie per Hash-Mix.
- `EnsureCoverage_GenreRule_Interleaving_EpisodeOrderPerShow` — Episoden-Reihenfolge je Strang + Strang-Cursor-Rows.
- `EnsureCoverage_GenreRule_SecondRun_ContinuesEachShowCursor` — Cursor-Fortsetzung je Strang.
- `StrandOrderKey_IsDeterministic_AndNotAlphabetic` — Reproduzierbarkeit + Regel-Abhängigkeit des FNV-1a-Mix (reiner Unit-Test ohne DB).
- `EnsureCoverage_GenreRule_LeadStrand_FollowsHashOrder_NotAlphabetical` — Führungsstrang folgt `StrandOrderKey`.
- `Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled` / `Reconcile_GenreRule_Interleaved_LongAiring_StaysScheduled` — Reproduzierbarkeit interleaveter Ausstrahlungen.

### `ProgramScheduleServiceTests_CursorSelection` (`..._CursorSelection.cs`, 3 Tests)
- `SelectStrandCursor_NullStrandKeyDuplicates_ReturnsYoungest` / `SelectStrandCursor_TiedUpdatedAt_ReturnsHigherId` — deterministische Cursor-Auswahl.
- `EnsureCoverage_NullStrandKeyDuplicates_PersistCursorsUpdatesWinner` — Update der Gewinner-Row.

### `ProgramScheduleServiceTests_Filler` (`..._Filler.cs`, 2 Tests)
- `EnsureCoverage_OnlyFillerRule_FillsEntireHorizon` — Füller-Ring deckt den Horizont.
- `EnsureCoverage_FillerItemTooLong_IsSkipped_NoFittingFiller_LeavesGap` — zu lange Füller-Items.

### `ProgramScheduleServiceTests_ClipSource` (`..._ClipSource.cs`, 8 Tests)
- `EnsureCoverage_ClipSourceFiller_FillsGapsWithBareMediaItems`, `EnsureCoverage_ClipSource_ExcludesClassifiedItems`, `EnsureCoverage_ClipSource_SkipsItemsWithoutDurationAndNonVideos`, `EnsureCoverage_ClipSource_NotFullySharedSource_YieldsNoSlots`, `EnsureCoverage_ClipSource_MissingSource_IsOrphaned` — Kandidaten-Filter der Clip-Quelle.
- `EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns` — Ringfortsetzung über Läufe (Cursor-Anker in `OrderBy(MediaItemId)`-Ring).
- `Reconcile_ClipSourceFillerSlots_StayScheduled` — Reproduzierbarkeit von Füller-Slots.
- `GetRuleDiagnoses_ClipSource_CountsProbePending` — Diagnose-Zähler.

### `ProgramScheduleServiceTests_ClipGroups` (`..._ClipGroups.cs`, 20 Tests)
- Grace-/Segment-Logik (`EnsureCoverage_GraceRemainder_FillsWithDistributedClips`, `..._RemainderAboveGrace_StartsNextTitle_Overshoots`, `..._RemainderExactlyGrace_StartsNextTitle`, `..._EpisodeCountAndSlotDuration_FillsTailWithClips`).
- Ring-Verhalten: `EnsureCoverage_TwoClipGroups_RoundRobinDistributed`, `EnsureCoverage_ClipRing_RotatesAcrossRuns`, `EnsureCoverage_ClipsSkipItemsWithoutDuration`, `EnsureCoverage_ClipGroup_NotFullySharedSource_YieldsNoClipSlots`.
- Trailer-Gruppe: `EnsureCoverage_TrailerGroup_AirsTrailerBeforeScheduledMovie`, `EnsureCoverage_TrailerGroup_NoLeadMatch_OtherGroupsFill`, `EnsureCoverage_TrailerClip_ExcludesItemsWithoutDuration`.
- Bounds/Reconcile: `EnsureCoverage_ClipOvershoot_ReservedBoundWindow_ClipDropped`, `Reconcile_ClipFillSlots_StayScheduled`, `Reconcile_RemovedClipGroupMember_DisplacesAndReplans`, `Reconcile_WorkerLoop_WithClipFill_NoDisplacedAccumulation`, `EnsureCoverage_InterleavedRule_ClipFillDistributed`, `EnsureCoverage_GeneratedClipFillSlots_MarkedIsClipFill`, `EnsureCoverage_NoGroups_UnchangedOvershootBehavior`, `EnsureCoverage_LastClip_MayOvershootPeriodEnd`, `EnsureCoverage_ClipTooLargeForFreeRoom_LeavesDocumentedGap`.

### `ProgramScheduleServiceTests_ClipFillPeriodBounds` (`..._ClipFillPeriodBounds.cs`, 3 Tests)
- `EnsureCoverage_StartedMidPeriod_NoClipsAfterNominalPeriodEnd`, `EnsureCoverage_OccupiedInsidePeriod_ClipFillDoesNotOverlap`, `EnsureCoverage_FullPrimetimeScenario_ClipsDistributedInsidePeriod` — Periodengrenzen der Clip-Füllung.

### `ProgramScheduleServiceTests_Trailer` (`..._Trailer.cs`, 5 Tests)
- `EnsureCoverage_TrailerFiller_AirsTrailerBeforeScheduledMovie`, `EnsureCoverage_TrailerOfUnscheduledMovie_IsNotAired`, `EnsureCoverage_BeyondLeadWindow_FallsBackToNormalFiller`, `EnsureCoverage_TrailerWithoutDuration_IsNotAired`, `Reconcile_KeptTrailerSlot_StaysScheduled` — Trailer-Vorlauf-Logik.

### `ProgramScheduleServiceTests_Reconcile` (`..._Reconcile.cs`, 20 Tests)
- Reproduzierbarkeit/Verdrängung: `Reconcile_NoChanges_LeavesEverythingScheduled`, `Reconcile_DeactivatedRule_DisplacesFutureAndRefills`, `Reconcile_DeletedRule_DisplacesSlotsAndKeepsRows`, `Reconcile_ChangedTimeOfDay_ReplansAtNewTime`, `Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip`, `Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip`, `Reconcile_PriorityRaised_EvictsLowerPriorityAiring`, `Reconcile_NewEpisodeInserted_ReplansInAirOrder`, `EnsureCoverage_IgnoresDisplacedSlots`, `Reconcile_PartiallyAiredOccurrence_*`, `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap`, `GetSchedule_ReturnsUpcomingSlotsWithStatus`, `Reconcile_LongAiring_*` (7 Varianten inkl. Shifted/BeyondMaxLookback).

### `ProgramScheduleServiceTests_Boundary` (`..._Boundary.cs`, 12 Tests)
- Fenstergrenzen: `EnsureCoverage_AdjacentWindows_OvershootCompletesAndShiftsSuccessor`, `Reconcile_PartialOccurrence_OvershootingTailStaysScheduled_SuccessorShifts`, `Reconcile_ShiftedAndOvershootingAirings_StayStableAcrossRuns`, `EnsureCoverage_OversizedHeadItem_AirsAndOvershootsSuccessor`, `EnsureCoverage_ShortRemainder_OvershootingItem_Completes`, `EnsureCoverage_RemainderBelowMinRemain_PlansFiller`, `EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence`; Options-Normalisierung; `DeleteDisplacedSlots_*`, `ResetSchedule_RemovesAllSlotsAndCursors`.

### `ProgramScheduleServiceTests_AgeWindow` (`..._AgeWindow.cs`, 16 Tests)
- Jugendschutz-Fenster inkl. der Skip-Semantik ungeordneter Ringe: `EnsureCoverage_UnorderedMovieRing_SkipsBlockedAndOffersItAgainAtNight`, `EnsureCoverage_OrderedShow_BlockedHeadDefersWholeAiring`, `EnsureCoverage_Interleaved_UnorderedOtherStrand_SkipsBlockedMovie`, `Reconcile_DisplacedUnorderedRing_ReoffersSkippedPositionOnlyInWindow`, `Reconcile_InterleavedWindowSkip_StaysReproducible_TighterWindowDisplaces` u. a. — **ordnungskritisch:** die Reproduzierbarkeitsprüfung rekonstruiert übersprungene Ringpositionen.

### `ProgramScheduleServiceTests_Access` / `..._ContentScope` / `..._Conflicts` / `..._Diagnosis`
- Freigabe (`NotFullyReleased`, `UnlockedForAll`, `NoNonAdminUsers`), ContentScope `AllContent`/`SharedOnly`, Prioritätskonflikte, Diagnose-Zähler.

### `ProgramRuleCursorUniquenessTests` / `ProgramRuleCursorNullStrandKeyMigrationTests`
- Unique-Index-Verhalten und NULL-StrandKey-Migration gegen echtes SQLite.

### Weitere sendernahe Suiten (nicht erschöpfend)
`ProgramGuideServiceTests(_ClipGroups)`, `ProgramRuleServiceTests_*` (CRUD/Genre/ClipSource/Trailer/Authorization), `ProgramScheduleWorkerTests`, `ProgramScheduleSignalTests`, `ProgramSettingsServiceTests(_AgeWindow|_SetupRace|ContentScope|LiveStream)`, `TVSenderAdmin*E2ETests`, `ProgramSettingsE2ETests`, `LiveTvE2ETests`, `ProgramGuideE2ETests`, `ProgramClipGroupTests`/`ProgramClipGroupServiceTests`.

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync(dbName, ct)` — shared in-memory SQLite (`file:{dbName}?mode=memory&cache=shared`), `EnsureCreated`, seedet `Setup` (`TvSenderEnabled=true`, `UnknownRatingMode=Unrestricted`) und die System-Clip-Gruppe „Trailer".
- `CreateService(scheduleOptions)` — `ProgramScheduleService` gegen die Fixture-DB (`ProgramRuleService`, `ProgramSettingsService`, `NullLogger`).
- `CreateGuideService()` — `ProgramGuideService`.
- `CreateSlotAsync(...)` — Slot direkt in der DB (Guide-/Reconcile-Seeds).
- `AddUserAsync(email, isAdmin)` / `EnsureSourceAsync(id)` / `GrantSourceAccessAsync(userId, sourceId)` — Publikums-/Quellen-Seeds.
- `CreateMediaItemAsync(sourceId, duration)` / `SeedProbeItemAsync(name, duration, probedAt, attempts, ct)` — MediaItems mit Probe-Status.
- `CreateMovieAsync(name, sourceId, duration, collectionId, releaseDate, ageRatingMinAge, ageRating)` — Film + MediaItem-Link; `releaseDate` steuert die Ring-Sortierung.
- `CreateShowAsync(name, episodeCount, sourceId, duration, ageRatingMinAge)` — Serie + Staffel + Episoden (nummeriert, je ein MediaItem).
- `CreateGenreAsync(name, sourceId, isHidden, alternateNames, startDate, endDate)` / `LinkMovieGenreAsync` / `LinkShowGenreAsync` — Genre-Seeds.
- `CreateClipGroupAsync(name, ct)` / `AddClipToGroupAsync(groupId, mediaItemId, ct)` — Clip-Gruppen.
- `SeedTrailerClipGroupAsync(ct)` — reservierte Systemgruppe (EnsureCreated führt InsertData-Seeds nicht aus).
- `CreateRuleAsync(channelId, name, type, recurrence, timeOfDay, materialType, materialId, priority, episodeCount, slotDuration, dayOfWeek, singleDate, isActive, genreIds, mediaSourceIds, clipGroupIds, placeholderGrace)` — Regel direkt in der DB inkl. Genre-/Quellen-/Clip-Group-Joins.
- `GetChannelAsync(ct)` — Kanal über `ProgramRuleService.GetOrCreateChannelAsync`.
- `SlotsAsync(ct)` — alle Slots sortiert nach `Start`.
- `AssertNoDisplacedAsync(fixture, summary, ct, context)` — kein `Displaced` nach Reconcile.
- `AssertGapless(slots, from, to)` — lückenlose Abdeckung.
- `SaveDiagAsync()` — `SaveChanges` mit FK-Diagnose im Fehlertext.
