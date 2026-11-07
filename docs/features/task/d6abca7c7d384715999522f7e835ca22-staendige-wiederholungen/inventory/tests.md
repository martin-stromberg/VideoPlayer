# Tests

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-07, Lauf gestartet ~11:22 Uhr MEZ (UTC+1), beendet ~11:25 Uhr.
- **Branch und Commit-ID:** `task/d6abca7c7d384715999522f7e835ca22-staendige-wiederholungen` @ `3b2a616128406f795eb6b450dbf317b4f22557d7` („Sendeplan: Scope-freie Weckrufe über Bündelfenster koaleszieren (Issue #337) (#340)").
- **Uncommittete Änderungen im getesteten Stand:** nur das untracked Feature-Verzeichnis `docs/features/task/d6abca7c7d384715999522f7e835ca22-staendige-wiederholungen/` (requirement.md, todo.md, diese Bestandsaufnahme). Keine Produktiv-/Testcode-Änderungen.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows, Git Bash, .NET SDK `10.0.403` (`dotnet --version`), xunit.v3 3.2.2, Microsoft.Playwright 1.62.0 (funktionsfähig lokal — E2E-Klassen ohne `Category`-Trait liefen im Unit-Filter mit und bestanden), echtes SQLite über `ProgramScheduleTestDb`.
- **Ermittelte Testsuiten und Quellen der Testbefehle:** CI (`.github/workflows/staging-ci.yml` + `pr-staging-ci.yml`) fährt drei Läufe gegen `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Release): `Category!=E2E` (Unit), `Category=Integration` (keine Tests mit diesem Trait im Bestand), `Category=E2E` (Playwright). AGENTS.md §7 verlangt die volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün; der Baseline-Lauf deckt den CI-Unit-Step (`Category!=E2E`) ab. Hinweis: Der Filter schließt nur getraitete E2E-Klassen aus — einige E2E-Klassen (u. a. `TVSenderAdminE2ETests`, `TVSenderAdminValidityE2ETests`) tragen kein Trait und liefen im Lauf mit.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline Unit | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category!=E2E" --logger "trx;LogFileName=baseline-unit-tests.trx" --logger "console;verbosity=normal"` | Repo-Wurzel | 0 | 2664 | 0 | 1 | [TRX](test-results/baseline-unit-tests.trx), [Konsolenlog](test-results/baseline-unit-tests-console.log) |

Nicht ausgeführt: `Category=E2E`-Trait-Lauf separat (Playwright-E2E-Suite — getraitete Klassen; untraitete E2E-Klassen liefen im Unit-Filter bereits erfolgreich mit) und `Category=Integration` (keine Tests mit diesem Trait vorhanden). Release-Build des Testprojekts erfolgte implizit durch `dotnet test` (kein `--no-build`).

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf endete ohne Fehlschlag.

### Testlücken und Ausführungsprobleme

| Test-ID inkl. Testfall | Suite / Dateipfad | Grund / Umfang | Lauf und Nachweis |
|------------------------|-------------------|----------------|-------------------|
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | `VideoWebPlayer.Tests` | Übersprungen (`NotExecuted`): plattformbedingt Unix-only, unter Windows nicht ausführbar — kein Fehler. | Baseline-TRX |

## Testklassen (ProgramSchedule-relevant)

Dateien unter `VideoWebPlayer.Tests/`; alle DB-Tests laufen gegen echtes SQLite (`ProgramScheduleTestDb`, `file:{dbName}?mode=memory&cache=shared`).

### `ProgramScheduleConventionTests`
- `DirectPassStepCalls_OnlyInApprovedStepTests` — Quellscan: Direktaufrufe von `EnsureCoverageAsync`/`ReconcileAsync`/`PurgeExpiredRulesAsync`/`CleanupSelfDisplacedSlotsAsync` nur in den Dateien der `StepTestFiles`-Whitelist. **Neue Schritt-Testdateien müssen dort eingetragen werden.**

### `ProgramScheduleServiceTests_Interleaving` (N19 Serien-Interleaving)
- `SeedGenreWindowAsync` — 2 Shows à 45 min, Genre-Regel täglich 12:00, `episodeCount: 2`.
- `EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays` — **kodiert Ist-Verhalten**: gleiche Führungsserie an derselben Slot-Position jeden Tag.
- `EnsureCoverage_GenreRule_Interleaving_EpisodeOrderPerShow` — +1-Episodenfortschritt je Show/Tag, 2 `show:`-Cursor-Zeilen.
- `EnsureCoverage_GenreRule_SecondRun_ContinuesEachShowCursor` — Cursor-Fortsetzung über Läufe.
- `StrandOrderKey_IsDeterministic_AndNotAlphabetic` — Hash-Mix-Contract.
- `EnsureCoverage_GenreRule_LeadStrand_FollowsHashOrder_NotAlphabetic` — **kodiert Ist-Verhalten**: Pick 0 der 25.10-Occurrence = Hash-Min-Strang (`episodeCount: 3`, 3 Stränge → `wanted >= m`, heute offset 0; bei verallgemeinerter Rotation betroffen).
- `Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled`, `..._LongAiring_StaysScheduled`, `Reconcile_GenreInterleaved_IdenticalUtcNow_SecondRunIsNoOp` — Validierungs-Idempotenz.

### `ProgramScheduleServiceTests_CollectionOrder` (Issue #334, geordnete `collection:{id}`-Stränge)
- `CreateCollectionAsync` — Hilfsmethode für `MovieCollection`-Seeds.
- `EnsureCoverage_MovieCollection_AirsInReleaseOrder_AndContinuesAcrossRuns` — Release-Reihenfolge + Cursor-Ringumlauf (Flachregel).
- `EnsureCoverage_GenreRule_CollectionStrand_KeepsReleaseOrder` — 3 Stränge (`collection`/`other`/`show`), `episodeCount: 3`; Sammlungstitel strikt in Release-Reihenfolge über Tage, eigener `collection:{id}`-Cursor.
- `EnsureCoverage_MovieCollection_BlockedHead_DefersInsteadOfSkipping` — `PreservesOrder`-Defer.
- `Reconcile_OrderedCollectionRing_StaysReproducible` — Ring-Kontiguität bleibt valid.

### `ProgramScheduleServiceTests_ClipGroups` (Clip-Grace + Segment-Füllung)
- `SeedClipAiringAsync` — Film 76 min, Regel 20:00 `slotDuration` 4 h, Grace 15 min → 3 Titel + Clips.
- `EnsureCoverage_GraceRemainder_FillsWithDistributedClips`, `..._RemainderAboveGrace_StartsNextTitle_Overshoots`, `..._RemainderExactlyGrace_StartsNextTitle`, `..._NoGroups_UnchangedOvershootBehavior` — Grace-Abbruchsemantik.
- `EnsureCoverage_TwoClipGroups_RoundRobinDistributed`, `..._ClipRing_RotatesAcrossRuns` — Gruppen-Round-Robin.
- `EnsureCoverage_InterleavedRule_ClipFillDistributed` — 2 `show:`-Stränge + `slotDuration` + Gruppe → Clip-Slot vorhanden.
- `Reconcile_ClipFillSlots_StayScheduled`, `..._RemovedClipGroupMember_DisplacesAndReplans`, `..._ClipSlotDurationMismatch_DisplacesAiring`, `..._WorkerLoop_WithClipFill_NoDisplacedAccumulation` — Clip-Validierung.

### `ProgramScheduleServiceTests_Issue320` (Deferral-/Cursor-Regressionen, 2-Strang-`SlotDuration`-Regeln)
- `SeedBlockedStrandRuleAsync` — 2 Shows (eine FSK-18), Genre täglich 20:00, `slotDuration` 4 h.
- `Reconcile_UnchangedRules_DeferredStrandInPrefix_StaysScheduled` — deferrierter Strang im ungeschriebenen Präfix.
- `Reconcile_UnchangedRules_UnwrittenPrefixTakeWithLaterGroupTake_StaysScheduled` — Cursor-Bias (135-min-Slot).
- `SetEpisodeDurationAsync` — nachträgliche Laufzeit-Setzung.

### `ProgramScheduleServiceTests_PrimetimeRolling` (Primetime-Szenario, nächste Parallele zum Produktivbefund)
- `SeedScenarioAsync` — `PrimetimeSeed`: Genre-Rotation 20:15, 225-min-Periode, 15 min Grace, Filme 100/95 min + Show 45 min + Clip-Gruppe + Trailer-Gruppe, Horizont 7 Tage.
- `Simulation_WeekOfDailyRuns_PlanOnlyExtendsNoDisplacement` — Wochensimulation über `PrimetimeSeed.StepAsync` (`ExecutePassAsync`), `AssertPreserved`, `AssertStartsInsidePeriod`, `AssertEveryEveningHasTitles`.
- `EnsureCoverage_WeekOfAirings_StrandStructureStableAndOrderContinues` — **kodiert Ist-Verhalten**: Titel-Typ-Sequenz jedes Abends == Referenzabend (Rotations-Regressionstest-Kandidat — schlägt bei geänderter Strang-Rotation fehl oder muss angepasst werden).
- `EnsureCoverage_RuleCreatedMidPrimetime_SchedulesBackToPeriodStart`, `..._FirstRunAfterEndedOccurrence_RingStartsAtBeginning`, `..._AcrossDstChange_EveryAiringStartsAt2015Local`.

### `ProgramScheduleServiceTests_Reconcile` (Re-Bewertung allgemein)
- 25 Tests: Regel-Status (`Reconcile_DeactivatedRule_...`, `Reconcile_DeletedRule_...`, `Reconcile_RuleLeftWindow_...`, `Reconcile_ChangedTimeOfDay_...`), Inhalt (`Reconcile_NewEpisodeInserted_ReplansInAirOrder`, `Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip`, `Reconcile_DeletedMediaItem_...`), Priorität (`Reconcile_PriorityRaised_EvictsLowerPriorityAiring`), Lauf/Anker (`Reconcile_PartiallyAiredOccurrence_*`, `Reconcile_LongAiring_*`, `Reconcile_UnlinkedMediaItem_...`, `Reconcile_*SecondMediaItemProbed_*`), Idempotenz (`Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap`, `Reconcile_IdenticalUtcNow_SecondRunIsNoOp`, `Reconcile_InvalidAiring_LogsRejectionReason` — Log-Grund via `ListLogger`).

### `ProgramScheduleServiceTests_Boundary` (Bound/Overshoot/Anker-Evidenz)
- Overshoot/Shift: `EnsureCoverage_AdjacentWindows_OvershootCompletesAndShiftsSuccessor`, `Reconcile_PartialOccurrence_...`, `Reconcile_ShiftedAndOvershootingAirings_...`, `Reconcile_ShiftedAiring_InvaderDisplaced_...`, `Reconcile_ShiftedAiring_ChainedInvaders_...`, `Reconcile_ShiftedAiring_EvidenceCleaned_ReplansOnce`.
- Min-Start-Rest/Hard-Bound: `EnsureCoverage_RemainderBelowMinRemain_PlansFiller`, `EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence`, `ProgramScheduleOptions_MinStartRemain_Normalizes`, `..._MaxOccurrenceLookback_Normalizes`, `Reconcile_PathologicalEpisodeCount_AnchorHeadroomSaturates`.
- Housekeeping: `DeleteDisplacedSlots_*`, `ResetSchedule_RemovesAllSlotsAndCursors`.

### `ProgramScheduleServiceTests_RingOrder` (Hash-Mischung ungeordneter Ringe, Issue #297)
- `ItemOrderKey_IsDeterministic_AndRuleDependent`, `EnsureCoverage_MovieCollectionRing_FirstPick_IsReleaseFirst`, `..._UnorderedRing_HeadPicks_DistributeAcrossRules`, `..._GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder`, `..._PlaylistRing_*`, `..._ClipSourceRing_*`, `..._ClipGroupRing_*`, `..._RingOrder_StableAcrossIndependentFixtures`, `Reconcile_MixedRing_StaysReproducible`, `EnsureCoverage_TrailerRing_KeepsAiringsOrder`.

### `ProgramScheduleServiceTests_Stability` (Reproduzierbarkeits-Schleifen)
- `Repro_MixedSchedule_ReconcileLoops_NoFalsePositives`, `Reconcile_PlaylistSecondMediaItemProbed_SlotStaysScheduled`, `Repro_ClipFillAiring_ReconcileLoops_NoFalsePositives`.

### `ProgramScheduleServiceTests_InvalidReason`
- `StageOrdering_IsStrictlyIncreasing` — Enum-Stufenvertrag von `AiringInvalidReason`.

### `ProgramScheduleServiceTests_CursorSelection` / `ProgramRuleCursor*`
- `SelectStrandCursor_NullStrandKeyDuplicates_ReturnsYoungest`, `EnsureCoverage_NullStrandKeyDuplicates_PersistCursorsUpdatesWinner` (Index gedroppt im Test).
- `ProgramRuleCursorUniquenessTests` — NULL-/Composite-Unique-Index auf echtem SQLite.
- `ProgramRuleCursorNullStrandKeyMigrationTests` — Datenmigration der NULL-Duplikate.

### Weitere ProgramSchedule-Testklassen (Kontext)
- `_Generation` (Occurrences/Validity/Flachring/SlotDuration-Füllung/`SecondRun_ExtendsNotDuplicates`), `_Genres` (Genre-Expansion/Filter/Seasonal), `_Conflicts` (Priorität), `_AgeWindow`/`_PrimetimeAgeWindow` (Fenster/Skip/Defer), `_PrimetimeChanges`/`_PrimetimeGrace`/`_PrimetimeTrailer` (Primetime-Szenarien), `_Access`/`_ContentScope` (Freigabe), `_CoverageBoundary` (Coverage-Rand), `_ClipFillPeriodBounds` (Periodenende/Bounds des Clip-Fills), `_ClipSource` (Clip-Quellen), `_Filler` (Füller), `_Diagnosis` (Regeldiagnose), `_Issue315` (laufender Slot/Evidenz), `_Cleanup`/`_Purge` (Housekeeping), `_Halloween` (saisonale Genres), `ProgramScheduleWorkerTests`, `ProgramScheduleSignalTests`, `ProgramScheduleAgeWindowTests`, `ProgramSlotTests_DateTimeKind`, `ProgramGuide*`, `TVSenderAdmin*Tests`/`...E2ETests` (Admin-UI, bUnit + teils ungetraitete Playwright).

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync(dbName, ct)` — shared-memory-SQLite + `EnsureCreated`, seedet `Setup` (`TvSenderEnabled`, `UnknownRatingMode = Unrestricted`) und die reservierte Trailer-Gruppe.
- `CreateService(scheduleOptions?, logger?)` — `ProgramScheduleService` mit `ProgramRuleService`/`ProgramSettingsService`/`NullLogger` (Logger injizierbar, z. B. `ListLogger`).
- `CreateRuleAsync(channelId, name, type, recurrence, timeOfDay, materialType, materialId, priority?, episodeCount?, slotDuration?, dayOfWeek?, singleDate?, isActive?, genreIds?, mediaSourceIds?, clipGroupIds?, placeholderGrace?, validity?)` — direkte Regelanlage inkl. Join-Zeilen und `ProgramRuleService.ApplyValidity`-Normalisierung.
- `CreateMovieAsync(name, sourceId, duration, collectionId?, releaseDate?, ageRatingMinAge?, ageRating?)` / `CreateShowAsync(name, episodeCount, sourceId, duration, ageRatingMinAge)` / `CreateMediaItemAsync(sourceId, duration)` / `SeedProbeItemAsync(name, duration, probedAt, attempts, ct)` — Inhalte mit probierter Laufzeit.
- `CreateGenreAsync(name, sourceId, isHidden?, alternateNames?, startDate?, endDate?)`, `LinkMovieGenreAsync`, `LinkShowGenreAsync` — Genre-Seeds/Links.
- `CreateClipGroupAsync`, `AddClipToGroupAsync`, `SeedTrailerClipGroupAsync` — Clip-Gruppen.
- `CreateSlotAsync(channelId, startUtc, endUtc, mediaItemId, movieId?, episodeId?, ruleId?, ruleType?, status?, isClipFill?)` — direkte Slot-Anlage.
- `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync`, `LinkMovieMediaItemAsync`, `LinkEpisodeMediaItemAsync` — Zugriff/Alternativen.
- `GetChannelAsync`, `SlotsAsync`, `SaveDiagAsync` (FK-Diagnose-Dump).
- Assert-Helfer: `AssertNoDisplacedAsync`, `AssertReconcileNoOpAsync`, `AssertNoSelfDisplacedAsync` (mit Slot-Dump im Fehlertext).

### `PrimetimeSeed` (`VideoWebPlayer.Tests/Helpers/PrimetimeSeed.cs`)
- Konstanten `Start` (20:15), `Period` (225 min), `Grace` (15 min), `HorizonDays` (7).
- `LocalUtc`/`ToLocal`/`LocalMinutesOfDay`/`EveningDateOf` — maschinen-TZ-feste Zeithelfer.
- `SetHorizonDaysAsync`, `CreateRuleAsync` (Primetime-Regel: Genre, täglich, `slotDuration`, `mediaSourceIds:[1]`, `clipGroupIds`, Grace), `CreateHolidayRuleAsync` (Annual-Window-Variante).
- `AddMovieGenreAsync`, `AddShowGenreAsync`, `AddClipGroupAsync`, `AddTrailerAsync` — Szenario-Seeds.
- `StepAsync` — ein Worker-Schritt über `ExecutePassAsync` (konventionskonforme Pass-Simulation).
- `AssertPreserved`, `AssertStartsInsidePeriod`, `AssertEveryEveningHasTitles`, `AiringOf` — Szenario-Assertions.

### `ListLogger<T>` (`VideoWebPlayer.Tests/Helpers/ListLogger.cs`)
- Fängt Log-Zeilen (z. B. „Ausstrahlung invalide … Grund {Reason}") für Assertions ab.
