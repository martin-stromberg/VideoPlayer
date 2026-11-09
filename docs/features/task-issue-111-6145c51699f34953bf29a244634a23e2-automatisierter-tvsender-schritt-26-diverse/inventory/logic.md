# Logik: Stränge, Expansion, Refresh

## `ProgramScheduleService` (`VideoWebPlayer/Services/ProgramScheduleService.cs`, 2015 Z.)

| Mitglied | Sichtbarkeit | Kurzbeschreibung |
|----------|-------------|------------------|
| `ExpandMaterialAsync` | private | Dispatch auf Materialtyp |
| `ExpandGenreAsync` | private | Episoden `ORDER BY Show.Name, SeasonId, Number`; `StrandKey="show:{id}"`; Filme `ORDER BY ReleaseDate,Name` mit `StrandKey="other"`; Rückgabe `movieItems.Concat(episodeItems)` |
| `RuleState.SetItems` | private | Baut `Strands`: `order` = Reihenfolge des ersten Auftretens im Ring → „other" immer zuerst, dann Serien alphabetisch |
| `StrandState` | private | Ring je Strang: `NextIndex`, `PeekAt`, `Commit`, `RewindTo`; Cursor pro `StrandKey` (`ProgramRuleCursor`) |
| `SelectAiringItemsInterleaved` | private static | Round-Robin über `strands` in fester Reihenfolge ab `startOffset` |
| `StrandStartOffset` | private static | `wanted < m` → `day % m` (Tagesrotation); `wanted >= m` → 0 (stabile Positionen, N19-Design) |
| `MaterializeOccurrence` | private | ShiftPastInvaders → SelectItems → Konfliktprüfung → Slots + `CommitTaken` |
| `PersistCursors` | private | Cursorzeilen je Strang (`LastPlaced`) |
| `NextIndexFor` | private static | Cursor → nächster Index im Strang (inhaltsbasiert, reihenfolgeabhängig!) |

**Wichtig:** `NextIndexFor` findet die Cursor-Position per ContentId im Strang — Strang-*Reihenfolge*
der `Strands`-Liste ist cursor-neutral, aber die Item-Reihenfolge **innerhalb** eines Strangs muss
stabil bleiben (Episodenreihenfolge!). Permutation der Stränge ist daher der sichere Eingriffspunkt.

## `ProgramGuideService` (`VideoWebPlayer/Services/ProgramGuideService.cs`)

| Methode | Zweck |
|---------|-------|
| `GetNowAsync(utcNow, ct)` | laufender + nächster Slot, rein zeitbasiert |
| `GetUpcomingAsync(utcNow, preview, ct)` | Slots mit `End > now && Start < now+preview` — enthält den laufenden Slot |

## `LiveTv.razor` (`VideoWebPlayer/Components/Pages/LiveTv.razor`)

| Mitglied | Zustand |
|----------|---------|
| `OnInitializedAsync` | Auth → Token → `RefreshGuideAsync(true)` → `_pollTask = PollLoopAsync()` |
| `PollLoopAsync` | `PeriodicTimer(15s)` → Tick: `RefreshGuideAsync(false)` + `InvokeAsync(StateHasChanged)`; Fehler → Warnung + Retry |
| `RefreshGuideAsync(bool)` | **ein** try um beide Requests: `RequestProgramGuideNowAsync` + `RequestProgramGuideUpcomingAsync` → `guide`/`upcoming` gemeinsam zugewiesen — Fehler im ersten lässt beide alt |
| `OnAfterRenderAsync` | Player-Attach bei Slot-Wechsel (`_attachedSlotId`) |
| `DisposeAsync` | `_cts.CancelAsync` → `_timer.Dispose` → `await _pollTask` |

**Defekte:** (1) Poll startet bereits in `OnInitializedAsync` → Prerender-Instanz (wird nicht
disposed) startet ebenfalls eine Endlosschleife. (2) `now`/`upcoming` nicht entkoppelt —
ein dauerhaft fehlschlagender `now`-Request friert `upcoming` trotz laufendem Poll ein.

## `VideoWebPlayerClient` (`VideoWebPlayer.Client/`)

- `HttpGetAsync<T>(endpoint, ct)` → `SendWithReauthorizationAsync` → 401 → `HandleUnauthorized` →
  `TryRenewDeviceSessionAsync` (kein Refresh-Token beim internen Client → schlägt fehl → Exception)
- `InternalVideoWebPlayerClient` (`VideoWebPlayer/Services/Authentication/`): impersoniert einmal
  (`AuthorizationToken` leer → `ImpersonateAsync`), danach nie erneut.
