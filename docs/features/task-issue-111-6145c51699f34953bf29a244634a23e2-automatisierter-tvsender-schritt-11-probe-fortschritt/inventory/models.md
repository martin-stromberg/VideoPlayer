# Bestandsaufnahme — Datenmodell

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs` (Basisklasse `MediaEntry` in `VideoWebPlayer/Data/MediaEntry.cs`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel (geerbt von `MediaEntry`) |
| `Name` | `string` | Dateiname/Anzeigename (geerbt) |
| `Path` | `string` | Pfad bzw. Verbindungsinformation (geerbt) |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt (geerbt) |
| `ClassifiedAt` | `DateTime?` | Zeitpunkt der letzten Klassifizierung (geerbt) |
| `Changed` | `bool` | Änderungsmarkierung für den Scanner (geerbt) |
| `MediaCollectionId` | `long` | Fremdschlüssel zur `MediaCollection` |
| `MediaCollection` | `MediaCollection` | Navigation zur Sammlung (darüber `MediaSource` erreichbar) |
| `Duration` | `TimeSpan?` | Ermittelte Wiedergabedauer; `null` = keine Laufzeit |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt des letzten Probe-Versuchs; `null` = nie geprüft. **Indiziert** (`MediaItemConfiguration.HasIndex`) |
| `DurationProbeAttempts` | `int` | Fehlerversuchszähler; `>= MediaProbeOptions.EffectiveMaxAttempts` = dauerhaft gescheitert |

## `MediaCollection` / `MediaSource`
Datei: `VideoWebPlayer/Data/` (unverändert relevante Lesepfade)

- `MediaItem.MediaCollection.MediaSource.SourceType` (`MediaSourceType.LocalDirectory` vs. `Sftp`
  u. a.) entscheidet in `ProbeItemAsync` über Datei- vs. Stream-Probe. Für die Fortschrittsanzeige
  nur als mögliche Kontextangabe (Quellname) relevant.
- `MediaCollection.Name`/`MediaSource.Name` sind vorhanden (werden in Seed-Helfern befüllt).

## `MediaProbeOptions` (Konfiguration, kein Datenmodell)
Datei: `VideoWebPlayer/Configuration/MediaProbeOptions.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `FfprobePath` | `string?` | Optionaler Pfad zum ffprobe-Binary |
| `FfmpegPath` | `string?` | Optionaler Pfad zum ffmpeg-Binary |
| `IntervalSeconds` / `EffectiveIntervalSeconds` | `int` | Worker-Intervall (Default 60) |
| `BatchSize` / `EffectiveBatchSize` | `int` | Batch-Größe (Default 10) |
| `TimeoutSeconds` / `EffectiveTimeoutSeconds` | `int` | ffprobe-Timeout lokal (Default 120) |
| `StreamTimeoutSeconds` / `EffectiveStreamTimeoutSeconds` | `int` | ffprobe-Timeout via `pipe:0` (Default 600) |
| `RetryDelaySeconds` / `EffectiveRetryDelaySeconds` | `int` | Mindestabstand zwischen Versuchen (Default 300) |
| `MaxAttempts` / `EffectiveMaxAttempts` | `int` | Versuchsgrenze = dauerhaft gescheitert (Default 10) |
| `ParallelProbes` / `EffectiveParallelProbes` | `int` | Parallele ffprobe-Prozesse (Default 4) |

Kein neuer Persistenzbedarf: alle Anzeigedaten aggregieren über die vorhandenen Spalten.
