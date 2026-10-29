# Enums

## `SegmenterPumpOutcome`

Datei: `VideoWebPlayer/Services/SegmenterProcessSupport.cs` (Zeilen 14–25), `internal`.

Rückgabewert von `SegmenterProcessSupport.PumpInputAsync`; wird von den privaten
`PumpInputAsync`-Hüllen in `FfmpegLiveStreamSegmenter` (Z. 315) und
`LiveStreamStandbySegmenter` (Z. 272–277) ausgewertet.

| Wert | Bedeutung |
| --- | --- |
| `Completed` | Quelle bis zum Ende gepumpt **oder** Pipe-Bruch während des Prozessendes (erwartetes Verhalten beim Stop; erzeugt aktuell den beanstandeten `LogDebug(ex, …)` mit Stacktrace). |
| `SourceUnavailable` | `OpenStreamAsync` lieferte `null` — Eingabe konnte nicht geöffnet werden; Aufrufer loggen selbst eine Warnung. |
| `Failed` | Unerwarteter Fehler beim Pumpen; bereits als `LogWarning(ex, …)` im Helper protokolliert. |

## `LiveStreamSyncAction`

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` (Zeilen 9–21), `public`.

Ergebnis eines `SynchronizeAsync`-Ticks; nur Auslösekontext, voraussichtlich unverändert.

| Wert | Bedeutung |
| --- | --- |
| `Unchanged` | Laufende Segmentierung passt zum Sendeplan. |
| `Started` | Segmentierungs-Job wurde (neu) gestartet. |
| `Stopped` | Segmentierung gestoppt (Sendelücke/kein Eingang). |
| `FfmpegUnavailable` | ffmpeg nicht auflösbar. |
| `MediaUnavailable` | Medium des Sendeplatzes nicht auflösbar. |
