# Tasks: Ständige Wiederholungen — faire Strang-Rotation bei der Interleaved-Ausstrahlungsbelegung

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Tests | Neue Schritt-Testdatei `ProgramScheduleServiceTests_StrandRotation.cs` anlegen und in `ProgramScheduleConventionTests.StepTestFiles` eintragen | Offen | — |
| 2 | Tests | Hilfsmethode `SeedCollectionRotationScenarioAsync` schreiben (Genre-Regel mit `slotDuration`, Clip-Gruppe, viele `collection:{id}`-Stränge via `CreateCollectionAsync`/`CreateMovieAsync(collectionId)`/`LinkMovieGenreAsync`) | Offen | — |
| 3 | Tests | Repro-Test `EnsureCoverage_ManyCollectionStrands_RotatesLeadStrandAcrossDays` schreiben (rot auf Ist-Stand) | Offen | — |
| 4 | Logik | Lap-Schleife aus `SelectAiringItemsInterleaved` in `startOffset`-parametrisierte Teillogik extrahieren | Offen | — |
| 5 | Logik | Bedarfsgesteuerte Rotation umsetzen: Probelauf mit Offset 0 bei `wanted >= strandCount`; bei bedienten Strängen < nicht-leere Stränge zweiter Lauf mit Tagesrotations-Offset; `wanted < m` weiterhin direkt rotiert | Offen | — |
| 6 | Logik | `StrandStartOffset`-Semantik/Signatur und überholte Dokumentationskommentare in `SelectAiringItemsInterleaved`/`StrandStartOffset` an die neue Rotationsbedingung anpassen | Offen | — |
| 7 | Tests | Test `EnsureCoverage_ManyCollectionStrands_ReleaseOrderAndCursorsPreserved` schreiben (Release-Reihenfolge je Sammlung + `ProgramRuleCursors`-Fortschreibung über Rotation) | Offen | — |
| 8 | Tests | Test `EnsureCoverage_AllStrandsServed_NoRotation_StableStructure` schreiben (Kontrollfall: alle Stränge bedient → keine Rotation) | Offen | — |
| 9 | Tests | Test `Reconcile_RotatedPlan_StaysScheduledNoFalseDisplacement` schreiben (pick-agnostische Validierung verwirft rotierten Plan nicht) | Offen | — |
| 10 | Tests | Test `EnsureCoverage_EpisodeCountBelowStrandCount_StillRotates` schreiben (Regression Altweg `episodeCount < strandCount`) | Offen | — |
| 11 | Tests | Bestandstest-Prüfliste abarbeiten (`_Issue320`, `_ClipGroups.EnsureCoverage_InterleavedRule_ClipFillDistributed`, `_PrimetimeGrace`, `_PrimetimeAgeWindow`, `_PrimetimeTrailer`, `_PrimetimeChanges`) und bei verhungernden Szenarien Erwartungsbilder begründet anpassen | Offen | — |
| 12 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md` aktualisieren (bedarfsgesteuerte Strang-Rotation, Hinweis einmaliger Plan-Reset für Altbelegungen) | Offen | — |
| 13 | Dokumentation | `docs/RELEASE_NOTES.md` Eintrag ergänzen; `docs/help/tv-sender.md` bei Bedarf anpassen | Offen | — |
| 14 | DoD | `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei; volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün | Offen | — |
