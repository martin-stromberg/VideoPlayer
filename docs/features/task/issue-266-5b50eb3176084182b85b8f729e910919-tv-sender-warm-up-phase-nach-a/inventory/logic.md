# Logik — Bestandsaufnahme

## LiveStreamController

Datei: `VideoWebPlayer/Controllers/LiveStreamController.cs`

`[ApiController]`, `[Route("api/live-stream")]`, `[BearerTokenCheck]` (JWT via `Authorization`-Header oder `access_token`-Query, siehe `BearerTokenCheckAttribute`). Erbt `CheckLoggedIn()`/`CurrentUser` von `ApiBaseController`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `GetFile(string fileName)` | `public` (`[HttpGet("{fileName}")]`) | Liefert `live.m3u8` oder `seg_*.ts` aus. Ablauf: `CheckLoggedIn()` → `GetTvSenderEnabledAsync` (sonst 404) → `IsTvSenderAccessGrantedAsync` (sonst 403) → `TryResolveFileAsync` (sonst 404). Bei `.m3u8`: `Cache-Control: no-store` + Playlist-Rewrite mit `access_token` in den Segment-URIs (`ContentResult`, Content-Type `application/vnd.apple.mpegurl`). Bei `.ts`/sonstigem: `FileStreamResult` (`video/mp2t` bzw. `application/octet-stream`) über `FileStream` mit `FileShare.ReadWrite \| FileShare.Delete`. **Kein `Cache-Control` auf Segment-Antworten.** 404 auch im Zustand „kein Stream produziert" (fehlende `live.m3u8`). |
| `RewritePlaylistAsync(string path, CancellationToken)` | `private` | Liest die Playlist mit `FileShare.ReadWrite \| FileShare.Delete` und hängt `?access_token=...` (URL-escaped) an jede Nicht-`#`-Zeile (Segment-URI). |

Konstanten: `M3u8ContentType` (`internal`), `SegmentContentType` (`internal`).

## LiveStreamSegmentStore

Datei: `VideoWebPlayer/Services/LiveStreamSegmentStore.cs`

Scoped-Service; Besitzer des Segment-Verzeichnisses.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `GetDirectoryAsync(CancellationToken)` | `public` | Effektives Verzeichnis: admin-konfiguriert (absolut oder relativ zum Content-Root) oder `Data/LiveStream`; legt es an und gibt den absoluten Pfad zurück. |
| `TryResolveFileAsync(string, CancellationToken)` | `public` | Whitelist-Prüfung über `SafeFileName` (`^[A-Za-z0-9][A-Za-z0-9._-]*$`), Containment-Prüfung gegen das Verzeichnis, `File.Exists` → Pfad oder `null`. |
| `SweepExpiredSegmentsAsync(DateTimeOffset, TimeSpan, CancellationToken)` | `public` | Löscht `seg_*.ts` älter als `utcNow - retention`; Playlist und andere Dateien bleiben unberührt; gibt Anzahl gelöschter Dateien zurück. |
| `ClearAsync(CancellationToken)` | `public` | Leert das gesamte Segment-Verzeichnis; wird einmal beim Worker-Start aufgerufen (derzeit **nach** `_initialDelay`). |

Konstanten: `SafeFileName` (`internal static Regex`), `SegmentFilePrefix` = `"seg_"`, `SegmentFileExtension` = `".ts"` (`internal`).

## LiveStreamWorker

Datei: `VideoWebPlayer/Services/LiveStreamWorker.cs`

`BackgroundService` (registriert via `AddHostedService`, nicht in Umgebung `Testing`).

| Methode / Mitglied | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `PollCap` | `internal static readonly TimeSpan` | Maximale Zeit zwischen zwei Sync-Ticks: 30 s. |
| `_initialDelay` | `private readonly` | Startverzögerung, Standard **10 s** (Konstruktor-Parameter `initialDelay`, injizierbar für Tests). |
| `ExecuteAsync(CancellationToken)` | `protected override` | Start-Sequenz: erst `Task.Delay(_initialDelay)`, **dann** `LiveStreamSegmentStore.ClearAsync` in einem Scope, dann Poll-Schleife. Pro Tick: `GetTvSenderEnabledAsync` prüfen (aus → laufenden `ILiveStreamSegmenter` stoppen), sonst `IBackgroundProcessingGate.EnterOperationAsync("Live-Stream")` + `LiveStreamCoordinator.SynchronizeAsync`; `WakeAtUtc` verkürzt das Delay; Aufwachen zusätzlich über `IProgramScheduleSignal.WaitAsync`. |
| `LogFfmpegUnavailableThrottled(string?)` | `private` | Warnung „Der Live-Stream pausiert, bis ffmpeg verfuegbar ist." höchstens alle 5 min. |

## LiveStreamCoordinator

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs`

Scoped-Service; rein zeitbasierte Synchronisation ffmpeg ↔ Sendeplan, kein persistierter Stream-Zustand.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `SynchronizeAsync(DateTimeOffset, CancellationToken)` | `public` | Ein Tick: `SynchronizeStreamAsync` + Retention-Sweep (`GetLiveStreamSegmentRetentionAsync` → `SweepExpiredSegmentsAsync`). |
| `SynchronizeStreamAsync(DateTimeOffset, CancellationToken)` | `private` | Entscheidungslogik: ffmpeg fehlt → `FfmpegUnavailable` + Stop; `guide.Now` `null` → `Stopped` + `WakeAtUtc` = nächster Slot; Medium nicht auflösbar → `MediaUnavailable` + Stop; läuft bereits und Drift ≤ `MaxDriftSeconds` (30 s) → `Unchanged`; sonst `LiveStreamJob` bauen (Segmentlänge aus `GetLiveStreamSegmentLengthAsync`, Verzeichnis aus `GetDirectoryAsync`) und `StartAsync` → `Started`. Nach `LastUnexpectedExit` mit `HttpTransportFailure` am selben Slot wird der Pipe-Eingang gewählt. |

Konstante: `MaxDriftSeconds` = 30 (`public`).

## FfmpegLiveStreamSegmenter / ISegmenterProcess / SegmenterProcess

Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`

Singleton (`ILiveStreamSegmenter` → `FfmpegLiveStreamSegmenter`). Hält genau einen ffmpeg-Prozess.

| Methode / Mitglied | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `PlaylistFileName` | `public const string` | `"live.m3u8"`. |
| `PlaylistWindowSegmentCount` | `internal const int` | 12 Segmente im Rolling-Window (`-hls_list_size`). |
| `IsRunning` | `public` | `true`, wenn der Prozess läuft (`HasExited == false`). |
| `CurrentSlotId` | `public` | Slot-ID des laufenden Jobs oder `null`. |
| `ExpectedPositionSeconds` | `public` | Erwartete Wiedergabeposition (`PositionSeconds + Laufzeit seit Start`), `null` ohne Job. |
| `LastUnexpectedExit` | `public` | `LiveStreamExitInfo?` des letzten vorzeitig von selbst beendeten Laufs; wird bei `StartAsync` zurückgesetzt. |
| `StartAsync(LiveStreamJob, CancellationToken)` | `public` | Unter `_gate` (SemaphoreSlim): erst `StopCoreAsync` (vorherigen Job beenden), dann `ProcessStartInfo` via `BuildArguments`, Prozessstart über injizierbare `_startProcess`-Fabrik; startet `DrainStandardErrorAsync` und bei Pipe-Eingang `PumpInputAsync` als Hintergrund-Tasks. |
| `StopAsync(CancellationToken)` | `public` | Unter `_gate`: `StopCoreAsync` — killt den Prozess (ganzer Baum), wartet bis `StopTimeout` (10 s), verwirft Zustand. |
| `BuildArguments(LiveStreamJob)` | `internal static` | ffmpeg-Argumente: `-re`, `-ss`, `-nostdin` bei Datei-/HTTP-Eingang, `-map 0:v:0 -map 0:a:0?`, `libx264`/`aac`, `-force_key_frames expr:gte(t,n_forced*N)`, `-f hls -hls_time N -hls_list_size 12`, `-hls_flags delete_segments+independent_segments+append_list+discont_start+program_date_time`, Segment-Muster `seg_{yyyyMMddHHmmss}_{slotId}_%05d.ts`, `-t` Restlaufzeit, Ziel `live.m3u8`. |
| `DrainStandardErrorAsync` / `ReportProcessExit` / `ReadExitCode` / `PumpInputAsync` / `StopCoreAsync` / `StartRealProcess` | `private` / `private static` | stderr-Drain (Tail-Puffer 8192 Zeichen), Exit-Klassifikation (Kill/geplantes Ende vs. unerwartet; HTTP-Transportfehler über `FfmpegErrorMarkers.ContainsHttpTransportError`; Token-/Credential-Maskierung im Log), stdin-Pumpe, Stop-Kern mit `StopTimeout`, echter `Process.Start`-Wrapper. |

`ISegmenterProcess` (internal Interface, Zeile 16): `HasExited`, `ExitCode`, `StandardInput`, `StandardError`, `WaitForExitAsync`, `Kill` — Abstraktion für Fake-Prozesse in Tests. `SegmenterProcess` (internal): Standard-Implementierung über `System.Diagnostics.Process`.

## LiveStreamMediaResolver

Datei: `VideoWebPlayer/Services/LiveStreamMediaResolver.cs`

Scoped-Service; löst `ProgramSlot.MediaItemId` zum ffmpeg-Eingang auf.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `ResolveAsync(long mediaItemId, TimeSpan? runDuration, bool useHttpInput, CancellationToken)` | `public` | `MediaItem` + `MediaCollection` + `MediaSource` laden; `LocalDirectory` → `LocalMediaSourceReader.ResolveLocalFilePath` → `ForFile`; remote → bei `useHttpInput` && `LiveStreamOptions.UseHttpInput` → `TryCreateStreamUri` (Token-Lifetime = Restlaufzeit + 15 min Margin, Default 45 min) → `ForHttp`; sonst `ForStream` (Pipe). `null` bei nicht auflösbarem Medium. |

## ProgramSettingsService (Stream-relevante Ausschnitte)

Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `GetTvSenderEnabledAsync` | `public` | Feature-Flag aus `Setup.TvSenderEnabled`. |
| `UpdateTvSenderEnabledAsync` | `public` | Persistiert das Feature-Flag. |
| `IsTvSenderAccessGrantedAsync(userId, isAdmin, ct)` | `public` | Kombi-Prüfung: Flag an **und** (Admin oder `TvSenderUserAccess`-Zeile). |
| `GetTvSenderAllowedUserIdsAsync` / `UpdateTvSenderAllowedUserIdsAsync` | `public` | Allowlist lesen/atomar ersetzen. |
| `GetLiveStreamSegmentLengthAsync` | `public` | `TimeSpan`; Standard 6 s bei ≤ 0. |
| `GetLiveStreamSegmentDirectoryAsync` | `public` | Konfiguriertes Verzeichnis oder `null`. |
| `GetLiveStreamSegmentRetentionAsync` | `public` | `TimeSpan`; Standard 30 min bei ≤ 0. |
| `UpdateLiveStreamSettingsAsync(segmentLengthSeconds, segmentDirectory, retentionMinutes, ct)` | `public` | Persistiert die drei Live-Stream-Einstellungen; Validierung: Länge 1–300, Retention ≥ 1, Pfad-Sonde `Path.GetFullPath`. |

## LiveTv.razor (Seite `/live-tv`)

Datei: `VideoWebPlayer/Components/Pages/LiveTv.razor`

| Mitglied | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `StreamUrl` | `private` | `/api/live-stream/live.m3u8?access_token={Token}` (aus `Client.AuthorizationToken`). |
| `OnInitializedAsync` | `protected override` | Auth → Token → `GetTvSenderEnabledAsync` → `IsTvSenderAccessGrantedAsync` → `RefreshGuideAsync(initial)`. |
| `OnAfterRenderAsync` | `protected override` | Poll-Loop (15 s) einmalig starten; Player-Attach/Detach über `liveTv.attach`/`liveTv.detach` bei Slot-Wechsel. |
| `RefreshGuideAsync(bool)` | `private` | Holt `RequestProgramGuideNowAsync` und `RequestProgramGuideUpcomingAsync` unabhängig voneinander; Initial-Fehler → `#live-tv-error`, spätere Fehler → stiller Retry. |
| `NotifyStreamPlaying` / `NotifyStreamError(string kind)` / `NotifyStreamUnsupported` | `public [JSInvokable]` | Callbacks aus `liveTv.js`: setzen `streamError`/`unsupportedBrowser` → `#live-tv-stream-error` (`LiveTvErrorMessages.StreamUnavailable`) bzw. `#live-tv-unsupported`. |
| `PollLoopAsync` | `private` | `PeriodicTimer` (15 s, `TimeProvider`) → `RefreshGuideAsync` + `StateHasChanged`. |

Anzeigezustände: `isLoading` → „Lade Daten..." (`#live-tv-loading`); `!tvSenderEnabled` → `#live-tv-disabled`; `!tvSenderAccessGranted` → `#live-tv-no-access`; `loadError` → `#live-tv-error`; `guide.Now == null` → `#live-tv-no-program` (`NoProgramRunning` + nächste Sendung); `streamError` → `#live-tv-stream-error` (`StreamUnavailable`).

## liveTv.js

Datei: `VideoWebPlayer/wwwroot/js/liveTv.js`

| Funktion | Zweck |
|---|---|
| `attach(videoEl, streamUrl, hlsScriptUrl, dotNetRef)` | Natives HLS (`canPlayType`, Safari) oder Lazy-Load von `hls.min.js` + hls.js über MSE. Zustand pro `<video>` in `WeakMap`. |
| `detach(videoEl)` | Aufräumen: Retry-Timer, Event-Listener, `hls.destroy()`, `src` entfernen. |
| `RETRY_MS` | 5000 ms — Selbst-Re-Attach nach fatalen Fehlern. |
| `attachHls` (intern) | hls.js-Instanz; `ERROR` fatal → `NotifyStreamError(type)` + differenziertes Recovery (`recoverMediaError()` bei `MEDIA_ERROR`, `startLoad()` bei `NETWORK_ERROR`, Neuaufbau sonst); `FRAG_BUFFERED` → `NotifyStreamPlaying`. |
| Fehlerpfad natives HLS | `error`-Event am `<video>` → `NotifyStreamError('native')` + Retry. |

→ Ein 404 auf `live.m3u8` löst auf hls.js-Seite einen `NETWORK_ERROR` bzw. nativ einen `error`-Event aus → `NotifyStreamError` → `#live-tv-stream-error` „Der Live-Stream ist momentan nicht verfügbar." mit 5-s-Retry.

## LiveTvErrorMessages

Datei: `VideoWebPlayer/ViewModels/LiveTvErrorMessages.cs` (`internal static`)

Konstanten: `NoProgramRunning` („Aktuell läuft keine Sendung."), `StreamUnavailable` („Der Live-Stream ist momentan nicht verfügbar. Es wird automatisch erneut versucht."), `UnsupportedBrowser`, `LoadFailed`, `Forbidden` („Sie haben keinen Zugriff auf den TV-Sender."). Methode `Describe(Exception)` → `Forbidden` bei `HttpRequestException` mit 403, sonst `null`. **Kein Text „nicht aktiv" / kein Standby-Text vorhanden.**

## Service-Registrierung

Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (Zeilen 245–252, 334–340)

- `ILiveStreamSegmenter` → `FfmpegLiveStreamSegmenter` (Singleton, Z. 249)
- `LiveStreamMediaResolver`, `LiveStreamSegmentStore`, `LiveStreamCoordinator` (Scoped, Z. 250–252)
- `LiveStreamOptions` aus Sektion `LiveStream` (Z. 245)
- `LiveStreamWorker` als Hosted Service — **nicht** in Umgebung `Testing` (Z. 334–340)
- `IFfmpegLocator` → `FfmpegLocator`, `IProgramScheduleSignal` → `ProgramScheduleSignal` (Singletons, Z. 246, 266–267)
- `IBackgroundProcessingGate` → `BackgroundProcessingGate` (Singleton, Z. 289)

## Backup-Registrierung

Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

`Setup.LiveStreamSegmentLengthSeconds`/-`Directory`/-`RetentionMinutes` und `Setup.TvSenderEnabled` stehen in `OptionalRestoreColumns` (Z. 94–96, 99); Int-Defaults 6/30 in `OptionalRestoreIntDefaults` (Z. 151–152), `TvSenderEnabled` Bool-Default `false` (Z. 133).

## Verwandte, nicht zentrale Artefakte

- `BearerTokenCheckAttribute` (`VideoWebPlayer/Controllers/Attributes/BearerTokenCheckAttribute.cs`): JWT-Validierung, `access_token`-Query-Fallback (Z. 31).
- `InternalMediaStreamController` (`api/internal/media-stream/{token}`): range-fähiger interner Stream-Endpunkt, den ffmpeg als HTTP-Eingang liest.
- `FfmpegErrorMarkers` (`internal static`, `VideoWebPlayer/Services/FfmpegErrorMarkers.cs`): stderr-Marker für HTTP-Transportfehler.
- `ProgramScheduleSignal` (`VideoWebPlayer/Services/ProgramScheduleSignal.cs`): Singleton-Signal zum Aufwecken des Workers.
- `TVSenderAdmin.razor` (`VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`, Z. 522–541, 937–939, 1400): Admin-UI für Segmentlänge/-verzeichnis/-aufbewahrung über `UpdateLiveStreamSettingsAsync`.

## Events

Keine .NET-Events in den betrachteten Klassen. Relevante Signal-/Callback-Mechanismen:

- `IProgramScheduleSignal.NotifyScheduleChanged`/`WaitAsync` — publiziert von Regel-/Medien-Änderungen, abonniert vom `LiveStreamWorker` (Aufwachen vor `PollCap`).
- JS→.NET: `liveTv.js` ruft `LiveTv.NotifyStreamPlaying`/`NotifyStreamError`/`NotifyStreamUnsupported` (JSInvokable).
- Prozess-Ebene: `FfmpegLiveStreamSegmenter` beobachtet das Prozessende indirekt über stderr-EOF (`DrainStandardErrorAsync` → `ReportProcessExit` → `LastUnexpectedExit`).

**Für die Anforderung relevant:** Es existiert kein Mechanismus, der „erste echte Segmentierung steht" signalisiert — keine Playlist-Beobachtung, kein Event, kein Flag; die einzigen Zustandsauskunftswege sind `ILiveStreamSegmenter.IsRunning`/`CurrentSlotId` (Prozessebene, nicht Dateiebene) und das Vorhandensein von `live.m3u8` im Verzeichnis (implizit über `TryResolveFileAsync`).
