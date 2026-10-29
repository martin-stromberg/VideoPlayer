# Tests – Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-29, Lauf gestartet ca. 18:23 +0100, Ende 18:28 +0100 (Dauer 5 min 38 s)
- Branch und Commit-ID: `task/285aeee4bd0b4c5383e642b11843c386-unnoetige-verdraengungen-im-se`,
  Commit `083371de5663521a4906791a01dd6d32aaf2d350`
  („feat: Sendeplan-Tage einzeln über Auswahlleiste wählbar (Issue #306) (#309)", Stand = staging-Basis)
- Uncommittete Änderungen im getesteten Stand: keine Produktiv-/Teständerungen; einzige
  untracked Datei ist das Feature-Arbeitsverzeichnis
  `docs/features/task-285aeee4bd0b4c5383e642b11843c386-unnoetige-verdraengungen-im-se/`
  (`requirement.md`, `todo.md`, diese Bestandsaufnahme samt Nachweisen)
- Testumgebung und Runtime-/SDK-Versionen: Windows, Git Bash; .NET SDK 10.0.403,
  Testziel `net10.0` (Debug-Build); Playwright-Chromium vorhanden (E2E-Tests liefen mit,
  nur 1 Test meldete `SkipBrowser`-ähnlichen Auslassgrund — s. u.)
- Ermittelte Testsuiten und Quellen der Testbefehle: einziges Testprojekt
  `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (xUnit); Standardbefehl laut
  Projektregeln `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vom Repo-Root.
  Keine weiteren Testsuiten (kein separates E2E-Projekt — E2E-Tests sind Teil der Suite).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "trx;LogFileName=baseline.trx" --results-directory docs/features/task-285aeee4bd0b4c5383e642b11843c386-unnoetige-verdraengungen-im-se/inventory/test-results` | Repo-Root `D:\Repositories\softwareschmiede\285aeee4-bd0b-4c53-83e6-42b11843c386` | 0 | 2713 | 0 | 2 | [baseline-console.log](test-results/baseline-console.log), [baseline.trx](test-results/baseline.trx) |

### Nachgewiesene bestehende Testfehler

Keine — der Ausgangslauf ist vollständig grün (0 Fehlschläge von 2715 ausgeführten Tests).

### Testlücken und Ausführungsprobleme

2 übersprungene Tests (xUnit `[SKIP]`-Marker im Konsolenlog, `NotExecuted` im TRX):

| Test-ID | Suite / Dateipfad | Grund |
|---------|-------------------|-------|
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | `VideoWebPlayer.Tests/Services/FfmpegToolDownloadServiceTests_Install.cs` | Plattform-Skip: „Unix-Dateirechte lassen sich nur unter Unix prüfen." (läuft unter Windows nicht) |
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | `VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs` | Bewusst deaktiviert mit dokumentiertem Befund: „api/continue-watching antwortet mit 500, wenn ein Film zu keiner Filmsammlung gehoert (ContinueWatchingService.Create ohne Null-Pruefung). Wird laut Auftrag in diesem Lauf nicht behoben." — bekanntes, von dieser Anforderung unabhängiges Problem |

Keine Build-, Setup- oder Infrastrukturfehler. Kein Flackern beobachtet (Einzellauf).

## Testklassen

Testprojekt: `VideoWebPlayer.Tests/` (xUnit, reale In-Memory-SQLite über
`ProgramScheduleTestDb` bzw. eigene `SqliteConnection`-Fixtures; E2E über
`WebApplicationFactory` + Kestrel + Playwright).

### `ProgramScheduleServiceTests_Reconcile`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Reconcile.cs` — zentrale
Reconcile-Testsuite (25 Tests).

- `Reconcile_NoChanges_LeavesEverythingScheduled` — ohne Änderung bleibt alles `Scheduled`; Plan bleibt lückenlos
- `Reconcile_DeactivatedRule_DisplacesFutureAndRefills` — deaktivierte Regel: Zukunft verdrängt + Fenster neu gefüllt
- `Reconcile_DeletedRule_DisplacesSlotsAndKeepsRows` — gelöschte Regel: verdrängt, Zeilen bleiben (weiche Referenz)
- `Reconcile_RuleLeftWindow_DisplacesFutureAirings` — verengtes Gültigkeitsfenster: Occurrence-Anker verloren → verdrängt
- `Reconcile_ChangedTimeOfDay_ReplansAtNewTime` — geänderte Sendezeit → Neuplanung zur neuen Zeit
- `Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip` — Serien-Verdrängung: gleiche Episoden in Sendereihenfolge neu eingeplant
- `Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip` — gelöschte Datei → verdrängt, nächste Episoden ohne Sprung
- `Reconcile_PriorityRaised_EvictsLowerPriorityAiring` — Prioritätserhöhung verdrängt schwächere Ausstrahlung
- `Reconcile_NewEpisodeInserted_ReplansInAirOrder` — neue Episode mittendrin → Neuplanung in Sendereihenfolge
- `EnsureCoverage_IgnoresDisplacedSlots` — Extend-only-Lauf ignoriert verdrängte Zeilen als Abdeckung
- `Reconcile_PartiallyAiredOccurrence_KeepsTailScheduled` — laufende Ausstrahlung: geschriebener Rest bleibt geplant (kein Oszillieren)
- `Reconcile_PartiallyAiredOccurrence_TomorrowAiringsStayStable` — Folge-Ausstrahlungen positionsstabil (Idempotenz)
- `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap` — 3× Reconcile+Ensure: keine Verdrängungs-Häufung, keine Überlappung
- `GetSchedule_ReturnsUpcomingSlotsWithStatus` — Admin-Abfrage inkl. Status
- `Reconcile_LongAiring_StaysScheduled` — >24 h-Ausstrahlung: Fenster-Widening hält Anker beweisbar
- `Reconcile_LongAiring_BeyondTwoDays_StaysScheduled` — Occurrence-Tage-Loop reicht über 2 Tage zurück
- `Reconcile_MultiPartLongAiring_StaysScheduled` — mehrteiliger Marathon: Widening-Trigger über Slot am Fensterrand
- `Reconcile_LongAiring_Shifted_StaysScheduled` — invader-verschobener Langfilm: Evidenz liegt im erweiterten Fenster
- `Reconcile_PartiallyMaterializedLongAiring_NoTrigger_StaysScheduled` — validierungsgetriebenes Widening ohne Trigger
- `Reconcile_LongAiring_ChangedRule_StillDisplaced` — echte Änderung bleibt verdrängt (Widening rettet nichts Ungültiges)
- `Reconcile_LongAiring_BeyondMaxLookback_Displaced` — dokumentierte Grenze: jenseits `MaxOccurrenceLookbackDays` wird verdrängt
- `Reconcile_MovieSecondMediaItemProbed_SlotStaysScheduled` — Issue #290: Repräsentantenwechsel nach Probe verdrängt nicht
- `Reconcile_EpisodeSecondMediaItemProbed_SlotStaysScheduled` — Serien-Pendant des Repräsentantenwechsels
- `Reconcile_UnlinkedMediaItem_StillDisplacesOnce` — echte Änderung (Entlinken) → genau einmal ersetzt, Folgelauf stabil
- `Reconcile_IdenticalUtcNow_SecondRunIsNoOp` — Issue #290 Baseline: zweiter Lauf gleiches `utcNow` → `AssertReconcileNoOpAsync` (0/0)

### `ProgramScheduleServiceTests_Boundary`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Boundary.cs` —
Occurrence-Fenstergrenzen; u. a.:

- `EnsureCoverage_AdjacentWindows_OvershootCompletesAndShiftsSuccessor` — Overshoot schiebt Nachfolger
- `Reconcile_PartialOccurrence_OvershootingTailStaysScheduled_SuccessorShifts` — laufender Overshoot: Tail bleibt, Nachfolger verschiebt sich
- `Reconcile_ShiftedAndOvershootingAirings_StayStableAcrossRuns` — verschobene Ausstrahlungen stabil über Läufe
- `Reconcile_ShiftedAiring_InvaderDisplaced_StableAcrossRuns` — Issue #290: Displaced-Zeilen als Invader-Evidenz (Oszillations-Schutz)
- `EnsureCoverage_OversizedHeadItem_AirsAndOvershootsSuccessor`, `EnsureCoverage_ShortRemainder_OvershootingItem_Completes`, `EnsureCoverage_RemainderBelowMinRemain_PlansFiller`, `EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence` — Bound-Semantik
- `ProgramScheduleOptions_MinStartRemain_Normalizes`, `ProgramScheduleOptions_MaxOccurrenceLookback_Normalizes` — Options-Normalisierung
- `DeleteDisplacedSlots_RemovesOnlyDisplaced`, `DeleteDisplacedSlots_NonAdmin_Throws`, `ResetSchedule_RemovesAllSlotsAndCursors` — Admin-Aktionen

### `ProgramScheduleServiceTests_Interleaving`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Interleaving.cs` —
Strang-Verzahnung von Genre-Regeln; u. a.:

- `EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays`, `EnsureCoverage_GenreRule_Interleaving_EpisodeOrderPerShow`, `EnsureCoverage_GenreRule_SecondRun_ContinuesEachShowCursor` — Interleaving + Strang-Cursor
- `StrandOrderKey_IsDeterministic_AndNotAlphabetic`, `EnsureCoverage_GenreRule_LeadStrand_FollowsHashOrder_NotAlphabetical` — deterministische Strangordnung
- `Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled` — unveränderte Interleaved-Ausstrahlung bleibt
- `Reconcile_GenreRule_Interleaved_LongAiring_StaysScheduled` — Langzeit-Ausstrahlung interleaved
- `Reconcile_GenreInterleaved_IdenticalUtcNow_SecondRunIsNoOp` — Issue #290 Baseline, mehrsträngig → No-Op

### `ProgramScheduleServiceTests_RingOrder`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_RingOrder.cs` —
deterministische Ringmischung (Issue #297); u. a.:

- `ItemOrderKey_IsDeterministic_AndRuleDependent`, `EnsureCoverage_*_FirstPick_FollowsItemOrderKey` (MovieCollection/ClipSource/ClipGroup) — Hash-Mix
- `EnsureCoverage_UnorderedRing_HeadPicks_DistributeAcrossRules`, `EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder` — Streuung vs. Sendereihenfolge
- `EnsureCoverage_PlaylistRing_ManualSortOrder_IsNotMixed`, `EnsureCoverage_PlaylistRing_ByReleaseDate_KeepsReleaseOrder`, `EnsureCoverage_TrailerRing_KeepsAiringsOrder` — geordnete Ringe bleiben ungemischt
- `EnsureCoverage_RingOrder_StableAcrossIndependentFixtures` — Prozess-Invarianz
- `Reconcile_MixedRing_StaysReproducible` — gemischter Ring bleibt reproduzierbar

### `ProgramScheduleServiceTests_ClipGroups`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipGroups.cs` — Clip-Füllung;
reconcile-relevant:

- `Reconcile_ClipFillSlots_StayScheduled` — Clip-Füll-Slots bleiben geplant
- `Reconcile_RemovedClipGroupMember_DisplacesAndReplans` — entferntes Gruppenmitglied → verdrängt + neu geplant
- `Reconcile_WorkerLoop_WithClipFill_NoDisplacedAccumulation` — Worker-Takt mit Clip-Fill ohne Verdrängungs-Häufung
- (plus 16 EnsureCoverage-Tests zu Grace, Verteilung, Round-Robin, Trailer-Gruppe)

### `ProgramScheduleServiceTests_ClipSource` / `ProgramScheduleServiceTests_Trailer` / `ProgramScheduleServiceTests_ClipFillPeriodBounds`

- `Reconcile_ClipSourceFillerSlots_StayScheduled` — Clip-Quellen-Füller bleibt geplant
- `Reconcile_KeptTrailerSlot_StaysScheduled` — Trailer-Slot bleibt geplant
- `EnsureCoverage_StartedMidPeriod_NoClipsAfterNominalPeriodEnd`, `EnsureCoverage_OccupiedInsidePeriod_ClipFillDoesNotOverlap`, `EnsureCoverage_FullPrimetimeScenario_ClipsDistributedInsidePeriod` — Perioden-Grenzen der Clip-Füllung

### `ProgramScheduleServiceTests_AgeWindow` / `ProgramScheduleAgeWindowTests`

Dateien: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_AgeWindow.cs`,
`VideoWebPlayer.Tests/ProgramScheduleAgeWindowTests.cs`; reconcile-relevant:

- `Reconcile_SameWindow_KeepsAiring_TighterWindow_Displaces` — verschärftes Fenster verdrängt
- `Reconcile_InterleavedWindowSkip_StaysReproducible_TighterWindowDisplaces` — Fenster-Skip in Interleaved-Struktur
- `Reconcile_DisplacedUnorderedRing_ReoffersSkippedPositionOnlyInWindow` — verdrängter ungeordneter Ring bietet übersprungene Position nur im Fenster wieder an

### `ProgramScheduleServiceTests_ContentScope` / `ProgramScheduleServiceTests_Genres`

- `Reconcile_SwitchBackToSharedOnly_DisplacesUnreleasedSlots` — Scope-Rückschaltung verdrängt nicht-freigegebene Slots
- `Reconcile_SeasonalGenre_UtcNowBasis_DeterministicExpansion` — Genre-Auflösung folgt Lauf-`utcNow`

### `ProgramScheduleServiceTests_Purge`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Purge.cs` —
`PurgeExpiredRules_DeletesExpiredOneTimeRule`, `PurgeExpiredRules_SlotsDisplacedAfterReconcile`,
`PurgeExpiredRules_KeepsOpenEndAndRecurringRules`.

### `ProgramScheduleWorkerTests`

Datei: `VideoWebPlayer.Tests/ProgramScheduleWorkerTests.cs` —
`Worker_RunsEnsureCoverage_OnStartup`, `Worker_IdlesWithoutSchedule_WhenFeatureNotReleased`,
`Worker_Run_PurgesExpiredRules`, `Worker_DisabledFeature_SkipsPurge`
(eigene In-Memory-SQLite-DI-Fixtures, echter Worker gegen `TimeProvider.System`).

### `ProgramScheduleSignalTests`

Datei: `VideoWebPlayer.Tests/ProgramScheduleSignalTests.cs` —
`Notify_WakesWaiter`, `Notify_IsCoalesced`,
`Notify_DuringScan_IsSuppressed_UntilScanEnds`,
`ScanEnd_WithoutSuppressedNotification_DoesNotWake`.

### `ProgramScheduleServiceTests_Generation` / `_Access` / `_Filler` / `_Conflicts` / `_CursorSelection` / `_Diagnosis`

Rechen-/Randlogik ohne direkten Reconcile-Bezug: Generierungs-/Recurrence-Tests,
Freigabe-Kontext, Füller-Auswahl, Prioritätskonflikte, Cursor-Sieger, Diagnose-Zähler.

### `ProgramSlotTests_DateTimeKind`

Datei: `VideoWebPlayer.Tests/ProgramSlotTests_DateTimeKind.cs` — UTC-`DateTimeKind`-
Roundtrip von `ProgramSlot` unter SQLite.

### UI-/E2E-Tests

- `TVSenderAdminE2ETests.TVSenderAdmin_RepeatGenerate_ProducesNoDisplacedSeries`
  (`VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs:649`) — Issue #290 E2E-Regression:
  zweimal „Jetzt generieren" → 0 `Displaced`-Zeilen, keine „Verdrängt"-Chips.
- `TVSenderAdminTests` (`VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs:516ff`) —
  Displaced-Slot rendert „Verdrängt"-Chip (bUnit).
- `TVSenderAdminTests_ClipBlocks.Sendeplan_DisplacedClip_NotGrouped` — Displaced-Clip
  wird nicht zum Clip-Block gruppiert.
- `ScheduleDisplayItemsTests.BuildScheduleDisplayItems_DisplacedClip_RendersSingle_AndSplits`
  — Displaced-Clip bricht die Blockgruppierung.

## Hilfsmethoden

### `ProgramScheduleTestDb`

Datei: `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` — geteilte
In-Memory-SQLite-Fixture (echte FKs); seeds `TvSenderEnabled = true` und
`ProgramScheduleUnknownRatingMode = Unrestricted` plus reservierte Trailer-Clip-Gruppe.

- `CreateAsync(dbName, ct)` — Fixture-Aufbau inkl. Setup-Seed und Trailer-Gruppe
- `SeedTrailerClipGroupAsync`, `CreateClipGroupAsync`, `AddClipToGroupAsync` — Clip-Gruppen-Seeds
- `CreateService(scheduleOptions?)` — `ProgramScheduleService` gegen die Test-DB (optional mit `ProgramScheduleOptions`, z. B. `MaxOccurrenceLookbackDays`)
- `CreateGuideService` — `ProgramGuideService`
- `CreateSlotAsync` — direkter Slot-Seed (Status, `IsClipFill` wählbar)
- `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync` — Benutzer/Quellen/Freigaben
- `CreateMediaItemAsync`, `SeedProbeItemAsync` — MediaItems mit Probe-Status
- `CreateMovieAsync`, `CreateShowAsync`, `CreateGenreAsync` — Inhalte mit probed Dauer/Altersfreigabe
- `LinkMovieGenreAsync`, `LinkShowGenreAsync` — Genre-Verknüpfungen
- `LinkMovieMediaItemAsync`, `LinkEpisodeMediaItemAsync` — zweite Datei an Inhalt (Issue #290, Repräsentantenwechsel)
- `CreateRuleAsync` — Regel-Seed (Recurrence, Material, Priorität, `clipGroupIds`, `placeholderGrace`, `validity`)
- `GetChannelAsync` — Kanal über `ProgramRuleService`
- `SaveDiagAsync` — SaveChanges mit FK-Diagnose im Fehlerfall
- `SlotsAsync` — alle Slots sortiert
- `AssertNoDisplacedAsync` — schlägt bei `SlotsDisplaced > 0` (Displaced-Dump im Fehlertext)
- `AssertReconcileNoOpAsync` — Issue #290: schlägt bei `SlotsDisplaced > 0 || SlotsCreated > 0` (voller Slot-Dump) — No-Op-Nachweis
- `AssertGapless` — Lückenlosigkeits-Assertion über `[from, to]`
