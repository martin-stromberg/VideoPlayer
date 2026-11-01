# Logik — Bestandsaufnahme Senderplanung

Zentrale Klasse: `ProgramScheduleService` (`VideoWebPlayer/Services/ProgramScheduleService.cs`,
3541 Zeilen, `internal sealed`, implementiert `IProgramScheduleService`). Zeilenangaben
beziehen sich auf Commit `58a63df` (unveränderte Produktionsquellen).

## `ProgramScheduleService` — Klassenkonstanten und Felder

| Member | Zeile | Beschreibung |
|--------|-------|--------------|
| `OccurrenceLookback` (static, `TimeSpan.FromHours(24)`) | 26 | Rückblick vor `coverageStart` beim Occurrence-Aufbau (EnsureCoverage) und als Basis-Fenster des Reconcile-Slot-Ladens |
| `DiagnosisOccurrenceHorizonDays` (const 9) | 33 | Horizont der Occurrence-Suche für die Regel-Diagnose |
| `MaxDeferredStrands` (const 3) | 1231 | Obergrenze der Deferral-Hypothesen je Strang im Interleaved-Tail-Check |
| `OtherStrandKey` (const `"other"`) | 1630 | Strang-Schlüssel des gemeinsamen Rings für Nicht-Episoden-Inhalt (Filme) einer Genre-Regel |
| `_minStartRemain`, `_trailerLead`, `_maxOccurrenceLookback` | 41–42, 69–71 | Aus `ProgramScheduleOptions` (`EffectiveMinStartRemain`, `EffectiveTrailerLead`, `EffectiveMaxOccurrenceLookback`) |
| `_maxProbeAttempts` | 39, 67 | Aus `MediaProbeOptions.EffectiveMaxAttempts` — nur Diagnose |

## `ProgramScheduleService` — öffentliche API

| Methode | Zeile | Beschreibung |
|---------|-------|--------------|
| `EnsureCoverageAsync(utcNow, ct)` | 75 | Extend-only-Lauf: lädt aktive Regeln, Cursor, baut `RuleState`s und `ClipGroupState`s (Trailer-Gruppen bleiben zunächst leer), `coverageStart = max(utcNow, letztes Scheduled-Ende)`, Occurrences via `BuildOccurrences` mit `OccurrenceLookback` (Z. 131), Materialisierung je Occurrence (`MaterializeOccurrence`, Z. 136–137), dann Trailer-/Clip-Gruppen-Spätexpansion, `InsertClipFill`, `FillGaps`, `PersistCursors`, SaveChanges |
| `ReconcileAsync(utcNow, ct)` | 158 | Re-bewertung: baut `RuleState`s für alle Regeln (auch inaktive — Cursor-Rewind), lädt das Slot-Fenster ab `coverageStart - OccurrenceLookback` (Z. 240–241) mit On-Demand-Verbreiterung bis `_maxOccurrenceLookback` (Z. 243–247) bzw. validierungsgetriebenem Widening (Z. 254–259), `ReconcileWindow.Reload` → Rebuild + Validate (Z. 248), Suffix-Verdrängung ab erster invalider Ausstrahlung je Regel (Z. 297–306), Filler-Einzelprüfung (Z. 308–320), `RewindCursor` (Z. 325–329), Re-Materialisierung über `MaterializeOccurrenceEvicting` nur für Occurrences `>= coverageStart - OccurrenceLookback` (Z. 382–412), Clip-Fill + `FillGaps`, `PersistCursors`, SaveChanges |
| `GetScheduleAsync` | 441 | Lesen des Plans (keine Planlogik) |
| `GetRuleDiagnosesAsync` | 450 | Admin-Diagnose; `DateTime.UtcNow` als Evaluationszeitpunkt (Z. 465 — bewusst nicht `utcNow`-gesteuert) |
| `DeleteDisplacedSlotsAsync` | 510 | Admin-Bereinigung (`ExecuteDeleteAsync` aller Displaced) |
| `CleanupSelfDisplacedSlotsAsync` | 521 | Löscht Displaced-Zeilen mit bewiesenem Scheduled-Zwilling (gleiches Intervall + Inhalt) |
| `PurgeExpiredRulesAsync` | 544 | Löscht abgelaufene `OneTime`-Regeln (`ProgramRuleValidity.IsExpired`) |
| `ResetScheduleAsync` | 570 | Löscht alle Slots + Cursor (Admin) |
| `RequireAdminAsync` | 587 | Admin-Prüfung für Diagnose/Bereinigung |

### Lokale Funktionen in `ReconcileAsync`

| Funktion | Zeile | Beschreibung |
|----------|-------|--------------|
| `LoadWindowAsync(from)` | 215 | Lädt Scheduled- und Displaced-Zeilen mit `End > from`; Displaced reiten als Invader-Evidence mit |
| `SlotMinAge(slot)` | 268 | Mindestalter eines Slots über die Expansion der erzeugenden Regel; Clip-Fill → `null` (unknown-rating-Modus) |
| `Protected(slot)` | 282 | Laufender Slot (`Start < coverageStart < End`) ist nie mid-air zu schneiden — außer die Altersprüfung an seinem eigenen Start verbietet ihn schon |
| `Displace(slot)` | 286 | Setzt `Status = Displaced`, außer vollständig gelaufen oder `Protected` |
| `DisplaceRuleFuture(ruleId, fromStartUtc)` | 337 | Verdrängt den kompletten Regel-Suffix ab einem Airings-Start + dessen Filler-Slots, `RewindCursor` |
| `Evicts(winner, kept)` | 364 | Wer gewinnt: `Protected` nie; Filler immer; sonst höhere Priorität, dann kleinere Regel-Id |

## Occurrence-Aufbau und Materialisierung

| Methode | Zeile | Beschreibung |
|---------|-------|--------------|
| `BuildOccurrences(rules, coverageStartUtc, horizonEnd, federalState, lookback)` | 2030 | Lokale Tage (`TimeZoneInfo.Local`), Gültigkeit via `ProgramRuleValidity.IsValidOn`, Recurrence-Mapping, UTC-Konvertierung; filtert `startUtc >= coverageStartUtc - lookback && < horizonEnd` (Z. 2061) |
| `MaterializeOccurrence(...)` | 2069 | Kern der Materialisierung: `ShiftPastInvaders` (Z. 2079) → `SelectItems` (Z. 2084) → Ranges nur für `End > coverageStart` (Z. 2093–2101) → Atomar-Konfliktcheck (Z. 2106) → Slots schreiben + `CommitTaken` (Z. 2129). **Ist-Stand Lookback:** eine komplett beendete Occurrence produziert `ranges.Count == 0` Slots, geht aber dennoch den Weg über `SelectItems` — die Cursor-Verschiebung erfolgt in `CommitTaken` **nur nach dem Konfliktcheck**; da `ranges` leer ist, prüft Z. 2106 gegen leere Liste → kein `return`, und Z. 2129 committet die komplette Auswahl. **Das ist der dokumentierte Lookback-Verbrauch:** gescannte Positionen (Taken + Skips) werden committed, ohne dass je ein Slot entstand |
| `MaterializeOccurrenceEvicting(...)` | 1529 | Reconcile-Variante: wie `MaterializeOccurrence`, gibt aber die verlierenden Bestands-Slots zurück statt die Ausstrahlung zu verwerfen; expliziter Kommentar Z. 1570–1573 „Fully-past occurrences produce nothing and must not consume the cursor" — gilt allerdings nur für den `ranges.Count == 0`-Fall nach `SelectItems`, die Auswahl selbst (inkl. Ring-Scans/Deferral-Flags) läuft vorher |
| `ShiftPastInvaders(startUtc, occupied, outranked)` | 1869 | Verschiebt einen Occurrence-Start hinter belegte Intervalle (Fixpunkt); `outranked`-Intervalle höherer Priorität schieben auch bei Berührung |
| `BoundStarts(occurrences, states)` | 2376 | Sortierte Occurrence-Starts aller Regeln mit Material — Fenstergrenzen |
| `BoundaryAfter(boundStarts, afterUtc, priority)` | 2395 | Erste Grenze nach `afterUtc` mit `Priority >= priority`; `Hard` bei strikt höherer Priorität |

## Titelauswahl (Ring-Scan)

| Methode | Zeile | Beschreibung |
|---------|-------|--------------|
| `SelectItems(state, rule, airingStartUtc, bound, ageRule)` | 2166 | Dispatcher: flach → `SelectAiringItems`, interleaved → `SelectAiringItemsInterleaved`; Ergebnis `SelectionResult` (Taken, Picks, Scanned, BlockedByAgeWindow) |
| `SelectAiringItems(items, headIndex, rule, airingStartUtc, bound, minStartRemain, clipGrace, preservesOrder, ageRule)` | 2313 | Flacher Scan: `wanted = EpisodeCount ?? (SlotDuration ? ∞ : 1)`; pro Kandidat Altersfenster (`IsAllowedAt`) → geordneter Strang bricht ab (Deferral), ungeordneter Ring skippt (Position verbraucht); `clipGrace`-Check `t - accumulated < grace` → Abbruch (Z. 2348); Bound-Check `b.StartUtc - slotStart < minStartRemain` bzw. `b.Hard && Ende > Bound` → Abbruch; Overshoot erlaubt (Item läuft zu Ende) |
| `SelectAiringItemsInterleaved(strands, rule, airingStartUtc, bound, minStartRemain, clipGrace, ageRule)` | 2211 | Interleaved-Variante: Round-Robin über `Strands` ab `StrandStartOffset`; `clipGrace` bricht die Rotation (`graceHit`, Z. 2249–2252); pro Strang eigener Skip/Deferral-Loop (Z. 2256–2290); Abbruch bei Ziel erreicht, `graceHit`, keinem Fortschritt oder >`maxLap` leeren Runden |
| `CommitTaken(state, selection)` | 2187 | Committet erst nach bestandenem Konfliktcheck: je Strang `CommitScan(scanned, lastTaken)` |
| `StrandStartOffset(wanted, strandCount, startUtc)` | 1864 | Start-Strang des Interleavings: rotiert nur bei `EpisodeCount < Strangzahl` (pro Kalendertag), sonst 0 |

## Ring-/Strang-Mechanik

| Methode / Typ | Zeile | Beschreibung |
|---------------|-------|--------------|
| `RuleState` (Klasse) | 1703 | Pro-Regel-Laufzustand: `Items`, `Strands`, `ClipGroups`, `IsInterleaved`, `ClipGrace` (nur bei Clip-Gruppen + `SlotDuration`, Z. 1723–1725), Flat-Delegierung auf `Strands[0]`; `SetItems` (Z. 1739) baut die Stränge: Genre-Regeln gruppieren nach `StrandKey` (`show:{id}`/`other`), ordnen per `StrandOrderKey`, mischen ungeordnete Ringe per `MixUnorderedRing`; Flat-Pfad Z. 1783–1785 |
| `StrandState` (Klasse) | 1636 | Ein Ring: `NextIndex` (Scan-Kopf), `LastPlaced` (letzter tatsächlich geplanter Titel — Basis des persistierten Cursors), `PreservesOrder` (Z. 1648: alle Items `TVShowEpisode`/`PlaylistEntry` → geordnet), `PeekAt`, `Commit` (Z. 1654), `CommitScan(scanned, lastTaken)` (Z. 1665 — Skips bleiben verbraucht, `LastPlaced` nur auf Taken), `RewindTo` |
| `IsUnorderedRing(items)` | 1836 | Alle Items `Movie`/`MediaItem` → ungeordnet (mischargesteuert) |
| `MixUnorderedRing(ruleId, strandKey, items)` | 1843 | Deterministische Mischung: `OrderBy(ItemOrderKey).ThenBy(ContentId)` — **wachstumsinstabil**: ein neues Item kann die ganze Ordnung umsortieren (Ursache von Befund 1) |
| `ItemOrderKey(ruleId, strandKey, contentType, contentId)` | 1815 | `Fnv1aHash("{ruleId}:{strandKey}:{contentType}:{contentId}")` — `internal` für Tests |
| `StrandOrderKey(ruleId, strandKey)` | 1807 | `Fnv1aHash("{ruleId}:{strandKey}")` — `internal` |
| `Fnv1aHash(input)` | 1818 | FNV-1a 64-bit, prozessinvariant |
| `SelectStrandCursor(cursors, key)` | 1854 | Gewinner-Cursor je Strang: höchstes `UpdatedAt`, dann höchste `Id` — `internal` |
| `NextIndexFor(items, cursor)` | 2021 | Ringposition hinter dem Cursor-Inhalt; 0 ohne Cursor oder bei nicht mehr auffindbarem Inhalt |
| `PersistCursors(states, cursors)` | 2557 | Schreibt je Strang mit `LastPlaced` eine Cursor-Row (`clipgroup:{id}`-Ringe eingeschlossen) |
| `RewindCursor(state, displacedSlots, cursorRows)` | 1348 | Split in Titel-/Clip-Slots über `ClipMediaItemIds`; `RewindRing` je Strang und Clip-Ring |
| `RewindRing(ring, key, slots, indexOf, cursorRows)` | 1369 | Setzt `NextIndex` zurück auf die früheste verdrängte, noch expandierbare Position (nur wenn der Cursor schon darüber hinaus ist) und persistiert den Vorgängerinhalt |
| `ClipGroupState` (Klasse) | 1678 | Clip-Gruppen-Ring: `StrandKey = "clipgroup:{id}"`, `IsTrailer` (Id 1), `SetItems` → `MixUnorderedRing` + `NextIndexFor` |

## Reproduzierbarkeitsprüfung (Reconcile)

| Methode | Zeile | Beschreibung |
|---------|-------|--------------|
| `GroupAirings(orderedSlots, coverageBoundary)` | 605 | Gruppiert Slots zu Ausstrahlungen (gleiche Regel, lückenlos, Bruch an der Coverage-Grenze) |
| `AiringReproducible(airing, state, occurrenceStarts, boundStarts, occupiedEvidence, minStartRemain, ageRule, laterGroupSlots, out invalidReason)` | 678 | Einstieg: Clip-Pre-Filter (`ClipMediaItemIds`, clip-freie Zeitprojektion, `TitlelessAiring`/`ClipChainBroken`), dann rekursiver Dispatch |
| `AiringReproducible(airing, offset, afterStart, context)` | 739 | Rekursion + Memo; Dispatch flach/interleaved |
| `FlatAiringTailReproducible(...)` | 753 | Rückwärts-Hypothesen k (unwritten head items + window-skips), rekonstruierter `airingStart`, Anker nur an Occurrences `> afterStart && <= airingStart` mit `ShiftProvable`-Beleg, dann `SelectAiringItems`-Re-Simulation und Slot-für-Slot-Abgleich; **liest die Ringordnung positionsbasiert — ein umsortierter Ring (neues Item) lässt die Positionen nicht mehr stimmen → `SlotSequenceMismatch`/`HeadPickInconsistent`** |
| `InterleavedAiringTailReproducible(...)` | 932 | Interleaved-Pendant: Slot→Strang-Zuordnung per `FindItemIndex`, `PrefixTakeAssignments`-Hypothesen mit `ProvableDeferral`, Scan-Kopf-Rekonstruktion (`headBase` aus geschriebenem Pick, späterem Pick oder Cursor), `SelectAiringItemsInterleaved`-Re-Simulation |
| `ShiftProvable(occurrenceStart, airingStart, rulePriority, occupiedEvidence)` | 907 | Belegt eine Startverschiebung durch besetzte Intervalle (Scheduled ∪ Displaced) |
| `PrefixTakeAssignments` / `DeferralAssignments` | 1240/1256 | Take-Count-Hypothesen des ungeschriebenen Präfix (dense zuerst, dann bis `MaxDeferredStrands` Deferrals) |
| `ProvableDeferral(...)` | 1288 | Belegt eine Nichts-Runde: geordneter Strang auf Age-/Bound-Block, ungeordneter Ring nur auf Bound-Block |
| `FindItemIndex(items, slot)` | 1333 | Slot→Item: erst `MediaItemId`, dann `MovieId`/`TVShowEpisodeId` (Representative-Wechsel tolerant) |
| `ClipMediaItemIds(state)` | 669 | MediaItemIds aller Clip-Ringe — Trennung Clip vs. Titel |
| `SlotMatchesItem(slot, item, expectedStart)` | 3532 | Start-/Ende-/MediaItem-Abgleich inkl. `MediaItemAlternates` |
| `ReconcileWindow` (Klasse) | 1405 | Fensterzustand: `Reload` → `Rebuild` (Evidence, Occurrences mit `reconcileLookback = coverageStart - loadFrom`, BoundStarts, `GroupAirings`) + `Validate` (erste invalide Ausstrahlung je Regel → `InvalidGroups`, `HasRescuableInvalid`) |

## Clip-Füllung

| Methode | Zeile | Beschreibung |
|---------|-------|--------------|
| `ClipFillContext` (Record) | 3152 | Kontext: Access, `AiringsByMovie` (Trailer-Lead), `CoverageStart`, `BoundStarts`, `HorizonEnd`, `Summary`, `AgeRule`, `OccurrenceStarts` (Basis des Periodenendes), `RoundRobin` |
| `BuildClipGroupStates(rule, cursors, ctx, ct)` | 3185 | Baut `ClipGroupState`s in `ProgramClipGroupId`-Reihenfolge; Trailer-Gruppe nur bei vorhandener `AiringsByMovie`-Map |
| `ExpandClipGroupStatesAsync(states, cursors, ctx, ct)` | 3212 | Spätexpansion der Trailer-Ringe (nach der Materialisierung) |
| `ExpandClipGroupItemsAsync(groupId, access, summary, ct)` | 3229 | Expansion einer Benutzer-Clip-Gruppe → `FilterClipCandidatesAsync` |
| `FilterClipCandidatesAsync(...)` | 3250 | Filter: unklassifiziertes Video, probierte Laufzeit, Publikumsfreigabe; sortiert nach `MediaItemId` |
| `NextClipItem(clipGroups, ref roundRobin, position, airingsByMovie, fits)` | 3296 | Round-Robin-Zug über die Gruppen; Trailer-Gruppe nur mit Lead-Match (`FindLeadMatchOffset`) |
| `InsertClipFill(newSlots, blocked, states, ctx)` | 3346 | Post-Pass: verteilt Clip-Slots in die n+1 Segmente jeder `SlotDuration`-Ausstrahlung mit Clip-Gruppen |
| `FillAiringSegments(airing, state, blocked, rebuilt, ctx)` | 3435 | Re-Layout einer Ausstrahlung: Rest zwischen letztem Titel-Ende und `periodEnd` (via `PeriodStart`, Z. 3441–3447) wird auf Segmente verteilt; Titel-Slots werden hinter den Clips neu positioniert |
| `PeriodStart(ctx, ruleId, headStartUtc)` | 3411 | Nominale Occurrence ≤ Kopf-Start — Basis des Periodenendes |
| `TailLimit` / `ChainLimit` | 3374 / 3392 | Freiraumgrenzen für Clip-Overshoot bzw. Re-Layout |
| `FillGaps(states, rules, blocked, newSlots, coverageStart, horizonEnd, summary, ageRule, airingsByMovie)` | 2403 | Restliche Lücken mit Filler-Regeln füllen; Trailer-Vorlauf via `FindLeadMatchOffset` + `_trailerLead`; Filler-Gültigkeit je Positionstag |
| `FindLeadMatchOffset(state/ring, fillAt, leadEnd, airingsByMovie)` | 2985/2988 | Erster Ring-Offset, dessen Film in `(fillAt, leadEnd]` ausgestrahlt wird |
| `GapSeconds` | 2539 | Restsekunden-Statistik |

## Material-Expansion

| Methode | Zeile | Beschreibung |
|---------|-------|--------------|
| `ExpandMaterialAsync(rule, access, summary, airingsByMovie, coverageStart, ct)` | 2593 | Dispatch nach `MaterialType`; `OrphanedRules` bei leerem Ergebnis |
| `ExpandGenreAsync(rule, access, summary, coverageStart, ct)` | 2806 | Genre-Expansion: Quell-Filter, kanonische Namen (`GenreCanonicalResolver`), `MaterialId 0` = alle Genres; Filme → `StrandKey = "other"`, Episoden → `StrandKey = "show:{id}"`; Filme sortiert nach `ReleaseDate ?? PremieredAt`, `Name`, `Id` (Z. 2871–2873), Episoden nach Serienname/Season/Nummer (Z. 2885–2888) — die Hash-Mischung geschieht erst in `SetItems`/`MixUnorderedRing` |
| `ExpandShowAsync` / `ExpandMovieAsync` / `ExpandMovieCollectionAsync` / `ExpandPlaylistAsync` | 2708 / 2760 / 2774 / 3069 | Weitere Materialtypen |
| `ExpandTrailerStatesAsync` | 2915 | Spätexpansion Trailer-Materialregeln |
| `ExpandTrailersAsync(access, airingsByMovie, coverageStart, summary, ct)` | 2938 | Trailer der im Fenster geplanten Filme, nach nächstem Start sortiert |
| `ExpandMediaSourceClipsAsync` | 3013 | Clip-Quellen-Material |
| `LoadMovieAiringsAsync` / `BuildMovieAiringsFromSlotsAsync` / `BuildMovieAiringsMap` | 3031 / 3044 / 3054 | Film-Ausstrahlungen > `afterUtc`; Trailer-Slots zählen nicht als Ausstrahlung |
| `TrailerMediaItemIdsAsync` | 3001 | MediaItemIds aller Trailer-Links |
| `MovieMediaItemsAsync` / `EpisodeMediaItemsAsync` / `ToMediaItemChoices` | 2636 / 2645 / 2656 | Probed-Representative + Alternates je Inhalt |
| `TryMovieItem` / `ToItem` / `CountNoDuration` | 2687 / 2739 / 2670 | Item-Erzeugung mit Dauer-/Freigabe-Prüfung |
| `LoadAccessContextAsync` | 1952 | Publikums-Snapshot (`ProgramScheduleContentScope.AllContent` → Everything) |
| `LoadAgeRuleAsync` | 2014 | `ProgramScheduleAgeWindowRule` aus den Settings |

## Interne Records/Hilfstypen

`SchedulableItem` (Z. 1613: ContentType/ContentId/MediaItemId/MovieId/EpisodeId/Duration/StrandKey/AgeRatingMinAge + `MediaItemAlternates`),
`Occurrence` (Z. 1792), `Interval` (Z. 1794, `Overlaps`), `SummaryCounters` (Z. 1903),
`AccessContext` (Z. 1929), `ReproContext` (Z. 630, mit `NoteFailure`),
`SelectionResult`/`StrandPick`/`FlatSelection`/`InterleavedSelection` (Z. 2146–2164),
`MediaItemRef`/`MediaItemVariant`/`MediaItemChoice`/`MediaItemLink` (Z. 2622–2634),
`ProxyEpisode` (Z. 2737), `ClipCandidate` (Z. 3174).

Publizierte/abonnierte Events: keine — Kommunikation läuft über `IProgramScheduleSignal`
(Worker-seitig).

## `ProgramScheduleWorker`

Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs` (`BackgroundService`, registriert
als Hosted Service). Referenzreihenfolge je Iteration (Z. 74–84):
`PurgeExpiredRulesAsync` → `CleanupSelfDisplacedSlotsAsync` → `ReconcileAsync` →
`EnsureCoverageAsync`, jeweils mit `_timeProvider.GetUtcNow()`; danach
`NotifyLiveScheduleChanged` bei Änderungen. Intervall aus
`GetProgramScheduleIntervalAsync` (Default 15 min), Feature-Flag
`GetTvSenderEnabledAsync`, `TimeProvider` injizierbar (Test-Helper
`ManualTimeProvider`/`ControllableTimeProvider` vorhanden — für diese Anforderung nicht
nötig, da die Tests den Service direkt mit `utcNow` fahren).

## `ProgramScheduleAgeWindow`, `ProgramScheduleAgeWindowRule`, `ProgramScheduleAgeWindowSettings`

Dateien: `Services/ProgramScheduleAgeWindow.cs`, `ProgramScheduleAgeWindowRule.cs`,
`ProgramScheduleAgeWindowSettings.cs`.

| Member | Zweck |
|--------|-------|
| `IsAllowedAt(minAge, utcStart, rule)` | Altersfenster-Prüfung am lokalen Slot-Start: `null` → `UnknownMode` entscheidet (`Unrestricted` → true, sonst Fsk18-Fenster); `>=18` → `Fsk18Earliest`, `>=16` → `Fsk16Earliest`, `>=12` → `Fsk12Earliest`, darunter immer erlaubt; erlaubt ist `local >= earliest \|\| local < NightEnd` — die Prüfung gilt am Start, ein begonnener Titel läuft über die Fenstergrenze hinaus |
| `CountBlockedAt(minAges, utcStart, rule)` | Zählt gesperrte Kandidaten (Diagnose) |
| `ProgramScheduleAgeWindowRule(Windows, UnknownMode)` | Fensterschema + Unknown-Modus als Einheit |
| `ProgramScheduleAgeWindowSettings` | `Fsk12Earliest`/`Fsk16Earliest`/`Fsk18Earliest`/`NightEnd`; `Default` = 20:00/22:00/23:00/06:00; `IsValid()` = monoton + `NightEnd < Fsk12` |

## `ProgramScheduleOptions`

Datei: `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs` (Section `ProgramSchedule`).

| Eigenschaft | Default | Zweck |
|-------------|---------|-------|
| `TrailerLeadDays` → `EffectiveTrailerLead` | 7 Tage | Vorlauffenster für Trailer-Füller |
| `MinStartRemainSeconds` → `EffectiveMinStartRemain` | 30 s | Mindestrest vor einer Fenstergrenze für einen Item-Start |
| `MaxOccurrenceLookbackDays` → `EffectiveMaxOccurrenceLookback` | 7 Tage (Clamp 30) | Maximales Reconcile-Rückblickfenster |

## `ProgramRuleValidity`

Datei: `VideoWebPlayer/Services/ProgramRuleValidity.cs` — `IsValidOn(rule, localDay)`
(Z. 34) wertet `ValidityMode` auf lokalen Tagen aus (in `BuildOccurrences` und der
Filler-Tagesprüfung in `FillGaps` aufgerufen); `IsExpired` für den OneTime-Purge.

## `ProgramRuleService` / `ProgramSettingsService` (verwendete Teile)

| Member | Datei:Zeile | Zweck im Kontext |
|--------|-------------|------------------|
| `GetOrCreateChannelAsync` | `ProgramRuleService.cs`:52 | Liefert/legt den Kanal an |
| `ApplyValidity(rule, input)` | `ProgramRuleService.cs`:190 (`internal`) | Modus-Feld-Normalisierung; vom Test-Helper `CreateRuleAsync` genutzt |
| `GetProgramScheduleHorizonAsync` | `ProgramSettingsService.cs`:345 | Planungshorizont (`Setup.ProgramScheduleHorizonHours`, Default 48 h; Tests setzen 7 Tage) |
| `GetProgramScheduleIntervalAsync` | `ProgramSettingsService.cs`:357 | Worker-Intervall (Default 15 min) |
| `GetTvSenderEnabledAsync` | `ProgramSettingsService.cs` | Feature-Flag (Worker) |
| `GetHolidayFederalStateAsync` | `ProgramSettingsService.cs` | Bundesland für `Holiday`-Recurrence |
| `GetProgramScheduleContentScopeAsync` | `ProgramSettingsService.cs`:392 | Content-Scope (Access-Kontext) |
| `GetProgramScheduleAgeWindowsAsync` | `ProgramSettingsService.cs`:426 | Fensterschema (Default bei ungültigen Werten) |
| `GetProgramScheduleUnknownRatingModeAsync` | `ProgramSettingsService.cs` | Unknown-Rating-Modus |

## `ProgramGuideService` / `ProgramClipGroupService` / `ProgramGuideController` (Rand)

`ProgramGuideService` (`Services/ProgramGuideService.cs`) liest den fertigen Plan für die
EPG-Anzeige; `ProgramClipGroupService` verwaltet die Clip-Gruppen (CRUD, Trailer-Gruppe
geschützt); `ProgramGuideController` ruft `EnsureCoverageAsync`/`ReconcileAsync` für
„Jetzt generieren" auf. Für die Anforderung nur konsumierend relevant.
