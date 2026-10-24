# Bestandsaufnahme: Datenmodell

## `Setup`

Datei: `VideoWebPlayer/Data/Setup.cs`

Singleton-Tabelle (`_db.Setups`), über `ProgramSettingsService.GetOrCreateSetupAsync` gelesen/angelegt.
Nur die für die Anforderung relevanten Eigenschaften:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TvSenderEnabled` | `bool` | Z. 80. Globaler Admin-Freischalter des automatisierten TV-Senders. Default `false`. Migration `20261023050115_AddSetupTvSenderEnabled`; in `VideoWebPlayerBackupData` als optionale Spalte registriert (`Setups.TvSenderEnabled`, Default `false` — Alt-Backups restaurieren ausgeschaltet). Doku-Kommentar nennt als abgedeckte Komponenten: Nav-Eintrag, `/live-tv`, `/admin/tv-sender`, `/api/program-guide`, `/api/live-stream`, Schedule-/Stream-/Probe-Worker — **der interne Media-Stream-Endpunkt ist nicht aufgeführt**. |
| `LiveStreamSegmentLengthSeconds` | `int` | Z. 105. Segmentlänge (Default 6 s) — Kontext der Segmentierung, die Stream-Tokens nutzt. |
| `LiveStreamSegmentDirectory` | `string?` | Z. 113. HLS-Segment-Verzeichnis. |
| `LiveStreamSegmentRetentionMinutes` | `int` | Z. 119. Segment-Aufbewahrung (Default 30 min). |

Keine neue Eigenschaft/Migration nötig — das Flag existiert bereits.

## `MediaItem` / `MediaCollection` / `MediaSource` (vom Endpunkt gelesen)

Dateien: `VideoWebPlayer/Data/MediaItem.cs`, `VideoWebPlayer/Data/MediaCollection.cs`, `VideoWebPlayer/Data/MediaSource.cs`

`InternalMediaStreamController.Stream` lädt `MediaItems` inklusive `MediaCollection.MediaSource`
(Z. 61–64), um über `IMediaSourceReader.OpenFileStream(collection, fileName)` die Datei zu
öffnen. Für die Flag-Entscheidung relevant ist nur:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSource.SourceType` | `MediaSourceType` | `LiveStreamMediaResolver` verzweigt hier: `LocalDirectory` → physischer Pfad (kein Token), alle anderen (z. B. `Sftp`) → `TryCreateStreamUri`/Pipe-Fallback. Stream-Tokens laufen also nur für **entfernte Quellen** über den Endpunkt. |

## Interne Records (keine EF-Entitäten)

### `InternalMediaStreamTokenService.TokenEntry`

Datei: `VideoWebPlayer/Services/InternalMediaStreamTokenService.cs` Z. 64

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaItemId` | `long` | Das eine Medium, das das Token autorisiert. |
| `ExpiresAt` | `DateTimeOffset` | Ablaufzeitpunkt (`TimeProvider.GetUtcNow() + lifetime` bei Ausstellung). |

Rein in-memory (`ConcurrentDictionary`), nie persistiert.

### `LiveStreamInput`

Datei: `VideoWebPlayer/Services/LiveStreamInput.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `FilePath` | `string?` | Lokaler Dateipfad für `ffmpeg -i` (`ForFile`). |
| `OpenStreamAsync` | `Func<CancellationToken, Task<Stream?>>?` | Pipe-Eingang `pipe:0` (`ForStream`), Fallback. |
| `HttpUrl` | `string?` | Loopback-URL des internen Media-Stream-Endpunkts (`ForHttp`) — wird zu `ffmpeg -i "{url}"` (`FfmpegLiveStreamSegmenter.BuildArguments` Z. 378–379). |
| `IsPipe` | `bool` | `true` bei Pipe-Eingang (steuert `-nostdin`). |

### `LiveStreamExitInfo`

Datei: `VideoWebPlayer/Services/LiveStreamExitInfo.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SlotId` | `long` | Sendeplatz des unerwartet beendeten Laufs. |
| `ExitCode` | `int` | ffmpeg-Exit-Code (−1 wenn unbestimmbar). |
| `HttpTransportFailure` | `bool` | `true` wenn stderr HTTP-Transportmarker enthielt (nur bei `HttpUrl`-Eingang ausgewertet) → `LiveStreamCoordinator` schaltet denselben Slot auf Pipe um. Ein flag-bedingtes 404 des Endpunkts würde ffmpeg-stderr-Marker „Server returned 404…" erzeugen und hier landen. |
| `OccurredAtUtc` | `DateTimeOffset` | Erkennungszeitpunkt. |
