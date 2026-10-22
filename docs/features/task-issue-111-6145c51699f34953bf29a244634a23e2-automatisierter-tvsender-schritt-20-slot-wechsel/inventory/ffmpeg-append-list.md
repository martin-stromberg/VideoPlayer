# Detaildokument: Empirischer ffmpeg-Nachweis (append_list / discont_start / program_date_time)

System: ffmpeg 9.0.2 (WinGet gyan.dev full build) auf `PATH`; Probe in `/tmp/hls-exp` mit
lavfi-Testquellen (`testsrc` + `sine`), `-hls_time 2`, `-hls_list_size 12`.

## Probe 1 — Referenzlauf ohne append_list

Playlist nach Laufende (`-t 4`):

```
#EXT-X-MEDIA-SEQUENCE:0
#EXT-X-INDEPENDENT-SEGMENTS
#EXTINF:4.000000,
seg_A_00000.ts
#EXT-X-ENDLIST
```

Ein Folgelauf schreibt `live.m3u8` komplett neu (heutiger Zustand → N17-Fehlerbild).

## Probe 2 — Folgelauf mit `append_list+discont_start+program_date_time`

```
#EXT-X-MEDIA-SEQUENCE:0
#EXT-X-DISCONTINUITY            ← Head-Tag (discont_start-Nebenwirkung)
#EXT-X-INDEPENDENT-SEGMENTS
#EXTINF:4.000000,
#EXT-X-PROGRAM-DATE-TIME:2026-10-22T14:39:05.053+0200
seg_A_00000.ts                   ← Alter Eintrag bleibt gelistet
#EXT-X-DISCONTINUITY             ← Generationsgrenze
#EXTINF:2.000000,
#EXT-X-PROGRAM-DATE-TIME:2026-10-22T14:39:09.053+0200
seg_B_00001.ts                   ← %05d-Zähler läuft weiter (nicht 00000!)
#EXTINF:2.000000,
seg_B_00002.ts
#EXT-X-ENDLIST                   ← nur, weil der Lauf regulär endete
```

## Probe 3 — Folgelauf mit nur `append_list` (+`program_date_time`, ohne `discont_start`)

```
#EXTINF:2.000000,
seg_D_00000.ts
#EXTINF:1.000000,
seg_D_00001.ts
#EXT-X-DISCONTINUITY             ← automatisch gesetzt, sauber vor dem neuen Segment
#EXTINF:2.000000,
#EXT-X-PROGRAM-DATE-TIME:2026-10-22T14:39:48.725+0200
seg_E_00002.ts
```

## Probe 4 — nur `append_list` (ohne PDT, ohne discont_start)

```
#EXTINF:2.000000,
seg_F_00000.ts
#EXTINF:1.000000,
seg_F_00001.ts
#EXT-X-DISCONTINUITY
#EXTINF:2.000000,
seg_G_00002.ts
```

## Befund

- `append_list` allein liefert in ffmpeg 9: fortgeführte Eintragsliste, fortgeführte
  Media-Sequence, fortgeführter Dateinamenzähler und `#EXT-X-DISCONTINUITY` vor dem ersten
  Segment jeder neuen Generation (auch auf frischer/fehlender Playlist — dann vor dem
  allerersten Segment; harmlos, spez-konform).
- `discont_start` erzeugt zusätzlich ein redundantes DISCONTINUITY im Playlist-Head —
  legal, aber nicht nötig; trotzdem als Versions-Sicherheit sinnvoll (ältere ffmpeg ohne
  automatische Markierung), da es den Grenzmarker garantiert.
- `program_date_time` ergänzt `EXT-X-PROGRAM-DATE-TIME` je Segment (auch für übernommene
  Alt-Einträge wird ein Wert geschrieben) — hilft hls.js bei `liveSyncPosition` über die
  Diskontinuität hinweg; bei `-re`-Echtzeitaufnahme ist die Zeitachse akkurat.
- `parse_playlist` ist tolerant gegenüber abgeschnittenen Playlists (Kill mitten im Write):
  liest, was es kann; im Fehlerfall entsteht eine frische Liste — Degradation auf den
  heutigen Zustand, nie schlechter.
