# Datenmodell

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs` (erbt `MediaEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel (geerbt von `MediaEntry`) |
| `Name` | `string` | Dateiname, in Probe-Listen angezeigt (geerbt) |
| `Path` | `string` | Dateipfad (geerbt) |
| `MediaCollectionId` | `long` | FK auf `MediaCollection` → `MediaSource.Name` = „Quelle" in der UI |
| `Duration` | `TimeSpan?` | ermittelte Laufzeit; `null` = nicht/nicht erfolgreich ermittelt |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt des letzten Probe-Versuchs; `null` = nie probiert |
| `DurationProbeAttempts` | `int` | Zähler fehlgeschlagener Versuche; `>= EffectiveMaxAttempts` = dauerhaft gescheitert |

## `MediaDurationProbeProgress`
Datei: `VideoWebPlayer/Services/MediaDurationProbeProgress.cs` (record)

| Parameter | Typ | Beschreibung |
|-----------|-----|--------------|
| `TotalCount` | `int` | alle Video-`MediaItem`s (Extension-Filter `VideoFileExtensions.All`: `.mp4`, `.avi`, `.mkv`, `.mpeg`) |
| `ProbedCount` | `int` | `Duration != null` |
| `PendingCount` | `int` | `TotalCount - ProbedCount - FailedCount` |
| `FailedCount` | `int` | `Duration == null && DurationProbeAttempts >= EffectiveMaxAttempts` |
| `LastActivityAt` | `DateTime?` | max. `DurationProbedAt` |
| `ProbedLastHour` | `int` | Versuche in der letzten Stunde |
| `RecentItems` | `IReadOnlyList<MediaDurationProbeRecentItem>` | jüngste Probes, neueste zuerst |

## `MediaDurationProbeRecentItem`
Datei: `VideoWebPlayer/Services/MediaDurationProbeProgress.cs` (record)

| Parameter | Typ | Beschreibung |
|-----------|-----|--------------|
| `Name` | `string` | Dateiname |
| `SourceName` | `string` | `MediaCollection.MediaSource.Name` |
| `ProbedAt` | `DateTime` | Zeitpunkt des letzten Versuchs (nicht nullable — RecentItems haben immer eins) |
| `Duration` | `TimeSpan?` | Ergebnis oder `null` bei Fehlschlag |
| `Attempts` | `int` | Versuchszähler nach dem Versuch |

## `MediaProbeOptions`
Datei: `VideoWebPlayer/Configuration/MediaProbeOptions.cs`

| Eigenschaft | Typ | Standard | Zweck |
|-------------|-----|----------|-------|
| `RetryDelaySeconds` / `EffectiveRetryDelaySeconds` | `int` | 300 | Wartezeit vor erneutem Versuch |
| `MaxAttempts` / `EffectiveMaxAttempts` | `int` | 10 | Versuchsgrenze für „dauerhaft fehlgeschlagen" |
| `ParallelProbes` / `EffectiveParallelProbes` | `int` | 4 | parallele ffprobe-Aufrufe |
