# Audit: Umgebungssensitivität der Tests in `VideoWebPlayer.Tests/`

Vollständiges Audit der 429 C#-Dateien des Testprojekts auf Betriebssystem-Schnittstellenberührung, als Entscheidungsgrundlage für die `OsInterface`-Markierung (Issue #368). Heuristik angepasst an dieses Repo (CI läuft auf `ubuntu-latest`, Blazor-Server-App): Playwright/Browser, echte Kindprozesse (ffmpeg/ffprobe/cmd/icacls), echte Dateisystem-/Temp-Szenarien, SQLite-Dateibanken, Loopback-Ports/Netzwerk, enge Zeitbudgets/Polling-Loops.

Bewertungskriterium (aus der Blaupause übernommen): Maßgeblich ist, ob die **Assertion** eines Tests am OS-Verhalten hängt — nicht, ob irgendwo `Process.Start` oder `Path.GetTempPath` vorkommt. Temp-Verzeichnis-Nutzung allein macht einen Test nicht automatisch OS-nah; relevant wird es bei lockanfälligen Löschpfaden, Junctions/Symlinks, Berechtigungsbits oder Prozess-Handles auf Dateien.

## Legende

- **(E2E)** — trägt heute `[Trait("Category", "E2E")]`, läuft im CI-Schritt `Category=E2E` (blockierend).
- **(ohne Trait)** — trägt keinen `Category`-Trait, läuft heute im blockierenden `Category!=E2E`-Schritt.
- **(kein Trait nötig / regulär)** — kein OS-Interface-Befund nach dem Kriterium oben.

## Kategorie A — Playwright/Browser + echter Kestrel-Loopback-Port (42 Testklassen)

Alle starten einen echten Kestrel-Host auf `http://127.0.0.1:0` (`WebApplicationFactory.UseKestrel()` + `StartServer()`) und einen echten Chromium-Prozess via `Microsoft.Playwright` (`IPlaywright`, `IBrowser`, `IPage`). Zusätzlich: temporäre SQLite-Dateibank (`GetTempPath`-`.db`) und Blazor-Circuit-Timing (feste `WaitForTimeoutAsync`-Sleeps, `DriveUntilStateAsync`-Polling mit 60-s-Deadline in `PlaylistsE2ETestBase`).

### Mit `Category=E2E`-Trait (34 Dateien)

| Datei | Basisklasse / Muster |
|-------|----------------------|
| `AgeRatingE2ETests.cs` | eigene `IAsyncLifetime`-Fixture (Kestrel+Browser+Temp-DB) |
| `BackupUploadE2ETests.cs` | eigene `IAsyncLifetime`-Fixture (Kestrel+Browser+Temp-DB+Temp-Backupdirs) |
| `DevicePairingE2ETests.cs` | eigene `IAsyncLifetime`-Fixture (Kestrel+Browser) |
| `MediaBoxContextMenuInteractionE2ETests.cs` | `MediaBoxContextMenuE2ETestBase` |
| `MediaBoxContextMenuPositionE2ETests.cs` | `MediaBoxContextMenuE2ETestBase` |
| `MediaSourceAdminFullScanE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `MediaSourceDeleteE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `MediaSourceExplorerRescanE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `MediaSourceHiddenInMenuE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `MediaSourceLocalDirectoryE2ETests.cs` | eigene `IAsyncLifetime`-Fixture; nutzt zusätzlich `TestHelpers.DenyReadAccess` (icacls/`SetUnixFileMode`) |
| `MediaSourceSwitchE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `MovieCollectionEditE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `PairingBootstrapE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `PlaylistDetailE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistDetailInteractionE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistDetailLayoutE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistDragDropFeedbackE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistDragDropReorderE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistEntriesE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistForbiddenEntryE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistMediaSearchE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistPlaybackE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistPlaybackViewportE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistReorderE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistsE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistsOverviewE2ETests.cs` | `PlaylistsE2ETestBase` |
| `PlaylistsOverviewLayoutE2ETests.cs` | `PlaylistsE2ETestBase` |
| `ProgramSettingsE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `SeasonalGenresE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `TVSenderAdminE2ETests_FfmpegDownload.cs` | `TVSenderAdminE2ETestBase` + `FfmpegPackageServer` (zweiter echter Kestrel-Loopback-Host) |
| `TvSenderOnAirE2ETests.cs` | `TVSenderAdminE2ETestBase` |
| `UnlockedMediaE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `UnlockedSourceE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |
| `UpdatesPageE2ETests.cs` | eigene `IAsyncLifetime`-Fixture |

### Ohne `Category`-Trait — laufen heute in der blockierenden `Category!=E2E`-Lane (8 Dateien)

**Befund:** Diese Playwright-Klassen tragen keinerlei `Category`-Trait und laufen deshalb aktuell im „Unit"-Schritt `Category!=E2E` — mit echtem Browser und echtem Port, genau die Klase Fehlschläge, die das Issue aus der blockierenden Lane entfernen will. Verifiziert im Baseline-Lauf (`TVSenderAdminValidityE2ETests` ~27 s, `TVSenderAdminE2ETests` ~36 s mit echtem Chromium unter Windows).

| Datei | Basisklasse |
|-------|-------------|
| `LiveTvE2ETests.cs` | `PlaylistsE2ETestBase` |
| `LiveTvClipBlocksE2ETests.cs` | `PlaylistsE2ETestBase` |
| `TVSenderAdminE2ETests.cs` | `TVSenderAdminE2ETestBase` |
| `TVSenderAdminE2ETests_AgeWindow.cs` | `TVSenderAdminE2ETestBase` |
| `TVSenderAdminClipBlocksE2ETests.cs` | `TVSenderAdminE2ETestBase` |
| `TVSenderAdminClipGroupsE2ETests.cs` | `TVSenderAdminE2ETestBase` |
| `TVSenderAdminDayBarE2ETests.cs` | `TVSenderAdminE2ETestBase` |
| `TVSenderAdminValidityE2ETests.cs` | `TVSenderAdminE2ETestBase` |

Die Requirement-Angabe „alle 79 `Category=E2E`-Klassen (Playwright)" ist daher zu korrigieren: 79 = Anzahl der `[Trait("Category","E2E")]`-Vorkommen (Klassen- und Methodenebene in 53 Dateien, davon 34 Browser-Klassen), nicht die Anzahl der Playwright-Klassen (42 = 34 getaggt + 8 ungetaggt).

## Kategorie B — Echte Kindprozesse / externe Binaries

| Datei | Trait | Befund |
|-------|-------|--------|
| `Services/FfmpegLiveStreamSegmenterE2ETests.cs` | (E2E) | Echter ffmpeg-Kindprozess segmentiert reale lavfi-generierte Videos; 7 Tests, Deadlines 45–60 s, `E2EBinaryLocator`-Abbruch ohne Binary, Temp-SegDirs mit geschütztem Delete |
| `MediaProbeHttpIntegrationTests.cs` | (E2E) | Echter Kestrel-Loopback + echter ffmpeg-Videogenerator (`Process.Start`, Z. 143) + echtes ffprobe gegen Token-URI; Skip-Pfad ohne Binary |
| `LiveStreamE2ETests.cs` | (E2E) | `PairedDevice_ConfiguredStandbyClip_PreparesAndServes` startet echtes ffmpeg (`Process.Start`, Z. 223) und pollt die Standby-Playlist bis 60 s (120×500 ms) |
| `Services/LocalMediaSourceReaderTests.cs` | (ohne Trait) | `TryCreateJunction` (Z. 298–326): Windows `cmd.exe /c mklink /J` (echter Kindprozess, Admin/SymbolicLink-Recht nötig) bzw. Unix `Directory.CreateSymbolicLink` — NTFS-Junction/Symlink-Szenario |
| `Helpers/TestHelpers.cs` (`RunIcacls`, Z. 288–303) | — | Windows `icacls`-Kindprozess bzw. `File.SetUnixFileMode` für `DenyReadAccess`/`ResetAccessControl`; benutzt von `MediaSourceAdminDetailsValidationTests` (ohne Trait) und `MediaSourceLocalDirectoryE2ETests` (E2E) |
| `Services/SegmenterProcessSupportTests.cs` | (ohne Trait) | `StartRealProcess_UnstartableBinary_*` startet real `SegmenterProcessSupport.StartRealProcess` auf eine nicht existierende `.exe` im Temp-Pfad (Start-Versuch = OS-Kontakt, schlägt kontrolliert fehl). Alle anderen Tests nutzen `FakePumpProcess` — grenzwertig; nach dem Assertionskriterium eher regulär |
| `Services/FfmpegToolDownloadServiceTests_Install.cs` | (ohne Trait) | `Run_OnUnix_SetsExecutableBit` — `Assert.SkipWhen(OperatingSystem.IsWindows())`: auf Unix echtes `chmod`-Verhalten des Downloads; schreibt reale Marker-Dateien ins Temp-Verzeichnis |
| `Services/FfmpegToolDownloadServiceTests_Extraction.cs` | (ohne Trait) | Echte Zip-/tar.xz-Archivextraktion ins Temp-Verzeichnis (Traversalschutz-Test), ungeschützte Pfade teils vorhanden |
| `Services/FfmpegToolDownloadServiceTests_Failures.cs` | (ohne Trait) | Reale Temp-Dateien (`ffmpeg-tools-{id}.download` in `GetTempPath`, Z. 232), Datei-vs.-Verzeichnis-Konflikte |
| `Services/FfmpegToolDownloadServiceTests_Start.cs` | (ohne Trait) | Reale Temp-Verzeichnisse via `FfmpegToolDownloadTestDoubles` (`vwp-ffmpegdl-{guid}`, Z. 280 f.) |

Keine echten Prozesse (nur `ISegmenterProcess`-Fakes, `ProcessStartInfo` wird lediglich aufgezeichnet und asserted): `Services/FfmpegLiveStreamSegmenterTests.cs`, `Services/FfmpegLiveStreamSegmenterTests_FirstOutput.cs`, `Services/LiveStreamStandbySegmenterTests.cs` — siehe Kategorie E/F (Temp-Dirs, Zeitbudgets).

## Kategorie C — Loopback-Ports / Netzwerk (ohne Browser)

| Datei | Trait | Befund |
|-------|-------|--------|
| `UdpDiscoveryListenerTests.cs` | (ohne Trait) | Echte `UdpClient`-Sockets auf `IPAddress.Loopback`: Listener starten/stoppen, Rebind, "Port already bound"-Szenario, Datagramm-Loss vor Bind; 10-s-Deadlines, 50/500-ms-Polling, `SocketException`-Races Windows vs. Linux ausdrücklich dokumentiert — **klarster OS-Interface-Kandidat außerhalb von E2E** |
| `Helpers/FfmpegPackageServer.cs` | — | Echter Kestrel-Loopback-Host (`Listen(Loopback, 0)`), genutzt von `TVSenderAdminE2ETests_FfmpegDownload` |
| `MediaProbeHttpIntegrationTests.cs` | (E2E) | siehe Kategorie B — echter Kestrel + echter `HttpClient` gegen Loopback |
| Alle Playwright-Klassen (Kategorie A) | gemischt | `UseUrls("http://127.0.0.1:0")` + `UseKestrel()` |

In-Memory-`TestServer` (`WebApplicationFactory` **ohne** `UseKestrel`/`StartServer`) gilt nicht als Loopback-Port: `DeviceClientTestBase`, `LocalMediaPlaylistE2ETestBase`, `PairingExchangeContractTestBase`, `PairingWebApplicationFactory`-Nutzer — deren OS-Kontakt ist die Datei-DB (Kategorie D).

## Kategorie D — SQLite-Dateibanken + WebApplicationFactory (TestServer, kein Browser, kein Port)

Temporäre `.db`-Datei unter `Path.GetTempPath()` (lockanfällig unter Windows — Delete im Dispose ist überall `try/catch`-geschützt). Assertion hängt am echten SQLite-Verhalten gegen Datei — OS-Kontakt: Dateisystem + Locking.

| Datei | Trait | Fixture |
|-------|-------|---------|
| `InternalMediaStreamControllerTests.cs` | **(ohne Trait)** | eigene `IDisposable`-Fixture, `_dbPath` Z. 28, `WebApplicationFactory` TestServer; geschützte Deletes Z. 188–189 |
| `ClientLibrary/InternalVideoWebPlayerClientTests_Impersonation.cs` | (ohne Trait) | `DeviceClientTestBase` → `PairingWebApplicationFactory.CreateTempDbPath` |
| `ClientLibrary/VideoWebPlayerClientTests_DeviceRunEndToEnd.cs` | (ohne Trait) | `DeviceClientTestBase` |
| `ClientLibrary/VideoWebPlayerClientTests_DeviceTokenHeader.cs` | (ohne Trait) | `DeviceClientTestBase` |
| `ClientLibrary/VideoWebPlayerClientTests_Pairing.cs` | (ohne Trait) | `DeviceClientTestBase` |
| `ClientLibrary/VideoWebPlayerClientTests_PlaylistSessionRenewal.cs` | (ohne Trait) | `DeviceClientTestBase` |
| `ClientLibrary/VideoWebPlayerClientTests_ProgramGuide.cs` | (ohne Trait) | `DeviceClientTestBase` |
| `ClientLibrary/VideoWebPlayerClientTests_Reauthorization.cs` | (ohne Trait) | `DeviceClientTestBase` |
| `ClientLibrary/VideoWebPlayerClientTests_RenewalCoverage.cs` | (ohne Trait) | `DeviceClientTestBase` |
| `ClientLibrary/VideoWebPlayerClientTests_Session.cs` | (ohne Trait) | `DeviceClientTestBase` |
| `ClientLibrary/VideoWebPlayerClientTests_StaleUnauthorized.cs` | (ohne Trait) | `DeviceClientTestBase` |
| `PairingBootstrapEndpointsTests.cs` | (ohne Trait) | `PairingWebApplicationFactory` direkt (`_dbPath` Z. 29), `IAsyncLifetime` |
| `PairingExchangeContractTests_Flow.cs` / `_JsonContract.cs` / `_Validation.cs` | (ohne Trait) | `PairingExchangeContractTestBase` → `PairingWebApplicationFactory` |
| `ApiDocumentationContractTests_Runtime.cs` | (ohne Trait) | `PairingWebApplicationFactory` (`_dbPath` Z. 39) |
| `DevicePlaylistE2ETests_MultiUser/_Playback/_PlaylistAccess/_SessionRenewal` | (E2E) | `DeviceClientTestBase` (TestServer + Datei-DB) |
| `LiveStreamE2ETests.cs` | (E2E) | `DeviceClientTestBase` + echtes Temp-Segmentverzeichnis |
| `ProgramGuideE2ETests.cs` | (E2E) | `DeviceClientTestBase` |
| `LocalMediaPlaylistE2ETests_*` (4 Dateien) | (E2E) | `LocalMediaPlaylistE2ETestBase` (TestServer + Datei-DB + echtes Medienverzeichnis) |
| `AboutPageE2ETests`, `ContinueWatchingE2ETests`, `EpisodesBackgroundImageAccessTokenE2ETests`, `FirstUserRedirectE2ETests`, `IdentityRedirectManagerE2ETests`, `LocalMediaSourcePipelineE2ETests`, `LocalMediaSourceStreamingE2ETests` | (E2E) | `IDisposable`-Fixtures mit eigenem `_dbPath` + `WebApplicationFactory` (TestServer) — HTTP-Level-„E2E" ohne Browser |

Zu beurteilen, ob die TestServer/Datei-DB-Klassen als `OsInterface` gelten sollen: die Assertionen hängen am HTTP-/Service-Verhalten, nicht am OS — der OS-Kontakt ist Fixture-Plumbing (Temp-DB-Datei). Die Blaupause würde sie nach dem Assertionskriterium eher regulär einstufen; Gegenargument: die Datei-Locks machen sie lockanfällig (Windows). **Zu entscheiden** — im Trait-Modell berücksichtigen.

## Kategorie E — Echtes Dateisystem / Temp-Verzeichnisse (deterministisch nutzbar, aber Cleanup-/Plattform-sensitiv)

Temp-Verzeichnisse unter `GetTempPath()` mit echten Dateibäumen. Risiko liegt im Cleanup (Locks, Attribute, Reste bei Parallelbetrieb), nicht in der Assertion selbst — nach Blaupausen-Kriterium regulär, aber **Cleanup-Härtungskandidaten** (siehe `test-infrastructure.md`).

| Datei | Besonderheit |
|-------|--------------|
| `Services/LocalMediaSourceReaderTests.cs` | Junction/Symlink (siehe B) + 2 Temp-Dirs, geschützt |
| `Services/LocalMediaSourceClassifierTests*.cs` (3 Dateien) | echte Medienbäume + `MediaSourceClassifierCollection`; Deletes geschützt |
| `MediaSourceScanServiceTests.cs` | echter Scan über Temp-Bäume, 2–10-s-Poll-Deadlines (F) |
| `Services/MediaSourceScannerLocalTests.cs`, `Services/MediaSourceScanCoordinatorScanTests.cs`, `Services/MediaSourceScanCoordinatorScopeTests.cs` | Temp-Roots, Deletes geschützt |
| `Services/LiveStreamStandbySegmenterTests.cs` | `_cacheDir` Temp, **`Directory.Delete` ungeschützt** (Z. 131), Fake-Prozesse + Zeitbudget-Tests |
| `LiveStreamCoordinatorTests.cs` | `_contentRoot` Temp, **`Directory.Delete` ungeschützt** (Z. 128) |
| `LiveStreamWorkerTests.cs` | `_contentRoot` Temp, **`Directory.Delete` ungeschützt** (Z. 116) |
| `Controllers/LiveStreamControllerTests.cs` | `_contentRoot` Temp, **`Directory.Delete` ungeschützt** (Z. 65); testet zusätzlich `File.Delete`-Lock-Semantik (Z. 140–157) |
| `Services/LiveStreamSegmentStoreTests.cs` | Temp, **`Directory.Delete` ungeschützt** (Z. 49) |
| `Services/LiveStreamStandbyProviderTests.cs` | Temp, **`Directory.Delete` ungeschützt** (Z. 37) |
| `Services/LiveStreamMediaResolverTests.cs` | Temp, **`Directory.Delete` ungeschützt** (Z. 65) |
| `Services/StaticAssetVersionerTests.cs` | Temp, **`Directory.Delete` ungeschützt** (Z. 17) |
| `Services/FfmpegLocatorTests.cs` | Temp + Stub-Exes, **`Directory.Delete` ungeschützt** (Z. 19); PATH-Injection per Delegate — deterministisch |
| `Services/MsToolsUpdaterIntegrationTests.cs` | Temp-Root, **`Directory.Delete` ungeschützt** (Z. 113); RID-/Plattform-abhängige Paketpfade |
| `Services/AutoUpdateProtectedFilesTests.cs` | Temp-Root, **`Directory.Delete` ungeschützt** (Z. 167) |
| `Extensions/LocalConfigurationTests.cs` | Temp-Root, **`Directory.Delete` ungeschützt** (Z. 128) |
| `Services/UpdateBackupCoordinatorTests.cs`, `Services/UpdateBackupEventBinderTests.cs` | Temp, **ungeschützt** (Z. 27) |
| `Services/VideoWebPlayerUpdateBackupServiceTests.cs` | Temp, **ungeschützt** (Z. 117) |
| `Services/Backups/VideoWebPlayerBackupFacadeTests.cs` | Temp, **ungeschützt** (Z. 152) |
| `Helpers/BackupUploadSessionServiceTestSupport.cs` | `TempDirectory.Dispose` geschützt (Z. 59–70), aber `DisposeSessionAsync` `File.Delete(session.TempPath)` **ungeschützt** (Z. 33–34) |
| `Services/FfprobeLocatorTests.cs`, `Services/LocalMediaSourceReaderTests.cs`, `Services/MediaDurationProbeServiceTests.cs`, `Services/MediaDurationProbeWorkerTests.cs`, `Services/MediaSourceReaderDispatcherTests.cs`, `Services/MediaSourceScannerLocalTests.cs`, `Services/MediaSourceScanCoordinator*Tests.cs`, `Controllers/ItemsControllerLocalSourceTests.cs`, `Controllers/ItemsControllerMediaFileTypeTests.cs`, `InternalMediaStreamControllerTests.cs`, `MediaSourceAdminDetailsValidationTests.cs`, `Services/UpdateBackupEventBinderTests.cs` u. a. | geschützte Deletes (`try/catch`) — Vereinheitlichungskandidaten für `TempDirectoryCleanup` |
| `Services/FfmpegLiveStreamSegmenterTests_FirstOutput.cs` | `_tempDirs`-Registry, Dispose geschützt (Z. 113–120) |
| `Services/FfmpegLiveStreamSegmenterE2ETests.cs` | Temp-SegDirs, `finally` + geschützt |
| `Components/GenreAdminTests.cs` | **`Path.GetTempPath()/genre-admin-wwwroot` — fester Pfad, kein Cleanup** (Z. 61): Kollisionsrisiko bei Parallel-/Folgeläufen |
| `ApiDocumentationContractTests.cs` | liest `docs/API.md` aus dem Repo-Checkout (Z. 229 ff.) — vom Arbeitsverzeichnis abhängig |
| `Services/Backups/*` | `LegacyBackupArchiveBuilder`, `VideoWebPlayerBackupDataTests*` — echte Zip/DB-Dateien in Temp |
| `Services/EpisodeBackgroundImage/EpisodeBackgroundImageServiceTests.cs` | Temp-Bilddateien |

## Kategorie F — Enge Zeitbudgets / Polling-Loops (ohne/zusätzlich zu anderen Kategorien)

| Datei | Budget |
|-------|--------|
| `Helpers/TestHelpers.cs` | `WaitFor*`-Familie: 10-ms-Poll mit Caller-Timeout (2–10 s), SQLite-Retry bei Error 5 (database locked) |
| `MediaSourceScanServiceTests.cs` | `WaitFor*` mit 2–10 s (`waitTimeSec = 10`, Z. 293) |
| `LiveStreamWorkerTests.cs` | `cts.CancelAfter(15–30 s)` + 50/100-ms-Poll-Schleifen; `ControllableTimeProvider` für Timer |
| `ProgramScheduleWorkerTests.cs`, `ProgramScheduleWorkerTests_SideEffects.cs` | `Task.Delay(10–200)`-Poll-Loops gegen Worker-Ticks |
| `Services/FfmpegLiveStreamSegmenterTests_FirstOutput.cs` | `WaitUntilAsync`: 200×10 ms = 2 s Host-Poll gegen `firstOutputWarnAfter`-Konfiguration des Segmenters |
| `Services/FfmpegLiveStreamSegmenterE2ETests.cs` | `timeout.CancelAfter(45–60 s)` gegen echte ffmpeg-Segmentierung |
| `LiveStreamE2ETests.cs` | 120×500 ms = 60 s Poll auf Standby-Playlist |
| `UdpDiscoveryListenerTests.cs` | 10-s-Deadlines, 50-ms-Polls, 500-ms-`WaitAsync`-Fenster |
| `Helpers/PlaylistsE2ETestBase.cs` | `Page.SetDefaultTimeout(30_000)`; `InteractionAttemptTimeout` 10 s; `InteractionDeadlineMilliseconds` 60 s; feste `WaitForTimeoutAsync(1000–2000)` |
| `Helpers/TVSenderAdminE2ETestBase.cs` | `WaitForSelectorAsync(Timeout = 30_000)`, `SwitchTabAsync` via `DriveUntilStateAsync` |
| `Services/SegmenterProcessSupportTests.cs` | Drain-Budgets 50 ms/5 s — testet das Timeout-Verhalten selbst (Host-Budget absichtlich knapp, kein Mismatch) |
| `Services/MediaSourceScanCoordinatorScopeTests.cs` | Restore-Lease-Waits (`RunFullScanAsync_WaitsForRestoreLease_ThenRuns`) |
| `Services/LiveStreamStandbySegmenterTests.cs` | konfigurierbare `transcodeTimeout`/`stderrDrainTimeout` gegen Fake-Prozesse; `UnkillableStallingProcess` testet Drain-Abbruch |
| `ContinueWatching*`-Tests u. a. | Signal-/Event-Waits teilweise zeitgebunden |

### Produktiv-Timeouts (Vergleichsmaßstab für die Deadline-Angleichung)

| Stelle | Budget |
|--------|--------|
| `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs:90` | `StopTimeout = 10 s` (`WaitForExitAsync.WaitAsync`, Z. 292) |
| `:93` | `ExitDetectionTimeout = 5 s` (Z. 473) |
| `:98` | `FirstOutputPollInterval = 500 ms` |
| `:369` | Segmentierungs-Toleranz `max(10 s, 2×SegmentSeconds)` |
| `:528–530` | Horizont `max(15 s, 3×SegmentSeconds)` bzw. `RemainingSeconds + max(30 s, 3×SegmentSeconds)` |
| `VideoWebPlayer/Services/SegmenterProcessSupport.cs:59` | `PumpDrainTimeout = 10 s` |
| `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs:31,49,53` | `MaxStandbyDurationSeconds = 60` → `TranscodeTimeout = 120 s`, `StderrDrainTimeout = 10 s` |
| CI-Job `build-and-test` | `timeout-minutes: 30` |
| `TestHelpers.RunIcacls` / `LocalMediaSourceReaderTests.TryCreateJunction` | `WaitForExit(10_000)` |

**Deadline-Befund:** Host-Polls (10–60 s in Tests) liegen klar über den Kind-Budgets (5–120 s in Produktivcode) — der Blaupausen-Fehler „Host 30 s < Kind 90 s" ist hier so nicht eingebaut; bemerkenswert knapp sind eher die 2–10-s-`WaitFor*`-Deadlines in `MediaSourceScanServiceTests` und die 45-s-ffmpeg-Deadlines in `FfmpegLiveStreamSegmenterE2ETests` gegen ein 30-min-Job-Budget. Details: `time-budgets.md`.

## Kategorie G — Plattformabhängige Tests (OS-Gates)

| Stelle | Verhalten |
|--------|-----------|
| `Services/FfmpegToolDownloadServiceTests_Install.cs:79` | `Assert.SkipWhen(OperatingSystem.IsWindows(), …)` — einziger `SkipWhen` im Projekt; unter Windows `NotExecuted` |
| `Services/LocalMediaSourceReaderTests.cs:298` | Windows `mklink /J` vs. Unix `CreateSymbolicLink` |
| `Helpers/TestHelpers.cs:261–286` | Windows `icacls` vs. Unix `SetUnixFileMode` |
| `Services/FfmpegLocatorTests.cs`, `FfprobeLocatorTests.cs`, `E2EBinaryLocator.cs` | `OperatingSystem.IsWindows()`-Abzweigungen (`.exe`-Suffix) — selbst aber umgebungsunabhängig (Stub-Dateien) |
| Diverse ffmpeg-E2E | `E2EBinaryLocator.Find("ffmpeg")` → stiller Return ohne Binary = **grün ohne Nachweis** |

**Ehrlichkeitsbefund:** Mehrere Selbst-Skip-Muster lassen Tests ohne Binary/Browser einfach „bestanden" zurückkehren (`E2EBinaryLocator`-Null-Return in `FfmpegLiveStreamSegmenterE2ETests`, `LiveStreamE2ETests`, `MediaProbeHttpIntegrationTests`; `SkipBrowser`-Flag in `PlaylistsE2ETestBase`/`MediaBoxContextMenuE2ETestBase` samt `if (SkipBrowser) return;` in den Testmethoden). In der CI installiert der Workflow die Playwright-Browser vorher (`playwright.ps1 install --with-deps chromium`), ffmpeg liegt auf Ubuntu-Runnern vor — lokal sind diese Tests sonst still grün. Relevant für die Flake-Statistik: ein `NotExecuted`-Outcome entsteht nur über `Assert.SkipWhen`, nicht über `return`-Pfade — diese Pfade erscheinen im TRX als `Passed`.

## Kategorie H — Explizit regulär (Schlüsselwort-Fehlpositive)

| Datei | Warum regulär |
|-------|---------------|
| `KestrelLimitsTests.cs` | reine String-Parserei (`KestrelLimits.ParseMaxRequestBodySize`), kein Kestrel-Host |
| `Services/FfprobeMediaDurationProbeTests.cs` | `FakeFfprobeLocator` — 127.0.0.1-Strings nur als stderr-Fixture-Text |
| `Services/InternalMediaStreamTokenServiceTests.cs` | `Kestrel:Endpoints:*` nur Konfigurationswerte |
| `Services/FfmpegLiveStreamSegmenterTests.cs` | `FakeSegmenterProcess`/`ControlledPipeStream`; keine Temp-Dateien, keine echten Prozesse |
| `MdnsAdvertiserWorkerTests.cs`, `Mdns*Tests.cs` | Fakes, keine Sockets |
| `Components/*` (28 Dateien) | bUnit `BunitContext` — in-process DOM, kein Browser (Ausnahme: `GenreAdminTests` → Kategorie E wegen Temp-wwwroot) |
| In-Memory-SQLite-Tests (`Data Source=file:{name}?mode=memory&cache=shared`; `PairingTestDb`, `ProgramScheduleTestDb`, `PlaylistServiceTestBase`, `PlaylistsControllerTestBase`, `ItemsControllerTestFactory`, ~50 Dateien) | echte SQLite-Engine, aber ohne Dateisystem-Kontakt — nach DoD bewusst so gewählt (FK/Unique-Tests brauchen echtes SQLite, nicht EF InMemory) |
| EF-`InMemory`-Tests (15 Dateien, `UseInMemoryDatabase`) | deterministisch in-process |

## Zusammenfassung des Audits

| Kategorie | Umfang | Heute blockierend? |
|-----------|--------|--------------------|
| A. Playwright+Kestrel+Temp-DB (traited) | 34 Dateien | ja (E2E-Schritt) |
| A. Playwright+Kestrel+Temp-DB (**ohne Trait**) | **8 Dateien** | **ja — im `Category!=E2E`-Schritt** |
| B. Echte Kindprozesse | 3 traited + ~7 untraited Dateien | gemischt |
| C. UDP/Kestrel ohne Browser | `UdpDiscoveryListenerTests` + Helfer | ja (untraited) |
| D. TestServer + SQLite-Dateibank | 17 traited (E2E), **16 untraited** (`ClientLibrary/*`, `Pairing*`, `InternalMediaStreamControllerTests`, `ApiDocumentationContractTests_Runtime`) | ja (untraited im Unit-Schritt) |
| E. Temp-Dateisystem (Cleanup-Kandidaten) | ~40 Dateien, davon **16 Stellen mit ungeschütztem `Directory.Delete`** | ja |
| F. Enge Zeitbudgets | ~15 Dateien | ja |
| G. OS-Gates | 4 Muster | — |
| H. Regulär trotz Keyword | ~20+ Dateien | — |

Offene Klassifizierungsfrage für die Trait-Vergabe: (1) die 8 ungetaggten Playwright-Klassen — Bug im Trait-Modell, entweder `OsInterface` oder fehlendes `E2E` nachziehen; (2) TestServer/Datei-DB-Klassen (D) — OS-Interface per Lockanfälligkeit oder regulär per Assertionskriterium; (3) `SegmenterProcessSupportTests` — ein Methoden-Test mit realem Start-Versuch (nur Methode taggen oder Fake nutzen).
