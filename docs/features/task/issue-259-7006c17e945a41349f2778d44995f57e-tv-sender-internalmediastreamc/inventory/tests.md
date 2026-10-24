# Bestandsaufnahme: Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-24, Lauf gestartet ~14:54 MESZ (UTC+2, Sommerzeit)
- Branch und Commit-ID: `task/issue-259-7006c17e945a41349f2778d44995f57e-tv-sender-internalmediastreamc` @ `7f9023deed6fbc16db53d5aa6268d6261a8b4c28` („TV-Sender: Airings > 24h bleiben in AiringReproducible reproduzierbar (Issue #261) (#274)")
- Uncommittete Änderungen im getesteten Stand: nur die untracked Dateien der Lifecycle-Artefakte unter `docs/features/task/issue-259-7006c17e945a41349f2778d44995f57e-tv-sender-internalmediastreamc/` (`requirement.md`, `todo.md`, diese Bestandsaufnahme inkl. `inventory/`); **keine** Änderungen an Produktiv- oder Testcode
- Testumgebung und Runtime-/SDK-Versionen: Windows (Git Bash/MSYS), .NET SDK 10.0.402, Ziel-Framework `net10.0` (Debug-Build)
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - Ein Projekt `VideoWebPlayer.Tests` (xUnit) mit drei Trait-Kategorien: ohne Kategorie (Unit), `Category=Integration`, `Category=E2E` (u. a. Playwright-Browser- und Kestrel-Tests)
  - CI (`.github/workflows/staging-ci.yml`, Job `build-and-test`) führt drei getrennte `dotnet test`-Läufe mit Filtern `Category!=E2E`, `Category=Integration`, `Category=E2E` in Release aus
  - `AGENTS.md` §7 nennt als projektüblichen Gesamtlauf: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (alle Kategorien in einem Lauf, Default-Konfiguration Debug) — dieser wurde als Baseline gewählt

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline (vollständige Suite, alle Kategorien, Debug) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "trx;LogFileName=baseline-20261024-145354.trx" --results-directory docs/features/task/issue-259-…/inventory/test-results --logger "console;verbosity=normal"` | Repo-Wurzel `D:\Repositories\softwareschmiede\7006c17e-945a-4134-9f27-78d44995f57e` | 0 | 2246 | 0 | 1 | [Konsolenlog](test-results/console-baseline.log), [TRX-Report](test-results/baseline-20261024-145354.trx) |

Gesamtdauer des Laufs laut Report: ~5,5 Minuten (Ende ~14:59 MESZ). Enthalten sind alle
Kategorien inklusive `E2E` (Playwright, Kestrel-Integration).

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf endete mit 0 fehlgeschlagenen Tests. Fehler, die spätere Arbeiten
auf diesem Stand erzeugen, sind damit **nicht** als preexisting zu werten.

### Testlücken und Ausführungsprobleme

- Ein Test wurde übersprungen: `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — absichtlich deaktiviert per `[Fact(Skip = "…")]` in `VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs` Z. 74–77 (dokumentierter Alt-Befund: `api/continue-watching` antwortet 500 bei Film ohne Filmsammlung; bekannt aus früheren Übergaben als „1 bekannter Alt-Skip").
- `MediaProbeHttpIntegrationTests` (E2E) überspringt sich teilweise selbst: `HttpProbe_RangeEndpoint_ReturnsDuration` kehrt ohne Assertion zurück, wenn kein ffprobe-Binary auffindbar ist oder das Testvideo nicht erzeugt werden konnte; `InternalStream_RangeRequest_ReturnsPartialContent` benötigt ein echtes Kestrel-Binding, läuft aber ohne ffprobe. Im Baseline-Lauf durchgelaufen (ffprobe/ffmpeg auf dem System vorhanden).
- Playwright-E2E-Tests benötigen installierte Browser; Einzeltests gelten projektbekannt als gelegentlich flackernd unter Last (siehe `AGENTS.md` §7, Handover-Hinweise zu `TVSenderAdminE2ETests`/`PlaylistSkipLockedEntriesE2ETest`) — im Baseline-Lauf kein Ausfall.
- Kein bestehender Test verbindet den internen Media-Stream-Endpunkt mit `Setup.TvSenderEnabled` — das Verhalten bei deaktiviertem Flag ist aktuell **ungetestet** (für Variante A fehlt der geforderte Regressionstest; die Bestandstests laufen mit implizit ausgeschaltetem Flag, weil `Setup` in den WebApplicationFactory-Tests nie angelegt/aktiviert wird — Beweis für das heutige „Endpunkt ohne Flag erreichbar"-Verhalten).

## Testklassen

### `InternalMediaStreamControllerTests` (`VideoWebPlayer.Tests/InternalMediaStreamControllerTests.cs`)

HTTP-Tests über `WebApplicationFactory<Program>` (In-Memory `TestServer`, eigene SQLite-Temp-DB,
`MediaProbe:HttpProbeBaseUrl=http://localhost` damit `TryCreateProbeUri` eine URI liefern kann,
`HttpsPort = null`). Seeded lokale Quelle + `clip.mp4` über `IssueProbeUriAsync()`; **setzt
`Setup.TvSenderEnabled` nie** — die 200-Antworten belegen das flag-unabhängige Verhalten.
Kategorie: keine (Unit-Lauf `Category!=E2E`).

- `Stream_InvalidToken_Returns404` — unbekanntes Token → 404.
- `Stream_ValidToken_ReturnsFile` — Probe-URI mit gültigem Token → 200, `video/mp4`, voller Inhalt.
- `Stream_ValidToken_RangeRequest_Returns206PartialContent` — `Range: bytes=2-4` → 206 mit Teilinhalten (Range-Verhalten, das ein Regressionstest bei aktiviertem Flag weiter absichern müsste).
- `Stream_TokenIsBoundToIssuedItem_UnknownAfterInvalid` — Token löst zur ausgestellten `MediaItemId` auf; fremdes Token → 404.

Hilfsmethode: `IssueProbeUriAsync()` — legt `MediaSource`/`MediaCollection`/`MediaItem` an und
stellt über `IInternalMediaStreamTokenService.TryCreateProbeUri` einen URI aus.

### `InternalMediaStreamTokenServiceTests` (`VideoWebPlayer.Tests/Services/InternalMediaStreamTokenServiceTests.cs`)

Unit-Tests mit `FakeServer` (IServer-Fake mit `IServerAddressesFeature`) und `ManualTimeProvider`.

- `TryCreateProbeUri_WildcardAddress_ReturnsLoopbackUriWithResolvableToken` — `http://*:5002` → `http://127.0.0.1:5002/api/internal/media-stream/{64-Hex}`, Token auflösbar.
- `TryNormalizeServerAddress_NormalizesHosts` (Theory, 7 Fälle) — Wildcard/Loopback → `127.0.0.1`, konkrete Hosts bleiben.
- `TryNormalizeServerAddress_NonHttpOrInvalid_ReturnsNull` (Theory, 4 Fälle) — https/ftp/ungültig/leer → `null`.
- `TryCreateProbeUri_OnlyHttpsAddress_ReturnsNull` — HTTPS-only → `null`.
- `TryCreateProbeUri_Disabled_ReturnsNull` — `HttpProbeEnabled=false` → `null`.
- `TryCreateProbeUri_ConfiguredBaseUrl_WinsOverAutoDetection` / `…_InvalidConfiguredBaseUrl_FallsBackToAutoDetection` — `HttpProbeBaseUrl`-Vorrang und Fallback.
- `ResolveMediaItemId_UnknownToken_ReturnsNull` — unbekannt/leer → `null`.
- `ResolveMediaItemId_ExpiredToken_ReturnsNull` — TTL-Grenze via `ManualTimeProvider` (60 s).
- `TryCreateProbeUri_NoServer_ReturnsNull` — kein `IServer` → `null`.
- `TryCreateStreamUri_CustomLifetime_Honored` — 2 h-TTL bleibt bis kurz vor Ablauf gültig.
- `TryCreateStreamUri_ProbeDisabled_StillIssuesUri` — Stream-Tokens sind **nicht** an `HttpProbeEnabled` gekoppelt.
- `TryCreateStreamUri_NoAddress_ReturnsNull` — nur https → `null`.
- `TryCreateStreamUri_TinyLifetime_ClampedToMinimum` — TTL < 60 s wird auf 60 s aufgezogen.

### `MediaProbeHttpIntegrationTests` (`VideoWebPlayer.Tests/MediaProbeHttpIntegrationTests.cs`, `Category=E2E`)

Echter Kestrel-Host (`UseKestrel`, `http://127.0.0.1:0`) + echtes ffprobe-Binary.

- `HttpProbe_RangeEndpoint_ReturnsDuration` — End-to-End: Probe-URI → ffprobe misst ~2 s-Dauer (selbst-überspringend ohne ffprobe/Video).
- `InternalStream_RangeRequest_ReturnsPartialContent` — `Range: 0-15` gegen echten Server → 206/16 Bytes.

### `LiveStreamMediaResolverTests` (`VideoWebPlayer.Tests/Services/LiveStreamMediaResolverTests.cs`)

SQLite-Fixture `ProgramScheduleTestDb` + `StubStreamTokenService` (liefert feste URI, zählt `StreamUriCalls`/`LastLifetime`).

- `ResolveAsync_LocalSource_ReturnsFilePath` / `…_LocalSourceMissingFile_ReturnsNull` — lokale Quelle ohne Token.
- `ResolveAsync_RemoteSource_ReturnsHttpUrl` — entfernte Quelle → `ForHttp` mit Token-URI.
- `ResolveAsync_UseHttpInputFalse_ReturnsPipe` / `…_TokenServiceNull_FallsBackToPipe` / `…_OptionDisabled_FallsBackToPipe` — Fallback-Pfade (`UseHttpInput=false`, `uri=null`).
- `ResolveAsync_RunDuration_SizesTokenLifetime` — `runDuration` + 15-min-Marge wird als TTL durchgereicht.
- `ResolveAsync_UnknownItem_ReturnsNull` — fehlendes `MediaItem` → `null`.

### `LiveStreamCoordinatorTests` (`VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs`)

Token-relevante Methoden:

- `SynchronizeAsync_RemoteMedia_UsesHttpInput` — Job bekommt `Input.HttpUrl` des Token-Stubs.
- `SynchronizeAsync_HttpTransportFailureSameSlot_FallsBackToPipe` — nach HTTP-Transportfehler desselben Slots `HttpUrl == null` (Pipe).
- `SynchronizeAsync_NonTransportExit_KeepsHttpInput` / `…_HttpTransportFailureOtherSlot_KeepsHttpInput` — Fallback-Grenzen.
- Übrige Methoden (Slot-Wechsel, Drift, Sendelücke, ffmpeg-Missing, MediaUnavailable) — Prozesslogik ohne Token-Bezug.

### Flag-Gate-Tests der übrigen Komponenten (Referenzmuster für Variante A)

- `ProgramGuideControllerTests` (`VideoWebPlayer.Tests/Controllers/ProgramGuideControllerTests.cs`): `GetNow_FeatureNotReleased_Returns404`, `GetUpcoming_FeatureNotReleased_Returns404` — Controller-Test setzt `Setup.TvSenderEnabled=false` direkt und ruft die Action mit `DefaultHttpContext` auf (Vorbild für einen Regressionstest ohne HTTP-Layer).
- `LiveStreamWorkerTests.ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts` (`VideoWebPlayer.Tests/LiveStreamWorkerTests.cs` Z. 119–171) — Flag aus ⇒ `StopAsync` auf laufendem Segmenter, kein Start.
- `MediaDurationProbeWorkerTests.ExecuteAsync_FeatureFlagEvaluatedPerCycle` (`VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs` Z. 109–170) — Flag wird pro Zyklus gelesen; Fixture seedet `TvSenderEnabled=true` (Z. 54).
- `ProgramScheduleWorkerTests.Worker_IdlesWithoutSchedule_WhenFeatureNotReleased` (Z. 23, `[Fact]`; Gegenstück `Worker_RunsEnsureCoverage_OnStartup` mit Flag an) — Flag aus ⇒ keine Slots.
- `NavMenuLiveTvTests.NavMenu_HidesLiveTvEntry_WhenFeatureNotReleased` — UI-Gate.
- `LiveTvTests.Page_FeatureNotReleased_ShowsHint_WithoutGuideCallOrPoll` — UI-Gate.
- `TvSenderUserAccessTests.IsGranted_RequiresFlagAndAllowlistOrAdmin` — kombinierte Zugriffsregel.
- `VideoWebPlayerBackupDataTests_TvSenderEnabled.ReadFromAsync_LegacyBackupWithoutTvSenderEnabledColumn_RestoresDefaultFalse` — Alt-Backup-Regressionsmuster.

### HTTP-E2E-Suiten mit Flag-Seed (Kontext)

`ProgramGuideE2ETests`, `LiveStreamE2ETests`, `LiveTvE2ETests`, `TVSenderAdminE2ETests`,
`VideoWebPlayerClientTests_ProgramGuide`, `NavMenuLiveTvTests` setzen `setup.TvSenderEnabled = true`
explizit in ihren Seeds (z. B. `LiveStreamE2ETests` Z. 36, `ProgramGuideE2ETests` Z. 31) — Beleg,
dass die öffentlichen Endpunkte ohne Flag nicht liefern. Für den internen Endpunkt existiert
kein solcher Gegenpol.

## Hilfsmethoden / Test-Doubles

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)

- `CreateAsync(name, ct)` — echte SQLite-DB in Temp-Datei, seedet `Setup { TvSenderEnabled = true }` (Z. 40); Basis der Service-/Controller-Tests.
- `CreateMediaItemAsync`, `CreateSlotAsync` — Sendeplan-Testdaten.

### `MediaProbeTestDoubles` (`VideoWebPlayer.Tests/Helpers/MediaProbeTestDoubles.cs`)

- `PresentLocator` (`IFfprobeLocator` → `"ffprobe"`), `FixedDurationProbe` (`IMediaDurationProbe`-Stub), `StubMediaSourceReader` (`IMediaSourceReader` mit `OpenFileStream`-Lambda) — Bausteine für Probe-/Controller-nahe Tests.

### `LiveStreamMediaResolverTests.StubStreamTokenService` / `LiveStreamWorkerTests.NullStreamTokenService` / `LiveStreamCoordinatorTests`-Stub

- `IInternalMediaStreamTokenService`-Fakes: liefern feste URIs/`null`, zählen Aufrufe und TTL — zeigen, wie der Token-Service in Unit-Tests ersetzbar ist.

### `DeviceClientTestBase` (`VideoWebPlayer.Tests/Helpers/DeviceClientTestBase.cs`)

- `CreateUserAndPairDeviceAsync(..., grantsTvAccess)` — Benutzer + `TvSenderUserAccess`-Zeile + Device-Pairing-Token für die `access_token`-Query der HTTP-E2E-Tests.
