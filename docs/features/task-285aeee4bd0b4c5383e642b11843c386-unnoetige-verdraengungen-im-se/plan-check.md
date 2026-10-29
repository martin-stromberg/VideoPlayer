# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Zulässige Sendeblöcke werden nicht mehr fälschlich für ungültig erklärt (Block-Ebene: `GroupAirings`-Gruppe statt Einzelslot) | Erweiterungspunkt `ReconcileWindow.Validate` / `AiringReproducible` verifiziert (Z. 1128–1156 bzw. 607–648, 670–783, 795–993); Fix im Evidenzmodell bzw. Hypothesenraum je nach Reproduktionsbefund | Fail-first-Reproduktionstest `Reconcile_ShiftedAiring_ChainedInvaders_StaysScheduled`, Diagnose-Schritt `AiringInvalidReason`-Log, Fallback-Kandidatenliste | Abgedeckt |
| Keine Selbst-Verdrängungs-Duplikate mehr („durch sich selbst ersetzt" — gleicher Inhalt im selben Zeitintervall) | Akzeptanzkriterium konkretisiert (Inhaltsidentität `MovieId`/`TVShowEpisodeId`, Rohmaterial `MediaItemId`, selbes Intervall); Behebung der Fehlklassifikation + Auto-Bereinigung `CleanupSelfDisplacedSlotsAsync` | Hilfsassertion `AssertNoSelfDisplacedAsync` in `ProgramScheduleTestDb`; Einsatz im Reproduktions- und Worker-Loop-Test | Abgedeckt |
| Reconcile ohne Eingabeänderung = No-Op (`SlotsDisplaced == 0`, `SlotsCreated == 0`) | Bleibt übergeordnetes Kriterium des Fixes | `AssertReconcileNoOpAsync` existiert (`ProgramScheduleTestDb.cs:357`); Reproduktionstest + Folgelauf-No-Op + Negativtest `Reconcile_ShiftedAiring_EvidenceCleaned_ReplansOnce` | Abgedeckt |
| Angehäufte `Displaced`-Duplikate bereinigen (offene Frage der Anforderung → automatisch entschieden) | Neue Wartungsmethode `CleanupSelfDisplacedSlotsAsync` an `IProgramScheduleService`, Worker-Einhängung vor `ReconcileAsync` (`ProgramScheduleWorker.cs` Z. 74–76, verifiziert), strenges Zwillings-Kriterium | `CleanupSelfDisplaced_RemovesProvenDuplicates`, `_KeepsDisplacedWithoutTwin` (4 Gegenproben), `_RepresentativeSwitch_Removes`, `_PreservesInvaderEvidence_NoReplan`, `Worker_Run_CleansSelfDisplacedDuplicates` | Abgedeckt |
| Geltungsbereich: alle Slot-Arten (Titel, `IsClipFill`, Filler) | Designentscheidung im Plan; Clip-Vorfilter (Z. 609–645) und Filler-Einzelprüfung (Z. 286–298) im Code verifiziert — Filler bleiben unverändert, Clip-Chain-Gründe im Diagnose-Enum | Implizit über bestehende `Reconcile_ClipFillSlots_StayScheduled`, `Reconcile_ClipSourceFillerSlots_StayScheduled` (Regressionszeugen, volle Suite) | Abgedeckt |
| Wechselwirkungen berücksichtigen (`_invaderStarts`-Evidenz, Suffix-Verdrängung, `RewindCursor`, `DeleteDisplacedSlotsAsync`) | Evidenz-Erhalt der Teilmengen-Löschung konstruktionsbedingt begründet (Zwilling deckt Intervall vollständig ab — gilt unter Map- wie Intervall-Modell); Risiken benannt | `CleanupSelfDisplaced_PreservesInvaderEvidence_NoReplan` (Gegenprobe), Negativtest Evidenzverlust | Abgedeckt |
| Diagnose-Ausbau (offene Frage → minimaler Ausbau entschieden) | `internal enum AiringInvalidReason` + Diagnose-Senke im `ReproContext` + `LogInformation` je invalidem Block; `InternalsVisibleTo` verifiziert (`VideoWebPlayer.csproj:15`), `ListLogger<T>` existiert (`Helpers/ListLogger.cs`) | `Reconcile_InvalidAiring_LogsRejectionReason` | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — alle Akzeptanzkriterien haben konkrete Testnachweise; Negativ- und Gegenprobe-Tests sowie die Liste der bestehenden Regressionszeugen sind vollständig benannt (alle im Code verifiziert, u. a. `ProgramScheduleServiceTests_Boundary.cs:139`, `ProgramScheduleServiceTests_Reconcile.cs:606`, `ProgramScheduleWorkerTests.cs`).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Sendeplan ohne Selbst-Verdrängungs-Duplikate (anwendersichtbarer Effekt: keine „Verdrängt"-Häufung) | Kein neuer Test — bestehender E2E-Regressionszeug `TVSenderAdmin_RepeatGenerate_ProducesNoDisplacedSeries` (`TVSenderAdminE2ETests.cs:649`, verifiziert: zweimal „Jetzt generieren" → 0 `Displaced`) | Nicht erforderlich mit Begründung: kein neuer/geänderter Benutzerfluss; Verschiebungskonstellation und Bereinigung entstehen über Materialisierung und Worker-Takt, nicht über eine UI-Aktion. Begründung im Plan nachvollziehbar. |

## Fehlende oder unvollständige Planbestandteile

Keine — Umsetzungsreihenfolge, Voraussetzungen, Seiteneffekte/Risiken, DoD (Debug+Release-Build, volle Suite, Doku/Release Notes) sind vollständig.

## Hinweise

Die folgenden Befunde betreffen die im Plan als „vor Reproduktion zu verifizieren" markierte
Primärhypothese bzw. Details des Ziel-Fixes. Sie ändern nichts an der Abdeckung der Anforderung
(der Plan sieht für genau diesen Fall Diagnose- und Fallback-Schritte vor), sollten aber in die
Umsetzung einfließen:

- **Primärhypothese wie beschrieben nicht über den Materialisierungspfad herstellbar.**
  `ShiftPastInvaders` (Z. 1498–1514) verlangt **strikte innere** Überdeckung
  (`i.Start < startUtc && i.End > startUtc`, Z. 1506). Bei einer *lückenlos* anschließenden
  Kette (Invader A `[15:30,17:00)`, Invader B `[17:00,18:30)`, Occurrence-Start 16:00) landet
  der Fixpunkt auf `A.End` = `B.Start` und stoppt dort — `B.Start < startUtc` ist falsch.
  Der reale Start ist damit `A.End`, und genau dieser Fall ist heute über
  `_invaderStarts` (`End → min Start`, Z. 1110–1113; Beweis Z. 746–748 / 951–954:
  `invaderStarts[A.End] = A.Start < occStart`) **bereits beweisbar**. Eine Verschiebung über
  ≥2 Belegungen setzt **überlappende** Intervalle in `blocked` voraus — die entstehen im
  Materialisierungspfad nicht: `blocked` enthält nur nicht-überlappende `Scheduled`-Intervalle
  plus overlap-geprüfte neue Ranges (`MaterializeOccurrence`, Z. 1712;
  `MaterializeOccurrenceEvicting`, Z. 1212). Konsequenz: Der im Plan beschriebene Aufbau „über
  `CreateRuleAsync` + `EnsureCoverageAsync` (realer Materialisierungspfad)" wird die behauptete
  Konstellation voraussichtlich **nicht** erzeugen — die Zielausstrahlung startet bei `A.End`
  (beweisbar) oder verwirft/verdrängt den zweiten Invader. Zu erwarten ist also, dass der
  fail-first-Test im Primärszenario grün bleibt oder ein anderes Bild zeigt; der Plan trägt dem
  bereits Rechnung (Kandidatenliste, „bis eine echte Selbst-Verdrängung reproduziert ist").
  Soll die Ketten-Lücke dennoch als Codepfad-Lücke nachgewiesen werden, ließe sich die
  unbeweisbare Konstellation nur über direkt geseedete *überlappende* Belegungen
  (`CreateSlotAsync`) herstellen — mit der Einschränkung, dass ein solcher Zustand produktiv
  vermutlich nicht erreichbar ist und der Nachweiswert entsprechend einzuordnen wäre.
- **Geplante Ersatz-Bedingung `airingStart == ShiftPastInvaders(occurrenceStart, evidence)`
  ist nicht äquivalent zur Materialisierungssemantik.** Die Evidenzmenge
  (Scheduled ∪ DisplacedEvidence, Z. 1110–1113) kann — anders als `blocked` zum
  Materialisierungszeitpunkt — überlappende Intervalle enthalten: Eine `Displaced`-Zeile kann
  den realen Start einer verschobenen Ausstrahlung überdecken, obwohl sie zum
  Materialisierungszeitpunkt gar nicht `Scheduled` war. Der Fixpunkt über die aktuelle
  Evidenzmenge kann dann **über** den tatsächlichen Start hinausschießen → Gleichheit scheitert
  → legitimer verschobener Block wird invalid (heute beweisbar, da die Existenzbedingung
  `I.End == airingStart && I.Start < occStart` den einzelnen Beleger nur verlangt). Das würde
  exakt die Fehlklassifikation erzeugen, die die Anforderung beseitigen will — bei der
  Umsetzung des Fixpunkts darauf achten, dass Evidenz nur bis `airingStart` zählt bzw. eine
  Kette, die exakt am realen Start endet, bewiesen wird (nicht der volle Fixpunkt über die
  gewachsene Evidenzmenge).
- **Zwillings-Löschkriterium ist wasserdicht.** Verifiziert: identischer Kanal/`Start`/`End`/
  `ProgramRuleId`/`IsClipFill` plus Inhalts-Disjunktion (`MediaItemId` oder nicht-null
  `MovieId`/`TVShowEpisodeId`) trifft nur echte Selbst-Verdrängungs-Duplikate — ein
  Eviction-Verlierer hat per Definition anderen Inhalt oder eine fremde `ProgramRuleId`.
  `ProgramRuleId == null` auf beiden Seiten matcht in SQL nicht (`NULL = NULL` → EXISTS
  falsch) → konservative Unter-Löschung, sichere Richtung; ggf. als eigener Gegenproben-Fall
  im `KeepsDisplacedWithoutTwin`-Test mitaufnehmen (im Plan nicht explizit, aber abgedeckt
  durch „kein Zwilling").
- **Nebenbefund Evidenz-Verlust der Admin-Aktion:** `DeleteDisplacedSlotsAsync` (Z. 484–492)
  entfernt weiterhin die komplette Evidenz — dokumentierte Wechselwirkung, im Plan benannt und
  per Negativtest abgesichert. Keine Lücke.
- Alle im Plan zitierten Zeilenreferenzen (`_invaderStarts` ~Z. 1110, `ShiftPastInvaders`
  Z. 1498–1514, Tail-Checks, `Validate`, `ReproContext`, `DeleteDisplacedSlotsAsync`,
  `SlotMatchesItem`, Worker-Kette Z. 74–76) und Hilfsmittel (`ProgramScheduleTestDb`,
  `ListLogger<T>`, `InternalsVisibleTo`, einzige `IProgramScheduleService`-Implementierung,
  `Interval`-Record Z. 1423) wurden direkt im Code verifiziert und stimmen.
