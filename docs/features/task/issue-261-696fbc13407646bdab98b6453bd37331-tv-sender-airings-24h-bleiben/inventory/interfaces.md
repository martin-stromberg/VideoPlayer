# Interfaces

## `IProgramScheduleService`

Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

Implementiert von `ProgramScheduleService`; konsumiert u. a. von `ProgramScheduleWorker`
(`ReconcileAsync` + `EnsureCoverageAsync` im Intervall) sowie den Admin-/Guide-Tests.
Die Anforderung ist eine interne Logikkorrektur — der Contract bleibt voraussichtlich unverändert.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleRunSummary>` | Extend-only-Ausbau des Plans bis `utcNow + horizon` |
| `ReconcileAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Neubewertung: invalide Airings → `Displaced`, Fenster neu materialisieren |
| `GetScheduleAsync` | `long channelId`, `DateTimeOffset fromUtc`, `int maxCount`, `CancellationToken` | `Task<List<ProgramSlot>>` | Slots ab `fromUtc` inkl. Status (Admin-Ansicht) |
| `DeleteDisplacedSlotsAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<int>` | Löscht `Displaced`-Slots (Admin) |
| `ResetScheduleAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<ProgramScheduleResetResult>` | Löscht alle Slots + Cursor (Admin) |
| `GetRuleDiagnosesAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Pro-Regel-Diagnose (Admin) |

### Zugehörige Record-Typen (gleiche Datei)

- `ProgramScheduleRunSummary(SlotsCreated, FillerSlotsCreated, SkippedNoDuration, SkippedNoAccess, OrphanedRules, DroppedConflicts, RemainingGapSeconds)`
- `ProgramScheduleReconcileSummary(SlotsDisplaced, SlotsCreated, FillerSlotsCreated, SkippedNoDuration, SkippedNoAccess, OrphanedRules, DroppedConflicts, RemainingGapSeconds)`
- `ProgramRuleDiagnosis(RuleId, MaterialMissing, CandidateCount, SchedulableCount, NoMediaItemCount, ProbePendingCount, ProbeExhaustedCount, NoAccessCount)`
- `ProgramScheduleResetResult(SlotsDeleted, CursorsDeleted)`

## `IProgramScheduleSignal`

Wird von `ProgramScheduleWorker` optional konsumiert (`WaitAsync`), um bei plan-relevanten
Änderungen vorzeitig aufzuwachen — für diese Anforderung nur als Aufrufkontext relevant.
