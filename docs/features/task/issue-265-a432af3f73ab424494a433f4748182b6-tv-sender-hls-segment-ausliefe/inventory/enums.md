# Enums — Bestandsaufnahme

## `LiveStreamSyncAction`

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` (Z. 9–21)

| Wert | Bedeutung |
|------|-----------|
| `Unchanged` | Laufende Segmentierung passt zum Sendeplan — nichts geändert |
| `Started` | Segmentierungs-Job für die aktuelle Sendung (neu) gestartet |
| `Stopped` | Segmentierung gestoppt (Sendelücke oder Eingang nicht auflösbar) |
| `FfmpegUnavailable` | ffmpeg nicht auffindbar — keine Segmentierung möglich |
| `MediaUnavailable` | Medium des laufenden Sendeplatzes nicht auflösbar |

Rückgabewert von `LiveStreamCoordinator.SynchronizeAsync` (als `LiveStreamSyncResult.Action`), ausgewertet in `LiveStreamWorker.ExecuteAsync` (Z. 91–94).
