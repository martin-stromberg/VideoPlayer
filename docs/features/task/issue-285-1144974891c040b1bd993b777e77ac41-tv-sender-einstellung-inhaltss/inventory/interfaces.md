# Interfaces — Bestandsaufnahme

## `IProgramScheduleService`

Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleRunSummary>` | Extend-only-Ausbau des Sendeplans bis zum Horizont |
| `ReconcileAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Neubewertung: nicht mehr reproduzierbare Slots → `Displaced`, Neuplanung |
| `GetScheduleAsync` | `long channelId`, `DateTimeOffset fromUtc`, `int maxCount`, `CancellationToken` | `Task<List<ProgramSlot>>` | Sendeplan-Anzeige (Admin) |
| `DeleteDisplacedSlotsAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<int>` | Admin-only: verdrängte Slots löschen |
| `ResetScheduleAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<ProgramScheduleResetResult>` | Admin-only: Plan + Cursors zurücksetzen |
| `GetRuleDiagnosesAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Admin-only: Regel-Diagnose (inkl. `NoAccessCount`) |

Beide Summary-Records enthalten `SkippedNoAccess`; `ProgramRuleDiagnosis` enthält
`NoAccessCount` (alle in derselben Datei definiert).

## `IProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | – | `void` | Weckt den `ProgramScheduleWorker` sofort (außer während `BeginScan`-Scope) — Konvention nach planrelevanten Änderungen |
| `BeginScan` | – | `IDisposable` | Scan-Scope; Ende des letzten Scopes weckt einmal |
| `WaitAsync` | `CancellationToken` | `ValueTask` | Wartepunkt des Workers; Signale werden koalesziert |

## `IProgramRuleService`

Datei: `VideoWebPlayer/Services/IProgramRuleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetOrCreateChannelAsync` | `CancellationToken` | `Task<TVChannel>` | Liefert den (einen) Sender, legt ihn an |
| `RenameChannelAsync` | `string userId`, `string name`, `CancellationToken` | `Task<TVChannel>` | Admin-only: Sender umbenennen (von `SaveChannelSettingsAsync` genutzt) |
| `GetRulesAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<List<ProgramRule>>` | Admin-only: Regeln laden |
| `GetGenreMaterialOptionsAsync` | `string userId`, `IReadOnlyCollection<long>? mediaSourceIds`, `CancellationToken` | `Task<List<DtoGenreOption>>` | Admin-only: Genre-Optionen des Regel-Editors |
| `CreateRuleAsync` / `UpdateRuleAsync` | `string userId`, `…`, `ProgramRuleInput`, `CancellationToken` | `Task<ProgramRule>` | Admin-only: Regel-CRUD |
| `SetRuleActiveAsync` / `DeleteRuleAsync` | `string userId`, `long ruleId`, `…`, `CancellationToken` | `Task<ProgramRule>` / `Task` | Admin-only: Aktivierung/Löschung |

## `IProgramClipGroupService`

Datei: `VideoWebPlayer/Services/` (Implementierung `ProgramClipGroupService.cs`)

Clip-Gruppen-CRUD und Kandidatensuche (`GetClipCandidatesAsync` — wirft
`ForbiddenAccessException` für Nicht-Admins, siehe `TVSenderAdminTests_Standby`).
Wird von der Standby-Auswahl der Einstellungs-Seite mitbenutzt; für die neue Einstellung
nicht zwingend betroffen.

## `ProgramSettingsService`

Kein Interface — konkrete `sealed class`, wird direkt injiziert (`TVSenderAdmin.razor`
`@inject ProgramSettingsService ProgramSettings`, `ProgramScheduleService` hält ihn als
`_settings`-Feld). Neue Getter/Updater folgen dem Muster
`GetHolidayFederalStateAsync`/`UpdateHolidayFederalStateAsync`.
