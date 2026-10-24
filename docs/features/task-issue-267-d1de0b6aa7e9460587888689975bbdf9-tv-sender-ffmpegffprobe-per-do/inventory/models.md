# Bestandsaufnahme: Datenmodell- und Optionsklassen

Relevante bestehende Klassen für „ffmpeg/ffprobe per Download-Button nachinstallieren".
Es existiert **kein** Download-/Installationsmodell — alles Folgende ist Bestand.

## `MediaProbeOptions` (Konfiguration)

**Datei:** `VideoWebPlayer/Configuration/MediaProbeOptions.cs`
**Bindung:** `services.Configure<MediaProbeOptions>(configuration.GetSection("MediaProbe"))` in `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs:234`

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `FfprobePath` | `string?` | Absoluter Pfad zum ffprobe-Binary; `null` = automatische Suche (Konventionsverzeichnisse, dann `PATH`) |
| `FfmpegPath` | `string?` | Absoluter Pfad zum ffmpeg-Binary; `null` = automatische Suche |
| `IntervalSeconds` | `int` | Poll-Intervall des Probe-Workers (Default 60) |
| `BatchSize` | `int` | Max. Medien-Items je Probe-Batch (Default 10) |
| `TimeoutSeconds` | `int` | Timeout je ffprobe-Aufruf auf Dateipfad (Default 120) |
| `StreamTimeoutSeconds` | `int` | Timeout je ffprobe-Aufruf über `pipe:0` (Default 600) |
| `RetryDelaySeconds` | `int` | Mindestwartezeit vor Wiederholung transienter Fehler (Default 300) |
| `MaxAttempts` | `int` | Max. Versuche je Item, dann dauerhaft fehlgeschlagen (Default 10) |
| `ParallelProbes` | `int` | Max. parallele ffprobe-Prozesse (Default 4) |
| `HttpProbeEnabled` | `bool` | HTTP-Loopback-Probe für Remote-Items (Default `true`) |
| `HttpProbeBaseUrl` | `string?` | Explizite Loopback-Basis-URL für HTTP-Probes |
| `HttpProbeTokenLifetimeSeconds` | `int` | Lebensdauer interner Stream-Tokens (Default 600) |
| `Effective*` | `int` (readonly) | Normalisierte Getter: `EffectiveIntervalSeconds`, `EffectiveBatchSize`, `EffectiveTimeoutSeconds`, `EffectiveStreamTimeoutSeconds`, `EffectiveRetryDelaySeconds`, `EffectiveMaxAttempts`, `EffectiveParallelProbes`, `EffectiveHttpProbeTokenLifetimeSeconds` |

Es gibt **keinen** Konfigurationsschlüssel für Download-Quellen (`MediaProbe:DownloadSources` o. ä.).
Ist-Zustand `appsettings.json` (Abschnitt `MediaProbe`): `FfprobePath: null`, `IntervalSeconds: 60`, `BatchSize: 10`, `TimeoutSeconds: 120`, `StreamTimeoutSeconds: 600`, `RetryDelaySeconds: 300`, `MaxAttempts: 10`, `ParallelProbes: 4`, `HttpProbeEnabled: true`, `HttpProbeBaseUrl: null`, `HttpProbeTokenLifetimeSeconds: 600` — `FfmpegPath` ist in `appsettings.json` nicht gesetzt (Default `null`).

## `LiveStreamOptions` (Konfiguration)

**Datei:** `VideoWebPlayer/Configuration/LiveStreamOptions.cs`
**Bindung:** `ServiceCollectionExtensions.cs:236` (`LiveStream`-Abschnitt)

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `UseHttpInput` | `bool` | Remote-Items dem ffmpeg-Segmentierer über den internen Range-Endpunkt statt `pipe:0` reichen (Default `true`) |

## `ProgramScheduleOptions` (Konfiguration, nur Kontext)

**Datei:** `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs` — `TrailerLeadDays`, `MinStartRemainSeconds`, `MaxOccurrenceLookbackDays` (+ `Effective*`-Getter). Nicht direkt betroffen.

## `MediaItem` (Datenmodell)

**Datei:** `VideoWebPlayer/Data/MediaItem.cs` — relevante Probe-Felder:

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Duration` | `TimeSpan?` | Ermittelte Videolaufzeit; `null` = nicht/nie ermittelt |
| `DurationProbedAt` | `DateTime?` | Zeitstempel des letzten Probe-Versuchs |
| `DurationProbeAttempts` | `int` | Zähler fehlgeschlagener Versuche; >= `MaxAttempts` = dauerhaft fehlgeschlagen |

## `MediaDurationProbeProgress` / `MediaDurationProbeRecentItem` / `MediaDurationProbeDetailItem` (Records)

**Datei:** `VideoWebPlayer/Services/MediaDurationProbeProgress.cs`
Aggregat-Record für die Admin-View (Tab „Laufzeitermittlung"):

| Member | Typ | Beschreibung |
|---|---|---|
| `TotalCount` | `int` | Anzahl aller Video-MediaItems |
| `ProbedCount` | `int` | Items mit ermittelter `Duration` |
| `PendingCount` | `int` | Items ohne Duration, Versuche < Max |
| `FailedCount` | `int` | Items ohne Duration, Versuche >= Max |
| `LastActivityAt` | `DateTime?` | Letzter Probe-Versuch |
| `ProbedLastHour` | `int` | Versuche in der letzten Stunde |
| `RecentItems` | `IReadOnlyList<MediaDurationProbeRecentItem>` | Letzte Ergebnisse |
| `PendingItems` | `IReadOnlyList<MediaDurationProbeDetailItem>` | Ausstehende (Detail-Liste, gedeckelt) |
| `FailedItems` | `IReadOnlyList<MediaDurationProbeDetailItem>` | Fehlgeschlagene (Detail-Liste, gedeckelt) |

Es gibt **kein** Verfügbarkeits-Flag für ffprobe in diesem Record — die Anforderung nennt es optional.

`MediaDurationProbeRecentItem`: `Name`, `SourceName`, `ProbedAt`, `Duration`, `Attempts`.
`MediaDurationProbeDetailItem`: `MediaItemId`, `Name`, `SourceName`, `LastAttemptAt`, `Attempts`.

## `MediaDurationProbeResult` (Record)

**Datei:** `VideoWebPlayer/Services/MediaDurationProbeResult.cs`

| Member | Typ | Beschreibung |
|---|---|---|
| `Duration` | `TimeSpan?` | Ermittelte Laufzeit oder `null` |
| `Transient` | `bool` | `true` = wiederholbar, `false` = dauerhaft |
| `UriUnsupported` | `bool` (init) | URI-Schema wird vom ffprobe-Build nicht unterstützt |
| `TransientFailure` / `PermanentFailure` / `UriUnsupportedFailure` | static readonly | Vorgefertigte Ergebnisinstanzen |
| `Success(duration)` | static | Erfolgs-Factory |

## `LiveStreamJob` / `LiveStreamInput` / `LiveStreamExitInfo` (Records)

**Dateien:** `VideoWebPlayer/Services/LiveStreamJob.cs`, `LiveStreamInput.cs`, `LiveStreamExitInfo.cs`

`LiveStreamJob(long SlotId, LiveStreamInput Input, double PositionSeconds, double RemainingSeconds, int SegmentSeconds, string SegmentDirectory, string FfmpegPath)` — trägt den per `IFfmpegLocator.Resolve()` aufgelösten Pfad.
`LiveStreamInput` (in `LiveStreamInput.cs`): `FilePath`, `HttpUrl`, `IsPipe`, `OpenStreamAsync`-Delegate.
`LiveStreamExitInfo(long SlotId, int ExitCode, bool HttpTransportFailure, DateTimeOffset At)` — Diagnose unerwarteter ffmpeg-Exits.

## `ManualBackupJobSnapshot` / `ManualBackupStartResult` (Records — Muster für den Download-Job)

**Datei:** `VideoWebPlayer/Services/Backups/ManualBackupJobService.cs:149-179`

| Record | Member | Beschreibung |
|---|---|---|
| `ManualBackupJobSnapshot` | `Id`, `Status`, `StartedAtUtc`, `CompletedAtUtc`, `Message`, `Error`, `IsActive`, `Idle` | Immutabler Job-Zustand, `Idle`-Singleton, `IsActive` = Queued/Running |
| `ManualBackupStartResult` | `Started`, `Snapshot` | Startversuch-Ergebnis (verhindert Parallelstarts) |

Analog existiert `RestoreBackupJobService` mit eigenem Snapshot + `BackupRestoreProgress` (Record-Fortschritt `RecordNumber`/`RecordTotal` über `IProgress<BackupRestoreProgress>`, `RestoreBackupJobService.cs:137`) — kein Byte-/Prozent-Fortschritt.

## `Setup` / `ProgramSettingsService`-Livestream-Felder (Datenmodell, Kontext)

`VideoWebPlayer/Services/ProgramSettingsService.cs:362-428`: `GetLiveStreamSegmentLengthAsync`, `GetLiveStreamSegmentDirectoryAsync`, `GetLiveStreamSegmentRetentionAsync`, `UpdateLiveStreamSettingsAsync(segmentLengthSeconds, segmentDirectory, retentionMinutes)` — Werte liegen in der `Setup`-Tabelle (`LiveStreamSegmentLengthSeconds`, `LiveStreamSegmentDirectory`, `LiveStreamSegmentRetentionMinutes`). Für den Download-Installationszustand ist kein Setup-Feld nötig (Anforderung: Dateisystem ist Wahrheit).

## Weitere relevante Records/Klassen

- `FfprobeInvocationResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut)` — internal record am Ende von `FfprobeMediaDurationProbe.cs:383`.
- `UpdateAdminSnapshot` / `UpdateAdminActionResult` — `VideoWebPlayer/Services/Updates/UpdateAdminService.cs:148-182` (Fassade `IAutoUpdateOrchestrator`/`IAutoUpdateCommandHandler` aus `msTools.Updater`; kein Byte-Fortschritt, nur `AutoUpdateState`-Status).
- `ForbiddenAccessException` — `VideoWebPlayer/Controllers/Exceptions/ForbiddenAccessException.cs` (internal, 403-Abbildung); wird von `MediaDurationProbeService.RequireAdminAsync` geworfen.
