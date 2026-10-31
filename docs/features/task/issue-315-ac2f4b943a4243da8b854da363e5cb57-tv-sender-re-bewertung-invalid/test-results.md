# Testergebnisse — Issue #315

## Umgesetzte Änderungen (Commits auf dem Task-Branch)

1. Kern-Ursache: `InterleavedAiringTailReproducible` verwarf Hypothesen für Stränge
   ohne geschriebenen Beitrag (`countInAiring == 0` → `HeadPickInconsistent`), obwohl
   Stränge ungeschriebene Prefix-Picks beigesteuert haben können. Fix: Scan-Head wird
   aus dem persistierten Cursor (`NextIndex`) rekonstruiert; leere Ringe bleiben
   ausgeschlossen.
2. `GroupAirings` teilt am Coverage-Rand (`End <= coverageStart` = eigene
   Historie-Gruppe, wird von `Validate` übersprungen); der laufende Slot koepft die
   Zukunftsgruppe.
3. Laufender Slot (`Start < coverageStart < End`) bleibt an allen
   Verdrängungspfaden geschützt, sofern sein Inhalt an seinem Slot-Start unter den
   aktuellen Altersfenster-Regeln zulässig ist.
4. `ProgramScheduleSignal`: dedizierter Live-Kanal (`NotifyLiveScheduleChanged`/
   `WaitLiveAsync`); `NotifyScheduleChanged` weckt beide Kanäle. ScheduleWorker
   meldet materialisierte Änderungen an den LiveStreamWorker.
5. `FfmpegLiveStreamSegmenter`: `#EXT-X-ENDLIST` wird beim planmässigen ffmpeg-Exit
   und beim Job-Start aus `live.m3u8` entfernt (Dauer-Ladekreis nach Slot-Wechsel).

## Testlauf

- `dotnet test --filter "ProgramSchedule|LiveStream|Issue315"`: **415/415 grün**
  (37 s), Debug-Build sauber.
- Neue Regressionstests (`ProgramScheduleServiceTests_Issue315.cs`):
  - `Reconcile_UnchangedRules_RunningAiringWithUnwrittenStrandHead_StaysScheduled`
    (failt ohne Fix; mit Fix grün, zweite Re-Bewertung ebenfalls ruhig)
  - `Reconcile_AiredHistoryUntouched_MergedTailStaysStable` (Historie bit-identisch,
    kein Churn nach Deaktivierung/Reaktivierung)
  - `Reconcile_InvalidAiring_RunningSlotRunsToItsEnd`
  - `Reconcile_RunningSlot_AgeWindowBlocksAtOwnStart_IsDisplaced`
- Angehobene Bestandstests: `Reconcile_LongAiring_ChangedRule_StillDisplaced`,
  `Reconcile_LongAiring_BeyondMaxLookback_Displaced`,
  `PurgeExpiredRules_SlotsDisplacedAfterReconcile` (laufender Slot läuft zu Ende).

## Review-Nacharbeit (siehe review-code.md)

- Ghost-Read-Hunger im Wait-Muster beider Worker behoben (Signal-Task über
  Iterationen wiederverwendet) — der neue Live-Kanal wäre sonst im Steady State
  wirkungslos geblieben.
- `SlotMinAge`: Clip-Füller liefern `null` (Trailer-Clips tragen die MovieId des
  beworbenen Films und dürfen dessen FSK nicht erben).
- NextIndex-Rekonstruktionslimitation im Kommentar + „Grenzen"-Abschnitt
  dokumentiert.
- Nach-Nacharbeit: 503/503 betroffene Tests grün (ProgramSchedule + LiveStream +
  Issue315 + MediaDurationProbe).

## Vollständige Suite (Release)

- `dotnet build VideoPlayer.sln -c Release`: 0 Fehler (140 vorhandene Warnungen).
- `dotnet test -c Release`: 2828/2831 grün, 2 übersprungen.
- Einziger Ausfall: `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule`,
  zweimal reproduziert — **zeitabhängiger Pre-Existing-Flake**: der Test seedet
  einen verdrängten Slot direkt (UtcNow+3h) und wählt nur den Tag des ersten
  Seed-Slots (UtcNow+2h) in der Tagesleiste; ab ~21:00 Ortszeit liegt +3h am
  Folgetag → `LocatorWaitFor` läuft in den 15-s-Timeout. Der Render-Pfad wird von
  diesem Change nicht berührt. Commit `c30cbba` hatte die Tagwahl schon für diesen
  Mechanismus ergänzt — sie deckt den zweiten Seed-Tag nicht ab.

## Noch offen (DoD)

- Feature-Verzeichnis-Aufräumen zum Abschluss, PR-Beschreibung aktualisieren
- Residual: `-re`+`-ss`-Seek-Latenz bei nicht-suchbaren Containern (Folgeissue)
