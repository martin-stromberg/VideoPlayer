# Interfaces — Bestandsaufnahme

**Kein Interface für Clip-Gruppen vorhanden** (kein `IProgramClipGroupService` o. ä.).

## `IProgramRuleService`
Datei: `VideoWebPlayer/Services/IProgramRuleService.cs` — Admin-CRUD für Sender und Programmregeln; jede Mutation erzwingt `ApplicationUser.IsAdmin` serverseitig.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAllowedMaterialTypes` | `ProgramRuleType ruleType` | `IReadOnlyList<ProgramRuleMaterialType>` | Zulässige Materialarten je Regelart |
| `GetOrCreateChannelAsync` | `CancellationToken` | `Task<TVChannel>` | Einziger Sender, wird bei Bedarf angelegt |
| `RenameChannelAsync` | `string userId, string name, CancellationToken` | `Task<TVChannel>` | Sender umbenennen |
| `GetRulesAsync` | `string userId, long channelId, CancellationToken` | `Task<List<ProgramRule>>` | Regeln inkl. Genre-/Quell-Links |
| `GetGenreMaterialOptionsAsync` | `string userId, IReadOnlyCollection<long>? mediaSourceIds, CancellationToken` | `Task<List<DtoGenreOption>>` | Wählbare Genres (kanonisch, dedupliziert) |
| `CreateRuleAsync` | `string userId, long channelId, ProgramRuleInput input, CancellationToken` | `Task<ProgramRule>` | Regel anlegen |
| `UpdateRuleAsync` | `string userId, long ruleId, ProgramRuleInput input, CancellationToken` | `Task<ProgramRule>` | Regel ändern |
| `SetRuleActiveAsync` | `string userId, long ruleId, bool isActive, CancellationToken` | `Task<ProgramRule>` | Aktiv-Schalter |
| `DeleteRuleAsync` | `string userId, long ruleId, CancellationToken` | `Task` | Regel löschen |

## `IProgramScheduleService`
Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs` — enthält zugleich die Ergebnis-Records.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `DateTimeOffset utcNow, CancellationToken` | `Task<ProgramScheduleRunSummary>` | Plan extend-only bis Horizont auffüllen |
| `ReconcileAsync` | `DateTimeOffset utcNow, CancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Plan gegen aktuelle Regeln/Medien neu bewerten |
| `GetScheduleAsync` | `long channelId, DateTimeOffset fromUtc, int maxCount, CancellationToken` | `Task<List<ProgramSlot>>` | Admin-Sendeplansicht |
| `DeleteDisplacedSlotsAsync` | `string userId, long channelId, CancellationToken` | `Task<int>` | Displaced-Slots bereinigen (Admin) |
| `ResetScheduleAsync` | `string userId, long channelId, CancellationToken` | `Task<ProgramScheduleResetResult>` | Plan + Cursor zurücksetzen (Admin) |
| `GetRuleDiagnosesAsync` | `string userId, long channelId, CancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Pro-Regel-Diagnose (Admin) |

Zugehörige Records in derselben Datei:
- `ProgramScheduleRunSummary(SlotsCreated, FillerSlotsCreated, SkippedNoDuration, SkippedNoAccess, OrphanedRules, DroppedConflicts, RemainingGapSeconds)` — `RemainingGapSeconds` ist der dokumentierte Restlücken-Zähler, auf den die Anforderung („verbleibender Rest bleibt dokumentierte Lücke") Bezug nimmt.
- `ProgramScheduleReconcileSummary(SlotsDisplaced, SlotsCreated, FillerSlotsCreated, SkippedNoDuration, SkippedNoAccess, OrphanedRules, DroppedConflicts, RemainingGapSeconds)`
- `ProgramRuleDiagnosis(RuleId, MaterialMissing, CandidateCount, SchedulableCount, NoMediaItemCount, ProbePendingCount, ProbeExhaustedCount, NoAccessCount)`
- `ProgramScheduleResetResult(SlotsDeleted, CursorsDeleted)`

## `IProgramGuideService`
Datei: `VideoWebPlayer/Services/IProgramGuideService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetNowAsync` | `DateTimeOffset utcNow, CancellationToken` | `Task<DtoProgramGuideNow>` | „Jetzt/Nächstes" |
| `GetUpcomingAsync` | `DateTimeOffset utcNow, TimeSpan preview, CancellationToken` | `Task<DtoProgramGuideEntry[]>` | Begrenzte Vorschau (≤12 h) |

## `IProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | — | `void` | Planänderung signalisieren (unterdrückt während `BeginScan`) |
| `BeginScan` | — | `IDisposable` | Scan-Scope; Ende weckt den Worker |
| `WaitAsync` | `CancellationToken` | `ValueTask` | Auf Signal warten |

## `ProgramRuleInput` (Record, DTO der Regel-Mutationen)
Datei: `VideoWebPlayer/Services/ProgramRuleInput.cs`

`ProgramRuleInput(string Name, ProgramRuleType RuleType, ProgramRuleRecurrence Recurrence, DayOfWeek? DayOfWeek, DateOnly? SingleDate, TimeOnly TimeOfDay, int? EpisodeCount, TimeSpan? SlotDuration, ProgramRuleMaterialType MaterialType, long MaterialId, int Priority, bool IsActive, IReadOnlyList<long>? GenreIds = null, IReadOnlyList<long>? MediaSourceIds = null)` — **ohne** Gruppen-Ids oder Karenzzeit.

## `DtoGenreOption` (Client-DTO)
Datei: `VideoWebPlayer.Client/Models/DtoMovie.cs` (Z. 100): `{ long Id; string Name }` — Muster für eine wählbare Listenoption (z. B. Gruppen-Picker).

## `IBackupData`-Implementierung
`VideoWebPlayerBackupData` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`) implementiert `msTools.Backup.IBackupData` — `WriteToAsync`/`ReadFromAsync` mit den Optional-/Default-Mechanismen aus [models.md](models.md).

## `IProgramGuideApiClient`
Registriert auf `VideoWebPlayerClient` (`ServiceCollectionExtensions.cs:234`); liefert der Blazor-Oberfläche die Programmauskunft-Daten — für die Anforderung nicht erweiterungsbedürftig im Bestand.
