# Bestandsaufnahme: Sender-Testhärtung — nicht sichergestellte Erwartungen

Analyse der Sendeplan-Testsuite (`VideoWebPlayer.Tests`) und der Worker-Nebeneffekte
(`ProgramScheduleWorker`) bezogen auf die Befundliste B1–B15 aus `issue.md` (GitHub-Issue #344).

## Zusammenfassung

- Zentraler Helper `ProgramScheduleTestDb` bietet `AssertGapless` (nur Lücken-, keine
  Überlappungsprüfung), `AssertNoDisplacedAsync` und `AssertReconcileNoOpAsync` — die
  strengeren Varianten existieren bereits, werden aber an den in B1/B8 genannten Stellen
  nicht verwendet.
- Alle in B2–B13, B15 genannten Teststellen existieren; die Zeilenangaben aus `issue.md`
  sind teilweise verdriftet — die tatsächlichen Fundstellen stehen in
  [findings.md](inventory/findings.md).
- **B9 ist auf diesem Branch nicht anwendbar:** der genannte Test
  `EnsureCoverage_SecondRun_ContinuesDisjointAcrossRunBoundary` mit der Schleife
  `if (served.Count == 0) break` existiert nur auf dem noch nicht gemergten
  Fix-Branch `task/d6abca7c7d384715999522f7e835ca22-strang-ueberlappung` (Issue #343).
  Dort liegt die Funktion bei Zeile ~284–310, `EpisodeCountBelowStrandCount_StillRotates`
  bei ~313–332 — genau die Zeilen aus `issue.md`. B4 (`Distinct >= 2` → alle 3 Stränge)
  betrifft dagegen eine Funktion, die hier existiert und gehärtet werden kann.
- Worker-Nebeneffekte (B14): `ProgramScheduleWorker` löst `IProgramScheduleService` über
  das Interface auf (fakebar), `ProgramSettingsService` konkret (echtes SQLite nutzbar),
  `IBackgroundProcessingGate` und `IProgramScheduleSignal` ebenfalls über Interfaces
  (fakebar). Es gibt bereits `ProgramScheduleWorkerTests` mit echtem SQLite +
  ServiceCollection-Aufbau — die neuen Tests können diesem Muster folgen.
- Test-Ausgangszustand: siehe [tests.md](inventory/tests.md) (Basis-Lauf vor Änderungen).

## Details

- [Befund-Mapping B1–B15 mit Ist-Zeilen](inventory/findings.md)
- [Test-Helper `ProgramScheduleTestDb`](inventory/testhelper.md)
- [Worker und Nebeneffekte](inventory/worker.md)
- [Testklassen und Ausgangszustand](inventory/tests.md)
