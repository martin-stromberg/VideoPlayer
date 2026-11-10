# Tasks: Anwendung neuer Regeln auf den bestehenden Sendeplan (Issue #351)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Testinfrastruktur | `ProgramScheduleTestDb.CreateMultiSeasonShowAsync` anlegen (3–8 Staffeln, 10–20 Episoden je Staffel, leicht variierende Laufzeiten, gelegentlich doppelte Länge, deterministisch) | Offen | — |
| 2 | Testinfrastruktur | `ProgramScheduleTestDb.CreateCollectionAsync` anlegen (MovieCollection + Filme in Releasedatum-Reihenfolge); `SeedCollectionAsync` in `ProgramScheduleServiceTests_PriorityTakeover` delegiert darauf | Offen | — |
| 3 | Testinfrastruktur | `ProgramScheduleTestDb.SeedRealisticLibraryAsync` anlegen (mehrere Mehrstaffel-Serien, mehrere Sammlungen, Einzelfilme) | Offen | — |
| 4 | Logik | `Evicts` in `ReconcileAsync` erweitern: expliziter Gleichregel-Schutz (`loser.Id == winner.Id` → `false`); bei `winner.Priority == loser.Priority` gewinnt die Occurrence, wenn `cursors` für `winner.Id` keinen Eintrag hat, sonst `winner.Id < loser.Id` | Offen | — |
| 5 | Logik | Occurrence-Schleife in `ReconcileAsync` auf `Priority desc → StartUtc → Rule.Id` umordnen | Offen | — |
| 6 | Logik | `DisplaceRuleFuture` auf slot-genaue Verdrängung (`slot.Start >= fromStartUtc` innerhalb der Regel-Airings) generalisieren; Aufruf mit `loser.Start` statt `airing[0].Start` (Präfix-Erhalt) | Offen | — |
| 7 | Tests | Testklasse `ProgramScheduleServiceTests_NewRules` anlegen: `Reconcile_NewRuleFreeWindow_MaterializesInAlreadyPlannedDays` | Offen | — |
| 8 | Tests | `Reconcile_NewRuleTailOverlap_ShiftsPrimetimeBlock` | Offen | — |
| 9 | Tests | `Reconcile_NewRuleOverlap_ShiftedBlockTailDefers` | Offen | — |
| 10 | Tests | `Reconcile_DroppedCollectionFilm_SequelsDisplaced_PredecessorsRefill` | Offen | — |
| 11 | Tests | `Reconcile_NewRuleLaterStart_PreservesAiringPrefix` | Offen | — |
| 12 | Tests | `Reconcile_NewRuleIsIdempotent_SecondRunNoOp` | Offen | — |
| 13 | Tests | `Reconcile_ReactivatedRule_MaterializesInAlreadyPlannedDays` | Offen | — |
| 14 | Tests | `Reconcile_NewRuleLowerPriority_DoesNotShiftHigherPriorityAiring` | Offen | — |
| 15 | Tests | `Reconcile_EqualPriorityEstablishedRules_DeterministicNoOscillation` | Offen | — |
| 16 | Tests | `Reconcile_NewRuleOverlap_RunningSlotStaysProtected` | Offen | — |
| 17 | Tests | `Reconcile_NewRuleRealisticLibrary_FullScenario` (mehrere `ExecutePassAsync`-Iterationen, lückenlos, geordnete Stränge, keine Duplikate) | Offen | — |
| 18 | Tests | Bestehende Suite fahren (`Category!=E2E`), Abweichungen in `*_Boundary`, `*_Reconcile`, `*_PriorityTakeover`, `*_CollectionOrder`, `*_Stability`, `*_StrandRotation` u. a. einzeln bewerten und Erwartungen anpassen (kein Deaktivieren/Abschwächen) | Offen | — |
| 19 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md`: Frisch-Regel-Gewinnersemantik, chronologische Occurrence-Iteration, slot-genaue Verdrängung und Sammlungs-Kaskade im Abschnitt Neu-Bewertung dokumentieren | Offen | — |
| 20 | Dokumentation | `docs/RELEASE_NOTES.md`: Eintrag für das neue Verhalten; `README.md` bei Bedarf | Offen | — |
| 21 | Abschluss | Debug- **und** Release-Build fehlerfrei; volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün; reale Abnahme (Akzeptanzszenario 1) durchführen oder dokumentiert verzichten/eskalieren | Offen | — |
