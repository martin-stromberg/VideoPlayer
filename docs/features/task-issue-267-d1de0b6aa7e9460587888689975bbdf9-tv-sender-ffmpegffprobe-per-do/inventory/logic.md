# Bestandsaufnahme: Logikklassen und Komponenten

## `FfprobeLocator`

**Datei:** `VideoWebPlayer/Services/FfprobeLocator.cs` — `sealed`, implementiert `IFfprobeLocator`, DI: Singleton.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `FfprobeLocator(IOptions<MediaProbeOptions>)` | public ctor | Delegiert an internen Ctor mit `AppContext.BaseDirectory` + `Environment.GetEnvironmentVariable("PATH")` |
| `FfprobeLocator(IOptions<MediaProbeOptions>, string baseDirectory, Func<string?> pathEnvironment)` | internal ctor | Testbarer Ctor (Basisverzeichnis + PATH-Quelle injizierbar) |
| `Resolve()` | public | Suchreihenfolge: 1) `MediaProbeOptions.FfprobePath` (gesetzt → nur dieser, `File.Exists`-Prüfung, sonst `null`), 2) `ConventionCandidates()`, 3) `ResolveFromPath()` |
| `ConventionCandidates()` | private | Pro Binary-Name: `{baseDir}/tools/ffmpeg/{name}` dann `{baseDir}/{name}`; `ExecutableNames` = `ffprobe.exe`,`ffprobe` (Windows) bzw. `ffprobe` (Linux) |
| `ResolveFromPath()` | private | Iteriert `PATH`-Verzeichnisse, `File.Exists` je Name; `ArgumentException`-sicher |

**Wichtig:** kein Caching — jeder `Resolve()`-Aufruf prüft das Dateisystem neu; ein nachinstalliertes Binary greift ohne Neustart.

## `FfmpegLocator`

**Datei:** `VideoWebPlayer/Services/FfmpegLocator.cs` — spiegelt `FfprobeLocator` 1:1 mit `MediaProbeOptions.FfmpegPath` und `ffmpeg(.exe)`.

## `MediaDurationProbeService`

**Datei:** `VideoWebPlayer/Services/MediaDurationProbeService.cs` — `sealed`, DI: Scoped. Konstruktor-Deps: `ApplicationDbContext`, `IMediaSourceReader`, `IMediaDurationProbe`, `IFfprobeLocator`, `IOptions<MediaProbeOptions>`, `ILogger`, optional `TimeProvider`, `IProgramScheduleSignal`, `IInternalMediaStreamTokenService`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `ProbePendingAsync(int batchSize, CancellationToken)` | public | Früher Abbruch bei `_locator.Resolve() == null` mit einmaliger Warnung (`static _missingBinaryLogged`, Z. 31/85-92); sonst Batch-Auswahl + `RunProbesAsync` + `ApplyOutcome`; benachrichtigt `IProgramScheduleSignal` bei geschriebenen Dauern |
| `GetProgressAsync(userId, recentCount=10, detailItemCount=25, ct)` | public | Admin-only (`RequireAdminAsync`); aggregiert Zähler + Recent-/Detail-Listen → `MediaDurationProbeProgress` |
| `ResetFailedProbeAsync(userId, mediaItemId, ct)` | public | Admin-only; `ExecuteUpdate` auf Failed-Set → zurück in „ausstehend" |
| `ResetFailedProbesAsync` (im Rest der Datei, nicht gezeigter Teil) | public | Reset aller fehlgeschlagenen Items |
| `RequireAdminAsync(userId, ct)` | private | Wirft `UnauthorizedAccessException` (nicht angemeldet) bzw. `ForbiddenAccessException` (kein Admin) — **das serverseitige Muster für die Download-Aktion** |
| `RunProbesAsync`, `ProbeItemAsync`, `ProbeRemoteItemAsync`, `ResolveRemotePath`, `ApplyOutcome`, `VideoExtensionPredicate`, `FailedProbePredicate`, `DetailItemsAsync` | private/static | Probe-Ausführung (Semaphore-begrenzt), Remote-Pfad HTTP→sftp→pipe-Fallback, Ergebnisanwendung |

## `FfprobeMediaDurationProbe`

**Datei:** `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs` — implementiert `IMediaDurationProbe`, DI: Singleton.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `ProbeFileAsync`, `ProbeUriAsync`, `ProbeStreamAsync` | public | `IMediaDurationProbe`-Implementierung; lösen `ffprobePath` je Aufruf über `_locator.Resolve()` — `null` → LogWarning + `TransientFailure` (Z. 146-151) |
| `ProbeAsync` (private) | private | Prozessausführung über injizierbaren `_executeAsync`-Delegate (Testbarkeit); Exit-Code/stderr-Klassifikation (permanente Marker, `Protocol not found` pro Schema gemerkt in `_unsupportedSchemes`, HTTP-Transportfehler via `FfmpegErrorMarkers`) |
| `BuildArguments`, `TryParseDurationOutput` | internal static | Argumentstring bzw. Output-Parsing — in Tests direkt geprüft |
| `ExecuteProcessAsync` | private static | `Process`-Start, stdout/stderr/stdin-Pump, Timeout-Kill |

## `FfmpegLiveStreamSegmenter`

**Datei:** `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs` — `sealed`, implementiert `ILiveStreamSegmenter`, DI: Singleton. Enthält auch `ISegmenterProcess` (internal, Testabstraktion) und `SegmenterProcess`.

| Methode/Eigenschaft | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `IsRunning`, `CurrentSlotId`, `LastUnexpectedExit`, `ExpectedPositionSeconds` | public | Prozesszustand (nicht persistiert) |
| `StartAsync(LiveStreamJob, ct)` | public | `SemaphoreSlim _gate` (nur 1 Prozess); stoppt vorherigen Lauf; startet ffmpeg mit `BuildArguments(job)` und `job.FfmpegPath`; stderr-Drain + stdin-Pump als Hintergrundtasks |
| `StopAsync(ct)` | public | Idempotenter Stopp (Kill + 10s-Timeout) |
| `BuildArguments(LiveStreamJob)` | internal static | ffmpeg-Kommandozeile (`-re`, `-ss`, libx264/aac, HLS `append_list`, `-t`) |
| `ReportProcessExit` | private | Klassifiziert Exit; setzt `LiveStreamExitInfo` (inkl. `HttpTransportFailure`-Flag via `FfmpegErrorMarkers`) |

## `LiveStreamCoordinator`

**Datei:** `VideoWebPlayer/Services/LiveStreamCoordinator.cs` — `sealed`, DI: Scoped. Enthält `enum LiveStreamSyncAction` + `record LiveStreamSyncResult` (siehe enums.md).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `SynchronizeAsync(utcNow, ct)` | public | `SynchronizeStreamAsync` + Segment-Retention-Sweep |
| `SynchronizeStreamAsync` | private | **Z. 91-97: `_ffmpegLocator.Resolve() == null` → `segmenter.StopAsync` + `LiveStreamSyncAction.FfmpegUnavailable`** („ffmpeg wurde nicht gefunden (konfigurierter Pfad, tools/ffmpeg/, PATH).") — heute nur Log-Warnung, kein UI-Hinweis; danach Guide-Abfrage, Input-Auflösung, Drift-Prüfung (`MaxDriftSeconds = 30`), Job-Start |

## `LiveStreamWorker`

**Datei:** `VideoWebPlayer/Services/LiveStreamWorker.cs` — `BackgroundService`, DI: Hosted (nicht in „Testing"-Umgebung). `PollCap = 30s`. Loggt `FfmpegUnavailable` gedrosselt (5 Min) als Warnung („Der Live-Stream pausiert, bis ffmpeg verfuegbar ist.", Z. 136-143). Nach Installation startet der nächste Takt den Stream automatisch.

## `MediaDurationProbeWorker`

**Datei:** `VideoWebPlayer/Services/MediaDurationProbeWorker.cs` — `BackgroundService`, DI: Hosted. Pollt `MediaDurationProbeService.ProbePendingAsync` im `MediaProbeOptions.EffectiveIntervalSeconds`-Takt hinter `IBackgroundProcessingGate`; TV-Sender-Feature-Flag wird je Zyklus geprüft.

## `ManualBackupJobService` (Referenzmuster Job-/Snapshot-Singleton)

**Datei:** `VideoWebPlayer/Services/Backups/ManualBackupJobService.cs` — `sealed`, DI: Singleton.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `GetSnapshot()` | public | Lock-geschützter `ManualBackupJobSnapshot` |
| `StartManualBackup(userId)` | public | `_current.IsActive` → abgelehnt (`Started=false`); sonst neuer Snapshot `Queued` + `Task.Run(RunBackupAsync)` — **verhindert Parallel-Jobs** |
| `RunBackupAsync`, `SetSnapshot` | private | Status-Übergänge Running → Succeeded/Failed; `IServiceScopeFactory` für Scoped-Deps außerhalb der Request-Lifetime |

Wird **nicht** direkt aus der Razor-Seite gestartet, sondern per Form-POST auf `BackupsController` (`[Authorize(Policy = "AdminOnly")]`, `VideoWebPlayer/Controllers/BackupsController.cs:15-16`, Route `admin/backups/api/create`) — die UI pollt nur den Snapshot. Das ist ein zweites, controller-basiertes Admin-Absicherungsmuster neben `RequireAdminAsync`.

## `TVSenderAdmin.razor`

**Datei:** `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (1527 Zeilen) — Route `/admin/tv-sender`, `@rendermode InteractiveServer`, `IDisposable`.

**Injektionen:** `ApplicationDbContext`, `IProgramRuleService`, `IProgramScheduleService`, `IProgramScheduleSignal`, `MediaDurationProbeService`, `ProgramSettingsService`, `AuthenticationStateProvider`, `VideoWebPlayerClient` — **keine** Locator-Injektion (`IFfprobeLocator`/`IFfmpegLocator` werden in der Komponente derzeit nicht genutzt).

**Tabs:** `regeln`, `sendeplan`, `einstellungen`, `laufzeit` — Panes bleiben gerendert, nur CSS-Umschaltung (`SelectTab`, Z. 803).

**„fehlt"-Erkennung heute:**
- Tab „Laufzeitermittlung": `ProbeStatusLabel(progress)` (Z. 853-862) meldet bei `PendingCount > 0` und `ProbedLastHour == 0` nur indirekt: „…die Ermittlung hängt möglicherweise (ffprobe verfügbar?)" — gerendert als `form-text` `#tvsender-probe-status` (Z. 499). Kein expliziter ffprobe-Check, kein Button.
- Tab „Einstellungen": Segmentierungs-Karte (`admin-sub-card`, Z. 445-459) mit Segmentlänge/-verzeichnis/-aufbewahrung — **kein** ffmpeg-Verfügbarkeits-/Pausier-Hinweis; der `FfmpegUnavailable`-Zustand existiert nur im Log.

**Fortschrittsanzeigen vorhanden:** `.progress`/`.progress-bar` (Probe-Fortschritt, Z. 525-528) und `.progress-track`/`.progress-fill` (laufender Slot, Z. 393). Dialog-Overlay-Muster `admin-dialog-overlay`/`admin-dialog` für Bestätigungen (`confirmCleanup`, `confirmReset`, `confirmProbeReset`, `deleteCandidate`) — verwendbar für einen GPL-Hinweis-Dialog.

**Polling-Muster:** `ProbeProgressLoopAsync` (Z. 812-848) — `PeriodicTimer(15s)`, `InvokeAsync`-Dispatcher-Hop, Fehler tolerierend, `disposed`-Flag + `CancellationTokenSource refreshCts`; explizit als Muster von `PollBackupStatusAsync` in `Backups.razor` referenziert. `Backups.razor:517-549` zeigt das Snapshot-Polling (3 s) für `ManualBackupJobService`/`RestoreBackupJobService`.

**Admin-Prüfung UI-seitig:** `isAdmin` aus Claim `IsAdmin=="True"` in `OnInitializedAsync` (Z. 766-769); Nicht-Admin → „Nicht autorisiert."-Alert. Service-Aufrufe prüfen serverseitig (`RequireAdminAsync`).

**Relevante `@code`-Member:** `probeProgress`, `probeStatus`, `confirmProbeReset`, `showPendingProbeDetails`, `showFailedProbeDetails`, `channelStatus`, Felder für Sender-/Planungs-/Segmentierungs-Einstellungen, `RecentProbeCount = 10`, `ProbeRefreshInterval = 15 s`.

## `Backups.razor` (Polling-Referenz)

`VideoWebPlayer/Components/Pages/Admin/Backups.razor`: `manualBackupJob`-Feld, `StartBackupStatusPolling` + `PollBackupStatusAsync` (`PeriodicTimer` 3 s), Status-Alerts via `GetManualBackupAlertClass`/`GetManualBackupStatusText`, Restore-Fortschritt `Datensatz x von y` (Z. 61-64).

## `UpdateAdminService` / Updates.razor (Download-Infrastruktur-Stand)

`VideoWebPlayer/Services/Updates/UpdateAdminService.cs`: Fassade über `msTools.Updater` (`IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`); `InstallAsync` ruft `_commands.DownloadAsync` — der Download läuft in der Bibliothek, UI sieht nur `AutoUpdateState.Downloading` als Chip-Text (`Updates.razor:468,482`). **Keine Byte-/Prozent-Fortschrittsanzeige für Downloads vorhanden.**

**HttpClient-Infrastruktur:** genau ein benannter Client `AddHttpClient("Internal")` (`ServiceCollectionExtensions.cs:171-177`) mit User-Agent + `X-API-Key` — für **interne** Self-Calls gedacht; ein ausgehender Download-Client existiert nicht. `services.AddScoped<HttpClient>` (Z. 180-213) liefert denselben Internal-Client mit Basis-URL-Auflösung. `IHttpClientFactory` ist im DI-Container verfügbar.

**Weitere HTTP-Nutzung:** `InternalVideoWebPlayerClient` (interne API), `MediaSourceClassifier` (externe Metadaten-APIs), `VideoWebPlayerUpdateSourceFactory` (GitHub-Releases via `AutoUpdateGithubSource`).

**Update-Dateierhalt:** `appsettings.json` `AutoUpdate:ProtectedFiles` schützt `web.config` (Merge) und `appsettings*.json` (Merge). `docs/TECH_Auto_Update.md:117ff` dokumentiert: deployment-seitig zusätzlich angelegte Dateien, die nicht im Paket enthalten sind, bleiben unverändert erhalten — `tools/ffmpeg/` fällt darunter (nicht im Paket), ist aber **nicht** explizit in `ProtectedFiles` eingetragen.
