# Tasks: Scan-Pipeline: Orchestrierung in einem Einstiegspunkt (Issue #333)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Konfiguration | Lebensdauer und Registrierung von Scanner, Classifier, Notifier, Signalen und Gate in `Program.cs` und Test-Host prüfen, fehlende ergänzen | Offen | — |
| 2 | Logik | Records `MediaSourceScanRunResult` und `CollectionRescanResult` anlegen | Offen | — |
| 3 | Logik | Klasse `MediaSourceScanCoordinator` mit `RunFullScanAsync` anlegen (Worker-Reihenfolge, Scopes, Gate, Cap-Parameter) | Offen | — |
| 4 | Logik | `MediaSourceScanCoordinator.RescanCollectionTreeAsync` anlegen (Teilbaum, Benachrichtigung, Genre-Reload, `ClassificationDeferred`) | Offen | — |
| 5 | Logik | DI-Registrierung von `MediaSourceScanCoordinator` (scoped) | Offen | — |
| 6 | Logik | Existenzprüfung der `collectionId` im Rescan mit Statusmeldung statt Exception | Offen | — |
| 7 | Logik | Worker `MediaSourceScanService.ExecuteAsync` auf `RunFullScanAsync("Automatischer Scanprozess", 64)` umstellen; Logmeldungen erhalten; Windows-1252-Kodierung erhalten | Offen | — |
| 8 | UI | `MediaSourceAdmin.razor::RunFullScan` auf `RunFullScanAsync("Manueller Komplettscan", null)` umstellen; Pausierungsprüfung bleibt | Offen | — |
| 9 | UI | `MediaSourceExplorer.razor::RescanCurrent` auf `RescanCollectionTreeAsync` umstellen; Hinweis bei `ClassificationDeferred` | Offen | — |
| 10 | UI | Ungenutzte Injektionen in Admin- und Explorer-Komponente entfernen | Offen | — |
| 11 | Tests | `MediaSourceScanCoordinatorTests` (Wecken je Commit, Benachrichtigung, Genre-Reload, Cap, Restore-Gate, Teilbaum, Deferred, fehlende Collection) gegen SQLite | Offen | — |
| 12 | Tests | Regressionstest Worker vs. Koordinator in `MediaSourceScanServiceTests` | Offen | — |
| 13 | Tests | Test-Helfer `ScanEverythingAsync` und `RescanCollectionAsync` auf Koordinator umstellen | Offen | — |
| 14 | Tests | `AgeRatingE2ETests` und `LocalMediaSourcePipelineE2ETests`: Eigene Sequenz durch Koordinator ersetzen | Offen | — |
| 15 | E2E-Tests | `MediaSourceAdminFullScanE2ETests`: Komplettscan-Klick mit Ergebnis; Restore-Blockade mit Statusmeldung | Offen | — |
| 16 | E2E-Tests | `MediaSourceExplorerRescanE2ETests`: „Neu erfassen“ findet neue Datei, Geschwister unverändert; Hinweis bei laufender Klassifizierung | Offen | — |
| 17 | Dokumentation | `docs/help/` für Komplettscan und Neu erfassen prüfen und bei Bedarf anpassen | Offen | — |
| 18 | Abnahme | `dotnet build VideoWebPlayer.sln` Debug und Release fehlerfrei | Offen | — |
| 19 | Abnahme | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig grün | Offen | — |
