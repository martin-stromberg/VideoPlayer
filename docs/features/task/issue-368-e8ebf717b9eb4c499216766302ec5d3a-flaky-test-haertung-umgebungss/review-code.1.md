# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Vier Befunde, alle geringer Schwere — keiner blockiert die Lane-Trennung. Die zentralen
Mechanismen wurden empirisch verifiziert: `FlakyConventionTests` schlägt bei einer
eingebauten E2E-ohne-OsInterface-Mutation tatsächlich fehl (Mutationsprobe, danach
entfernt), die Lane-Partition ist vollständig (2.792 Tests = 2.368 regulär + 424
OS-Interface, per `--list-tests` gezählt), `Get-FlakyTestNames` findet exakt den einen
`[Flaky]`-Test, und `Retry-FlakyTests.ps1` lief gegen ein reales TRX korrekt
(recovered → `::notice::` → Exit 0). `Analyze-FlakyTestResults.ps1` wertete die
Inventory-TRXs korrekt aus. `Xunit.TraitAttribute` implementiert `ITraitAttribute`
(reflektiv verifiziert), die `FactAttribute`/`TheoryAttribute`-Konstruktor-Signaturen
mit `CallerFilePath`/`CallerLineNumber` passen. Host-Budgets > Kind-Budgets stimmen
(Host-Poll 150 s > `TranscodeTimeout` = 2 × 60 s; `WaitUntilAsync` 10 s > 500-ms-Raster
des Kinds; Generator-Budgets 60/120 s ≪ Job-Timeout). Workflow-Abhängigkeiten sind
korrekt: `os-interface-tests` steht in keiner `needs:`-Kette blockierender Jobs,
`if: failure()`/`if: always()` und `continue-on-error` sind konsistent gesetzt, das
Artefakt-Glob deckt das Retry-TRX ab. Keine Secrets, keine Injection-Pfade in den
Skripten (Testnamen werden nur in den `--filter`-String eingesetzt; Worst Case ist ein
Syntax-Fehler im Filter, kein Code-Einschleusen).

## Befunde

### MediaProbeHttpIntegrationTests.cs (MediaProbeHttpIntegrationTests)

- **Fehlerbehandlung / toter Code** — Zeile 154–157 i. V. m. `DisposeAsync` (~Z. 79–88):
  `using var process` disposed das `Process`-Objekt bereits am Ende von
  `CreateTestVideoAsync`. Das in `_processesToKill` registrierte Objekt ist in
  `DisposeAsync` daher garantiert disposed — `process.HasExited`/`Kill` werfen
  `ObjectDisposedException`, das `TempDirectoryCleanup` still schluckt. Das im
  Kommentar deklarierte „Kill-Sicherheitsnetz" kann so nie greifen: Ein die Methode
  überlebender ffmpeg-Generator (Abbruchpfad zwischen `Process.Start` und
  `WaitForExitAsync`) bliebe unkilled. Zusätzlich doppelt entsorgt (`using` plus
  Dispose-Schleife in `DisposeAsync`).

  Empfehlung: `using` entfernen — die `DisposeAsync`-Schleife entsorgt die
  registrierten Prozesse bereits; nur so kann `TempDirectoryCleanup.DeleteBestEffort`
  das Kill-Netz tatsächlich auslösen. Alternativ Registrierung und Kommentar
  entfernen, wenn das Netz bewusst nicht gebraucht wird.

### scripts/Retry-FlakyTests.ps1 + scripts/Analyze-FlakyTestResults.ps1

- **Doppelter Code** — `ConvertTo-MethodName` ist identisch in beiden Skripten
  dupliziert (`Retry-FlakyTests.ps1` Z. 61–66, `Analyze-FlakyTestResults.ps1`
  Z. 103–108). Genau für solche gemeinsamen Helfer wurden `scripts/lib/Trx.ps1` und
  `scripts/lib/TestDiscovery.ps1` angelegt. Ebenso leicht dupliziert: das
  `Write-Annotation`/`$inGitHubActions`-Muster (Retry Z. 55–59; Analyzer inliniert es
  zweimal, Z. 264/265 und 285/286).

  Empfehlung: `ConvertTo-MethodName` in eine Lib auslagern (z. B.
  `scripts/lib/TestDiscovery.ps1` oder eigene `Naming.ps1`); optional auch
  `Write-Annotation` dorthin heben.

### scripts/Analyze-FlakyTestResults.ps1

- **Kommentar-Genauigkeit** — Die Kommentare in Z. 17–21, 169–173 und 191–193 nennen
  `SOFTWARESCHMIEDE_SKIP_*`-Flags als Beispiel für `NotExecuted`-Ursachen. Solche
  Flags existieren in diesem Repository nicht (nur in
  `inventory/blueprint-comparison.md` erwähnt); die tatsächlichen Mechanismen sind
  `[Fact(Skip = …)]` (ergibt `NotExecuted`) und `if (SkipBrowser) return;`
  (ergibt sogar `Passed`). Die `NotExecuted`-Logik selbst ist korrekt, nur der
  konkrete Flag-Name stammt aus der Blaupause.

  Empfehlung: Kommentar an die realen Skip-Mechanismen anpassen oder den Flag-Namen
  generisch formulieren.

### VideoWebPlayer.Tests/Infrastructure/Testing/OsInterfaceTheoryAttribute.cs

- **Speculative Generality** — Das Attribut ist angelegt und in
  `docs/help/stabilitaet/os-interface-tests.md` als Alternative dokumentiert, wird
  aber im gesamten Projekt nie verwendet (`OsInterfaceFactAttribute` hat genau einen
  Verwender in `TempDirectoryCleanupTests`). Als dokumentierte, symmetrische
  API-Ergänzung zu `[OsInterfaceFact]` vertretbar — dann aber als bewusste
  Designentscheidung stehen lassen.

  Empfehlung: Entweder entfernen und bei Bedarf nachziehen, oder bestehen lassen —
  die Hilfe-Doku begründet die API bereits; in diesem Fall ist kein Fix nötig und
  der Punkt nur protokolliert.

## Geprüfte Dateien

Workflows und Skripte:
- `.github/workflows/pr-staging-ci.yml`
- `.github/workflows/staging-ci.yml`
- `.github/workflows/release.yml`
- `scripts/lib/Trx.ps1`
- `scripts/lib/TestDiscovery.ps1`
- `scripts/Retry-FlakyTests.ps1`
- `scripts/Analyze-FlakyTestResults.ps1`

Neue Testinfrastruktur und Helper:
- `VideoWebPlayer.Tests/Infrastructure/Testing/TestCategories.cs`
- `VideoWebPlayer.Tests/Infrastructure/Testing/OsInterfaceAttribute.cs`
- `VideoWebPlayer.Tests/Infrastructure/Testing/FlakyAttribute.cs`
- `VideoWebPlayer.Tests/Infrastructure/Testing/OsInterfaceFactAttribute.cs`
- `VideoWebPlayer.Tests/Infrastructure/Testing/OsInterfaceTheoryAttribute.cs`
- `VideoWebPlayer.Tests/Infrastructure/Testing/FlakyConventionTests.cs`
- `VideoWebPlayer.Tests/Helpers/TempDirectoryCleanup.cs`
- `VideoWebPlayer.Tests/Helpers/TempDirectoryCleanupTests.cs`
- `VideoWebPlayer.Tests/Helpers/TestHelpers.cs` (`RunIcacls`-Timeout/Kill + Diagnose)
- `VideoWebPlayer.Tests/Helpers/BackupUploadSessionServiceTestSupport.cs`
- `VideoWebPlayer.Tests/Helpers/DeviceClientTestBase.cs`
- `VideoWebPlayer.Tests/Helpers/LocalMediaPlaylistE2ETestBase.cs`
- `VideoWebPlayer.Tests/Helpers/MediaBoxContextMenuE2ETestBase.cs`
- `VideoWebPlayer.Tests/Helpers/PairingExchangeContractTestBase.cs`
- `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`

Testdateien mit inhaltlichen Änderungen (im Detail gelesen):
- `VideoWebPlayer.Tests/Components/GenreAdminTests.cs` (GUID-Temp-Pfad + Dispose)
- `VideoWebPlayer.Tests/LiveStreamE2ETests.cs` (Generator-Budget 120 s + Kill-Netz, Poll 150 s)
- `VideoWebPlayer.Tests/MediaProbeHttpIntegrationTests.cs` (siehe Befund)
- `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs` (Wait-Deadlines → 30 s)
- `VideoWebPlayer.Tests/Services/MediaSourceScanCoordinatorScopeTests.cs` (Wake-Deadline → 30 s)
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests_FirstOutput.cs` (WaitUntilAsync → 10 s)
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs` (GenerateVideoAsync-Budget)
- `VideoWebPlayer.Tests/Services/LocalMediaSourceReaderTests.cs` (TryCreateJunction-Diagnose + Kill)
- `VideoWebPlayer.Tests/Services/SegmenterProcessSupportTests.cs` (methodenebenes `[OsInterface]`)
- `VideoWebPlayer.Tests/TvSenderOnAirE2ETests.cs` (einzige `[Flaky]`-Vergabe, Methode, OsInterface auf Klasse vorhanden)

Übrige Testdateien (Trait-Vergabe `[OsInterface]` bzw. `[Trait("Category","E2E")]` +
Cleanup-Umstellung, Muster geprüft und per Vollscan verifiziert — alle konkreten
E2E-/Helper-Ableitungen tragen `OsInterface`, keine Datei ohne Trait vergessen):
- `VideoWebPlayer.Tests/AboutPageE2ETests.cs`, `AgeRatingE2ETests.cs`,
  `ApiDocumentationContractTests_Runtime.cs`, `BackupUploadE2ETests.cs`,
  `ClientLibrary/*.cs` (10 Dateien), `ContinueWatchingE2ETests.cs`,
  `Controllers/ItemsControllerLocalSourceTests.cs`,
  `Controllers/ItemsControllerMediaFileTypeTests.cs`,
  `Controllers/LiveStreamControllerTests.cs`, `DevicePairingE2ETests.cs`,
  `DevicePlaylistE2ETests_*.cs` (4), `EpisodesBackgroundImageAccessTokenE2ETests.cs`,
  `Extensions/LocalConfigurationTests.cs`, `FirstUserRedirectE2ETests.cs`,
  `IdentityRedirectManagerE2ETests.cs`, `InternalMediaStreamControllerTests.cs`,
  `LiveStreamCoordinatorTests.cs`, `LiveStreamWorkerTests.cs`,
  `LiveTvClipBlocksE2ETests.cs`, `LiveTvE2ETests.cs`,
  `LocalMediaPlaylistE2ETests_*.cs` (4), `LocalMediaSourceClassifierTests*.cs` (3),
  `LocalMediaSourcePipelineE2ETests.cs`, `LocalMediaSourceStreamingE2ETests.cs`,
  `MediaBoxContextMenu*E2ETests.cs` (2), `MediaSourceAdmin*Tests.cs` (2),
  `MediaSourceDelete/ExplorerRescan/HiddenInMenu/LocalDirectory/Switch E2ETests.cs` (5),
  `MovieCollectionEditE2ETests.cs`, `PairingBootstrapE2ETests.cs`,
  `PairingBootstrapEndpointsTests.cs`, `PairingExchangeContractTests_*.cs` (3),
  `Playlist*E2ETests.cs` (13), `ProgramGuideE2ETests.cs`, `ProgramSettingsE2ETests.cs`,
  `SeasonalGenresE2ETests.cs`, `Services/AutoUpdateProtectedFilesTests.cs`,
  `Services/Backups/*.cs` (3), `Services/FfmpegLocatorTests.cs`,
  `Services/FfmpegToolDownloadServiceTests_*.cs` (4), `Services/FfprobeLocatorTests.cs`,
  `Services/LiveStream*Tests.cs` (4), `Services/Media*Tests.cs` (3),
  `Services/MsToolsUpdaterIntegrationTests.cs`, `Services/StaticAssetVersionerTests.cs`,
  `Services/UpdateBackup*Tests.cs` (2), `Services/VideoWebPlayerUpdateBackupServiceTests.cs`,
  `TVSenderAdmin*E2ETests.cs` (7), `UdpDiscoveryListenerTests.cs`,
  `Unlocked*E2ETests.cs` (2), `UpdatesPageE2ETests.cs`

Dokumentation und Prozessartefakte:
- `README.md`, `docs/RELEASE_NOTES.md`, `docs/STAGING_WORKFLOW.md`, `docs/help/index.md`
- `docs/help/stabilitaet/os-interface-tests.md` (vollständig gelesen, konsistent mit Code)
- `docs/features/task/issue-354-…/inventory/tests.md` (Link-Reparatur, außerhalb des Auftrags — korrekt separat begründet)
- `docs/features/task/issue-368-…/*` (requirement, inventory, plan, plan-check, todo,
  tasks; Inventory-Audit `os-interface-audit.md` und `time-budgets.md` gelesen)
