# Interfaces (Bestandsaufnahme Issue #333)

## `IProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` (Zeile 10)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged()` | – | `void` | Meldet Änderungen an den Sendeplan-Eingaben. Während eines offenen Scans wird nur gezählt, das Wecken erfolgt beim Scan-Ende. |
| `BeginScan()` | – | `IDisposable` | Öffnet einen Scan-Scope. Beim letzten Scope-Ende wird einmal geweckt, falls unterdrückt wurde. |
| `NotifyLiveScheduleChanged()` | – | `void` | Meldet, dass der Sendeplan materialisiert geändert wurde (nur für den Live-Stream-Worker). |
| `WaitAsync(CancellationToken)` | `CancellationToken` | `ValueTask` | Wartet auf ein Wecksignal des Sendeplan-Workers. |
| `WaitLiveAsync(CancellationToken)` | `CancellationToken` | `ValueTask` | Wartet auf ein Wecksignal des Live-Stream-Workers. |

Implementierung: `ProgramScheduleSignal` (Singleton laut Dokumentation). Testdoppel: `MediaDurationProbeWorkerTests.cs` (Zeilen ca. 292–325, Wrapper um die echte Implementierung).

## `IPlaylistBackfillSignal`
Datei: `VideoWebPlayer/Services/PlaylistBackfillSignal.cs` (Zeile 10)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyMarkersWritten()` | – | `void` | Meldet geschriebene Backfill-Marker. Während eines Scans kein Wecken. |
| `BeginScan()` | – | `IDisposable` | Öffnet einen Scan-Scope. Beim letzten Scope-Ende wird immer geweckt. |
| `WaitAsync(CancellationToken)` | `CancellationToken` | `ValueTask` | Wartet auf das Wecksignal des `PlaylistBackfillWorker`. |

Implementierung: `PlaylistBackfillSignal`. Testdoppel: `PlaylistBackfillMarkerHookTests.cs` (Zeile ca. 296, `NotifyMarkersWritten` zählt Aufrufe).

## `IBackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs` (Zeile 6)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsPausedForRestore` (Eigenschaft) | – | `bool` | `true`, solange ein Restore die Hintergrundverarbeitung pausiert. |
| `EnterOperationAsync(string name, CancellationToken)` | Operationsname, Abbruchtoken | `Task<IAsyncDisposable>` | Erwirbt eine Lease für eine Operation. Wartet, solange ein Restore läuft. |
| `PauseForRestoreAsync(CancellationToken)` | Abbruchtoken | `Task<IAsyncDisposable>` | Pausiert alle Operationen für einen Restore; Lease wird beim Dispose freigegeben. |

Implementierung: `BackgroundProcessingGate` (`VideoWebPlayer/Services/Backups/BackgroundProcessingGate.cs`). Die Tests `BackgroundProcessingGateTests` (drei Fakten) prüfen Blockieren, Parallelitäts-Sperre und Abbruch beim Warten.

## Nicht vorhanden
- Kein Interface und keine Klasse `MediaSourceScanCoordinator` (Suche im gesamten Repository ohne Treffer).
