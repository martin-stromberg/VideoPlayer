# Tests — Ausgangszustand und Bestand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-29, ca. 07:57–08:15 Uhr +0100 (MEZ), Windows, Git Bash
- Branch und Commit-ID: `task/0cd6706b52b14c7687d045a98b4c18d0-ausnahmefehler-im-livestreamse`,
  Commit `408723c9ed393db1bc2a529b104dd5acc7f7e288`
- Uncommittete Änderungen: keine an verfolgten Dateien; nur das untracked Feature-Verzeichnis
  `docs/features/task/0cd6706b52b14c7687d045a98b4c18d0-ausnahmefehler-im-livestreamse/`
  (Anforderung und diese Bestandsaufnahme)
- Testumgebung und Runtime-/SDK-Versionen: `dotnet` SDK 10.0.403, Test-Laufzeit .NET 10.0.14,
  `net10.0`, Windows; ffmpeg 9.0.2 (`ffmpeg-9.0.2-full_build`, winget Gyan-Paket) im `PATH`;
  Playwright-Chromium-Builds unter `%USERPROFILE%\AppData\Local\ms-playwright` vorhanden
  (chromium-1169/1200/1228/1234/1248)
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - AGENTS.md nennt `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` als volle Suite.
  - CI (`.github/workflows/pr-staging-ci.yml`, `staging-ci.yml`) baut Release und führt drei
    gefilterte Läufe aus: `--filter "Category!=E2E"`, `--filter "Category=Integration"`,
    `--filter "Category=E2E"` (mit vorherigem `playwright.ps1 install chromium`).
  - Kategorisierung per `[Trait("Category", "E2E")]` in 50 Testdateien; **kein** Test trägt
    `Category=Integration`. Einige Playwright-E2E-Klassen (z. B. `TVSenderAdminE2ETests`)
    tragen den Trait **nicht** und laufen daher in der Nicht-E2E-Suite mit.
  - Build vor den Läufen: `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj
    -c Debug -p:NoWarn=NU1903` — 0 Fehler, 140 Warnungen (bestehende Bestandswarnungen).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Debug --filter "Category!=E2E" --logger "console;verbosity=normal"` | Repository-Wurzel | 0 | 2444 | 0 | 1 | `test-results/unit-tests-category-not-e2e.log` |
| 2 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Debug --filter "FullyQualifiedName~FfmpegLiveStreamSegmenterE2ETests" --logger "console;verbosity=normal"` | Repository-Wurzel | 0 | 2 | 0 | 0 | `test-results/e2e-segmenter-tests.log` |
| 3 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Debug --filter "Category=Integration" --logger "console;verbosity=normal"` | Repository-Wurzel | 0 | 0 | 0 | 0 | `test-results/integration-tests.log` (kein Test trägt den Trait `Integration`) |

### Nachgewiesene bestehende Testfehler

Keine. Beide ausgeführten Läufe ohne Fehlschlag.

Der einzige übersprungene Test von Lauf 1:
`VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit`
— Unix-spezifisch, unter Windows erwartungsgemäß übersprungen (kein Fehler).

### Testlücken und Ausführungsprobleme

- **Keine Abdeckung für `SegmenterProcessSupport.PumpInputAsync`**: `SegmenterProcessSupportTests`
  enthält nur einen `StartRealProcess`-Test. Pipe-Bruch beim geplanten Stop (der beanstandete
  `LogDebug(ex, …)`-Pfad), `SourceUnavailable`, `Failed`-Mapping und das garantierte
  stdin-`Close()` im `finally` sind ungetestet.
- **Kein Test zum Pump-/Log-Verhalten beim Stop in `FfmpegLiveStreamSegmenter`**: Die
  vorhandenen Tests prüfen Argumente, Lebenszyklus und unerwartete Prozessenden — nicht die
  Eingabe-Pumpe beim Slot-Wechsel/Stop. `FakeSegmenterProcess.StandardInput` ist ein
  `MemoryStream`; ein `IOException`-Pipe-Bruch beim Schreiben wird aktuell von keinem
  Fake erzeugt — für den Nachweis braucht es eine Prozess-/Stream-Attrappe, deren
  `StandardInput` beim Schreiben wirft (Muster: `ThrowingSftpMediaSourceReader`/
  `UnreadableFileReader` unter `VideoWebPlayer.Tests/Helpers/`).
- **`Category=E2E`-Suite nicht vollständig ausgeführt** (50 Dateien, überwiegend Playwright
  gegen gehostete App): auf diesem Lauf aus Zeitgründen beschränkt auf die direkt
  feature-bezogenen `FfmpegLiveStreamSegmenterE2ETests` (echtes ffmpeg, beide grün).
  Umgebung ist grundsätzlich lauffähig: Chromium-Builds installiert, ffmpeg im `PATH`, und
  die Playwright-Tests ohne E2E-Trait liefen in Lauf 1 erfolgreich mit.
- **`Category=Integration`-Filter der CI findet keinen Test** (Trait wird nirgends gesetzt)
  — Lauf 3 dokumentiert das; kein Ausführungsfehler.
- AGENTS.md vermerkt: einzelne Playwright-Tests flackern gelegentlich unter Last; in Lauf 1
  trat kein Flackern auf.

## Testklassen

### `SegmenterProcessSupportTests`

Datei: `VideoWebPlayer.Tests/Services/SegmenterProcessSupportTests.cs`

- `StartRealProcess_UnstartableBinary_LogsCauseAndReturnsNull` — nicht startbares Binary
  liefert `null` und eine `Warning` mit Caller-Tag, Dateiname und Ausnahme-Text.
- (Keine Tests für `PumpInputAsync`/`DrainStandardErrorAsync`/`InputSpec`/`TranscodeArgs`.)

### `FfmpegLiveStreamSegmenterTests`

Datei: `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs`

- `StartAsync_FileInput_BuildsExpectedArguments` — Argumentstring für Datei-Eingabe inkl. `-nostdin`, `-ss`, HLS-Flags, Segmentmuster.
- `StartAsync_HttpInput_UsesUrlArgumentAndNoStdin` — HTTP-Eingabe mit quotierter URL, kein stdin-Redirect.
- `StartAsync_PipeInput_UsesPipeArgumentAndRedirectsStdin` — `-i pipe:0`, kein `-nostdin`, `RedirectStandardInput == true` (Pump-Task selbst nicht geprüft).
- `StartAsync_SetsRunningStateAndSlot` — `IsRunning`/`CurrentSlotId`/`ExpectedPositionSeconds` nach Start.
- `StopAsync_KillsProcessAndClearsState` — `Kill` am Fake, Zustand zurückgesetzt.
- `StopAsync_Kill_IsNotReportedAsUnexpectedExit` — geplanter Kill landet als Debug „wurde angehalten", keine Warnung, `LastUnexpectedExit == null`.
- `UnexpectedExit_LogsWarningWithExitCodeAndStderrTail` — Warnung mit Exit-Code und stderr-Tail, `LastUnexpectedExit` gesetzt.
- `UnexpectedExit_MasksStreamTokenInStderr` — Stream-Token-Maskierung in Warnung und Debug-Argumenten.
- `UnexpectedExit_HttpInputTransportMarker_SetsHttpTransportFailure` — Marker → `HttpTransportFailure == true` bei HTTP-Eingabe.
- `UnexpectedExit_PipeInputTransportMarker_NoHttpTransportFlag` — gleiche Marker bei Pipe-Eingabe setzen das Flag nicht.
- `ExpectedEndOfRun_NoWarningAndNoUnexpectedExit` — Exit nahe Laufende gilt als planmäßig.
- `StartAsync_ClearsLastUnexpectedExit` — Neustart räumt die Diagnose.
- `StartAsync_SecondJob_StopsPreviousProcess` — Slot-Wechsel killt den alten Prozess.
- `StartAsync_FailedStart_ThrowsAndStaysStopped` — `InvalidOperationException` bei `null`-Rückgabe des Factories.
- `ExpectedPositionSeconds_NoJob_IsNull` — Zustandsabfrage ohne Job.

### `FfmpegLiveStreamSegmenterE2ETests`

Datei: `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs`, `[Trait("Category", "E2E")]`, echter ffmpeg-Prozess (still beendet, wenn kein Binary im `PATH`).

- `RealFfmpeg_ProducesPlaylistAndSegments` — echte Segmentierung erzeugt `live.m3u8` + `seg_*.ts`, `LastUnexpectedExit == null`.
- `SecondJob_PlaylistContinuesAndMarksDiscontinuity` — Sendeplatz-Wechsel mit laufendem Job (N17-Regression): Playlist läuft mit `#EXT-X-DISCONTINUITY` weiter statt zurückzusetzen.

### `LiveStreamStandbySegmenterTests`

Datei: `VideoWebPlayer.Tests/Services/LiveStreamStandbySegmenterTests.cs`

- `TranscodeAsync_BuildsSegmentArgs_AndAppliesDurationCap` — Segment-Muxer-Argumente, `-t 60`, ffprobe-Fallback-Dauer.
- `TranscodeAsync_PipeInput_UsesPipeAndPumpsStream` — Pipe-Eingabe pumpt in stdin (`-i pipe:0`, `RedirectStandardInput`) — einzige Pump-Berührung in der Suite, prüft aber weder Outcome noch Logging.
- `TranscodeAsync_ExitCodeNonZero_ReturnsNull` — Fehler-Exit → `null`.
- `TranscodeAsync_NoSegmentsProduced_ReturnsNull` — leeres Ergebnis → `null`.
- `TranscodeAsync_StallingProcess_TimesOutKillsAndReturnsNull` — `TranscodeTimeout` killt stallenden Prozess.
- `TranscodeAsync_KillFailsAndStderrNeverEnds_ReturnsWithinBound` — fehlschlagendes `Kill` + endloser stderr-Stream: `TranscodeAsync` kehrt zeitgebunden zurück (stderr-Drain-/Pump-Timeout-Pfad).
- `TranscodeAsync_LockedStaleSegment_StaysOutOfResult` — gesperrtes Alt-Segment landet nicht im Ergebnis (Windows-Verhalten geprüft).
- `TranscodeAsync_FfmpegMissing_ReturnsNull` — fehlendes ffmpeg-Binary → `null`.

### `LiveStreamCoordinatorTests`

Datei: `VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs` (nutzt `FakeSegmenter : ILiveStreamSegmenter`)

- `SynchronizeAsync_RunningSlot_StartsSegmentation` — laufender Sendeplatz startet Job.
- `SynchronizeAsync_SameSlotWithinDrift_Unchanged` — Drift ≤ `MaxDriftSeconds` → kein Neustart.
- `SynchronizeAsync_SameSlotBeyondDrift_Restarts` — Drift > 30 s → Neustart.
- `SynchronizeAsync_DifferentSlot_RestartsOnNewSlot` — Sendeplatz-Wechsel → neuer Job.
- `SynchronizeAsync_ScheduleGap_StopsAndWakesAtNextStart` — Sendelücke stoppt.
- `SynchronizeAsync_FfmpegMissing_ReportsUnavailable` — kein ffmpeg → `FfmpegUnavailable` + Stop.
- `SynchronizeAsync_UnresolvableMedia_ReportsMediaUnavailable` — nicht auflösbares Medium → Stop.
- `SynchronizeAsync_RemoteMedia_UsesHttpInput` — Remote-Quelle bevorzugt HTTP-Eingang.
- `SynchronizeAsync_HttpTransportFailureSameSlot_FallsBackToPipe` — `LastUnexpectedExit` mit `HttpTransportFailure` am selben Slot → Pipe-Eingang.
- `SynchronizeAsync_NonTransportExit_KeepsHttpInput` — anderer Fehler behält HTTP-Eingang.
- `SynchronizeAsync_HttpTransportFailureOtherSlot_KeepsHttpInput` — Fallback gilt nur pro Slot.

### `LiveStreamWorkerTests`

Datei: `VideoWebPlayer.Tests/LiveStreamWorkerTests.cs` (nutzt eigenes `FakeSegmenter`)

- `ExecuteAsync_Startup_ClearsSegmentDirectoryAndTicks` — Startaufräumen + Synchronisationstakt.
- `ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts` — deaktiviertes Feature-Flag stoppt den Segmentierer.
- `ExecuteAsync_ClearsSegmentDirectory_BeforeInitialDelay` — Reihenfolge der Startphase.

### `LiveStreamStandbyProviderTests`

Datei: `VideoWebPlayer.Tests/Services/LiveStreamStandbyProviderTests.cs` —
Provider-Sicht (Playlist-Fenster, Cache, Transcode-Auslösung); nutzt
`LiveStreamStandbySegmenter` über den Produktiv-Ctor mit Fake-Locators.

## Hilfsmethoden

### `ListLogger<T>` (`VideoWebPlayer.Tests/Helpers/ListLogger.cs`)

- `Log<TState>(...)` — zeichnet `(LogLevel, Message)` in `Entries` auf; hängt die
  Ausnahme inkl. Stacktrace an die Nachricht an (`exception is not null` →
  `message + NewLine + exception`) — damit ist der „kein Stacktrace"-Nachweis der
  Anforderung direkt testbar (`Entries` auf Vorhandensein von `Exception`-Text prüfen).
- `Entries` / `_messages` — getrennte Queues mit bzw. ohne Level.

### `FfmpegLiveStreamSegmenterTests` (klassenintern)

- `ControlledPipeStream` — `Stream`-Attrappe, die Reads blockiert, bis `WriteText`/
  `End` aufruft (modelliert stderr ohne EOF vor Prozessende).
- `FakeSegmenterProcess` — `ISegmenterProcess`-Fake mit `SimulateExit`,
  `Killed`-Flag, `MemoryStream` als `StandardInput`, `ControlledPipeStream` als stderr.
- `WaitUntilAsync` — pollt eine Bedingung bis zu ~2 s (für asynchrone Drain-/Pump-Nebeneffekte).
- `CreateSegmenter` / `FileJob` — Fabrik mit protokollierendem `_startProcess`-Factory und Standardjob.

### `LiveStreamStandbySegmenterTests` (klassenintern)

- `FakeProcess` — `ISegmenterProcess`-Fake mit steuerbarem Exit-Code und `onWait`-Callback.
- `StallingProcess` — `WaitForExitAsync` endet nur per Cancellation (stallender Prozess).
- `UnkillableStallingProcess` — `Kill` wirft, stderr endet nie (Blockierpfad-Test).
- `NeverEndingStream` — Leseaufrufe schließen nie ab.
- `FakeFfmpegLocator` / `FakeFfprobeLocator` / `MissingFfmpegLocator` — Locator-Attrappen.
- `CreateSegmenter` — Ctor mit injizierbaren Timeouts (`transcodeTimeout`, `stderrDrainTimeout`).

### `E2EBinaryLocator` (`VideoWebPlayer.Tests/Helpers/E2EBinaryLocator.cs`)

- `Find(name)` — sucht ein Werkzeug-Binary im `PATH`; E2E-Tests beenden sich still bei `null`.
