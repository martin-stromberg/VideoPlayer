# Enums — Bestandsaufnahme

## LiveStreamSyncAction

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` (Zeilen 9–21)

| Wert | Bedeutung |
|---|---|
| `Unchanged` | Laufende Segmentierung passt bereits zum Sendeplan — nichts geändert. |
| `Started` | Segmentierungs-Job für die laufende Sendung (neu) gestartet. |
| `Stopped` | Segmentierung gestoppt (Sendelücke oder nicht verfügbarer Eingang). |
| `FfmpegUnavailable` | ffmpeg nicht auffindbar — keine Segmentierung möglich. |
| `MediaUnavailable` | Medium des laufenden Sendeplatzes nicht zu einem Eingang auflösbar. |
