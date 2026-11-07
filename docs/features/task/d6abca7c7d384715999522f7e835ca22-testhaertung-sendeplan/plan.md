# Umsetzungsplan: Sender-Testhärtung — nicht sichergestellte Erwartungen

## Übersicht

Die in `issue.md` (GitHub-Issue #344) gelisteten Blindstellen B1–B15 der
Sendeplan-Testsuite werden durch verstärkte Assertions geschlossen: Der zentrale Helper
`AssertGapless` wird auf echte Lücken- **und** Überlappungsprüfung verschärft, danach die
einzelnen Teststellen auf fachliche Invarianten (Sequenz-, Mengen-, Zeit-, Verteilungs-
und Nebeneffekt-Vergleiche) statt Mechanismus-/Existenz-Checks umgestellt. Wird eine
verstärkte Assertion gegen den Ist-Code rot, liegt ein Produktiv-Bug vor, der im selben
Auftrag behoben wird (separater `fix:`-Commit).

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein Sendeplan-Lauf (`EnsureCoverageAsync`/`ReconcileAsync`) bzw. eine `ProgramScheduleWorker`-Iteration, jeweils als Testfall in `VideoWebPlayer.Tests` gegen echtes SQLite (`ProgramScheduleTestDb`) bzw. Fakes |
| Auslösende Ereignisse | Ausführung der Testsuite (`dotnet test`); keine Laufzeit-Ereignisse |
| Erwartete Wirkung | Jede verstärkte Assertion schlägt bei Bruch der benannten fachlichen Invariante fehl und bleibt gegen korrekten Ist-Code grün; bei Rot liegt ein Produktiv-Bug vor → Fix |
| Umgang mit fehlenden Objekten | Für B14: neue Fakes für `IProgramScheduleService`, `IProgramScheduleSignal`, `IBackgroundProcessingGate` sowie ein vorrückbarer `TimeProvider` (in `VideoWebPlayer.Tests/Helpers/` erzeugen); B9-Zieltest existiert auf diesem Branch nicht → nicht erzeugen (siehe Ausschluss 2) |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | `PeriodStart == null`-Pfad (`ProgramScheduleService.cs:3114-3126`) und Concurrent-Reschedule-Races werden nicht abgesichert | Explizites Nicht-Ziel der Anforderung — wird dokumentiert, optional Folgeaufgabe | bestätigt (Anforderung, issue.md „Nicht-Ziele") |
| 2 | B9 (`served.Count == 0` → Mindest-Tageszahl) kann nicht gehärtet werden | Der Zieltest `EnsureCoverage_SecondRun_ContinuesDisjointAcrossRunBoundary` existiert nur auf dem ungemergten #343-Fix-Branch `task/d6abca7c7d384715999522f7e835ca22-strang-ueberlappung`; auf diesem Branch gibt es nichts zu ändern. Dokumentierter Nicht-Befund — beim Merge von #343 ist die Stelle mitzuhärten | bestätigt (Anforderung: #343-Fix-Stand prüfen — er ist nicht in staging) |
| 3 | E2E-/Playwright-Blindflecken (`TVSenderAdmin*Tests`, `ProgramGuideE2ETests`, `ScheduleDisplayItemsTests`, `ProgramScheduleSignalTests`, `ProgramGuideServiceTests_ClipGroups`, `VideoWebPlayerBackupDataTests_*`) | Explizites Nicht-Ziel der Anforderung — nicht analysiert, ggf. separater Lauf | bestätigt (Anforderung, issue.md „Nicht-Ziele") |
| 4 | Spröde Gegen-Assertions (`ClipGroups.cs:232-237`, `PrimetimeGrace.cs:139-144`) werden nicht gelockert | Geprüft: beide Szenarien säen volle Gruppen (8 bzw. 4 Clips je Gruppe), ein legitimes Überspringen ist nicht gegeben — die strikte Alternation ist korrekt scharf | bestätigt (Anforderung: „bewerten, ggf. lockern" — Bewertung: nicht nötig) |
| 5 | Reale End-to-End-Abnahme gegen die Installation `http://192.168.10.230:83/` | Dokumentierter Verzicht durch den Anwender (Auftrag, 2026-11-09): Testhärtung betrifft keinen Auslieferungs-Code-Pfad, der E2E-Nachweis erfolgt über die Testsuite | bestätigt (Anwender, 2026-11-09) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `AssertGapless` (B1) | Strikte Kettenprüfung: erster Fenster-Slot darf `<= from` beginnen (Fensterkante überspannend), danach muss `slot.Start == cursor` gelten — `> cursor` = Lücke, `< cursor` = Überlappung; `cursor >= to` am Ende | Signatur und Aufrufer bleiben unverändert kompatibel; ein Slot, der die Fensterkante überspannt (laufender Slot), ist ein legitimer Fall in den Aufrufern |
| B8 „StaysScheduled" | `AssertReconcileNoOpAsync` (Displaced=0 && Created=0), wenn `ReconcileAsync` am jeweiligen Zeitpunkt keinen legitimen Horizont-Nachbau meldet; sonst `AssertNoDisplacedAsync` + Mengen-Gleichheit `(Start,End,MediaItemId,ProgramRuleId)` der vorher geplanten Slots | Reconcile markiert Bestandsslots nur Displaced oder legt neue an — verändert sie nie; die einzig verbleibende Blindstelle ist `SlotsCreated > 0` bzw. inhaltlich umgeschriebene Slots |
| B14 Intervall-Neulesung | Neuer `ScheduleTestTimeProvider` (Helper): Timer werden bei `CreateTimer` mit Fälligkeitszeitpunkt registriert und bei `Advance(delta)` in Reihenfolge gefeuert — `Task.Delay(span, provider, ct)` läuft über `CreateTimer` | Deterministisch; `Microsoft.Extensions.TimeProvider.Testing` ist nicht referenziert (kein neues NuGet-Paket für einen Test-Helper) |
| B14 Worker-Fakes | `IProgramScheduleService`/`IProgramScheduleSignal`/`IBackgroundProcessingGate` als Fakes in `Helpers/`; `ProgramSettingsService` konkret über echtes SQLite (Setups-Row steuerbar); neue Testklasse `ProgramScheduleWorkerTests_SideEffects` | test-class-structure: separates Thema (Nebeneffekte statt Pass-Ergebnisse); `ProgramSettingsService` ist konkret und über `GetRequiredService<ProgramSettingsService>` aufgelöst, daher echte DB statt Interface-Fake |
| Ringfortsetzung B2/B3 | Ring aus der beobachteten Periodizität der ersten Lauf-Folge ableiten (Periodenlänge = Clip-Anzahl), dann Naht/Fortsetzung gegen das erwartete Ringelement assertieren | `ItemOrderKey`-Ordnung ist Implementierungsdetail; der beobachtete Zyklus ist die fachliche Ringordnung — ein Bruch der Zyklizität schlägt ebenfalls fehl |

## Programmabläufe

### B1: `AssertGapless` verschärfen

1. `AssertGapless` in `ProgramScheduleTestDb.cs` umbauen: Iteration über die im Fenster
   `[from, to)` liegenden Slots (geordnet nach `Start`); pro Slot:
   - `slot.Start > cursor` → `Assert.Fail` „Lücke"
   - `slot.Start < cursor` → `Assert.Fail` „Überlappung" — **außer** es ist der erste
     Fenster-Slot und `slot.Start <= from` (überspannt die Fensterkante)
   - sonst `cursor = slot.End`
2. Abschluss `cursor >= to` unverändert.
3. Alle ~9 Aufrufer unverändert; Lauf der betroffenen Tests gegen Ist-Code — Rot =
   bisher maskierte Überlappung → Produktiv-Bug separat fixen.

Beteiligte Klassen/Komponenten: `ProgramScheduleTestDb`, alle Aufrufer-Testklassen.

### B2–B6, B10–B13, B15, B7, B8: Einzelassertions verstärken

Je Befund die Assertion an der benannten Stelle wie unter „Änderungen an bestehenden
Klassen" beschrieben verschärfen; jede sofort gegen Ist-Code laufen lassen
(`dotnet test --filter <Testname>`).

### B14: Worker-Nebeneffekt-Tests

1. `ScheduleTestTimeProvider` in `Helpers/` anlegen (vorrückbarer Zeitgeber: `Advance`
   feuert fällige Timer).
2. Fakes in `Helpers/` anlegen: `FakeProgramScheduleService` (programmierbare
   `ProgramSchedulePassSummary`-Folge + Aufrufzähler), `FakeScheduleSignal`
   (`NotifyLiveScheduleChanged`-Zähler, `WaitAsync` nie abgeschlossen),
   `FakeBackgroundProcessingGate` (gemeinsames Ablauf-Log „gate:enter"/„pass"/„gate:exit").
3. Neue Testklasse `ProgramScheduleWorkerTests_SideEffects` (Setup-Muster wie
   `ProgramScheduleWorkerTests`: ServiceCollection + echtes In-Memory-SQLite +
   `ProgramSettingsService`):
   - `Worker_PassWithChanges_NotifiesLiveScheduleChanged` — `Reconcile.SlotsDisplaced=1`
     → Signal-Zähler > 0.
   - `Worker_PassWithoutChanges_DoesNotNotifyLiveScheduleChanged` — alles 0 → Zähler 0.
   - `Worker_Run_WrapsPassInGateLease` — Ablauf-Log: `gate:enter` < `pass` < `gate:exit`,
     Name `"Sendeplan"`.
   - `Worker_RereadsIntervalEachIteration` — `ProgramScheduleIntervalMinutes` 60 → Pass 1;
     zwischen den Pässen auf 1 stellen; `Advance(60min)` → Pass 2; `Advance(1min)` →
     Pass 3 (ohne Neulesung wären +60 nötig).

Beteiligte Klassen/Komponenten: `ProgramScheduleWorker`, `IProgramScheduleSignal`,
`IBackgroundProcessingGate`, `ProgramSettingsService`, neue Helpers/Fakes.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ScheduleTestTimeProvider` | Test-Helper (`VideoWebPlayer.Tests/Helpers/`) | `TimeProvider` mit `Advance(delta)`, der registrierte Timer fälligkeitsgeordnet feuert |
| `FakeProgramScheduleService` | Test-Fake (`Helpers/`) | `IProgramScheduleService`: programmierbare Pass-Ergebnisse, Aufrufzähler, Ablauf-Log-Eintrag „pass" |
| `FakeScheduleSignal` | Test-Fake (`Helpers/`) | `IProgramScheduleSignal`: zählt `NotifyLiveScheduleChanged`, `WaitAsync`/`WaitLiveAsync` pending |
| `FakeBackgroundProcessingGate` | Test-Fake (`Helpers/`) | `IBackgroundProcessingGate`: Lease mit Ablauf-Log „gate:enter"/„gate:exit" |
| `ProgramScheduleWorkerTests_SideEffects` | Testklasse | B14: Signal-, Gate- und Intervall-Nebeneffekte des Workers |

## Änderungen an bestehenden Klassen

### `ProgramScheduleTestDb` (Test-Helper)

- **Geänderte Methoden:** `AssertGapless` — strikte Kettenprüfung (Lücke, Überlappung,
  Fensterkanten-Ausnahme für den ersten Slot) wie unter „Programmabläufe" beschrieben;
  Fehlertexte differenziert (Lücke vs. Überlappung).

### `ProgramScheduleServiceTests_ClipGroups`

- **Geänderte Tests:**
  - `EnsureCoverage_ClipRing_RotatesAcrossRuns` (B2): Ring aus Tag-1-Periodizität (5 Clips)
    ableiten und Tag-2-Folge gegen die erwartete Fortsetzung `ring[(12 + i) % 5]`
    assertieren (statt nur `NotEqual` des ersten Clips).
  - `EnsureCoverage_TrailerGroup_AirsTrailerBeforeScheduledMovie` (B6): Lead-Fenster-Bezug
    `movie.Start ∈ (trailer.Start, trailer.Start + TrailerLeadDays]` assertieren
    (`TrailerLeadDays`-Default aus `ProgramScheduleOptions`).
  - `EnsureCoverage_InterleavedRule_ClipFillDistributed` (B13): lückenlose Kette
    (`End[i-1] == Start[i]`) + Clip-Verteilung über ≥2 Segmente (vor/zwischen/nach
    Titeln) assertieren.

### `ProgramScheduleServiceTests_ClipSource`

- **Geänderte Tests:** `EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns` (B3):
  dritten Clip säen; Ring aus Lauf-1-Periodizität ableiten; Naht gegen das erwartete
  nächste Ringelement `ring[(indexLastBefore + 1) % 3]` assertieren.

### `ProgramScheduleServiceTests_StrandRotation`

- **Geänderte Tests:** `EnsureCoverage_EpisodeCountBelowStrandCount_StillRotates` (B4):
  `leads.Distinct().Count() == 3` (alle drei Stränge rotieren über 4 Abende).

### `ProgramScheduleServiceTests_AgeWindow`

- **Geänderte Tests:**
  - `EnsureCoverage_StartedItem_RunsPastNightEnd` (B5): zusätzlich
    `s.End - s.Start == Laufzeit (90 min)` und `End` jenseits des Nachtendes (06:00).
  - `EnsureCoverage_WeeklyRule_StartedItemRunsPastNightEnd` (B5): dito.
  - `EnsureCoverage_MpaaRatedMovie_RespectsMappedWindow` (B11): im erlaubten Fall
    `LocalMinutes(s.Start) == allowedHour*60 + allowedMinute` (Regel-`TimeOfDay`).

### `ProgramScheduleServiceTests_Generation`

- **Geänderte Tests:**
  - `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder` (B12): vollständige Folge
    nach dem zweiten Lauf gegen die zyklische Fortsetzung ab dem Cursor
    (`episodes[(4 + i) % 5]`) vergleichen — kein Ring-Neustart.
  - `EnsureCoverage_SlotDuration_FillsUntilTarget` (B10): pro Ausstrahlungstag beweisen,
    dass nach Erreichen des Slot-Targets (150 min; Overshoot des begonnenen Titels
    erlaubt) kein weiterer Titel derselben Ausstrahlung folgt.

### `ProgramScheduleServiceTests_Reconcile`

- **Geänderte Tests:**
  - `Reconcile_PartiallyAiredOccurrence_TomorrowAiringsStayStable` (B8): zusätzlich
    `summary.SlotsDisplaced == 0` bzw. `AssertReconcileNoOpAsync` und Mengen-Gleichheit
    statt Teilmenge.
  - `GetSchedule_ReturnsUpcomingSlotsWithStatus` (B7): `OrderBy(Start)`-Sequenz und
    `maxCount`-Take assertieren.
  - `Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip` (B15):
    vollständige Episodenfolge über alle Ausstrahlungstage
    (`E1,E2 | E4,E5 | E6,E7 …` laut Horizont) statt `Any(SequenceEqual)`; toter
    `var scheduled`-Aufruf in :282 entfernen.

### `ProgramScheduleServiceTests_RingOrder`, `_CollectionOrder`, `_Interleaving`, `_Trailer`

- **Geänderte Tests:** die „StaysScheduled"-Reconcile-Tests (B8) — `AssertNoDisplacedAsync`
  durch `AssertReconcileNoOpAsync` ersetzen, sofern `ReconcileAsync` an der Stelle keinen
  legitimen `SlotsCreated > 0` meldet; andernfalls `AssertNoDisplacedAsync` +
  Mengen-Gleichheit der vorher geplanten Slots `(Start,End,MediaItemId,ProgramRuleId)`.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — reine Testhärtung. Die „Validierung" ist der Rot/Grün-Nachweis je Assertion.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`AssertGapless`-Verschärfung:** kann bisher maskierte echte Überlappungen aufdecken
  → Produktiv-Bug; Fix im selben Auftrag (separater Commit).
- **`AssertReconcileNoOpAsync` in B8:** kann rot werden, wenn `ReconcileAsync` am
  Horizont-Ende legitim nachbaut — dann dokumentierte Fallback-Variante (Mengen-
  Gleichheit), kein Aufweichen auf `SlotsDisplaced == 0` allein.
- **#343-Fix-Branch:** ändert dieselbe Datei `ProgramScheduleServiceTests_StrandRotation.cs`
  (B4-Stelle) und enthält den B9-Zieltest — beim späteren Merge von
  `task/…-strang-ueberlappung` beide Stellen erneut ansehen.
- **Worker-Tests mit Fakes:** laufen gegen `ExecutePassAsync`-Interface — eine
  Signaturänderung am Interface würde den Fake brechen (gewollt).

## Umsetzungsreihenfolge

1. **B1: `AssertGapless` verschärfen**
   - Voraussetzungen: Keine.
   - Beschreibung: Helper umbauen; alle Aufrufer-Tests laufen lassen; bei Rot
     Produktiv-Ursache diagnostizieren und separat fixen.
2. **B2–B6, B7, B10–B13, B15: Einzel-Assertions in den genannten Testklassen**
   - Voraussetzungen: Keine (unabhängig voneinander).
   - Beschreibung: Je Befund Assertion ersetzen/ergänzen und den Einzeltest laufen
     lassen; bei Rot → Produktiv-Bug.
3. **B8: StaysScheduled-Tests**
   - Voraussetzungen: Keine.
   - Beschreibung: `AssertReconcileNoOpAsync`-Variante probieren; bei legitimer
     Horizont-Verlängerung Mengen-Gleichheit-Variante.
4. **B14: Worker-Nebeneffekte**
   - Voraussetzungen: neue Helpers/Fakes (Schritt im selben Block, zuerst anlegen).
   - Beschreibung: TimeProvider + 3 Fakes + `ProgramScheduleWorkerTests_SideEffects`
     mit 4 Tests.
5. **Gesamtverifikation**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `dotnet build` Debug + Release, volle Suite; Gegenprobe für B1
     (künstlich überlappter Slot muss `AssertGapless` fehlschlagen lassen —
     verifiziert über einen temporären Gegenbeweis-Test oder kurzzeitige Mutation).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ScheduleTestTimeProvider` | `Helpers/` | Vorrückbarer Zeitgeber für `Task.Delay(span, provider, ct)` |
| `FakeProgramScheduleService` | `Helpers/` | Programmierbare Pass-Ergebnisse + Aufrufzähler/Ablauf-Log |
| `FakeScheduleSignal` | `Helpers/` | `NotifyLiveScheduleChanged`-Zähler |
| `FakeBackgroundProcessingGate` | `Helpers/` | Lease-Enter/Exit-Reihenfolge-Log |
| `Worker_PassWithChanges_NotifiesLiveScheduleChanged` | `ProgramScheduleWorkerTests_SideEffects` | Displaced>0/Created>0 → Live-Signal feuert |
| `Worker_PassWithoutChanges_DoesNotNotifyLiveScheduleChanged` | `ProgramScheduleWorkerTests_SideEffects` | Alles 0 → kein Live-Signal |
| `Worker_Run_WrapsPassInGateLease` | `ProgramScheduleWorkerTests_SideEffects` | Enter vor Pass, Exit nach Pass, Name „Sendeplan" |
| `Worker_RereadsIntervalEachIteration` | `ProgramScheduleWorkerTests_SideEffects` | Intervall-Änderung wirkt ohne Neustart |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `EnsureCoverage_ClipRing_RotatesAcrossRuns` (ClipGroups) | B2 |
| `EnsureCoverage_TrailerGroup_AirsTrailerBeforeScheduledMovie` (ClipGroups) | B6 |
| `EnsureCoverage_InterleavedRule_ClipFillDistributed` (ClipGroups) | B13 |
| `EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns` (ClipSource) | B3 |
| `EnsureCoverage_EpisodeCountBelowStrandCount_StillRotates` (StrandRotation) | B4 |
| `EnsureCoverage_StartedItem_RunsPastNightEnd`, `EnsureCoverage_WeeklyRule_StartedItemRunsPastNightEnd`, `EnsureCoverage_MpaaRatedMovie_RespectsMappedWindow` (AgeWindow) | B5, B11 |
| `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder`, `EnsureCoverage_SlotDuration_FillsUntilTarget` (Generation) | B12, B10 |
| `Reconcile_PartiallyAiredOccurrence_TomorrowAiringsStayStable`, `GetSchedule_ReturnsUpcomingSlotsWithStatus`, `Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip` (Reconcile) | B8, B7, B15 |
| `Reconcile_MixedRing_StaysReproducible` (RingOrder), `Reconcile_OrderedCollectionRing_StaysReproducible` (CollectionOrder), `Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled` (Interleaving), `Reconcile_KeptTrailerSlot_StaysScheduled` (Trailer) | B8 |
| Alle `AssertGapless`-Aufrufer (Reconcile ×4, ContentScope, Filler ×2, Trailer, Generation ×2) | B1 — keine Änderung am Aufruf, Verhalten wird strenger |

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich — die Anforderung berührt keinen Benutzerfluss: Sie
härtet ausschließlich Assertions vorhandener Unit-/Integrations-Tests sowie
Worker-Nebeneffekte auf Service-Ebene. Kein UI-, CLI- oder API-Pfad ändert sich;
ein reales Benutzer-Szenario existiert nicht, das E2E nachweisen müsste.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Verstärkte Assertions grün gegen Ist-Code (Happy Path) | `dotnet test` | Unveränderter Produktivcode | Alle B-Punkte umgesetzt; gefilterte Läufe + volle Suite | Jede verstärkte Assertion besteht → Lücke geschlossen ohne Produktiv-Bug | test-results.md |
| 2 | Verstärkte Assertion rot → Produktiv-Bug | `dotnet test` | Unveränderter Produktivcode | Betroffene Ursache analysiert, Produktiv-Fix in eigenem Commit | Test grün nach Fix; Fix im Bericht benannt | test-results.md, Commit-Historie |
| 3 | Gegenbeweis `AssertGapless` (B1) | Mutationsprüfung | Verschärfter Helper | Künstlich überlappende Slot-Kette gegen `AssertGapless` | Assertion schlägt fehl (Nachweis, dass die Überlappungsprüfung wirklich feuert) | test-results.md / Hinweis im Bericht |

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Meldet `ReconcileAsync` an den B8-Stellen legitim `SlotsCreated > 0` (Horizont-Verlängerung)? Erst zur Laufzeit entscheidbar | Zuerst `AssertReconcileNoOpAsync` probieren; bei legitimer Erzeugung auf `AssertNoDisplacedAsync` + Mengen-Gleichheit der vorher geplanten Slots zurückgehen (kein stilles Aufweichen) — Annahme: Reconcile baut nicht legitim am Horizont-Ende nach (Extend ist Aufgabe von `EnsureCoverageAsync`), `Reconcile_NoChanges` zeigt nur, dass er es *darf* |
| 2 | Exakte erwartete Ring-/Episodenfolgen (B2, B3, B12, B15) und Clip-Verteilung (B13) sind erst gegen den Ist-Lauf final bezifferbar | Erwartungswerte aus dem tatsächlichen Ist-Lauf ableiten und als Invarianten-Formel (nicht als hart kopierte ID-Liste) assertieren; wo nur eine harte Folge aussagekräftig ist, mit Begründungs-Kommentar festhalten |
| 3 | B9: Härtung des `served.Count == 0`-Abbruchs gehört fachlich zum #343-Branch | Hier dokumentiert lassen (Scope-Ausschluss 2); beim späteren #343-Merge Mindest-Tageszahl nachrüsten — Hinweis an den Anwender im Abschlussbericht |
