# Interfaces — Bestandsaufnahme

Alle Contracts bleiben unverändert; die Übersicht nutzt bereits heute die aufgeführten
Methoden. Alle Mutations- und Diagnose-Methoden prüfen die Admin-Berechtigung
serverseitig (`userId`).

## `IProgramRuleService`
Datei: `VideoWebPlayer/Services/IProgramRuleService.cs` — injiziert in `TVSenderAdmin.razor` (Z. 11)

| Methode | Parameter | Rückgabewert | Zweck / Aufrufer in der Seite |
|---------|-----------|--------------|-------------------------------|
| `GetAllowedMaterialTypes` | `ProgramRuleType ruleType` | `IReadOnlyList<ProgramRuleMaterialType>` | Materialarten je Regelart — Editor (`AllowedMaterialTypes`, Z. 1123) |
| `GetOrCreateChannelAsync` | `CancellationToken` | `Task<TVChannel>` | Liefert/legt den einen Sender an (`OnInitializedAsync` Z. 1145) |
| `RenameChannelAsync` | `userId`, `name`, `CancellationToken` | `Task<TVChannel>` | Sender umbenennen (`SaveChannelSettingsAsync`) |
| `GetRulesAsync` | `userId`, `channelId`, `CancellationToken` | `Task<List<ProgramRule>>` | Alle Regeln inkl. `Genres`/`MediaSourceFilters`/`ClipGroups` eager, nach Name sortiert (`ReloadRulesAsync` Z. 1394) — Datenquelle der Übersicht |
| `GetGenreMaterialOptionsAsync` | `userId`, `mediaSourceIds?`, `CancellationToken` | `Task<List<DtoGenreOption>>` | Genre-Optionen der Editor-Checkboxliste (`ReloadGenreOptionsAsync`) |
| `CreateRuleAsync` | `userId`, `channelId`, `ProgramRuleInput`, `CancellationToken` | `Task<ProgramRule>` | Regel anlegen (`SaveRuleAsync`) |
| `UpdateRuleAsync` | `userId`, `ruleId`, `ProgramRuleInput`, `CancellationToken` | `Task<ProgramRule>` | Regel speichern (`SaveRuleAsync`) |
| `SetRuleActiveAsync` | `userId`, `ruleId`, `isActive`, `CancellationToken` | `Task<ProgramRule>` | Aktivieren/Deaktivieren (`ToggleActiveAsync` Z. 2103) |
| `DeleteRuleAsync` | `userId`, `ruleId`, `CancellationToken` | `Task` | Regel löschen (`DeleteRuleAsync` Z. 2138) |

## `IProgramScheduleService`
Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs` — injiziert in `TVSenderAdmin.razor` (Z. 12)

| Methode | Parameter | Rückgabewert | Zweck / Aufrufer in der Seite |
|---------|-----------|--------------|-------------------------------|
| `GetRuleDiagnosesAsync` | `userId`, `channelId`, `CancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Pro-Regel-Planungsdiagnose, admin-gegat (`ReloadDiagnosesAsync` Z. 1401) — Inhalt der Spalte „Planbar" |

Weitere Methoden des Interfaces (betreffen den Sendeplan-Tab, nicht die Regel-Übersicht):
`EnsureCoverageAsync`, `ReconcileAsync`, `GetScheduleAsync`, `DeleteDisplacedSlotsAsync`,
`ResetScheduleAsync`.

## `IProgramClipGroupService`
Datei: `VideoWebPlayer/Services/IProgramClipGroupService.cs` — injiziert in `TVSenderAdmin.razor` (Z. 14)

Von der Seite für den Regel-Editor genutzt (Clip-Gruppen-Checkboxliste und Standby-Picker):

| Methode | Parameter | Rückgabewert | Zweck / Aufrufer in der Seite |
|---------|-----------|--------------|-------------------------------|
| `GetGroupsAsync` | `userId`, `CancellationToken` | `Task<List<ProgramClipGroup>>` | Gruppenliste (`ReloadClipGroupsAsync` Z. 2121) |
| `GetGroupCountsAsync` | `userId`, `CancellationToken` | `Task<IReadOnlyDictionary<long, int>>` | Clip-Anzahl je Gruppe (`ReloadClipGroupsAsync`) |
| `GetClipCandidatesAsync` | `userId`, `search?`, `mediaSourceId?`, `skip`, `take`, `CancellationToken` | `Task<List<ProgramClipGroupCandidate>>` | Kandidaten des Standby-Pickers (Z. 1812) |

(Weitere Methoden für die Clip-Gruppen-Verwaltung — `CreateGroupAsync`, `RenameGroupAsync`,
`DeleteGroupAsync`, `GetGroupClipsAsync`, `AddClipsToGroupAsync`, `RemoveClipFromGroupAsync` —
werden von `TVSenderAdminClipGroups.razor` genutzt.)

## `IProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` (ab Z. 10) — injiziert in `TVSenderAdmin.razor` (Z. 13)

| Methode | Parameter | Rückgabewert | Zweck / Aufrufer in der Seite |
|---------|-----------|--------------|-------------------------------|
| `NotifyScheduleChanged` | — | `void` | Weckt den Sendeplan-Worker; aufgerufen in `GenerateNowAsync`, `ResetScheduleAsync`, `SaveChannelSettingsAsync` |
| `BeginScan` | — | `IDisposable` | Scan-Scope, unterdrückt Worker-Weckrufe (nicht von der Seite genutzt) |

Die Komponente publiziert nur — sie abonniert keine Events.
