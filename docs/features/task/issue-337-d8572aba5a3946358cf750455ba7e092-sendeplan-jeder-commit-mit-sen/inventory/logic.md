# Logik — Bestandsaufnahme Issue #337

## `ProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

Singleton (DI: `ServiceCollectionExtensions.cs` Z. 269–270, `ProgramScheduleSignal` als Singleton, `IProgramScheduleSignal` darauf gemappt). Zentraler Weckpunkt und naheliegender Erweiterungspunkt für eine Bündelung außerhalb von Scopes.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `NotifyScheduleChanged()` | `public` | Meldet sendeplan-relevante Eingabeänderung. Ohne offenen Scan-Scope (`_activeScans == 0`): schreibt sofort in `_channel` **und** `_liveChannel` (je 1-Slot, `DropWrite`). Mit offenem Scope: inkrementiert nur `_suppressedNotifications`; fällt der letzte Scope zwischen Check und Increment zu, wird direkt zugestellt (Race-Behandlung Z. 90–95). |
| `NotifyLiveScheduleChanged()` | `public` | Schreibt sofort in `_liveChannel` (materialisierte Planänderung, Issue #315). |
| `BeginScan()` | `public` | `_activeScans++`, liefert `ScanScope`-`IDisposable`. |
| `WaitAsync(CancellationToken)` | `public` | `ReadAsync` auf `_channel` (Plan-Leitung, Konsument: `ProgramScheduleWorker`). |
| `WaitLiveAsync(CancellationToken)` | `public` | `ReadAsync` auf `_liveChannel` (Live-Leitung, Konsument: `LiveStreamWorker`). |
| `EndScan()` | `private` | `_activeScans--`; beim letzten Scope einmaliges Wecken beider Kanäle, aber nur wenn `_suppressedNotifications > 0` war. |

Zustand: `_channel` + `_liveChannel` (je `Channel.CreateBounded<bool>(1)`, `BoundedChannelFullMode.DropWrite`, `SingleReader = true`), `int _activeScans`, `int _suppressedNotifications` (Z. 64–80). `ScanScope` ist eine private `IDisposable`-Klasse, idempotent (`Interlocked.Exchange`).

Publizierte Events: keine — Kommunikation läuft über die beiden internen Channels.
Abonnierte Events: keine.

Wichtig: `NotifyScheduleChanged()` hat **keinerlei Zeitkomponente** — außerhalb eines Scopes ist jeder Aufruf ein sofortiges Wecken (maximal durch den 1-Slot-Channel koalesziert, solange niemand liest). Es existiert kein Debounce-/Batch-Mechanismus.

## `ProgramScheduleWorker`
Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs`

`BackgroundService`, Konsument der Plan-Leitung. Jeder empfangene Signal-Impuls löst einen kompletten Lauf aus.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync(CancellationToken)` | `protected override` | Startdelay (Default 15 s), dann Schleife: je Iteration `ProgramSettingsService.GetProgramScheduleIntervalAsync` + `GetTvSenderEnabledAsync` lesen; bei aktivem Flag hinter `IBackgroundProcessingGate.EnterOperationAsync("Sendeplan")` `IProgramScheduleService.ExecutePassAsync` aufrufen; bei materialisierten Änderungen (`Reconcile.SlotsDisplaced`/`SlotsCreated`/`Coverage.SlotsCreated` > 0) `NotifyLiveScheduleChanged()` feuern (Z. 77–79). Danach `Task.WhenAny(delay, signalTask)` — `signalTask` überlebt Iterationen (Ghost-Reader-Vermeidung, Issue #315, Z. 93–111). |

Abonniert: `IProgramScheduleSignal.WaitAsync` (optionaler Ctor-Parameter `signal`, `null` → nur Intervall).
Publiziert: `IProgramScheduleSignal.NotifyLiveScheduleChanged` nach eigener Planänderung.

## `LiveStreamWorker`
Datei: `VideoWebPlayer/Services/LiveStreamWorker.cs`

`BackgroundService`, Konsument der Live-Leitung. Da `NotifyScheduleChanged` auch `_liveChannel` speist, wacht der Worker bei **jeder** Eingabeänderung — nicht nur bei materialisierten Planänderungen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync(CancellationToken)` | `protected override` | Segment-Verzeichnis aufräumen, Standby-Refresh, Startdelay (Default 10 s); Schleife: Feature-Flag prüfen, ggf. ffmpeg stoppen, sonst hinter Gate-Lease `LiveStreamCoordinator.SynchronizeAsync` je Tick; `WakeAtUtc` des Ergebnisses verkürzt den Sleep (max. `PollCap` = 30 s). Schlafen via `Task.WhenAny(delay, signalTask)` mit `WaitLiveAsync` (Z. 155–173). |
| `LogFfmpegUnavailableThrottled` | `private` | 5-Minuten-Drosselung der Fehlermeldung. |

Abonniert: `IProgramScheduleSignal.WaitLiveAsync` (optionaler Ctor-Parameter `signal`).

## `ApplicationDbContext` (partiell, Commit-Hook)
Dateien: `VideoWebPlayer/Data/ApplicationDbContext.cs`, `VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SaveChanges(bool)` | `public override` | Sammelt Mark-Kandidaten (`CollectMarkCandidates`), schreibt `PlaylistBackfillMarker` per Upsert in derselben Transaktion, feuert danach `_backfillSignal?.NotifyMarkersWritten()` **und** `_programScheduleSignal?.NotifyScheduleChanged()` (Z. 84–85, 93–94). Feuert nur, wenn Marker geschrieben wurden. |
| `SaveChangesAsync(bool, CancellationToken)` | `public override` | Async-Pendant, gleiche Signalfolge (Z. 132–133, 141–142). |
| `CollectMarkCandidates` / `TryGetChangedForeignKey` / `ResolveMarks(Async)` / `UpsertMarkersSql(Async)` / `UpsertMarkersTracked(Async)` | `private` | Markierungslogik: neue/umgehängte `Movie` → MovieCollection; `TVShowSeason` → TVShow; `TVShowEpisode` → Season + Show. In-Memory-Dedupe, chunked Upsert (200er-Chunks), Lookup-Query nur für nicht getrackte Seasons (500er-Chunks). |
| `DeleteMediaSourceAsync` | `public` | Löscht eine `MediaSource` kaskadierend (großteils per `ExecuteDeleteAsync`); nach `transaction.CommitAsync()` publiziert `MediaSourceDeletedEvent` und ruft `_programScheduleSignal?.NotifyScheduleChanged()` auf (`ApplicationDbContext.cs` Z. 447–448). |

Felder: `_eventManager` (`EventManager`), `_programScheduleSignal` (optionaler Ctor-Parameter, `ApplicationDbContext.cs` Z. 19, 28–33), `_backfillSignal` (partielle Datei, Z. 47). DI: `AddDbContext<ApplicationDbContext>(… UseSqlite …)` (`ServiceCollectionExtensions.cs` Z. 162); die Signale werden über den DI-Konstruktor injiziert.

Publiziert: `IPlaylistBackfillSignal.NotifyMarkersWritten`, `IProgramScheduleSignal.NotifyScheduleChanged` — je Commit mit Markern bzw. je Quell-Löschung.

## `ProgramRuleService`
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs`

Regel-CRUD mit Admin-Erzwingung. `_scheduleSignal` optional injiziert (Z. 24, 32).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateRuleAsync` | `public` | Regel anlegen + Genre-/ClipGroup-Links, danach `NotifyScheduleChanged()` (Z. 150). |
| `UpdateRuleAsync` | `public` | Regel ändern + Links, danach `NotifyScheduleChanged()` (Z. 182). |
| `SetRuleActiveAsync` | `public` | Aktiv-Flag, danach `NotifyScheduleChanged()` (Z. 254). |
| `DeleteRuleAsync` | `public` | Löschen, danach `NotifyScheduleChanged()` (Z. 268). |
| `ApplyValidity` | `internal static` | Normalisierung des Gültigkeitsfensters (mit Test-Seed geteilt). |
| `GetAllowedMaterialTypes`, `RequireAdminAsync`, `ValidateRuleInputAsync`, `ReplaceGenreLinksAsync`, `ReplaceClipGroupLinksAsync` u. a. | `public`/`private` | Fachliche Validierung und Pflege der Verknüpfungstabellen. |

Publiziert: `IProgramScheduleSignal.NotifyScheduleChanged` — **einmal je Service-Aufruf**, d. h. ein UI-Bearbeitungsfluss mit mehreren Saves feuert mehrfach.

## `ProgramClipGroupService`
Datei: `VideoWebPlayer/Services/ProgramClipGroupService.cs`

Clip-Gruppen-CRUD mit Admin-Erzwingung. `_scheduleSignal` optional injiziert (Z. 16, 25).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateGroupAsync` | `public` | Gruppe anlegen, danach `NotifyScheduleChanged()` (Z. 51). |
| `RenameGroupAsync` | `public` | Umbenennen, danach `NotifyScheduleChanged()` (Z. 68). |
| `DeleteGroupAsync` | `public` | Löschen, danach `NotifyScheduleChanged()` (Z. 83). |
| `AddClipsToGroupAsync` | `public` | Clips zufügen, danach `NotifyScheduleChanged()` (Z. 206). |
| `RemoveClipFromGroupAsync` | `public` | Clip entfernen, danach `NotifyScheduleChanged()` (Z. 224) — feuert auch, wenn das Item nicht gefunden wurde. |
| `GetGroupsAsync`, `GetGroupCountsAsync`, `ValidateGroupNameAsync` u. a. | `public`/`private` | Lesen und Validieren. |

## `MediaDurationProbeService`
Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProbePendingAsync(int, CancellationToken)` | `public` | Probt bis zu `batchSize` Items (parallel via `SemaphoreSlim`), speichert pro Item einzeln (`SaveChangesAsync` je Item, Z. 117); feuert **einmal je Aufruf** `NotifyScheduleChanged()`, wenn mindestens eine `Duration` geschrieben wurde (Z. 126–129). |
| `GetProgressAsync`, `ResetFailedProbeAsync`, `ResetFailedProbesAsync` | `public` | Admin-Fortschritt und Probe-Resets (kein Signal). |
| `RunProbesAsync`, `ProbeItemAsync`, `ProbeRemoteItemAsync`, `ApplyOutcome`, `VideoExtensionPredicate`, `FailedProbePredicate` | `private` | ffprobe-Aufrufe und Outcome-Mapping. |

`_scheduleSignal` optional (Z. 27, 56). Der Aufruf über den `MediaDurationProbeWorker` liegt in dessen `BeginScan`-Scope (gebündelt); direkte andere Aufrufer feuern sofort.

## `MediaDurationProbeWorker`
Datei: `VideoWebPlayer/Services/MediaDurationProbeWorker.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` | `protected override` | Startdelay (Default 10 s), Schleife im `MediaProbeOptions.EffectiveIntervalSeconds`-Takt; bei aktivem Flag hinter Gate-Lease **Backlog-Schleife `do { ProbePendingAsync(batchSize) } while (processed > 0)`** innerhalb eines `signal?.BeginScan()`-Scopes (Z. 81–91) — die Batch-Signale koaleszieren zu genau einem Wecken am Scope-Ende (Issue #290). |

Abonniert/publiziert: nutzt `IProgramScheduleSignal.BeginScan` (Suppression), feuert selbst nicht.

## `MediaSourceScanCoordinator`
Datei: `VideoWebPlayer/Services/MediaSourceScanCoordinator.cs` (Issue #333, Merge-Commit `7f33424`)

Referenzimplementierung des bestehenden Bündelungsmusters: `using var … = _programScheduleSignal.BeginScan()` umschließt den gesamten Scan inkl. Klassifizierung; Commit-Signale währenddessen werden unterdrückt und zum Scope-Ende zu **einem** Wecken koalesziert.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunFullScanAsync(string, int?, CancellationToken)` | `public` | Öffnet `BeginScan` auf **beiden** Signalen (Backfill Z. 85, Schedule Z. 86), Gate-Lease, `ScanAllSourcesAsync`, Notification, Genre-Reload, Collection-Schleife, Klassifizierung. |
| `RescanCollectionTreeAsync(long, string, CancellationToken)` | `public` | Gleiche Scope-Struktur (Z. 141–142) für einen Collection-Teilbaum. |
| `CountNewItemsAsync`, `ClassifyPendingCollectionsAsync` | `private` | Hilfsschritte. |

Aufrufer: `MediaSourceScanService` (Worker, Z. 101–102), `MediaSourceAdmin.razor` (Komplettscan, Z. 221), `MediaSourceExplorer.razor` („Neu erfassen“, Z. 251).

## `MediaSourceScanService`
Datei: `VideoWebPlayer/Services/MediaSourceScanService.cs`

`BackgroundService` für den automatischen Scanprozess; ruft im Intervall (`GetScanIntervalsAsync`) `MediaSourceScanCoordinator.RunFullScanAsync` auf (Z. 99–102). Der Schedule-Scope entsteht ausschließlich im Coordinator.

## `PlaylistBackfillSignal` / `PlaylistBackfillWorker` (Referenzmuster)
Dateien: `VideoWebPlayer/Services/PlaylistBackfillSignal.cs`, `VideoWebPlayer/Services/PlaylistBackfillWorker.cs`

Strukturell dasselbe Muster (`NotifyMarkersWritten`, `BeginScan`, `WaitAsync`, 1-Slot-Channel, `_activeScans`), aber **abweichende Weckbedingung**: `EndScan` weckt dort **unbedingt** (Z. 70–74), beim Schedule-Signal nur nach unterdrückter Notification. Nicht Gegenstand der Anforderung.

## Razor-Aufrufer

| Komponente | Stelle | Aufruf |
|------------|--------|--------|
| `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` | `@inject IProgramScheduleSignal ScheduleSignal` (Z. 13) | `GenerateNowAsync` („Jetzt generieren“, Z. 1541): feuert, wartet **4 s**, lädt Plan + Diagnosen neu. `ResetScheduleAsync` (Sendeplan-Reset, Z. 1590). `SaveChannelSettingsAsync` (Kanalname, Inhaltsspektrum, Jugendschutzfenster, Live-Settings, Z. 2028). |
| `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor` | `@inject IProgramScheduleSignal ScheduleSignal` (Z. 8) | `SaveAsync` (Z. 234): nach `UpdateGeneralSettingsAsync` + `UpdateTvSenderAllowedUserIdsAsync` — weckt Sendeplan/Stream/Probe bei Flag-Änderung sofort. |

## `ProgramSettingsService` (Konfigurationsdrehscheibe)
Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

Datenbankbasierte Sender-Laufzeitparameter je Installation: `GetTvSenderEnabledAsync` (Z. 195), `GetProgramScheduleIntervalAsync` (Z. 357, Default 15 min), dazu Update-Methoden (`UpdateProgramScheduleSettingsAsync`, `UpdateProgramScheduleContentScopeAsync`, `UpdateProgramScheduleAgeWindowsAsync`, `UpdateProgramScheduleUnknownRatingModeAsync`, `UpdateLiveStreamSettingsAsync`, `UpdateLiveStreamStandbyClipAsync`, `UpdateHolidayFederalStateAsync`) — alle werden in `TVSenderAdmin.razor`/`ProgramSettings.razor` aufgerufen, die anschließend selbst `NotifyScheduleChanged()` feuern.

## `LiveStreamCoordinator`
Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SynchronizeAsync(DateTimeOffset, CancellationToken)` | `public` | Ein Sync-Tick: fragt `IProgramGuideService.GetNowAsync`, startet/stoppt/ffmpeg-neusynchronisiert je nach Slot-Lage; liefert `LiveStreamSyncResult` mit `WakeAtUtc` (nächster Entscheidungspunkt). |

## `IBackgroundProcessingGate` (Restore-Schutz)
Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs`

`EnterOperationAsync(name, ct)` / `PauseForRestoreAsync(ct)`; `ProgramScheduleWorker`, `LiveStreamWorker`, `MediaDurationProbeWorker` und `MediaSourceScanCoordinator` treten je Lauf in die Sperre ein. Unberührt von der Anforderung.
