# Datenmodell — Bestandsaufnahme

## Setup

Datei: `VideoWebPlayer/Data/Setup.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `Id` | `int` | Primärschlüssel des Setup-Datensatzes. |
| `TvSenderEnabled` | `bool` | Admin-Freigabeschalter (Feature-Flag) des TV-Senders; steuert Navigation, `/live-tv`, `/api/program-guide`, `/api/live-stream`, `/api/internal/media-stream` und die Sender-Worker. Standard: `false` (Zeile 80). |
| `LiveStreamSegmentLengthSeconds` | `int` | Ziel-Segmentlänge der HLS-Segmentierung in Sekunden. Standard: `6` (Zeile 105). |
| `LiveStreamSegmentDirectory` | `string?` | Admin-konfiguriertes Segment-Verzeichnis; `null`/leer = Standard `Data/LiveStream` unter dem Content-Root, relative Pfade werden gegen den Content-Root aufgelöst (Zeile 113). |
| `LiveStreamSegmentRetentionMinutes` | `int` | Aufbewahrungsdauer abgelaufener HLS-Segmente in Minuten für den Retention-Sweep. Standard: `30` (Zeile 119). |

Keine Standby-/Warm-up-bezogenen Eigenschaften vorhanden.

## TvSenderUserAccess

Datei: `VideoWebPlayer/Data/TvSenderUserAccess.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `UserId` | `string` | Benutzer-ID mit expliziter Sender-Freigabe (Allowlist; Admins sind implizit freigegeben). |

## LiveStreamJob (Record)

Datei: `VideoWebPlayer/Services/LiveStreamJob.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `SlotId` | `long` | ID des `ProgramSlot`, zu dem der Segmentierungs-Job gehört. |
| `Input` | `LiveStreamInput` | Medieneingang (Dateipfad, HTTP-URL oder Pipe-Stream). |
| `PositionSeconds` | `double` | Startposition im Medium (`-ss`). |
| `RemainingSeconds` | `double` | Maximale Ausgabedauer (`-t`, Rest-Sendezeit des Slots). |
| `SegmentSeconds` | `int` | Ziel-HLS-Segmentlänge in Sekunden. |
| `SegmentDirectory` | `string` | Verzeichnis, in das Segmente und Playlist geschrieben werden. |
| `FfmpegPath` | `string` | Aufgelöster ffmpeg-Pfad. |

## LiveStreamInput (Record)

Datei: `VideoWebPlayer/Services/LiveStreamInput.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `FilePath` | `string?` | Lokaler Dateipfad für direkten ffmpeg-Dateieingang (schnelles `-ss`-Seeking). |
| `OpenStreamAsync` | `Func<CancellationToken, Task<Stream?>>?` | Fabrik, die den Mediastream für den Pipe-Eingang (`pipe:0`) öffnet. |
| `HttpUrl` | `string?` | Range-fähige URL des internen Media-Stream-Endpunkts (`api/internal/media-stream/{token}`). |
| `IsPipe` | `bool` | Abgeleitet: `true`, wenn `OpenStreamAsync` gesetzt ist. |
| `ForFile(...)` | `static` | Erzeugt einen Dateipfad-Eingang. |
| `ForStream(...)` | `static` | Erzeugt einen Stdin-Stream-Eingang. |
| `ForHttp(...)` | `static` | Erzeugt einen Range-fähigen HTTP-URL-Eingang. |

## LiveStreamExitInfo (Record)

Datei: `VideoWebPlayer/Services/LiveStreamExitInfo.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `SlotId` | `long` | Sendeplatz des unerwartet beendeten Jobs. |
| `ExitCode` | `int` | Prozess-Exit-Code (`-1` bei unbekannt). |
| `HttpTransportFailure` | `bool` | `true`, wenn der stderr-Tail als HTTP-Transportfehler klassifiziert wurde (nur dann hilft Pipe-Fallback). |
| `OccurredAtUtc` | `DateTimeOffset` | Erkennungszeitpunkt des Exits. |

## LiveStreamSyncResult (Record)

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` (Zeile 31)

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `Action` | `LiveStreamSyncAction` | Entscheidung des Synchronisations-Ticks. |
| `WakeAtUtc` | `DateTimeOffset?` | Nächster Entscheidungszeitpunkt (Slot-Ende/-Start); `null` = regulärer Poll-Takt. |
| `Detail` | `string?` | Menschenlesbares Detail fürs Logging. |

## DtoProgramGuideNow / DtoProgramGuideEntry (Client-DTOs)

Dateien: `VideoWebPlayer.Client/Models/DtoProgramGuideNow.cs`, `VideoWebPlayer.Client/Models/DtoProgramGuideEntry.cs`

`DtoProgramGuideNow`: `ServerTimeUtc` (`DateTimeOffset`), `Now` (`DtoProgramGuideEntry?`, `null` in Sendelücke), `Next` (`DtoProgramGuideEntry?`).

`DtoProgramGuideEntry` (Auswahl stream-relevanter Felder): `SlotId` (`long`), `MediaItemId` (`long`), `StartUtc`/`EndUtc` (`DateTimeOffset`), `DurationSeconds` (`double`), `PositionSeconds`/`RemainingSeconds` (`double?`, nur für laufende Sendung), dazu Anzeigefelder (`Title`, `ShowName`, `SeasonNumber`, `EpisodeNumber`, `PosterPictureId`, `Description`, `GenreName`, `MediaType`, `MediaId`, `IsFiller`).

## LiveStreamOptions (Konfiguration, nicht DB)

Datei: `VideoWebPlayer/Configuration/LiveStreamOptions.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `UseHttpInput` | `bool` | Ob Remote-Medien über den range-fähigen internen Media-Stream-Endpunkt an ffmpeg gehen (Standard `true`); `false` = Pipe-Eingang `pipe:0`. Konfigurationssektion `LiveStream` (appsettings), keine DB-Spalte. |
