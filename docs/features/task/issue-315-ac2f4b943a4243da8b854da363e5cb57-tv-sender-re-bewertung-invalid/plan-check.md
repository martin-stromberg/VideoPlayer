# Plan-Check — Issue #315

## Ergebnis

**Status:** Plan vollständig (nach Nachbesserung)

Der Plan-Check (Unteragent, Kreuzverhör gegen den Code) fand vier echte Lücken;
der Plan wurde entsprechend korrigiert:

## Befunde und Auflösung

1. **Vergangenheits-Gruppe nach Split prinzipiell invalid** (`consumed <= rest` kann
   ein abgeschnittenes Präfix nie erfüllen) → **aufgelöst:** Split bei
   `End <= coverageStart`; überspannender Slot wird Kopf der *Zukunfts*-Gruppe,
   die Vergangenheits-Gruppe ist komplett ausgestrahlt und wird natürlich
   übersprungen.
2. **Suffix-Verdrängung zieht die Tail-Gruppe mit** → **aufgelöst:** Verifiziert im
   Code (ProgramScheduleService.cs:1294-1298) — übersprungene Gruppen setzen
   `firstInvalid` nie; die Zukunfts-Gruppe validiert eigenständig.
3. **Seitenwahl bricht den Clip-Kopf-Fall** (`AnchorWithoutInvaderEvidence` durch
   gruppenexterne Clip-Zeit) → **aufgelöst** durch dieselbe Seitenwahl wie 1.
4. **Schutz nur in `Displace` umgehbar** (`DisplaceRuleFuture`, Evict-Loser) →
   **aufgelöst:** Guard an allen drei Verdrängungspfaden.
5. **ENDLIST-Fenster [Exit→Job-Start] bleibt offen** → **aufgelöst:** Strip beim
   planmäßigen ffmpeg-Exit UND beim Job-Start.
6. **Wake-Signal racy** (Single-Reader-Channel, zwei Warter, Selbst-Verzehr) →
   **aufgelöst:** dedizierter Live-Kanal.

## Hinweise

- Offener Punkt bewusst dokumentiert: `-re`/`-ss`-Seek-Latenz (Folgeissue-Kandidat);
  Evict-vs.-Running-Slot-Verhalten („Occurrence fällt statt laufender Slot").
- Bestandstests mit geändertem Erwartungsbild (laufender Slot bleibt):
  `Reconcile_LongAiring_*`, `Reconcile_WorkerLoop_*`, weitere laut Bericht —
  bei Implementierung einzeln prüfen und begründet anpassen.
