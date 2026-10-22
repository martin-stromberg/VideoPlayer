# Bestandsaufnahme – Schritt 22

## Befund an der Live-Datenbank (`VideoWebPlayer/Data/WebVideoPlayer.db`, WAL-Stand 16:22)

| Regel | Name | Zeit (lokal) | Länge | Scheduled | Displaced | Cursor |
|-------|------|--------------|-------|-----------|-----------|--------|
| 3 | Fimilien-Mittag | 10:00 | 6 h | 25 (endet 16:37–16:45 lokal) | 116 | 1/8272 |
| 4 | Vorbendserien | 16:00 | 4 h | **0** | 35 | 1/8961 |
| 5 | Filmabend | 20:00 | 4 h | 6 (endet 01:24/02:23 lokal) | 67 | 0/70 |
| 6 | Nachtprogramm | 00:00 | 6 h | **0** | 0 | keiner |
| 7 | Morgenprogramm | 06:00 | 4 h | **0** | 4374 | 1/22864 |

Alle Regeln: `RuleType=GenreRotation(2)`, `Recurrence=Daily`, `MaterialType=Genre(3)`,
`Priority=0`. **Keine Filler-Regel** — Lücken bleiben ungefüllt. Regel 4 wird durch den
Überstand von Regel 3 blockiert (Id 3 < 4), Regel 6 durch Regel 5 (5 < 6), Regel 7 durch
eigenen Überstand über Regel 3. Genau drei Regeln tot — konsistent mit dem Befund.

## Relevante Codestellen (`VideoWebPlayer/Services/ProgramScheduleService.cs`)

- `EnsureCoverageAsync` (~Z. 63–126): `coverageStart = max(now, lastEnd)`; `blocked` =
  Scheduled ∩ [coverageStart, horizonEnd); `occurrences` via `BuildOccurrences`
  (Lookback 24 h); `MaterializeOccurrence` je Occurrence (atomarer Konflikt-Drop).
- `ReconcileAsync` (~Z. 129–338): Validierung via `GroupAirings` + `AiringReproducible`;
  `DisplaceRuleFuture` (Regel-Suffix), `Evicts` (Priorität, Tiebreak Id);
  `MaterializeOccurrenceEvicting` (Eviction-Pfad); `FillGaps`.
- `MaterializeOccurrence` (~Z. 793): `taken` akkumuliert bis `≥ target` → **Überstand
  ins nächste Regelfenster**; Drop bei Overlap mit `blocked`/`newSlots`.
- `MaterializeOccurrenceEvicting` (~Z. 541): gleiche `taken`-Logik; Konflikte gegen
  `keptSlots` + `newSlots`.
- `AiringReproducible` (~Z. 426): Rückwärtslauf über Item-Ring, prüft Sequenz.
- `RuleState` (~Z. 631): `PeekAt`/`Commit`/`RewindTo`; `NextIndex` kumulativ.
- `RewindCursor` (~Z. 511): Re-Air verdrängter Inhalte am nächsten Occurrence.
- `SummaryCounters` (~Z. 658): `DroppedConflicts` u. a.

## UI (`VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`)

- Sendeplan-Tab Z. 303–358: Tabelle `schedule`, Button „Jetzt generieren"
  (`GenerateNowAsync`, Z. 798), Status `scheduleStatus`.
- Bestätigungsdialog-Muster: `deleteCandidate` (Z. 586–600, `AskDelete`/`DeleteRuleAsync`).
- Admin-Gate: `isAdmin` in `OnInitializedAsync` (Z. 650); serverseitig
  `RequireAdminAsync` in `ProgramRuleService` (Z. 225) und `ProgramScheduleService` (Z. 385).

## Tests

- `ProgramScheduleTestDb` (Helper): echtes SQLite In-Memory, `CreateRuleAsync`
  (inkl. `genreIds`/`mediaSourceIds`), `CreateSlotAsync`, `CreateShowAsync`,
  `CreateMovieAsync`, `AssertGapless`.
- `ProgramScheduleServiceTests_Reconcile.cs`: Worker-Schleifen-Test als Referenz.

## Hinweise

- Filler-Slots sind von `airings`-Gruppierung ausgenommen (nur Non-Filler);
  Filler-Invasoren werden einzeln verdrängt.
- `ProgramScheduleWorker` führt pro Takt `ReconcileAsync` + `EnsureCoverageAsync` aus.
