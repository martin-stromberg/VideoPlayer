# Bestandsaufnahme – Robustheit der Laufzeitermittlung (N4)

## Überblick

Die Laufzeitermittlung besteht aus drei Schichten: dem BackgroundService `MediaDurationProbeWorker` (Intervall/Gate-Steuerung), dem scoped `MediaDurationProbeService` (Auswahl + Persistenz) und `FfprobeMediaDurationProbe` (ffprobe-Prozessaufruf). Fehler werden heute undifferenziert als `TimeSpan? == null` zurückgegeben und markieren das Item dauerhaft als fehlgeschlagen. Detaildokument: [inventory/probe-pipeline.md](inventory/probe-pipeline.md).

## Betroffene Artefakte (Ist-Zustand)

| Artefakt | Rolle | Relevante Details |
|----------|-------|-------------------|
| `VideoWebPlayer/Configuration/MediaProbeOptions.cs` | Optionen `MediaProbe` | `FfprobePath`, `FfmpegPath`, `IntervalSeconds`(60), `BatchSize`(10), `TimeoutSeconds`(120) + `Effective*`-Getter; keine Stream-/Retry-/Parallelitäts-Optionen |
| `VideoWebPlayer/appsettings.json` (Zeile 51-56) | Konfiguration | Abschnitt `MediaProbe` mit `FfprobePath`, `IntervalSeconds`, `BatchSize`, `TimeoutSeconds` |
| `VideoWebPlayer/Data/MediaItem.cs` | Entität | `Duration` (TimeSpan?), `DurationProbedAt` (DateTime?, „failed = nie erneut"-Semantik in XML-Doku) |
| `VideoWebPlayer/Services/IMediaDurationProbe.cs` | Interface | `ProbeFileAsync`/`ProbeStreamAsync` → `Task<TimeSpan?>`; einzige Prod.-Implementierung `FfprobeMediaDurationProbe` |
| `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs` | ffprobe-Aufruf | `StandardInputArgument = "pipe:0"`; ein `TimeoutSeconds` für beide Pfade; `FfprobeInvocationResult(ExitCode, StandardOutput, TimedOut)` — **kein stderr**; interne Ctor-Seam `_executeAsync` für Tests |
| `VideoWebPlayer/Services/MediaDurationProbeService.cs` | Auswahl + Persistenz | Selektion `DurationProbedAt == null` + Video-Endungen; `LocalDirectory` → `ProbeFileAsync(item.Path)`, sonst `_reader.OpenFileStream` → `ProbeStreamAsync`; `DurationProbedAt = probedAt` in jedem Fall; **kein `IOptions<MediaProbeOptions>` injiziert**, kein `IProgramScheduleSignal` |
| `VideoWebPlayer/Services/MediaDurationProbeWorker.cs` | BackgroundService | 10 s Startdelay, Gate-Lease „Laufzeitermittlung", `do { ProbePendingAsync(BatchSize) } while (processed > 0)`, danach `IntervalSeconds` Pause |
| `VideoWebPlayer/Services/SftpMediaSourceReader.cs` | Remote-Zugriff | `OpenFileStream` erzeugt **pro Aufruf einen neuen `SftpClient`** → parallele Probes sind verbindungs-seitig sicher |
| `VideoWebPlayer/Services/ProgramScheduleWorker.cs` + `ProgramScheduleSignal.cs` | Planbarkeit | `ProgramScheduleService` selektiert `MediaItem.Duration != null`; `IProgramScheduleSignal.NotifyScheduleChanged()` weckt den Worker sofort — wird heute von Scan/Regel-CRUD genutzt, **nicht** vom Probe-Service |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` | Backup-Restore | `OptionalRestoreColumns` (enthält bereits `MediaItems.Duration`/`DurationProbedAt`), `OptionalRestoreIntDefaults` (Muster für int-Defaults, Zeile ~119) |

## Tests (Ist-Zustand)

- `MediaDurationProbeServiceTests` — echtes SQLite (shared in-memory), `FakeDurationProbe`/`StubMediaSourceReader`-Fakes; Test `ProbePendingAsync_FailedProbe_MarksProbedWithoutDuration` assertiert explizit „wird nicht erneut geprüft" → muss auf die neue Retry-Semantik umgestellt werden.
- `FfprobeMediaDurationProbeTests` — injizierte `_executeAsync`-Seam; assertiert `TimeSpan`-Timeouts aus `MediaProbeOptions`.
- `MediaDurationProbeWorkerTests` — Worker treibt Service ohne Interaktion.
- `VideoWebPlayerBackupDataTests` — Muster `ReadFromAsync_LegacyBackupWithoutDurationColumnsInMediaItems_RestoresSuccessfully` + `LegacyBackupArchiveBuilder.RemoveColumnsAsync` für die Alt-Backup-Regression.

## Konventionen

- Optionen bekommen `Effective*`-Getter mit Fallback auf Default bei `<= 0`.
- Neue int-Spalten mit Restore-Default kommen in `OptionalRestoreColumns` **und** `OptionalRestoreIntDefaults`.
- EF-Migrationen via `dotnet ef migrations add` (SQL-Server-Migrationsordner `VideoWebPlayer/Migrations`; SQLite nutzt `EnsureCreated` in Tests — das Modell ist shared).
- Hintergrundarbeiten laufen hinter `IBackgroundProcessingGate` („Laufzeitermittlung"-Lease bereits vorhanden).
