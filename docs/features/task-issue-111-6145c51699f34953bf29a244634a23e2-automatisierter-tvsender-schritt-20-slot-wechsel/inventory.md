# Bestandsaufnahme: Nahtloser Sendeplatz-Wechsel im HLS-Live-Stream (N17)

Analyse der HLS-Erzeugung (`FfmpegLiveStreamSegmenter`), Playlist-Auslieferung
(`LiveStreamController`), Job-Steuerung (`LiveStreamCoordinator`/`LiveStreamWorker`) und
Player-Anbindung (`liveTv.js`) bezogen auf die N17-Anforderung — inklusive empirischer
Verifikation des ffmpeg-HLS-Muxers (Version 9.0.2) auf diesem System.

## Zusammenfassung

- Die Segmentierung läuft pro Sendeplatz als eigener ffmpeg-Prozess
  (`-f hls -hls_flags delete_segments+independent_segments`); jeder Start schreibt
  `live.m3u8` komplett neu: `EXT-X-MEDIA-SEQUENCE` beginnt wieder bei 0, alle zuvor
  gelisteten Segmente verschwinden, die Zeitbasis springt auf 0 — für hls.js ist das eine
  neue, inkompatible Playlist (Media-Sequence darf nie sinken, RFC 8216) → eingefrorenes
  letztes Bild / endlos Laden (Live-Befund N17).
- **Empirisch verifiziert (ffmpeg 9.0.2, Probe in `/tmp/hls-exp`):** der Muxer-Flag
  `append_list` hält die alten Playlist-Einträge, führt `EXT-X-MEDIA-SEQUENCE` **und** den
  `%05d`-Dateinamenzähler fort und markiert das erste Segment jedes neuen Laufs automatisch
  mit `#EXT-X-DISCONTINUITY` — sauber, ohne Zusatztag im Header. `discont_start` erzeugt
  zusätzlich ein (legales, aber redundantes) Head-Discontinuity-Tag; `program_date_time`
  ergänzt `EXT-X-PROGRAM-DATE-TIME` je Segment.
- Der `LiveStreamController` rewritet nur nicht-`#`-Zeilen (Token-Injection) — DISCONTINUITY-
  und PROGRAM-DATE-TIME-Zeilen passieren unverändert; kein Eingriff nötig.
- `liveTv.js` behandelt nur `data.fatal`-Fehler und ruft pauschal `hls.startLoad()` — für
  `MEDIA_ERROR` empfiehlt hls.js `recoverMediaError()` (Recovery-Lücke).
- Test-Ausgangszustand: **2146/2147 grün, 1 Alt-Skip, 0 Fehler** (4:12 min) — Nachweis:
  `inventory/test-results/baseline-summary.txt`. Debug-Build: 0 Fehler, 144 Warnungen
  (Baseline).

## Details

- [Logik / Streaming-Pfad](inventory/logic.md)
- [Empirischer ffmpeg-Nachweis](inventory/ffmpeg-append-list.md)
- [Tests](inventory/tests.md)
