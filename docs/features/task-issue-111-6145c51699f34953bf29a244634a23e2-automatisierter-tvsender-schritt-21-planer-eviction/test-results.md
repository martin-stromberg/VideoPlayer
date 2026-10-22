# Testergebnisse — N16 Planer-Eviction

- Neue Tests: 3 (`Reconcile_PartiallyAiredOccurrence_KeepsTailScheduled`,
  `Reconcile_PartiallyAiredOccurrence_TomorrowAiringsStayStable`,
  `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap`) — echtes SQLite (In-Memory).
- Mutationscheck: ohne Service-Fix schlagen alle 3 fehl (Dump zeigt das Live-Fehlerbild:
  partielles Tail → gesamte Regel-Zukunft verdrängt).
- `ProgramScheduleServiceTests_*`: 65/65 grün.
- Volle Suite synchron (Debug): **2150 grün, 0 Fehler, 1 Alt-Skip**
  (`PairedDevice_ReportsProgressForMovieWithoutCollection...`, bekannter Baseline-Skip),
  Dauer ~4:15 min, 2151 gesamt.
- Builds: `VideoPlayer.sln` Release = 0 Fehler; Debug-Kompilierung aller drei Projekte
  fehlerfrei (Warnungsstand 144 = Baseline). **Hinweis:** Die laufende Benutzer-App
  (`VideoWebPlayer`, PID 42440) sperrt `bin\Debug` — regulärer Debug-Build schlägt deshalb mit
  MSB3027 fehl; verifiziert wurde über `-p:OutDir=<repo>/scratch-test-bin` (gleiche Kompilierung,
  0 Fehler). Release-Pfad (`bin\Release`) unbeeinträchtigt.
- `dotnet format VideoPlayer.sln --verify-no-changes --severity error`: sauber.
