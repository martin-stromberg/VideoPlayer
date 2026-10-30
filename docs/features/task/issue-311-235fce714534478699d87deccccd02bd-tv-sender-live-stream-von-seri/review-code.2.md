# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Geprüft wurde `git diff 8a6b914..HEAD` (Feature-Commits inkl. der acht
Nachbesserungs-Commits `a33cd1e`…`afaaeff` der Iteration 2).

**Verifikation der Iteration-1-Befunde:** Alle acht Befunde sind korrekt behoben —
(1) `UndecodableInput` ist jetzt an das Pump-Ergebnis gekoppelt
(`FfmpegLiveStreamSegmenter.cs:333`), (2) `PumpInputAsync` trennt Lese-/Schreibseite
und meldet Quell-Abbrüche als `Truncated`/`Failed` statt `Completed`
(`SegmenterProcessSupport.cs:181`–226), (3) Standby-Pfad hat einen
sftp→pipe-Fallback (`LiveStreamStandbyProvider.cs:252`–298), (4) `LastUnexpectedExit`
wird nur einmal pro Tick gelesen (`LiveStreamCoordinator.cs:113`), (5)
`_lastPersistentResult` wird bei Zwischenzuständen zurückgesetzt
(`LiveStreamWorker.cs:117`), (6) `ResolveBaseUrl` läuft unter `lock`
(`InternalMediaStreamTokenService.cs:163`), (7) `LiveStreamInput` ist nur über
Fabrikmethoden erzeugbar (`LiveStreamInput.cs:27`), (8) ffprobe-Marker sind auf
`FfmpegErrorMarkers` delegiert (`FfprobeMediaDurationProbe.cs:169`–192).

**Tests:** 220 betroffene Tests grün (Debug-Build fehlerfrei). Mutationsprobe:
Ohne die Pump-Ergebnis-Kopplung schlagen
`UnexpectedExit_PipePumpSourceUnavailable_DemuxMarkersNoUndecodableInput` und
`UnexpectedExit_PipePumpTruncated_DemuxMarkersNoUndecodableInput` fehl — die
Absicherung greift tatsächlich.

## Befunde

### LiveStreamCoordinator.cs (LiveStreamCoordinator)

- **Logik / unvollständige Fallback-Kaskade** — `SynchronizeStreamAsync`
  (Z. 130–141): `useHttpInput` und `useSftpInput` werden jeweils nur aus dem
  *letzten* Exit abgeleitet — das Gedächtnis ist genau einen Tick tief. Für eine
  SFTP-Quelle mit auflösbarer HTTP-Basis-URL ergibt sich bei anhaltendem
  HTTP-Transportfehler **und** anhaltendem sftp-Transportfehler eine Endlos-
  Alternation: HTTP scheitert (Tick 1) → sftp scheitert (Tick 2) → `useHttpInput`
  ist wieder `true` (Z. 130), `useSftpInput` wieder `false` → HTTP scheitert
  (Tick 3) → sftp scheitert (Tick 4) → … Die deklarierte „last resort"-Stufe
  `pipe:0` (Z. 133: „the piped stream is the last resort") wird nur erreicht,
  wenn beide Flags im selben Tick `false` sind — das ist nur über den
  `SftpInputSupported`-Latch (Schema-Unsupported, kein Transportfehler) möglich.
  Ein Szenario, in dem ausschließlich der Pipe-Pfad funktioniert (z. B. SSH.NET
  kann den Server erreichen, ffmpegs libssh aber nicht — andere
  KEX/Verschlüsselungs-Implementierung), lässt den Sendeplatz dauerhaft tot,
  obwohl ein funktionierender Fallback existiert.

  Empfehlung: Fehlschläge pro Sendeplatz kumulieren statt nur den letzten Exit
  auszuwerten — z. B. `LiveStreamExitInfo` um die bereits gescheiterten
  Eingangsstufen des Slots erweitern oder im Koordinator eine
  Slot→Fehlstufen-Maske führen, sodass die Kaskade HTTP → sftp → pipe:0 über
  aufeinanderfolgende Ticks wirklich abgearbeitet wird.

### FfprobeMediaDurationProbe.cs (FfprobeMediaDurationProbe)

- **Fehlerbehandlung / fachliche Fehlklassifikation** — `ProbeAsync`
  (Z. 186–192): die Behebung von Befund 8 hat die Permanent-Marker auf die
  Obermenge `UndecodableInputErrorMarkers` („partial file", „Invalid data found
  when processing input", „Could not find codec parameters", „Cannot determine
  format", „moov atom not found") erweitert — ohne Transport-Vorrang für
  `sftp://`-Probes. Für `http(s)` existiert ein Transport-Check (Z. 178), für
  `sftp` nicht: Bricht der Datentransfer eines seekable-sftp-Probes mittendrin ab
  (Netzwerk-Störung), enthält der stderr-Tail typischerweise eine I/O-Meldung
  *und* eine Demux-Klage wie „Could not find codec parameters" — das Item wird
  als `PermanentFailure` gewertet und `ApplyOutcome` erschöpft das
  Versuchsbudget (`MediaDurationProbeService.cs:367`), bis ein Admin
  zurücksetzt — obwohl ein Retry erfolgreich wäre. Dasselbe
  Transient-gegen-Permanent-Problem, das Befund 1 der Iteration 1 im Segmenter
  monierte.

  Empfehlung: für URI-Probes vor dem Demux-Check auf Transport-/I/O-Marker
  prüfen (`FfmpegErrorMarkers.ContainsSftpInputError` ist bereits vorhanden und
  nur in dieser Stelle ungenutzt) und in dem Fall `TransientFailure` liefern —
  oder die engere Teilmenge für die Permanent-Klassifikation begründet
  beibehalten.

### FfmpegLiveStreamSegmenter.cs (FfmpegLiveStreamSegmenter)

- **Race Condition (Restfenster der Befund-1-Behebung)** — `ReportProcessExit`
  (Z. 317 i. V. m. Z. 333–336): `pumpResult` wird nur gelesen, wenn der
  Pump-Task zum Zeitpunkt der Exit-Klassifikation bereits
  `IsCompletedSuccessfully` ist. Der Drain wartet den Pump bewusst nicht ab —
  läuft die Pump-Task-Vervollständigung der stderr-Beendigung hinterher (z. B.
  `SourceUnavailable`-Rückgabe unter Thread-Pool-Druck), ist `pumpResult` `null`
  und `UndecodableInput` kann trotz eines faktisch fehlgeschlagenen Pumps gesetzt
  werden — der Coordinator überspringt dann den Sendeplatz, genau der
  Fehlschlag, den `a33cd1e` beseitigen sollte. Fenster ist klein, aber die
  Kopplung ist damit nicht vollständig.

  Empfehlung: den Pump-Task vor der Klassifikation zeitgebunden einholen
  (z. B. `await pumpTask.WaitAsync(kurze Schranke)` im Drain-Pfad — ein
  blockierter Pump kann bei einem beendeten Prozess nur noch auf den
  Pipe-Bruch warten, die Schranke bleibt also klein) oder
  „Pump läuft noch" explizit als eigenen, nicht-undecodable Fall behandeln.

### LiveStreamStandbyProvider.cs (LiveStreamStandbyProvider)

- **Fehlermeldung ohne korrekten Kontext / unvollständige Kaskade** —
  `PrepareAsync` (Z. 287–290): die Info-Meldung behauptet „versuche Fallback
  auf den gestreamten Eingang (pipe:0)", die Neauflösung läuft aber mit
  `useHttpInput: true` (`ResolveMediaInputDefaultAsync`, Z. 343–349) — liefert
  die Basis-URL eine URI, ist der Fallback faktisch der HTTP-Eingang, nicht
  `pipe:0`. Zudem endet die Standby-Kaskade nach einem Retry: Scheitert auch
  der HTTP-Transcode, wird `_failedMediaItemId` gelatcht, ohne den gestreamten
  Eingang zu versuchen — dieselbe unvollständige Kaskade wie im
  Koordinator-Befund, hier sogar ohne Alternation (permanent gelatcht).

  Empfehlung: Meldung neutral formulieren („Fallback auf die nächste
  Eingangsstufe") bzw. den tatsächlich gewählten `InputKindLabel` nennen; für
  die Vollständigkeit erwägen, nach einem gescheiterten HTTP-Eingang ebenfalls
  `pipe:0` zu versuchen (erfordert ein `useHttpInput`-Flag am
  `StandbyMediaInputResolver`-Delegate).

## Geprüfte Dateien

- `VideoWebPlayer/Configuration/LiveStreamOptions.cs`
- `VideoWebPlayer/Services/FfmpegErrorMarkers.cs`
- `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`
- `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs`
- `VideoWebPlayer/Services/ILiveStreamSegmenter.cs`
- `VideoWebPlayer/Services/InternalMediaStreamTokenService.cs`
- `VideoWebPlayer/Services/LiveStreamCoordinator.cs`
- `VideoWebPlayer/Services/LiveStreamExitInfo.cs`
- `VideoWebPlayer/Services/LiveStreamInput.cs`
- `VideoWebPlayer/Services/LiveStreamJob.cs` (referenziert, unverändert)
- `VideoWebPlayer/Services/LiveStreamMediaResolver.cs`
- `VideoWebPlayer/Services/LiveStreamStandbyProvider.cs`
- `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs`
- `VideoWebPlayer/Services/LiveStreamWorker.cs`
- `VideoWebPlayer/Services/LocalMediaSourceReader.cs` (referenziert)
- `VideoWebPlayer/Services/MediaDurationProbeService.cs`
- `VideoWebPlayer/Services/SegmenterProcessSupport.cs`
- `VideoWebPlayer/Services/SftpMediaSourceReader.cs` (referenziert)
- `VideoWebPlayer/Services/SftpProbeUriBuilder.cs`
- `VideoWebPlayer/appsettings.json`
- `VideoWebPlayer.Tests/Helpers/FailingReadStream.cs`
- `VideoWebPlayer.Tests/Helpers/TruncatingReadStream.cs`
- `VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs`
- `VideoWebPlayer.Tests/LiveStreamWorkerTests.cs`
- `VideoWebPlayer.Tests/Services/FfmpegErrorMarkersTests.cs`
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs`
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs`
- `VideoWebPlayer.Tests/Services/FfprobeMediaDurationProbeTests.cs`
- `VideoWebPlayer.Tests/Services/InternalMediaStreamTokenServiceTests.cs`
- `VideoWebPlayer.Tests/Services/LiveStreamMediaResolverTests.cs`
- `VideoWebPlayer.Tests/Services/LiveStreamStandbyProviderTests.cs`
- `VideoWebPlayer.Tests/Services/LiveStreamStandbySegmenterTests.cs`
- `VideoWebPlayer.Tests/Services/SegmenterProcessSupportTests.cs`
