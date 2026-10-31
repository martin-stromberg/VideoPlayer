# Code-Review — Issue #315

Unabhängiges Review (frischer Unteragent, read-only) über die geänderten Dateien:
ProgramScheduleService.cs, ProgramScheduleSignal.cs, ProgramScheduleWorker.cs,
LiveStreamWorker.cs, FfmpegLiveStreamSegmenter.cs + Tests.

## Ergebnis

**Status:** Befunde vorhanden → alle drei behoben und verifiziert (503/503 Tests grün).

## Befunde und Behebung

1. **Hoch — Ghost-Read-Hunger im Wait-Muster (LiveStreamWorker/ProgramScheduleWorker):**
   `WaitAsync().AsTask()` pro Schleifeniteration hinterließ bei verlorenem `WhenAny`
   einen hängenden Channel-Read; blockierte Reader werden FIFO bedient → jede
   Delay-Iteration wuchs die Ghost-Queue und verschluckte spätere Signale — der neue
   dedizierte Live-Kanal wäre im Steady State wirkungslos gewesen.
   **Behebung:** `signalTask` über Iterationen wiederverwendet (`??=`, nach Verbrauch
   auf `null`), Muster aus PlaylistBackfillWorker.cs übernommen. Gilt für beide
   Worker — der Fehler war strukturell älter, ist hier mit behoben.

2. **Mittel — NextIndex als Rekonstruktionsbasis (InterleavedAiringTailReproducible):**
   Der persistierte Cursor markiert den Strang-Letztzugriff des ganzen Plans (Ende),
   nicht den Occurrence-lokalen Prefix-Stand; ein Strang, der in einer späteren
   nicht-kontigüren Gruppe beitrug, erzeugt Hypothesen von der falschen Position
   (konservativ: unnötige Verdrängung statt falscher Stabilität).
   **Behebung:** Limitation im Kommentar dokumentiert; die gewählte Richtung bleibt
   sicher (Protected-Guard hält den laufenden Titel zusätzlich). Fachliche
   Vertiefung (p_s=0-Hypothese „Strang deferriert") als mögliche Folgearbeit notiert.

3. **Niedrig — Trailer-Clip erbt FSK des beworbenen Films (SlotMinAge):**
   `FindItemIndex` fand Trailer-Clip-Slots über den MovieId-Fallback → laufender
   Trailer hätte die FSK des Zielfilms geerbt (mid-air-Kill oder Falsch-Schutz).
   **Behebung:** `slot.IsClipFill → null` in SlotMinAge; Clips unterliegen dem
   Unknown-Rating-Modus.

## Hinweise des Reviews (verifiziert, keine weiteren Befunde)

- Boundary-Logik `GroupAirings` korrekt: genau ein Bruch vor dem ersten Slot mit
  `End > boundary`; `End == boundary` bleibt Historie; Aufrufer liefern sortiert.
- Alle vier `Status = Displaced`-Schreibstellen hinter Guards; der
  „Tail-Invader"-Pfad ist defensiver Dead-Code (kein Überschreib-Pfad gefunden).
- Veto-Drop ist atomar: geschützter Slot mitten in einer Occurrence verwirft deren
  ganze Nachmaterialisierung — das Fenster geht an Filler (gewollte Degradation).
- `StripEndlistFromPlaylist`: FileShare konsistent zum Controller-Leser; ffmpeg an
  beiden Call-Sites tot/noch nicht gestartet. Restrisiko: externer Reader ohne
  Write-Share lässt den Strip scheitern (Debug-Log, gebundenes Fenster).
- Signal: `EndScan` schreibt beide Kanäle; Suppression-Race geschlossen;
  `NotifyLiveScheduleChanged` bewusst unsuppressed; einzige produktive
  Implementierung `ProgramScheduleSignal` (DI-Singleton).
