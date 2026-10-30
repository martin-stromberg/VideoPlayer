# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Neue Objekte

- [x] `SegmenterPumpResult` (interner Record in `SegmenterProcessSupport.cs:40-43`) — angelegt: `Outcome` (`SegmenterPumpOutcome`), `BytesPumped` (`long`), `ExpectedBytes` (`long?`)
- [x] `SegmenterPumpOutcome.Truncated` (Enum-Wert, `SegmenterProcessSupport.cs:28`) — angelegt
- [x] `TruncatingReadStream` (`internal` Test-Hilfsklasse, `VideoWebPlayer.Tests/Helpers/TruncatingReadStream.cs`) — angelegt: seekbar (`CanSeek => true`), `Length` > gelieferte Bytes; wird von `SegmenterProcessSupportTests` und `LiveStreamStandbySegmenterTests` geteilt
- [x] `FfmpegErrorMarkersTests` (Testklasse, `VideoWebPlayer.Tests/Services/FfmpegErrorMarkersTests.cs`) — neu angelegt

### Phase 1 — `InternalMediaStreamTokenService`

- [x] Konstruktorparameter `IConfiguration? configuration = null` (`InternalMediaStreamTokenService.cs:83`) — vorhanden, Default leere `ConfigurationBuilder`-Konfiguration (Z. 89)
- [x] `ResolveBaseUrl` Stufe 3 — `Kestrel:Endpoints`-Children enumerieren (Z. 200-221), `Protocols`-Filter `Http1`/`Http2` mit `OrdinalIgnoreCase` (Z. 202-208), jede `Url` über `TryNormalizeServerAddress` normalisiert, erster http-fähiger Treffer gewinnt, `https`/Port-0/ungültig übersprungen ohne Folgetreffer zu blockieren
- [x] Cache-Korrektur — `_baseUrlResolved` wird nur bei positivem Ergebnis gesetzt (Z. 169, 191, 218); `null`-Ergebnis wird beim nächsten Aufruf erneut versucht (Kommentar Z. 223-225)
- [x] `_invalidConfigWarned`-Latch (Z. 65, 173-179) — Einmal-Warnung für ungültige `HttpProbeBaseUrl` auch über Retries hinweg
- [x] `TryNormalizeServerAddress` — Port ≤ 0 abgelehnt (Z. 292-294)
- [x] Einmal-Warnung erweitert — nennt HTTP-Laufzeitermittlung **und** Live-Stream-HTTP-Eingang sowie die geprüften Wege inkl. `Kestrel:Endpoints` (Z. 226-231)

### Phase 1 — `LiveStreamInput` / Resolver / Coordinator / Worker

- [x] `LiveStreamInput.ResolutionNote` (`string?`, `LiveStreamInput.cs:28`) — vorhanden
- [x] `LiveStreamInput.InputKindLabel` (interner Getter, Z. 55-58) — „Datei"/„HTTP"/„sftp"/„pipe:0"
- [x] `ForStream`-Parameter `resolutionNote` (Z. 36-38) — vorhanden, Default `null`
- [x] `LiveStreamInput.ToString()`-Override (Z. 61-66) — maskiert `HttpUrl`-Token via `MaskStreamTokenInText` und `SftpUrl` via `MaskCredentialsInText`
- [x] `LiveStreamMediaResolver.ResolveAsync` — `ForStream`/`ForSftp` tragen `ResolutionNote` mit konkreten Ursachen: Option aus, HTTP-Transport-Vorschaden, keine http-Basis-URL inkl. `Kestrel:Endpoints`, sftp-Gründe (`LiveStreamMediaResolver.cs:128-175`)
- [x] `FfmpegErrorMarkers.ContainsUndecodableInputError` (`FfmpegErrorMarkers.cs:67-79`) — alle fünf Demux-Marker vorhanden
- [x] `SegmenterProcessSupport.PumpInputAsync` — manuelle Kopierschleife mit `bytesPumped`-Zählung, `ExpectedBytes` aus `source.Length` bei `CanSeek` (tolerant), `Truncated`-Outcome + Byte-Logs, Rückgabetyp `SegmenterPumpResult` (Z. 151-210)
- [x] `LiveStreamExitInfo.UndecodableInput` (optionaler Positionsparameter, `LiveStreamExitInfo.cs:32`) — vorhanden
- [x] `FfmpegLiveStreamSegmenter` — Pump-Task per Referenz an `DrainStandardErrorAsync`/`ReportProcessExit` (Z. 189-196, 267-275), `UndecodableInput`-Klassifikation mit Transport-Vorrang (Z. 325-327), Pump-Statistik in Warnzeile (Z. 330-334), `PumpInputAsync` auf `SegmenterPumpResult` (Z. 361-368)
- [x] `LiveStreamStandbySegmenter.PumpInputAsync` — neuer Rückgabetyp; `Truncated` führt wie `Failed` zum Prozess-Kill (Z. 255-266)
- [x] `LiveStreamSyncAction.InputUnplayable` (`LiveStreamCoordinator.cs:21`) — vorhanden
- [x] `LiveStreamCoordinator.SynchronizeStreamAsync` — Skip-Verzweigung bei `UndecodableInput` am selben Slot **vor** der Auflösung: `StopAsync` + `InputUnplayable` mit `WakeAtUtc = now.EndUtc` und Grund im `Detail`, keine eigene Warnung je Tick (Z. 111-119)
- [x] Per-Tick-Fallback-Infolog entfernt — ehemaliger `LogInformation`-Block bei `useHttpInput == false` ist im Diff entfernt; `Started`-`Detail` trägt `InputKindLabel` + `ResolutionNote` (Z. 173-175)
- [x] `LiveStreamWorker.ExecuteAsync` — `InputUnplayable`/`MediaUnavailable` auf Information, identische `(Action, Detail)`-Wiederholungen per `_lastPersistentResult`-Schlüssel auf `Debug` dedupliziert (Z. 116-126)

### Phase 2 — sftp-Eingangsstufe

- [x] `LiveStreamInput.SftpUrl` + Fabrik `ForSftp(string, string? resolutionNote = null)` (`LiveStreamInput.cs:27, 48-49`) — vorhanden; `InputKindLabel` um „sftp" erweitert; `ToString()` maskiert `SftpUrl`
- [x] `SegmenterProcessSupport.InputSpec` — `SftpUrl` als quotiertes `-i`-Argument zwischen `HttpUrl` und `pipe:0` (Z. 92-93)
- [x] `LiveStreamStandbyProvider.SourceDescriptor` — `internal` statt `private`, bildet `SftpUrl` auf `"sftp"` ab (`LiveStreamStandbyProvider.cs:398-399`)
- [x] `LiveStreamOptions.UseSftpInput` (`bool`, Default `true`, `LiveStreamOptions.cs:27`) — angelegt; `"UseSftpInput": true` in `appsettings.json` Section `LiveStream`
- [x] `AutoUpdate:ProtectedFiles[].JsonKeys` — `LiveStream:UseSftpInput` **und** Bestandsschlüssel `LiveStream:UseHttpInput`, `MediaProbe:HttpProbeEnabled`, `MediaProbe:HttpProbeBaseUrl` ergänzt (`appsettings.json`, eigener Commit `c816555`)
- [x] `SftpProbeUriBuilder.ResolveRemotePath(MediaCollection, MediaItem)` (`internal static`, `SftpProbeUriBuilder.cs:70-77`) — angelegt
- [x] `MediaDurationProbeService` — privates `ResolveRemotePath` entfernt, `ProbeRemoteItemAsync` ruft `SftpProbeUriBuilder.ResolveRemotePath(collection, item)` (Diff)
- [x] `LiveStreamMediaResolver.ResolveAsync` — Parameter `bool useSftpInput = true` vor `cancellationToken` (Z. 87); sftp-Stufe (`SourceType == Sftp` + Option + `TryBuildUri` auf `ResolveRemotePath`) zwischen HTTP und Pipe (Z. 149-171); `ResolutionNote` um sftp-Gründe erweitert
- [x] `LiveStreamStandbyProvider.ResolveMediaInputDefaultAsync` — `useSftpInput: true` + benanntes `cancellationToken` (Z. 310-312)
- [x] `LiveStreamCoordinator.SynchronizeStreamAsync` — `useSftpInput` aus `SftpInputSupported`/`LastUnexpectedExit.SftpInputFailure` am selben Slot abgeleitet und per benanntem `cancellationToken` an `ResolveAsync` gereicht (Z. 121-128)
- [x] `FfmpegErrorMarkers.ContainsSftpInputError` + `ContainsUnsupportedSchemeError` samt Markerlisten (`FfmpegErrorMarkers.cs:86-149`) — vorhanden
- [x] `LiveStreamExitInfo.SftpInputFailure` (optionaler Positionsparameter, `LiveStreamExitInfo.cs:33`) — vorhanden
- [x] `ILiveStreamSegmenter.SftpInputSupported` (`ILiveStreamSegmenter.cs:44`) — deklariert; implementiert in `FfmpegLiveStreamSegmenter` (volatile Latch, initial `true`, Schema-Marker setzen `false`) sowie in beiden Test-Fakes (`LiveStreamCoordinatorTests.cs:41`, `LiveStreamWorkerTests.cs:40`)
- [x] `FfmpegLiveStreamSegmenter.ReportProcessExit` — `SftpInputFailure`-Klassifikation nur bei `SftpUrl`-Jobs, Schema-Latch, Transport-Flags haben Vorrang vor Demux-Markern

### Tests (alle im Plan benannten Tests vorhanden und grün)

- [x] `InternalMediaStreamTokenServiceTests` — alle 12 neuen Tests vorhanden: `TryCreateStreamUri_OnlyKestrelEndpoint_ReturnsLoopbackUri`, `TryCreateProbeUri_OnlyKestrelEndpoint_ReturnsLoopbackUri`, `TryCreateStreamUri_KestrelCustomEndpointName_ReturnsLoopbackUri`, `TryCreateStreamUri_KestrelHttpsBeforeHttp_SkipsHttpsAndUsesHttp`, `TryCreateStreamUri_KestrelHttp3OnlyEndpoint_SkipsEndpoint`, `TryNormalizeServerAddress_PortZero_ReturnsNull`, `TryCreateProbeUri_AddressesAppearAfterFirstMiss_RetriesAndResolves`, `TryCreateProbeUri_PositiveResult_StaysCached`, `TryCreateStreamUri_BoundAddressWins_OverKestrelEndpoint`, `TryCreateStreamUri_KestrelHttpsOnly_ReturnsNull`, `TryCreateStreamUri_NoAddressNoKestrel_ReturnsNull`, `TryCreateProbeUri_InvalidBaseUrl_WarnsOnlyOnceAcrossRetries`; `CreateService` um `IConfiguration`/`ILogger`-Übergabe erweitert (Z. 37-48)
- [x] `FfmpegErrorMarkersTests` — `ContainsUndecodableInputError_*`, `ContainsSftpInputError_*`, `ContainsUnsupportedSchemeError_*` jeweils positiv/negativ
- [x] `LiveStreamMediaResolverTests` — `ResolveAsync_PipeFallback_ResolutionNoteNamesCause` (+ Bonus `..._OptionDisabled_NotesOptionCause`), `ResolveAsync_RemoteSourceCredentialsNoHttpUri_ReturnsSftpUrl`, `..._UseHttpInputFalseWithCredentials_ReturnsSftpUrl`, `..._UseSftpInputFalse_SkipsSftp`, `..._SftpOptionDisabled_SkipsSftp`, `..._SftpMissingCredentials_FallsBackToPipe`; Seed-Helfer `SeedSftpWithCredentialsAsync` mit Credentials/POSIX-Pfaden
- [x] `SegmenterProcessSupportTests` — `PumpInputAsync_Success_ReportsPumpedAndExpectedBytes`, `..._NonSeekableSource_CompletedWithoutExpectedBytes`, `..._ShortStream_ReturnsTruncated`, `InputSpec_SftpUrl_ReturnsQuotedUrl`, `NoStdinArg_SftpInput_ReturnsNostdin`; bestehende Pump-Tests auf `result.Outcome` umgestellt
- [x] `FfmpegLiveStreamSegmenterTests` — `UnexpectedExit_PipeDemuxMarkers_SetsUndecodableInput`, `..._FileInputDemuxMarkers_SetsUndecodableInput`, `..._PipeInput_IncludesPumpStatsInWarning`, `StartAsync_SftpInput_UsesUrlArgumentAndNoStdin`, `UnexpectedExit_SftpInputTransportMarkers_SetsSftpInputFailure`, `..._SftpSchemeMarkers_LatchesSftpInputSupportedFalse`, `..._SftpInput_MasksCredentialsInStderr`
- [x] `LiveStreamCoordinatorTests` — `SynchronizeAsync_UndecodableInputSameSlot_SkipsSlotUntilEnd`, `..._UndecodableInputOtherSlot_IgnoresFailure`, `..._RemotePipeFallback_StartedDetailNamesInputKindAndNote`, `..._RemoteMedia_StartedDetailNamesHttpInput`, `..._HttpTransportFailureSameSlotWithCredentials_FallsBackToSftp`, `..._SftpFailureSameSlot_FallsBackToPipe`, `..._SftpUnsupported_SkipsSftpInput`; `FakeSegmenter` mit `LastUnexpectedExit`-Setter und `SftpInputSupported`
- [x] `LiveStreamStandbySegmenterTests` — `TranscodeAsync_TruncatedPumpInput_KillsProcessAndReturnsNull` über gemeinsame `TruncatingReadStream`
- [x] `LiveStreamStandbyProviderTests` — `SourceDescriptor_SftpInput_ReturnsSftpDescriptor`
- [x] `LiveStreamWorkerTests` — `ExecuteAsync_UndecodableSlotSkips_LogsInputUnplayableOnce` mit `FakeGuide.Result`-Variante (laufender Sendeplatz), `ListLogger<LiveStreamWorker>`, `ControllableTimeProvider`; `FakeSegmenter.LastUnexpectedExit` setzbar, `SftpInputSupported` implementiert
- [x] `FfmpegLiveStreamSegmenterE2ETests` — `RealFfmpeg_MoovAtEndPipeInput_FlagsUndecodableInput`, `RealFfmpeg_MoovAtEndFileInputWithOffset_ProducesSegments`, `RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure` — alle mit echtem ffmpeg (9.0.2 gyan.dev) grün

### Dokumentation

- [x] `docs/help/tv-sender.md` — dreistufige Basis-URL-Erkennung inkl. `Kestrel:Endpoints`, Kette HTTP → `sftp://` → `pipe:0`, `LiveStream:UseSftpInput`, Credentials-/Kommandozeilen-Hinweis, `ffmpeg -protocols`-Prüfung, Slot-Skip bei nicht lesbarem Eingang, präzisierter moov-Hinweis (Z. 565-588, 768-773)
- [x] `docs/help/tv-sender-ablauf-technisch.md` — Eingabe-Pump auf `SegmenterPumpResult`/`Truncated`/Bytezählung aktualisiert, `Truncated`-Kill im Standby-Pfad, `UndecodableInput`/`InputUnplayable`-Skip, `SftpInputFailure`/`SftpInputSupported`-Latch, Deskriptor `http`/`sftp`/`stream`, Fallback-Kette im `ResolveAsync`-Abschnitt
- [x] `docs/help/videolaufzeit.md` — dreistufige Erkennung Konfiguration → gebundene Adressen → `Kestrel:Endpoints` für beide Verbraucher (Z. 8, Konfigtabelle)
- [x] `docs/RELEASE_NOTES.md` — „What's New"-Eintrag im vorhandenen Stil
- [x] `appsettings.Production.json` — wie geplant unverändert (kein Diff)

### Regression

- [x] `dotnet build VideoPlayer.sln -c Debug` — 0 Fehler, 0 Warnungen (Review-Lauf)
- [x] `dotnet build VideoPlayer.sln -c Release` — 0 Fehler (141 bekannte BL0008-/XSD-Warnungen, Review-Lauf)
- [x] Betroffene Testklassen im Review-Lauf: 163/163 grün (`InternalMediaStreamTokenServiceTests`, `FfmpegErrorMarkersTests`, `LiveStreamMediaResolverTests`, `SegmenterProcessSupportTests`, `LiveStreamCoordinatorTests`, `LiveStreamWorkerTests`, `LiveStreamStandbySegmenterTests`, `LiveStreamStandbyProviderTests`, `FfmpegLiveStreamSegmenterTests`)
- [x] `FfmpegLiveStreamSegmenterE2ETests` im Review-Lauf: 6/6 grün (keine Skips — ffmpeg vorhanden); der im Plan benannte Flaky-Test `SecondJob_PlaylistContinuesAndMarksDiscontinuity` lief in diesem Durchlauf fehlerfrei
- [x] Volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release` im Review-Lauf: 2794/2797 grün, 2 übersprungen, **1 Ausfall**: `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (Playwright strict-mode: 2 statt 1 `.tvsender-slot-displaced`-Zeilen — ein Hintergrund-Sendeplan-Tick hatte unter Last eine zweite Zeile verdrängt). Einzelwiederholung: **grün** (16 s) → bekannter Flaky-Typus „Playwright unter Last", nicht durch die Änderungen dieses Plans berührt (Testdatei zuletzt in Commit `6ef4a8e`, Issue #307, geändert; die Implementierungs-Commits fassen weder Sendeplan- noch Admin-UI-Code an)

## Hinweise

- Die volle Testsuite wurde im Review-Lauf ausgeführt: 2794/2797 grün; der einzige Ausfall (`TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule`) ist ein lastabhängiger Playwright-Flake — die Einzelwiederholung lief grün und der Test liegt fachlich außerhalb des Änderungsumfangs (Sendeplan-UI, nicht Live-Stream-Eingangskette). Die im Plan ehrlich genannten Punkte (bewusste Verhaltensänderungen: Cache-Semantik-Korrektur, entfernter Per-Tick-Infolog, `Truncated`-Kill im Standby-Pfad, einmalige Cache-Invalidierung durch `"sftp"`-Deskriptor) sind alle wie geplant umgesetzt.
- `LiveStreamInput.ToString()`-Maskierung und der erweiterte Warnungstext haben keinen direkten Test — im Plan ebenfalls keiner vorgesehen.
- `git status` zeigt eine uncommittete Änderung an `todo.md` (Arbeitsstand des Orchestrierers — kein Planartefakt).
