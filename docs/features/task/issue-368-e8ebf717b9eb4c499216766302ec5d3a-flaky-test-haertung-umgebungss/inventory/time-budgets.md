# Bestandsaufnahme: Zeitbudgets und Deadlines

Bestand der Host- vs. Kind-Budgets in Tests und Produktivcode — Grundlage für die Deadline-Angleichung aus Issue #368 (Referenzfehler der Blaupause: Host-Poll 30 s < Kind-Phasenbudget 90 s).

## Produktivcode-Budgets (Kind-/Phasen-Ebene)

| Stelle | Konstante | Wert |
|--------|-----------|------|
| `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs:90` | `StopTimeout` | 10 s — `process.WaitForExitAsync().WaitAsync(StopTimeout)` beim geplanten Stop (Z. 292) |
| `:93` | `ExitDetectionTimeout` | 5 s — synchrones `Wait` auf den Exit-Check (Z. 473) |
| `:98` | `FirstOutputPollInterval` | 500 ms — Poll-Intervall für „erstes Segment" |
| `:103,146` | `_pumpDrainTimeout` | `SegmenterProcessSupport.PumpDrainTimeout` = 10 s (injizierbar) |
| `:369` | Exit-Toleranz | `max(10 s, 2×SegmentSeconds)` |
| `:528–530` | Watch-Horizont | `max(15 s, 3×SegmentSeconds)` bzw. `RemainingSeconds + max(30 s, 3×SegmentSeconds)` |
| `VideoWebPlayer/Services/SegmenterProcessSupport.cs:59` | `PumpDrainTimeout` | 10 s — `AwaitDrainObservingFaultsAsync` (Z. 291–298) |
| `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs:31` | `MaxStandbyDurationSeconds` | 60 s |
| `:49` | `TranscodeTimeout` | 120 s (= 2×60) |
| `:53` | `StderrDrainTimeout` | 10 s |

## Test-Deadlines (Host-Ebene)

| Stelle | Budget | Beziehung zum Kind-Budget |
|--------|--------|---------------------------|
| `Services/FfmpegLiveStreamSegmenterE2ETests.cs:51` | `timeout.CancelAfter(45 s)` auf `IsRunning` | > `StopTimeout` 10 s, > 6-s-Job — passt |
| `:245,294,340` | 45–60 s gegen echte ffmpeg-Läufe | über Segment-Toleranz (12–18 s bei 2-s-Segmenten) — knapp, aber plausibel |
| `:398–406` | `WaitForPlaylistEntryAsync` 45 s | dto. |
| `LiveStreamE2ETests.cs:260–267` | 120×500 ms = 60 s Poll auf `standby_1_` | vs. `TranscodeTimeout` 120 s des Standby-Segmenters — **Host-Budget < Kind-Budget**: bei langsamem Transcode schlägt der Test fehl, obwohl das Kind regulär weiterlaufen dürfte. Kandidat für Angleichung |
| `Services/FfmpegLiveStreamSegmenterTests_FirstOutput.cs:105–111` | `WaitUntilAsync` 200×10 ms = **2 s** | vs. `FirstOutputPollInterval` 500 ms + konfigurierbarem `firstOutputWarnAfter` — nur ~4 Poll-Runden; knapp bei CI-Last |
| `Helpers/TestHelpers.cs` `WaitFor*` | 10-ms-Poll, Caller-Timeouts 2–10 s | `MediaSourceScanServiceTests` ruft mit 2–10 s (`waitTimeSec = 10`, Z. 293) — knapp für echten Scan + Klassifikation unter Last |
| `LiveStreamWorkerTests.cs:138,176,213,252,315,371,443` | `cts.CancelAfter(15–30 s)` + 50–100-ms-Poll | Worker-Tick über `ControllableTimeProvider` — großzügig |
| `UdpDiscoveryListenerTests.cs` | 10-s-Deadlines, 50-ms-Poll, 500-ms-`WaitAsync`-Fenster (Z. 27, 86–96, 136–150, 325–328) | UDP-Roundtrip Loopback — ausreichend; Socket-Race (Bind/ICMP) dokumentiert |
| `Helpers/PlaylistsE2ETestBase.cs:100` | `Page.SetDefaultTimeout(30_000)` | Playwright-Aktionen |
| `:170–176` | `InteractionAttemptTimeout` 10 s / `InteractionDeadlineMilliseconds` 60 s | `DriveUntilStateAsync`-Wiederholer gegen Circuit-Handover |
| `:134,349,352` | feste `WaitForTimeoutAsync(2000/1500/1000)` | feste Sleeps = klassische Flake-Quelle bei Last |
| `Helpers/TVSenderAdminE2ETestBase.cs:35` | `WaitForSelectorAsync(Timeout = 30_000)` | — |
| `Services/SegmenterProcessSupportTests.cs:324,338,356,382` | Drain-Budgets 5 s / 50 ms | testet das Timeout-Verhalten selbst — bewusst knapp, kein Mismatch |
| `Helpers/TestHelpers.cs:301` (`RunIcacls`), `Services/LocalMediaSourceReaderTests.cs:324` (`TryCreateJunction`) | `WaitForExit(10_000)` | Kindprozess-Warte — Rückgabewert von `WaitForExit(timeout)` wird **nicht** ausgewertet (icacls prüft nur `ExitCode`, Junction prüft `ExitCode == 0 && Exists`) — ein hängender Prozess blockiert 10 s, liefert dann ggf. `ExitCode` != 0 |
| `MediaProbeHttpIntegrationTests.cs:144` | `WaitForExitAsync()` **ohne** Timeout | ffmpeg-Videogenerierung kann theoretisch hängen — kein Budget |
| `Services/FfmpegLiveStreamSegmenterE2ETests.cs:445+` (`GenerateVideoAsync`), `LiveStreamE2ETests.cs:233` | `WaitForExitAsync(ct)` | Test-CT, kein eigenes Budget |

## Job-Budgets

| Ebene | Wert |
|-------|------|
| CI `build-and-test` (`staging-ci.yml:99`, `pr-staging-ci.yml:119`) | `timeout-minutes: 30` |
| CI `static-checks` | 15 min; `version` 10; `prerelease` 20; `release` 30 |
| Blaupause `os-interface-tests`-Job | `timeout-minutes: 30` |

## Befund

- Der Blaupausen-Mismatch (Host-Poll < Kind-Phasenbudget) existiert hier **nur punktuell**: `LiveStreamE2ETests` Standby-Poll 60 s vs. `TranscodeTimeout` 120 s ist der deutlichste Fall; `FfmpegLiveStreamSegmenterTests_FirstOutput` (2 s) und die `WaitFor*`-Deadlines (2–10 s) in `MediaSourceScanServiceTests`/`MediaSourceScanCoordinator*` sind die knappsten Host-Budgets.
- Zwei unbegrenzte `WaitForExitAsync`/`WaitForExitAsync(ct)`-Stellen auf echten Kindprozessen (`MediaProbeHttpIntegrationTests`, ffmpeg-Generatoren) haben kein eigenes Zeitbudget — ein hängender ffmpeg-Prozess friert den Test bis zum Job-Timeout ein.
- Die 30-s/60-s-Playwright-Timeouts gegen ein 30-min-Job-Budget sind unproblematisch (Größenordnungen dazwischen).
- Diagnose-Ausgaben sind vorhanden und gut: `LiveStreamExitInfo` (ExitCode, HttpTransportFailure, UndecodableInput, SftpInputFailure), `WaitFor*`-Assert-Meldungen enthalten Log-Dumps (`TestHelpers.cs:29–32`), `DriveUntilStateAsync` lässt den echten Playwright-Fehler durch.
