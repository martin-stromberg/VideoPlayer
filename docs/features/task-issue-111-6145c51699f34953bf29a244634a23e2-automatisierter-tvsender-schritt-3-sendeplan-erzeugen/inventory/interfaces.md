# Interfaces – Bestandsaufnahme

## `IProgramRuleService`
Datei: `VideoWebPlayer/Services/IProgramRuleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAllowedMaterialTypes` | `ProgramRuleType` | `IReadOnlyList<ProgramRuleMaterialType>` | Zulässige Materialarten je Regelart |
| `GetOrCreateChannelAsync` | – | `Task<TVChannel>` | Einzelsender lesen/anlegen |
| `RenameChannelAsync` | `userId`, `name` | `Task<TVChannel>` | Sender umbenennen (Admin) |
| `GetRulesAsync` | `userId`, `channelId` | `Task<List<ProgramRule>>` | Regeln lesen (Admin) |
| `CreateRuleAsync` / `UpdateRuleAsync` / `SetRuleActiveAsync` / `DeleteRuleAsync` | `userId`, Regeldaten | `Task<ProgramRule>`/`Task` | Regel-Mutationen (Admin) |

## `IUnlockedMediaService`
Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetMediaSourceIdsForUserAsync` | `userId` | `Task<long[]>` | Regulär freigegebene Quellen des Benutzers |
| `GetUnlockedMovieCollectionIdsForUserAsync` / `GetUnlockedTVShowIdsForUserAsync` | `userId` | `Task<long[]>` | Einzelfreischaltungen des Benutzers |
| `IsAccessible` | `hasSourceAccess`, `isUnlocked` | `bool` | Kanonische Zugriffsregel (Quelle ODER Freischaltung) |

## `IBackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnterOperationAsync` | `name`, `cancellationToken` | `Task<IAsyncDisposable>` | Hintergrund-Schreibzugriff (blockiert während Restore) |
| `PauseForRestoreAsync` | `cancellationToken` | `Task<IAsyncDisposable>` | Exklusiver Restore-Zugriff |

## `IPlaylistBackfillSignal`
Datei: `VideoWebPlayer/Services/PlaylistBackfillSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyMarkersWritten` | – | `void` | Wecksignal für den Worker |
| `WaitAsync` | `cancellationToken` | `ValueTask` | Schlafen bis Signal |
| `BeginScan` | – | `IDisposable` | Scan-Scope (Signale koaleszieren) |
