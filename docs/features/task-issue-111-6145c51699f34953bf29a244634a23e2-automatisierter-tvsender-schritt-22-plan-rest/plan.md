# Umsetzungsplan – Schritt 22 (N16-Rest + N18)

## Teil A — Fenstergrenze für Occurrences (N16-Rest)

### `ProgramScheduleService.cs`

1. **Neuer Helper `SelectAiringItems`** (statisch, ersetzt die doppelte
   `taken`-Akkumulation in `MaterializeOccurrence` und `MaterializeOccurrenceEvicting`):

   ```csharp
   private static (List<SchedulableItem> Selected, int ConsumedLeading) SelectAiringItems(
       List<SchedulableItem> items, int headIndex, ProgramRule rule,
       DateTime occurrenceStartUtc, DateTime? softEndUtc)
   ```

   - `wanted`/`target` wie bisher (EpisodeCount | SlotDuration | 1).
   - Ring-Walk ab `headIndex`: bricht ab, wenn `slotStart + item.Duration > softEnd`
     (Airing endet vor der Grenze → Unterlauf statt Invasion).
   - **Oversized-Head:** passt das *erste* Item schon nicht vor `softEnd`, wird es als
     `ConsumedLeading` gezählt (Cursor-Konsum ohne Slot — dauerhaft übersprungen,
     Rotation blockiert nicht ewig). Abbruch-Sicherung: `consumed <= items.Count`.
   - Rückgabe: ausgewählte Items + Anzahl konsumierter Kopf-Items.

2. **`BoundaryAfter`-Helper:** sortierte Liste `boundStarts` aller Occurrence-Starts von
   Regeln mit `state.Items.Count > 0`; `BoundaryAfter(starts, afterUtc)` = binäre Suche
   erstes Element `> afterUtc` (streng — gleichzeitige Occurrences begrenzen sich
   gegenseitig nicht; deren Konflikt löst weiterhin Priorität).

3. **`MaterializeOccurrence`** (EnsureCoverage): `softEnd` Parameter; `taken` via
   `SelectAiringItems`; `state.Commit(consumed)` bei `consumed > 0` (auch ohne Slots);
   Rest unverändert (Range-Filter `end > coverageStart`, atomarer Drop).

4. **`MaterializeOccurrenceEvicting`** (Reconcile): gleiche Auswahl + Consume; bei den
   Konflikten Split:
   - `competitors` = kept-Slots mit `Start >= occurrence.StartUtc` → bisherige
     `evicts`-Prüfung (+ `newSlots`-Overlap → Drop).
   - `invaders` = kept-Slots mit `Start < occurrence.StartUtc` → weichen immer;
     werden als Loser zurückgegeben und vom Aufrufer anders behandelt.

5. **`ReconcileAsync` Aufrufer:** für Loser mit `Start < occurrence.StartUtc`
   (Invasor): `DisplaceAiringSuffix(airing, occurrence.StartUtc)` — verdrängt nur die
   Slots der Airing-Gruppe mit `End > occurrence.StartUtc` (nicht die komplette
   Regel-Zukunft); ungruppierte (Filler-)Invasoren einzeln; danach `RewindCursor`.
   Kompetitoren → bisheriges `DisplaceRuleFuture`-Verhalten.

6. **`AiringReproducible`:** zusätzlicher Parameter `boundStarts`; für jeden
   Kandidaten-`occurrenceStart` `bound = BoundaryAfter(boundStarts, occurrenceStart)`;
   erwartete Sequenz via `SelectAiringItems(items, headIndex, rule, occurrenceStart,
   bound)`; `writtenOffset = k - consumed` (Kopf-Konsum + vergangene Items zählen
   beide in `k`); Vergleich `expected[writtenOffset + i]` wie bisher.

7. **`EnsureCoverageAsync`/`ReconcileAsync`:** `boundStarts` einmalig aus
   `occurrences` (nur Regeln mit Items) aufbauen und an die Materialisierung bzw.
   `AiringReproducible` durchreichen.

### Neue Tests (`ProgramScheduleServiceTests_Reconcile.cs`)

- `Reconcile_AdjacentRules_PredecessorOvershoot_KeepsBothWindows` — zwei Regeln
  (z. B. 14:00 4 h + 18:00 4 h, gleiche Priorität), Material des Vorgängers überzieht;
  zweiter Lauf: beide Fenster belegt, Vorgänger-Airing endet ≤ Grenze, idempotent.
- `Reconcile_PartiallyAiredOccurrence_WithInvadingPredecessor_MaterializesTail` —
  Occurrence „heute 16:00, jetzt 16:30": belegter Slot des Vorgängers ragt in das
  Fenster → nur dessen Suffix wird verdrängt, Regel 4 materialisiert Rest-Slots.
- `EnsureCoverage_AiringBoundedByNextOccurrence` — frischer Lauf, Vorgänger-Airing
  endet ≤ nächstem Occurrence-Start, kein `DroppedConflicts`.
- `EnsureCoverage_OversizedHeadItem_IsConsumedWithoutSlot` — Item > Fensterlücke wird
  übersprungen, Folge-Items werden geplant (Cursor rutscht weiter).
- `Reconcile_TrimmedAiring_StaysValidOnNextRun` — gekappte Airing bleibt beim
  Folge-Reconcile `Scheduled` (keine Oszillation).

## Teil B — N18: „Verdrängte bereinigen"

### `IProgramScheduleService` / `ProgramScheduleService`

```csharp
Task<int> DeleteDisplacedSlotsAsync(string userId, long channelId, CancellationToken ct = default);
Task<ProgramScheduleResetResult> ResetScheduleAsync(string userId, long channelId, CancellationToken ct = default);
```

- `RequireAdminAsync` (vorhanden) + Channel-Existenzprüfung.
- `ExecuteDeleteAsync` auf `ProgramSlots WHERE Status == Displaced && TVChannelId` → Anzahl.
- Reset (optional, sauber separierbar): alle Slots des Kanals + `ProgramRuleCursors`
  löschen; liefert Anzahlen. Danach `IProgramScheduleSignal`? — kein Signal im
  Schedule-Service vorhanden; der Worker-Takt baut den Plan ohnehin neu auf.
  → Entscheidung: Reset **wird umgesetzt** (klein, nützlich als Fallback) mit
  eigenem Dialog/Warnhinweis.

### `TVSenderAdmin.razor`

- Sendeplan-Tab Header: Buttons „Verdrängte bereinigen" (danger-outline) und
  „Plan zurücksetzen" (danger, mit Warnhinweis im Dialog).
- Bestätigungsdialoge nach `deleteCandidate`-Muster (`confirmCleanup` /
  `confirmReset` Flags statt generischem Kandidaten).
- Nach Ausführung: `scheduleStatus` = „… Sendeplätze entfernt", `ReloadScheduleAsync`.

### Tests

- `ProgramScheduleServiceTests_Reconcile` oder neue Datei
  `ProgramScheduleServiceTests_Cleanup.cs`:
  - `DeleteDisplaced_RemovesOnlyDisplaced` — Mischung Scheduled/Displaced → nur
    Displaced weg, Rückgabe = Anzahl.
  - `DeleteDisplaced_RequiresAdmin` — Non-Admin → `ForbiddenAccessException`.
  - `ResetSchedule_RemovesSlotsAndCursors` — Slots + Cursor weg.

## Doku

- `docs/help/tv-sender.md`: Abschnitt „Sendeplan" — Bereinigen/Zurücksetzen;
  Satz zur Fensterbegrenzung (Ausstrahlung endet vor Folge-Occurrence).
- `docs/RELEASE_NOTES.md`: Einträge für Fix + Button.
- `nachbesserungen.md`: N16-Nachtrag/N18 auf erledigt markieren? (nicht — Projekt-
  Doku pflegt der Orchestrierer separat; im Abschlussbericht erwähnen)

## Offene Punkte

Keine.
