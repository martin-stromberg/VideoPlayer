# Bestandsaufnahme: Enums

## `MediaSourceType`

Datei: `VideoWebPlayer/Data/MediaSourceType.cs`

Steuert in `LiveStreamMediaResolver.ResolveAsync` (Z. 110), ob ein Stream-Token überhaupt
nötig ist.

| Wert | Bedeutung |
|------|-----------|
| `Sftp = 0` | Entfernter SFTP-Server — Resolver nutzt bevorzugt `TryCreateStreamUri` (HTTP-Eingang) bzw. Pipe-Fallback; Probes nutzen `TryCreateProbeUri`. |
| `LocalDirectory = 1` | Lokales Verzeichnis — Resolver gibt den physischen Pfad (`LiveStreamInput.ForFile`), **kein** Token nötig; Probes lesen lokal direkt. |

## `LiveStreamSyncAction`

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` (Z. 9–21)

Ergebnisart eines `SynchronizeAsync`-Ticks; für die Anforderung nur Kontext (ein
flag-bedingter Abbruch des ffmpeg-Reads würde als unerwarteter Exit klassifiziert, nicht
als eigene Aktion).

| Wert | Bedeutung |
|------|-----------|
| `Unchanged` | Laufende Segmentierung passt zum Programm. |
| `Started` | Neuer/erneuter Segmentierungslauf gestartet. |
| `Stopped` | Segmentierung gestoppt (Sendelücke oder nicht auflösbares Medium). |
| `FfmpegUnavailable` | ffmpeg-Binary nicht gefunden. |
| `MediaUnavailable` | Medium des laufenden Sendeplatzes nicht auflösbar. |
