# Auslöser der Plan-Neuberechnung — Bestandsaufnahme (Issue #290)

Alle Stellen, die eine Neubewertung des Sendeplans auslösen oder zeitlich treiben. DI-Registrierung: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` — `ProgramScheduleSignal` als Singleton (Z. 268–269), `MediaDurationProbeWorker` (Z. 258), `MediaSourceScanService` (Z. 326), `ProgramScheduleWorker` (Z. 335), `LiveStreamWorker` (Z. 339) als Hosted Services.

## 1. Worker-Takt: `ProgramScheduleWorker`
Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| Konstruktor | public | `IServiceScopeFactory`, `ILogger`, optional `IProgramScheduleSignal`, `initialDelay` (Default 15 s), `TimeProvider` |
| `ExecuteAsync` | protected override | Endlosschleife bis Abbruch |

Ablauf je Iteration (Z. 54–100):
1. Frischer DI-Scope; Intervall jedes Mal neu gelesen: `ProgramSettingsService.GetProgramScheduleIntervalAsync` (Fallback 15 min, Z. 61).
2. Nur wenn `GetTvSenderEnabledAsync` `true` liefert: Lease über `IBackgroundProcessingGate` („Sendeplan"), dann nacheinander `IProgramScheduleService.ReconcileAsync(_timeProvider.GetUtcNow(), …)` (Z. 71) **und** `EnsureCoverageAsync(...)` (Z. 72) — `ReconcileAsync` läuft also bei jedem Tick.
3. Schlafen: `Task.Delay(interval)` **oder** `_signal.WaitAsync(stoppingToken)` — was zuerst eintritt (`Task.WhenAny`, Z. 88–99). Bei Feature-Flag `false` bleibt der Takt und das Signal-Aufwachen bestehen, ohne dass bewertet wird (Z. 74–75).
4. Fehler je Iteration werden nur geloggt (`[ProgramScheduleWorker] Fehler bei der Sendeplan-Nachfuehrung.`, Z. 83) — die Schleife läuft weiter.

Folge: Jede `ReconcileAsync`-Nicht-Idempotenz wird pro Intervall (Default 15 min) **und** pro Signal-Aufwachen erneut ausgeführt.

## 2. Weckleitung: `IProgramScheduleSignal` / `ProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

- Bounded Channel der Kapazität 1 (`BoundedChannelFullMode.DropWrite`, `SingleReader`): Mehrere Signale ohne Wartenden koaleszieren zu genau einem Aufwachen (Z. 44–48).
- `NotifyScheduleChanged()` schreibt nur, wenn `_activeScans == 0` (Z. 53–59); `BeginScan()` liefert einen `IDisposable`-Scope, dessen `Dispose` beim letzten offenen Scope genau einmal weckt (`EndScan`, Z. 72–76). `WaitAsync` liest den Channel (Z. 69–70).
- Zweiter Konsument neben `ProgramScheduleWorker`: `LiveStreamWorker` wartet auf dasselbe Signal (`VideoWebPlayer/Services/LiveStreamWorker.cs:22, 40`).

## 3. Signalquellen (Produzenten von `NotifyScheduleChanged` / `BeginScan`)

### `ProgramRuleService` — Regel-CRUD
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs` (optionale Ctor-Injection `IProgramScheduleSignal?`, Z. 23/32)

| Aufrufstelle | Methode | Anlass |
|--------------|---------|--------|
| Z. 143 | `CreateRuleAsync` | Neue Regel (nach Save + Link-Ersetzung) |
| Z. 174 | `UpdateRuleAsync` | Regeländerung (alle Felder inkl. `TimeOfDay`, `Priority`, `IsActive`, Clip-Gruppen, Genre-Links) |
| Z. 226 | `SetRuleActiveAsync` | (De)Aktivierung |
| Z. 240 | `DeleteRuleAsync` | Regel-Löschung |

### `ProgramClipGroupService` — Clip-Gruppen-CRUD
Datei: `VideoWebPlayer/Services/ProgramClipGroupService.cs` (Injection Z. 16/25)

| Aufrufstelle | Methode | Anlass |
|--------------|---------|--------|
| Z. 51 | `CreateGroupAsync` | Neue Gruppe |
| Z. 68 | `RenameGroupAsync` | Umbenennen |
| Z. 83 | `DeleteGroupAsync` | Gruppe gelöscht |
| Z. 206 | `AddClipsToGroupAsync` | Clip-Mitgliedschaften hinzugefügt |
| Z. 224 | `RemoveClipFromGroupAsync` | Mitgliedschaft entfernt — feuert auch, wenn die Zeile gar nicht existierte (`FindAsync` → `null`: kein Remove, trotzdem Signal, Z. 218–224) |

### `MediaDurationProbeService` — Laufzeit-Proben (Dauerfeuer während Backlogs)
Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs` (Injection Z. 27/56)

- `ProbePendingAsync(int batchSize, …)` (Z. 78–132): pro Aufruf bis zu `batchSize` Items (`EffectiveBatchSize`, Default 10). Für jedes verarbeitete Item wird einzeln `SaveChangesAsync` aufgerufen (Z. 117); nach der Schleife feuert `NotifyScheduleChanged` genau **einmal je `ProbePendingAsync`-Aufruf**, wenn mindestens eine `Duration` geschrieben wurde (Z. 123–129).
- `MediaDurationProbeWorker.ExecuteAsync` (`VideoWebPlayer/Services/MediaDurationProbeWorker.cs:48–100`): ruft `ProbePendingAsync` in einer `do … while (processed > 0)`-Schleife auf (Z. 74–80) — bei einem Backlog von N Items feuert das Signal also ⌈N/BatchSize⌉-mal pro Worker-Durchlauf; der Worker schläft danach `EffectiveIntervalSeconds` (Default 60 s, Z. 92). Feature-Flag-Prüfung je Durchlauf (Z. 65).
- Jede geschriebene `Duration` ist zugleich eine echte Eingabeänderung des Plans (Item wird planbar) **und** kann den Repräsentanten in `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` wechseln (bevorzugt wird `Duration != null`, dann kleinste `MediaItemId`, `ProgramScheduleService.cs:2150–2154, 2163–2167`).
- `ResetFailedProbeAsync`/`ResetFailedProbesAsync` (Z. 430–475) feuern kein Signal.

### `MediaSourceScanService` — Scanende-Bündelung
Datei: `VideoWebPlayer/Services/MediaSourceScanService.cs`

- Umschließt den kompletten Scan+Klassifizierungs-Block mit `IProgramScheduleSignal.BeginScan()` (Z. 103, daneben `IPlaylistBackfillSignal.BeginScan()`, Z. 101): alle `NotifyScheduleChanged`-Aufrufe während des Scans (v. a. der SaveChanges-Markierungshook) sind unterdrückt; das Dispose des letzten Scopes am Scanende weckt den Worker genau einmal. Scan-Intervall aus `GetScanIntervalsAsync` (Fallback 1 h, Z. 92–95).

### `ApplicationDbContext` — SaveChanges-Hook + Quell-Löschung
Dateien: `VideoWebPlayer/Data/ApplicationDbContext.cs`, `ApplicationDbContext.PlaylistBackfillMarking.cs` (Injection `IProgramScheduleSignal? _programScheduleSignal`, `ApplicationDbContext.cs:19, 28, 33`)

- `SaveChanges(bool)`/`SaveChangesAsync(bool, CancellationToken)` (`PlaylistBackfillMarking.cs:55–144`): `CollectMarkCandidates` scannt den Change Tracker nach neuen/umgehängten `Movie`, `TVShowSeason`, `TVShowEpisode`; nur wenn Markierungen geschrieben werden, feuert `_programScheduleSignal?.NotifyScheduleChanged()` (Z. 85, 94 sync; Z. 133, 142 async). Ohne relevante Entitäten passiert nichts — `ProgramSlot`-/`ProgramRuleCursor`-Schreibungen der Planläufe selbst erzeugen **kein** Signal.
- `DeleteMediaSourceAsync` (`ApplicationDbContext.cs:293–455`): feuert nach Commit `NotifyScheduleChanged` (Z. 448).
- Raw-SQL/Bulk-Pfade (`ExecuteUpdate`, `ExecuteDelete`, Restore-Inserts) umgehen den Hook — hier greift nur der Intervall-Takt bzw. ein anderes Signal.

### Admin-UI
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (`@inject IProgramScheduleSignal ScheduleSignal`, Z. 13)

| Aufrufstelle | Methode | Anlass |
|--------------|---------|--------|
| Z. 1420 | `GenerateNowAsync` | Button „Jetzt generieren" (`#tvsender-generate-now`, Z. 402); danach 4 s Wartezeit und `ReloadScheduleAsync`/`ReloadDiagnosesAsync` |
| Z. 1469 | `ResetScheduleAsync` | Nach `ResetScheduleAsync` (löscht alle Slots + Cursor, Z. 1467) |
| Z. 1874 | `SaveChannelSettingsAsync` | Nach dem Speichern aller Sender-Einstellungen (Sendername, Bundesland, Horizont/Intervall, Inhaltsspektrum, Jugendschutzfenster, Unbekannte-Freigabe-Modus, Live-Stream-Parameter, Standby-Clip) |
| — | `CleanupDisplacedAsync` (Z. 1439–1457) | „Verdrängte bereinigen" ruft nur `DeleteDisplacedSlotsAsync` — **kein** Signal |
| — | `SaveRuleAsync`/`ToggleRuleActiveAsync`/`DeleteRuleAsync` (Z. 2052, 2108, 2138) | Kein direktes Signal — indirekt über `ProgramRuleService` |

`OnInitializedAsync` (Z. 1132–1188) feuert **kein** Signal — ein Seitenaufruf löst selbst keine Neubewertung aus; indirekt wirken nur die parallel laufenden Hintergrunddienste. Es läuft jedoch eine `ProbeProgressLoopAsync`-Aktualisierung (reine Anzeige).

Datei: `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor` (`@inject IProgramScheduleSignal ScheduleSignal`, Z. 8)

| Aufrufstelle | Methode | Anlass |
|--------------|---------|--------|
| Z. 234 | `SaveAsync` | Nach `UpdateGeneralSettingsAsync` (u. a. `TvSenderEnabled`) + `UpdateTvSenderAllowedUserIdsAsync` — feuert bei **jedem** Speichern der allgemeinen Einstellungen, auch ohne Änderung am Sender |

## 4. Frequenz-Übersicht (was `ReconcileAsync` faktisch antreibt)

| Quelle | Frequenz | Bemerkung |
|--------|----------|-----------|
| Intervall-Tick | alle `ProgramScheduleIntervalMinutes` (Default 15 min) | immer, solange `TvSenderEnabled` |
| Regel-/Clip-Gruppen-CRUD | je Mutation | sachlich begründet |
| `ProbePendingAsync` | je Batch mit ≥1 geschriebener `Duration`; Backlog-Schleife ohne Pause | bei laufendem Backlog potenziell mehrfach pro Minute |
| SaveChanges-Markierungshook | je `SaveChanges` mit neuen/umgehängten Medien | während Scans durch `BeginScan` gebündelt |
| `DeleteMediaSourceAsync` | einmal je Löschung | |
| Scanende (`BeginScan`-Dispose) | einmal je Scan | Bündelung aller Scan-internen Signale |
| UI („Jetzt generieren", Reset, Einstellungs-Speichern ×2) | je Klick | manuell |

Das Signal selbst koalesziert nur während eines laufenden `WaitAsync` nicht anstehende Mehrfachfeuer (1-Slot-Channel); jede aufeinanderfolgende Benachrichtigung nach einem abgeschlossenen `WaitAsync` weckt erneut.
