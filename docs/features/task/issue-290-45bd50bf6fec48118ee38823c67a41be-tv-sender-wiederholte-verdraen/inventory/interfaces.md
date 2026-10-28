# Interfaces — Bestandsaufnahme (Issue #290)

## `IProgramScheduleService`
Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleRunSummary>` | Extend-only: Plan bis `utcNow + Horizon` auffüllen, bestehende Slots unverändert |
| `ReconcileAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Neu bewerten: ungültige `Scheduled`-Slots → `Displaced` (nie gelöscht), freie Fenster neu materialisieren, Serien per Cursor in Sendedreihenfolge nachziehen |
| `GetScheduleAsync` | `long channelId`, `DateTimeOffset fromUtc`, `int maxCount`, `CancellationToken` | `Task<List<ProgramSlot>>` | Kommende Slots (`Scheduled` **und** `Displaced`) ab `fromUtc` — Admin-Planansicht |
| `DeleteDisplacedSlotsAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<int>` | Alle `Displaced`-Zeilen löschen (Admin) |
| `ResetScheduleAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<ProgramScheduleResetResult>` | Alle Slots + Regel-Cursor löschen — komplette Neuplanung (Admin) |
| `GetRuleDiagnosesAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Pro-Regel-Planbarkeitsdiagnose (Admin) |

Implementiert von `ProgramScheduleService`; DI: `ServiceCollectionExtensions.cs` (`AddScoped<IProgramRuleService, ProgramRuleService>`, `AddScoped<IProgramScheduleService, ProgramScheduleService>`).

## `IProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | — | `void` | Planeingaben geändert; weckt den Worker sofort (unterdrückt während offenem Scan-Scope) |
| `BeginScan` | — | `IDisposable` | Scan-Scope öffnen: Signale werden bis zum Dispose des letzten Scopes zurückgehalten, dann einmal geweckt |
| `WaitAsync` | `CancellationToken` | `ValueTask` | Warten auf das nächste Signal; anstehende Signale koaleszieren |

Implementiert von `ProgramScheduleSignal` (Singleton). Konsumenten: `ProgramScheduleWorker`, `LiveStreamWorker`. Produzenten: siehe [ausloeser.md](ausloeser.md).

## Ergebnis-Records (gleiche Datei `IProgramScheduleService.cs`)

| Record | Felder | Zweck |
|--------|--------|-------|
| `ProgramScheduleRunSummary` | `SlotsCreated`, `FillerSlotsCreated`, `SkippedNoDuration`, `SkippedNoAccess`, `OrphanedRules`, `DroppedConflicts`, `RemainingGapSeconds`, `BlockedByAgeWindow` | Ergebnis eines `EnsureCoverageAsync`-Laufs |
| `ProgramScheduleReconcileSummary` | `SlotsDisplaced`, `SlotsCreated`, `FillerSlotsCreated`, `SkippedNoDuration`, `SkippedNoAccess`, `OrphanedRules`, `DroppedConflicts`, `RemainingGapSeconds`, `BlockedByAgeWindow` | Ergebnis eines `ReconcileAsync`-Laufs — `SlotsDisplaced`/`SlotsCreated` sind die Größen, die bei Idempotenz `0` sein müssten |
| `ProgramRuleDiagnosis` | `RuleId`, `MaterialMissing`, `CandidateCount`, `SchedulableCount`, `NoMediaItemCount`, `ProbePendingCount`, `ProbeExhaustedCount`, `NoAccessCount`, `AgeWindowBlockedCount` | Diagnose je Regel für die Admin-UI |
| `ProgramScheduleResetResult` | `SlotsDeleted`, `CursorsDeleted` | Ergebnis von `ResetScheduleAsync` |

## Verwandte, indirekt beteiligte Interfaces (Kontext)

- `IProgramRuleService` (`VideoWebPlayer/Services/IProgramRuleService.cs`): Kanal-/Regel-CRUD — alle Mutationen feuern das Signal (Implementierung `ProgramRuleService`).
- `IProgramClipGroupService` (`VideoWebPlayer/Services/IProgramClipGroupService.cs`): Clip-Gruppen-CRUD — feuert ebenfalls (Implementierung `ProgramClipGroupService`).
- `IProgramGuideService` (`VideoWebPlayer/Services/IProgramGuideService.cs`): Programmauskunft; `ProgramGuideService` filtert strikt `Status == Scheduled`.
- `IBackgroundProcessingGate` (`VideoWebPlayer/Services/Backups/`): Lease-Gate, hinter dem beide Worker laufen (Backup-Restore darf nicht überfahren werden).
