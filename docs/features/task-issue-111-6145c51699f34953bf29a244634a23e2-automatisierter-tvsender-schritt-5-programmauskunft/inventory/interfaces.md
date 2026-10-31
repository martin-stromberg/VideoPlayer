# Bestandsaufnahme: Interfaces

## `IProgramScheduleService`

Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleRunSummary>` | Plan lückenlos bis Horizont erweitern |
| `ReconcileAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Plan nach Änderungen re-bewerten/verdrängen |
| `GetScheduleAsync` | `long channelId`, `DateTimeOffset fromUtc`, `int maxCount`, `CancellationToken` | `Task<List<ProgramSlot>>` | Slots inkl. Displaced für Admin-Ansicht |

Alle Methoden nehmen `utcNow` explizit entgegen — Namens- und Signaturmuster, dem die
Programmauskunft folgt.

## `IProgramRuleService`

Datei: `VideoWebPlayer/Services/IProgramRuleService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetOrCreateChannelAsync` | `CancellationToken` | `Task<TVChannel>` | Liefert/legt den einzigen Sender an |
| (weitere: CRUD für `ProgramRule`, Umbenennung des Senders) | — | — | nicht relevant für die Auskunft |

## `IAuthService`

Datei: `VideoWebPlayer/Services/Authentication/` (IAuthService)

| Methode/Property | Zweck |
|------------------|-------|
| `CurrentUser` | Aktuell angemeldeter `ApplicationUser` oder `null` — Basis von `ApiBaseController.CurrentUser`/`CheckLoggedIn` |
| `LoginAsync`, `ImpersonateAsync` | Token-Ausstellung |

## `IProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

Koaleszierendes Weck-Signal für den `ProgramScheduleWorker` (Schreib-Seite; für die reine
Lese-Auskunft nicht benötigt).

## Neu zu schaffen

`IProgramGuideService` — Programmauskunft (eigener Begriff, kein „Playlist"-Naming), siehe Plan.
