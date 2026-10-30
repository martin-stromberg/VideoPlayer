# Test-Ergebnisse

**Iteration 3** — Lauf nach den 5 Review-Fixes (Commits `bd76cc2`…`0ebcf27`:
Eskalationsgedächtnis im Coordinator, sftp-Transient-Vorrang im
ffprobe-Laufzeit-Probe, Pump-Drain-Await vor der Exit-Klassifikation,
Standby-Kaskade HTTP→sftp→pipe:0; 8 neue Tests).

Lauf: `dotnet build VideoPlayer.sln` (Debug) + `dotnet test
VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build
--collect:"XPlat Code Coverage"` auf Branch
`task/issue-311-235fce714534478699d87deccccd02bd-tv-sender-live-stream-von-seri`.

- **Debug-Build:** erfolgreich — 0 Warnungen, 0 Fehler (1,49 s);
  Protokoll `test-output/build-debug-iter3.log`.
- **Testlauf (mit Coverage):** 2818 Tests in 5,9 Min — **vollständig grün**,
  kein einziger Ausfall, keine Wiederholung nötig;
  Rohprotokoll `test-output/dotnet-test-iter3-coverage.log`, Coverage:
  `test-output/TestResults/9a5d76dc-fdac-46cd-9a59-084d6b21600a/coverage.cobertura.xml`.

## Ergebnis

**Status:** Keine Fehler

## E2E-Abdeckung

Backend-Feature ohne UI-Fluss; geplante E2E-Szenarien sind die ffmpeg-basierten
Tests in `FfmpegLiveStreamSegmenterE2ETests` (echtes ffmpeg-Binary) — alle
bestanden:

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| moov-am-Ende-MP4 über `pipe:0` → `UndecodableInput` erkannt, keine Restart-Schleife (AK 3+4) | `FfmpegLiveStreamSegmenterE2ETests.RealFfmpeg_MoovAtEndPipeInput_FlagsUndecodableInput` | Bestanden |
| Seekbarer Eingang segmentiert moov-am-Ende-MP4 inkl. `-ss`-Offset (AK 1) | `FfmpegLiveStreamSegmenterE2ETests.RealFfmpeg_MoovAtEndFileInputWithOffset_ProducesSegments` | Bestanden |
| `sftp://`-Fehlschlag fällt gestuft zurück / ffmpeg ohne libssh erkannt (AK 3+5) | `FfmpegLiveStreamSegmenterE2ETests.RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure` | Bestanden |
| Playlist-Fortsetzung mit Discontinuity-Marker (bekanntes Flackern, dokumentiert) | `FfmpegLiveStreamSegmenterE2ETests.SecondJob_PlaylistContinuesAndMarksDiscontinuity` | Bestanden |
| Playlisten-/Segment-Erzeugung Basispfad | `FfmpegLiveStreamSegmenterE2ETests.RealFfmpeg_ProducesPlaylistAndSegments` | Bestanden |
| Pipe-Input-Stop ohne Exception-Stacktrace im Log | `FfmpegLiveStreamSegmenterE2ETests.RealFfmpeg_PipeInputStop_LogsNoExceptionStacktrace` | Bestanden |

Auch die beiden bekannten Playwright-Flakes
(`LiveTv_ViaNavMenu_ShowsPlayerAndProgram`,
`Admin_CreateEditDeactivateDeleteRule`) sowie der Iteration-2-Flake
`Sendeplan_ClipFillBlock_ShowsBlockCardAndDetailOverlay` liefen ohne Ausfall
durch.

## Zusammenfassung

- Gesamt: 2818
- Bestanden: 2816
- Fehlgeschlagen: 0
- Übersprungen: 2
  (`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
  — dokumentierter `Skip`-Befund zu einem bekannten API-500;
  `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` —
  plattformbedingt, Unix-only)

Gegenüber Iteration 2 (2810 Tests) +8 Tests aus den Review-Fixes dieser
Iteration — wie angekündigt. Kein Flake in diesem Lauf; keine
Einzelwiederholungen erforderlich.

## Testabdeckung

**Abdeckung:** 95,8 % Zeilen (69,4 % Verzweigungen), XPlat Code Coverage

Feature-geänderte bzw. feature-relevante Dateien unter 80 % (alle anderen
Feature-Dateien liegen bei 81,4–100 %, darunter `LiveStreamCoordinator` 100 %,
`LiveStreamJob` 100 %, `SftpProbeUriBuilder` 100 %, `LiveStreamExitInfo` 100 %,
`FfmpegErrorMarkers` 98,7 %, `FfmpegLiveStreamSegmenter` 95,9 %,
`SegmenterProcessSupport` 92,4 %, `InternalMediaStreamTokenService` 92,1 %,
`LiveStreamMediaResolver` 92,0 %, `LiveStreamStandbyProvider` 90,5 %,
`LiveStreamStandbySegmenter` 88,5 %, `LiveStreamInput` 84,4 %,
`FfprobeMediaDurationProbe` 81,4 %):

| Datei | Abdeckung |
|-------|-----------|
| VideoWebPlayer/Services/LiveStreamWorker.cs | 71,8 % |
| VideoWebPlayer/Services/SftpMediaSourceReader.cs | 11,4 % |
| VideoWebPlayer/Services/SftpStreamWrapper.cs | 0 % |

Projektweit liegen 191 von 616 Dateien unter 80 % (u. a. `.razor`-Seiten/
Komponenten, EF-Migrationen, Client-DTOs); vollständige Liste in
`coverage.cobertura.xml`.

## Fehlende Tests

Quelle: `Coverage-Daten` — Dateien mit 0 % Zeilenabdeckung (nur
Nicht-Razor-Quelldateien; zusätzlich 38 `.razor`-Dateien ohne Zeilenabdeckung,
u. a. Account-/Admin-Seiten):

- `VideoWebPlayer/Services/SftpStreamWrapper.cs` — 0 % Abdeckung (kein echter
  SFTP-Server in der Suite; feature-referenziert, in diesem Branch nicht
  geändert)
- `VideoWebPlayer/Hubs/MediaUpdateHub.cs` — 0 % Abdeckung
- `VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs` — 0 % Abdeckung
- `VideoWebPlayer/Utils/LocalNetworkHelper.cs` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/IdentityNoOpEmailSender.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` — 0 % Abdeckung
  (Interface-Marker)
- `VideoWebPlayer.Client/Models/DtoRecentEntry.cs` — 0 % Abdeckung (Client-DTO)
- `VideoWebPlayer.Client/Models/ImpersonateRequest.cs` — 0 % Abdeckung (Client-DTO)
