# Interfaces — Bestandsaufnahme

## `IProgramRuleService`
Datei: `VideoWebPlayer/Services/IProgramRuleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAllowedMaterialTypes` | `ProgramRuleType` | `IReadOnlyList<ProgramRuleMaterialType>` | Zulässige Materialarten je Regelart |
| `GetOrCreateChannelAsync` | `CancellationToken` | `Task<TVChannel>` | Singleton-Sender |
| `RenameChannelAsync` | `userId, name, ct` | `Task<TVChannel>` | Admin-geschützt |
| `GetRulesAsync` | `userId, channelId, ct` | `Task<List<ProgramRule>>` | Admin-geschützt |
| `CreateRuleAsync` | `userId, channelId, ProgramRuleInput, ct` | `Task<ProgramRule>` | Admin-geschützt, validiert |
| `UpdateRuleAsync` | `userId, ruleId, ProgramRuleInput, ct` | `Task<ProgramRule>` | Admin-geschützt, validiert |
| `SetRuleActiveAsync` | `userId, ruleId, isActive, ct` | `Task<ProgramRule>` | Admin-geschützt |
| `DeleteRuleAsync` | `userId, ruleId, ct` | `Task` | Admin-geschützt |

Kein Endpoint/Interface für eine sender-spezifische Genre-Optionsquelle vorhanden — `TVSenderAdmin` nutzt das gemeinsame `api/items/genres`.

## `IProgramScheduleService`
`EnsureCoverageAsync`, `ReconcileAsync`, `GetScheduleAsync` — Schnittstelle unverändert nutzbar; die Erweiterung betrifft nur die interne Expansion.

## `IGenreService`
`MarkGenresAsChangedAsync`, `GetSeasonalGenresAsync` — saisonale Fensterlogik wiederverwendbar.
