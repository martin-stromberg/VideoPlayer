# Umsetzungsplan — N22: Videos niemals kürzen (Überlauf erlaubt)

## Ziel-Semantik

1. Ein Item wird eingeplant, wenn sein **Start** mindestens `MinStartRemain` (Default 30 s,
   konfigurierbar via `ProgramSchedule:MinStartRemainSeconds`) vor der nächsten
   Occurrence-Grenze liegt — es läuft dann **immer vollständig** (`Slot.End` = reales
   Item-Ende, auch > Fensterende).
2. Grenzen werden zweigeteilt:
   - **weich** (Folge-Occurrence hat `Priority <=` dieser Regel): Item startet → läuft aus;
     die Folge-Materialisierung beginnt erst am Überstand-Ende („verschiebt sich").
   - **hart** (Folge-Occurrence hat `Priority >` dieser Regel): Item wird nur platziert, wenn
     es vollständig vor der Grenze endet; andernfalls endet die Ausstrahlung — das Item bleibt
     am Cursor für die nächste eigene Occurrence (kein Konsum, kein Verwerfen der
     Gesamt-Ausstrahlung mehr).
3. Ein Slot, der von einer früheren Ausstrahlung über den Occurrence-Start hinausragt
   (Invasor), wird **nie verdrängt/gekürzt** — die Materialisierung shiftet den effektiven
   Start hinter sein Ende (gilt für `blocked`-Bestand und frisch erzeugte `newSlots`
   gleichermaßen, in `EnsureCoverage` wie im Reconcile).
4. Kein Konsum-Pfad mehr: `SelectAiringItems` gibt kein `consumed` zurück (außer Signatur-
   behaltung mit 0). Gilt analog für `SelectAiringItemsInterleaved` (keine `null`-Picks mehr).

## Änderungen

### `ProgramScheduleOptions` (`VideoWebPlayer/Configuration/ProgramScheduleOptions.cs`)

- `public int MinStartRemainSeconds { get; set; } = 30;`
- `public TimeSpan EffectiveMinStartRemain => MinStartRemainSeconds > 0 ? FromSeconds(...) : FromSeconds(30);`

### `ProgramScheduleService`

- `BoundaryAfter` → liefert `(DateTime StartUtc, bool Hard)?`: erster Bound-Start nach
  `afterUtc` **beliebiger** Priorität; `Hard = boundPriority > occurrencePriority`.
  (`BoundStarts` unverändert.)
- `SelectAiringItems` / `SelectAiringItemsInterleaved`:
  - Parameter `softEndUtc` → `(DateTime StartUtc, bool Hard)? bound` + `TimeSpan minRemain`.
  - Platzieren, wenn `bound is null` oder `bound.StartUtc − slotStart ≥ minRemain` **und**
    (`!bound.Hard` oder `slotStart + duration ≤ bound.StartUtc`). Sonst: Auswahl endet
    (Item verschoben, nicht konsumiert).
  - `consumed` entfällt (Rückgabe nur noch `Selected`); `SelectAndCommitConsumed` wird zu
    `SelectItems` o. ä. ohne Commit-Seiteneffekt; `CommitTaken` bleibt.
  - Interleaved: `picks` ohne `null`-Einträge → `(int Strand, SchedulableItem Item)`;
    Strang ist `done`, wenn sein Kopf nicht mehr platziert werden darf.
- `MaterializeOccurrence` + `MaterializeOccurrenceEvicting`:
  - Vor der Auswahl: `effectiveStart = max(occ.StartUtc, max End aller Intervalle in
    blocked/newSlots/keptSlots mit Start < occ.StartUtc < End)` (Invasor-Überstand).
  - Auswahl und Slot-Kette laufen ab `effectiveStart` mit derselben nominalen Grenze.
  - Konflikt-/Evict-Prüfung unverändert auf den resultierenden Ranges.
- `ReconcileAsync` (Caller von `MaterializeOccurrenceEvicting`):
  - Invasor-Zweig (`loser.Start < occurrence.StartUtc`) + `DisplaceAiringSuffix` entfallen —
    Invasoren erscheinen nicht mehr als Loser (Ranges starten hinter ihnen).
- `AiringReproducible`: Occurrence-Anker auf `∃ o ∈ occurrenceStarts: o <= airing[0].Start
  − skipped` lockern; Simulation mit `simStart = airing[0].Start − skipped` (reeller
  Airing-Start) und `bound = BoundaryAfter(boundStarts, o, priority)`.
- `AiringReproducibleInterleaved`: analog — `simStart = airing[0].Start −` Summe der
  geschriebenen Präfix-Picks; `o <= simStart`; ohne `null`-Picks vereinfacht sich
  `writtenOffset = p`.

### Tests (`ProgramScheduleServiceTests_Boundary.cs` + neue Datei ggf. `..._Overshoot.cs`)

- Bestand an neue Semantik anpassen (siehe `inventory/betroffene-tests.md`):
  - `AdjacentWindows`: Überzug über 16:00 erlaubt, Nachfolger startet am Überstand-Ende.
  - `InvadingPredecessorTail`: Invasor bleibt `Scheduled`, Folge-Slots ab 16:37.
  - `OversizedHeadItem`: 300-min-Film wird eingeplant + überzieht → umbenennen
    (`IsScheduledAndOvershoots` o. ä.).
- Neu:
  - `EnsureCoverage_ShortRemainder_OvershootingItem_Completes` (3 min Rest, 45-min-Item →
    `End >` Fensterende; Folge-Regel shifted).
  - `EnsureCoverage_RemainderBelowMinRemain_PlansFiller` (Rest < 30 s → kein Regel-Item,
    Lücke gefüllt bzw. `RemainingGapSeconds`).
  - `Reconcile_OvershootingTail_StaysScheduled_SuccessorShifts` (Invasor + Folge-Regel).
  - `Reconcile_ShiftedAiring_StaysStableAcrossRuns` (Stabilität über ≥3 Läufe).
  - `HardBound` — höherpriore Folge-Occurrence: nicht passendes Item wird verschoben
    (nächste eigene Occurrence bekommt es), niedrigere Items vor der Grenze laufen.
  - `ProgramScheduleOptions_MinStartRemain` — Normalisierung (0/negativ → 30 s).

### Doku

- `docs/help/tv-sender.md` — Abschnitt „Sendeplan": Überlauf-Regel + Konfigurationsschlüssel.
- `docs/RELEASE_NOTES.md` — ein Bullet unter „What's New".

## Verifikation

- `dotnet build VideoPlayer.sln` Debug **und** Release, 0 Fehler/Warnungen (laufende
  Benutzer-App sperrt ggf. `bin/Debug` → nie beenden, nötigenfalls `-p:OutDir`).
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` synchron, komplett —
  Ziel: bisherige Basis grün + neue Tests; Flakes einzeln wiederholen.
- Regression: kurz die Schwellen-Logik brechen → neue Tests müssen rot werden (mental/gezielt).
