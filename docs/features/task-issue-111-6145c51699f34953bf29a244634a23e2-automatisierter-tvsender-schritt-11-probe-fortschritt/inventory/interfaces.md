# Bestandsaufnahme — Interfaces und öffentliche Typen

## `IProgramScheduleService`
Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleRunSummary>` | Sendeplan bis Horizont auffüllen (nur append) |
| `ReconcileAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Sendeplan neu bewerten, veraltete Slots `Displaced` markieren |
| `GetScheduleAsync` | `long channelId`, `DateTimeOffset fromUtc`, `int maxCount`, `CancellationToken` | `Task<List<ProgramSlot>>` | Kommende Sendeplätze für die Admin-Ansicht |
| `GetRuleDiagnosesAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Pro-Regel-Planungsdiagnose, **admin-gegate** (Referenzmuster für diese Anforderung) |

Zugehörige öffentliche Resultat-Typen in derselben Datei:
- `ProgramScheduleRunSummary`, `ProgramScheduleReconcileSummary` (readonly record struct)
- `ProgramRuleDiagnosis` (readonly record struct): `RuleId`, `MaterialMissing`,
  `CandidateCount`, `SchedulableCount`, `NoMediaItemCount`, `ProbePendingCount`,
  `ProbeExhaustedCount`, `NoAccessCount` — Vorbild für den neuen Fortschritts-Record.

## `MediaDurationProbeService`
Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs` — konkrete `sealed`-Klasse
**ohne** Interface (wie `ProgramSettingsService`); wird vom Worker per Scope aufgelöst und
kann in `TVSenderAdmin.razor` direkt injiziert werden. Derzeitige öffentliche Fläche: nur
`ProbePendingAsync`. Für die Anforderung kommt eine Abfragemethode (z. B.
`GetProgressAsync(string userId, int recentCount, CancellationToken)`) hinzu.

## Weitere beteiligte Interfaces (unverändert)

- `IMediaDurationProbe` — `ProbeFileAsync`/`ProbeStreamAsync` → `MediaDurationProbeResult`
- `IMediaSourceReader` — `OpenFileStream` u. a. für Remote-Quellen
- `IFfprobeLocator` — `Resolve()` → Pfad oder `null`
- `IProgramScheduleSignal` — `NotifyScheduleChanged()` / `WaitAsync` / `BeginScan`
- `ForbiddenAccessException` (`internal`, `VideoWebPlayer.Controllers.Exceptions`) und
  `UnauthorizedAccessException` — etablierte Fehler der `RequireAdminAsync`-Muster.
