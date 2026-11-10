# Logik

## `ProgramScheduleService`

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (3229 Zeilen, `sealed`, implementiert
`IProgramScheduleService`)

Konstanten/Felder: `OccurrenceLookback = 24h`, `DiagnosisOccurrenceHorizonDays = 9`,
`_maxProbeAttempts`, `_trailerLead`, `_minStartRemain`, `_maxOccurrenceLookback` (aus
`ProgramScheduleOptions`), `OtherStrandKey = "other"`.

### Öffentliche Einstiegspunkte

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `ExecutePassAsync` | 75–86 | Produktionsreihenfolge je Worker-Lauf: `PurgeExpiredRulesAsync` → `CleanupSelfDisplacedSlotsAsync` → `ReconcileAsync` → `EnsureCoverageAsync`; Ergebnis `ProgramSchedulePassSummary` |
| `EnsureCoverageAsync` | 89–178 | **Extend-only**: `coverageStart = max(now, letztes Scheduled-End)`; schreibt nur hinter der bestehenden Belegung, verändert keine Slots. Occurrence-Schleife `Priority` desc → `Id` → `StartUtc` → `MaterializeOccurrence`; danach Trailer-/Clip-Expansion, `InsertClipFill`, `FillGaps`, `PersistCursors` |
| `ReconcileAsync` | 181–461 | Neu-Bewertung des gesamten Horizonts (siehe unten) |
| `GetScheduleAsync` | 464–470 | Slots (Scheduled+Displaced) ab `fromUtc` für die Admin-Anzeige |
| `GetRuleDiagnosesAsync` | 473–530 | Pro-Regel-Diagnose (Admin) |
| `DeleteDisplacedSlotsAsync` | 533–541 | Löscht alle Displaced-Zeilen (Admin) |
| `CleanupSelfDisplacedSlotsAsync` | 544–564 | Löscht nur bewiesene Selbst-Verdrängungs-Zwillinge (Worker) |
| `PurgeExpiredRulesAsync` | 567–590 | Löscht abgelaufene `OneTime`-Regeln (Worker) |
| `ResetScheduleAsync` | 593–608 | Löscht alle Slots + Cursor (Admin-Fallback „Plan zurücksetzen") |
| `RequireAdminAsync` | 610–617 | Admin-Prüfung serverseitig |

### `ReconcileAsync` — Ablauf im Detail (Zeilenangaben)

1. **Zustände laden** (183–224): alle Regeln des Kanals inkl. inaktiver (`rulesById`,
   `activeRules`); `coverageStart = utcNow`, `horizonEnd = utcNow + Horizon`; Access-Kontext,
   Feiertagsland, Altersregel; `RuleState` je Regel (Expansion via `ExpandMaterialAsync`,
   Clip-Gruppen via `BuildClipGroupStates` — Trailer-Gruppe sofort, da `airingsByMovie` aus
   persistierten Slots bekannt ist).
2. **Fenster laden** (238–282): lokale `LoadWindowAsync(from)` lädt `Scheduled` **und**
   `Displaced`-Zeilen in einer Abfrage (Trennung über projizierten DB-Status). Basisfenster
   `coverageStart - OccurrenceLookback` (24 h); Kostheuristik-Widening (`widened`, ein Slot mit
   `Start <= loadFrom`) oder validierungsgetriebenes Widening (`window.HasRescuableInvalid`)
   bis `coverageStart - _maxOccurrenceLookback` (Default 7 d). `window.Reload(...)` re-deriviert
   und re-validiert atomar.
3. **`Protected(slot)`** (305–307): laufender Slot (`Start < coverageStart < End`) — nie
   mitten in der Ausstrahlung schneiden (Issue #315); einzige Ausnahme: die **aktuellen**
   Jugendschutz-Einstellungen verbieten den Inhalt an seinem eigenen Slot-Start
   (`SlotMinAge` löst FSK über die Expansion der Erzeuger-Regel; Clip-Fill → `null`).
4. **Suffix-Verdrängung invalider Ausstrahlungen** (320–329): je `(ordered, firstInvalid)` aus
   `window.InvalidGroups` wird die gesamte Rest-Zukunft der Regel verdrängt (Cursor-Kontext
   veraltet). `Displace(slot)` (309–315) respektiert `slot.End <= coverageStart` (Historie)
   und `Protected`.
5. **Filler-Einzelprüfung** (331–343): Filler-Slots > coverageStart mit fehlender/inaktiver
   Regel oder nicht mehr expandiertem MediaItem → `Displace`.
6. **`RewindCursor`** (348–352) je verdrängter Regel.
7. **Belegung aufbauen** (356–358): `keptSlots` = noch `Scheduled` und `End > coverageStart`;
   `blocked` = deren Intervalle.
8. **`DisplaceRuleFuture(ruleId, fromStartUtc)`** (360–385): verdrängt **alle** Ausstrahlungen
   der Regel ab `fromStartUtc` (ganze Suffix, nicht nur den überlappenden Teil) plus
   Filler-Slots der Regel; räumt `keptSlots`/`blocked`; `RewindCursor`.
9. **`Evicts(winner, kept)`** (387–400): `Protected` → `false`; Filler → `true`; Regel des
   Slots unauflösbar → `true`; sonst `winner.Priority > loser.Priority ||
   (== && winner.Id < loser.Id)` — **Id-Tie-Break gegen neue Regeln**.
10. **Occurrence-Schleife** (405–435): `window.Occurrences` mit `StartUtc >= coverageStart -
    OccurrenceLookback`, sortiert `Priority` desc → `Id` → `StartUtc`; je Occurrence
    `MaterializeOccurrenceEvicting` mit `bound = BoundaryAfter(window.BoundStarts, StartUtc,
    Priority)`. Zurückgelieferte Verlierer: gruppierte Ausstrahlung → `DisplaceRuleFuture`
    (ganzer Regel-Suffix ab der getroffenen Ausstrahlung); ungruppierter Verlierer (Filler)
    → einzeln `Displaced` + `RewindCursor`.
11. **Nachläufe** (437–451): `BuildMovieAiringsFromSlotsAsync(kept + new)` → `InsertClipFill`
    (mit `BoundStarts`, `OccurrenceStarts`) → `FillGaps` → `PersistCursors` → `AddRange` →
    `SaveChangesAsync`. Rückgabe `ProgramScheduleReconcileSummary` (**kein** Zähler für
    verschobene Ausstrahlungen; `DroppedConflicts` zählt atomar verworfene Occurrences).

### `MaterializeOccurrenceEvicting` (1111–1203) — der entscheidende Pfad

| Schritt | Detail |
|---------|--------|
| Items prüfen | `state.Items.Count == 0` → `null` (keine Verlierer) |
| `outranked` | Intervalle gehaltener/frisch materialisierter Slots **strikt** höherer `Priority` |
| `airingStart` | `ShiftPastInvaders(occurrence.StartUtc, blocked, outranked)` — strikt überdeckende Belegung schiebt; `outranked`-Intervalle schieben auch bei bloßer Berührung (Übernahme-Kette, Issue #314) |
| `WindowLostToTakeover` (1139) | verschobener Start ≥ `StartUtc + SlotDuration` **und** ohne Übernahme-Beleger läge er noch im Fenster → `null` (Occurrence verweist auf ihre nächste; kein `DroppedConflicts`) |
| Rotation | `StrandRotationOffset` für interleaved Regeln aus gehaltenen+neuen Slots der eigenen Regel |
| Auswahl | `SelectItems(state, rule, airingStart, bound, ageRule, rotationStart)`; `taken.Count == 0` → `null` |
| `ranges` | aufeinanderfolgende Intervalle ab `airingStart`; nur `End > coverageStart` schreibt; `ranges.Count == 0` → `null` (Cursor unberührt) |
| **Konfliktprüfung** (1169–1181) | `conflicts` = gehaltene Slots, die `ranges` überlappen; `competitors` = `conflicts` mit `k.Start >= occurrence.StartUtc` (Kopf-an-Kopf im Fenster). **`competitors.Any(!evicts)` ODER Überlapp mit `newSlots` → `DroppedConflicts++`, `return null` — gesamte Occurrence fällt atomar, nichts wird verdrängt.** Slots mit `k.Start < occurrence.StartUtc` (überschießende Tails) verlieren dagegen stets — sie gehen als Verlierer zurück |
| Erfolg | neue `ProgramSlot`-Zeilen → `newSlots`, `blocked.AddRange`, `CommitTaken`, `SlotsCreated += ranges.Count`, Rückgabe `conflicts` (Verlierer) |

`MaterializeOccurrence` (1703–1791, `EnsureCoverageAsync`-Pfad): gleiche Schritte ohne
Verdrängung — **jede** Überlapp mit `blocked`/`newSlots` wirft die ganze Ausstrahlung
(`DroppedConflicts++`).

### Fenster-/Verschiebe-Helfer

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `GroupAirings(orderedSlots, coverageBoundary)` | 629–647 | Maximal lückenlose Ketten gleicher `ProgramRuleId`; teilt zusätzlich an `coverageStart` (Historien- vs. Zukunftsgruppe) |
| `ShiftPastInvaders(startUtc, occupied, outranked)` | 1484–1516 | Fixpunkt-Verschiebung hinter Belegungen; `outranked` schiebt zusätzlich bei Intervall-Berührung |
| `WindowLostToTakeover` | 1529–1535 | siehe oben (nur bei `SlotDuration` relevant) |
| `BoundStarts(occurrences, states)` | 2071–2077 | sortierte Starts aller Occurrences item-führender Regeln |
| `BoundaryAfter(boundStarts, afterUtc, priority)` | 2090–2096 | erster Bound > `afterUtc` mit `p >= priority`; `Hard = p > priority` |
| `SelectItems` / `SelectAiringItems` / `SelectAiringItemsInterleaved` / `RunInterleavedLaps` | 1825–1999 | Titel-Auswahl: `EpisodeCount` oder `SlotDuration`; Soft-Bound: Start nur bis `minStartRemain` vor Bound, begonnener Titel läuft zu Ende (überschießt legal); Hard-Bound: Titel muss **komplett** vor Bound enden sonst defer; `clipGrace` beendet Titelwahl früh; geordnete Stränge (`PreservesOrder`) defern Fenster-blockierte Köpfe, ungeordnete überspringen |
| `CommitTaken` | 1846–1860 | Scan-Konsum + `LastPlaced` je Strang erst nach Konfliktprüfung |
| `AnchorProvable` | 781–810 | Kopf-Anker: nominaler Occurrence-Start ≤ Kopf — exakt, per `ShiftProvable`-Kette oder im `UnwrittenHeadroom`; zwei Ankerziele für boundary-geteilte Gruppen (`AiringRealStart`) |
| `ShiftProvable` | 832–838 | reproduziert `ShiftPastInvaders` auf Evidenz-Intervallen (`End ≤ realStart`, `Priority > rulePriority` als outranked) |
| `AiringRealStart` | 817–823 | faltet voranlaufendes lückenloses Fragment auf den realen Start zurück |
| `UnwrittenHeadroom` | 755–767 | `SlotDuration ?? (wanted-1)·maxItemDuration` (saturiert) |
| `SlotStillValid` | 682–738 | Clip-Check (Expansion, Laufzeit, Altersfenster) bzw. Titel-Check (`LocateInStrands`, `MatchingItemDuration`, `IsAllowedAt`, Ring-Kontiguität `PreservesOrder` → `StrandSequenceBroken`); Fehler: `ItemNotInExpansion`/`SlotSequenceMismatch`/`StrandSequenceBroken` |
| `RewindCursor` / `RewindRing` | 908–957 | je Strang/Clip-Ring: Cursor auf Vorgänger des **frühesten verdrängten, noch expandierbaren** Inhalts (nur wenn Ring-Cursor schon darüber hinaus); Clip-/Titel-Trennung über `ClipMediaItemIds` |
| `PersistCursors` | 2252–2284 | schreibt/updated eine `ProgramRuleCursor`-Zeile je Strang mit `LastPlaced` — legt den Cursor einer neuen Regel beim ersten Commit an |
| `BuildOccurrences` | 1664–1701 | Tagesschleife `[coverageStart - lookback, horizonEnd)` lokal: `ProgramRuleValidity.IsValidOn` + `Recurrence`-Switch → UTC-Starts |
| `FindItemIndex` / `LocateInStrands` / `TitleStrandIndex` / `StrandRotationOffset` / `ClipMediaItemIds` / `MatchingItemDuration` | 651–903 | Auflösungs-Helfer (Clip-zuerst-Klassifikation; Inhalts-Fallback über `MovieId`/`TVShowEpisodeId`; fortlaufender Round-Robin-Zeiger aus geschriebenen Slots) |
| `MixUnorderedRing` / `StrandOrderKey` / `ItemOrderKey` / `Fnv1aHash` / `IsUnorderedRing` | 1422–1463 | deterministischer FNV-1a-Hash-Mix nur für ungeordnete Ringe |
| `NextIndexFor` / `SelectStrandCursor` | 1469–1474, 1655–1662 | Ringposition hinter persistiertem Cursor; deterministische Cursor-Wahl (UpdatedAt, Id) |
| `StrandStartOffset` | 1481–1482 | Tages-Rotation bei `wanted < Strangzahl` |

### `ReconcileWindow` (965–1103) — abgeleiteter Fensterzustand

`Reload(loadFrom, scheduled, displacedEvidence)` → `Rebuild` + `Validate` atomar.
`Rebuild`: `_occupiedEvidence` aus Scheduled ∪ Displaced (mit Regel-Priorität); `Occurrences`
(via `BuildOccurrences` über `reconcileLookback`); `_occurrenceStarts`; `BoundStarts`;
`Airings` (Nicht-Filler, geteilt am coverageStart); `FillerSlots`.
`Validate`: je Regel-Gruppe erste invalide Ausstrahlung → `InvalidGroups`; Gründe:
`RuleMissing`/`RuleInactive`/`NoOccurrenceAnchor`/`TitlelessAiring`/Anker/`SlotStillValid`;
nur aktive Regeln setzen `HasRescuableInvalid`; Log-Zeile je erstem invaliden Block.

### Clip-Füllung / Lücken

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `InsertClipFill` | 3049–3073 | Nachlauf über **nur** `newSlots`-Ausstrahlungen mit Clip-Gruppen + `SlotDuration` → `FillAiringSegments` |
| `FillAiringSegments` | 3138–3228 | verteilt Rest bis Periodenende (`PeriodStart`+`SlotDuration`) auf n+1 Segmente, Round-Robin `NextClipItem`; Freiraum-Wächter `TailLimit`/`ChainLimit`; legt `IsClipFill`-Slots an, verschiebt Titel zeitlich |
| `NextClipItem` | 2999–3037 | Round-Robin-Zug über `ClipGroupState.Ring`s; Trailer-Gruppe nur bei Lead-Match |
| `FillGaps` / `GapSeconds` | 2098–2248 | füllt Restlücken je Position mit Filler-Regeln (Gültigkeit pro Lückentag, Trailer-Vorlauf, Horizont-Overshoot des letzten Füllers); Reste → `RemainingGapSeconds` |

### Interne Typen

`SchedulableItem` (1207–1225, `Sequenced` für Sammlungsfilme), `StrandState` (1236–1274:
`NextIndex`, `LastPlaced`, `PreservesOrder`, `PeekAt`/`Commit`/`CommitScan`/`RewindTo`),
`ClipGroupState` (1279–1302, `StrandKey = clipgroup:{id}`, `IsTrailer`), `RuleState`
(1304–1393: `Strands`, `ClipGroups`, `IsInterleaved`, `ClipGrace`, Flat-Fassade, `SetItems`
mit Strang-Aufbau/Hash-Mix), `Occurrence` (1395), `Interval` (1397, `Overlaps`), `OccupiedSlot`
(1406–1412), `SelectionResult`/`StrandPick`/`FlatSelection`/`InterleavedSelection`
(1806–1823), `SummaryCounters` (1537–1558), `AccessContext` (1563–1584),
`MediaItemRef`/`MediaItemVariant`/`MediaItemChoice`/`MediaItemLink`/`ClipCandidate`/
`ProxyEpisode` (2317–2439, 2877).

### Expansion

`ExpandMaterialAsync` (2288–2312) dispatcht je `MaterialType`:
`ExpandShowAsync`/`ExpandMovieAsync`/`ExpandMovieCollectionAsync` (Releasedatum-Reihenfolge,
`collection:{id}`)/`ExpandGenreAsync` (Stränge `show:{id}`, `collection:{id}`, `other`)/
`ExpandPlaylistAsync`/`ExpandTrailersAsync`/`ExpandMediaSourceClipsAsync`;
`ExpandTrailerStatesAsync` + `ExpandClipGroupStatesAsync` (verzögert, Trailer brauchen
`airingsByMovie`); `BuildClipGroupStates`; `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync`/
`ToMediaItemChoices` (Repräsentant + `MediaItemAlternates`); `LoadMovieAiringsAsync`/
`BuildMovieAiringsFromSlotsAsync`/`BuildMovieAiringsMap`; `FilterClipCandidatesAsync`;
`FindLeadMatchOffset`; `TrailerMediaItemIdsAsync`.

## `ProgramScheduleWorker`

Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs`

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `ExecuteAsync` | 45–119 | Intervall-Schleife (`GetProgramScheduleIntervalAsync`, Default 15 min): nur bei `TvSenderEnabled` hinter `IBackgroundProcessingGate` → `ExecutePassAsync`; bei materialisierten Änderungen `_signal.NotifyLiveScheduleChanged()`. Weckleitung: `Task.Delay` vs. `_signal.WaitAsync` (Signal-Task überlebt Iterationen) |

## `ProgramRuleService` (relevante Auslöser)

Datei: `VideoWebPlayer/Services/ProgramRuleService.cs`

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `CreateRuleAsync` | 116–152 | Admin-Pflicht; Feld-Normalisierung (`ApplyValidity`, `MaterialIdFor`); `NotifyScheduleChanged()` |
| `UpdateRuleAsync` | 155–184 | dto.; `NotifyScheduleChanged()` |
| `SetRuleActiveAsync` | 244–256 | (Re-)Aktivierung; `NotifyScheduleChanged()` |
| `DeleteRuleAsync` | 259–269 | Löscht Regel (Cursor/Joins kaskadieren); `NotifyScheduleChanged()` |
| `ApplyValidity` (internal) | 190–202 | Modus-Feld-Normalisierung (mit Test-Seed geteilt) |
| `RequireAdminAsync` | 271–278 | Admin-Pflicht |

## `ProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` — gebündelte Weckleitung
(Debounce `ScheduleChangedDebounceSeconds`, Scan-Unterdrückung via `BeginScan`,
getrennter `_liveChannel` für `NotifyLiveScheduleChanged`).

## `ProgramRuleValidity`

Datei: `VideoWebPlayer/Services/ProgramRuleValidity.cs` — `IsValidOn` (35–91, alle Modi,
inkl. Jahreswechsel-Wrap) und `IsExpired` (102–106, nur `OneTime` mit `ValidTo < heute`).

## `ProgramSettingsService` / `ProgramScheduleOptions` (Konfiguration)

`ProgramSettingsService`: `GetProgramScheduleHorizonAsync` (Default 48 h),
`GetProgramScheduleIntervalAsync` (Default 15 min), `GetTvSenderEnabledAsync`,
`GetHolidayFederalStateAsync`, `GetProgramScheduleAgeWindowsAsync`,
`GetProgramScheduleUnknownRatingModeAsync`, `GetProgramScheduleContentScopeAsync`.
`ProgramScheduleOptions` (`ProgramSchedule`-Sektion): `TrailerLeadDays` (7),
`MinStartRemainSeconds` (30), `MaxOccurrenceLookbackDays` (7, Clamp 30),
`ScheduleChangedDebounceSeconds` (5). Keine neue Konfiguration in der Anforderung vorgesehen.

## Sichtbarkeits-/Konsumentenpfade

- `TVSenderAdmin.razor` — Tab „Sendeplan" (`GetScheduleAsync`, `ReloadScheduleAsync`),
  „Jetzt generieren" → `ScheduleSignal.NotifyScheduleChangedNow()`, „Sendeplan zurücksetzen"
  → `ResetScheduleAsync` (Admin-Fallback), „Verdrängte bereinigen" →
  `DeleteDisplacedSlotsAsync`; `BuildScheduleDisplayItems` gruppiert Clip-Blöcke (Anzeige).
- `ProgramGuideService` / `ProgramGuideController` — Programmauskunft über
  `Status == Scheduled` Slots; `LiveTv.razor` öffentliche Vorschau.
- `docs/help/tv-sender-ablauf-technisch.md` — Abschnitt „5. Neu-Bewertung (`ReconcileAsync`)"
  und „Stabile Neu-Bewertung des Sendeplans (Idempotenz)" dokumentieren den Ist-Stand
  (bei Umsetzung zu aktualisieren).
