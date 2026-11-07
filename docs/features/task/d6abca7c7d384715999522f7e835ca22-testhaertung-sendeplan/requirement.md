# Übersetzte Anforderung: Sender-Testhärtung — nicht sichergestellte Erwartungen

Quelle: `issue.md` (GitHub-Issue #344), Aufgaben-ID `d6abca7c-7d38-4715-9995-22f7e835ca22`.

## Fachliche Zusammenfassung

Die Testsuite des automatischen TV-Senders (`ProgramScheduleService`, `ProgramScheduleWorker`,
`ProgramScheduleTestDb`) enthält Assertions, die den Mechanismus prüfen statt der fachlichen
Invariante — sie bleiben bei einem Regressionsbruch grün (gleiche Fehlerklasse wie der
`leads[i] != leads[i-1]`-Fall aus Issue #343). Die in der Korrekturanforderung gelisteten
Blindstellen B1–B15 sind durch verstärkte Assertions zu schließen. Schlägt eine verstärkte
Assertion gegen den Ist-Code fehl, liegt ein Produktiv-Bug vor; dieser ist ebenfalls zu
beheben (separater Commit, im Bericht benannt).

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein Sendeplan-Lauf des `ProgramScheduleService` bzw. eine
  Iteration des `ProgramScheduleWorker` — jeweils als Testfall in `VideoWebPlayer.Tests`
  gegen echtes SQLite (`ProgramScheduleTestDb`) bzw. Fakes.
- **Auslösende Ereignisse:** Keine Laufzeit-Ereignisse — Auslöser ist die Ausführung der
  Testsuite (`dotnet test`). Fachlich bildet die Suite Generierung, Reconcile und
  Nachführung des Sendeplans ab.
- **Erwartete Wirkung:** Jede verstärkte Assertion schlägt bei einem Bruch der benannten
  fachlichen Invariante fehl (z. B. überlappende Sendeplätze, Ring-Fortsetzung falsch,
  Reconcile-Nebeneffekte). Nachweisbar: künstlich gebrochener Code lässt den Test rot
  werden; unveränderter korrekter Code bleibt grün.
- **Umgang mit fehlenden Objekten:** Keine Laufzeit-Objekte betroffen. Für B14 sind
  Fake-Implementierungen von Signal, Settings und `IBackgroundProcessingGate`
  bereitzustellen, falls noch nicht vorhanden.
- **Scope-Ausschlüsse:**
  1. `PeriodStart == null`-Pfad (`ProgramScheduleService.cs:3114-3126`) und
     Concurrent-Reschedule-Races — nur dokumentiert, optional Folgeaufgabe (vorgegeben
     durch die Anforderung als Nicht-Ziel).
  2. E2E-/Playwright-Blindflecken (`TVSenderAdmin*Tests`, `ProgramGuideE2ETests`,
     `ScheduleDisplayItemsTests`, `ProgramScheduleSignalTests`,
     `ProgramGuideServiceTests_ClipGroups`, `VideoWebPlayerBackupDataTests_*`) — nicht
     analysiert, ggf. separater Lauf (vorgegeben durch die Anforderung als Nicht-Ziel).
  3. Spröde Gegen-Assertions (`ClipGroups.cs:232-237`, `PrimetimeGrace.cs:138-144`,
     strikte A/B-Alternation) — nur bewerten; ggf. lockern mit Kommentar (vorgegeben
     durch die Anforderung).

## Betroffene Klassen und Komponenten

- Test-Helper: `ProgramScheduleTestDb` (`AssertGapless`, `AssertReconcileNoOpAsync`,
  ggf. neue Hilfsmethoden)
- Testklassen `VideoWebPlayer.Tests/ProgramScheduleServiceTests_*`: `Reconcile`,
  `RingOrder`, `CollectionOrder`, `Interleaving`, `Trailer`, `ContentScope`, `Filler`,
  `Generation`, `ClipGroups`, `ClipSource`, `StrandRotation`, `AgeWindow`, `PrimetimeGrace`
- Produktivcode (nur bei Rot-Nachweis): `VideoWebPlayer` — `ProgramScheduleService`,
  `ProgramScheduleWorker`
- Neue Worker-Tests mit Fakes: `NotifyLiveScheduleChanged`-Signal, Settings-Neulesung,
  `IBackgroundProcessingGate`-Lease (B14)

## Implementierungsansatz

- B1 zuerst: `AssertGapless` auf strikte Lücken- **und** Überlappungsprüfung verschärfen;
  alle Aufrufer bleiben kompatibel. Aufgedeckte echte Überlappungen sind Produktiv-Bugs
  und werden separat behoben.
- Danach die Einzelbefunde B2–B15 je Teststelle: Assertion an der fachlichen Invariante
  ausrichten (Sequenz-, Mengen-, Zeit- und Nebeneffekt-Vergleiche statt Existenz-/
  Mechanismus-Checks). Jede Verstärkung wird gegen den Ist-Code verifiziert.
- B14: Worker-Verhalten mit Fakes absichern; falls der Aufwand den Schritt sprengt, als
  dokumentierten Nicht-Befund/`continue.md`-Punkt mit Begründung behandeln — nicht still
  weglassen.
- Testklassen-Konvention `test-class-structure` beachten (thematisch kleine Klassen,
  `{Klasse}Tests_{Thema}`).

## Konfiguration

Keine — reine Testhärtung, keine neue Laufzeit-Konfiguration.

## Offene Fragen

Keine — die Korrekturanforderung in `issue.md` ist vollständig spezifiziert
(Datei:Zeile plus vorgeschlagene Assertion je Befund). Eventuell bei der Umsetzung
auftretende Unklarheiten werden als begründete Annahmen in `plan.md` dokumentiert.
