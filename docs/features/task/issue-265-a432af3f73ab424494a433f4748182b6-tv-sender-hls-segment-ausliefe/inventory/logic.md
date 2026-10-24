# Logik — Bestandsaufnahme

Betroffener Bereich: HLS-Auslieferung des automatisierten TV-Senders über `GET /api/live-stream/{fileName}` sowie die Erzeugung/Löschung der Segmente im Hintergrund.

## `LiveStreamController`

Datei: `VideoWebPlayer/Controllers/LiveStreamController.cs`

Klassenkonstanten: `M3u8ContentType = "application/vnd.apple.mpegurl"` (Z. 20), `SegmentContentType = "video/mp2t"` (Z. 21). Route `api/live-stream`, Klassen-Attribut `[BearerTokenCheck]` (akzeptiert `access_token` im Query-String als Bearer-Ersatz), Basisklasse `ApiBaseController`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LiveStreamController(LiveStreamSegmentStore, ProgramSettingsService, IAuthService, ILogger<LiveStreamController>)` | public | Konstruktor; übergibt Auth/Logger an `ApiBaseController` (Z. 33–38) |
| `GetFile(string fileName)` → `Task<IActionResult>` | public, `[HttpGet("{fileName}")]` | Einziger Auslieferungs-Endpunkt (Z. 49–95). Ablauf: `CheckLoggedIn()` (401 via `catch UnauthorizedAccessException`, Z. 80–84) → Feature-Flag `GetTvSenderEnabledAsync` (404, Z. 56–57) → `IsTvSenderAccessGrantedAsync` (403 `Forbid`, Z. 60–62) → `_segmentStore.TryResolveFileAsync` (404 bei `null`, Z. 64–66) → `.m3u8`-Zweig: `CacheControl: no-store` + `RewritePlaylistAsync` → `ContentResult` (Z. 68–73) → `.ts`-/Fallback-Zweig: `PhysicalFile(path, contentType)` (Z. 75–78) — **Änderungsstelle der Anforderung**. Fehlerfänge: `IOException`/`UnauthorizedAccessException` → 500 (Z. 85–89), übrige → 500 (Z. 90–94) |
| `RewritePlaylistAsync(string path, CancellationToken)` → `Task<string>` | private | Playlist-Rewrite (Z. 100–128): öffnet die Datei bereits korrekt mit `new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite \| FileShare.Delete)` (Z. 106–107, Kommentar verweist auf das ffmpeg-Rewrite-in-place-Problem), hängt `access_token` an jede Nicht-`#`-Zeile an |

Details zur Änderungsstelle:

- Z. 75–77: `contentType` ist `video/mp2t` für `*.ts` (OrdinalIgnoreCase), sonst `application/octet-stream`.
- Z. 78: `PhysicalFile(path, contentType)` erzeugt ein `PhysicalFileResult` ohne `EnableRangeProcessing`; der zugehörige ASP.NET-Core-Executor öffnet die Datei per `File.OpenRead` → `FileShare.Read` (kein `Delete`-Share). Genau dies verursacht die Sharing-Verletzung, wenn `ffmpeg`/der Sweep ein noch ausgeliefertes Segment löscht.
- Der `TryResolveFileAsync`-Aufruf (Z. 64) liefert den geprüften absoluten Pfad; der `.ts`-Zweig öffnet die Datei danach ein zweites Mal (TOCTOU-Fenster: Löschen zwischen Resolve und Open führt zu `FileNotFoundException`/`IOException` → 500).

Abonnierte Events: keine
Publizierte Events: keine

## `LiveStreamSegmentStore`

Datei: `VideoWebPlayer/Services/LiveStreamSegmentStore.cs`

Bleibt laut Anforderung unverändert; liefert über `TryResolveFileAsync` den einzigen Pfad in den Auslieferungszweig und enthält die Löschlogik, die mit der Auslieferung konkurriert.

Klassenkonstanten/Felder: `SafeFileName` (Whitelist-Regex `^[A-Za-z0-9][A-Za-z0-9._-]*$`, Z. 21), `DefaultRelativeDirectory = Data/LiveStream` (Z. 23), `SegmentFilePrefix = "seg_"` (Z. 24), `SegmentFileExtension = ".ts"` (Z. 25).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LiveStreamSegmentStore(IWebHostEnvironment, ProgramSettingsService, ILogger<LiveStreamSegmentStore>)` | public | Konstruktor (Z. 37–45) |
| `GetDirectoryAsync(CancellationToken)` → `Task<string>` | public | Effektives Segmentverzeichnis (konfiguriert via `GetLiveStreamSegmentDirectoryAsync`, sonst `Data/LiveStream` unter ContentRoot), legt es an (Z. 54–65) |
| `TryResolveFileAsync(string fileName, CancellationToken)` → `Task<string?>` | public | Whitelist-Prüfung (`SafeFileName`), `Path.GetFullPath`-Containment unter dem Segmentverzeichnis, `File.Exists` → Pfad oder `null` (Z. 74–95). Aufgerufen von `LiveStreamController.GetFile` (Z. 64) |
| `SweepExpiredSegmentsAsync(DateTimeOffset utcNow, TimeSpan retention, CancellationToken)` → `Task<int>` | public | Löscht `seg_*.ts` mit `LastWriteTimeUtc < utcNow - retention`; `IOException`/`UnauthorizedAccessException` je Datei → `LogDebug` + weiter (Z. 106–131). Aufgerufen von `LiveStreamCoordinator.SynchronizeAsync` (Z. 85) |
| `ClearAsync(CancellationToken)` | public | Leert das gesamte Segmentverzeichnis, Fehler je Datei → `LogDebug` (Z. 138–152). Aufgerufen von `LiveStreamWorker.ExecuteAsync` (Z. 62) |

## `LiveStreamCoordinator`

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SynchronizeAsync(DateTimeOffset utcNow, CancellationToken)` → `Task<LiveStreamSyncResult>` | public | Synchronisiert die ffmpeg-Segmentierung mit dem Sendeplan und führt danach den Retention-Sweep aus: `_settings.GetLiveStreamSegmentRetentionAsync` + `_segmentStore.SweepExpiredSegmentsAsync` (Z. 81–87) — der Aufrufer der Löschlogik, die mit offenen Segment-Streams konkurriert |
| `SynchronizeStreamAsync(DateTimeOffset utcNow, CancellationToken)` → `Task<LiveStreamSyncResult>` | private | Slot-/Drift-Entscheidung, `LiveStreamJob`-Erstellung inkl. Segmentverzeichnis via `_segmentStore.GetDirectoryAsync` (Z. 89–161) |

Konstante: `MaxDriftSeconds = 30` (Z. 43).

## `LiveStreamWorker`

Datei: `VideoWebPlayer/Services/LiveStreamWorker.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync(CancellationToken)` | protected override | BackgroundService: nach Start-Verzögerung einmalig `LiveStreamSegmentStore.ClearAsync` (Z. 59–63), danach Poll-Schleife mit `LiveStreamCoordinator.SynchronizeAsync` je Scope hinter `IBackgroundProcessingGate` (Z. 65–127). Bei deaktiviertem Feature-Flag wird ein laufender Segmenter gestoppt (Z. 73–80) |
| `LogFfmpegUnavailableThrottled(string?)` | private | 5-Minuten-Drossel für die ffmpeg-fehlt-Warnung (Z. 136–143) |

Konstante: `PollCap = 30 s` (Z. 17).

## `FfmpegLiveStreamSegmenter`

Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`

Implementiert `ILiveStreamSegmenter`; betreibt den einzelnen ffmpeg-Prozess. Für die Anforderung relevant: ffmpeg pflegt das HLS-Rolling-Window selbst und **löscht abgelaufene Segmente eigenständig** — Argumente in `BuildArguments` (Z. 372–397): `-f hls -hls_time {segmentSeconds} -hls_list_size 12` mit `-hls_flags delete_segments+independent_segments+append_list+discont_start+program_date_time`, Segmentnamen `seg_{yyyyMMddHHmmss}_{SlotId}_%05d.ts`, Playlist `live.m3u8` (`PlaylistFileName`, Z. 86). Genau dieses Löschen kollidiert unter Windows mit einem Auslieferungs-Stream ohne `FileShare.Delete`.

| Methode/Eigenschaft | Sichtbarkeit | Kurzbeschreibung |
|---------------------|-------------|------------------|
| `IsRunning`, `CurrentSlotId`, `LastUnexpectedExit`, `ExpectedPositionSeconds` | public | Prozesszustand (Z. 130–144) |
| `StartAsync(LiveStreamJob, CancellationToken)` | public | Startet (nach Stop) den ffmpeg-Prozess; hinter `_gate`-Semaphore, daher nie zwei Prozesse (Z. 147–188) |
| `StopAsync(CancellationToken)` | public | Stoppt den Prozess (Kill + 10 s Timeout), Z. 191–202 / `StopCoreAsync` Z. 204–231 |
| `DrainStandardErrorAsync` / `ReportProcessExit` | private | stderr-Drain + Klassifikation erwarteter/unerwarteter Exits (Z. 246–315) |
| `BuildArguments(LiveStreamJob)` → `string` | internal static | ffmpeg-Argumente inkl. `delete_segments` (Z. 372–397) |

Hilfsabstraktion `ISegmenterProcess`/`SegmenterProcess` (Z. 16–72): interne Prozess-Abstraktion für Tests.

## `ApiBaseController` (Basisklasse)

Datei: `VideoWebPlayer/Controllers/ApiBaseController.cs`

| Methode/Eigenschaft | Sichtbarkeit | Kurzbeschreibung |
|---------------------|-------------|------------------|
| `Logger`, `CurrentUser` | protected | Logger und aktueller Benutzer aus `IAuthService` (Z. 31–35) |
| `CheckLoggedIn()` | protected | Wirft `UnauthorizedAccessException`, wenn `CurrentUser` `null` — wird in `GetFile` zu 401 (Z. 40–44) |
