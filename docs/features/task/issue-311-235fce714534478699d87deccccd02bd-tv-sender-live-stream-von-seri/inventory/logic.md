# Bestandsaufnahme: Logik — Live-Stream-Eingangskette

Alle Klassen im Originalnamen. Aufrufkette: `LiveStreamWorker` → `LiveStreamCoordinator` → `LiveStreamMediaResolver` → `ILiveStreamSegmenter` (`FfmpegLiveStreamSegmenter`) mit `SegmenterProcessSupport` als Prozess-/Pump-Plumbing.

## `LiveStreamWorker`
Datei: `VideoWebPlayer/Services/LiveStreamWorker.cs` — `BackgroundService` (Singleton-Hosted-Service)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` | `protected override` | Start: `LiveStreamSegmentStore.ClearAsync` leert das Segmentverzeichnis **vor** der Startverzögerung; `LiveStreamStandbyProvider.RefreshAsync` wärmt den Standby-Clip (Fehler toleriert); danach Poll-Schleife max. alle 30 s (`PollCap`). Pro Tick: `GetTvSenderEnabledAsync` prüfen (aus → Segmenter stoppen), `IBackgroundProcessingGate`-Lease, `LiveStreamCoordinator.SynchronizeAsync`. Ergebnis `WakeAtUtc` begrenzt die Schlafzeit; `IProgramScheduleSignal` weckt bei Planänderungen vorzeitig. |
| `LogFfmpegUnavailableThrottled` | `private` | `FfmpegUnavailable`-Warnung max. alle 5 min. |

## `LiveStreamCoordinator`
Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` — scoped

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SynchronizeAsync` | `public` | Führt `SynchronizeStreamAsync` aus und fegt danach abgelaufene Segmente (`SweepExpiredSegmentsAsync` mit Admin-Retention). |
| `SynchronizeStreamAsync` | `private` | Kernlogik: ffmpeg-Pfad auflösen (sonst `FfmpegUnavailable`); `IProgramGuideService.GetNowAsync` (kein Slot → `Stopped`); **HTTP-Fallback-Entscheidung Zeile 111-112**: `useHttpInput = false` nur wenn `LastUnexpectedExit.HttpTransportFailure == true` **und** dieselbe `SlotId` — ein Pipe-Eingangsfehler setzt `HttpTransportFailure` nie, wird also wie jeder andere Exit behandelt → Neustart-Schleife mit unverändertem Eingang. `MediaResolver.ResolveAsync(MediaItemId, runDuration, useHttpInput)`; `null` → `MediaUnavailable`. Driftprüfung (`MaxDriftSeconds = 30`) → `Unchanged` oder Neustart. Job bauen (Segmentlänge aus `Setup`, Verzeichnis aus `LiveStreamSegmentStore`) → `ILiveStreamSegmenter.StartAsync`. |

Abonnierte Events: keine (zeit-/pollbasiert).
Publizierte Events: keine.

## `LiveStreamMediaResolver`
Datei: `VideoWebPlayer/Services/LiveStreamMediaResolver.cs` — scoped. **Zentrale Stelle der Eingangs-Auswahl.**

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveAsync(long mediaItemId, TimeSpan? runDuration, bool useHttpInput, CancellationToken)` | `public` | Entscheidungskette (Zeilen 78-133): 1) `MediaItem` + `MediaCollection` + `MediaSource` laden; nicht gefunden → `null` (= Sendelücke). 2) `MediaSource.SourceType == LocalDirectory` → `LocalMediaSourceReader.ResolveLocalFilePath` → `LiveStreamInput.ForFile(pfad)` (seekbar; `null` → `null`). 3) Entfernte Quelle **und** `useHttpInput`-Parameter **und** `LiveStreamOptions.UseHttpInput` → `TryCreateStreamUri` mit Token-Lebensdauer `runDuration + 15 min` (Default 45 min); URI ≠ `null` → `LiveStreamInput.ForHttp`. 4) Fallback → `LiveStreamInput.ForStream(_ => _reader.OpenFileStream(collection, fileName))` → `pipe:0`-Pump über `IMediaSourceReader`. **Es gibt keinen `sftp://`-Pfad und keine Diagnose-Ausgabe, warum der Pipe-Weg gewählt wurde** — der `null`-Fall von `TryCreateStreamUri` bleibt für den Aufrufer ununterscheidbar von „Option aus". |

## `FfmpegLiveStreamSegmenter`
Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs` — `ILiveStreamSegmenter`, Singleton

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartAsync` | `public` | Gate-serialisiert: vorherigen Lauf stoppen; `ProcessStartInfo` mit `BuildArguments(job)`, `RedirectStandardInput = job.Input.IsPipe`, stderr redirect; Prozess über injizierbare `_startProcess`-Fabrik starten; Hintergrund-Tasks: `DrainStandardErrorAsync` + (nur Pipe) `PumpInputAsync`. Loggt den ffmpeg-Aufruf auf Debug mit Token-/Credential-Maskierung (Zeilen 177-180) — **einzige Stelle, an der produktiv sichtbar ist, welcher Eingang (`pipe:0`/Datei/URL) tatsächlich gewählt wurde**. |
| `StopAsync` | `public` | `StopCoreAsync`: Kill + zeitgebundenes Warten (10 s), danach `AwaitDrainObservingFaultsAsync` auf den Pump-Task (`_pumpDrainTimeout`, Default 10 s). |
| `BuildArguments` | `internal static` | Zeilen 345-362: `-hide_banner -loglevel warning -y ` + (`-nostdin ` bei Datei/URL) + `-re -ss {pos} -i {InputSpec} ` + `-map 0:v:0 -map 0:a:0? -c:v libx264 -preset veryfast -c:a aac -b:a 128k -force_key_frames "expr:gte(t,n_forced*{seg})" ` + `-f hls -hls_time {seg} -hls_list_size 12 -hls_flags delete_segments+independent_segments+append_list+discont_start+program_date_time -hls_segment_filename "seg_{ts}_{slot}_%05d.ts" -t {remaining} "live.m3u8"`. **Keine `-analyzeduration`/`-probesize`-Optionen.** `-ss` steht vor `-i` (Input-Seek) — auf nicht seekbarem `pipe:0` verwirft ffmpeg Eingang bis zur Position; mit `-re` geschieht das in Echtzeit. |
| `DrainStandardErrorAsync` | `private` | stderr-Endlosdrain (`SegmenterProcessSupport`), danach `ReportProcessExit`. |
| `ReportProcessExit` | `private` | Klassifiziert Prozessende: geplanter Stop (`_process`-Referenz gewechselt) oder planmäßiges Ende (innerhalb Toleranz `max(10, 2·seg)` s vor Slotende) → Debug. Sonst unerwarteter Exit: `httpTransportFailure = job.Input.HttpUrl is not null && FfmpegErrorMarkers.ContainsHttpTransportError(tail)` → `LiveStreamExitInfo` in `_lastUnexpectedExit` + Warnlog mit maskiertem stderr-Tail (8 KiB). |
| `PumpInputAsync` | `private` | Delegiert an `SegmenterProcessSupport.PumpInputAsync`; `SourceUnavailable` → Warnung. Pump-Fehler beeinflussen die Exit-Klassifikation **nicht**. |
| `ExpectedPositionSeconds` / `IsRunning` / `CurrentSlotId` / `LastUnexpectedExit` | `public` | Zustand für Drift-Erkennung bzw. Coordinator-Fallback. |

Enthaltene interne Typen: `ISegmenterProcess` (Prozess-Abstraktion, siehe `interfaces.md`), `SegmenterProcess` (Default-Implementierung über `System.Diagnostics.Process`).

## `SegmenterProcessSupport`
Datei: `VideoWebPlayer/Services/SegmenterProcessSupport.cs` — `internal static`, gemeinsam von `FfmpegLiveStreamSegmenter` und `LiveStreamStandbySegmenter` genutzt.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartRealProcess` | `internal static` | `Process.Start` + `SegmenterProcess`-Wrap; Startfehler geloggt, `null` zurück. |
| `InputSpec` | `internal static` | `-i`-Argument: quotierter `FilePath`, quotierte `HttpUrl`, sonst `pipe:0`. |
| `NoStdinArg` | `internal static` | `-nostdin ` für Datei/URL, leer für Pipe. |
| `TranscodeArgs` | `internal static` | Gemeinsamer Transcode-Block (`-map 0:v:0 -map 0:a:0? -c:v libx264 -preset veryfast -c:a aac -b:a 128k -force_key_frames …`). |
| `DrainStandardErrorAsync` | `internal static` | Liest stderr bis EOF, hält die letzten 8192 Zeichen (`StderrTailCapacityChars`) als Tail. |
| `PumpInputAsync` | `internal static` | Zeilen 128-161: `input.OpenStreamAsync()` → `source.CopyToAsync(process.StandardInput)`; stdin wird im `finally` geschlossen. `IOException`/`ObjectDisposedException`/`OperationCanceledException` = erwarteter Pipe-Bruch → `Completed` (Debug-Log); andere Ausnahmen → `Failed` (Warnlog); `null`-Quelle → `SourceUnavailable`. **`CopyToAsync` liefert keine Byte-Zählung; die gepumpte Datenmenge wird weder gezählt noch mit der erwarteten Dateigröße verglichen** — „unvollständig geliefert" und „moov-Atom am Ende" sind im Log nicht unterscheidbar. Kein CancellationToken im Pump. |
| `AwaitDrainObservingFaultsAsync` (2 Überladungen) | `internal static` | Zeitgebundenes Einholen eines Neben-Tasks (Pump/stderr); Timeout → Warnlog + verwaisen lassen, Fault → Warnlog; Fehler werden nie weitergeworfen. |
| `PumpDrainTimeout` | `internal static readonly` | 10 s Grace für den Pump beim Teardown. |

Zugehöriges Enum `SegmenterPumpOutcome` (siehe `enums.md`).

## `InternalMediaStreamTokenService`
Datei: `VideoWebPlayer/Services/InternalMediaStreamTokenService.cs` — `IInternalMediaStreamTokenService`, Singleton

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TryCreateProbeUri` | `public` | Token-URI für ffprobe; `null` wenn `HttpProbeEnabled == false` oder keine Basis-URL. |
| `TryCreateStreamUri` | `public` | Token-URI für den Segmenter (Mindestlebensdauer 60 s); **`null`, wenn `ResolveBaseUrl()` keine `http`-Adresse liefert** — reiner HTTPS-Betrieb/Reverse-Proxy ohne http-Bindung fällt so still auf `pipe:0` zurück. |
| `ResolveMediaItemId` | `public` | Token → `MediaItemId`; unbekannt/abgelaufen → `null`. |
| `ResolveBaseUrl` | `private` | Einmalige Auflösung: `MediaProbe:HttpProbeBaseUrl` (wenn gültige http(s)-URL) gewinnt, sonst erste gebundene `http://`-Adresse aus `IServerAddressesFeature` (Wildcard/`localhost` → `127.0.0.1`, Port erhalten). **Nur `http`-Adressen akzeptiert** (`TryNormalizeServerAddress` lehnt https ab). Fehlschlag wird nicht dauerhaft gecacht (Start-Up-Race), aber genau **einmal** gewarnt (Zeile 190-192; Warnungstext spricht nur von Probes, nicht vom Segmenter). |
| `TryNormalizeServerAddress` | `internal static` | `http://host:port`-Normalisierung (IPv6-Klammern, Wildcards). |
| `MaskStreamTokenInText` | `internal static` | Regex-Maskierung des Tokens (`/api/internal/media-stream/{hex}`) in Logtexten; vom Segmenter und Standby-Segmenter genutzt. |

Tokens: 256-bit zufällig, in `ConcurrentDictionary` im Speicher, an genau ein `MediaItemId` gebunden, Lebensdauer je nach Aufruf.

## `LiveStreamStandbySegmenter`
Datei: `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs` — Singleton

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TranscodeAsync` | `internal` | **Einmaliger Offline-Transcode** des Standby-Inputs nach `standby_%03d.ts` in `Data/LiveStreamStandby` (`-f segment`, kein `-re`, kein `-ss`, `-t 60` Cap, `TranscodeTimeout` 120 s). Unterstützt dieselben drei `LiveStreamInput`-Arten inkl. Pipe-Pump. Alt-Segmente werden vorher gelöscht, nicht löschbare per Zeitstempel aus dem Ergebnis ausgeschlossen. Exit ≠ 0 / Timeout / keine Segmente → `null`. Danach ffprobe-Dauer je Segment (Fallback 4 s nominal). |
| `BuildArguments` | `internal static` | `-hide_banner -loglevel warning -y {-nostdin }-i {InputSpec} {TranscodeArgs} -f segment -segment_time 4 -reset_timestamps 1 -t 60 "standby_%03d.ts"`. |

## `LiveStreamStandbyProvider`
Datei: `VideoWebPlayer/Services/LiveStreamStandbyProvider.cs` — Singleton

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `BuildPlaylist` | `public` | Rotierende Standby-`live.m3u8` (Fenster 3 Segmente); löst bei abweichender konfigurierter MediaItem-Id dedupliziert die Hintergrund-Vorbereitung aus. |
| `TryGetSegment` | `public` | Standby-Segment-Bytes aus dem Speicher. |
| `NoteStandbyPlaylistServed` / `ConsumePendingStandbyTransition` | `public` | Merkt/verbraucht den Standby→Echt-Übergang für `#EXT-X-DISCONTINUITY`. |
| `RefreshAsync` | `public` | Worker-Start: konfiguriertes Standby-MediaItem vorbereiten. |
| `PrepareAsync` | `internal` | Gate-dedupliziert: `null` → eingebetteter Default (Funki-Clip, 3 Segmente); sonst Auflösung **über `LiveStreamMediaResolver.ResolveAsync(mediaItemId, null, useHttpInput: true)`** (Zeile 311) → Disk-Cache-Prüfung (`standby-source.json`, für Datei-Quellen inkl. Änderungsstempel) → `TranscodeAsync`. Fehlschlag → `_failedMediaItemId` gegen Retry-Sturm. |
| `ResolveMediaInputDefaultAsync` | `private` | Scope-Auflösung + Resolver-Aufruf (s. o.). |

## `InternalMediaStreamController`
Datei: `VideoWebPlayer/Controllers/InternalMediaStreamController.cs` — `GET api/internal/media-stream/{token}`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Stream` | `public` | Zulieferer des HTTP-Range-Eingangs: `TvSenderEnabled` sonst 404; Token → `MediaItem` (mit Collection+Source); `IMediaSourceReader.OpenFileStream` → `File(stream, contentType, enableRangeProcessing: true)` — ffmpeg seekt so per HTTP-Range durch denselben Reader-Pfad wie die Wiedergabe. Fehler → 404/500. |

## `LiveStreamController`
Datei: `VideoWebPlayer/Controllers/LiveStreamController.cs` — `GET api/live-stream/{fileName}` (Bearer + `access_token`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetFile` | `public` | Liefert `live.m3u8`/`.ts` aus dem Segmentverzeichnis (`LiveStreamSegmentStore.TryResolveFileAsync`, strenges Dateinamen-Whitelist); Playlist wird um `access_token` je Segment-URI umgeschrieben; Playlist ohne Segmente → Standby-Fallback; Standby-Playlist/Segmente aus dem Speicher. `Cache-Control: no-store` überall. |

## `LiveStreamSegmentStore`
Datei: `VideoWebPlayer/Services/LiveStreamSegmentStore.cs` — scoped

`GetDirectoryAsync` (Setup-Pfad, Default `Data/LiveStream`), `TryResolveFileAsync` (Containment), `SweepExpiredSegmentsAsync` (Retention, nur `seg_*.ts`), `ClearAsync` (Worker-Start).

## `SftpMediaSourceReader` / `SftpStreamWrapper` / `LocalMediaSourceReader` / `MediaSourceReaderDispatcher`
Dateien: `VideoWebPlayer/Services/{SftpMediaSourceReader,SftpStreamWrapper,LocalMediaSourceReader,MediaSourceReaderDispatcher}.cs`

- `MediaSourceReaderDispatcher` (`IMediaSourceReader`, scoped): wählt pro Aufruf `LocalMediaSourceReader` bei `SourceType == LocalDirectory`, sonst `SftpMediaSourceReader` (`GetReader`, Zeile 63-64).
- `SftpMediaSourceReader.OpenFileStream` (Zeilen 272-302): neuer `SftpClient` je Aufruf, `client.OpenRead(fullPath)` → `SftpStreamWrapper` (gibt `null` bei fehlender Datei/Verzeichnis statt Ausnahme). Der Stream ist prinzipiell seekbar (SSH.NET `SftpFileStream`), wird über die Pipe aber nur sequenziell gelesen; `Dispose` räumt Stream **und** Client ab. Kein Byte-Fortschritt, kein Timeout.
- `LocalMediaSourceReader.OpenFileStream`/`ResolveLocalFilePath`: `FileStream` lesend mit `FileShare.Read`; `ResolveFilePath` erzwingt Root-Containment und löst Segmente case-insensitiv auf, ReparsePoints/Nicht-Dateien → `null`.

## `FfmpegErrorMarkers`
Datei: `VideoWebPlayer/Services/FfmpegErrorMarkers.cs` — `internal static`

`ContainsHttpTransportError(stderr)`: Substring-Match auf `HTTP error`, `Server returned`, `Connection refused`, `Connection timed out`, `No route to host`, `Failed to connect` (OrdinalIgnoreCase). Darf **nur** auf `http(s)`-Eingänge angewendet werden (Dokumentationshinweis in der Datei); wird vom Segmenter nur bei `HttpUrl`-Jobs ausgewertet.

## `MediaDurationProbeService` + `FfprobeMediaDurationProbe` + `SftpProbeUriBuilder`
Dateien: `VideoWebPlayer/Services/{MediaDurationProbeService,FfprobeMediaDurationProbe,SftpProbeUriBuilder}.cs`

Nebenpfad (Laufzeitermittlung, nicht die Segmentierung selbst), aber fachlich direkt relevant: `ProbeRemoteItemAsync` (Zeilen ~311-341) hat für entfernte Medien eine **dreistufige Fallback-Kette, die der Segmenter nicht hat**: 1) HTTP-Loopback-URI (`TryCreateProbeUri`) → 2) **`sftp://user:pass@host/path`-URI** (`SftpProbeUriBuilder.TryBuildUri`, seekbar via libssh in ffprobe — vorausgesetzt Credentials + absoluter Remote-Pfad) → 3) `pipe:0`-Pump. Der Segmenter (`LiveStreamMediaResolver`) kennt dagegen nur HTTP → Pipe; `sftp://` wird ffmpeg nie angeboten. `FfprobeMediaDurationProbe` setzt ebenfalls **keine** `-analyzeduration`/`-probesize`-Optionen (`BuildArguments`: nur `-v error -show_entries format=duration …`); unterscheidet aber permanent vs. transient anhand stderr-Marker (`moov atom not found`, `Invalid data found…` — nur bei seekbaren Eingängen ausgewertet) und merkt sich nicht unterstützte URI-Schemata.

## DI-Registrierungen
Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (Zeilen 245-339)

- Singletons: `IFfmpegLocator`, `IFfmpegToolDownloadService`, `ILiveStreamSegmenter` → `FfmpegLiveStreamSegmenter`, `LiveStreamStandbySegmenter`, `LiveStreamStandbyProvider`, `IInternalMediaStreamTokenService` → `InternalMediaStreamTokenService`.
- Scoped: `LiveStreamMediaResolver`, `LiveStreamSegmentStore`, `LiveStreamCoordinator`, `SftpMediaSourceReader`, `LocalMediaSourceReader`, `IMediaSourceReader` → `MediaSourceReaderDispatcher`.
- `LiveStreamWorker` als `AddHostedService` (Zeile 339).
- `LiveStreamOptions` aus Section `LiveStream` (Zeile 245).
