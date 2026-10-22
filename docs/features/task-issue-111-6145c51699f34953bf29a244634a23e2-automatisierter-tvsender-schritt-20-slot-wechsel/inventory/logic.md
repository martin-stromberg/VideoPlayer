# Detaildokument: Logik — Streaming-Pfad

## `FfmpegLiveStreamSegmenter`
Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`

- `StartAsync` (Z. 144–185): Serialisiert über `_gate`; ruft zuerst `StopCoreAsync`
  (killt ggf. den laufenden Prozess und wartet ≤ 10 s), baut dann `ProcessStartInfo` mit
  `BuildArguments(job)` und startet den Prozess. Danach `DrainStandardErrorAsync` und ggf.
  `PumpInputAsync` als Hintergrundtasks.
- `BuildArguments` (Z. 367–392, `internal static`): erzeugt die ffmpeg-Kommandozeile:
  `-re -ss {pos} -i {input} -map 0:v:0 -map 0:a:0? -c:v libx264 -preset veryfast -c:a aac`
  `-force_key_frames expr:gte(t,n_forced*{seg})`
  `-f hls -hls_time {seg} -hls_list_size 12`
  `-hls_flags delete_segments+independent_segments`
  `-hls_segment_filename "{dir}/seg_{yyyyMMddHHmmss}_{slotId}_%05d.ts" -t {rest} "{dir}/live.m3u8"`
- Konstanten: `PlaylistFileName = "live.m3u8"` (Z. 83), `PlaylistWindowSegmentCount = 12` (Z. 86).
- `CurrentSlotId` (Z. 130) liefert die `SlotId` des laufenden Jobs — der Controller/Koordinator
  kennt den Generationswechsel sonst nur am Dateinamen-Muster.
- `LiveStreamJob` (`Services/LiveStreamJob.cs`): `SlotId`, `Input`, `PositionSeconds`,
  `RemainingSeconds`, `SegmentSeconds`, `SegmentDirectory`, `FfmpegPath`.

## `LiveStreamCoordinator` / `LiveStreamWorker`
Dateien: `VideoWebPlayer/Services/LiveStreamCoordinator.cs`, `LiveStreamWorker.cs`

- `SynchronizeStreamAsync` (Z. 89–161): rein zeitbasiert — Sendelücke → `StopAsync`;
  laufender Prozess mit derselben `SlotId` und Drift ≤ 30 s → `Unchanged`; sonst neuer
  `LiveStreamJob` + `StartAsync`. Bei einem Sendeplatz-Wechsel ist der Aufruf also
  `StartAsync` während der alte Prozess noch läuft (Stop→Start in einem Gate).
- `LiveStreamWorker.ExecuteAsync` leert das Segment-Verzeichnis einmalig beim Start
  (`ClearAsync`) — eine verwaiste `live.m3u8` aus einem früheren Serverlauf überlebt
  einen Neustart also nicht.

## `LiveStreamController`
Datei: `VideoWebPlayer/Controllers/LiveStreamController.cs`

- `GetFile` (Z. 46–83): `.m3u8` → `RewritePlaylistAsync` + `Content(...)`; `.ts` →
  `PhysicalFile`. Sonst 404.
- `RewritePlaylistAsync` (Z. 88–107): Zeilen, die mit `#` beginnen, bleiben unverändert —
  `#EXT-X-DISCONTINUITY`, `#EXT-X-PROGRAM-DATE-TIME` und `#EXT-X-MEDIA-SEQUENCE` aus der
  ffmpeg-Playlist werden also transparent durchgereicht; nur Segment-URI-Zeilen bekommen
  `?access_token=` angehängt.

## `LiveStreamSegmentStore`
Datei: `VideoWebPlayer/Services/LiveStreamSegmentStore.cs`

- Retention-Sweep löscht `seg_*.ts` nach Alter (Standard 30 min) — unabhängig von der
  Playlist-Generation. `delete_segments` in ffmpeg löscht aus dem Fenster gerutschte
  Einträge zusätzlich. Mit `append_list` bleiben Alt-Einträge nur innerhalb des
  12-Segment-Fensters gelistet — kein neuer Konflikt.

## `liveTv.js` (Player)
Datei: `VideoWebPlayer/wwwroot/js/liveTv.js`

- `attachHls` (Z. 45–62): `new Hls({ enableWorker: true })`, `ERROR`-Handler reagiert nur
  auf `data.fatal` → `notifyError` + Retry via `hls.startLoad()`. `FRAG_BUFFERED` meldet
  „playing" zurück. Gebündelte hls.js-Version: **1.7.3**.
- Lücke: bei `fatal` `MEDIA_ERROR` ist `startLoad()` wirkungslos — hls.js-Doku empfiehlt
  `recoverMediaError()`; bei `OTHER`/`INTERNAL` empfiehlt sich destroy+Neuaufbau.
