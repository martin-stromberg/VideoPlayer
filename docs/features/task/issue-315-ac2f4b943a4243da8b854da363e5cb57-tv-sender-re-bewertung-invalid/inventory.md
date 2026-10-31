# Bestandsaufnahme — Issue #315

Kurzfassung der Inventur (Unteragent, Details in der Session-Aufzeichnung).

## Block A — HLS/Live-Stream bei Slot-Wechsel

- `LiveStreamCoordinator.SynchronizeStreamAsync` (LiveStreamCoordinator.cs:154-175): startet bei
  anderem `SlotId` oder Drift > 30 s einen neuen `LiveStreamJob`; ffmpeg hart neu (`StopCoreAsync`,
  FfmpegLiveStreamSegmenter.cs:224-262) mit `-ss {PositionSeconds}` und `-t {RemainingSeconds}`.
- `BuildArguments` (Z.443-460): `-f hls`, `append_list`, `discont_start`, `program_date_time`,
  Segmentnamen `seg_{ts}_{slotId}_%05d.ts` — Kontinuität ist gewollt (E2E-Test
  `SecondJob_PlaylistContinuesAndMarksDiscontinuity`).
- `LiveStreamController.InjectDiscontinuity` (Z.178-195): DISCONTINUITY nur beim
  Standby→Echt-Übergang, prozessweit einmalig (StandbyProvider.cs:190).

### Verdächtige Hänger (Produktiv-Symptom: ffmpeg läuft fehlerfrei mit `-ss 104.942`, nur Ladekreis)

1. **ENDLIST-Leiche (Hauptverdacht):** Bei planmäßigem ffmpeg-Ende schreibt der Muxer
   `#EXT-X-ENDLIST` in `live.m3u8`. Beim nächsten Job liest `append_list` die alte Playlist;
   bis zum ersten neuen Segment liegt die Datei **mit ENDLIST** auf Disk → hls.js liest eine
   beendete (VOD-)Playlist und hört auf zu pollen → permanenter Ladekreis. Ob ffmpeg das alte
   ENDLIST zuverlässig entfernt, ist nicht getestet.
2. **`-re` drosselt den Seek-Discard auf Echtzeit:** `-re -ss {pos} -i {http}` — kann der
   Demuxer Zeitstempel nicht auf Byte-Offsets abbilden (MKV ohne Cues), scannt ffmpeg ab Byte 0
   in Echtzeit; Accurate-Seek decodiert+verwirft vom Keyframe bis Ziel, ebenfalls `-re`-gedrosselt.
   `ExpectedPositionSeconds` (Z.157-159) ist Wanduhr-Projektion → der Drift-Wächter sieht den
   hängenden Seek nie → kein Restart, kein Warn-Log.
3. **Reconcile weckt den Live-Worker nicht:** `ProgramScheduleWorker` ruft nach
   Reconcile/EnsureCoverage kein `IProgramScheduleSignal.NotifyScheduleChanged` — der
   LiveStreamWorker merkt den ersetzten Slot erst am nächsten Tick (≤ 30 s).
4. Nebenbefunde: SSH-Handshake je ffmpeg-Range-Request (SftpMediaSourceReader.cs:272-302);
   Retention-Sweep (LiveStreamSegmentStore.cs:106-131) kann referenzierte Segmente löschen;
   Standby-Transition-Flag ist prozessweit, nicht pro Client.

### Testlücke (Block A)

- Kein Test für ENDLIST-Übergang, kein HTTP+`-ss`-E2E, kein „Playlist bleibt live über
  Job-Restart". Vorhanden: `LiveStreamCoordinatorTests` (FakeSegmenter, Slot-Wechsel-Restarts),
  `FfmpegLiveStreamSegmenterE2ETests` (Datei-Eingang, PositionSeconds 0, Discontinuity).

## Block B — Reconciliation/Gruppierung

- `GroupAirings` (ProgramScheduleService.cs:574-590): lückenlose Läufe gleicher Regel —
  **keine Teilung an `coverageStart`**. Aufrufe: `ReconcileWindow.Rebuild` (alle Scheduled),
  `InsertClipFill` (nur newSlots).
- `Displace` (Z.264-270): `End <= coverageStart` bleibt (History); der überspannende Slot wird
  verdrängt — Schutz fehlt.
- Oszillationspfade der gemergten Survivor+Tail-Gruppe: Strand-Kontiguität
  (`StrandSequenceBroken`), Prefix-Rekonstruktion gegen zurückgespulter `NextIndex`
  (`HeadPickInconsistent`), `AnchorWithoutInvaderEvidence`, `OutsideOccurrenceWindow`/
  `SlotSequenceMismatch`, Clip-Geometrie (`ClipChainBroken`).
- Testlücke: kein Test deaktiviert+reaktiviert eine Regel mitten in der Ausstrahlung.
