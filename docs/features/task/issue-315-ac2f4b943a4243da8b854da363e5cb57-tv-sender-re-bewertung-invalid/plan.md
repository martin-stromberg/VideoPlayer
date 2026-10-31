# Umsetzungsplan — Issue #315

## Gefundene Ursache (bereits behoben, Commit auf dem Branch)

`InterleavedAiringTailReproducible` verwarf jede Strang-Hypothese, sobald ein Strang
keinen geschriebenen Beitrag im Rest der Ausstrahlungs-Gruppe hatte
(`countInAiring[s] == 0 → HeadPickInconsistent`). Ein Strang kann aber ungeschriebene
Prefix-Picks beigesteuert haben: Die Materialisierung committet genommene Picks, ohne
dafür Slots zu schreiben, wenn der Coverage-Lauf mitten in der Occurrence begann.
**Fix:** Scan-Head unbeobachteter Stränge wird aus dem persistierten Cursor
(`strands[s].NextIndex`) rückwärts rekonstruiert; leere Ringe bleiben ausgeschlossen.

Damit ist der Produktiv-Fall „unveränderte Regeln → HeadPickInconsistent →
Verdrängungs-Oszillation" abgedeckt (Test:
`Reconcile_UnchangedRules_RunningAiringWithUnwrittenStrandHead_StaysScheduled`).

## Verbleibende Arbeit

### A. Schutz des laufenden Slots + Gruppenteilung am Coverage-Rand

Problem: Wird eine Ausstrahlung aus echtem Grund invalidiert, verdrängt `Displace`
auch den Slot, der die Abspielposition trägt (Kriterium 3). Ein naiver Schutz
(laufenden Slot einfach behalten) erzeugt eine neue Oszillation: Die
Survivor-Gruppe `[alte Slots + laufender Slot]` verschmilzt mit dem neu
materialisierten Tail zu einer Gruppe, deren erste Sub-Ausstrahlung über das
Survivor-Ende hinaus Slots konsumieren will → permanente Invalidierung →
Verdrängen/Re-Materialisieren im Takt (in Inventur Block B bestätigt).

Lösung (zweiteilig — nach Plan-Check Befunde 1-4 korrigiert):
1. **Gruppenteilung am Coverage-Rand:** `GroupAirings` erhält einen optionalen
   `coverageBoundary`-Parameter; die lückenlose Kette bricht genau einmal — vor
   dem ersten Slot mit `End > boundary` (dessen Vorgänger `End <= boundary` hat).
   Damit ist der überspannende (laufende) Slot **Kopf der Zukunfts-Gruppe**:
   - Die Vergangenheits-Gruppe ist komplett ausgestrahlt → `Validate` überspringt
     sie (`!airing.Any(End > coverageStart)` → `continue`, Z.1297) → Kriterium 2
     und kein unbeweisbarer Präfix-Rest (der Check verträgt Tails, nicht
     abgeschnittene Prefixe).
   - Liegt die Grenze in einem laufenden **Clip** (Produktiv-Fall), bleibt der
     Clip gruppenintern → Clip-Zeit-Projektion und `ShiftProvable` intakt.
   - Die Zukunfts-Gruppe verankert über den überspannenden Kopf-Slot an ihrer
     Occurrence (Prefix-Rekonstruktion aus Teil A) und wird getrennt validiert.
   - `InsertClipFill` ruft `GroupAirings` weiterhin ohne Grenze (nur newSlots).
2. **Schutz des laufenden Slots an allen Verdrängungspfaden:** ein Slot mit
   `Start < coverageStart && End > coverageStart` bleibt stehen, es sei denn,
   sein Inhalt ist unter den aktuellen Altersfenster-Einstellungen an seinem
   eigenen Slot-Start unzulässig (`IsAllowedAt(minAge, slot.Start, ageRule)` =
   false → legitimer Kill). Nicht auflösbarer Inhalt (Clip-Slots ohne Rating)
   gilt als zulässig. Angewendet in `Displace`, `DisplaceRuleFuture` und dem
   Evict-Loser-Pfad (`MaterializeOccurrenceEvicting`): ein geschützter Slot kann
   nicht als Loser enden — die konkurrierende Occurrence fällt stattdessen bzw.
   wird hinter `slot.End` materialisiert (`ShiftPastInvaders` respektiert ihn
   bereits via `blocked`).

Tests:
- Laufender Slot bleibt `Scheduled`, wenn die Ausstrahlung aus echtem Grund
  invalidiert wird (z. B. Regel deaktiviert) — auch über Folge-Reconciles.
- Laufender Slot wird `Displaced`, wenn der Titel an seinem Slot-Start gegen das
  aktuelle Altersfenster verstößt (Einstellung verschärft).
- Kein Churn: Nach Deaktivierung+Reaktivierung mitten in der Ausstrahlung bleiben
  Folge-Reconciles ruhig und bereits-ausgestrahlte Slots unverändert.
- Grenze mitten im laufenden Clip-Slot: Tail validiert (kein
  `AnchorWithoutInvaderEvidence`).

### B. HLS/FFmpeg-Strecke bei erzwungenem Slot-Wechsel (Kriterium 4)

Befund Inventur Block A — Umsetzung (nach Plan-Check Befunde 7-8 korrigiert):
1. **ENDLIST-Leiche an beiden Rändern beseitigen:** die Zeile `#EXT-X-ENDLIST`
   wird aus `live.m3u8` entfernt (a) beim planmäßigen ffmpeg-Exit
   (`ReportProcessExit`-Expected-Zweig) und (b) erneut beim Job-Start, bevor
   ffmpeg mit `append_list` anhängt. Erst dann ist das Fenster
   [Exit → nächster Job-Start → erstes Segment] geschlossen; hls.js sieht nie
   eine beendete Playlist. Rewrite unter `_gate` mit FileShare-freundlichem
   Zugriff (Controller liest parallel). Kontinuität bleibt erhalten.
2. **Eigenes Wake-Signal für den LiveStreamWorker:** `IProgramScheduleSignal` ist
   ein Bounded(1)-SingleReader-Channel, an dem bereits `ProgramScheduleWorker`
   **und** `LiveStreamWorker` warten — racy, und ein selbst gefeuertes Signal
   käme als Dauerschleife zurück. Ergänzung eines dedizierten Live-Kanals
   (z. B. `NotifyLiveScheduleChanged`/`WaitLiveChangedAsync` am selben Dienst
   oder eigener Signal-Dienst), den nur der LiveStreamWorker konsumiert;
   `ProgramScheduleWorker` feuert ihn nach Reconcile/Coverage bei
   tatsächlichen Änderungen (`SlotsDisplaced > 0 || SlotsCreated > 0`).
3. **Residual (dokumentiert, nicht geändert):** `-re` + Input-`-ss` kann bei
   nicht-suchbaren Containern den Seek-Discard auf Echtzeit drosseln —
   ggf. Folgeissue; `ExpectedPositionSeconds` ist Wanduhr-Projektion, der
   Drift-Wächter sieht den hängenden Seek nicht.

Tests:
- Playlist bleibt über Exit→Restart live: Playlist mit ENDLIST + Job-Start →
  `live.m3u8` enthält keine ENDLIST-Zeile mehr (Unit-/Store-Ebene).
- Live-Wake-Signal wird nach Reconciliation gefeuert und nur vom
  LiveStreamWorker konsumiert (Worker-Test).

### C. DoD-Nachweis

- `dotnet build` Debug + Release, volle Testsuite grün.
- `docs/help/` + Release Notes: Verhalten „unveränderte Ausstrahlung bleibt stabil",
  laufender Slot läuft zu Ende, Gruppenteilung am Coverage-Rand, Playlist-ENDLIST.

## Offene Punkte

- Keine — Inventur abgeschlossen; Restrisiko: `-re`/`-ss`-Seek-Latenz (s. Teil B.3).
