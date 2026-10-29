# Tasks: Ausnahmefehler im LiveStreamSegmenter

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `SegmenterProcessSupport.PumpInputAsync`: erwarteter-Stop-Catch loggt ohne Ausnahmeobjekt (kompakter `Debug`-Eintrag mit `ex.GetType().Name`/`ex.Message`, kein Stacktrace) | Offen | — |
| 2 | Logik | `FfmpegLiveStreamSegmenter`: `_pumpTask`-Feld, `PumpDrainTimeout`-Konstante + injizierbarer `_pumpDrainTimeout` (interner Ctor); Pump-Task in `StartAsync` festhalten | Offen | — |
| 3 | Logik | `FfmpegLiveStreamSegmenter.StopCoreAsync`: `_pumpTask` nach Prozessende zeitgebunden einholen (`WaitAsync`, `TimeoutException` schlucken, `OnlyOnFaulted`-Beobachtung), vor der „beendet"-Meldung | Offen | — |
| 4 | Tests | `SegmenterProcessSupportTests`: Attrappen `ThrowingWriteStream`/`FakePumpProcess`; Tests Pipe-Bruch → `Completed` + `Debug` ohne Stacktrace; weitere erwartete Ausnahmen (Theory) | Offen | — |
| 5 | Tests | `SegmenterProcessSupportTests`: unerwarteter Fehler → `Failed` + `Warning` mit Ausnahme; `SourceUnavailable`; erfolgreiches Pumpen inkl. stdin-`Close()` | Offen | — |
| 6 | Tests | `FfmpegLiveStreamSegmenterTests`: Fakes erweitern (`StandardInput` wirft `IOException` nach Kill; nie endende Quelle) | Offen | — |
| 7 | Tests | `FfmpegLiveStreamSegmenterTests`: Stop mit Pipe-Eingabe → Pump-Debug-Eintrag ohne Stacktrace vor der „beendet"-Meldung; stallende Pumpe → `StopAsync` kehrt innerhalb Schranke zurück; Slot-Wechsel sammelt alte Pumpe ein | Offen | — |
| 8 | Tests (E2E) | `FfmpegLiveStreamSegmenterE2ETests`: `RealFfmpeg_PipeInputStop_LogsNoExceptionStacktrace` — echter ffmpeg, Pipe-Eingabe, Stop → kein Eintrag mit Ausnahme-Stacktrace, keine Pump-Warnung | Offen | — |
| 9 | Abschluss | `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei; volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün | Offen | — |
