# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Die fachliche Architektur des Plans trägt (Kestrel-Endpoints als dritte Erkennungsstufe,
`ResolutionNote`-Diagnose, `UndecodableInput`-Klassifikation mit Slot-Skip, Byte-zählender
Pump, sftp-Mittelstufe mit `SftpInputSupported`-Latch) und deckt alle sechs
Akzeptanzkriterien in Umsetzung und Test ab — die Kernannahmen wurden direkt am Code
verifiziert (`InternalMediaStreamTokenService.cs:148-195` liest tatsächlich nur
`HttpProbeBaseUrl` + `IServerAddressesFeature.Addresses`; `TryNormalizeServerAddress`
verarbeitet `*`/`+`/`0.0.0.0`/`[::]`/`localhost`/`::1` korrekt und lehnt `https` ab;
`appsettings.Production.json:22-26` enthält `Kestrel:Endpoints:Http:Url = http://*:5002`;
das `MdnsServiceProfileBuilder`-/`DiscoveryResponseBuilder`-Präzedenz für `IConfiguration`
existiert). Vier konkrete Lücken verhindern den Status „vollständig": eine falsch
beschriebene Cache-Semantik (siehe Punkt 1 unten), ein nicht genannter betroffener
bestehender Test-Fake, eine nicht genannte Call-Site-Änderung und ein nicht eingeplantes
veraltetes Doku-Dokument.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Serien-/Film-Sendeplätze aus entfernten Quellen werden segmentiert (Exit 0 am Slotende) | Phase 1 Schritt 1: `ResolveBaseUrl` + `Kestrel:Endpoints:*:Url`-Stufe → `ForHttp` (seekbar); Phase 2: `ForSftp`-Stufe | 6 neue `InternalMediaStreamTokenServiceTests` (In-Memory-Config); E2E `RealFfmpeg_MoovAtEndFileInputWithOffset_ProducesSegments` als Argument-Analog für URL-Eingänge; bestehende `InternalMediaStreamControllerTests` (Range-Requests) | Abgedeckt (Hinweis: kein E2E über den echten HTTP-Loopback-Endpunkt — Kompensation durch ForFile-Analog + Controller-Tests ist vertretbar und im Plan begründet) |
| 2. Ursache des `pipe:0`-Wegs festgestellt und dokumentiert | Root Cause im Plan dokumentiert; `ResolutionNote` am `LiveStreamInput` + `Started`-`Detail`; Warnungstext um Segmenter-Verbraucher und `Kestrel:Endpoints` erweitert | `ResolveAsync_PipeFallback_ResolutionNoteNamesCause` | Abgedeckt (Hinweis: kein Test, der Eingangsart/`ResolutionNote` im `Started`-`Detail`/Log assertiert) |
| 3. `pipe:0`-Fallback: Fehler erkannt, geloggt, keine Restart-Schleife | `ContainsUndecodableInputError`-Marker (decken die Produktiv-Meldungen `partial file`/`Could not find codec parameters`/`Cannot determine format` ab) + `LiveStreamSyncAction.InputUnplayable`-Skip (`WakeAtUtc = now.EndUtc`); Phase 2 `SftpInputFailure`/`SftpInputSupported`-Latch | `ContainsUndecodableInputError_DemuxMarkers_Match`, `UnexpectedExit_*DemuxMarkers_SetsUndecodableInput` (Pipe + Datei), `SynchronizeAsync_UndecodableInputSameSlot_SkipsSlotUntilEnd` / `..._OtherSlot_IgnoresFailure`; E2E `RealFfmpeg_MoovAtEndPipeInput_FlagsUndecodableInput`, `RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure` | Abgedeckt |
| 4. Unvollständiger Pump von moov-Problem unterscheidbar | Manuelle Pufferschleife mit `BytesPumped`/`ExpectedBytes`, Outcome `Truncated`, Bytezahlen in Pump-Logs und Exit-Warnzeile | `PumpInputAsync_Success_ReportsPumpedAndExpectedBytes`, `PumpInputAsync_ShortStream_ReturnsTruncated` (`TruncatingReadStream`), `UnexpectedExit_PipeInput_IncludesPumpStatsInWarning` | Abgedeckt |
| 5. Bestehendes Verhalten unverändert | Reihenfolge der Erkennungsstufen bleibt (HttpProbeBaseUrl → Addresses → Kestrel); lokale `ForFile`-/HTTP-Pfade/`append_list`/Token-/Credential-Maskierung unverändert; `sftp`-Stufe nur bei `SourceType == Sftp` | `TryCreateStreamUri_BoundAddressWins_OverKestrelEndpoint`, `..._NoAddressNoKestrel_ReturnsNull`, `ResolveAsync_SftpMissingCredentials_FallsBackToPipe`, `UnexpectedExit_SftpInput_MasksCredentialsInStderr`; unveränderte Playlist-E2Es | Abgedeckt (siehe jedoch Lücke 1: die beschriebene Cache-Semantik stimmt nicht mit dem Code überein — je nach Umsetzung ändert sich Bestandsverhalten) |
| 6. Tests decken den behobenen Pfad; Builds grün | Umsetzungsschritte 7/8/17/18 (neue Unit-/E2E-Tests, Anpassung betroffener Tests) und Schritt 20 (Debug+Release-Build, volle Suite) | Testtabellen „Neue Tests"/„Betroffene bestehende Tests"/E2E-Tabelle | Abgedeckt — mit den unter „Testanforderungen" genannten Lücken |

## Fehlende oder unvollständige Testanforderungen

- [ ] `LiveStreamWorkerTests.FakeSegmenter` (`VideoWebPlayer.Tests/LiveStreamWorkerTests.cs:31-42`) implementiert ebenfalls `ILiveStreamSegmenter` und bricht mit dem neuen Member `SftpInputSupported` (Phase 2, Schritt 15) — der Plan nennt unter „betroffene Tests" nur `LiveStreamCoordinatorTests.FakeSegmenter`; `LiveStreamWorkerTests` wird nur für die neue Aktion `InputUnplayable` erwähnt.
- [ ] Kein Test für die Skip-und-Weitersuchen-Semantik bei gemischten Kestrel-Endpunkten (z. B. `Endpoints: { Https: https://*:443, Http: http://*:5002 }` → `http` muss gefunden werden); `TryCreateStreamUri_KestrelHttpsOnly_ReturnsNull` deckt nur „https führt zu `null`" ab, nicht „https wird übersprungen und ein späterer http-Endpunkt gewinnt" (Akzeptanzkriterium 1).
- [ ] Kein Test, der das neue `Started`-`Detail` (Eingangsart Datei/HTTP/sftp/`pipe:0` + `ResolutionNote`) assertiert — die `ResolutionNote` selbst ist getestet, ihr Transport bis ins Log (Akzeptanzkriterium 2) nicht.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kein UI-Benutzerfluss (Server-Hintergrundlogik, keine Razor-/Playwright-Änderung) | Begründung im Plan: Prozess-Ebene via `FfmpegLiveStreamSegmenterE2ETests` (echtes ffmpeg vorhanden, `E2EBinaryLocator`) | Nicht erforderlich mit Begründung |
| Reproduktion des Produktiv-Fehlschlags: moov-am-Ende-MP4 über `pipe:0` wird als Eingangsproblem erkannt (AK 3 + 4) | `RealFfmpeg_MoovAtEndPipeInput_FlagsUndecodableInput` | Abgedeckt — Prämisse plausibel, da `RealFfmpeg_PipeInputStop_LogsNoExceptionStacktrace` (`FfmpegLiveStreamSegmenterE2ETests.cs:159-160`) die moov-Falle mit echtem ffmpeg bereits empirisch bestätigt |
| Seekbarer Eingang segmentiert moov-am-Ende-MP4 inkl. `-ss`-Sprung (AK 1) | `RealFfmpeg_MoovAtEndFileInputWithOffset_ProducesSegments` (`ForFile`, `PositionSeconds > 0`) | Abgedeckt — deckt `ForHttp`/`ForSftp` nur argumentgleich ab; die eigentliche Loopback-Erreichbarkeit des internen Endpunkts wird nicht prozessübergreifend bewiesen (vom Plan begründet, Restrisiko benannt) |
| sftp-Fehlschlag fällt gestuft zurück statt zu loopen; ffmpeg-Build-Eigenschaft (libssh) wird erkannt (AK 3 + 5) | `RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure` (`sftp://u:p@127.0.0.1:1/x`, assertiert `SftpInputFailure` bzw. `SftpInputSupported == false`) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] **Falsche Prämisse zur Cache-Semantik von `ResolveBaseUrl`:** Der Plan behauptet (Programmablauf „Basis-URL-Erkennung", Schritt 1), „ein Fehlschlag wird **nicht** dauerhaft gecacht" — der Code setzt `_baseUrlResolved = true` jedoch **unbedingt** am Methodenanfang (`InternalMediaStreamTokenService.cs:153`), sodass ein `null`-Ergebnis heute prozessweit gecacht wird; der Inline-Kommentar (Z. 184-186) beschreibt die intendierte, nicht implementierte Semantik (verifiziert auch über Commit `a9d9b08`). Der Plan muss explizit entscheiden: Ist-Semantik beibehalten (Plan-/Kommentartext korrigieren) oder dem dokumentierten Intent folgen (`_baseUrlResolved` nur bei Erfolg setzen) — letzteres ändert Bestandsverhalten (betrifft AK 5) und schließt genau die Startup-Race, die der Plan als gewahrt darstellt (relevant für `UseUrls`/`ASPNETCORE_URLS`-Installationen ohne `Kestrel:Endpoints`, wo ein früher Aufruf die Erkennung sonst prozessweit vergiftet).
- [ ] **Call-Site-Bruch nicht eingeplant:** `LiveStreamStandbyProvider.ResolveMediaInputDefaultAsync` (`LiveStreamStandbyProvider.cs:311`) ruft `ResolveAsync(mediaItemId, runDuration: null, useHttpInput: true, cancellationToken)` mit **positionalem** `CancellationToken` — der vor `cancellationToken` eingefügte Parameter `bool useSftpInput = true` lässt die Stelle nicht mehr kompilieren (CS1503); unter `LiveStreamStandbyProvider` listet der Plan nur die `SourceDescriptor`-Änderung.
- [ ] **Veraltete Technik-Doku nicht eingeplant:** `docs/help/tv-sender-ablauf-technisch.md` (Z. 357-434) dokumentiert `PumpInputAsync` mit Rückgabe `SegmenterPumpOutcome`/`Completed` — mit `SegmenterPumpResult`/`Truncated` und Bytezählung teilweise falsch; der Dokumentationsschritt nennt nur `tv-sender.md` und `RELEASE_NOTES.md`.
- [ ] **Log-Wiederholung beim Slot-Skip nicht adressiert:** `InputUnplayable` führt bei jedem ~30-s-Tick erneut in die Skip-Verzweigung (Koordinator-Warnung + Worker-Information bis `now.EndUtc`) — bei langen Sendeplätzen entsteht eine Warnung alle ~30 s; der Plan begründet Spam-Vermeidung für `ResolveAsync`, benennt aber keinen Throttle/Dedup für die Skip-Warnung.

## Hinweise

- **Port-0-Kantenfall der Kestrel-Stufe:** Ein Endpunkt `http://*:0`/`http://127.0.0.1:0` (dynamischer Port, vgl. `appsettings.Testing.json:8`) würde eine unbrauchbare `http://127.0.0.1:0`-Basis-URL liefern; produktiv fängt der HTTP-Transport-Fallback das auf, ein expliziter `port <= 0`-Skip wäre sauberer. In `WebApplicationFactory`-Hosts ungefährlich, da dort `IServerAddressesFeature.Addresses` den echten zufälligen Port enthält und Stufe 2 vor Stufe 3 greift.
- **`Protocols`-Filtern der Kestrel-Stufe nicht bedacht:** Ein `http://`-Endpunkt mit `Protocols: Http3` hört nur UDP — TCP-Loopback scheitert; der HTTP-Transport-Fallback fängt das, ein Blick auf `Kestrel:Endpoints:*:Protocols` wäre robuster.
- **`Truncated`→Kill im Standby-Pfad** ist beschrieben (korrekt, da „nicht `Completed` ⇒ Kill" bereits die bestehende Logik ist), aber ohne benannten Test in `LiveStreamStandbySegmenterTests`.
- **`AutoUpdate:ProtectedFiles/JsonKeys`** (`appsettings.json:151-211`) whitelistet weder `LiveStream:*` noch `MediaProbe:*` — Operator-Overrides auf `UseSftpInput` (und schon heute `UseHttpInput`/`HttpProbeBaseUrl`) überleben ein Paket-Update nicht; pre-existing für Bestandsoptionen, für die neue Option analog zu erwägen.
- `FfprobeMediaDurationProbe` pumpt **nicht** über `SegmenterProcessSupport.PumpInputAsync` (eigener `_executeAsync`-Pfad) — die im Plan genannten betroffenen Pump-Aufrufer (`FfmpegLiveStreamSegmenter`, `LiveStreamStandbySegmenter`) sind damit vollständig.
- Betroffene-Tests-Tabelle ansonsten verifiziert: die fünf `SegmenterProcessSupportTests`-Pump-Tests existieren wie benannt; `InternalMediaStreamTokenServiceTests.CreateService` (Z. 34-42) und `LiveStreamCoordinatorTests.SeedRemoteMediaItemAsync` (Z. 123-143, ohne `Username`/`Password`) entsprechen den Planbehauptungen; `FfmpegErrorMarkersTests` existiert noch nicht (Plan sagt korrekt „neu").
- Positionsparameter-Erweiterungen sind kompilierbar verifiziert: `LiveStreamInput`/`LiveStreamExitInfo` als optionale Positionsparameter am Ende halten alle Aufrufstellen (`FfmpegLiveStreamSegmenter.cs:304`, Test-Konstruktionen mit benanntem `HttpTransportFailure:`) kompilierfähig; `NoStdinArg` liefert für `ForSftp` automatisch `-nostdin` (`IsPipe == false`).
- `ILiveStreamSegmenter`-Implementierungen außerhalb der Tests: nur `FfmpegLiveStreamSegmenter`; in `LiveStreamE2ETests`/`Testing`-Umgebung wird `LiveStreamWorker` nicht gehostet (`ServiceCollectionExtensions.cs:336-339`), dort bricht kein Fake.
