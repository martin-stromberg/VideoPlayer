# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Die Überarbeitung (Runde 4) hat alle 5 Lücken aus `plan-check.1.md` und alle 3 Lücken aus
`plan-check.2.md` eingearbeitet — jede Einarbeitung wurde am Code verifiziert:

- **Cache-Semantik:** `InternalMediaStreamTokenService.cs:153` setzt `_baseUrlResolved`
  tatsächlich unbedingt (null wird prozessweit gecacht, entgegen den Kommentaren Z. 57-59,
  184-186); der Plan korrigiert jetzt zum dokumentierten Intent und ergänzt den
  `_invalidConfigWarned`-Latch für die Warnung Z. 165-168 samt Regressionstest.
- **`LiveStreamWorkerTests.FakeSegmenter`** (`LiveStreamWorkerTests.cs:31-42`, `LastUnexpectedExit`
  read-only `=> null` an Z. 37) ist für `SftpInputSupported` und als setzbar eingeplant.
- **Call-Sites mit positionalem `CancellationToken`:** `LiveStreamCoordinator.cs:116` und
  `LiveStreamStandbyProvider.cs:311` — beide CS1503-Brüche sind den Schritten 13/16 zugeordnet,
  das nicht-buildbare Zwischenfenster ist benannt. Alle Test-Aufrufe nutzen benannte
  `cancellationToken:`-Argumente (verifiziert).
- **Per-Tick-Fallback-Infolog** (`LiveStreamCoordinator.cs:124-129`) entfällt geplant — kein
  Test hängt daran (`NullLogger<LiveStreamCoordinator>` in `LiveStreamCoordinatorTests.cs:112`).
- **`TruncatingReadStream`** ist als `internal` Helper in `VideoWebPlayer.Tests/Helpers/`
  eingeplant (Muster `ThrottledStream`/`StalledStream` in `Helpers/FfmpegToolDownloadTestDoubles.cs:56,118`)
  und wird von `SegmenterProcessSupportTests` und `LiveStreamStandbySegmenterTests` geteilt.
- **`ToString()`-Maskierung** am positionalen Record `LiveStreamInput` nutzt die vorhandenen
  Helfer `MaskStreamTokenInText`/`MaskCredentialsInText`; aktuell wird der Record nirgends
  geloggt (verifiziert) — Absicherung korrekt eingeordnet.
- **`Protocols`-Check** (Substring `OrdinalIgnoreCase` auf `Http1`/`Http2`) ist jetzt korrekt
  beschrieben — kein `All`-Member, `Http1AndHttp2*` per Substring abgedeckt.
- **`AutoUpdate`-JsonKeys:** Die Bestandslücke ist real — `appsettings.json:154-211` enthält
  keinerlei `LiveStream:*`-/`MediaProbe:*`-Schlüssel; die vier Ergänzungen (inkl. eigenem
  Commit für die Bestandskorrektur) sind eingeplant. `Kestrel:Endpoints:Http:Url` ist bereits
  whitelistet (Z. 162).
- **Doku:** `videolaufzeit.md` (Z. 8 + `MediaProbe:HttpProbeBaseUrl`-Zeile ~Z. 55),
  `tv-sender.md` (~Z. 559-570 entfernte Quellen, ~Z. 747-752 moov-Hinweis) und
  `tv-sender-ablauf-technisch.md` (~Z. 328 `ResolveAsync`, Z. ~357-455 Pump-Abschnitt mit
  veralteten `SegmenterPumpOutcome`-Rückgaben) sind alle eingeplant — die drei Dateien sind
  auch die einzigen Hilfe-Dokumente mit `pipe:0`/`UseHttpInput`/`moov`-Bezug (verifiziert).
- **Implementierungsanleitung:** Jede Zeile der Änderungstabellen ist einem der 20 Schritte
  der Umsetzungsreihenfolge zugeordnet (beide Phasen vollständig abgebildet); eine
  `…-tasks.md` existiert für dieses Feature noch nicht (sie entsteht erst im
  Implementierungsschritt) — der Plan ist die konsistente Task-Quelle.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Serien-/Film-Sendeplätze aus entfernten Quellen werden segmentiert (Exit 0 am Slotende) | Phase 1 Schritt 1: `Kestrel:Endpoints`-Stufe in `ResolveBaseUrl` → `ForHttp` (seekbar via `InternalMediaStreamController`, Range-206); Phase 2 `ForSftp`-Stufe | 11 neue `InternalMediaStreamTokenServiceTests` (In-Memory-Config: Kestrel-Treffer, freier Endpunktname, https-vor-http-Skip, Http3-Skip, Port-0, BoundAddress-wins, https-only, kein-Treffer, Retry/Cache, Einmal-Warnung); E2E `RealFfmpeg_MoovAtEndFileInputWithOffset_ProducesSegments` (seekbarer Analog-Nachweis); bestehende `InternalMediaStreamControllerTests` | Abgedeckt — Root Cause verifiziert: kein `UseUrls`/`ASPNETCORE_URLS` im Projekt (nur `ConfigureKestrel` für Limits, `Program.cs:36-42`), Bindung ausschließlich über `Kestrel:Endpoints` (`appsettings.Production.json:22-26` → `http://*:5002`) |
| 2. Ursache des `pipe:0`-Wegs festgestellt und dokumentiert | Root Cause im Plan dokumentiert und produktiv bewiesen; `ResolutionNote` am `LiveStreamInput`, Transport ins `Started`-`Detail` (`InputKindLabel`); Warnungstext benennt beide Verbraucher + geprüfte Wege | `ResolveAsync_PipeFallback_ResolutionNoteNamesCause`, `SynchronizeAsync_RemotePipeFallback_StartedDetailNamesInputKindAndNote`, `SynchronizeAsync_RemoteMedia_StartedDetailNamesHttpInput` | Abgedeckt |
| 3. `pipe:0`-Fallback: Fehler erkannt, geloggt, keine Restart-Schleife | `ContainsUndecodableInputError` (Marker decken exakt die Produktiv-Meldungen `partial file`/`Could not find codec parameters`/`Cannot determine format`/`moov atom not found`) + `InputUnplayable`-Skip (`StopAsync`, `WakeAtUtc = now.EndUtc`, keine Per-Tick-Warnung) + Worker-Dedup; Phase 2 `SftpInputFailure`/`SftpInputSupported`-Latch; Transport-Flags vor Demux-Flags | Marker-Tests (`FfmpegErrorMarkersTests` neu), `UnexpectedExit_*DemuxMarkers_SetsUndecodableInput` (Pipe + Datei), `SynchronizeAsync_UndecodableInputSameSlot_SkipsSlotUntilEnd` / `..._OtherSlot_IgnoresFailure`, `ExecuteAsync_UndecodableSlotSkips_LogsInputUnplayableOnce`, sftp-Fallback-Tests (Credentials→sftp, SftpFailure→Pipe, Unsupported→Skip); E2E `RealFfmpeg_MoovAtEndPipeInput_FlagsUndecodableInput` + `RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure` | Abgedeckt |
| 4. Unvollständiger Pump von moov-Problem unterscheidbar | `SegmenterPumpResult` (`BytesPumped`/`ExpectedBytes` via `CanSeek`/`Length` — `SftpStreamWrapper` delegiert beides korrekt, `SftpStreamWrapper.cs:30,34`), Outcome `Truncated` + Warnung mit Bytevergleich, Bytezahlen in Pump-Logs und Exit-Warnzeile | `PumpInputAsync_Success_ReportsPumpedAndExpectedBytes`, `..._NonSeekableSource_CompletedWithoutExpectedBytes`, `PumpInputAsync_ShortStream_ReturnsTruncated` (`TruncatingReadStream`), `UnexpectedExit_PipeInput_IncludesPumpStatsInWarning`, `TranscodeAsync_TruncatedPumpInput_KillsProcessAndReturnsNull` | Abgedeckt |
| 5. Bestehendes Verhalten unverändert | Stufenreihenfolge HttpProbeBaseUrl → Addresses → Kestrel bleibt; `ForFile`/HTTP/`append_list`/Maskierung unverändert; bewusste Abweichungen (Cache-Korrektur, Port-≤0-Ablehnung, Per-Tick-Log-Entfernung, Worker-Dedup, sftp-Stufe) sind in Designentscheidungen/Risiken ausdrücklich benannt | `TryCreateStreamUri_BoundAddressWins_OverKestrelEndpoint`, `..._NoAddressNoKestrel_ReturnsNull`, `TryCreateProbeUri_PositiveResult_StaysCached`, `ResolveAsync_SftpMissingCredentials_FallsBackToPipe`, `UnexpectedExit_SftpInput_MasksCredentialsInStderr`; unveränderte Playlist-E2Es | Abgedeckt |
| 6. Tests decken den behobenen Pfad; Builds grün | Schritte 7/8/17/18 (neue Unit-/E2E-Tests, Anpassung betroffener Tests) + Schritt 20 (Debug+Release-Build, volle Suite) | Testtabellen vollständig; betroffene Bestandstests alle verifiziert (sieben Tabellenzeilen inkl. fünf `SegmenterProcessSupportTests`-Pump-Tests, beide `FakeSegmenter`, `CreateService`-Erweiterung) | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kein UI-Benutzerfluss (Server-Hintergrundlogik, keine Razor-/Playwright-Änderung) | Begründung im Plan: Prozess-Ebene via `FfmpegLiveStreamSegmenterE2ETests` (echtes ffmpeg vorhanden, `E2EBinaryLocator`) | Nicht erforderlich mit Begründung — nachvollziehbar |
| Reproduktion des Produktiv-Fehlschlags: moov-am-Ende-MP4 über `pipe:0` ⇒ `UndecodableInput` (AK 3 + 4) | `RealFfmpeg_MoovAtEndPipeInput_FlagsUndecodableInput` | Abgedeckt — `GenerateVideoAsync` (`FfmpegLiveStreamSegmenterE2ETests.cs:290-301`) erzeugt MP4 ohne `-movflags faststart` (moov am Ende); der Kommentar Z. 159-160 bestätigt die Falle empirisch |
| Seekbarer Eingang segmentiert moov-am-Ende-MP4 inkl. `-ss` (AK 1) | `RealFfmpeg_MoovAtEndFileInputWithOffset_ProducesSegments` (`ForFile`, `PositionSeconds > 0`, `LastUnexpectedExit == null`) | Abgedeckt — deckt `ForHttp`/`ForSftp` argumentgleich mit ab (identische quotierte `-i`-Behandlung); die Loopback-Erreichbarkeit des internen Endpunkts selbst ist über `InternalMediaStreamControllerTests` (Range-206) abgesichert, Restrisiko im Plan benannt |
| sftp-Fehlschlag fällt gestuft zurück; ffmpeg-Build-Eigenschaft (libssh) wird erkannt (AK 3 + 5) | `RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure` (`sftp://u:p@127.0.0.1:1/x`) | Abgedeckt — Assertion toleriert beide Ausgänge (`SftpInputFailure` bzw. `SftpInputSupported == false`), da der libssh-Bestand des Test-Builds unbekannt ist |

## Hinweise

- **`MediaUnavailable`-Dedup ist ebenfalls eine Verhaltensänderung an bestehendem
  Log-Verhalten** (bislang eine `Information`-Zeile je ~30-s-Tick, künftig einmalig +
  Folgeticks `Debug`) — in den Designentscheidungen und Abläufen korrekt beschrieben,
  im Risiken-Abschnitt aber nicht so explizit als Abnahme-relevante Abweichung markiert
  wie die Per-Tick-Log-Entfernung (Z. 229); im Abnahmebericht mitnennen.
- **JsonKeys-Bestandslücke ist breiter als die vier geplanten Schlüssel:** Auch
  `MediaProbe:HttpProbeTokenLifetimeSeconds`, `MediaProbe:FfprobePath`, `MediaProbe:Interval*`,
  `MediaProbe:DownloadSources:*`, `Playlists:*`, `ProgramSchedule:*`, die
  Serilog-/Logging-Overrides u. a. fehlen in `appsettings.json:154-211`. Der Plan begrenzt
  die Korrektur bewusst auf die feature-nahen Schlüssel — akzeptable Abgrenzung, für eine
  Vollinventarisierung ggf. Folgeticket.
- **`Protocols: Http2` (reines h2c) an einem `Kestrel:Endpoints`-Endpunkt** würde von
  Stufe 3 als gültige Basis-URL gewertet (Substring trifft `Http2`), ffmpegs HTTP/1.1
  scheitert dort jedoch — wird vom vorhandenen HTTP-Transport-Fallback aufgefangen;
  pathologische Konfiguration, kein Planrisiko.
- **Zeilenangabe `tv-sender-ablauf-technisch.md`:** Der Pump-Abschnitt läuft real ca.
  Z. 357-455 (Plan: „Z. 357-439") — kosmetisch, der benannte Inhalt stimmt.
- **`SynchronizeAsync_RemoteMedia_UsesHttpInput` und die übrigen Remote-Tests bleiben
  gültig:** Die Seeds tragen keine Credentials (`LiveStreamCoordinatorTests.cs:123-143`,
  `LiveStreamMediaResolverTests.SeedAsync`) — die sftp-Stufe kann keine URI bilden und
  fällt korrekt auf Pipe zurück; Seeds mit Credentials sind für Phase 2 eingeplant.
- **`ResolveRemotePath`-Verlagerung** ist regressionssicher: `MediaDurationProbeServiceTests`
  assertiert die gebildete `sftp://`-URI bereits (`..._WithCredentials_ProbesViaUri`);
  die neue Signatur `(collection, item)` passt zum `AsNoTracking`-/separaten-Collection-Load
  des Resolvers (`LiveStreamMediaResolver.cs:84-96`).
- Weitere verifizierte Stellen ohne Handlungsbedarf: `ILiveStreamSegmenter`-Implementierer
  sind genau `FfmpegLiveStreamSegmenter` + die zwei benannten Test-Fakes (Testing-/E2E-Hosts
  fahren den `LiveStreamWorker` nicht, `ServiceCollectionExtensions.cs:336-339`);
  `PumpInputAsync`-Aufrufer sind genau die zwei benannten (`FfprobeMediaDurationProbe.cs:315`
  hat einen eigenen Pump-Pfad); `TryCreateStreamUri` wird von `HttpProbeEnabled` nicht
  abgehängt (`InternalMediaStreamTokenService.cs:91` — nur `TryCreateProbeUri`); kein
  `Kestrel`-Eintrag in `appsettings.Development.json`; `ListLogger`/`ControllableTimeProvider`
  als Test-Fixtures vorhanden.
