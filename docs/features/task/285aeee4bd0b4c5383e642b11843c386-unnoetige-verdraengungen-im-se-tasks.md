# Tasks: Unnötige Verdrängungen im Sendeplan vermeiden (Issue #310)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Tests | Fail-first-Reproduktionstest `Reconcile_ShiftedAiring_ChainedInvaders_StaysScheduled` in `ProgramScheduleServiceTests_Boundary` anlegen (verkettete Invader-Evidenz, Erwartung: bleibt geplant / No-Op) | Offen | — |
| 2 | Tests | Bei Nicht-Reproduktion des Primärszenarios: weiteren Reproduktionstest aus der Kandidatenliste anlegen (Evidenzverlust nach `DeleteDisplacedSlotsAsync`, Ring-Duplikat über `FindItemIndex`, Interleaved-`strandOf`-Doppeldeutigkeit, `k < wanted`-Deckel) | Offen | — |
| 3 | Diagnose | `internal enum AiringInvalidReason` anlegen (stufen-geordnet: `None`, `RuleMissing`, `RuleInactive`, `NoOccurrenceAnchor`, `ClipChainBroken`, `TitlelessAiring`, `HeadItemNotInExpansion`, `StrandSequenceBroken`, `AnchorWithoutInvaderEvidence`, `HeadPickInconsistent`, `SlotSequenceMismatch`, `RemainderNotReproducible`) | Offen | — |
| 4 | Diagnose | Diagnose-Senke in `ReproContext` + Stufenschreiben in Clip-Vorfilter, `FlatAiringTailReproducible` und `InterleavedAiringTailReproducible` (Maximum-/„weiteste Stufe"-Semantik); `out AiringInvalidReason` am `AiringReproducible`-Einstieg | Offen | — |
| 5 | Diagnose | `ReconcileWindow.Validate`: Ablehnungsgrund bestimmen und je invalidem Block `LogInformation` (Regel-Id, Kopf-Start/End, Slot-Anzahl, Grund); `ProgramScheduleTestDb.CreateService` um optionalen `ILogger<ProgramScheduleService>`-Parameter erweitern | Offen | — |
| 6 | Logik | Ablehnungspfad diagnostizieren (welcher Ast von `AiringReproducible`/`FlatAiringTailReproducible`/`InterleavedAiringTailReproducible` den Block invalid erklärt — neuer Log-Grund + Debug-Lauf) und Befund dokumentieren | Offen | — |
| 7 | Logik | `ReconcileWindow.Rebuild`: belegte `Interval`-Menge aus Scheduled ∪ DisplacedEvidence aufbauen (ersetzt/ergänzt `_invaderStarts`) | Offen | — |
| 8 | Logik | `ReproContext`: `InvaderStarts`-Komponente auf Intervall-Evidenz umstellen | Offen | — |
| 9 | Logik | `FlatAiringTailReproducible`: Shift-Beweis auf `ShiftPastInvaders`-Fixpunkt über Intervall-Evidenz umstellen | Offen | — |
| 10 | Logik | `InterleavedAiringTailReproducible`: Shift-Beweis analog umstellen | Offen | — |
| 11 | Logik | Bei anderem Diagnosebefund: diagnostizierte Hypothesenraum-Lücke schließen (nur den bestätigten Pfad, kein Kriterium-Wechsel) | Offen | — |
| 12 | Tests | Hilfsassertion `AssertNoSelfDisplacedAsync` in `ProgramScheduleTestDb` ergänzen (kein `Displaced`/`Scheduled`-Paar gleichen Inhalts im selben Zeitintervall) | Offen | — |
| 13 | Tests | Optionaler Test `Reconcile_ShiftedAiring_ChainedInvaders_InvaderDisplaced_StableAcrossRuns` (Ketten-Evidenz nach verdrängtem Kettenglied) | Offen | — |
| 14 | Tests | Negativ-/Wechselwirkungs-Test `Reconcile_ShiftedAiring_EvidenceCleaned_ReplansOnce` (Evidenzverlust durch `DeleteDisplacedSlotsAsync` → einmalige Neuplanung, Folgelauf No-Op) | Offen | — |
| 15 | Logik | `IProgramScheduleService.CleanupSelfDisplacedSlotsAsync` deklarieren (`Task<int>`, nicht-administrativ, XML-Doku im `PurgeExpiredRulesAsync`-Stil) | Offen | — |
| 16 | Logik | `ProgramScheduleService.CleanupSelfDisplacedSlotsAsync` implementieren: `ExecuteDeleteAsync` über `Displaced`-Zeilen mit `Any`-Subquery auf `Scheduled`-Zwilling (identischer Kanal/`Start`/`End`/`ProgramRuleId`/`IsClipFill`, Inhalt via `MediaItemId`-/`MovieId`-/`TVShowEpisodeId`-Disjunktion), `LogInformation` mit Anzahl | Offen | — |
| 17 | Logik | `ProgramScheduleWorker`: `CleanupSelfDisplacedSlotsAsync` im `TvSenderEnabled`-Block nach `PurgeExpiredRulesAsync` und vor `ReconcileAsync` einhängen (Kommentar zur Reihenfolge) | Offen | — |
| 18 | Tests | `CleanupSelfDisplaced_RemovesProvenDuplicates` (Duplikat-Kette 1× `Scheduled` + n× `Displaced` via `CreateSlotAsync`: nur die `Displaced`-Kopien gelöscht, Rückgabewert = n) | Offen | — |
| 19 | Tests | `CleanupSelfDisplaced_KeepsDisplacedWithoutTwin` (Gegenprobe: anderer Inhalt am selben Intervall, kein `Scheduled`-Zwilling, fremde `ProgramRuleId`, abweichendes `IsClipFill` — bleibt alles) | Offen | — |
| 20 | Tests | `CleanupSelfDisplaced_RepresentativeSwitch_Removes` (Zwilling mit anderer `MediaItemId`, gleichem `MovieId` wird gelöscht) | Offen | — |
| 21 | Tests | `CleanupSelfDisplaced_PreservesInvaderEvidence_NoReplan` (Evidenz-Duplikat gelöscht → `ReconcileAsync` bleibt No-Op, keine echte Neuplanung) | Offen | — |
| 22 | Tests | `Reconcile_InvalidAiring_LogsRejectionReason` (bekanntermaßen invalider Block erzeugt Logzeile mit erwartetem Grund; `ListLogger<ProgramScheduleService>` über neuen `CreateService`-Parameter) | Offen | — |
| 23 | Tests | `Worker_Run_CleansSelfDisplacedDuplicates` in `ProgramScheduleWorkerTests` (geseedetes Duplikat-Paar wird durch Worker-Lauf entfernt) | Offen | — |
| 24 | Tests | Regressionszeugen verifizieren: bestehende Shift-/ChangedRule-/No-Op-/WorkerLoop-/DeleteDisplacedSlots-Tests müssen unverändert grün bleiben | Offen | — |
| 25 | Abschluss | Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün; `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei | Offen | — |
| 26 | Doku | `docs/help/tv-sender-ablauf-technisch.md` (Worker-Kette + Auto-Bereinigung) und Release Notes aktualisieren (Bugfix, automatische Duplikat-Bereinigung, Diagnose-Log) | Offen | — |
