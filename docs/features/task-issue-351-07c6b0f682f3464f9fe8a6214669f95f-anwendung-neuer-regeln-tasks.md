# Tasks: Anwendung neuer Regeln auf den bestehenden Sendeplan (Issue #351)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Testinfrastruktur | `ProgramScheduleTestDb.CreateMultiSeasonShowAsync` anlegen (3–8 Staffeln, 10–20 Episoden je Staffel, leicht variierende Laufzeiten, gelegentlich doppelte Länge, deterministisch) | Offen | — |
| 2 | Testinfrastruktur | `ProgramScheduleTestDb.CreateCollectionAsync` anlegen (MovieCollection + Filme in Releasedatum-Reihenfolge); `SeedCollectionAsync` in `ProgramScheduleServiceTests_PriorityTakeover` delegiert darauf | Offen | — |
| 3 | Testinfrastruktur | `ProgramScheduleTestDb.SeedRealisticLibraryAsync` anlegen (mehrere Mehrstaffel-Serien, mehrere Sammlungen, Einzelfilme) | Offen | — |
| 4 | Testinfrastruktur | `ProgramScheduleTestDb.CreateRuleAsync` um `createdUtc`/`updatedUtc`-Parameter erweitern und `SetRuleActiveAsync`-Helfer (`IsActive` + `UpdatedAt = UtcNow`, produktionsidentisch zu `ProgramRuleService.SetRuleActiveAsync`) ergänzen | Offen | — |
| 5 | Logik | `Evicts` in `ReconcileAsync` erweitern: expliziter Gleichregel-Schutz (`loser.Id == winner.Id` → `false`) **vor** den Frische-Stufen; bei `winner.Priority == loser.Priority` gewinnt die Occurrence, wenn (a) `cursors` für `winner.Id` keinen Eintrag hat (nie materialisiert) **oder** (b) `winner.UpdatedAt > kept.CreatedAt` (Regeländerung/-aktivierung jünger als die konkurrierende Belegung — Re-Aktivierung), sonst `winner.Id < loser.Id` | Offen | — |
| 6 | Logik | Occurrence-Schleife in `ReconcileAsync` auf `Priority desc → StartUtc → Rule.Id` umordnen | Offen | — |
| 7 | Logik | `DisplaceRuleFuture` auf slot-genaue Verdrängung (`slot.Start >= fromStartUtc` innerhalb der Regel-Airings) generalisieren; Aufruf mit `loser.Start` statt `airing[0].Start` (Präfix-Erhalt); `losers` deterministisch verarbeiten (aufsteigender `Start`, dann `ProgramRuleId`) | Offen | — |
| 8 | Tests | Testklasse `ProgramScheduleServiceTests_NewRules` anlegen: `Reconcile_NewRuleFreeWindow_MaterializesInAlreadyPlannedDays` | Offen | — |
| 9 | Tests | `Reconcile_NewRuleTailOverlap_ShiftsPrimetimeBlock` | Offen | — |
| 10 | Tests | `Reconcile_NewRuleOverlap_ShiftedBlockTailDefers` | Offen | — |
| 11 | Tests | `Reconcile_DroppedCollectionFilm_SequelsDisplaced_PredecessorsRefill` | Offen | — |
| 12 | Tests | `Reconcile_NewRuleLaterStart_PreservesAiringPrefix` (Geometrie: Occurrence-Start streng innerhalb eines Bestands-Slots → überspannter Slot + Präfix bleiben, Suffix ab Folge-Slot `Displaced`) | Offen | — |
| 13 | Tests | `Reconcile_NewRuleIsIdempotent_SecondRunNoOp` | Offen | — |
| 14 | Tests | `Reconcile_ReactivatedRule_MaterializesInAlreadyPlannedDays` (reaktivierte Regel **jünger** als Inhaber: `R2.Id > R1.Id`; `UpdatedAt` > Belegungs-`CreatedAt`; Lauf 3 No-Op) | Offen | — |
| 15 | Tests | `Reconcile_FreshVsFresh_SameWindow_ConvergesDeterministic` (zwei neue gleichpriore Regeln, selbes Fenster: Lauf 1 Iterationsreihenfolge, Lauf 2 einmaliger Wechsel, ab Lauf 3 Seniorität; ≤2 Gewinnerwechsel, dann No-Op) | Offen | — |
| 16 | Tests | `Reconcile_NewRuleOverlap_FillerSlotDisplaced` (Fenster nur mit Filler-Slots: einzeln `Displaced`, frischer Block Scheduled, Plan lückenlos) | Offen | — |
| 17 | Tests | `Reconcile_NewRuleLowerPriority_DoesNotShiftHigherPriorityAiring` (höherpriore Slots unverändert; freies Präfix materialisiert, Überhang defert — kein Komplett-Verfall) | Offen | — |
| 18 | Tests | `Reconcile_EqualPriorityEstablishedRules_DeterministicNoOscillation` | Offen | — |
| 19 | Tests | `Reconcile_NewRuleOverlap_RunningSlotStaysProtected` | Offen | — |
| 20 | Tests | `Reconcile_NewRuleRealisticLibrary_FullScenario` (mehrere `ExecutePassAsync`-Iterationen, lückenlos, geordnete Stränge, keine Duplikate) | Offen | — |
| 21 | Tests | Bestehende Suite fahren (`Category!=E2E`), Abweichungen in `*_Boundary`, `*_Reconcile`, `*_PriorityTakeover`, `*_CollectionOrder`, `*_Stability`, `*_StrandRotation` u. a. einzeln bewerten und Erwartungen anpassen (kein Deaktivieren/Abschwächen) | Offen | — |
| 22 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md`: Frisch-Regel-Gewinnersemantik (beide Frische-Stufen inkl. `UpdatedAt`-Nachweis), chronologische Occurrence-Iteration, slot-genaue Verdrängung und Sammlungs-Kaskade im Abschnitt Neu-Bewertung dokumentieren | Offen | — |
| 23 | Dokumentation | `docs/RELEASE_NOTES.md`: Eintrag für das neue Verhalten; `README.md` bei Bedarf | Offen | — |
| 24 | Abschluss | Debug- **und** Release-Build fehlerfrei; volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün; reale Abnahme (Akzeptanzszenario 1) durchführen oder dokumentiert verzichten/eskalieren | Offen | — |
