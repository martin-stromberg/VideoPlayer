# Interfaces

## `ISegmenterProcess`

Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs` (Zeilen 15–34), `internal`, erbt `IDisposable`.

Abstraktion über den laufenden ffmpeg-Prozess, damit Tests einen Fake-Prozess einsetzen
können. Produktiv-Implementierung: `SegmenterProcess` (Zeilen 39–71) über
`System.Diagnostics.Process` (`StandardInput`/`StandardError` geben `BaseStream` zurück;
`Kill` nutzt `entireProcessTree: true`). Test-Implementierungen: `FakeSegmenterProcess`
(`FfmpegLiveStreamSegmenterTests`), `FakeProcess`/`StallingProcess`/`UnkillableStallingProcess`
(`LiveStreamStandbySegmenterTests`).

| Member | Typ | Beschreibung |
| --- | --- | --- |
| `HasExited` | `bool` (get) | Prozess beendet? |
| `ExitCode` | `int` (get) | Exit-Code, −1 solange der Prozess läuft. |
| `StandardInput` | `Stream` (get) | stdin-Stream für gepumpte Pipe-Eingabe (`pipe:0`). |
| `StandardError` | `Stream` (get) | stderr-Stream (muss gedraint werden, sonst Pipe-Stall). |
| `WaitForExitAsync(CancellationToken)` | `Task` | Wartet auf Prozessende. |
| `Kill()` | `void` | Beendet den Prozess (und Kindprozesse). |
| `Dispose()` (via `IDisposable`) | `void` | Ressourcenfreigabe. |

## `ILiveStreamSegmenter`

Datei: `VideoWebPlayer/Services/ILiveStreamSegmenter.cs` (Zeilen 12–50), `public`.

Vertrag des einen kontinuierlichen HLS-Segmentierungsprozesses; einzige Implementierung
`FfmpegLiveStreamSegmenter`; konsumiert von `LiveStreamCoordinator` und `LiveStreamWorker`.
Test-Implementierung: `FakeSegmenter` in `LiveStreamCoordinatorTests` und
`LiveStreamWorkerTests`.

| Member | Typ | Beschreibung |
| --- | --- | --- |
| `IsRunning` | `bool` (get) | Läuft aktuell ein Segmentierungsprozess? |
| `CurrentSlotId` | `long?` (get) | Sendeplatz-ID des laufenden Jobs, `null` wenn keiner läuft. |
| `ExpectedPositionSeconds` | `double?` (get) | Erwartete Abspielposition des laufenden Prozesses (Drift-Erkennung), `null` ohne Job. |
| `LastUnexpectedExit` | `LiveStreamExitInfo?` (get) | Diagnose des letzten vorzeitig selbst beendeten Prozesses; `null` bei erwartetem Ende oder nach Neustart — steuert den HTTP→Pipe-Input-Fallback des Koordinators. |
| `StartAsync(LiveStreamJob, CancellationToken)` | `Task` | Job starten; ein laufender Job wird vorher gestoppt. |
| `StopAsync(CancellationToken)` | `Task` | Laufenden Prozess stoppen (idempotent). |
