# Interfaces

## `IProgramScheduleService`

Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ExecutePassAsync` | `utcNow`, `cancellationToken` | `Task<ProgramSchedulePassSummary>` | Einziger produktiver Einstieg: Purge → Cleanup → Reconcile → Coverage. |
| `EnsureCoverageAsync` | `utcNow`, `cancellationToken` | `Task<ProgramScheduleRunSummary>` | Extend-only Befüllung bis zum Horizont — Pfad 1 der Strang-Belegung. |
| `ReconcileAsync` | `utcNow`, `cancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Titelweise Re-Bewertung + Neuplanung — Pfad 2 der Strang-Belegung. |
| `GetScheduleAsync` | `channelId`, `fromUtc`, `maxCount`, `cancellationToken` | `Task<List<ProgramSlot>>` | Kommende Slots für die Admin-Anzeige. |
| `DeleteDisplacedSlotsAsync` | `userId`, `channelId`, `cancellationToken` | `Task<int>` | Admin: displaced Zeilen löschen. |
| `PurgeExpiredRulesAsync` | `utcNow`, `cancellationToken` | `Task<int>` | Abgelaufene OneTime-Regeln löschen (Schritt 1 des Passes). |
| `CleanupSelfDisplacedSlotsAsync` | `cancellationToken` | `Task<int>` | Beweisbare Selbst-Verdrängungs-Duplikate löschen (Schritt 2). |
| `ResetScheduleAsync` | `userId`, `channelId`, `cancellationToken` | `Task<ProgramScheduleResetResult>` | Admin: kompletter Plan-Reset (Slots + Cursor). |
| `GetRuleDiagnosesAsync` | `userId`, `channelId`, `cancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Admin: Regeldiagnose — nutzt nur die Material-Expansion, **keine** Strang-Auswahl. |

Summaries/Records derselben Datei: `ProgramScheduleRunSummary`, `ProgramScheduleReconcileSummary`, `ProgramRuleDiagnosis`, `ProgramScheduleResetResult`, `ProgramSchedulePassSummary`.

## `IProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChangedNow` | – | `void` | Weckt den Worker sofort („Jetzt generieren", Regel-/Medienänderungen). |
| `NotifyLiveScheduleChanged` | – | `void` | Weckt den Live-Stream bei materialisierten Planänderungen. |
| `WaitAsync` | `cancellationToken` | `ValueTask` | Warte-Ende des Weck-Kanals im Worker. |
