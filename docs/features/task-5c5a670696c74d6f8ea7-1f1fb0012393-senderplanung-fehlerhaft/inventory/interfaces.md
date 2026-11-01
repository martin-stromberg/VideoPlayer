# Interfaces — Bestandsaufnahme Senderplanung

## `IProgramScheduleService`

Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

Implementiert von `ProgramScheduleService` (`internal sealed`); registriert als Scoped
in `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` Z. 282
(`AddScoped<IProgramScheduleService, ProgramScheduleService>`). Aufgerufen vom
`ProgramScheduleWorker` in der Reihenfolge `PurgeExpiredRulesAsync` →
`CleanupSelfDisplacedSlotsAsync` → `ReconcileAsync` → `EnsureCoverageAsync` — dieselbe
Reihenfolge simuliert `PrimetimeSeed.StepAsync`. Der `utcNow`-Parameter ist der
vereinbarte Zeitsteuerungspunkt der Tests; Signaturen bleiben laut Anforderung
unverändert.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `utcNow`, `cancellationToken` | `Task<ProgramScheduleRunSummary>` | Erweitert den Plan bis zum Horizont (extend-only); baut Occurrences mit `OccurrenceLookback` (24 h) hinter `coverageStart` und materialisiert sie |
| `ReconcileAsync` | `utcNow`, `cancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Re-bewertet `[utcNow, utcNow+Horizont]`: nicht mehr reproduzierbare Scheduled-Slots → `Displaced` (nie gelöscht), freigewordene Fenster werden neu materialisiert |
| `GetScheduleAsync` | `channelId`, `fromUtc`, `maxCount`, `cancellationToken` | `Task<List<ProgramSlot>>` | Kommende Slots ab `fromUtc` (Scheduled + Displaced), für die Admin-Ansicht |
| `DeleteDisplacedSlotsAsync` | `userId`, `channelId`, `cancellationToken` | `Task<int>` | Löscht alle Displaced-Slots (Admin) |
| `PurgeExpiredRulesAsync` | `utcNow`, `cancellationToken` | `Task<int>` | Löscht abgelaufene `OneTime`-Regeln; vom Worker vor dem Reconcile aufgerufen |
| `CleanupSelfDisplacedSlotsAsync` | `cancellationToken` | `Task<int>` | Löscht bewiesene Selbst-Verdrängungs-Duplikate (Scheduled-Zwilling, gleiches Intervall/Inhalt); vom Worker vor dem Reconcile aufgerufen |
| `ResetScheduleAsync` | `userId`, `channelId`, `cancellationToken` | `Task<ProgramScheduleResetResult>` | Löscht alle Slots und Cursor der Kanals (Admin) |
| `GetRuleDiagnosesAsync` | `userId`, `channelId`, `cancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Pro-Regel-Diagnose für die Admin-Ansicht (Admin) |

## Zugehörige Summary-/Ergebnis-Records (gleiche Datei)

| Record | Felder | Zweck |
|--------|--------|-------|
| `ProgramScheduleRunSummary` | `SlotsCreated`, `FillerSlotsCreated`, `SkippedNoDuration`, `SkippedNoAccess`, `OrphanedRules`, `DroppedConflicts`, `RemainingGapSeconds`, `BlockedByAgeWindow` | Ergebnis von `EnsureCoverageAsync` |
| `ProgramScheduleReconcileSummary` | wie oben plus `SlotsDisplaced` (erste Position) | Ergebnis von `ReconcileAsync`; die Tests lesen `SlotsDisplaced`/`SlotsCreated` für die Verdrängungs-Assertions |
| `ProgramRuleDiagnosis` | `RuleId`, `MaterialMissing`, `CandidateCount`, `SchedulableCount`, `NoMediaItemCount`, `ProbePendingCount`, `ProbeExhaustedCount`, `NoAccessCount`, `AgeWindowBlockedCount` | Pro-Regel-Diagnosezeile |
| `ProgramScheduleResetResult` | `SlotsDeleted`, `CursorsDeleted` | Ergebnis von `ResetScheduleAsync` |

## `IProgramRuleService` (Randbereich)

Datei: `VideoWebPlayer/Services/IProgramRuleService.cs`

`ProgramScheduleService` nutzt daraus ausschließlich
`GetOrCreateChannelAsync(CancellationToken)` — liefert den (einzigen) Kanal, legt ihn
bei Bedarf an. Die übrigen Members (Regel-CRUD) sind für diese Anforderung nicht
relevant; `ProgramRuleService` feuert bei Regel-Änderungen `IProgramScheduleSignal`.

## `IProgramScheduleSignal` (Randbereich)

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

Aufwach-Signal für den `ProgramScheduleWorker` (Regel-CRUD, Medienänderungen) und
Rückmeldung `NotifyLiveScheduleChanged()` an den Live-Stream, wenn ein Lauf Slots
verändert hat. Für die Planlogik selbst ohne Bedeutung — die Tests steuern den
Service direkt.
