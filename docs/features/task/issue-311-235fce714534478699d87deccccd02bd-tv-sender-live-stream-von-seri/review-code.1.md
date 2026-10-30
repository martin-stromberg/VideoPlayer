# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

**Warnung zum Basisbranch:** `git merge-base HEAD main` liegt auf `a82e0fc` — der
Branch enthält die gesamte Vorgänger-Historie (hunderte Diff-Dateien). Geprüft wurden
deshalb die Änderungen der Feature-Commits `8a6b914..HEAD` (`20f3166`, `47795bd`,
`c816555`, `08eb89e`), die der Auftrag als relevant benennt.

## Befunde

### FfmpegLiveStreamSegmenter.cs (FfmpegLiveStreamSegmenter)

- **Fehlerbehandlung / fachliche Fehlklassifikation** — `ReportProcessExit`
  (Z. 327–332): `UndecodableInput` wird allein aus stderr-Markern gesetzt, ohne das
  Ergebnis der Eingabe-Pumpe zu berücksichtigen — obwohl `pumpResult` (Z. 334) bereits
  verfügbar ist. Bei einem Pipe-Eingang, dessen Pumpe mit `SourceUnavailable`,
  `Failed` oder `Truncated` endet (transiente Quell-Störung, z. B. SFTP-Verbindungsabbruch
  beim `OpenFileStream`/`ReadAsync`), bekommt ffmpeg zwangsläufig keine oder nur Teildaten
  und bricht mit genau den Demux-Markern ab („partial file", „Invalid data found when
  processing input", „Cannot determine format", „moov atom not found"). Die Marker sind
  dann die *erwartete Folge des Pump-Abbruchs*, kein Beweis für ein nicht-decodierbares
  Medium. Folge: `LiveStreamCoordinator` überspringt den Sendeplatz bis zu seinem Ende
  (`InputUnplayable`), obwohl ein Retry Sekunden später sehr wahrscheinlich erfolgreich
  wäre — bei einem Spielfilm ein vermeidbarer Totalausfall des Slots. Das neue Verhalten
  ist hier strikt schlechter als die alte Restart-Logik.

  Empfehlung: `undecodableInput` nur setzen, wenn kein Pipe-Eingang vorliegt oder
  `pumpResult?.Outcome == SegmenterPumpOutcome.Completed` (bzw. `pumpResult is null`);
  bei Pump-Fehlschlag `UndecodableInput = false` lassen, damit der Coordinator normal
  neu versucht.

### SegmenterProcessSupport.cs (SegmenterProcessSupport)

- **Fehlerbehandlung** — `PumpInputAsync` (Z. 193–200): der Catch für
  `IOException`/`ObjectDisposedException`/`OperationCanceledException` meldet
  `Completed`, auch wenn die Ausnahme aus `source.ReadAsync` stammte und
  `bytesPumped < expectedBytes` gilt — die Truncated-Prüfung (Z. 182–188) wird durch
  die Ausnahme umgangen. Folge in `LiveStreamStandbySegmenter.PumpInputAsync`
  (LiveStreamStandbySegmenter.cs:259–265): `Completed` → kein `process.Kill()` → ffmpeg
  transkodiert das Teilmaterial zu Ende und die unvollständigen Standby-Segmente werden
  übernommen — exakt der Fall, den der neue `Truncated`-Pfad verhindern soll
  („ein abgebrochener Pump darf kein Teilmaterial übernehmen").

  Empfehlung: auch im Catch-Pfad `expectedBytes` gegen `bytesPumped` prüfen und bei
  Unterschreitung `Truncated` zurückgeben (nur `bytesPumped >= expected` oder unbekannte
  Länge als `Completed`), oder Lese- und Schreib-Seite getrennt behandeln.

### LiveStreamStandbyProvider.cs (LiveStreamStandbyProvider)

- **Fehlerbehandlung / fehlende Fallback-Stufe** — `ResolveMediaInputDefaultAsync`
  (Z. 311–312): die Standby-Auflösung ruft den Resolver mit `useSftpInput: true` ohne
  eigene Rückfallebene. Scheitert der darauf folgende `TranscodeAsync`-Lauf am
  sftp-Eingang (ffmpeg ohne libssh → „Protocol not found", oder Transport-/Auth-Fehler),
  wird `_failedMediaItemId` gesetzt und der Standby-Clip **dauerhaft** nie vorbereitet —
  der funktionierende `pipe:0`-Fallback wird im Gegensatz zum Live-Pfad
  (`SftpInputSupported`-Latch + Koordinator-Fallback) und zur Laufzeitprobe
  (`UriUnsupported` → `ProbeStreamAsync`) nie versucht.

  Empfehlung: nach einem gescheiterten sftp-Transcode die Auflösung mit
  `useSftpInput: false` einmal erneut versuchen, bevor das Item als fehlgeschlagen
  gelatcht wird.

### LiveStreamCoordinator.cs (LiveStreamCoordinator)

- **Race Condition** — `SynchronizeStreamAsync` (Z. 115–132) liest
  `_segmenter.LastUnexpectedExit` dreimal hintereinander (Undecodable-, HTTP-Transport-
  und sftp-Fehler-Prüfung). Zwischen den Reads kann der Drain-Task des Segmenters einen
  neuen Exit melden, sodass die drei Klassifikationen auf unterschiedliche Exits zeigen.

  Empfehlung: den Wert einmal in eine lokale Variable lesen und alle Prüfungen dagegen
  auswerten.

### LiveStreamWorker.cs (LiveStreamWorker)

- **Logik** — `_lastPersistentResult` (Z. 117–128) wird bei anderen Actions
  (`Started`, `Unchanged`, `FfmpegUnavailable`, `Stopped`) nicht zurückgesetzt. Da das
  `MediaUnavailable`-Detail nur die `MediaItemId` enthält (keine `SlotId`), wird ein
  identischer Auflösungsfehler desselben Mediums in einem *späteren anderen* Sendeplatz
  nach einem Zwischenzustand fälschlich auf `Debug` dedupliziert, obwohl es ein neues
  Ereignis ist.

  Empfehlung: `_lastPersistentResult` bei allen nicht-persistierenden Actions
  zurücksetzen, sodass nur unmittelbar aufeinanderfolgende Gleichergebnisse dedupliziert
  werden.

### InternalMediaStreamTokenService.cs (InternalMediaStreamTokenService)

- **Race Condition** — `ResolveBaseUrl` (Z. 157–234): die Cache-/Warn-Felder
  `_baseUrlResolved`, `_resolvedBaseUrl`, `_noAddressWarned`, `_invalidConfigWarned`
  (Z. 62–65) werden unsynchronisiert gelesen und geschrieben. Der Service ist Singleton
  und kann parallel aus Laufzeitprobe und Live-Stream-Auflösung aufgerufen werden;
  ein torn read (`_baseUrlResolved == true` bei noch nicht sichtbarem
  `_resolvedBaseUrl`) liefert einmalig `null` statt der Basis-URL, außerdem sind
  doppelte Warnungen möglich. Folgen sind harmlos (selbstheilender Fallback), aber
  trivial behebbar.

  Empfehlung: `Volatile.Read`/`Volatile.Write` oder ein einfaches `lock` um die
  Auflösung legen.

### LiveStreamInput.cs (LiveStreamInput)

- **Struktur / Konsistenz** — der öffentliche positionsbasierte Record-Konstruktor
  (Z. 23–28) erlaubt inkonsistente Zustände, die Fabrikmethoden nie erzeugen:
  `new LiveStreamInput(null, openStream, HttpUrl: "…")` ergibt `IsPipe == true`
  (stdin wird umgeleitet, die Pumpe läuft, kein `-nostdin`), während `InputSpec` und
  `InputKindLabel` die URL wählen — ffmpeg bekommt die URL als `-i` und eine sinnlos
  befüllte stdin-Pipe. Umgekehrt liefert `new LiveStreamInput(null, null)` das Label
  `pipe:0`, obwohl gar kein Pump-Input existiert.

  Empfehlung: Instanzen nur über die Fabrikmethoden erzeugbar machen (Konstruktor
  `internal` bzw. private Factory-Schnittstelle) oder die Priorisierung von
  `IsPipe`/`InputSpec`/`InputKindLabel` konsistent an einer Stelle definieren.

### FfprobeMediaDurationProbe.cs / FfmpegErrorMarkers.cs

- **Doppelter Code** — `FfprobeMediaDurationProbe` hält mit `PermanentErrorMarkers`
  (Z. 30–34) und `ProtocolNotFoundMarker` (Z. 40) eigene Teilmengen der Marker, obwohl
  `FfmpegErrorMarkers` in diesem Branch ausdrücklich als geteilte Marker-Sammlung der
  ffmpeg/ffprobe-Familie eingeführt wurde — die HTTP-Transport-Marker wurden bereits
  dorthin delegiert, die beiden anderen nicht.

  Empfehlung: `ContainsPermanentErrorMarker` auf
  `FfmpegErrorMarkers.ContainsUndecodableInputError` und den Scheme-Check auf
  `ContainsUnsupportedSchemeError` umstellen — oder, falls die engere Teilmenge für die
  Probe-Klassifikation bewusst gewählt ist, das kurz im Kommentar begründen.

## Geprüfte Dateien

- `VideoWebPlayer/Configuration/LiveStreamOptions.cs`
- `VideoWebPlayer/Services/FfmpegErrorMarkers.cs`
- `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`
- `VideoWebPlayer/Services/ILiveStreamSegmenter.cs`
- `VideoWebPlayer/Services/InternalMediaStreamTokenService.cs`
- `VideoWebPlayer/Services/LiveStreamCoordinator.cs`
- `VideoWebPlayer/Services/LiveStreamExitInfo.cs`
- `VideoWebPlayer/Services/LiveStreamInput.cs`
- `VideoWebPlayer/Services/LiveStreamMediaResolver.cs`
- `VideoWebPlayer/Services/LiveStreamStandbyProvider.cs`
- `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs`
- `VideoWebPlayer/Services/LiveStreamWorker.cs`
- `VideoWebPlayer/Services/MediaDurationProbeService.cs`
- `VideoWebPlayer/Services/SegmenterProcessSupport.cs`
- `VideoWebPlayer/Services/SftpProbeUriBuilder.cs`
- `VideoWebPlayer/appsettings.json`
- `VideoWebPlayer.Tests/Helpers/TruncatingReadStream.cs`
- `VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs`
- `VideoWebPlayer.Tests/LiveStreamWorkerTests.cs`
- `VideoWebPlayer.Tests/Services/FfmpegErrorMarkersTests.cs`
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs`
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs`
- `VideoWebPlayer.Tests/Services/InternalMediaStreamTokenServiceTests.cs`
- `VideoWebPlayer.Tests/Services/LiveStreamMediaResolverTests.cs`
- `VideoWebPlayer.Tests/Services/LiveStreamStandbyProviderTests.cs`
- `VideoWebPlayer.Tests/Services/LiveStreamStandbySegmenterTests.cs`
- `VideoWebPlayer.Tests/Services/SegmenterProcessSupportTests.cs`
