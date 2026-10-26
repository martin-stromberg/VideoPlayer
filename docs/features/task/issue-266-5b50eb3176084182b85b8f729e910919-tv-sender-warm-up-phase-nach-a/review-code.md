# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine. Die Befunde der Vor-Runde (`review-code.2.md`) sind im geprüften Stand
(Branch-Diff gegen `origin/staging` inklusive uncommitteter Änderungen) behoben:

- `LiveStreamStandbySegmenter.cs` — ungenutztes `using System.Globalization;` entfernt.
- `LiveStreamStandbySegmenter.cs` — Alt-Segment-Löschung loggt nun `LogWarning` mit
  Exception; nicht löschbare Reste werden mit Änderungsstempel in `staleLeftOver`
  gemerkt und aus dem aufgenommenen Satz herausgehalten (`standby_000.ts`-Neuprodukt
  mit gleichem Namen, aber neuem Stempel zählt weiterhin als Produkt des Laufs).
  Abgesichert durch `TranscodeAsync_LockedStaleSegment_StaysOutOfResult`.
- `LiveStreamStandbySegmenter.cs` — `stderrTask` läuft jetzt begrenzt per
  `WaitAsync(_stderrDrainTimeout)` (Default 10 s, im internen Ctor injizierbar);
  ein Kill-Fehler im Timeout-Pfad wird geloggt; eine spätere Faultierung des
  verwaisten Drains wird per `OnlyOnFaulted`-Continuation beobachtet. Abgesichert
  durch `TranscodeAsync_KillFailsAndStderrNeverEnds_ReturnsWithinBound` (hängt ohne
  den Fix endlos — der Test begrenzt sich selbst über `WaitAsync`).
- `SegmenterProcessSupport.cs` — `StartRealProcess` erhielt `ILogger`- und
  `caller`-Parameter und loggt die Startursache (Exception samt `FileName`);
  beide Segmenter adaptieren über Lambda. Abgesichert durch
  `SegmenterProcessSupportTests.StartRealProcess_UnstartableBinary_LogsCauseAndReturnsNull`.
- `LiveStreamStandbyProvider.cs` — `StandbyCacheMarker.PreparedAtUtc` entfernt
  (Feld und Schreibstelle); Alt-Marker mit dem JSON-Feld deserialisieren weiter
  (unbekannte Eigenschaft wird ignoriert).
- `LiveStreamController.cs` — `CheckLoggedIn()` steht in einem eigenen try/catch;
  der `catch (Exception) when (… or UnauthorizedAccessException)`-Filter ist
  entfernt, jede Ausnahme hinter der Anmeldeprüfung führt zu geloggtem 500.
  Abgesichert durch `GetFile_AccessDeniedAfterLogin_Returns500Not401` (schlägt
  ohne den Fix mit `UnauthorizedObjectResult` fehl — verifiziert).
- `FfmpegLiveStreamSegmenterE2ETests.cs` — private `FindBinary`-Kopie gelöscht,
  beide Aufrufe nutzen `E2EBinaryLocator.Find("ffmpeg")`.
- `LiveTvE2ETests.cs` — `<summary>`-Block liegt wieder über
  `SeedChannelWithRunningProgramAsync`.
- `TVSenderAdmin.razor` — verwaiste Standby-Auswahl wird beim Anzeigen des
  Fallback-Textes still zurückgesetzt (`LoadStandbyLabelAsync`), sodass der
  sichtbare Zustand dem wirksamen folgt und „Speichern" funktioniert; Überschrift
  „Standby-Clip (Überbrückung)" und anwenderorientierter Hilfetext; Hinweis
  `#tvsender-standby-pending` bei ungespeicherter Abweichung vom persistierten
  Stand (`standbySelectionPending` gegen `standbySavedMediaItemId`); Picker-Knopf
  wechselt „Ändern"/„Auswahl schließen" mit `aria-expanded`/`aria-controls`;
  `standbyCandidatesLoading` liefert „Clips werden geladen…" und sperrt
  „Weitere laden". Abgesichert durch `StandbyCard_OrphanedSelection_SaveStillWorks`
  (schlägt ohne den Fix mit „Das gewählte Standby-Video wurde nicht gefunden."
  fehl — verifiziert).

## Geprüfte Dateien

- `VideoWebPlayer/Services/LiveStreamStandbyProvider.cs` (neu)
- `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs` (neu)
- `VideoWebPlayer/Services/SegmenterProcessSupport.cs` (neu)
- `VideoWebPlayer/Services/MediaItemClipEligibility.cs` (neu)
- `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`
- `VideoWebPlayer/Controllers/LiveStreamController.cs`
- `VideoWebPlayer/Services/LiveStreamWorker.cs`
- `VideoWebPlayer/Services/ProgramSettingsService.cs`
- `VideoWebPlayer/Services/ProgramClipGroupService.cs`
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/Components/Pages/LiveTv.razor`
- `VideoWebPlayer/Data/Setup.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Migrations/20261026154346_AddSetupLiveStreamStandbyMediaItem.cs` + `…Designer.cs` (neu)
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/VideoWebPlayer.csproj`, `.gitignore`, `VideoWebPlayer/Assets/LiveStreamStandby/*.ts` (neu)
- `VideoWebPlayer.Tests/Helpers/E2EBinaryLocator.cs` (neu)
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs`
- `VideoWebPlayer.Tests/Services/SegmenterProcessSupportTests.cs` (neu)
- `VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs`
- `VideoWebPlayer.Tests/LiveTvE2ETests.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_Standby.cs` (neu)
- `VideoWebPlayer.Tests/Services/LiveStreamStandbySegmenterTests.cs` (neu)
- `VideoWebPlayer.Tests/Services/LiveStreamStandbyProviderTests.cs` (neu)
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_LiveStreamStandbyMediaItem.cs` (neu)
- `VideoWebPlayer.Tests/Helpers/TVSenderAdminE2ETestBase.cs`, `VideoWebPlayer.Tests/LiveStreamE2ETests.cs`, `VideoWebPlayer.Tests/LiveStreamWorkerTests.cs`, `VideoWebPlayer.Tests/MediaProbeHttpIntegrationTests.cs`, `VideoWebPlayer.Tests/ProgramSettingsServiceLiveStreamTests.cs`, `VideoWebPlayer.Tests/SetupsSingleRowMigrationTests.cs`, `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs`, `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
