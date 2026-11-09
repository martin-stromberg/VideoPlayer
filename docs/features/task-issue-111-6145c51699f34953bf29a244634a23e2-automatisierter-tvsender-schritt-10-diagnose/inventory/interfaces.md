# Interfaces — Bestandsaufnahme

## `IProgramScheduleService`
Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `DateTimeOffset utcNow, CancellationToken` | `Task<ProgramScheduleRunSummary>` | Extend-only-Planfortschreibung. |
| `ReconcileAsync` | `DateTimeOffset utcNow, CancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Neu-Bewertung bestehender Slots. |
| `GetScheduleAsync` | `long channelId, DateTimeOffset fromUtc, int maxCount, CancellationToken` | `Task<List<ProgramSlot>>` | Admin-Sendeplanansicht. |

Bestehende Summary-Records (`ProgramScheduleRunSummary`, `ProgramScheduleReconcileSummary`)
enthalten bereits `SkippedNoDuration`, `SkippedNoAccess`, `OrphanedRules` — läufübergreifend,
nicht pro Regel, ohne Probezustand-Differenzierung. Ein neuer Record `ProgramRuleDiagnosis` +
Methode `GetRuleDiagnosesAsync` fehlt.

## `IProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | – | `void` | Weckt den Worker (koaleszierend; in Scan-Scopes zurückgehalten). |
| `BeginScan` | – | `IDisposable` | Scan-Scope; Ende weckt einmalig. |
| `WaitAsync` | `CancellationToken` | `ValueTask` | Warteseite des Workers. |

## `IProgramRuleService`
Datei: `VideoWebPlayer/Services/IProgramRuleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRulesAsync` | `string userId, long channelId, CancellationToken` | `Task<List<ProgramRule>>` | Regeln inkl. Genre-/Quell-Links; admin-only (`RequireAdminAsync`). |
| `GetGenreMaterialOptionsAsync` | `string userId, IReadOnlyCollection<long>? mediaSourceIds, CancellationToken` | `Task<List<DtoGenreOption>>` | Kanonische Genre-Optionen; admin-only. |
| CRUD/Activate | `string userId, …` | `Task<…>` | admin-only, feuern `NotifyScheduleChanged`. |
