# Tasks: TV-Sender – Airings > 24 h bleiben in `AiringReproducible` nicht invalid (Issue #261)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Konfiguration | `ProgramScheduleOptions.MaxOccurrenceLookbackDays` (int, Default 7) anlegen | Offen | — |
| 2 | Konfiguration | `ProgramScheduleOptions.EffectiveMaxOccurrenceLookback` (Normalisierung `<= 0` → 7 Tage) anlegen | Offen | — |
| 3 | Logik | `ProgramScheduleService._maxOccurrenceLookback` im Konstruktor aus `EffectiveMaxOccurrenceLookback` lesen | Offen | — |
| 4 | Logik | `BuildOccurrences` um Parameter `TimeSpan lookback` erweitern: `windowStartDay` aus `coverageStartUtc - lookback` (lokal) ableiten, Fensterfilter `startUtc >= coverageStartUtc - lookback` | Offen | — |
| 5 | Logik | `EnsureCoverageAsync`: `BuildOccurrences`-Aufruf um Argument `OccurrenceLookback` ergänzen (Verhalten unverändert) | Offen | — |
| 6 | Logik | `ReconcileAsync`: Laden von `scheduled`, Strukturaufbau (`invaderStarts`, `occurrences`/`occurrenceStarts`/`boundStarts`, `airings`/`fillerSlots`) und Gruppen-Validierung als wiederholbare Abschnitte organisieren (lokale Funktionen); Validierung liefert `firstInvalid` je Regel ohne Slot-Stati zu ändern | Offen | — |
| 7 | Logik | `ReconcileAsync`: Auslöser-Heuristik `scheduled.Any(s => s.Start <= loadFrom)` — bei Treffer `loadFrom = coverageStart - _maxOccurrenceLookback`, Reload und einmalige Validierung | Offen | — |
| 8 | Logik | `ReconcileAsync`: Validierungsgetriebene Verbreiterung — feuert die Heuristik nicht und liefert die Basis-Validierung mindestens eine invalid-Gruppe einer aktiven Regel mit `End > coverageStart`-Slot, einmalig verbreitern, neu laden, erneut validieren; Suffix-Verdrängung erst nach dem finalen Durchlauf | Offen | — |
| 9 | Logik | `ReconcileAsync`: `reconcileLookback = coverageStart - loadFrom` an `BuildOccurrences` übergeben | Offen | — |
| 10 | Logik | `ReconcileAsync`: `MaterializeOccurrenceEvicting`-Schleife auf `o.StartUtc >= coverageStart - OccurrenceLookback` filtern | Offen | — |
| 11 | Logik | `AiringReproducible`: Mehr-Occurrence-Hypothese — konsumierte Slot-Zahl `c = expected.Count - k` als Präfix zulassen (`1 <= c <= airing.Count`), Rest-Liste rekursiv gegen Occurrences `> occurrenceStart` prüfen; Memoization | Offen | — |
| 12 | Logik | `AiringReproducibleInterleaved`: analoge Erweiterung — `picks.Count - p` als konsumiertes Präfix, rekursive Restprüfung gegen Occurrences `> occurrenceStart` | Offen | — |
| 13 | Dokumentation | `ProgramSchedule:MaxOccurrenceLookbackDays` in `docs/help/tv-sender.md` dokumentieren (inkl. Verhalten jenseits der Obergrenze) | Offen | — |
| 14 | Dokumentation | Eintrag in `docs/RELEASE_NOTES.md` (Korrektur + neue Option) | Offen | — |
| 15 | Tests | `Reconcile_LongAiring_StaysScheduled` (> 24-h-Film, tägliche Regel, verschmolzene Kette, bleibt `Scheduled`, zweiter Lauf ruhig) | Offen | — |
| 16 | Tests | `Reconcile_LongAiring_BeyondTwoDays_StaysScheduled` (> 48-h-Item, Tagesschleifen-Erweiterung) | Offen | — |
| 17 | Tests | `Reconcile_MultiPartLongAiring_StaysScheduled` (Marathon-Block: mehrteilige Item-Kette > 24 h, Auslöser-Pfad auf Slot-Ebene) | Offen | — |
| 18 | Tests | `Reconcile_LongAiring_Shifted_StaysScheduled` (verspäteter realer Start, `invaderStarts`-Evidenz > 24 h) | Offen | — |
| 19 | Tests | `Reconcile_PartiallyMaterializedLongAiring_NoTrigger_StaysScheduled` (`k > 0`-Restlücke: tail-only materialisierte Occurrence, kein Slot `Start <= loadFrom` — Auslöser feuert nicht; validierungsgetriebene Verbreiterung rettet, `SlotsDisplaced == 0`) | Offen | — |
| 20 | Tests | `Reconcile_LongAiring_ChangedRule_StillDisplaced` (echte Veraltung bleibt verdrängt) | Offen | — |
| 21 | Tests | `Reconcile_LongAiring_BeyondMaxLookback_Displaced` (Options-Obergrenze wirkt, dokumentierte Grenze) | Offen | — |
| 22 | Tests | `Reconcile_GenreRule_Interleaved_LongAiring_StaysScheduled` (Interleaved-Pfad, Pick-Kette > 24 h) | Offen | — |
| 23 | Tests | `ProgramScheduleOptions_MaxOccurrenceLookback_Normalizes` (Options-Normalisierung) | Offen | — |
| 24 | Regression | Volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün; Build Debug + Release (`dotnet build VideoPlayer.sln`) | Offen | — |
