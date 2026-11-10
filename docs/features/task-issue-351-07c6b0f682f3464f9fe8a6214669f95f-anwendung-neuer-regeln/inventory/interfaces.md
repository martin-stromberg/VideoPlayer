# Interfaces

## `IProgramScheduleService`

Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ExecutePassAsync` | `utcNow`, `ct` | `Task<ProgramSchedulePassSummary>` | einziger Produktions-Einstieg (Worker): Purge → Cleanup → Reconcile → Coverage |
| `EnsureCoverageAsync` | `utcNow`, `ct` | `Task<ProgramScheduleRunSummary>` | Extend-only-Ausbau bis Horizont; bestehende Slots nie angefasst |
| `ReconcileAsync` | `utcNow`, `ct` | `Task<ProgramScheduleReconcileSummary>` | Neu-Bewertung des Fensters; hier sitzt das neue Verhalten — **voraussichtlich signaturstabil** |
| `GetScheduleAsync` | `channelId`, `fromUtc`, `maxCount`, `ct` | `Task<List<ProgramSlot>>` | Admin-Ansicht (Scheduled+Displaced) |
| `DeleteDisplacedSlotsAsync` | `userId`, `channelId`, `ct` | `Task<int>` | Vollsweep der Displaced-Historie (Admin) |
| `PurgeExpiredRulesAsync` | `utcNow`, `ct` | `Task<int>` | abgelaufene OneTime-Regeln löschen (Worker) |
| `CleanupSelfDisplacedSlotsAsync` | `ct` | `Task<int>` | bewiesene Selbst-Verdrängungs-Zwillinge löschen (Worker) |
| `ResetScheduleAsync` | `userId`, `channelId`, `ct` | `Task<ProgramScheduleResetResult>` | kompletter Plan-Reset (Admin-Fallback) |
| `GetRuleDiagnosesAsync` | `userId`, `channelId`, `ct` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Regel-Diagnose (Admin) |

Summary-Records (selbe Datei): `ProgramScheduleRunSummary`, `ProgramScheduleReconcileSummary`
(`SlotsDisplaced`, `SlotsCreated`, `FillerSlotsCreated`, `SkippedNoDuration`, `SkippedNoAccess`,
`OrphanedRules`, `DroppedConflicts`, `RemainingGapSeconds`, `BlockedByAgeWindow`),
`ProgramRuleDiagnosis`, `ProgramScheduleResetResult`, `ProgramSchedulePassSummary`.
Kein Zähler für „verschobene" Ausstrahlungen vorhanden — Erweiterung ggf. nötig (in der
Anforderung als „ggf." geführt).

## `IProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | — | `void` | Eingabeänderung melden (Regel-CRUD, Medienänderung); Debounce-Fenster |
| `NotifyScheduleChangedNow` | — | `void` | sofortiges Aufwecken („Jetzt generieren") |
| `BeginScan` | — | `IDisposable` | Scan-Scope: unterdrückt Wake-ups, weckt einmal am Ende |
| `NotifyLiveScheduleChanged` | — | `void` | materialisierte Änderung → Live-Stream-Worker (eigener Kanal) |
| `WaitAsync` | `ct` | `ValueTask` | Wartepunkt des Schedule-Workers |
| `WaitLiveAsync` | `ct` | `ValueTask` | Wartepunkt des Live-Stream-Workers |

## `IProgramRuleService` (relevante Methoden)

Datei: `VideoWebPlayer/Services/IProgramRuleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetOrCreateChannelAsync` | `ct` | `Task<TVChannel>` | Kanal des Senders (wird von `ProgramScheduleService` für jeden Lauf geholt) |
| `GetRulesAsync` | `userId`, `channelId`, `ct` | `Task<List<ProgramRule>>` | Regel-Liste (Admin-UI) |
| `CreateRuleAsync` | `userId`, `channelId`, `ProgramRuleInput`, `ct` | `Task<ProgramRule>` | **Auslöser:** neue Regel + `NotifyScheduleChanged()` |
| `UpdateRuleAsync` | `userId`, `ruleId`, `ProgramRuleInput`, `ct` | `Task<ProgramRule>` | **Auslöser:** Regeländerung + `NotifyScheduleChanged()` |
| `SetRuleActiveAsync` | `userId`, `ruleId`, `isActive`, `ct` | `Task<ProgramRule>` | **Auslöser:** (Re-)Aktivierung + `NotifyScheduleChanged()` |
| `DeleteRuleAsync` | `userId`, `ruleId`, `ct` | `Task` | Regel löschen + `NotifyScheduleChanged()` |
| `RenameChannelAsync` / `GetGenreMaterialOptionsAsync` | — | — | nicht berührt |
