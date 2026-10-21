# Tests — Bestand und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-21, 19:52–20:01 (lokale Zeit, UTC+2 Sommerzeit)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-14-http-probe`, `2006afae6bb3a9f61d07dbbbb60d1c8b619f3991`
- Uncommittete Änderungen im getesteten Stand: nur neue, nicht eingecheckte
  Dokumentationsdateien unter `docs/features/…schritt-14-http-probe/` — kein Produktivcode
- Testumgebung: Windows, .NET 10 SDK (net10.0), xUnit; ffprobe 9.0.2 auf PATH
  (winget Gyan.FFmpeg full build)
- Ermittelte Testsuite: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
  (vollständige Suite laut AGENTS.md)

### Testläufe

| Lauf | Befehl | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build` (nach Debug-Build 0 Fehler) | Repo-Root | 0 | 2013 | 42 | 1 | [Log](test-results/baseline-20261021-195240.log) |
| 2 | `TMP=TEMP=TMPDIR=D:\temp\vwp-tests dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build` | Repo-Root | 0 | 2055 | 0 | 1 | [Log-Auszug](test-results/baseline-passed.log) |

### Nachgewiesene bestehende Testfehler

Keine. Lauf 1 schlug 42× mit `Microsoft.Data.Sqlite.SqliteException : SQLite Error 13:
'database or disk is full'` fehl — Ursache war die zu 100 % volle C:-Platte
(`Path.GetTempPath()` liegt auf C:). Mit `TMP`/`TEMP`/`TMPDIR` auf D: (5,5 GB frei) läuft
dieselbe Suite fehlerfrei → kein Code-Defekt, reines Umgebungsproblem. Der eine
übersprungene Test (`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`)
ist ein bekannter Alt-Skip.

**Hinweis für Folgeläufe:** In dieser Umgebung `TMP`/`TEMP`/`TMPDIR` auf
`D:\temp\vwp-tests` setzen, sonst failen E2E-/DB-Tests am vollen C:-Laufwerk.

### Testlücken und Ausführungsprobleme

- Kein Test ruft ein echtes ffprobe-Binary auf — `FfprobeMediaDurationProbeTests` injiziert
  den `executeAsync`-Delegate (simuliert Exit-Code/stdout/stderr).
- Kein E2E-Test für die Probe-Pipeline über HTTP — `LocalMediaSourceStreamingE2ETests`
  prüft den Stream-Endpunkt nur über `WebApplicationFactory.CreateClient` (TestServer,
  kein echter TCP-Port) und ohne ffprobe.

## Testklassen (Probe-Bereich)

### `MediaDurationProbeServiceTests` (VideoWebPlayer.Tests/Services)
- `ProbePendingAsync_ProbesPendingVideoItems_WritesDuration` — Happy Path
- `ProbePendingAsync_SuccessfulProbe_NotifiesScheduleSignal` / `…_DoesNotNotify…` — Signal
- `…_TransientFailure_RetriedAfterDelay`, `…_MaxAttemptsReached_NotRetried`,
  `…_PermanentFailure_ExhaustsAttemptsAndStops`, `…_LegacyFailedItem_IsPickedUpAgain` — Retry
- `ProbePendingAsync_LocalSource_UsesFileProbe` — Pfadwahl lokal
- `ProbePendingAsync_SftpSource_StreamsViaOpenFileStream` — Pipe-Fallback ohne Credentials
- `ProbePendingAsync_SftpSource_WithCredentials_ProbesViaUri` — `sftp://`-Pfad
- `ProbePendingAsync_SftpSource_UriUnsupported_FallsBackToStream` — Fallback sftp→pipe
- `ProbePendingAsync_SftpSource_RelativePaths_FallBackToStream` — kein absoluter Pfad
- `ProbePendingAsync_SftpSource_UriPermanentFailure_ExhaustsAttempts`,
  `…_NullStream_CountsTransientAttempt`, `…_ProbeThrows_MarksTransientAndContinues`,
  `…_NoFfprobe_LeavesItemsUnprobed`, `…_RespectsBatchSize`,
  `…_ParallelProbes_RunsConcurrently`, `…_SequentialProbes_RunOneAtATime`,
  `…_SkipsAlreadyProbedItems`, `…_SkipsNonVideoItems`

Hilfsklassen in derselben Datei: `FakeDurationProbe` (zählt File/Stream/Uri-Calls,
konfigurierbare Results, `ThrowOnPathContaining`, `Delay`, `MaxConcurrentCalls`),
`FakeFfprobeLocator`, `FakeScheduleSignal`, `StubMediaSourceReader`.

### `MediaDurationProbeServiceTests_Progress`
Fortschritts-Aggregation `GetProgressAsync` (Admin-Check, Zähler, Recent-Liste).

### `FfprobeMediaDurationProbeTests`
- `ProbeUriAsync_ProtocolNotFound_ReturnsUriUnsupported_AndSkipsLaterInvocations` —
  `_uriProtocolUnsupported`-Merker
- Duration-Parsing, Timeout→Transient, Permanent-Marker nur bei seekable, Exit≠0-Handling,
  Credential-Maskierung in Logs

### `FfprobeLocatorTests`, `SftpProbeUriBuilderTests`, `MediaDurationProbeWorkerTests`
Locator-Konventionen, sftp-URI-Aufbau/Maskierung, Worker-Loop.

### E2E-/Integrations-Infrastruktur
- `LocalMediaSourceStreamingE2ETests` (`[Trait("Category","E2E")]`): Stream/Download über
  `api/items/{type}/{id}/stream?access_token=` mit TestServer; `SeedMovieAsync`-Pattern
  (User + `MediaSourceUser` + `MediaSource`/`MediaCollection`/`Movie`/`MediaItem`/
  `MovieMediaItem`), Token via `AuthorizationTokenService.CreateToken`.
- `PlaylistsE2ETestBase` u. a.: `WebApplicationFactory<Program>` + `UseKestrel()` +
  `UseUrls("http://127.0.0.1:0")` → echter TCP-Port; gebundene Adresse via
  `IServer`/`IServerAddressesFeature` — Vorbild für einen echten ffprobe-Integrationstest.
