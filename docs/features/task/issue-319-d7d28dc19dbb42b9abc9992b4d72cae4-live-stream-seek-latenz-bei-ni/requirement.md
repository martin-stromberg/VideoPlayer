# Anforderung — Issue #319: Seek-Latenz bei nicht-seekbaren Eingängen beobachtbar machen

Folgepunkt aus #315 (dokumentiert in `docs/features/task/issue-315-*/continue.md`,
Punkt 1). GitHub-Issue: martin-stromberg/VideoPlayer#319.

## Befund (aus dem Issue, im Code verifiziert)

`FfmpegLiveStreamSegmenter.BuildArguments` erzeugt `-re -ss {pos} -i {spec}`. Für
seekbare Eingänge (`ForFile`, `ForHttp` mit Range, `ForSftp`) greift der Demuxer-Seek
`avformat_seek_file` vor dem Streaming — kein Problem. Latenz entsteht nur, wenn kein
Zeit→Byte-Mapping existiert:

- `pipe:0` (Fallback-Eingang): jeder Byte vor dem Ziel wird in Echtzeit gelesen und
  verworfen — Verzögerung ≈ `PositionSeconds`, kann die Restlaufzeit überschreiten.
- Seekbarer Container ohne brauchbaren Index (z. B. MKV ohne Cues): `-re`-gedrosselter
  Vorwärtsscan.

**Beobachtbarkeit fehlt heute:** `ExpectedPositionSeconds` ist reine Wanduhr-Projektion
— der 30-s-Drift-Wächter (`LiveStreamCoordinator.cs:154-164`) sieht einen hängenden
Seek nie; stderr läuft auf `-loglevel warning` und meldet während des Scans nichts.

## Vorgehen (aus dem Issue übernommen)

1. Wandzeit vom Start (`_startedAt`) bis zum ersten `seg_*_{slotId}_*.ts` bzw. zur
   ersten Playlist-Rewrite messen und loggen.
2. Warnung, wenn nach einer plausiblen Obergrenze (normal: Prozessstart + eine
   Segmentlänge) noch keine Ausgabe vorliegt — der hängende Seek soll im Log sichtbar
   werden, während er läuft.
3. ffmpeg-Optionen **nicht** ändern: `-noaccurate_seek`, output-seitiges `-ss` und
   `-seekable 0` sind im Issue jeweils als kontraproduktiv begründet; differenzierte
   Optionen je Eingangsart folgen erst bei Befund (Diag-Lauf mit `-loglevel info`
   gegen reale Quellen).

## Umsetzung (Entwurf)

- „First-Output-Wächter" als Neben-Task des Segmenters (analog stderr-Drain):
  Snapshot des Segmentverzeichnisses beim Start (bekannte `seg_*_{slotId}_*.ts`-
  Namen + Playlist-mtime — ein Segmentzähler `00000` ist bei `append_list`-Neustarts
  nicht garantiert), danach Poll bis erstes neues Segment oder Playlist-Rewrite.
- Info-Log „Erstes Segment … nach {Latency}s (Eingang, Position)" beim ersten Output;
  Warning „Kein erstes Segment … — Seek-/Scan-Latenz vermutet" oberhalb der Schwelle.
- Unexpected-Exit-Report ergänzt, ob überhaupt ein Segment erzeugt wurde — ein beim
  Scan sterbender ffmpeg bleibt sonst ununterscheidbar von einem echten Demux-Fehler.

## Abnahme-Status

- [x] Wandzeit bis zum ersten Segment wird geloggt (mit Eingangsart und Position)
- [x] Ausbleibendes erstes Segment erzeugt eine Warnung oberhalb der Schwelle
- [x] Vorhandene Segmente älterer Läufe zählen nicht als erstes Segment des neuen Laufs
- [x] Exit-Report nennt, ob ein Segment erzeugt wurde
- [x] Volle Testsuite grün (siehe test-results.md)
