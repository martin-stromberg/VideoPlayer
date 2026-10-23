# Inventar: Datenmodell

Bestand der für Issue #258 (Probe-Reset fehlgeschlagener Laufzeit-Probes) relevanten
Datenmodellklassen. Alle Angaben ohne Annahmen — nur tatsächlich im Code vorhandene Member.

## `MediaItem` (`VideoWebPlayer/Data/MediaItem.cs`)

Erbt von `MediaEntry`. Die drei Probe-Felder existieren bereits und sind persistiert
(EF-Spalten, Migrations `AddMediaItemDuration` / `AddMediaItemDurationProbeAttempts`).

| Eigenschaft | Typ | Zweck |
|---|---|---|
| `MediaCollectionId` | `long` | FK zur besitzenden `MediaCollection` |
| `MediaCollection` | `MediaCollection` | Navigation (liefert `MediaCollection.MediaSource.Name`) |
| `Duration` | `TimeSpan?` | ermittelte Wiedergabedauer; `null` = nicht ermittelt / Versuch fehlgeschlagen |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt des letzten Probe-Versuchs; `null` = nie geprobt; gesetzt + `Duration == null` = fehlgeschlagener Versuch |
| `DurationProbeAttempts` | `int` | Zähler fehlgeschlagener Versuche; `>= MediaProbeOptions.EffectiveMaxAttempts` = dauerhaft fehlgeschlagen |

„Dauerhaft fehlgeschlagen" ist damit im Code realisiert als
`Duration == null && DurationProbeAttempts >= maxAttempts`
(`MediaDurationProbeService.GetProgressAsync`, `MediaDurationProbeService.cs:157-158` bzw. `199-201`).

Geerbte Member aus `MediaEntry` (`VideoWebPlayer/Data/MediaEntry.cs`):

| Eigenschaft | Typ | Zweck |
|---|---|---|
| `Id` | `long` | Primärschlüssel — für den geplanten Reset die eindeutige Item-Identität |
| `Name` | `string` | Dateiname (heute das einzige Kennzeichen in den Detail-Records) |
| `Path` | `string` | Dateipfad (bei SFTP der volle Remote-Pfad) |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt; `MediaSourceScanner` nutzt Änderungen als Rescan-/Reset-Signal |
| `ClassifiedAt` | `DateTime?` | Zeitpunkt der Klassifizierung |
| `Changed` | `bool` | „geändert"-Markierung |

EF-Konfiguration (`VideoWebPlayer/Data/Configurations/MediaItemConfiguration.cs`):
FK `MediaCollectionId` → `MediaCollection.MediaItems`; Index auf `DurationProbedAt`
(`HasIndex(mi => mi.DurationProbedAt)`).

## Backup-Relevanz (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`)

- `MediaItems.Duration`, `MediaItems.DurationProbedAt`, `MediaItems.DurationProbeAttempts`
  stehen bereits in `OptionalRestoreColumns` (Zeilen 83-85).
- `MediaItems.DurationProbeAttempts` hat zusätzlich einen Eintrag in
  `OptionalRestoreIntDefaults` mit Standardwert `0` (Zeile 139).
- Keine neue Spalte nötig — ein Reset ändert nur Werte bestehender Spalten.

## Konfigurationsobjekt `MediaProbeOptions` (`VideoWebPlayer/Configuration/MediaProbeOptions.cs`)

Keine Datenmodellklasse, aber maßgeblich für die Failed-Schwelle:

| Member | Typ | Zweck |
|---|---|---|
| `MaxAttempts` | `int` (Default 10) | Versuchslimit, ab dem ein Item als dauerhaft fehlgeschlagen gilt |
| `EffectiveMaxAttempts` | `int` | normalisierter Wert (`<= 0` → 10) |
| `RetryDelaySeconds` / `EffectiveRetryDelaySeconds` | `int` (Default 300) | Mindestabstand für Wiederholungsversuche |
| `IntervalSeconds` / `EffectiveIntervalSeconds` | `int` (Default 60) | Takt des `MediaDurationProbeWorker` |
| `BatchSize` / `EffectiveBatchSize` | `int` (Default 10) | Items je Probe-Durchlauf |
| `TimeoutSeconds`, `StreamTimeoutSeconds`, `ParallelProbes`, `HttpProbeEnabled`, `HttpProbeBaseUrl`, `HttpProbeTokenLifetimeSeconds`, `FfprobePath`, `FfmpegPath` | diverse | übrige Probe-Optionen (für dieses Issue unverändert) |

Registriert über `services.Configure<MediaProbeOptions>(configuration.GetSection("MediaProbe"))`
(`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs:234`).
