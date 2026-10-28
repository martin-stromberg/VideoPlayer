# Interfaces — Bestandsaufnahme

## `IProgramRuleService`
Datei: `VideoWebPlayer/Services/IProgramRuleService.cs`; Implementierung: `ProgramRuleService`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAllowedMaterialTypes` | `ProgramRuleType ruleType` | `IReadOnlyList<ProgramRuleMaterialType>` | Zulässige Materialarten je Regelart |
| `GetOrCreateChannelAsync` | `CancellationToken` | `Task<TVChannel>` | Singleton-Sender („TV-Sender") |
| `RenameChannelAsync` | `userId, name, ct` | `Task<TVChannel>` | Admin-geschützt |
| `GetRulesAsync` | `userId, channelId, ct` | `Task<List<ProgramRule>>` | Alle Regeln des Senders inkl. Join-Links, **aktuell `OrderBy(Name)`** — Doku (Z. 29–31) nennt nur „ordered by name"; Admin-geschützt |
| `GetGenreMaterialOptionsAsync` | `userId, mediaSourceIds?, ct` | `Task<List<DtoGenreOption>>` | Wählbare Genre-Optionen (kanonisch, Out-of-Season inkl. Fenster-Label) |
| `CreateRuleAsync` | `userId, channelId, ProgramRuleInput, ct` | `Task<ProgramRule>` | Admin-geschützt, validiert |
| `UpdateRuleAsync` | `userId, ruleId, ProgramRuleInput, ct` | `Task<ProgramRule>` | Admin-geschützt, validiert |
| `SetRuleActiveAsync` | `userId, ruleId, isActive, ct` | `Task<ProgramRule>` | Admin-geschützt |
| `DeleteRuleAsync` | `userId, ruleId, ct` | `Task` | Admin-geschützt; `userId`-gebunden — eine automatische Bereinigung (Worker-Kontext) könnte diese Signatur nicht direkt nutzen |

**Keine** Methode zum Bereinigen abgelaufener Regeln vorhanden — für die geforderte automatische Löschung wäre eine neue Methode (hier oder am `IProgramScheduleService`) nötig; die Admin-Prüfung (`userId`) passt nicht zum Worker-Kontext.

## `IProgramScheduleService`
Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`; Implementierung: `ProgramScheduleService`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `utcNow, ct` | `Task<ProgramScheduleRunSummary>` | Extend-only-Ausbau |
| `ReconcileAsync` | `utcNow, ct` | `Task<ProgramScheduleReconcileSummary>` | Neu-Bewertung; verdrängt Slots ungültiger/gelöschter Regeln |
| `GetScheduleAsync` | `channelId, fromUtc, maxCount, ct` | `Task<List<ProgramSlot>>` | Kommende Slots |
| `DeleteDisplacedSlotsAsync` | `userId, channelId, ct` | `Task<int>` | Housekeeping (Admin); Logging-Muster für Bereinigungen |
| `ResetScheduleAsync` | `userId, channelId, ct` | `Task<ProgramScheduleResetResult>` | Kompletter Plan-Reset (Admin) |
| `GetRuleDiagnosesAsync` | `userId, channelId, ct` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Pro-Regel-Diagnose (Admin) |

Zugehörige Records (selbe Datei): `ProgramScheduleRunSummary`, `ProgramScheduleReconcileSummary`, `ProgramRuleDiagnosis`, `ProgramScheduleResetResult` — kein Bereinigungs-/Purge-Ergebnistyp vorhanden.

## `IProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`; Implementierung: `ProgramScheduleSignal` (Singleton)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | — | `void` | Aufwecken des Workers bei Plan-Eingabeänderung; von `ProgramRuleService` nach jeder Mutation gefeuert — auch nach einer automatischen Löschung fachlich nötig, damit `ReconcileAsync` die Slots der entfernten Regel verdrängt |
| `BeginScan` | — | `IDisposable` | Scan-Scope (unterdrückt Wake-ups) |
| `WaitAsync` | `ct` | `ValueTask` | Coalesced Warten |

## `IGenreService` (relevante Methode)
Datei: `VideoWebPlayer/Services/IGenreService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSeasonalGenresAsync` | `ct` | `Task<List<Genre>>` | Saisonale Genres über `SeasonalGenreWindow.GetExpiry` — Referenz der Jahresfenster-Semantik |
