# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### VideoWebPlayer/Services/MediaSourceScanCoordinator.cs (MediaSourceScanCoordinator)

- **Doppelter Code** — `RunFullScanAsync` (Zeilen 116–146): Der `if (scannedCollections > 0)`/`else`-Block enthält zwei nahezu identische Abläufe (Zählen von `unclassifiedBefore`, Logging, Aufruf von `_classifier.ClassifyMediaCollectionsAsync`, erneutes Logging, `_notificationService.NotifyNewVideosScannedAsync`). Der einzige fachliche Unterschied ist, dass der `if`-Zweig die Klassifizierung zusätzlich an `unclassifiedBefore > 0` bindet, während der `else`-Zweig sie unbedingt ausführt – diese Asymmetrie ist aus dem Code allein nicht erkennbar begründet und geht beim Zusammenführen leicht verloren oder wird unbeabsichtigt vereinheitlicht.

  Empfehlung: Beide Zweige in eine private Methode extrahieren (z. B. `ClassifyPendingCollectionsAsync(bool requireUnclassified, CancellationToken cancellationToken)`), die Zählung, Logging, Klassifizierung und Benachrichtigung einmal kapselt. Die beiden Aufrufstellen unterscheiden sich dann nur noch im `requireUnclassified`-Flag, und die beabsichtigte Asymmetrie wird an einer Stelle sichtbar statt doppelt gepflegt.

- **Doppelter Code** — Das Muster „Items vor dem Scan zählen → Scan-Aktion ausführen → Items nach dem Scan zählen → Differenz bilden" ist in `RunFullScanAsync` (Zeilen 89–95, mit `_scanner.ScanAllSourcesAsync`) und in `RescanCollectionTreeAsync` (Zeilen 168–173, mit `_scanner.ScanCollectionTreeAsync`) wortgleich implementiert.

  Empfehlung: In eine private Hilfsmethode extrahieren, z. B. `async Task<int> CountNewItemsAsync(Func<CancellationToken, Task> scanAction, CancellationToken cancellationToken)`, die vor und nach `scanAction` zählt und die Differenz zurückgibt. Beide öffentlichen Methoden rufen dann nur noch `await CountNewItemsAsync(ct => _scanner.ScanAllSourcesAsync(ct), cancellationToken)` bzw. die Collection-Variante auf.

## Hinweise

- Der eigentliche Zweck dieser Änderung – die Scan-Pipeline-Schritte in einem einzigen Einstiegspunkt (`MediaSourceScanCoordinator`) zu bündeln, statt sie in `MediaSourceScanService` (Worker), `MediaSourceAdmin.razor` und `MediaSourceExplorer.razor` jeweils eigenständig nachzubilden – ist konsequent umgesetzt: Alle drei früheren Ausführungspfade rufen jetzt ausschließlich `RunFullScanAsync` bzw. `RescanCollectionTreeAsync` auf, keiner bildet die Schrittfolge mehr selbst nach. Das deckt sich mit der Review-Kategorie „Ablaufverantwortung bei mehrstufigen Vorgängen" – hier liegt kein Befund vor, weil genau das Empfohlene bereits gemacht wurde.
- Die neuen bzw. angepassten Tests (`MediaSourceScanCoordinatorScanTests`, `MediaSourceScanCoordinatorScopeTests`, `MediaSourceAdminFullScanE2ETests`, `MediaSourceExplorerRescanE2ETests`, `LocalMediaPlaylistE2ETestBase`, `AgeRatingE2ETests`, `LocalMediaSourcePipelineE2ETests`) rufen durchgängig den produktiven Einstiegspunkt des Koordinators auf statt Einzelschritte künstlich zusammenzusetzen. Der neue Regressionstest `MediaSourceScanServiceTests.Worker_AndManualFullScan_ProduceSameResult` vergleicht Worker-Pfad und Koordinator-Pfad explizit und ist dafür sauber mit Arrange/Act/Assert strukturiert.
- `TestableMediaSourceScanService.CreateNullNotificationService()` wurde von `private` auf `public` geändert, um sie aus `MediaSourceScanServiceTests` wiederzuverwenden – das ist eine sinnvolle Vermeidung von Codeverdoppelung und keine unnötige Erweiterung der öffentlichen Oberfläche, da die Methode weiterhin nur innerhalb der Testbibliothek gebraucht wird.
- Keine Befunde zu Fehlerbehandlung: `RunFullScanAsync` validiert `maxCollectionsPerRun` vorab mit einer aussagekräftigen `ArgumentOutOfRangeException`; die Razor-Seiten fangen `Exception` nur an der UI-Grenze ab, loggen sie und zeigen eine Statusmeldung – das ist für einen Admin-Button angemessen, kein zu breiter oder stiller Handler.
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
