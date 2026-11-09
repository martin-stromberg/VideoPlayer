# Tasks: Filme einer Filmsammlung in Releasedatum-Reihenfolge senden (Issue #334)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `SchedulableItem.Sequenced`-Flag einführen (Filme mit `MovieCollectionId` tragen fachliche Abspielreihenfolge) | Fertig | Build fehlerfrei |
| 2 | Logik | `TryMovieItem`: Sammlungsfilme erhalten Strang-Schlüssel `collection:{id}` + `Sequenced` | Fertig | `EnsureCoverage_GenreRule_CollectionStrand_KeepsReleaseOrder` (Cursor `collection:{id}`) |
| 3 | Logik | `StrandState.PreservesOrder` um `Sequenced`-Items erweitern; `IsUnorderedRing` schließt sie aus der Hash-Mischung aus | Fertig | `EnsureCoverage_MovieCollection_AirsInReleaseOrder_AndContinuesAcrossRuns`, `EnsureCoverage_MovieCollection_BlockedHead_DefersInsteadOfSkipping` |
| 4 | Logik | `ExpandGenreAsync`: Sammlungsfilme behalten ihren `collection:{id}`-Strang statt `other`; sammlungslose Filme weiter in `other` | Fertig | `EnsureCoverage_GenreRule_CollectionStrand_KeepsReleaseOrder`, `EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder` (unveraendert gruen) |
| 5 | Tests | Neue Testklasse `ProgramScheduleServiceTests_CollectionOrder` (echtes SQLite) + Eintrag in `ProgramScheduleConventionTests.StepTestFiles` | Fertig | 4 Tests gruen |
| 6 | Tests | Bestandstests an das geaenderte Semantik anpassen: `_RingOrder` (Collection-Kopf = Release-Erster; Verteilungstest auf Genre-Material), `_AgeWindow` (drei Unordered-Skip-Tests auf Genre-Material umgestellt), `_Boundary` (Release-Reihenfolge im Seed, Kommentare) | Fertig | Alle 284 ProgramSchedule-Tests gruen |
| 7 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md` (Ringmischung, `PreservesOrder`), `docs/help/tv-sender.md` (Jugendschutz-Semantik, Genre-Interleaving, Materialreihenfolge), `docs/RELEASE_NOTES.md` (EN+DE) | Fertig | — |
| 8 | Abschluss | `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei; volle Suite `dotnet test VideoWebPlayer.Tests` gruen | Offen | laeuft |

## Entscheidungen

- **Geordneter Strang je Sammlung**: Sammlungsfilme bilden einen `PreservesOrder`-Strang
  (analog den `show:{id}`-Episoden-Strängen) mit eigenem persistierten Cursor
  `collection:{id}` — die in der Expansion vorhandene `ReleaseDate ?? PremieredAt`-Sortierung
  wird nicht mehr von `MixUnorderedRing` verworfen.
- **Reihenfolge statt Block**: In Genre-Rotationen interleaved der Sammlungs-Strang mit den
  übrigen Strängen — andere Titel dürfen zwischen zwei Sammlungsfilmen liegen, garantiert
  ist nur die Reihenfolge (Teil n+1 nie vor Teil n).
- **Unvollständige Reihen**: Es ordnen schlicht die im Bestand vorhandenen Teile — kein
  Sonderverhalten nötig.
- **Defer statt Skip bei Altersfenster**: Ein gesperrter Sammlungs-Kopf wartet wie bei
  Episoden, statt übersprungen zu werden — sonst könnte eine Fortsetzung ihren Vorgänger
  überholen. Die drei Skip-Semantik-Tests der AgeWindow-Klasse laufen deshalb jetzt gegen
  sammlungslose Genre-Filme (dort bleibt die Skip-Semantik erhalten).
- **Kein Schema-Eingriff**: `collection:{id}`-Cursors nutzen die vorhandene
  `ProgramRuleCursors`-Tabelle (StrandKey-Unique-Index), keine Migration noetig.
