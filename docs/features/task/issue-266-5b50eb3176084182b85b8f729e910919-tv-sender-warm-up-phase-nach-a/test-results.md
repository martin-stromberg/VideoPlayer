# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Lauf (vollständig, mit Coverage, `dotnet test … --collect:"XPlat Code Coverage"`):
2493 Tests, 2491 bestanden, 0 fehlgeschlagen, 2 übersprungen, ~5,4 min — ohne
einmalige Wiederholung; kein Test wurde deaktiviert oder abgeschwächt.

Neu gegenüber der Vor-Runde: 5 Tests (`+5`) — die Absicherungen der
Review-Nachbesserungen:

- `LiveStreamStandbySegmenterTests.TranscodeAsync_KillFailsAndStderrNeverEnds_ReturnsWithinBound`
  (Timeout-Kill schlägt fehl + stderr endet nie → begrenzte Rückkehr statt Endlos-Hängen;
  ohne den Fix schlägt der Test fehl, weil `TranscodeAsync` nie zurückkehrt)
- `LiveStreamStandbySegmenterTests.TranscodeAsync_LockedStaleSegment_StaysOutOfResult`
  (nicht löschbares Alt-Segment bleibt aus dem neuen Satz; Löschfehler wird geloggt)
- `SegmenterProcessSupportTests.StartRealProcess_UnstartableBinary_LogsCauseAndReturnsNull`
  (Startursache landet im Log statt verschluckt zu werden)
- `LiveStreamControllerTests.GetFile_AccessDeniedAfterLogin_Returns500Not401`
  (Zugriffsverweigerung hinter der Anmeldeprüfung → 500, nicht 401; ohne den Fix
  `UnauthorizedObjectResult` — verifiziert)
- `TVSenderAdminTests_Standby.StandbyCard_OrphanedSelection_SaveStillWorks`
  (verwaiste Auswahl: „Speichern" funktioniert im angezeigten Standard-Zustand;
  ohne den Fix „Das gewählte Standby-Video wurde nicht gefunden." — verifiziert)

## Fehlgeschlagene Tests

Keine — der Gesamtlauf lief ohne Ausfälle durch.

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Zuschauer öffnet `/live-tv` in der Warm-up-Phase → Standby läuft, keine Fehleranzeige | `LiveTvE2ETests.LiveTv_Warmup_PlaysStandbyWithoutErrorHint` | Bestanden |
| Externer Player / gekoppeltes Gerät: Playlist ohne produzierten Stream | `LiveStreamE2ETests.PairedDevice_RequestPlaylistWithoutProducedStream_ReturnsStandbyPlaylist` | Bestanden |
| Externer Player: Standby-Segment ausliefern | `LiveStreamE2ETests.PairedDevice_RequestStandbySegment_ReturnsSegment` | Bestanden |
| Übergang Standby → echter Stream (`DISCONTINUITY`, `access_token`-Rewrite, `no-store`) | `LiveStreamE2ETests.PairedDevice_RequestPlaylist_TransitionsStandbyToReal_Discontinuity` | Bestanden |
| Admin wählt Standby-Clip (Medienquelle → MediaItem → Speichern, persistiert) | `TVSenderAdminE2ETests.Admin_StandbyClip_PickAndPersist` | Bestanden |
| Konfigurierter Clip wird vorbereitet (Auflösung → Transcode → Cache → Auslieferung) | `LiveStreamE2ETests.PairedDevice_ConfiguredStandbyClip_PreparesAndServes` | Bestanden |
| `no-store` auf echter `.ts`-Antwort über die HTTP-Pipeline | `LiveStreamE2ETests.PairedDevice_RequestSegment_ReturnsNoStore` | Bestanden |
| Auth-/Freigabefälle (401/403/404 auf fremde Dateien) greifen vor dem Fallback | Bestehende E2E-/Controller-Tests | Bestanden |

## Zusammenfassung

- Gesamt: 2493
- Bestanden: 2491
- Fehlgeschlagen: 0
- Übersprungen: 2
  - `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` (nur Unix, unter
    Windows erwartungsgemäß übersprungen)
  - `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
    (im Test bedingt übersprungen, feature-fremd)

## Testabdeckung

**Abdeckung:** 95,5 % Zeilenabdeckung gesamt (Cobertura: 161 246 / 168 827 Zeilen;
Anlage `VideoWebPlayer.Tests/TestResults/91f5ae6f-240d-4d80-90a6-9949b55c01a1/coverage.cobertura.xml`)

Tabelle nur für in diesem Branch neue/geänderte Quelldateien < 80 %:

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer/Services/LiveStreamWorker.cs` (geändert) | 60,5 % |
| `VideoWebPlayer/Services/SegmenterProcessSupport.cs` (neu) | 59,1 % |
| `VideoWebPlayer/Components/Pages/LiveTv.razor` (geändert) | 69,2 % |
| `VideoWebPlayer/Services/ProgramClipGroupService.cs` (geändert) | 72,7 % |
| `VideoWebPlayer/Migrations/20261026154346_AddSetupLiveStreamStandbyMediaItem.cs` (neu) | 58,3 % (nur `Down()` ungetestet, üblich) |

Zum Vergleich, übrige geänderte/neue Dateien ≥ 80 %: `TVSenderAdmin.razor` 100 %,
`LiveStreamController.cs` 100 %, `ProgramSettingsService.cs` 100 %,
`VideoWebPlayerBackupData.cs` 100 %, `MediaItemClipEligibility.cs` 100 %,
`Setup.cs` 100 %, `ApplicationDbContextModelSnapshot.cs` 100 %,
`ServiceCollectionExtensions.cs` 93,7 %, `LiveStreamStandbySegmenter.cs` 92,5 %,
`LiveStreamStandbyProvider.cs` 85,7 %, `FfmpegLiveStreamSegmenter.cs` 81,8 %.

Erläuterungen zu den < 80 %-Dateien:

- `LiveStreamWorker.cs`: Restabdeckung ist überwiegend historischer, in diesem Branch
  unveränderter Code; die branch-neue Start-Sequenz ist über die Worker-Tests abgedeckt.
- `SegmenterProcessSupport.cs` (neu): ungedeckt sind v. a. Fehlerpfade in
  `PumpInputAsync` und `DrainStandardErrorAsync` — Stderr-Drain-/Pipe-Fehlerfälle, die
  nur indirekt über die Segmenter-Tests laufen; `StartRealProcess` ist direkt getestet.
- `LiveTv.razor`: Restabdeckung überwiegend historischer Player-Code; die branch-neue
  Änderung (`data-error-kind`-Attribut) ist reines Diagnosemerkmal ohne Logik.
- `ProgramClipGroupService.cs`: Restabdeckung überwiegend historische Pfade; der
  branch-neue `GetClipCandidatesAsync`-Teil ist über Kandidaten-Tests abgedeckt.

## Fehlende Tests

Keine branch-neue/geänderte Quelldatei hat 0 % Zeilenabdeckung — es fehlt keine
Testdatei vollständig. Alle Review-Nachbesserungen (Alt-Segment-Reste,
stderr-Drain-Begrenzung, Prozess-Start-Logging, 401/500-Zuordnung, verwaiste
Standby-Auswahl) sind mit Tests abgesichert, die ohne die Fixes fehlschlagen.
