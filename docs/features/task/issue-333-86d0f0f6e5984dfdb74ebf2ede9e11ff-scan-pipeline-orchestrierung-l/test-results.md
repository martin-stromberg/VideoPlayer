# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Hinweis: Die automatisierten Tests sind grün. Die reale End-to-End-Abnahme ist **nicht** durchgeführt (siehe Abschnitt „Reale End-to-End-Abnahme“). Der Schritt gilt damit noch nicht als vollständig abgenommen.

## Fehlgeschlagene Tests

Keine.

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| 1. Admin-Komplettscan (Happy Path) | `MediaSourceAdminFullScanE2ETests.Admin_RunsFullScan_AndNewEpisodeAppearsInExplorer` | Bestanden |
| 1. Komplettscan, Scope-Freigabe | `MediaSourceScanCoordinatorScopeTests.RunFullScanAsync_DefersScheduleWake_UntilScanEnds` | Bestanden |
| 2. Explorer „Neu erfassen“ (Happy Path) | `MediaSourceExplorerRescanE2ETests.RescanCurrent_FindsNewFile_InSubtreeOnly_SiblingUnchanged` | Bestanden |
| 2. Teilbaum-Scan | `MediaSourceScanCoordinatorScanTests.RescanCollectionTreeAsync_ScansOnlySubtree` | Bestanden |
| 3. Restore aktiv, Komplettscan blockiert | `MediaSourceAdminFullScanE2ETests.Admin_FullScanBlocked_WhileRestoreIsActive` | Bestanden |
| 3. Restore-Lease, danach Lauf | `MediaSourceScanCoordinatorScopeTests.RunFullScanAsync_WaitsForRestoreLease_ThenRuns` | Bestanden |
| 4. Klassifizierung aktiv, Rescan verschoben | `MediaSourceExplorerRescanE2ETests.RescanCurrent_WhileClassificationRunning_ShowsDeferredHint` | Bestanden |
| 4. Verschobene Klassifizierung | `MediaSourceScanCoordinatorScanTests.RescanCollectionTreeAsync_ReportsDeferredClassification` | Bestanden |
| 7. Nicht vorhandene Collection | `MediaSourceScanCoordinatorScanTests.RescanCollectionTreeAsync_MissingCollection_ReturnsStatusInsteadOfThrowing` | Bestanden |
| Umgestellt: AgeRating (Koordinator) | `AgeRatingE2ETests` (3 Tests) | Bestanden |
| Umgestellt: Pipeline (Koordinator) | `LocalMediaSourcePipelineE2ETests.LocalSource_ScanAndClassify_ExposesTvShowViaApi` | Bestanden |
| Umgestellt: Backfill (Helfer) | `LocalMediaPlaylistE2ETests_Backfill` (2 Tests) | Bestanden |
| Umgestellt: SourceDeletion (Helfer) | `LocalMediaPlaylistE2ETests_SourceDeletion` (2 Tests) | Bestanden |

## Reale End-to-End-Abnahme

Die Anforderung berührt die Benutzeroberfläche (Admin- und Explorer-Seite) und die Persistenz. Eine reale Abnahme ist daher erforderlich.

**Reale Abnahme: Nicht durchgeführt.** Dieser Lauf hat die Anwendung nicht real gestartet und die Szenarien nicht manuell durchgespielt. Die E2E-Tests laufen mit dem echten Host und Playwright, ersetzen aber nicht den realen Ablauf (Start der Anwendung, Klick, sichtbare Wirkung). Die Szenarien 1 bis 4 und 7 sind daher offen. Die Entscheidung über manuelle Abnahme oder dokumentierten Verzicht liegt beim Anwender.

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| 1. Admin-Komplettscan | – | Offen (nicht durchgeführt) |
| 2. Explorer „Neu erfassen“ | – | Offen (nicht durchgeführt) |
| 3. Restore aktiv | – | Offen (nicht durchgeführt) |
| 4. Klassifizierung aktiv | – | Offen (nicht durchgeführt) |
| 7. Nicht vorhandene Collection | – | Offen (nicht durchgeführt) |

## Zusammenfassung

- Gesamt: 2901
- Bestanden: 2899
- Fehlgeschlagen: 0
- Übersprungen: 2 (`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`, `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit`)

Build: `dotnet build VideoPlayer.sln` (Debug) mit 0 Warnungen und 0 Fehlern. Release-Build wurde in diesem Lauf nicht ausgeführt.

## Testabdeckung

**Abdeckung:** 95,9 % Zeilen (173455 von 180869), 70,3 % Zweige (Quelle: Cobertura-Bericht `VideoWebPlayer.Tests/TestResults/24145496-cf4d-47c9-a15d-788acdb93973/coverage.cobertura.xml`)

Dateien des Scan-Pfads (Auswahl):

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer/Services/MediaSourceScanCoordinator.cs` | 98,8 % |
| `VideoWebPlayer/Services/MediaSourceScanService.cs` | 92,1 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor` | 74,8 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor` | 77,1 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor` | 60,4 % |
| `VideoWebPlayer/Services/SftpMediaSourceReader.cs` | 11,4 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

Dateien mit 0 % Zeilenabdeckung (Auswahl, ohne Identity-Seiten und Samples):

- `VideoWebPlayer/Hubs/MediaUpdateHub.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/SftpStreamWrapper.cs` — 0 % Abdeckung
- `VideoWebPlayer/Utils/LocalNetworkHelper.cs` — 0 % Abdeckung
- `VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/UserManagement.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Actors/Actors.razor` — 0 % Abdeckung
- `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` — 0 % Abdeckung
- `VideoWebPlayer.Client/Models/DtoRecentEntry.cs` — 0 % Abdeckung

Außerdem 0 %: zahlreiche Razor-Seiten unter `VideoWebPlayer/Components/Account/` (Identity-Boilerplate) und `Components/Pages/Samples/`.

Hinweis zum Scan-Pfad: `SftpMediaSourceReader.cs` liegt bei 11,4 % und ist nicht Teil dieses Schritts im Diff. Die Coverage-Lücke besteht unabhängig davon.
