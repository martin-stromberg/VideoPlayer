# Detail: Probe-Pipeline (Ist-Zustand)

## Ablauf

1. `MediaDurationProbeWorker.ExecuteAsync`: 10 s Delay → Schleife: Scope → `IBackgroundProcessingGate.EnterOperationAsync("Laufzeitermittlung")` → `do ProbePendingAsync(BatchSize) while processed > 0` → `Delay(IntervalSeconds)`.
2. `MediaDurationProbeService.ProbePendingAsync(batchSize, ct)`:
   - `batchSize <= 0` → 0; `IFfprobeLocator.Resolve() == null` → einmalige Warnung, 0.
   - Query: `MediaItems.Include(MediaCollection.MediaSource).Where(DurationProbedAt == null).Where(<Video-Endung>).OrderBy(Id).Take(batchSize)`.
   - Pro Item `ProbeItemAsync`: `LocalDirectory` → `ProbeFileAsync(item.Path)`; sonst `_reader.OpenFileStream(collection, name)` → `null` = Fehler, sonst `ProbeStreamAsync(stream)`.
   - Ergebnis → `item.Duration`; Exception (außer Cancellation) → Warnung + `Duration = null`. Danach immer `DurationProbedAt = probedAt` + `SaveChangesAsync`.
3. `FfprobeMediaDurationProbe`: `Resolve()` → Argumente `-v error -show_entries format=duration -of default=... {input}` → `_executeAsync(path, args, stdin, TimeSpan.FromSeconds(EffectiveTimeoutSeconds), ct)` → `TimedOut`/Exception/`ExitCode != 0`/unparsebar → `null`; sonst `TryParseDurationOutput(stdout)`.
   - `ExecuteProcessAsync`: piped stdin per `CopyToAsync`, `WaitForExitAsync` mit linked CTS (`CancelAfter(timeout)`), Timeout → `Kill(entireProcessTree: true)`.

## Beobachtete Probleme (Live-Befund N4)

- Ein Timeout (120 s) für Datei- und Pipe-Probes; bei MP4 ohne moov-Atom liest ffprobe die gesamte Datei über die SFTP-Pipe → fast immer Timeout.
- Jeder Fehlschlag (inkl. Timeout) setzt `DurationProbedAt` → Item wird nie wieder selektiert („unplanbar gebranntmarkt").
- Serielle Abarbeitung: bei Timeout ~1 Item / 2 min → ~30/h → 111k Backlog nie aufholbar.

## Fehlerklassifikation (neu)

| ffprobe-Ausgang | Klasse | Begründung |
|------------------|--------|------------|
| `TimedOut` | transient | Latenz/Datei zu groß für Limit — längeres Timeout oder späterer Versuch kann helfen |
| `ExitCode == -1` (Start fehlgeschlagen) | transient | Binary/Umgebung vorübergehend nicht verfügbar |
| Ausnahme in `_executeAsync` | transient | IO-/Umgebungsfehler |
| `ExitCode != 0` | transient | Bei Pipe-Streams typischerweise Stream-Abriss/Underrun; begrenzte Wiederholung fängt auch korrupte Dateien ab |
| `ExitCode == 0`, Ausgabe unparsebar/`N/A` | permanent | ffprobe lief sauber, Datei hat schlicht keine bestimmbare Dauer |

## Auswahl-Semantik (neu)

`Duration == null` UND ( `DurationProbedAt == null` ODER ( `DurationProbeAttempts < MaxAttempts` UND `DurationProbedAt <= jetzt - RetryDelay` ) )

- `DurationProbedAt` wird zum Zeitstempel des **letzten Versuchs** (nicht mehr „endgültig entschieden").
- Permanente Fehler setzen `DurationProbeAttempts = MaxAttempts` → ausgeschlossen; wird `MaxAttempts` später erhöht, kommt das Item wieder dran (harmlos/gewollt).
- Bestandsitems (`DurationProbedAt` gesetzt, `Duration` NULL, `DurationProbeAttempts` = 0 nach Migration) werden nach Ablauf von `RetryDelay` seit `DurationProbedAt` automatisch wieder aufgegriffen.

## Parallelität

`SftpMediaSourceReader.OpenFileStream` öffnet pro Aufruf einen eigenen `SftpClient` → parallele Probes sind verbindungsseitig unproblematisch. DbContext bleibt single-threaded: Probes laufen parallel (`SemaphoreSlim(ParallelProbes)`), Ergebniszuweisung und `SaveChangesAsync` seriell pro Item nach `Task.WhenAll`.
