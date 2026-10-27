# Interface-Bestandsaufnahme

## `IProgramScheduleService`

Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `utcNow: DateTimeOffset`, `cancellationToken` | `Task<ProgramScheduleRunSummary>` | Extend-only-Ausbau des Sendeplans bis zum Horizont; implementiert von `ProgramScheduleService`. |
| `ReconcileAsync` | `utcNow: DateTimeOffset`, `cancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Re-Bewertung des Plans gegen aktuelle Regeln/Mediathek (Verdrängen + Neu-Materialisieren). |
| `GetScheduleAsync` | `channelId: long`, `fromUtc: DateTimeOffset`, `maxCount: int`, `cancellationToken` | `Task<List<ProgramSlot>>` | Kommende Slots inkl. Status (Admin-Ansicht). |
| `DeleteDisplacedSlotsAsync` | `userId: string`, `channelId: long`, `cancellationToken` | `Task<int>` | Housekeeping: `Displaced`-Slots löschen (Admin). |
| `ResetScheduleAsync` | `userId: string`, `channelId: long`, `cancellationToken` | `Task<ProgramScheduleResetResult>` | Kompletter Plan-Reset inkl. Cursor (Admin). |
| `GetRuleDiagnosesAsync` | `userId: string`, `channelId: long`, `cancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Pro-Regel-Plandiagnose (Admin); nutzt dieselbe `ExpandMaterialAsync`-Logik. |

Zugehörige Records (gleiche Datei): `ProgramScheduleRunSummary`, `ProgramScheduleReconcileSummary`,
`ProgramRuleDiagnosis`, `ProgramScheduleResetResult`.

Aufrufer von `EnsureCoverageAsync`/`ReconcileAsync`: `ProgramScheduleWorker`
(`VideoWebPlayer/Services/ProgramScheduleWorker.cs`, Z. 71-72) sowie direkt in Tests.
