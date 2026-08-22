# Tasks: Fehlerhafte Ermittlung der nächsten Episode

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Tests | Neue Testklasse `ContinueWatchingServiceGetNextEpisodeTests` erstellen | Offen | — |
| 2 | Tests | Unit-Test `HappyPath_SimpleEpisodeSequence_ReturnsNextEpisode()` implementieren | Offen | — |
| 3 | Tests | Unit-Test `AllEpisodesWithoutReleaseDate_SortsByNumber()` implementieren | Offen | — |
| 4 | Tests | Unit-Test `AllEpisodesWithReleaseDate_SortsCorrectly()` implementieren | Offen | — |
| 5 | Tests | Unit-Test `MixedReleaseDate_NullAndNonNull_SortsConsistently()` implementieren | Offen | — |
| 6 | Tests | Unit-Test `EpisodeGaps_SkipsGappedEpisodes_FindsNext()` implementieren | Offen | — |
| 7 | Tests | Unit-Test `FirstEpisodeMissing_SkipsTo_NextAvailable()` implementieren | Offen | — |
| 8 | Tests | Unit-Test `LastEpisodeOfSeason_ReturnsNull_InSameSeason()` implementieren | Offen | — |
| 9 | Tests | Unit-Test `SeasonTransition_LastEpisodeOfSeason_JumpsToNextSeason()` implementieren | Offen | — |
| 10 | Tests | Unit-Test `NoNextSeason_ReturnsNull()` implementieren | Offen | — |
| 11 | Tests | Unit-Test `NextSeasonEmpty_ReturnsNull()` implementieren | Offen | — |
| 12 | Tests | Unit-Test `SingleEpisodeInSeason_ReturnsNull()` implementieren | Offen | — |
| 13 | Tests | Unit-Test `MultipleEpisodesWithIdenticalReleaseDate_SortsByNumber()` implementieren | Offen | — |
| 14 | Tests | Unit-Test `RegressionTest_LoopScenario_NoInfiniteLoop()` implementieren | Offen | — |
| 15 | Tests | Unit-Test `OffByOne_PositionNotConfusedWithId()` implementieren | Offen | — |
| 16 | Tests | Hilfsmethode `CreateTestShowWithSeasons()` implementieren | Offen | — |
| 17 | Tests | Hilfsmethode `AssertEpisodeEquals()` implementieren | Offen | — |
| 18 | Logik | WHERE-Klausel in `GetNextEpisodeAsync()` refaktorieren: `e.Number > current.Number` statt `e.ReleaseDate >= current.ReleaseDate` | Offen | — |
| 19 | Logik | Sortierung in `GetNextEpisodeAsync()` ändern: `OrderBy(e => e.Number)` als Primärsortierung | Offen | — |
| 20 | Logik | Überprüfung: Keine SQL-Vergleiche mit NULL in der refaktorierten Logik | Offen | — |
| 21 | Logik | Staffelwechsel-Logik in `GetNextEpisodeAsync()` prüfen auf Kompatibilität mit neuer Sortierung | Offen | — |
| 22 | Referenz | `GetNextMovieAsync()` überprüfen auf ähnliche NULL-Probleme | Offen | — |
| 23 | Verifikation | Bestehende Tests `ContinueWatchingServiceSignalRTests` ausführen und grün validieren | Offen | — |
| 24 | Code Review | Code Review der refaktorierten `GetNextEpisodeAsync()`-Methode durchführen | Offen | — |
| 25 | E2E-Tests | E2E-Test für Happy Path: Episode 1 → Episode 2 implementieren | Offen | — |
| 26 | E2E-Tests | E2E-Test für Episode-Lücken-Szenario implementieren | Offen | — |
| 27 | E2E-Tests | E2E-Test für Staffelwechsel implementieren | Offen | — |
| 28 | E2E-Tests | E2E-Test für Serienende (letzte Episode, letzte Staffel) implementieren | Offen | — |
