# Bestandsaufnahme: TV-Sender – Airings > 24 h bleiben in `AiringReproducible` nicht invalid (Issue #261)

Analysiert wurde der Sendeplan-Bereich (`ProgramScheduleService` und Umfeld) bezogen auf die
Anforderung in `requirement.md`: `ReconcileAsync` wertet Airings als nicht reproduzierbar, sobald
ihr realer Beginn mehr als 24 Stunden vor `coverageStart` liegt, und verdrängt sie.

## Zusammenfassung

- **Bug-Stelle bestätigt:** `OccurrenceLookback = 24 h` (`ProgramScheduleService.cs` Z. 26) ist die
  einzige Lookback-Konstante und wirkt an zwei Stellen: dem Occurrence-Fensterfilter in
  `BuildOccurrences` (Z. 1119) — gemeinsam genutzt von `EnsureCoverageAsync` (Z. 113) und
  `ReconcileAsync` (Z. 188) — sowie dem Slot-Ladefenster `loadFrom` in `ReconcileAsync` (Z. 175).
- **Verdrängungsmechanik:** `AiringReproducible` bricht ab, sobald der rekonstruierte reale
  Beginn `airingStart < earliestStart` ist (Z. 510–511); `earliestStart` ist das Minimum der
  Occurrence-Starts im Lookback-Fenster. `AiringReproducibleInterleaved` iteriert nur
  `occurrenceStarts <= airing[0].Start` (Z. 602) — für einen >24 h zurückliegenden Beginn gibt es
  schlicht keinen Kandidaten. Beide Pfade liefern `false` → `Displaced` → Neu-Materialisierung.
- **Zweite Fenstergrenze:** `BuildOccurrences` iteriert außerdem nur Tage von
  `coverageStart`-Tag − 1 bis `horizonEnd`-Tag (Z. 1096–1102) — Occurrences älter als ~2 Tage
  werden unabhängig vom Lookback-Wert gar nicht erzeugt. Relevant für einen vergrößerten
  Lookback (Variante A der Anforderung).
- **Ladefenster-Kopplung:** `loadFrom` deckt nur `End > coverageStart − 24 h` ab (Z. 175–177);
  Kopf-Slots und `invaderStarts`-Evidenz sehr langer Airings liegen teilweise außerhalb
  (Akzeptanzkriterium 4). Es gibt **keine** obere Start-Grenze — überschießende Tails werden
  bewusst komplett geladen (Kommentar Z. 170–174).
- **Konfiguration:** `ProgramScheduleOptions` enthält `TrailerLeadDays` und `MinStartRemainSeconds`
  mit `Effective…`-Normalisierung (Muster für eine evtl. neue Lookback-Obergrenze); für den
  Lookback existiert keine Option. `appsettings.json` setzt nur `TrailerLeadDays: 7`.
- **Aufrufer:** `ProgramScheduleWorker` führt `ReconcileAsync` dann `EnsureCoverageAsync` im
  konfigurierten Intervall (Default 15 min) hinter `IBackgroundProcessingGate` aus — die
  Verdrängung wiederholt sich also im Betrieb zyklisch.
- **Interface:** `IProgramScheduleService` ist ein interner Vertrag (6 Methoden + 4 Summary-
  Records); die Korrektur ist voraussichtlich interface-stabil.
- **Tests:** 12 `ProgramScheduleServiceTests_*`-Suiten (76 Tests) existieren; geteilte Fixture
  `ProgramScheduleTestDb` (echtes In-Memory-SQLite, FK aktiv) baut Szenarien aus
  Material + `CreateRuleAsync` + `EnsureCoverageAsync` + `ReconcileAsync` auf. Die vorhandenen
  Reproduzierbarkeits-Tests (`_Reconcile`, `_Boundary`, `_Interleaving`) decken Teil-Airings,
  Verschiebungen und Overshoots ab — **alle innerhalb des 24-h-Fensters; ein >24-h-Airing
  testet bisher nichts**. Lange Items sind per `CreateMovieAsync(duration: TimeSpan…)`
  abbildbar (Präzedenz: 300 min). Direktes Seeden von Slots geht über `CreateSlotAsync`.

**Test-Ausgangszustand:** vollständig grün — Unit-Suite (`Category!=E2E`) 2012/2012 in 1:21 min,
E2E-Suite (`Category=E2E`) 225/226 mit 1 absichtlich übersprungenem Test in 4:13 min, kein
Flackern. Nachweise und Details: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `ProgramSlot`, `ProgramRule`, `ProgramRuleCursor`
- [Logik](inventory/logic.md) — `ProgramScheduleService` (vollständige Methodenlandkarte),
  `ProgramScheduleWorker`, `ProgramSettingsService`, `ProgramScheduleOptions`
- [Enums](inventory/enums.md) — `ProgramSlotStatus`, `ProgramRuleType`, `ProgramRuleRecurrence`,
  `ProgramRuleMaterialType`, `ProgramCursorContentType`
- [Interfaces](inventory/interfaces.md) — `IProgramScheduleService`, `IProgramScheduleSignal`
- [Tests](inventory/tests.md) — Test-Baseline, bereichsbezogene Testklassen, Fixture-Hilfsmethoden,
  Muster für einen Reproduktionstest
