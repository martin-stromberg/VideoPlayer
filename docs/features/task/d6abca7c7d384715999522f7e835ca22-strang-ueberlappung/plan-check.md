# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium (aus `requirement.md`/`issue.md`) | Umsetzung im Plan | Testnachweis im Plan | Status |
|------------------------------------------------------|-------------------|----------------------|--------|
| Bedient-Mengen aufeinanderfolgender Ausstrahlungen disjunkt, solange `strandCount > picksPerAiring` | Zeiger = Position hinter letztem bedientem Strang, abgeleitet aus letztem Titel-Slot der Regel vor `airingStartUtc` (Schritt „Programmabläufe") | `EnsureCoverage_ManyCollectionStrands_ConsecutiveAiringsServeDisjointStrands` (Pflichttest, echtes SQLite, rot auf Altstand) | Abgedeckt |
| Rotationszeiger berücksichtigt tatsächlich bediente Strangzahl (fortlaufend statt starr +1/Tag) | Zeiger folgt der tatsächlich geplanten Slot-Folge — die bediente Strangzahl je Ausstrahlung geht automatisch ein | Beide neuen Tests | Abgedeckt |
| Ordnung innerhalb geordneter Stränge bewahrt | Kein Eingriff in Strangringe/`PreservesOrder`/Ring-Commit — nur Startindex des Walks ändert sich | `..._ReleaseOrderAndCursorsPreserved` (Bestand) | Abgedeckt |
| `ProgramRuleCursors`-Fortschreibung korrekt | `CommitTaken`/`CommitScan`/`PersistCursors` unverändert | `..._ReleaseOrderAndCursorsPreserved` (Bestand) | Abgedeckt |
| `StrandSequenceBroken`-Validierung unangetastet | `SlotStillValid` unverändert; `FindItemIndex`-Überladung ist reine Extraktion | `Reconcile_RotatedPlan_StaysScheduledNoFalseDisplacement` (Bestand) | Abgedeckt |
| Stabilität bei all-served-Fenstern | Probelauf-Mechanismus unverändert (Offset 0 gewinnt, wenn alle Stränge bedient) | `..._AllStrandsServed_NoRotation_StableStructure` (Bestand) | Abgedeckt |
| `EpisodeCount`-Rotation unverändert | `wanted < m`-Pfad bleibt bei `StrandStartOffset` | `..._EpisodeCountBelowStrandCount_StillRotates` (Bestand) | Abgedeckt |
| Fortsetzung über Planlauf-/Prozessgrenzen | Ableitung aus `persistedOccupancy`/`keptSlots` + `newSlots` — ground truth statt gespeichertem Zeiger | `EnsureCoverage_SecondRun_ContinuesDisjoint` | Abgedeckt |
| Erwartbares Ergebnis: abwechselnde Sammlungen im Sendeplan | Disjunkte Bedient-Mengen über Zyklus ⇒ jeder Strang reihum | Pflichttest + realer Verzicht dokumentiert | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

— (Status `Plan vollständig`)

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| — | — | Nicht erforderlich: Die Anforderung betrifft ein internes Planungsverhalten ohne Benutzerinteraktion und ohne UI-Änderung; der Funktionsnachweis erfolgt über SQLite-Integrationstests, die den produktiven `EnsureCoverage`-/`Reconcile`-Pfad durchlaufen. |

## Fehlende oder unvollständige Planbestandteile

— (Status `Plan vollständig`)

## Hinweise

- Die Umsetzungsreihenfolge setzt den Rot-Nachweis der neuen Tests ausdrücklich voraus (Test-First gemäß `/fix-with-test`): Beide neuen Tests müssen vor der Codeänderung auf dem Ist-Stand fehlschlagen.
- Der planmäßige Verzicht auf neue persistierte Zeiger-Daten entfällt die AGENTS-§7-Pflichten (EF-Migration, Backup-Registrierung) — bewusste Designentscheidung, im Plan begründet.
- Risiko-Restpunkt (in „Seiteneffekte" benannt): Re-Materialisierungen/Lookback-Replays erzeugen Zeiger-Kandidaten aus eigenen persistierten Slots — das Kollisionsverhalten ist dem bisherigen Tages-Offset äquivalent (atomare Konfliktprüfung verwirft Replay-Belegungen).
