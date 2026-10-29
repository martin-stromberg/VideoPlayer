# Interfaces – Bestandsaufnahme

## `IProgramScheduleService`

Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

Implementiert von `ProgramScheduleService` (einzige Implementierung). Registriert als scoped
Service; aufgerufen aus `ProgramScheduleWorker` (Kette `PurgeExpiredRulesAsync` →
`ReconcileAsync` → `EnsureCoverageAsync`) und aus `TVSenderAdmin.razor`
(`GetScheduleAsync`, `DeleteDisplacedSlotsAsync`, `ResetScheduleAsync`, `GetRuleDiagnosesAsync`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `utcNow`, `cancellationToken` | `Task<ProgramScheduleRunSummary>` | Extend-only: füllt `[max(now, letztes Slot-Ende), now + Horizont]`; verändert Bestandsslots nie, verdrängt nicht |
| `ReconcileAsync` | `utcNow`, `cancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Neu-Bewertung des Plans — **Kern der Anforderung**: invalid Sendeplätze → `Displaced`, freie Fenster neu materialisiert |
| `GetScheduleAsync` | `channelId`, `fromUtc`, `maxCount`, `cancellationToken` | `Task<List<ProgramSlot>>` | Sendeplätze inkl. Status ab `fromUtc` (Admin-Anzeige) |
| `DeleteDisplacedSlotsAsync` | `userId`, `channelId`, `cancellationToken` | `Task<int>` | Löscht alle `Displaced`-Zeilen (Admin, „Verdrängte bereinigen") — entfernt `_invaderStarts`-Evidenz |
| `PurgeExpiredRulesAsync` | `utcNow`, `cancellationToken` | `Task<int>` | Löscht abgelaufene `OneTime`-Regeln; Slots bleiben als weiche Referenz und werden vom folgenden Reconcile verdrängt |
| `ResetScheduleAsync` | `userId`, `channelId`, `cancellationToken` | `Task<ProgramScheduleResetResult>` | Löscht gesamten Sendeplan + alle Cursor (Admin) |
| `GetRuleDiagnosesAsync` | `userId`, `channelId`, `cancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Planungsdiagnose je Regel (Admin) |

Zugehörige Records derselben Datei: `ProgramScheduleRunSummary`,
`ProgramScheduleReconcileSummary` (mit `SlotsDisplaced`, `SlotsCreated`,
`FillerSlotsCreated`, `SkippedNoDuration`, `SkippedNoAccess`, `OrphanedRules`,
`DroppedConflicts`, `RemainingGapSeconds`, `BlockedByAgeWindow`), `ProgramRuleDiagnosis`,
`ProgramScheduleResetResult`.

## `IProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

Singleton (`ProgramScheduleSignal`); weckt `ProgramScheduleWorker` bei planrelevanten
Änderungen. Feuert aus `ProgramRuleService`, `ProgramClipGroupService`,
`MediaDurationProbeService`/`MediaDurationProbeWorker`, `ApplicationDbContext`
(Media-Markierung) und Admin-Seiten (`TVSenderAdmin.razor`, `ProgramSettings.razor`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | – | `void` | Meldet geänderte Planeingaben; während eines Scans nur gezählt, sonst sofortiger Wake |
| `BeginScan` | – | `IDisposable` | Scan-Scope: unterdrückt Wake-ups; Dispose des letzten Scopes weckt einmalig bei unterdrückten Meldungen |
| `WaitAsync` | `cancellationToken` | `ValueTask` | Wartet auf das nächste Signal (1-Slot-Channel, koalesziert) |
