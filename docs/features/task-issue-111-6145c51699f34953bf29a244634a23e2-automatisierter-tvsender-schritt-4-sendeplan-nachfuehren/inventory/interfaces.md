# Interfaces — Bestandsaufnahme

## `IProgramScheduleService`
Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleRunSummary>` | Rollierende Fortschreibung des Sendeplans (extend-only; verändert bestehende Sendeplätze nie) |

`ProgramScheduleRunSummary` (readonly record struct): `SlotsCreated`, `FillerSlotsCreated`, `SkippedNoDuration`, `SkippedNoAccess`, `OrphanedRules`, `DroppedConflicts`, `RemainingGapSeconds`.

## `IProgramRuleService`
Datei: `VideoWebPlayer/Services/IProgramRuleService.cs`

| Methode | Zweck |
|---------|-------|
| `GetAllowedMaterialTypes(ProgramRuleType)` | zulässige Materialarten je Regelart |
| `GetOrCreateChannelAsync` | Kanal liefern/anlegen |
| `RenameChannelAsync` | Kanal umbenennen (Admin) |
| `GetRulesAsync` | Regeln eines Kanals lesen (Admin) |
| `CreateRuleAsync` | Regel anlegen (Admin, validiert) |
| `UpdateRuleAsync` | Regel aktualisieren (Admin, validiert) |
| `SetRuleActiveAsync` | `IsActive` setzen (Admin) |
| `DeleteRuleAsync` | Regel löschen (Admin) |

## `IPlaylistBackfillSignal` (Referenzmuster)
Datei: `VideoWebPlayer/Services/PlaylistBackfillSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyMarkersWritten` | – | `void` | Weckt den Worker (unterdrückt während Scan) |
| `BeginScan` | – | `IDisposable` | Scan-Scope; Ende des letzten weckt einmal |
| `WaitAsync` | `CancellationToken` | `ValueTask` | Warten bis Signal ansteht (koalesziert) |

## `IBackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnterOperationAsync` | `string name`, `CancellationToken` | `Task<IAsyncDisposable>` | Lease für Hintergrund-Schreiboperation |
| `PauseForRestoreAsync` | `CancellationToken` | `Task<IAsyncDisposable>` | Restore-Exklusivität |
| `IsPausedForRestore` / `ActiveOperationCount` | – | `bool` / `int` | Zustand |

## `IUnlockedMediaService`
Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs` — Einzelfreischaltungen je Benutzer (`IsUnlockedAsync`, `GetUnlockedUserIdsAsync`, `SetUnlockedUsersAsync`, Sammlungs-/Serien-Freischaltlisten). Wird in `ProgramScheduleService` nicht direkt genutzt (Zugriff über `UnlockedMediaEntries` im `AccessContext`).

## Fehlend (neu anzulegen)
- Ein Sendeplan-Signal (`IProgramScheduleSignal` o. ä.): Regel-CRUD und Medienbestands-Änderungen wecken den `ProgramScheduleWorker` für eine sofortige Re-Bewertung; Muster `IPlaylistBackfillSignal`.
