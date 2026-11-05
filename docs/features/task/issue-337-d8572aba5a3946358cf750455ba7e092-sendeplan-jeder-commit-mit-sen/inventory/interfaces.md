# Interfaces — Bestandsaufnahme Issue #337

## `IProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` (Interface und Implementierung `ProgramScheduleSignal` in derselben Datei; DI-Registrierung als Singleton in `ServiceCollectionExtensions.cs` Z. 269–270)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | — | `void` | Meldet sendeplan-relevante Eingabeänderung. Während eines `BeginScan`-Scopes unterdrückt (Scope-Ende weckt einmal); außerhalb sofortiges Wecken von `_channel` **und** `_liveChannel`. |
| `BeginScan` | — | `IDisposable` | Scan-Scope; Dispose des letzten Scopes weckt beide Kanäle einmal, aber nur wenn mindestens eine Notification unterdrückt wurde. |
| `NotifyLiveScheduleChanged` | — | `void` | Meldet materialisierte Planänderung — nur `_liveChannel` (Issue #315); kein Eingabe-Commit-Signal. |
| `WaitAsync` | `CancellationToken` | `ValueTask` | Warten auf Plan-Neubewertung; Konsument `ProgramScheduleWorker`. Mehrere Signale ohne Wartenden koaleszieren (1-Slot-Channel). |
| `WaitLiveAsync` | `CancellationToken` | `ValueTask` | Warten auf Stream-Neusynchronisation; Konsument `LiveStreamWorker`. Wird von `NotifyScheduleChanged` **und** `NotifyLiveScheduleChanged` gespeist. |

Implementierungen im Bestand: `ProgramScheduleSignal` (Produktiv). Test-Doubles: `GatedScheduleSignal` (privat in `MediaDurationProbeWorkerTests`), `FakeScheduleSignal` (privat in `MediaDurationProbeServiceTests`) — beide implementieren alle fünf Member und müssten bei einer Interface-Erweiterung nachgezogen werden.

## `IPlaylistBackfillSignal` (Referenzmuster, ausdrücklich nicht betroffen)
Datei: `VideoWebPlayer/Services/PlaylistBackfillSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyMarkersWritten` | — | `void` | Marker-Commit melden; während Scan-Scope unterdrückt. |
| `BeginScan` | — | `IDisposable` | Scan-Scope; Ende des letzten Scopes weckt **unbedingt** (abweichend vom Schedule-Signal). |
| `WaitAsync` | `CancellationToken` | `ValueTask` | Warten des `PlaylistBackfillWorker`. |

## `IProgramScheduleService`
Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ExecutePassAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramSchedulePassSummary>` | Ein kompletter Pflegedurchlauf (Purge → Cleanup → Reconcile → Coverage). Der Lauf, der je Signal-Weckruf im `ProgramScheduleWorker` ausgelöst wird und dessen Bündelung die Anforderung adressiert. |
| `EnsureCoverageAsync` | `DateTimeOffset`, `CancellationToken` | `Task<ProgramScheduleRunSummary>` | Einzelschritt: Abdeckung bis zum Horizont ausbauen. |
| `ReconcileAsync` | `DateTimeOffset`, `CancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Einzelschritt: Bestandsplan neu bewerten/reparieren (Displaced-Markierung). |
| `PurgeExpiredRulesAsync` | `DateTimeOffset`, `CancellationToken` | `Task<int>` | Einzelschritt: abgelaufene Einmal-Regeln löschen. |
| `CleanupSelfDisplacedSlotsAsync` | `CancellationToken` | `Task<int>` | Einzelschritt: bewiesene Selbst-Verdrängungs-Duplikate löschen. |
| `GetScheduleAsync` | `channelId`, `fromUtc`, `maxCount`, `CancellationToken` | `Task<List<ProgramSlot>>` | Planansicht für die Admin-UI. |
| `DeleteDisplacedSlotsAsync` | `userId`, `channelId`, `CancellationToken` | `Task<int>` | Verdrängte Slots bereinigen (Admin). |
| `ResetScheduleAsync` | `userId`, `channelId`, `CancellationToken` | `Task<ProgramScheduleResetResult>` | Sendeplan-Reset (Admin); `TVSenderAdmin.razor` feuert danach `NotifyScheduleChanged()`. |
| `GetRuleDiagnosesAsync` | `userId`, `channelId`, `CancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Regel-Diagnosen für die Admin-UI. |

`ProgramSchedulePassSummary` (record struct, selbe Datei): `ExpiredRulesPurged`, `SelfDisplacedCleaned`, `Reconcile`, `Coverage` — der Worker liest `Reconcile.SlotsDisplaced`, `Reconcile.SlotsCreated`, `Coverage.SlotsCreated`.

## `IProgramRuleService`
Datei: `VideoWebPlayer/Services/IProgramRuleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetOrCreateChannelAsync` | `CancellationToken` | `Task<TVChannel>` | Kanal lesen/anlegen (kein Signal). |
| `RenameChannelAsync` | `userId`, `name`, `CancellationToken` | `Task<TVChannel>` | Kanalname ändern (kein Signal — die Komponente feuert selbst). |
| `GetRulesAsync` | `userId`, `channelId`, `CancellationToken` | `Task<List<ProgramRule>>` | Regeln lesen. |
| `GetGenreMaterialOptionsAsync` | `userId`, `mediaSourceIds`, `CancellationToken` | `Task<List<DtoGenreOption>>` | Genre-Materialoptionen. |
| `CreateRuleAsync` | `userId`, `channelId`, `ProgramRuleInput`, `CancellationToken` | `Task<ProgramRule>` | Regel anlegen → `NotifyScheduleChanged()`. |
| `UpdateRuleAsync` | `userId`, `ruleId`, `ProgramRuleInput`, `CancellationToken` | `Task<ProgramRule>` | Regel ändern → `NotifyScheduleChanged()`. |
| `SetRuleActiveAsync` | `userId`, `ruleId`, `isActive`, `CancellationToken` | `Task<ProgramRule>` | Aktiv schalten → `NotifyScheduleChanged()`. |
| `DeleteRuleAsync` | `userId`, `ruleId`, `CancellationToken` | `Task` | Löschen → `NotifyScheduleChanged()`. |

## `IProgramClipGroupService`
Datei: `VideoWebPlayer/Services/IProgramClipGroupService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetGroupsAsync` / `GetGroupCountsAsync` / `GetGroupClipsAsync` / `GetClipCandidatesAsync` | `userId`, ggf. `groupId` u. a., `CancellationToken` | `Task<...>` | Lesen (kein Signal). |
| `CreateGroupAsync` | `userId`, `name`, `CancellationToken` | `Task<ProgramClipGroup>` | Anlegen → `NotifyScheduleChanged()`. |
| `RenameGroupAsync` | `userId`, `groupId`, `name`, `CancellationToken` | `Task<ProgramClipGroup>` | Umbenennen → `NotifyScheduleChanged()`. |
| `DeleteGroupAsync` | `userId`, `groupId`, `CancellationToken` | `Task` | Löschen → `NotifyScheduleChanged()`. |
| `AddClipsToGroupAsync` | `userId`, `groupId`, `mediaItemIds`, `CancellationToken` | `Task` | Clips zufügen → `NotifyScheduleChanged()`. |
| `RemoveClipFromGroupAsync` | `userId`, `groupId`, `mediaItemId`, `CancellationToken` | `Task` | Clip entfernen → `NotifyScheduleChanged()`. |

## `IBackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsPausedForRestore` | — | `bool` | Restore-Sperre aktiv. |
| `ActiveOperationCount` | — | `int` | Anzahl laufender Operationen. |
| `EnterOperationAsync` | `name`, `CancellationToken` | `Task<IAsyncDisposable>` | Lease je Worker-Lauf/Scan — schützt vor Restore-Überlagerung. |
| `PauseForRestoreAsync` | `CancellationToken` | `Task<IAsyncDisposable>` | Neue Operationen pausieren, laufende abwarten. |

Unberührt; relevant nur als Rahmen jeder `ExecutePassAsync`-Ausführung.
