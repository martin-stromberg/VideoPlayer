# Übersetzte Anforderung: Nahtloser Sendeplatz-Wechsel im HLS-Live-Stream (N17)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N17 (Priorität: hoch)

## Fachliche Zusammenfassung

Beim Sendeplatz-Wechsel des automatischen TV-Senders startet der Server einen neuen
`ffmpeg`-Segmentierungslauf, der `live.m3u8` vollständig neu schreibt (neue Segment-Dateinamen
mit der neuen `SlotId`, `EXT-X-MEDIA-SEQUENCE` zurückgesetzt, Zeitbasis auf 0). Der hls.js-Player
in der Live-TV-Ansicht kann die neue Playlist nicht an seinen Puffer anschließen — das letzte
Bild friert ein und die Wiedergabe lädt endlos, bis der Anwender pausiert und neu startet.
Der Übergang soll für HLS-Clients nahtlos werden: Die Playlist muss den Generationswechsel
regelkonform kennzeichnen (`#EXT-X-DISCONTINUITY`) und die Media-Sequence über den Wechsel
hinweg fortführen, statt sie zurückzusetzen.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs` — `BuildArguments` erzeugt die
  `-f hls`-Argumente (`-hls_flags delete_segments+independent_segments`, Segmentmuster
  `seg_{ts}_{slotId}_%05d.ts`, `PlaylistFileName`, `PlaylistWindowSegmentCount`).
- `VideoWebPlayer/Controllers/LiveStreamController.cs` — liefert `live.m3u8` aus und
  rewritet Segment-URIs mit `access_token` (`RewritePlaylistAsync`).
- `VideoWebPlayer/Services/LiveStreamCoordinator.cs` / `LiveStreamWorker.cs` — Job-Wechsel
  (`StartAsync` stoppt den alten Prozess und startet den neuen; erkennt Wechsel über
  `CurrentSlotId` / Drift).
- `VideoWebPlayer/Services/LiveStreamSegmentStore.cs` — Segment-Verzeichnis, Retention-Sweep.
- `VideoWebPlayer/wwwroot/js/liveTv.js` — hls.js-Anbindung, Fehler-/Retry-Behandlung.
- Tests: `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs` (echtes ffmpeg,
  lavfi-Dummy-Video-Muster), `FfmpegLiveStreamSegmenterTests.cs` (Argument-Asserts),
  `LiveStreamControllerTests.cs`.

## Implementierungsansatz

Primärer Weg (üblichste Lösung für zusammengesetzte Live-Pläne): den Übergang
**serverseitig in der Playlist** regelkonform abbilden — `#EXT-X-DISCONTINUITY` am
Generationswechsel plus fortgeführte `EXT-X-MEDIA-SEQUENCE`. Realisierbar entweder über die
`ffmpeg`-HLS-Muxer-Flags (`-hls_flags append_list` hält alte Einträge und führt die Sequence
fort; `discont_start` markiert das erste Segment des neuen Laufs; `program_date_time` ergänzt
`EXT-X-PROGRAM-DATE-TIME` für die Live-Edge-Synchronisation) oder über eine Playlist-Fortführung
beim Rewrite im `LiveStreamController`. Die Entscheidung wird im Plan anhand des empirisch
geprüften Muxer-Verhaltens begründet.

Ergänzend (nur falls serverseitig nicht ausreichend): Player-seitige Fehlerbehandlung in
`liveTv.js` — differenzierte hls.js-Recovery (`startLoad` bei Netzwerkfehlern,
`recoverMediaError` bei Medienfehlern), damit die Wiedergabe sich am Übergang selbst heilt.

Regressionstest (explizit gefordert): E2E mit echtem `ffmpeg` und zwei per lavfi erzeugten
Dummy-Videos — zwei aufeinanderfolgende `LiveStreamJob`s (verschiedene `SlotId`s); nach dem
Übergang muss die Playlist `#EXT-X-DISCONTINUITY` enthalten und die alten Segmente weiter
listen (Sequence-Kontinuität).

## Konfiguration

Keine neue Konfiguration — das Verhalten soll standardmäßig korrekt sein.

## Offene Fragen

Keine — der Live-Befund in N17 ist vollständig dokumentiert; die Designvarianten sind
Bestandteil der Planung und werden dort begründet entschieden.
