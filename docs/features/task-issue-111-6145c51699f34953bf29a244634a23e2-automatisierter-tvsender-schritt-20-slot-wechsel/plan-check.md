# Plan-Check: Nahtloser Sendeplatz-Wechsel im HLS-Live-Stream (N17)

**Status: Plan vollständig**

## Prüfung gegen die Anforderung

| Anforderungs-Punkt | Abgedeckt? | Stelle im Plan |
|---|---|---|
| `#EXT-X-DISCONTINUITY` am Übergang | ✓ | `append_list` (automatisch in ffmpeg ≥9) + `discont_start` (Versions-Sicherheit) |
| Sequence-Kontinuität | ✓ | `append_list` führt Einträge + Media-Sequence fort (empirisch verifiziert) |
| `EXT-X-PROGRAM-DATE-TIME` (ergänzend) | ✓ | `program_date_time`-Flag |
| Player-seitige Recovery `liveTv.js` | ✓ | differenzierte hls.js-Recovery (NETWORK → startLoad, MEDIA → recoverMediaError, sonst Neu-Attach) |
| E2E-Regressionstest: zwei echte ffmpeg-Jobs, Playlist muss DISCONTINUITY enthalten | ✓ | neuer Test in `FfmpegLiveStreamSegmenterE2ETests`, Job 2 wird bei laufendem Job 1 gestartet (exakter Slot-Wechsel-Pfad) |
| Analyse/Entscheidungsbegründung (Marker vs. Sequence vs. Player-Recovery) | ✓ | Tabelle „Designentscheidungen" |

## Selbstkritik — geprüfte Schwachstellen

- **Head-DISCONTINUITY durch `discont_start`:** empirisch ein zusätzliches Tag im Playlist-
  Header-Bereich (vor `EXT-X-INDEPENDENT-SEGMENTS`). Legal (Tag gilt für das nächste
  Segment), hls.js zählt es nur in den cc-Zähler — harmlos. Behalten, weil es den
  Grenzmarker auf älteren ffmpeg-Builds garantiert.
- **E2E-Assert-Trennschärfe:** „Playlist enthält DISCONTINUITY" allein wäre zu schwach
  (ffmpeg markiert auch das erste Segment eines frischen Laufs). Der Test assertet deshalb
  eine DISCONTINUITY-Zeile, deren **nächste Media-URI ein `_2_`-Segment** ist, plus einen
  gelisteten `_1_`-URI — das ist der eigentliche N17-Regressionspunkt (Playlist-Rewrite
  mit Sequence-Reset).
- **PDT-Format:** ffmpeg schreibt `+0200` (±hhmm) — ISO-8601-konform, hls.js parst es;
  durch die Probe belegt.
- **Log-Geheimnisse:** neue Flags ändern nichts an Argument-/stderr-Logging; bestehende
  Maskierung (`MaskStreamTokenInText`/`MaskCredentialsInText`) unberührt.
- **Retention vs. Fenster:** Alt-Einträge bleiben nur im 12er-Fenster gelistet —
  ≪ 30-min-Retention; kein neuer 404-Pfad.
- **Abriss der Playlist beim Kill:** `parse_playlist` tolerant → schlimmstenfalls heutiges
  Verhalten, nie schlechter.
- **Keine Migration/DB-Änderung**, keine UI-(.razor-)Änderung → Usability-Review nur
  formal (JS-Fehlerbehandlung nicht anwendersichtbar).

## Testabdeckung

- Primärer Funktionsnachweis: E2E mit echtem ffmpeg (explizit von N17 gefordert) — vorhanden.
- Unit-Assert auf die Flag-Zeile — vorhanden.
- Bestehende Tests bleiben grün: `LiveStreamJob`/Interface unverändert, Controller-Rewrite
  unverändert (`#`-Zeilen passieren).
