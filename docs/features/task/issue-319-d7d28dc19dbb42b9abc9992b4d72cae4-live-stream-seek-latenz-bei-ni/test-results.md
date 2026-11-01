# Testergebnisse — Issue #319

## Neue Tests (`FfmpegLiveStreamSegmenterTests_FirstOutput`, 7 Tests — alle grün)

- `StartAsync_NewSegmentNameAfterStart_LogsFirstOutputLatency`: Rest-Segment einer
  früheren Generation zählt nicht; erst der neue `seg_*_42_*.ts`-Name löst das
  „Erstes Segment"-Log (mit Sendeplatz, Eingangsart, Position) aus.
- `StartAsync_PlaylistRewrite_LogsFirstOutput`: eine `live.m3u8`-Rewrite nach dem
  Start-Snapshot zählt ebenfalls als erste Ausgabe.
- `StartAsync_SegmentOfOtherSlot_NotCountedAsFirstOutput`: `seg_*_99_*.ts` eines
  fremden Sendeplatzes wird ignoriert.
- `StartAsync_NoOutputWithinThreshold_LogsLatencyWarning`: ohne Ausgabe erscheint
  oberhalb der Schwelle die Warnung „Kein erstes Segment … — Seek-/Scan-Latenz
  vermutet".
- `StopAsync_BeforeThreshold_NoLatencyWarning`: der Wächter endet still beim
  geplanten Stop — keine nachträgliche Warnung.
- `UnexpectedExit_BeforeFirstOutput_ReportsSegmentAusgeblieben` /
  `UnexpectedExit_AfterFirstOutput_ReportsSegmentErzeugt`: der Exit-Report führt
  „erstes Segment: ausgeblieben|erzeugt".

## Bestandstests

- `FfmpegLiveStreamSegmenterTests` + `FfmpegLiveStreamSegmenterE2ETests` +
  `LiveStreamCoordinatorTests` + `LiveStreamWorkerTests` (Finaler Stand): 72/72 grün.

## Volle Suite

- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Debug):
  **2838 von 2840 grün, 2 übersprungen, 0 Fehler** (Dauer 5:35). Hinweis: Die volle
  Suite lief auf dem Stand vor einer letzten Kleinigkeit (Auslesen des
  `_firstOutputObserved`-Flags aus dem Gate-Block heraus verlagert — gleiche Logik,
  nur Zugriffszeitpunkt); alle 72 segmenter-/koordinator-nahen Tests wurden danach
  auf dem finalen Stand erneut ausgeführt: grün.
- `dotnet build VideoPlayer.sln` Debug und Release: fehlerfrei (0 Fehler,
  140 pre-existing Warnungen).
