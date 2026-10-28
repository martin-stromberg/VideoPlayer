# Tasks: Wiederholte Verdrängung gleicher Titel bei Sendeplan-Neuberechnung verhindern (Issue #290)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Testinfrastruktur | `AssertReconcileNoOpAsync` in `ProgramScheduleTestDb` anlegen (`SlotsDisplaced == 0` und `SlotsCreated == 0`, Slot-Dump im Fehlerfall) | Offen | — |
| 2 | Testinfrastruktur | `LinkMovieMediaItemAsync` / `LinkEpisodeMediaItemAsync` in `ProgramScheduleTestDb` anlegen (zweite Mediendatei an bestehenden Inhalt) | Offen | — |
| 3 | Diagnose | Baseline-Test `Reconcile_IdenticalUtcNow_SecondRunIsNoOp` (flache Regel, zweiter Lauf identisches `utcNow` → 0/0) | Offen | — |
| 4 | Diagnose | Baseline-Test `Reconcile_GenreInterleaved_IdenticalUtcNow_SecondRunIsNoOp` (mehrsträngige Genre-Regel → 0/0) | Offen | — |
| 5 | Diagnose | Reproduktionstest `Reconcile_MovieSecondMediaItemProbed_SlotStaysScheduled` (Repräsentantenwechsel nach Probe — fail-first) | Offen | — |
| 6 | Diagnose | Reproduktionstest `Reconcile_ShiftedAiring_InvaderDisplaced_StableAcrossRuns` (Invader-Oszillation — entscheidet Schritt 7 des Plans) | Offen | — |
| 7 | Diagnose | Log-Korrelation `[ProgramSchedule] Re-Bewertung`-Zeilen gegen `MediaDurationProbeWorker`-Batches (produktiver Nachweis der Repräsentantenwechsel-Kaskade als Hauptursache) | Offen | — |
| 8 | Logik | Interne Records `MediaItemVariant` und `MediaItemChoice` in `ProgramScheduleService.cs` anlegen | Offen | — |
| 9 | Logik | `MovieMediaItemsAsync` / `EpisodeMediaItemsAsync` auf `Dictionary<long, MediaItemChoice>` umstellen (Repräsentant + gemessene Alternativen) | Offen | — |
| 10 | Logik | `TryMovieItem` / `ToItem` und alle `MediaItemRef`-Aufrufer auf `MediaItemChoice` anpassen; `SchedulableItem.MediaItemAlternates` befüllen | Offen | — |
| 11 | Logik | `SlotMatchesItem` implementieren und in `FlatAiringTailReproducible` / `InterleavedAiringTailReproducible` einsetzen (toleranter Slot-Vergleich) | Offen | — |
| 12 | Logik | `ExpandGenreAsync` `coverageStart`-Parameter einführen und an `GenreCanonicalResolver.ResolveCanonicalNames` durchreichen | Offen | — |
| 13 | Logik | `ExpandMaterialAsync`-Overload ohne `coverageStart` beseitigen; `EnsureCoverageAsync` und `GetRuleDiagnosesAsync` übergeben ihre Zeitbasis | Offen | — |
| 14 | Logik | `MediaDurationProbeWorker`: optionalen `IProgramScheduleSignal`-Parameter + `BeginScan`-Scope um die Backlog-Schleife | Offen | — |
| 15 | Logik | Invader-Korrektur je Diagnoseergebnis (`_invaderStarts` bei nachgewiesener Oszillation zusätzlich aus `Displaced`-Historie des Fensters speisen — Evidenz nur; Verschiebungs-Provenienz am Slot nur bei Widerlegung) | Offen | — |
| 16 | Tests | `Reconcile_EpisodeSecondMediaItemProbed_SlotStaysScheduled` (Serien-Pendant Repräsentantenwechsel) | Offen | — |
| 17 | Tests | `Reconcile_UnlinkedMediaItem_StillDisplacesOnce` (echte Änderung → genau einmal ersetzt, Folgelauf stabil) | Offen | — |
| 18 | Tests | `Reconcile_SeasonalGenre_UtcNowBasis_DeterministicExpansion` (Genre-Auflösung folgt Lauf-`utcNow`) | Offen | — |
| 19 | Tests | Neue `MediaDurationProbeWorkerTests`: `MediaDurationProbeWorker_BundlesBacklogSignals_SingleWake` | Offen | — |
| 20 | E2E-Tests | `TVSenderAdmin_RepeatGenerate_ProducesNoDisplacedSeries` (zweimal „Jetzt generieren" → keine Verdrängt-Serie) | Offen | — |
| 21 | Verifikation | DoD-Lauf: `dotnet build` Debug + Release fehlerfrei, volle Testsuite grün, Kaputtmachen-Beweis der neuen Tests | Offen | — |
