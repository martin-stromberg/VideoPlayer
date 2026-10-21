# Datenmodell — relevante Entitäten

## `MediaItem` (Data/MediaItem.cs, erbt `MediaEntry`)
| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `MediaCollectionId` | long | Collection der Datei |
| `MediaCollection` | MediaCollection | Navigation (enthält `MediaSource`) |
| `Duration` | TimeSpan? | ermittelte Laufzeit (null = nicht ermittelt) |
| `DurationProbedAt` | DateTime? | Zeitpunkt des letzten Probe-Versuchs |
| `DurationProbeAttempts` | int | Versuchszähler (Retry/MaxAttempts) |
| geerbt: `Name`, `Path` | string | Dateiname / Pfad (bei SFTP absoluter Remote-Pfad) |

## `MediaSource` (Data/MediaSource.cs, erbt `MediaEntry`)
| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `Host` | string | SFTP-Host |
| `Port` | int | SFTP-Port |
| `Username`/`Password` | string? | SFTP-Zugangsdaten |
| `SourceType` | MediaSourceType | `Sftp = 0`, `LocalDirectory = 1` |
| `Path` | string | Root-Pfad (Basis der Root-Collection) |

## `MediaCollection` (erbt `MediaEntry`)
`MediaSourceId`/`MediaSource`, `ParentMediaCollectionId`, `Path` (Verzeichnis der Dateien).

## Join-Tabellen MediaItem ↔ Inhalt
| Entität | Eigenschaften | Zweck |
|---------|---------------|-------|
| `MovieMediaItem` | `MovieId`, `MediaItemId` | `api/items/movie/{MovieId}/stream` löst über diese Tabelle auf |
| `TVShowEpisodeMediaItem` | `TVShowEpisodeId`, `MediaItemId` | `api/items/tvshowepisode/{TVShowEpisodeId}/stream` |

## `MediaProbeOptions` (Configuration/MediaProbeOptions.cs, Section `MediaProbe`)
| Eigenschaft | Standard | Zweck |
|-------------|----------|-------|
| `FfprobePath`/`FfmpegPath` | null | Explizite Binary-Pfade |
| `IntervalSeconds` | 60 | Worker-Intervall |
| `BatchSize` | 10 | Items je Durchlauf |
| `TimeoutSeconds` | 120 | Timeout Dateipfad-/URI-Probes |
| `StreamTimeoutSeconds` | 600 | Timeout `pipe:0`-Probes |
| `RetryDelaySeconds` | 300 | Mindestabstand Retry |
| `MaxAttempts` | 10 | Versuchsbudget |
| `ParallelProbes` | 4 | Parallele ffprobe-Prozesse |
| `Effective*` | — | normalisierte Getter (>0 sonst Default) |

Kein neues Datenmodell nötig (keine Migration, keine Backup-Tabellen).
