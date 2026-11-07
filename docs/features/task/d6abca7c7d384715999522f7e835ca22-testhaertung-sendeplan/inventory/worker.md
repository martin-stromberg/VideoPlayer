# Worker und Nebeneffekte (B14)

## `ProgramScheduleWorker`

Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs` — `BackgroundService`,
Konstruktor: `(IServiceScopeFactory, ILogger<ProgramScheduleWorker>, IProgramScheduleSignal? = null, TimeSpan? initialDelay = null, TimeProvider? timeProvider = null)`.

Schleife (`ExecuteAsync`, :55–112):

1. Scope öffnen; `ProgramSettingsService` (konkret) auflösen.
2. `interval = await settings.GetProgramScheduleIntervalAsync(ct)` — **wird je Iteration neu
   gelesen** (:62); Quelle: `Setups.ProgramScheduleIntervalMinutes` (Default 15, `<=0` → 15;
   `UpdateProgramScheduleSettingsAsync` erzwingt Minimum 1 Minute).
3. `GetTvSenderEnabledAsync` → nur wenn `true`:
   - `IBackgroundProcessingGate` optional aus dem Scope (:66); `EnterOperationAsync("Sendeplan", ct)`
     liefert `IAsyncDisposable`-Lease, das den Pass umschließt (:67–69).
   - `IProgramScheduleService.ExecutePassAsync(_timeProvider.GetUtcNow(), ct)` (:72).
   - `pass.Reconcile.SlotsDisplaced > 0 || pass.Reconcile.SlotsCreated > 0 || pass.Coverage.SlotsCreated > 0`
     → `_signal.NotifyLiveScheduleChanged()` (:77–79) — **derzeit ungetestet**.
4. Sleep: `Task.Delay(interval, _timeProvider, ct)` vs. `_signal.WaitAsync(ct)` — `Task.WhenAny`;
   `signalTask` überlebt Iterationen (:97–111).

## Interfaces / Typen

| Typ | Rolle | Fakebar? |
|-----|-------|----------|
| `IProgramScheduleService` | `ExecutePassAsync` → `ProgramSchedulePassSummary(ExpiredRulesPurged, SelfDisplacedCleaned, Reconcile: ProgramScheduleReconcileSummary, Coverage: ProgramScheduleRunSummary)` | Ja — Interface, scoped |
| `ProgramSettingsService` | `GetProgramScheduleIntervalAsync`, `GetTvSenderEnabledAsync` — konkrete Klasse, liest `Setups` | Über echtes SQLite steuerbar (Setups-Row ändern) |
| `IBackgroundProcessingGate` | `EnterOperationAsync(name, ct)` → `IAsyncDisposable`-Lease; `IsPausedForRestore`, `ActiveOperationCount`, `PauseForRestoreAsync` | Ja — Interface, scoped |
| `IProgramScheduleSignal` | `NotifyScheduleChanged`, `NotifyScheduleChangedNow`, `BeginScan`, `NotifyLiveScheduleChanged`, `WaitAsync`, `WaitLiveAsync` | Ja — Interface |
| `TimeProvider` | treibt `Task.Delay` und `GetUtcNow` | Ja — Konstruktorparameter; `Task.Delay(span, provider, ct)` läuft über `CreateTimer` |

## Bestehende Worker-Tests

`VideoWebPlayer.Tests/ProgramScheduleWorkerTests.cs` (334 Zeilen, 5 Tests): baut
`ServiceCollection` mit echtem In-Memory-SQLite, echtem `ProgramScheduleService` und
`ProgramScheduleWorker(initialDelay: Zero)`; pollt die DB bis zur Wirkung (Deadline 30 s /
5 s). Abgedeckt: EnsureCoverage beim Start, Feature-Flag aus, Purge abgelaufener Regeln,
Selbst-Verdrängungs-Bereinigung. **Nicht** abgedeckt: `NotifyLiveScheduleChanged`,
Gate-Lease, Intervall-Neulesung, Signal-Wakeup.

## Bestehende TimeProvider-Helfer

- `ManualTimeProvider` — steuerbares „Jetzt", Timer laufen auf Echtzeit.
- `ControllableTimeProvider` — Timer feuern nur bei `TickTimers()`.
- Kein Provider, der `Task.Delay(..., provider)` nach virtuellem Zeitfortschritt auslöst
  (`Microsoft.Extensions.TimeProvider.Testing` ist nicht referenziert) — für die
  Intervall-Neulesung braucht es einen kleinen eigenen Provider, der Timer bei
  `Advance` auslöst.
