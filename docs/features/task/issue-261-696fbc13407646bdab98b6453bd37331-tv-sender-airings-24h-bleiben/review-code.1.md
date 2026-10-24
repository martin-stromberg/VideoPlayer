# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ProgramScheduleService.cs (ProgramScheduleService)

- **God-Methode / fehlende Kapselung** — `ReconcileAsync` (Zeilen 136–428, ~290 Zeilen) trägt
  jetzt acht mit `= null!` deklarierte Closure-Variablen (`invaderStarts`, `occurrences`,
  `occurrenceStarts`, `boundStarts`, `airings`, `fillerSlots` in Zeilen 204–209 sowie
  `invalidGroups`, `hasRescuableInvalid` in Zeilen 238–239), die zwei lokale Funktionen
  (`BuildWindowStructures`, `ValidateAirings`) als versteckte Out-Parameter füllen. Die
  Korrektheit hängt vollständig an der Aufrufreihenfolge: Wird nach dem Reload
  (Zeilen 278–284) eines der beiden Rebuilds vergessen oder die Reihenfolge vertauscht,
  bleiben Strukturen des schmaleren Fensters aktiv bzw. `invalidGroups` null
  (NullReferenceException statt Compilerfehler, weil `null!` die Null-Prüfung unterdrückt).

  Empfehlung: Fensterzustand in eine kleine private Klasse auslagern (z. B.
  `ReconcileWindow` mit Feldern + `Rebuild()`/`Validate()`-Methoden). `loadFrom`,
  `scheduled` und alle abgeleiteten Strukturen wandern dorthin; die `null!`-Initialisierung
  und die streuende Closure-Mutation entfallen, Aufrufreihenfolge wird im Typ selbst
  garantiert.

- **Long Parameter List / Data Clump** — `AiringReproducible` (Zeile 559),
  `FlatAiringTailReproducible` (Zeile 573) und `AiringReproducibleInterleaved` (Zeile 653)
  haben je 9–10 Parameter; sechs davon (`state`, `occurrenceStarts`, `boundStarts`,
  `invaderStarts`, `minStartRemain`, `memo`) sind über die gesamte Rekursion invariant und
  werden unverändert weitergereicht.

  Empfehlung: Kontext in einen kleinen Record bündeln (z. B.
  `ReproContext(State, OccurrenceStarts, BoundStarts, InvaderStarts, MinStartRemain)` plus
  Memo), sodass die Methodensignaturen auf `(airing, offset, afterStart, context)`
  schrumpfen und der Dispatcher die Parameterliste nicht wiederholt.

- **Namenskonventionen** — Zwei Kleinigkeiten: (a) Die Memo-Typangabe ist uneinheitlich —
  der Dispatcher deklariert `Dictionary<(int Offset, DateTime AfterStart), bool>`
  (Zeile 551), die beiden Blattmethoden nehmen `Dictionary<(int, DateTime), bool>`
  (Zeilen 573, 653). (b) Die Dispatch-Zweige heißen asymmetrisch:
  `FlatAiringTailReproducible` vs. `AiringReproducibleInterleaved` — nur die flache Variante
  trägt das „Tail"-Konzept im Namen, obwohl beide denselben `offset`/`afterStart`-Vertrag
  erfüllen.

  Empfehlung: Tupelelementnamen überall gleich (oder gleich den Context-Record aus dem
  vorigen Befund) und die Methodennamen symmetrisch wählen, z. B.
  `FlatAiringTailReproducible` / `InterleavedAiringTailReproducible`.

### ProgramScheduleOptions.cs (ProgramScheduleOptions)

- **Fehlende Eingabevalidierung** — `MaxOccurrenceLookbackDays` hat nur eine Untergrenze
  (`<= 0` → Default), aber keine Obergrenze. Ein absurd großer Wert (z. B.
  `int.MaxValue`) lässt `coverageStart - _maxOccurrenceLookback` in
  `ReconcileAsync` (Zeilen 198, 280) in einen `ArgumentOutOfRangeException`-Unterlauf
  laufen bzw. ließe die Tages-Schleife in `BuildOccurrences` (Zeile 1206 ff.) praktisch
  endlos iterieren — ein Reconcile-Lauf würde hängen oder abstürzen.

  Empfehlung: In `EffectiveMaxOccurrenceLookback` zusätzlich nach oben klemmen
  (z. B. `Math.Min(MaxOccurrenceLookbackDays, 30)`), analog dazu ggf. denselben Schutz für
  `TrailerLeadDays` prüfen (gleiches Muster, bestehender Code).

### ProgramScheduleServiceTests_Reconcile.cs

- **Testqualität: tautologische Assertion** — `Reconcile_LongAiring_BeyondTwoDays_StaysScheduled`
  (Zeilen 540–541): `Assert.All(await ScheduledAsync(fixture, ct), s => Assert.Equal(
  ProgramSlotStatus.Scheduled, s.Status))` kann nie fehlschlagen — `ScheduledAsync` filtert
  bereits auf `Status == Scheduled`. Die Assertion prüft nichts, was `SlotsDisplaced == 0`
  nicht ohnehin abdeckt; ein alter verdrängter Slot würde von ihr gar nicht gesehen.

  Empfehlung: Entfernen oder durch eine echte Bedingung ersetzen, z. B.
  `Assert.All(await fixture.SlotsAsync(ct), s => Assert.Equal(ProgramSlotStatus.Scheduled,
  s.Status))` (prüft dann auch historische Slots und etwaige Alt-Verdrängungen).

- **Doppelter Code** — Der Displaced-Dump-plus-Assert-Block
  (`var dumped = (await fixture.SlotsAsync(ct)).Where(...).Select(...).ToList();
  Assert.True(summary.SlotsDisplaced == 0, "Displaced: " + ...)`) wird in den neuen Tests
  fünfmal nahezu identisch wiederholt (Zeilen ~508, ~541, ~571, ~638 und sinngemäß in
  `ProgramScheduleServiceTests_Interleaving.cs` Zeile ~253).

  Empfehlung: In eine gemeinsame Assert-Hilfsmethode auslagern, z. B.
  `static async Task AssertNoDisplacedAsync(ProgramScheduleTestDb fixture,
  ProgramScheduleReconcileSummary summary, CancellationToken ct)`, die den Dump nur im
  Fehlerfall aufbaut. (Bestehende Tests nutzen dasselbe Muster — die Konsolidierung kann
  dort gleich mitgezogen werden.)

## Geprüfte Dateien

- `VideoWebPlayer/Services/ProgramScheduleService.cs`
- `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs`
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Reconcile.cs`
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Interleaving.cs`
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Boundary.cs`
- `docs/help/tv-sender.md` (stichprobenartig)
- `docs/RELEASE_NOTES.md` (stichprobenartig)

## Anmerkungen zur Korrektheit (keine Befunde)

Geprüft und für korrekt befunden:

- **Rekursion/Memoization** (`AiringReproducible`, Zeilen 550–571): Abbruch bei
  `offset >= airing.Count`; jeder Rekursionsschritt konsumiert ≥ 1 Slot (`consumed >= 1`)
  und belegt einen streng größeren Occurrence-Start — Tiefe durch die Anzahl der
  Occurrences im Fenster begrenzt (≤ ~Fenstertage), kein Stackrisiko. Der Memo-Key
  `(offset, afterStart)` ist vollständig (alle übrigen Parameter sind
  rekursionsinvariant); höheres `afterStart` erzeugt eigene Einträge, keine falsche
  Wiederverwendung.
- **Dreiphasen-Logik:** Phase-1-Validierung ist tatsächlich read-only —
  `SelectAiringItems`/`SelectAiringItemsInterleaved` mutieren weder `items` noch Cursor;
  die Interleaved-Simulation arbeitet auf frischen `StrandState`-Kopien (`simStrands`).
  `keptSlots`, `blocked`, `airings`, `fillerSlots`, `boundStarts`, `occurrenceStarts`,
  `invaderStarts` und `invalidGroups` werden alle aus dem finalen `scheduled`-Stand
  aufgebaut; EF-Tracking liefert beim Reload dieselben Entitätsinstanzen, daher sind die
  Referenzen in `invalidGroups` konsistent.
- **Verbreiterung:** max. ein zusätzlicher Reload (Trigger ODER validierungsgetrieben,
  nie beides), Obergrenze `_maxOccurrenceLookback`; `BoundaryAfter` ist für
  Basis-Fenster-Anker durch die breiteren `boundStarts` unverändert (alle Zusatz-
  Occurrences liegen vor der Basis-Kante).
- **Fenstergrenzen:** `End > loadFrom`, `s.Start <= loadFrom`,
  `o.StartUtc >= coverageStart - OccurrenceLookback` und `o > afterStart && o <= airingStart`
  sind konsistent; der Materialisierungsfilter hält die Neuplanung bewusst auf dem
  Basisfenster (Oszillations-Kommentar, Zeile 378–380).
- **Tests:** Die neuen Tests sind verhaltensbasiert (öffentliche `ReconcileAsync`,
  DB-Zustand), decken Trigger-Pfad, validierungsgetriebenen Pfad, verschobene Starts,
  die Max-Lookback-Grenze, echte Staleness und den Interleaved-Pfad ab; alle 9 neuen
  Tests laufen lokal grün (`dotnet test --filter "LongAiring|MaxOccurrenceLookback|
  MultiPartLongAiring|PartiallyMaterializedLongAiring"` — 9/9 bestanden) und die Solution
  baut ohne Warnungen.
