# Umsetzungsplan: Nahtloser Sendeplatz-Wechsel im HLS-Live-Stream (N17)

## Übersicht

Der ffmpeg-HLS-Lauf bekommt die Playlist-Fortführung (`append_list`) plus explizite
Grenzmarkierung (`discont_start`) und Wall-Clock-Anker (`program_date_time`), sodass
`live.m3u8` über Job-Grenzen hinweg eine legale, kontinuierliche Live-Playlist bleibt:
alte Segmente bleiben gelistet, `EXT-X-MEDIA-SEQUENCE` läuft weiter und
`#EXT-X-DISCONTINUITY` markiert den Generationswechsel. Ergänzend wird die hls.js-
Fehlerbehandlung in `liveTv.js` auf die dokumentierte differenzierte Recovery umgestellt.
Ein E2E-Regressionstest mit zwei echten ffmpeg-Läufen sichert das Verhalten.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Grenzmarkierung + Sequence-Kontinuität | **ffmpeg-Muxer-Flags** `append_list+discont_start+program_date_time` in `BuildArguments` | `append_list` erzeugt *beides* nativ (fortgeführte Einträge + Media-Sequence + automatischer DISCONTINUITY am Generationswechsel — empirisch verifiziert, siehe `inventory/ffmpeg-append-list.md`). Eine Controller-Injektion kann nur den Marker setzen, nicht die zurückgesetzte Sequence reparieren — dafür bräuchte es einen zustandsvollen Playlist-Stitcher (Nachbau von append_list, neue Races gegen ffmpeg-Writes). |
| Sequence-Kontinuität per `-start_number`/`hls_start_number_source` | verworfen | Fortlaufende Nummern allein lassen alle URIs wechseln (Loch in der Fragmentliste, kein Backfill) und markieren keine Diskontinuität — strikt schwächer als `append_list`. |
| Ein ffmpeg-Dauerlauf mit Input-Umschaltung | verworfen | Großer Umbau (concat/live-switch, Neustartlogik), nicht nötig — der Markerweg löst den Live-Befund vollständig. |
| `discont_start` obwohl ffmpeg 9 den Grenzmarker automatisch setzt | beibehalten | Garantiert den Marker auch auf älteren ffmpeg-Builds (Deployment sucht ffmpeg per Pfad/tools/PATH — Version nicht kontrollierbar); kostet nur ein redundantes, legales Head-Tag. |
| `program_date_time` | beibehalten | `EXT-X-PROGRAM-DATE-TIME` verankert `liveSyncPosition` in hls.js an der Wanduhr — stabilisiert die Live-Edge-Position über der Diskontinuität; bei `-re` akkurate Zeitachse. |
| Player-Recovery in `liveTv.js` | kleine Härtung | hls.js-Referenzmuster: `NETWORK_ERROR` → `startLoad()`, `MEDIA_ERROR` → `recoverMediaError()`, sonst destroy+Neu-Attach. Deckt Restfälle ab (z. B. eskalierte Buffer-Fehler am Übergang); Blazor-Benachrichtigungen bleiben unverändert. Kein Ersatz für den Server-Fix. |
| `LiveStreamController`-Rewrite | unverändert | `#`-Zeilen (DISCONTINUITY, PROGRAM-DATE-TIME, MEDIA-SEQUENCE) passieren den Token-Rewrite bereits transparent. |

## Programmabläufe

Sendeplatz-Wechsel (neu): `LiveStreamCoordinator.SynchronizeStreamAsync` →
`FfmpegLiveStreamSegmenter.StartAsync` → `StopCoreAsync` (alter Prozess endet; dessen
Playlist bleibt auf Platte) → neuer ffmpeg-Lauf mit `append_list`: der Muxer liest die
bestehende `live.m3u8`, übernimmt deren Einträge + Sequence, markiert sein erstes Segment
mit `#EXT-X-DISCONTINUITY` und führt das Fenster fort. hls.js sieht eine legale
Live-Playlist mit echter Diskontinuität und spielt nahtlos weiter.

Gleiches gilt für Drift-/Crash-Neustarts (ebenfalls echte Diskontinuitäten) und — weil
`ClearAsync` beim Worker-Start das Verzeichnis leert — unverändert frisch nach
Server-Neustart.

## Schritte

1. `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`
   - `BuildArguments`: `-hls_flags` um `append_list+discont_start+program_date_time`
     erweitern → `delete_segments+independent_segments+append_list+discont_start+program_date_time`.
   - XML-Doku der Klasse/`BuildArguments` anpassen: Playlist wird über Job-Grenzen
     fortgeführt, Grenzen per DISCONTINUITY markiert (statt „beginnt je Lauf sauber").
2. `VideoWebPlayer/wwwroot/js/liveTv.js`
   - `ERROR`-Handler differenzieren: `Hls.ErrorTypes.NETWORK_ERROR` → `startLoad()`;
     `Hls.ErrorTypes.MEDIA_ERROR` → `recoverMediaError()` (einmal, dann Retry-Takt);
     sonst → `hls.destroy()` + Neu-`attachHls` über den bestehenden Retry-Takt.
     `notifyError`/`errorNotified`-Semantik unverändert.
3. `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs`
   - `StartAsync_FileInput_BuildsExpectedArguments`: zusätzlich
     `Assert.Contains("append_list", args)`, `Assert.Contains("discont_start", args)`,
     `Assert.Contains("program_date_time", args)`.
4. `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs`
   - Neuer Test `TwoConsecutiveJobs_PlaylistContinuesAndMarksDiscontinuity`:
     zwei lavfi-Dummy-Videos; Job 1 (`SlotId 1`, `RemainingSeconds` groß, `SegmentSeconds 2`)
     starten und warten, bis die Playlist einen `_1_`-Segment-URI listet; Job 2
     (`SlotId 2`) per `StartAsync` **bei noch laufendem Job 1** starten (exakter
     Slot-Wechsel-Pfad: Stop→Start im selben Gate); warten bis ein `_2_`-URI gelistet ist;
     `StopAsync`. Asserts:
     - Playlist enthält `#EXT-X-DISCONTINUITY` direkt über dem ersten `_2_`-Eintrag
       (zeilenweise: DISCONTINUITY-Zeile, deren nächste Nicht-`#`-Zeile ein `_2_`-URI ist);
     - Playlist listet weiter einen `_1_`-Segment-URI (Sequence-/Fenster-Kontinuität);
     - Playlist enthält `#EXT-X-PROGRAM-DATE-TIME` (PDT-Flag wirkt);
     - kein `LastUnexpectedExit` (beide Läufe erwartet gestoppt/laufend).
   - Wiederverwendung: `GenerateVideoAsync` (pro Video anderer `sine`-Frequenz),
     `FindBinary`, Temp-Dir-Pattern, 45-s-Timeout.
5. Doku: `docs/help/tv-sender.md` Abschnitt „Live-Stream (HLS)" — Satz „die Wiedergabeliste
   beginnt je Sendung sauber" durch Beschreibung der fortgeführten Playlist mit
   DISCONTINUITY-Markierung ersetzen. `docs/RELEASE_NOTES.md`: je ein Eintrag oben unter
   „What's New" und „Neuerungen".

## Tests

- Unit: Argument-Asserts (Schritt 3).
- E2E (primärer Funktionsnachweis, gefordert in N17): Schritt 4 — zwei echte
  ffmpeg-Läufe, Playlist-Struktur als Nachweis (Marker + fortgeführte Einträge).
- Vollsuite: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` synchron;
  Basis 2146 grün + 1 Alt-Skip. Build Debug+Release 0 Fehler, keine neuen Warnungen.

## Risiken / Abwägungen

- Älteres ffmpeg ohne `append_list`-Auto-Markierung: durch `discont_start` abgedeckt;
  ohne `append_list`-Unterstützung (sehr alt) schlägt der Muxer-Start fehl →
  `LastUnexpectedExit`-Diagnose greift, Verhalten wie heute (kein Stream) — akzeptabel,
  `append_list` existiert seit ffmpeg ~2.x.
- Playlist eines gekillten Laufs kann abgeschnitten sein: `parse_playlist` tolerant,
  Degradation = heutiger Zustand.
- Alt-Segmente im Fenster verweisen evtl. auf von der Retention gelöschte Dateien →
  404 beim Nachladen weit hinter der Live-Kante; Fenster (12) × Segmentlänge ≪ 30 min
  Retention — praktisch irrelevant.
- `EXT-X-ENDLIST` des planmäßig geendeten Vorlaufs verschwindet beim ersten
  Fenster-Update des Folgelaufs; hls.js wertet den Live-Status je Reload neu.

## Offene Punkte

Keine.
