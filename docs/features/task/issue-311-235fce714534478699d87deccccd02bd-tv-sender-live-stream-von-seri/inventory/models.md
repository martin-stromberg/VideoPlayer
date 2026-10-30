# Bestandsaufnahme: Datenmodelle und Records — Live-Stream-Eingangskette

## `LiveStreamInput`
Datei: `VideoWebPlayer/Services/LiveStreamInput.cs`

`sealed record` — die ffmpeg-Eingangsspezifikation eines Sendeplatzes; genau eine der drei Ausprägungen ist gesetzt.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `FilePath` | `string?` | Lokaler Dateipfad, der ffmpeg direkt als `-i` übergeben wird (seekbar, auch für MP4 mit `moov`-Atom am Ende). |
| `OpenStreamAsync` | `Func<CancellationToken, Task<Stream?>>?` | Fabrik, die den Medienstream für den Pipe-Eingang öffnet; Inhalt wird in ffmpeg-stdin gepumpt (`pipe:0`, nicht seekbar). |
| `HttpUrl` | `string?` | Range-fähige URL des internen Media-Stream-Endpunkts (`api/internal/media-stream/{token}`). |
| `IsPipe` | `bool` | `true`, wenn `OpenStreamAsync` gesetzt ist — steuert `RedirectStandardInput` und den Pump-Task. |

Fabrikmethoden: `ForFile(string)`, `ForStream(Func<...>)`, `ForHttp(string)` — alle in `LiveStreamMediaResolver.ResolveAsync` erzeugt.

## `LiveStreamJob`
Datei: `VideoWebPlayer/Services/LiveStreamJob.cs`

`sealed record` — ein Segmentierungsauftrag für einen Sendeplatz, erzeugt in `LiveStreamCoordinator.SynchronizeStreamAsync` (Zeile 149-156).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SlotId` | `long` | `ProgramSlot`-Id des Sendeplatzes. |
| `Input` | `LiveStreamInput` | Der aufgelöste Medieneingang. |
| `PositionSeconds` | `double` | Wiedergabeposition, an der gestartet wird (`-ss`-Wert). |
| `RemainingSeconds` | `double` | Maximale Ausgabedauer = restliche Sendezeit (`-t`-Wert). |
| `SegmentSeconds` | `int` | Ziel-Segmentlänge (`-hls_time`, keyframe-Ausrichtung). |
| `SegmentDirectory` | `string` | Zielverzeichnis für Segmente und `live.m3u8`. |
| `FfmpegPath` | `string` | Aufgelöster ffmpeg-Pfad. |

## `LiveStreamExitInfo`
Datei: `VideoWebPlayer/Services/LiveStreamExitInfo.cs`

`sealed record` — Diagnose eines vorzeitig von selbst beendeten Segmentierungsprozesses; an `LiveStreamCoordinator` gemeldet (`ILiveStreamSegmenter.LastUnexpectedExit`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SlotId` | `long` | Sendeplatz des fehlgeschlagenen Laufs. |
| `ExitCode` | `int` | Prozess-Exit-Code (-1 bei unbekannt). |
| `HttpTransportFailure` | `bool` | `true` nur, wenn der Lauf einen HTTP-Eingang hatte **und** stderr einen Transportmarker enthielt (`FfmpegLiveStreamSegmenter.cs:301-302`) — steuert den Fallback auf `pipe:0`. |
| `OccurredAtUtc` | `DateTimeOffset` | Zeitpunkt der Erkennung. |

## `LiveStreamSyncResult`
Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs:31`

`sealed record` — Ergebnis eines Synchronisationsticks.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Action` | `LiveStreamSyncAction` | Was der Tick entschieden hat. |
| `WakeAtUtc` | `DateTimeOffset?` | Nächster fälliger Entscheidungspunkt; `null` = regulärer Poll-Takt. |
| `Detail` | `string?` | Menschenlesbares Detail fürs Log. |

## `MediaSource`
Datei: `VideoWebPlayer/Data/MediaSource.cs` (CP1252-kodiert)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Host` | `string` | Hostname (SFTP). |
| `Port` | `int` | Port (SFTP). |
| `Username` / `Password` | `string?` | Zugangsdaten (SFTP). |
| `SourceType` | `MediaSourceType` | `Sftp` (0, Default) oder `LocalDirectory` (1) — **der einzige Schalter, der im Resolver zwischen `ForFile` und HTTP/Pipe unterscheidet** (`LiveStreamMediaResolver.cs:110`). |
| `IconPictureId`, `LastScannedAt`, `HiddenInMenu` | — | Für die Anforderung nicht relevant. |
| `MediaCollections`, `MediaSourceUsers` | `ICollection<...>` | Navigationen. |

Basisklasse `MediaEntry` (`Id`, `Name`, `Path`, `CreatedAt`, `ClassifiedAt`, `Changed`).

## `MediaCollection` / `MediaItem`
Dateien: `VideoWebPlayer/Data/MediaCollection.cs`, `VideoWebPlayer/Data/MediaItem.cs`

| Klasse | Relevante Eigenschaften | Zweck |
|--------|------------------------|-------|
| `MediaCollection` | `MediaSourceId`, `MediaSource`, `Path`, `ParentMediaCollectionId`, `MediaItems` | Verzeichnis innerhalb einer Quelle; `MediaSource` muss eager geladen sein (`MediaSourceReaderDispatcher` wirft sonst). |
| `MediaItem` | `MediaCollectionId`, `MediaCollection`, `Duration`, `DurationProbedAt`, `DurationProbeAttempts` | Die Videodatei; `Path`/`Name` aus `MediaEntry` — der Resolver nutzt `Path.GetFileName(item.Path)` als Dateinamen (`LiveStreamMediaResolver.cs:103`). |

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Sendeplatz-Id (= `SlotId` im Job). |
| `Start` / `End` | `DateTime` | Sendefenster (UTC); `End − now` wird zu `RemainingSeconds`. |
| `MediaItemId` | `long` | Weiche Referenz auf die Videodatei (kein FK) — Eingang des Resolvers. |
| `MovieId` / `TVShowEpisodeId` | `long?` | Weiche Inhaltsreferenzen; `null` für reine Clip-Slots. |
| `RuleType` | `ProgramRuleType` | Denormalisierter Regeltyp (u. a. `Filler`). |
| `IsClipFill` | `bool` | Markiert Clip-Füll-Slots — **wird von der Eingangs-Auflösung nicht ausgewertet**; Platzhalter- und Serien-Slots durchlaufen denselben Pfad. |
| `Status` | `ProgramSlotStatus` | `Scheduled`/`Displaced`. |

## `DtoProgramGuideEntry` / `DtoProgramGuideNow`
Datei: `VideoWebPlayer.Client/Models/DtoProgramGuideEntry.cs`

Vom `IProgramGuideService.GetNowAsync` gelieferter laufender Sendeplatz; der Coordinator liest daraus `SlotId`, `MediaItemId`, `PositionSeconds`, `RemainingSeconds`, `EndUtc`, `Title` (`LiveStreamCoordinator.cs:99-160`).

## `LiveStreamOptions`
Datei: `VideoWebPlayer/Configuration/LiveStreamOptions.cs` — Section `LiveStream`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `UseHttpInput` | `bool` (Default `true`) | Schaltet den HTTP-Range-Eingang entfernter Quellen ab; `false` = immer `pipe:0`. In `appsettings.json:64-66` explizit `true`. |

## `MediaProbeOptions` (relevante Teilmenge)
Datei: `VideoWebPlayer/Configuration/MediaProbeOptions.cs` — Section `MediaProbe`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `HttpProbeEnabled` | `bool` (Default `true`) | Gate nur für `TryCreateProbeUri` (ffprobe), **nicht** für `TryCreateStreamUri` (Segmenter). |
| `HttpProbeBaseUrl` | `string?` | Explizite Loopback-Basis-URL; einzige Alternative zur Auto-Erkennung einer gebundenen `http://`-Adresse (`InternalMediaStreamTokenService.cs:155-168`). |
| `FfmpegPath` / `FfprobePath` | `string?` | Konfigurierte Werkzeugpfade (Auto-Auflösung sonst). |

## `StandbyTranscodedSegment` / `StandbySegment` / `StandbySegmentSet` / `StandbyCacheMarker`
Dateien: `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs:16`, `VideoWebPlayer/Services/LiveStreamStandbyProvider.cs:20-38` (Marker: private Klasse in derselben Datei)

Interne Records der Standby-Auslieferung (vorproduzierte Segmente im Speicher bzw. Disk-Cache `Data/LiveStreamStandby` mit Marker-Datei `standby-source.json`). Relevant, weil der „Platzhalter-Clip"-Begriff zwei verschiedene Mechanismen umfasst (siehe `logic.md`).
