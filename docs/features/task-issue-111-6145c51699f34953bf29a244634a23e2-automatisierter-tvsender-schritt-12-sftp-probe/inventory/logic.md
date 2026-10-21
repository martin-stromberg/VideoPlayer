# Bestandsaufnahme — Logik

## `FfprobeMediaDurationProbe`
Datei: `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProbeFileAsync(string, CancellationToken)` | public | Probt über Dateipfad-Argument `"path"`, Timeout `EffectiveTimeoutSeconds` (120 s). |
| `ProbeStreamAsync(Stream, CancellationToken)` | public | Probt über `pipe:0` + stdin-Pump, Timeout `EffectiveStreamTimeoutSeconds` (600 s). |
| `ProbeAsync(inputArgument, stdin, timeout, ct)` | private | Gemeinsamer Ablauf: `_locator.Resolve()` → `BuildArguments` → `_executeAsync` → Ergebnis-Mapping. |
| `BuildArguments(inputArgument)` | internal static | `-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 {input}` |
| `TryParseDurationOutput(output)` | internal static | Parst Sekunden-Dezimalzahl; `null` bei leer/`N/A`/nicht-numerisch/<=0. |
| `Truncate` | private static | stderr auf 200 Zeichen für Warn-Log gekürzt. |
| `ExecuteProcessAsync` | private static | Prozessstart, stdin-Pump, Timeout via `CancelAfter`, `Kill(entireProcessTree)` bei Timeout. |

Testseam: interner Konstruktor injiziert `Func<string, string, Stream?, TimeSpan, CancellationToken, Task<FfprobeInvocationResult>>` (`_executeAsync`).

Fehlerklassifikation heute (alle Eingabearten gleich):
- `_locator.Resolve() == null` → `TransientFailure` + Warn-Log
- Ausnahme in `_executeAsync` → `TransientFailure` + Warn-Log (loggt `inputArgument` als `{Input}`)
- `result.TimedOut` → `TransientFailure` + Warn-Log
- `result.ExitCode != 0` → `TransientFailure` + Warn-Log inkl. gekürztem stderr (`Truncate(result.StandardError)`)
- Exit 0, keine parsebare Dauer → `PermanentFailure`

**Logging-Fundstelle für Maskierung:** `inputArgument` wird in 3 Warn-Logs als `{Input}` ausgegeben; stderr wird in der Exit≠0-Meldung mitgeloggt — ffprobe echot die Input-URL in stderr (z. B. `sftp://user:pass@host/pfad: moov atom not found`), beides muss maskiert werden.

## `MediaDurationProbeService`
Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProbePendingAsync(batchSize, ct)` | public | Wählt Items (`Duration == null`, Retry-Gate über `DurationProbedAt`/`DurationProbeAttempts`/`EffectiveMaxAttempts`/`EffectiveRetryDelaySeconds`, `VideoExtensionPredicate`), ruft `RunProbesAsync`, wendet `ApplyOutcome` an, feuert `IProgramScheduleSignal`. |
| `GetProgressAsync(userId, recentCount, ct)` | public | Admin-only Fortschrittsabfrage (Schritt 11). |
| `RunProbesAsync` | private | `SemaphoreSlim(EffectiveParallelProbes)` + `Task.WhenAll`; keine DB-Nutzung in den Tasks. |
| `ProbeItemAsync` | private | **Kerstelle für diese Anforderung:** `LocalDirectory` → `_probe.ProbeFileAsync(item.Path)`; sonst `_reader.OpenFileStream(item.MediaCollection, item.Name)` → `_probe.ProbeStreamAsync(stream, ct)`; `null`-Stream → `TransientFailure`. |
| `ApplyOutcome` | private | Success → `Duration` setzen, Attempts=0; Transient → Attempts+1; Permanent → Attempts=max. |
| `VideoExtensionPredicate` | private static | Filter auf `VideoFileExtensions.All` via `EndsWith` auf `Name`. |

## `SftpMediaSourceReader`
Datei: `VideoWebPlayer/Services/SftpMediaSourceReader.cs` (SSH.NET / Renci.SshNet)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OpenFileStream(collection, fileName)` | public | Neuer `SftpClient` pro Aufruf (`Host`, `Port`, `Username`, `Password`); `CombineSftpPath(collection.Path, fileName)` → `client.OpenRead(fullPath)` in `SftpStreamWrapper`. `null` bei leerem Namen / nicht gefunden / Verzeichnis. |
| `CombineSftpPath(part1, part2)` | private static | `part1.TrimEnd('/') + "/" + part2.TrimStart('/')` — **Pfadbildungs-Konvention**. |
| `ReadDirectoryEntries` / `ReadSubtree` / `ReadDirectoryInternal` | public/private | Scan; `MediaItem.Path = entry.FullName` (**voller SFTP-Pfad**), `MediaItem.Name = entry.Name`. |

## `MediaSourceReaderDispatcher`
Datei: `VideoWebPlayer/Services/MediaSourceReaderDispatcher.cs`

- `GetReader`: `LocalDirectory` → `LocalMediaSourceReader`, sonst `SftpMediaSourceReader`. Wird als `IMediaSourceReader` injiziert (`AddScoped<IMediaSourceReader, MediaSourceReaderDispatcher>`).

## `LiveStreamMediaResolver` / `FfmpegLiveStreamSegmenter` (Kontext, nicht zu ändern)

- `ResolveAsync`: `LocalDirectory` → `LiveStreamInput.ForFile(phys. Pfad)`; sonst `LiveStreamInput.ForStream(_reader.OpenFileStream(collection, Path.GetFileName(item.Path)))`. Gleiche Pipe-Einschränkung wie die Probe — laut Auftrag **nicht** ändern.
- `FfmpegLiveStreamSegmenter.BuildArguments`: `pipe:0` für Stream-Inputs, sonst quotierter Pfad.

## `MediaDurationProbeWorker`
Datei: `VideoWebPlayer/Services/MediaDurationProbeWorker.cs` — Hosted Service, ruft `ProbePendingAsync(EffectiveBatchSize)` im `IntervalSeconds`-Takt; loggt nur Start/Stop/Fehler (keine Credential-relevanten Stellen).

## ffprobe-Aufruf / Credential-Sichtbarkeit

- `ExecuteProcessAsync` startet `Process` mit `Arguments = arguments` (ein String). Eine `sftp://user:pass@…`-URL in `arguments` ist in der Prozess-Kommandozeile für denselben Benutzer sichtbar (Task-Manager/Process-Explorer/`Get-CimInstance Win32_Process`).
- libssh-Protokolloptionen (verifiziert via `ffprobe -h protocol=sftp`, Gyan full build 9.0.2): `timeout`, `truncate`, `private_key` — **keine** `user`/`password`-Optionen → Credentials nur via URL-Userinfo möglich.
- Gyan-FFmpeg-full-Build enthält `--enable-libssh` → `sftp` im `-protocols`-Listing (Input+Output). Build ohne libssh meldet `sftp: Protocol not found` (stderr, Exit ≠ 0).
