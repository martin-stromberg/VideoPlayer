# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt: 2026-10-26 (lokal, Europe/Berlin)
- Branch/Commit: `task/issue-291-…-tv-sender-platzhalter-clips-in` @ `b695ef2` (Basis: Standby-Clip #266)
- Uncommittete Änderungen im getesteten Stand: nur Feature-Verzeichnis `docs/features/task-issue-291-…` (Dokumente), kein Code
- Umgebung: .NET SDK 10.0.402, Windows, `dotnet test` (xUnit)
- Testsuiten lt. CI (`staging-ci.yml`): `Category!=E2E`, `Category=Integration`, `Category=E2E` (Playwright)

### Testläufe

| Lauf | Befehl | Arbeitsverzeichnis | Exit | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------|--------------------|------|-------------|----------------|--------------|----------|
| Baseline non-E2E | `dotnet test VideoWebPlayer.Tests -c Debug --no-build --filter "Category!=E2E"` | Repo-Root | 0 | 2248 | 0 | 1 | [baseline-non-e2e.trx](test-results/baseline-non-e2e.trx) |

Hinweis: Der Filter `Category!=E2E` erfasst in diesem Projekt auch E2E-benannte Klassen (die Kategorie-Traits sind nicht konsistent gesetzt); der Lauf enthielt u. a. `TVSenderAdminE2ETests`. Keine nachgewiesenen bestehenden Testfehler.

### Testlücken und Ausführungsprobleme

- Der explizite `Category=E2E`- bzw. `Category=Integration`-Teillauf wurde für die Bestandsaufnahme nicht separat ausgeführt; die Bestandsaufnahme nutzt den Vollsuite-Lauf ohne E2E-Filter. Für den Abschluss (Definition of Done) ist die volle Suite vorgesehen.
- `dotnet build VideoPlayer.sln -c Debug`: fehlerfrei (140 preexisting Warnungen, v. a. BL0008).

## Testklassen (bestehend, Bereich Clip-Füllung)

### `ProgramScheduleServiceTests_ClipGroups`
- `EnsureCoverage_GraceRemainder_FillsWithDistributedClips` — Rest < Grace → Clips auf n+1 Segmente verteilt, lückenlos bis Periodenende
- `EnsureCoverage_RemainderAboveGrace_StartsNextTitle_Overshoots` — Rest ≥ Grace → weiterer Titel mit Überhang
- `EnsureCoverage_RemainderExactlyGrace_StartsNextTitle`, `EnsureCoverage_NoGroups_UnchangedOvershootBehavior`
- `EnsureCoverage_LastClip_MayOvershootPeriodEnd` — letzter Clip darf über Periodenende ragen
- `EnsureCoverage_ClipTooLargeForFreeRoom_LeavesDocumentedGap`, `EnsureCoverage_ClipsSkipItemsWithoutDuration`
- `EnsureCoverage_ClipGroup_NotFullySharedSource_YieldsNoClipSlots`, `EnsureCoverage_TwoClipGroups_RoundRobinDistributed`
- `EnsureCoverage_ClipOvershoot_ReservedBoundWindow_ClipDropped` — Clip darf nicht in Bound-Fenster ragen
- `EnsureCoverage_ClipRing_RotatesAcrossRuns`, `EnsureCoverage_TrailerGroup_*`, `EnsureCoverage_EpisodeCountAndSlotDuration_FillsTailWithClips`
- `EnsureCoverage_InterleavedRule_ClipFillDistributed`, `Reconcile_ClipFillSlots_StayScheduled`, `Reconcile_RemovedClipGroupMember_DisplacesAndReplans`, `Reconcile_WorkerLoop_WithClipFill_NoDisplacedAccumulation`

### `ProgramScheduleServiceTests_Boundary`
- Bound-/Overshoot-Verhalten der Titel-Materialisierung (weiche/harte Grenzen, `minStartRemain`)

## Neue Reproduktionstests (vor Implementierung, erwartungsgemäß rot)

`ProgramScheduleServiceTests_ClipFillPeriodBounds` (neu):
- `EnsureCoverage_StartedMidPeriod_NoClipsAfterNominalPeriodEnd` — ROT: 33+ Clips starten nach Periodenende 00:15 (Defekt A)
- `EnsureCoverage_OccupiedInsidePeriod_ClipFillDoesNotOverlap` — ROT: Titel 21:19–23:05 überlappt belegten Slot 23:00–23:30 (Defekt B)
- `EnsureCoverage_FullPrimetimeScenario_ClipsDistributedInsidePeriod` — grün (Positivkontrolle: Verteilung/Lückenlosigkeit im fehlerfreien Fall)

## Hilfsmethoden

### `ProgramScheduleTestDb`
- `CreateAsync`, `GetChannelAsync`, `CreateRuleAsync` (mit `clipGroupIds`, `placeholderGrace`, `slotDuration`, `priority`, `episodeCount`), `CreateMovieAsync` (mit `collectionId`), `CreateMediaItemAsync`, `CreateClipGroupAsync`, `AddClipToGroupAsync`, `CreateShowAsync`, `AssertGapless`, `AssertNoDisplacedAsync`, `SlotsAsync`
