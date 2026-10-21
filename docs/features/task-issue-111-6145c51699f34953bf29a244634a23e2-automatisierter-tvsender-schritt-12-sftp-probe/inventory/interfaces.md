# Bestandsaufnahme — Interfaces

## `IMediaDurationProbe`
Datei: `VideoWebPlayer/Services/IMediaDurationProbe.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ProbeFileAsync` | `string filePath`, `CancellationToken` | `Task<MediaDurationProbeResult>` | Lokaler Dateipfad-Probe |
| `ProbeStreamAsync` | `Stream input`, `CancellationToken` | `Task<MediaDurationProbeResult>` | Stream-Probe via `pipe:0` (SFTP-Fallback) |

Implementierung: `FfprobeMediaDurationProbe` (Singleton, `AddSingleton<IMediaDurationProbe, FfprobeMediaDurationProbe>`).
Bekannte weitere Implementierung: `FakeDurationProbe` in `MediaDurationProbeServiceTests` — Signaturänderung betrifft diesen Fake.

## `IMediaSourceReader`
Datei: `VideoWebPlayer/Services/IMediaSourceReader.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReadRootDirectory` | `MediaSource` | `IEnumerable<MediaEntry>` | Root-Collection |
| `ReadDirectoryEntries` | `MediaCollection` | `IEnumerable<MediaEntry>` | Verzeichnisinhalt |
| `FileExistsAsync` | `MediaCollection`, `string fileName` | `Task<bool>` | Existenzprüfung |
| `ReadFileAsync` | `MediaCollection`, `string fileName` | `Task<string?>` | Dateiinhalt als String |
| `ReadFileStreamAsync` | `MediaCollection`, `string fileName` | `Task<Stream?>` | Datei als MemoryStream |
| `OpenFileStream` | `MediaCollection`, `string fileName` | `Stream?` | Seekbarer SFTP-Stream (live lesend) bzw. lokaler Dateistream |

Implementierungen: `SftpMediaSourceReader`, `LocalMediaSourceReader`, `MediaSourceReaderDispatcher` (Dispatch nach `SourceType`).

## `MediaDurationProbeResult`
Datei: `VideoWebPlayer/Services/MediaDurationProbeResult.cs` — `public sealed record MediaDurationProbeResult(TimeSpan? Duration, bool Transient)` mit statischen `TransientFailure`/`PermanentFailure` und Fabrik `Success(TimeSpan)`.

## `MediaProbeOptions`
Datei: `VideoWebPlayer/Configuration/MediaProbeOptions.cs` — `FfprobePath`, `FfmpegPath`, `IntervalSeconds`, `BatchSize`, `TimeoutSeconds` (120), `StreamTimeoutSeconds` (600), `RetryDelaySeconds` (300), `MaxAttempts` (10), `ParallelProbes` (4); jeweils `Effective*`-Fallback. `appsettings.json` Sektion `MediaProbe` spiegelt die Schlüssel.
