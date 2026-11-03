# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs (MediaSourceScanServiceTests)

- **Doppelter Code** — Die neue private Methode `BuildScanRunServices` (ca. Zeilen 346–378) baut den kompletten Service-Provider (DbContext, `MediaSourceScanner`, `MediaSourceClassifier`, Logger, `MediaUpdateNotificationService`, `IPlaylistBackfillSignal`, `IProgramScheduleSignal`, `MediaSourceScanCoordinator` usw.) noch einmal praktisch identisch zu der in derselben Branch neu geschaffenen `MediaSourceScanCoordinatorTestServices.Build` (`VideoWebPlayer.Tests/Helpers/MediaSourceScanCoordinatorTestServices.cs`, Zeilen 32–86) auf. Der einzige fachliche Unterschied ist der `TimeProvider` (System vs. `IncrementingTimeProvider`) und der `IMediaSourceReader` (lokal vs. `SeriesSftpMediaSourceReader`). Genau zu dem Zweck, diese Verdrahtung nicht mehrfach zu pflegen, wurde `MediaSourceScanCoordinatorTestServices.Build` in dieser Branch erst geschaffen – hier wird sie nicht verwendet.

  Empfehlung: `MediaSourceScanCoordinatorTestServices.Build` um optionale Parameter für `TimeProvider` und `IMediaSourceReader` erweitern (Default: `TimeProvider.System` bzw. `LocalMediaSourceReader`) und `BuildScanRunServices` durch einen Aufruf dieser Methode ersetzen, statt die Registrierungen erneut aufzuschreiben.

### VideoWebPlayer/Services/MediaSourceScanService.cs (MediaSourceScanService)

- **Hardcodierte Werte** — Zeile 100: `await coordinator.RunFullScanAsync("Automatischer Scanprozess", 64, stoppingToken);`. Vor dem Refactoring stand dieselbe Obergrenze als benannte Konstante `const int maxCollectionsPerRun = 64;` im Code; beim Verschieben der Logik in den Coordinator-Aufruf ist daraus eine unbenannte Magic Number am Aufrufort geworden.

  Empfehlung: Die Obergrenze wieder als benannte Konstante führen (z. B. `private const int MaxCollectionsPerAutomaticRun = 64;` in `MediaSourceScanService`, oder als benannter Default-Parameter/Konstante in `MediaSourceScanCoordinator`) und diese an der Aufrufstelle verwenden statt der nackten `64`.

### VideoWebPlayer.Tests/Services/MediaSourceScanCoordinatorScanTests.cs (MediaSourceScanCoordinatorScanTests)

- **Testqualität** — `RunFullScanAsync_RespectsCap_WhenProvided` (Zeilen 100–137) prüft zwei getrennte fachliche Fälle in einem Testfall: den gedeckelten Lauf (`maxCollectionsPerRun = 1`, `CapReached == true`) und den ungedeckelten Lauf (`maxCollectionsPerRun = null`, `CapReached == false`), jeweils mit eigener Datenbank/eigenem Service-Provider. Beide Teile sind vollständig unabhängig voneinander und könnten jeweils für sich fehlschlagen, ohne dass der Testname den betroffenen Fall erkennen lässt.

  Empfehlung: In zwei Testmethoden aufteilen, z. B. `RunFullScanAsync_RespectsCap_WhenProvided` (nur der gedeckelte Teil, Zeilen 110–122) und `RunFullScanAsync_ScansAllCollections_WhenCapNotProvided` (nur der ungedeckelte Teil, Zeilen 124–136).

### VideoWebPlayer/Services/MediaSourceScanCoordinator.cs (MediaSourceScanCoordinator)

- **Doppelter Code** — `ClassifyPendingCollectionsAsync` (Zeilen 165–179) protokolliert nach der Klassifizierung zwei inhaltlich überlappende Meldungen unmittelbar hintereinander: Zeile 175 `"Klassifizierung der Collections abgeschlossen."` und Zeile 176 `"Klassifizierung abgeschlossen."`. Die zweite Zeile ist in diesem Kontext redundant (es gibt keine weitere, noch ausstehende Klassifizierungsstufe, über die sie sich abgrenzen würde).

  Empfehlung: Eine der beiden Logzeilen entfernen bzw. zu einer Meldung zusammenführen.

## Hinweise

- Der eigentliche Zweck der Änderung – alle drei früheren Ausführungspfade (`MediaSourceScanService`-Worker, `MediaSourceAdmin.razor`, `MediaSourceExplorer.razor`) sowie die Test-Helfer (`LocalMediaPlaylistE2ETestBase`, `AgeRatingE2ETests`, `LocalMediaSourcePipelineE2ETests`) auf den gemeinsamen Einstiegspunkt `MediaSourceScanCoordinator.RunFullScanAsync`/`RescanCollectionTreeAsync` umzustellen, statt die Schrittfolge jeweils selbst nachzubilden – ist konsequent umgesetzt. Kein Befund zu „Ablaufverantwortung bei mehrstufigen Vorgängen" bzw. „Künstlicher Ablauf/Eingefrorener Fehler" in Tests.
- Die beiden vorherigen Befunde aus `review-code.1.md` (doppelte Klassifizierungs-/Zähl-Logik in `RunFullScanAsync`/`RescanCollectionTreeAsync`) sind durch die privaten Hilfsmethoden `CountNewItemsAsync` und `ClassifyPendingCollectionsAsync` behoben.
- `MediaSourceExplorer.razor`: Der `catch`-Block von `RescanCurrent` verwendet jetzt `collectionId` statt `currentCollection.Id` fürs Logging – das behebt beiläufig eine mögliche `NullReferenceException`, falls `currentCollection` zwischenzeitlich `null` wurde. Diese Änderung ist nicht Teil des Auftrags, aber eine sinnvolle Absicherung; sie sollte im Handover als Änderung außerhalb des Auftrags vermerkt sein.
- `MediaSourceAdminFullScanE2ETests.cs` und `MediaSourceExplorerRescanE2ETests.cs` dupliziert den vollständigen `WebApplicationFactory`/Kestrel/Playwright-Boilerplate (Konstruktor, `InitializeAsync`, `DisposeAsync`, `LoginAsAdminAsync`, `SeedDatabaseAsync`) nahezu 1:1. Das entspricht aber exakt dem bereits vor dieser Branch bestehenden Muster in `MediaSourceLocalDirectoryE2ETests.cs` (unveränderte Datei) – es handelt sich um eine vorbestehende Konvention der Playwright-E2E-Tests in diesem Projekt (u. a. weil `PairingWebApplicationFactory.Create` kein `UseUrls`/`UseStaticWebAssets`/`UseKestrel` für echte Browser-Tests bereitstellt), nicht um eine neu eingeführte Abweichung. Kein Befund, da eine Bereinigung alle drei Dateien (inkl. der unveränderten) beträfe und damit über den Auftrag dieses Schritts hinausginge.
- Keine Befunde zu Fehlerbehandlung: `RunFullScanAsync` validiert `maxCollectionsPerRun` vorab mit einer aussagekräftigen `ArgumentOutOfRangeException`; die Razor-Seiten fangen `Exception` nur an der UI-Grenze ab, loggen sie und zeigen eine Statusmeldung.
- Nicht vertieft geprüft, da keine Quelldateien im technischen Sinn: `docs/RELEASE_NOTES.md`, `docs/help/medienquellen/fehlerbehebung-anwender.md`, `docs/features/task/issue-333-86d0f0f6e5984dfdb74ebf2ede9e11ff-scan-pipeline-orchestrierung-l/todo.md`.

## Geprüfte Dateien

- `VideoWebPlayer/Services/MediaSourceScanCoordinator.cs`
- `VideoWebPlayer/Services/MediaSourceScanService.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor`
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor`
- `VideoWebPlayer.Tests/Services/MediaSourceScanCoordinatorScanTests.cs`
- `VideoWebPlayer.Tests/Services/MediaSourceScanCoordinatorScopeTests.cs`
- `VideoWebPlayer.Tests/Helpers/MediaSourceScanCoordinatorTestServices.cs`
- `VideoWebPlayer.Tests/Helpers/GatedMetadataWriteCoordinator.cs`
- `VideoWebPlayer.Tests/MediaSourceAdminFullScanE2ETests.cs`
- `VideoWebPlayer.Tests/MediaSourceExplorerRescanE2ETests.cs`
- `VideoWebPlayer.Tests/AgeRatingE2ETests.cs`
- `VideoWebPlayer.Tests/LocalMediaSourcePipelineE2ETests.cs`
- `VideoWebPlayer.Tests/Helpers/LocalMediaPlaylistE2ETestBase.cs`
- `VideoWebPlayer.Tests/Helpers/TestableMediaSourceScanService.cs`
- `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`
- `docs/RELEASE_NOTES.md` (nur überflogen, keine Code-Kriterien anwendbar)
- `docs/help/medienquellen/fehlerbehebung-anwender.md` (nur überflogen, keine Code-Kriterien anwendbar)
- `docs/features/task/issue-333-86d0f0f6e5984dfdb74ebf2ede9e11ff-scan-pipeline-orchestrierung-l/todo.md` (nur überflogen, keine Code-Kriterien anwendbar)
