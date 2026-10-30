# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Die Überarbeitung (Runde 3) hat alle 5 Lücken und alle Hinweise aus `plan-check.1.md`
korrekt und verifizierbar eingearbeitet: Die `_baseUrlResolved`-Cache-Semantik ist jetzt
korrekt beschrieben und geplant (`InternalMediaStreamTokenService.cs:153` setzt den Latch
tatsächlich unbedingt; die Kommentare Z. 57-59/184-186 beschreiben den Intent — verifiziert),
`_invalidConfigWarned` ist als Ersatz-Einmal-Latch für die ungültige-`HttpProbeBaseUrl`-Warnung
(Z. 165-168) richtig durchdacht, `LiveStreamWorkerTests.FakeSegmenter` (exakt
`LiveStreamWorkerTests.cs:31-42`), die Call-Site `LiveStreamStandbyProvider.cs:311`
(positionales `CancellationToken` → CS1503), `tv-sender-ablauf-technisch.md` und der
Worker-Dedup samt Test sind vollständig eingeplant. Alle behaupteten Bruchstellen wurden am
Code nachgeprüft und sind vollständig abgedeckt (Details unter „Hinweise"). Drei kleinere,
konkrete Lücken verhindern „vollständig": eine fehlende Hilfe-Doku-Aktualisierung, ein nicht
benanntes Test-Fixture und ein fehlender Test zum geplanten Einmal-Latch.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Serien-/Film-Sendeplätze aus entfernten Quellen segmentiert (Exit 0 am Slotende) | Phase 1 Schritt 1: `Kestrel:Endpoints`-Stufe in `ResolveBaseUrl` → `ForHttp` (seekbar, HTTP-Range über `InternalMediaStreamController`); Phase 2 `ForSftp`-Stufe | 9 neue `InternalMediaStreamTokenServiceTests` (In-Memory-Config: Kestrel-Treffer, freier Endpunktname, https-vor-http-Skip, Http3-Skip, Port-0, BoundAddress-wins, https-only, Retry/Cache); E2E `RealFfmpeg_MoovAtEndFileInputWithOffset_ProducesSegments` (seekbarer Analog-Nachweis); bestehende `InternalMediaStreamControllerTests` (Range-206) | Abgedeckt — Root Cause am Code belegt: kein `UseUrls`/`ASPNETCORE_URLS` im Projekt, Bindung ausschließlich über `Kestrel:Endpoints` (`appsettings.Production.json:22-26` → `http://*:5002`), damit bleibt `IServerAddressesFeature.Addresses` leer |
| 2. Ursache des `pipe:0`-Wegs festgestellt und dokumentiert | Root Cause im Plan dokumentiert (verifiziert); `ResolutionNote` am `LiveStreamInput`, Transport ins `Started`-`Detail`; Warnungstext benennt beide Verbraucher + geprüfte Wege | `ResolveAsync_PipeFallback_ResolutionNoteNamesCause`, `SynchronizeAsync_RemotePipeFallback_StartedDetailNamesInputKindAndNote`, `SynchronizeAsync_RemoteMedia_StartedDetailNamesHttpInput` | Abgedeckt |
| 3. `pipe:0`-Fallback: Fehler erkannt, geloggt, keine Restart-Schleife | `ContainsUndecodableInputError` (Marker decken exakt die Produktiv-Meldungen) + `InputUnplayable`-Skip (`WakeAtUtc = now.EndUtc`, `StopAsync`); Phase 2 `SftpInputFailure`/`SftpInputSupported`-Latch; Transport-Flags vor Demux-Flags | Marker-Tests, `UnexpectedExit_*DemuxMarkers_SetsUndecodableInput` (Pipe + Datei), `SynchronizeAsync_UndecodableInputSameSlot_SkipsSlotUntilEnd` / `..._OtherSlot_IgnoresFailure`, Worker-Dedup-Test; E2E `RealFfmpeg_MoovAtEndPipeInput_FlagsUndecodableInput` + `RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure` | Abgedeckt |
| 4. Unvollständiger Pump von moov-Problem unterscheidbar | `SegmenterPumpResult` (`BytesPumped`/`ExpectedBytes` via `CanSeek`/`Length` — `SftpStreamWrapper` delegiert beides korrekt an den seekbaren `SftpFileStream`), Outcome `Truncated`, Bytezahlen in Pump-Logs + Exit-Warnung | `PumpInputAsync_Success_ReportsPumpedAndExpectedBytes`, `..._NonSeekableSource_CompletedWithoutExpectedBytes`, `PumpInputAsync_ShortStream_ReturnsTruncated`, `UnexpectedExit_PipeInput_IncludesPumpStatsInWarning` | Abgedeckt |
| 5. Bestehendes Verhalten unverändert | Stufenreihenfolge HttpProbeBaseUrl → Addresses → Kestrel bleibt; `ForFile`/HTTP/`append_list`/Maskierung unverändert; bewusste Abweichungen (Cache-Korrektur, Port-≤0-Ablehnung, Log-Dedup, sftp-Stufe) sind ausdrücklich benannt | `TryCreateStreamUri_BoundAddressWins_OverKestrelEndpoint`, `..._NoAddressNoKestrel_ReturnsNull`, `ResolveAsync_SftpMissingCredentials_FallsBackToPipe`, `UnexpectedExit_SftpInput_MasksCredentialsInStderr`, `TryCreateProbeUri_PositiveResult_StaysCached`; unveränderte Playlist-E2Es | Abgedeckt |
| 6. Tests decken behobenen Pfad; Builds grün | Schritte 7/8/17/18 (Unit-/E2E-Tests) + Schritt 20 (Debug+Release-Build, volle Suite) | Testtabellen vollständig; betroffene Bestandstests verifiziert komplett (s. Hinweise) | Abgedeckt — mit den unten genannten Rest-Lücken |

## Fehlende oder unvollständige Testanforderungen

- [ ] Kein Test für den geplanten `_invalidConfigWarned`-Einmal-Latch: Mit der korrigierten Cache-Semantik (Latch nur bei positivem Ergebnis) würde eine ungültige `MediaProbe:HttpProbeBaseUrl` ohne Latch **bei jedem Aufruf** erneut warnen (`InternalMediaStreamTokenService.cs:165-168`) — genau die Regression, für die der Latch explizit geplant ist; ein `ListLogger`-Test „ungültige Basis-URL + zwei Aufrufe ⇒ eine Warnung" fehlt in der Testtabelle (Bezug AK 5/bewusste Verhaltensänderung).
- [ ] Test-Fixture unvollständig benannt: `TranscodeAsync_TruncatedPumpInput_KillsProcessAndReturnsNull` (`LiveStreamStandbySegmenterTests`) braucht ebenfalls eine truncating-Quelle (seekbar, `Length` > gelieferte Bytes) — `TruncatingReadStream` ist als private Hilfsklasse nur in `SegmenterProcessSupportTests` eingeplant und kann als private nested class nicht dateiübergreifend geteilt werden (Bezug AK 4).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kein UI-Benutzerfluss (Server-Hintergrundlogik, keine Razor-/Playwright-Änderung) | Begründung im Plan: Prozess-Ebene via `FfmpegLiveStreamSegmenterE2ETests` (echtes ffmpeg vorhanden, `E2EBinaryLocator`) | Nicht erforderlich mit Begründung — nachvollziehbar |
| Reproduktion des Produktiv-Fehlschlags: moov-am-Ende-MP4 über `pipe:0` ⇒ `UndecodableInput` (AK 3 + 4) | `RealFfmpeg_MoovAtEndPipeInput_FlagsUndecodableInput` | Abgedeckt — Prämisse verifiziert: `GenerateVideoAsync` erzeugt MP4 ohne faststart (moov am Ende), der Kommentar in `FfmpegLiveStreamSegmenterE2ETests.cs:159-160` bestätigt die Falle empirisch; `RemainingSeconds` > Laufzeit + Toleranz sichert die „unerwartet"-Klassifikation |
| Seekbarer Eingang segmentiert moov-am-Ende-MP4 inkl. `-ss` (AK 1) | `RealFfmpeg_MoovAtEndFileInputWithOffset_ProducesSegments` (`ForFile`, `PositionSeconds > 0`, `LastUnexpectedExit == null`) | Abgedeckt — deckt `ForHttp`/`ForSftp` argumentgleich mit ab (identische `-i`-Behandlung); Loopback-Erreichbarkeit selbst nicht prozessübergreifend bewiesen — vom Plan begründet, Restrisiko benannt |
| sftp-Fehlschlag fällt gestuft zurück; Build-Fähigkeit (libssh) erkannt (AK 3 + 5) | `RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure` (`sftp://u:p@127.0.0.1:1/x`) | Abgedeckt — Assertion toleriert beide Ausgänge (`SftpInputFailure` bzw. `SftpInputSupported == false`), da das Test-ffmpeg-build unbekannt ist |

## Fehlende oder unvollständige Planbestandteile

- [ ] **Hilfe-Dokument `docs/help/videolaufzeit.md` nicht eingeplant:** Die Datei beschreibt genau den geänderten Mechanismus — Z. 8: „Die Loopback-Adresse wird aus den gebundenen Adressen der laufenden Instanz ermittelt", Z. 55: „automatische Erkennung aus den gebundenen Server-Adressen" — beides wird mit der `Kestrel:Endpoints`-Stufe unvollständig; `TryCreateProbeUri` (ffprobe-HTTP-Probe) profitiert laut Plan ausdrücklich vom selben Fix. Der Dokumentationsschritt (9) nennt nur `tv-sender.md`, `tv-sender-ablauf-technisch.md` und `RELEASE_NOTES.md` — dieselbe Lückenklasse wie der in Runde 2 beanstandete `tv-sender-ablauf-technisch.md`-Punkt (betrifft AK 2, Dokumentation der Erkennungskette).

## Hinweise

- **`Protocols`-Vergleich präzisieren:** Der Plan behauptet, der Substring-Check auf `Http1`/`Http2` decke „`Http1AndHttp2`/`Http1AndHttp2AndHttp3`/`All`" ab — `HttpProtocols` hat **kein** `All`-Member (nur `None`/`Http1`/`Http2`/`Http1AndHttp2`/`Http3`/`Http1AndHttp2AndHttp3`); ein `All`-Wert scheitert ohnehin am Kestrel-Config-Binding — harmlos, aber ungenau. Zusätzlich sollte der Vergleich `OrdinalIgnoreCase` sein (Konfigurationswerte wie `http3` sind für Kestrel gültig). Fehlgriffe sind unkritisch, da der HTTP-Transport-Fallback auffängt.
- **Alle Bruchstellen vollständig verifiziert:** `ResolveAsync`-Aufrufer = `LiveStreamCoordinator.cs:116` (positionales `cancellationToken` → CS1503, Schritt 16), `LiveStreamStandbyProvider.cs:311` (Schritt 13), Test-Aufrufe durchgängig mit benanntem `cancellationToken:` (kompilierfähig); `ILiveStreamSegmenter`-Implementierer = genau `FfmpegLiveStreamSegmenter` + beide benannten Test-Fakes (Testing-/E2E-Umgebung hostet `LiveStreamWorker` nicht — `ServiceCollectionExtensions.cs:336-339`); `PumpInputAsync`-Aufrufer = genau `FfmpegLiveStreamSegmenter.PumpInputAsync` + `LiveStreamStandbySegmenter.PumpInputAsync` + die fünf benannten `SegmenterProcessSupportTests` (`FfprobeMediaDurationProbe.cs:315` bestätigt: eigener Pump-Pfad); `new LiveStreamExitInfo(...)`-Stellen (Coordinator-Tests, `FfmpegLiveStreamSegmenter.cs:304`) bleiben mit optionalen Positionsparametern kompilierfähig; `InputSpec`/`NoStdinArg`/`SourceDescriptor`/`IsPipe`-Semantik für `SftpUrl` konsistent (`-nostdin`, kein Pump-Task, `-i "sftp://..."`).
- **`IConfiguration`-DI verifiziert:** `IConfiguration` steht in DI bereit (Präzedenz `DiscoveryBaseUrlResolver`, registriert `ServiceCollectionExtensions.cs:321`; `MdnsServiceProfileBuilder.cs:61` liest `Kestrel:Endpoints:Http:Url` bereits — das zitierte Vorbild existiert).
- **Zwischenschritt-Build:** Zwischen Schritt 13 (`useSftpInput`-Parameter) und Schritt 16 (Coordinator-Aufruf) kompiliert der Stand nicht — bei schrittweisen Commits ist das ein bewusst nicht-buidbarer Zwischencommit; der Plan benennt es, akzeptabel.
- **`LiveStreamWorkerTests.FakeSegmenter.LastUnexpectedExit`** ist aktuell `=> null` (read-only) — der Worker-Dedup-Test braucht es setzbar; implizit durch das neue Member abgedeckt, bei Umsetzung mitbedenken.
- **Zeilenangabe `tv-sender-ablauf-technisch.md`:** Der genannte Pump-Abschnitt läuft real ca. Z. 357-451 (Plan: 357-439); `~Z. 328` (`ResolveAsync`-Erwähnung in der Standby-Auflösung) trifft zu.
- **Bestehender per-Tick-Infolog:** `LiveStreamCoordinator.cs:124-129` loggt den HTTP-Fallback-Hinweis **jeden** ~30-s-Tick, solange `useHttpInput == false` — dieselbe Spam-Klasse, die der neue Dedup für `InputUnplayable`/`MediaUnavailable` löst; der Plan passt hier nur den Text an (optional mitdeduplizieren).
- **`"sftp"`-`SourceDescriptor`** (`LiveStreamStandbyProvider.cs:397-398`) bekommt keinen eigenen Test — die einmalige Cache-Invalidierung ist bewusst akzeptiert, aber ungetestet (klein).
- **`LiveStreamInput`-ToString:** Der Record-ToString würde `SftpUrl` inkl. Credentials ausgeben, falls die Eingabe je direkt geloggt wird — heute passiert das nirgends (`Started`-`Detail` und `ffmpeg-Aufruf`-Debug sind maskiert bzw. ohne URL); gleiche Expositionsklasse wie `HttpUrl`/Token bisher.
- **`ResolveRemotePath`-Verlagerung:** private Methode `MediaDurationProbeService.cs:358-367` existiert wie beschrieben; Regression ist über die bestehenden `MediaDurationProbeServiceTests` (sftp-Pfad) mittelbar gedeckt.
- **Testing-Umgebung `Kestrel:Endpoints:Http:Url = http://127.0.0.1:0` (`appsettings.Testing.json:8`):** Die Port-≤0-Ablehnung ist zwingend, damit Stufe 3 dort keine unbrauchbare `http://127.0.0.1:0`-Basis liefert — korrekt eingeplant und getestet.
