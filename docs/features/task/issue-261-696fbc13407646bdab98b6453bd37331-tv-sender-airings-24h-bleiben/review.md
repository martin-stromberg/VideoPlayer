# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] Feld `MaxOccurrenceLookbackDays` (`int`, Default `7`) in `ProgramScheduleOptions` — angelegt (`VideoWebPlayer/Configuration/ProgramScheduleOptions.cs`)
- [x] Eigenschaft `EffectiveMaxOccurrenceLookback` (`TimeSpan`, get-only, `<= 0` → 7 Tage) in `ProgramScheduleOptions` — vorhanden, folgt dem `Effective…`-Muster
- [x] Feld `_maxOccurrenceLookback` (`TimeSpan`) in `ProgramScheduleService`, im Konstruktor aus `EffectiveMaxOccurrenceLookback` gelesen — vorhanden (Z. 35, 64)
- [x] Methode `BuildOccurrences` um Parameter `TimeSpan lookback` erweitert — `windowStartDay` aus `coverageStartUtc - lookback` (lokale Zeitzone), Fensterfilter `startUtc >= coverageStartUtc - lookback` (Z. ~1194, ~1220)
- [x] `ReconcileAsync` dreiphasig umgebaut — lokale Funktionen `LoadScheduledAsync`, `BuildWindowStructures` (invaderStarts/occurrences/occurrenceStarts/boundStarts/airings/fillerSlots aus dem aktuellen `scheduled`) und `ValidateAirings` (rein lesend, `firstInvalid` je Regel ohne Statusänderung) vorhanden (Z. ~176–284)
- [x] Auslöser-Heuristik `scheduled.Any(s => s.Start <= loadFrom)` — bei Treffer `loadFrom = coverageStart - _maxOccurrenceLookback`, Reload, danach einmalige Validierung (Z. ~193–199)
- [x] Validierungsgetriebene Verbreiterung — nur bei `!widened && hasRescuableInvalid` (invalid-Gruppe aktiver Regel mit `End > coverageStart`); einmaliger Reload + erneute Validierung (Z. ~274–284)
- [x] Suffix-Verdrängung erst nach dem finalen Validierungsdurchlauf über `invalidGroups` (Z. ~297–309)
- [x] `reconcileLookback = coverageStart - loadFrom` wird an `BuildOccurrences` übergeben (in `BuildWindowStructures`)
- [x] `MaterializeOccurrenceEvicting`-Schleife gefiltert auf `o.StartUtc >= coverageStart - OccurrenceLookback` (Z. 381)
- [x] `EnsureCoverageAsync` — `BuildOccurrences`-Aufruf um Argument `OccurrenceLookback` ergänzt, sonst unverändert (Z. 115)
- [x] `AiringReproducible` — Mehr-Occurrence-Hypothese: Dispatcher + `FlatAiringTailReproducible` mit `offset`/`afterStart`, konsumierte Slot-Zahl `consumed = expected.Count - k` als Präfix (`1 <= consumed <= airing.Count - offset`), rekursive Restprüfung gegen Occurrences `> occurrenceStart`, Memoization über `(Offset, AfterStart)` (Z. ~548–640)
- [x] `AiringReproducibleInterleaved` — analoge Erweiterung (`picks.Count - p` als konsumiertes Präfix, rekursive Restprüfung, `strandOf`/`firstIndex`/`countInAiring`/`maxPrefix`/Strand-Offsets je Rekursionsstufe auf der Rest-Liste neu bestimmt) (Z. ~650–790)
- [x] Unverändert geblieben wie geplant: `OccurrenceLookback`-Konstante (24 h, Z. 26), `GroupAirings`, `ShiftPastInvaders`, `MaterializeOccurrence(Evicting)`, `Displace`, `RewindCursor`, `IProgramScheduleService`
- [x] `ProgramSchedule:MaxOccurrenceLookbackDays` in `docs/help/tv-sender.md` dokumentiert inkl. Verhalten jenseits der Obergrenze (verdrängt + neu eingeplant)
- [x] Eintrag in `docs/RELEASE_NOTES.md` vorhanden (EN „What's New" + DE „Neuerungen", Korrektur + neue Option)
- [x] Test `Reconcile_LongAiring_StaysScheduled` — vorhanden, grün (30-h-Film, `Daily`, verschmolzene Kette ≥ 2 Slots, `ReconcileAsync` 38 h nach Occurrence-Start, `SlotsDisplaced == 0`, zweiter Lauf ruhig)
- [x] Test `Reconcile_LongAiring_BeyondTwoDays_StaysScheduled` — vorhanden, grün (50-h-Item, Tagesschleifen-Erweiterung)
- [x] Test `Reconcile_MultiPartLongAiring_StaysScheduled` — vorhanden, grün (45-min-Episoden-Kette über 30 h, Innen-Slot über der Fensterkante → Auslöser-Pfad)
- [x] Test `Reconcile_LongAiring_Shifted_StaysScheduled` — vorhanden, grün (Vorgänger-Overshoot, `invaderStarts`-Evidenz außerhalb des Basisfensters)
- [x] Test `Reconcile_PartiallyMaterializedLongAiring_NoTrigger_StaysScheduled` — vorhanden, grün (tail-only materialisierte Occurrence, Auslöser feuert konstruktionsbedingt nicht → validierungsgetriebene Verbreiterung)
- [x] Test `Reconcile_LongAiring_ChangedRule_StillDisplaced` — vorhanden, grün (`TimeOfDay`-Änderung → Verdrängung + Neu-Einplanung)
- [x] Test `Reconcile_LongAiring_BeyondMaxLookback_Displaced` — vorhanden, grün (`MaxOccurrenceLookbackDays = 1` → 30-h-Airing verdrängt)
- [x] Test `Reconcile_GenreRule_Interleaved_LongAiring_StaysScheduled` — vorhanden, grün (GenreRotation `Daily`, 13-h-Episoden, Pick-Kette > 24 h)
- [x] Test `ProgramScheduleOptions_MaxOccurrenceLookback_Normalizes` — vorhanden, grün (`0`/`-3` → 7 Tage, `4` → 4 Tage, Default 7 Tage)

## Hinweise

- **Dokumentierte Planabweichung `Recurrence.Once`:** `Reconcile_MultiPartLongAiring_StaysScheduled` nutzt `Recurrence.Once` + `singleDate` + `slotDuration: 30h` statt der im Plan genannten `Daily`-Regel. Das Nachweisziel des Tests — mehrteilige Item-Kette > 24 h, Kopf-Slots vor `loadFrom`, kein Einzelslot überspannt beide Grenzen, Auslöser-Pfad `Start <= loadFrom` — ist vollständig erbracht; `Once` isoliert den Auslöser-Pfad sogar ohne die zusätzliche Variablen einer Folge-Occurrence. Die verschmolzene Multi-Occurrence-Kette ist über `Reconcile_LongAiring_StaysScheduled`/`…_BeyondTwoDays_…`/Interleaved-Test (alle `Daily`) abgedeckt. Kein offener Rest.
- `Reconcile_PartiallyMaterializedLongAiring_NoTrigger_StaysScheduled` nutzt ebenfalls `Once` — das entspricht der Planvorgabe „eine Occurrence" (junge Planhistorie, tail-only materialisiert); eine `Daily`-Regel würde eine zweite Occurrence in die `k`-Rekonstruktion einbringen. Keine Abweichung.
- **Verifikation durch den Prüfer:** Alle 9 neuen Tests einzeln ausgeführt — 9/9 grün. Volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` — 2246 bestanden, 1 übersprungen, 0 Fehler. `dotnet build VideoPlayer.sln` Debug und Release jeweils 0 Fehler (ein erster Release-Versuch scheiterte nur an einer Dateisperre durch einen parallel laufenden `VideoWebPlayer.Tests.exe`-Prozess — Umgebungsproblem, kein Compile-Fehler; Wiederholung erfolgreich).
