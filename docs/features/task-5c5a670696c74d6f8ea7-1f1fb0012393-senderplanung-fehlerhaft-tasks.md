# Tasks: Senderplanung fehlerhaft

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | Lookback-Guard in `MaterializeOccurrence`: Early-Return `ranges.Count == 0` vor dem Konfliktcheck (kein `CommitTaken`, kein Cursor-Verbrauch für beendete Occurrences) | Offen | — |
| 2 | Logik | `MatchingItemDuration`-Hilfsmethode anlegen (Item-Duration bzw. passende `MediaItemAlternates`-Variante; Laufzeitteil aus `SlotMatchesItem`) | Offen | — |
| 3 | Logik | `UnwrittenHeadroom`-Hilfsmethode anlegen (`rule.SlotDuration ?? (wanted − 1) · maxItemDuration` der Expansion) | Offen | — |
| 4 | Logik | `AnchorProvable`-Hilfsmethode anlegen (Kopf-Anker: exakte Lage ∨ `ShiftProvable` ∨ innerhalb `UnwrittenHeadroom`; Mapping auf `NoOccurrenceAnchor`/`AnchorWithoutInvaderEvidence`) | Offen | — |
| 5 | Logik | `SlotStillValid`-Hilfsmethode anlegen (Clip-Mitgliedschaft via `ClipMediaItemIds`; Item-Mitgliedschaft via `FindItemIndex`; Laufzeit; `IsAllowedAt` am Slot-Start; Ring-Kontiguität geordneter Stränge je Gruppe) | Offen | — |
| 6 | Logik | `ReconcileWindow.Validate` auf titelweise Validierung umstellen (Kopf-Anker + slotweise Prüfung je Ausstrahlung; `InvalidGroups`-Struktur und `HasRescuableInvalid` beibehalten) | Offen | — |
| 7 | Logik | Re-Simulations-Machinerie entfernen: `ReproContext`, beide `AiringReproducible`-Überladungen, `FlatAiringTailReproducible`, `InterleavedAiringTailReproducible`, `PrefixTakeAssignments`, `DeferralAssignments`, `ProvableDeferral`, `MaxDeferredStrands`, `SlotMatchesItem`, `LaterGroupSlots`-Plumbing in `Validate` | Offen | — |
| 8 | Logik | XML-Doku von `IProgramScheduleService.EnsureCoverageAsync` (Lookback nur laufende Ausstrahlungen) und `ReconcileAsync` (titelweise Validierung) präzisieren | Offen | — |
| 9 | Tests | Neuen Test `Simulation_MovieAddedToFlatGenreRule_NoDisplacement` in `ProgramScheduleServiceTests_PrimetimeChanges` anlegen (nur Film-Genre → Flachpfad; `AssertNoDisplacedAsync` + `AssertPreserved`) | Offen | — |
| 10 | Tests | `ProgramScheduleServiceTests_Generation.EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder` an neue Soll-Positionen anpassen (`episodes.Take(4)`, Cursor `episodes[3]`) | Offen | — |
| 11 | Tests | `ProgramScheduleServiceTests_Generation.EnsureCoverage_SeriesExhausted_WrapsAround` anpassen (`Take(3) = [E1,E2,E1]` statt `[E2,E1,E2]`) | Offen | — |
| 12 | Tests | `ProgramScheduleServiceTests_Reconcile.Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip` anpassen (`Skip(2)` → `Skip(0)`, beide Stellen) | Offen | — |
| 13 | Tests | `ProgramScheduleServiceTests_Reconcile.Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip`: eine tatsächlich verplante Episode löschen (z. B. `episodes[3]`) und Erwartungen an neue Belegung anpassen | Offen | — |
| 14 | Tests | `ProgramScheduleServiceTests_Reconcile.Reconcile_NewEpisodeInserted_ReplansInAirOrder`: erwartete Reihenfolge auf `[E1,E2,E3,newE]` anpassen (heutige Ausstrahlung bleibt titelweise gültig) | Offen | — |
| 15 | Tests | `ProgramScheduleServiceTests_Interleaving.EnsureCoverage_GenreRule_SecondRun_ContinuesEachShowCursor`: Assertions verifizieren und Lookback-Konsum-Kommentar anpassen | Offen | — |
| 16 | Tests | Audit-Lauf: volle Suite ausführen, weitere implizit vom Lookback-Konsum abhängige Assertions identifizieren und anpassen | Offen | — |
| 17 | Verifikation | Prüfpflicht: Guard zurückbauen → `EnsureCoverage_FirstRunAfterEndedOccurrence_RingStartsAtBeginning` rot; titelweise Prüfung zurückbauen/sabotieren → beide `Simulation_*_NoDisplacement`-Tests rot (18 Displaced) | Offen | — |
| 18 | Verifikation | Volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün (16 Primetime-Tests + ~212 ProgramSchedule-Wächter); `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei | Offen | — |
