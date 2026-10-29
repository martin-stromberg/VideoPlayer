# Logik – Bestandsaufnahme

Kernbereich: die Neu-Bewertung des Sendeplans (`ReconcileAsync`) in
`VideoWebPlayer/Services/ProgramScheduleService.cs` (3139 Zeilen, `sealed class`, einzige
`IProgramScheduleService`-Implementierung). Alle Reconcile-Interna sind `private` bzw.
`private sealed` — kein separates Interface, kein eigener Service nötig.

## `ProgramScheduleService`

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs`

### Öffentliche Oberfläche

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` | public | Extend-only-Ausbau: füllt `[max(now, letztes Slot-Ende), now+Horizont]`; verdrängt nie (Zeilen 75–155) |
| `ReconcileAsync` | public | Neu-Bewertung: Fenster laden → validieren → Suffix-Verdrängung → Neu-Materialisierung → Clip-Fill/Lücken → Cursor persistieren (Zeilen 158–412) |
| `GetScheduleAsync` | public | Slots ab `fromUtc` inkl. Status (Zeilen 415–421) |
| `GetRuleDiagnosesAsync` | public | Pro-Regel-Diagnose, Admin (Zeilen 424–481) |
| `DeleteDisplacedSlotsAsync` | public | `ExecuteDelete` aller `Displaced`-Zeilen, Admin (Zeilen 484–492) |
| `PurgeExpiredRulesAsync` | public | Löscht abgelaufene `OneTime`-Regeln (Zeilen 495–518) |
| `ResetScheduleAsync` | public | Löscht alle Slots + Cursor, Admin (Zeilen 521–536) |
| `RequireAdminAsync` | private | Admin-Prüfung für die Admin-Methoden (Zeilen 538–545) |

### `ReconcileAsync` — Ablauf (Zeilen 158–412)

1. Kanal + alle Regeln laden (inkl. inaktiver — deren verdrängte Slots brauchen Cursor-Rewind).
2. `RuleState` je Regel aufbauen (Expansion via `ExpandMaterialAsync`, Cursor via
   `ProgramRuleCursors`; Trailer-/Clip-Gruppen expandieren sofort, da `airingsByMovie` aus
   persistierten Slots bekannt).
3. `LoadWindowAsync(from)` (lokale Funktion, Zeilen 215–228): lädt `Scheduled`- und
   `Displaced`-Zeilen mit `End > from`; Trennung über projizierten DB-Status (nicht Entity-
   Property — Bulk-`ExecuteUpdate` kann getrackte Instanzen veralten lassen). Displaced-Zeilen
   dienen ausschließlich als Invader-Evidenz.
4. Basisfenster `coverageStart - OccurrenceLookback` (24 h); Widening auf
   `coverageStart - _maxOccurrenceLookback` (`ProgramScheduleOptions.MaxOccurrenceLookbackDays`,
   Default 7 d, Clamp 30 d) per Trigger (`Start <= loadFrom`) oder validierungsgetrieben
   (`window.HasRescuableInvalid`).
5. `ReconcileWindow.Reload` → `Rebuild` + `Validate` (atomar re-derived).
6. Suffix-Verdrängung über `InvalidGroups`: ab `firstInvalid` die gesamte verbleibende
   Regel-Zukunft (`Displace` setzt `Status = Displaced`; Slots mit `End <= coverageStart`
   bleiben Historie).
7. `FillerSlots` einzeln geprüft: invalid bei fehlender/inaktiver Regel oder wenn
   `slot.MediaItemId` nicht mehr in `state.Items` enthalten.
8. `RewindCursor` je betroffener Regel (früheste noch expandierbare verdrängte Position →
   Ring-Cursor zurück).
9. Neu-Materialisierung über `MaterializeOccurrenceEvicting` je Occurrence (nur Basis-Fenster,
   `StartUtc >= coverageStart - OccurrenceLookback`); Verlierer-Slots verdrängen ihre Regel-
   Zukunft (`DisplaceRuleFuture`) bzw. einzeln (Filler).
10. `InsertClipFill` + `FillGaps` über die neuen Slots, `PersistCursors`, `SaveChangesAsync`.
    Ergebnis `ProgramScheduleReconcileSummary` (`SlotsDisplaced` = `displaced.Count`).

Lokale Funktionen in `ReconcileAsync`: `LoadWindowAsync`, `Displace`, `DisplaceRuleFuture`,
`Evicts`.

### Gruppierung & Validierung (der Erweiterungspunkt der Anforderung)

| Methode / Typ | Sichtbarkeit | Kurzbeschreibung |
|---------------|-------------|------------------|
| `GroupAirings` | private static | Gruppiert sortierte Slots zu Ausstrahlungen: maximale lückenlose Runs gleicher `ProgramRuleId` (`prev.End == slot.Start`) — die „Sendeblöcke" der Anforderung (Zeilen 551–567) |
| `ReconcileWindow` | private sealed class | Fenster-State: `Scheduled`, `Occurrences`, `BoundStarts`, `Airings`, `FillerSlots`, `InvalidGroups`, `HasRescuableInvalid`; `Reload(loadFrom, scheduled, displacedEvidence)` → `Rebuild` + `Validate` atomar (Zeilen 1069–1157) |
| `ReconcileWindow.Rebuild` | private | `_invaderStarts` aus Scheduled ∪ DisplacedEvidence gruppiert nach `End` → min `Start`; `BuildOccurrences` mit Fenster-Lookback; `_occurrenceStarts` je Regel; `BoundStarts`; `GroupAirings` über Nicht-Filler; `FillerSlots`-Liste (Zeilen 1103–1126) |
| `ReconcileWindow.Validate` | private | Je Regel-Gruppe: Airings zeitlich sortiert, erste invalide Ausstrahlung markiert `firstInvalid`; invalid = Regel fehlt/inaktiv/kein Occurrence-Anker/`!AiringReproducible`; `HasRescuableInvalid` bei aktiver Regel (Zeilen 1128–1156) |
| `ReproContext` | private sealed record | Rekursions-Kontext + Memo `Dictionary<(Offset, AfterStart), bool>` für die Tail-Checks (Zeilen 572–579) |
| `AiringReproducible` (Overload 1) | private static | Clip-bewusster Einstieg: `ClipMediaItemIds`-Klassifikation, Clip-Slots → Mitgliedschafts- und Lückenlosigkeitsprüfung; Titel-Teilfolge auf clip-freie Zeitachse projiziert (Zeilen 607–648) |
| `AiringReproducible` (Overload 2) | private static | Rekursiver Dispatcher `(airing, offset, afterStart)`: flat vs. interleaved Tail-Check mit Memo (Zeilen 656–668) |
| `FlatAiringTailReproducible` | private static | Flacher Tail-Check: Rückwärts-Hypothesen k über genommene-aber-nicht-geschriebene Kopf-Items (inkl. Fenster-Skips), rekonstruierter Real-Start, Occurrence-Anker + Invader-Evidenz, Re-Simulation via `SelectAiringItems`, Slot-Vergleich via `SlotMatchesItem` (Zeilen 670–783) |
| `InterleavedAiringTailReproducible` | private static | Mehrsträngiges Pendant: Strang-Zuordnung der Slots, Präfix-Pick-Hypothesen p, `StrandStartOffset`-Konsistenz, Re-Simulation via `SelectAiringItemsInterleaved` (Zeilen 795–993) |
| `SlotMatchesItem` | private static | Toleranter Slot-Vergleich (Issue #290): Start exakt, Laufzeit muss zur gespeicherten Datei passen (`item.MediaItemId` oder `MediaItemAlternates`); entlinkte Datei → kein Match (Zeilen 3130–3138) |
| `FindItemIndex` | private static | Lokalisiert Slot-Inhalt in der Expansion: `MediaItemId`, dann `MovieId`/`TVShowEpisodeId`-Fallback (Zeilen 997–1007) |
| `ClipMediaItemIds` | private static | MediaItemIds aller Clip-Gruppen-Ringe — Clip-vs-Titel-Klassifikation (Zeilen 598–605) |

### Verdrängungs- und Materialisierungsmechanik

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `MaterializeOccurrenceEvicting` | private | Wie `MaterializeOccurrence`, gibt aber Konflikt-Kept-Slots als Verlierer zurück (Zeilen 1165–1238) |
| `MaterializeOccurrence` | private | Extend-only-Materialisierung mit atomarer Konfliktverwerfung (Zeilen 1680–1737) |
| `ShiftPastInvaders` | private static | Schiebt Occurrence-Start hinter belegte Intervalle (Zeilen 1498–1514) |
| `RewindCursor` / `RewindRing` | private static | Cursor-Rücklauf auf Vorgänger des frühesten verdrängten, noch expandierbaren Inhalts; Titel-Stränge vs. Clip-Ringe getrennt (Zeilen 1012–1061) |
| `SelectItems` / `SelectAiringItems` / `SelectAiringItemsInterleaved` | private | Auswahl-Scan (EpisodeCount/SlotDuration, `minStartRemain`, weiche/harte Bounds, Altersfenster, `clipGrace`); liefert Taken + Scan-Spur (`Positions`/`Picks`, `Scanned`) (Zeilen 1772–1974) |
| `CommitTaken` | private static | Commit der Auswahl: `NextIndex` hinter Scan-Ende, `LastPlaced` = letztes genommenes Item (Zeilen 1793–1807) |
| `BoundStarts` / `BoundaryAfter` | private static | Fenstergrenzen aus Occurrence-Starts item-tragender Regeln; soft/hard nach Priorität (Zeilen 1979–1999) |
| `BuildOccurrences` | private static | Occurrence-Starts je Regel über lokale Tage × Gültigkeit × Recurrence (Zeilen 1641–1678) |
| `FillGaps` / `GapSeconds` | private | Füller-Auffüllung verbleibender Lücken (Zeilen 2001–2151) |
| `InsertClipFill` / `FillAiringSegments` / `NextClipItem` / `TailLimit` / `ChainLimit` / `PeriodStart` | private | Clip-Füllung in SlotDuration-Ausstrahlungen; einzige `IsClipFill`-Erzeugungsstelle (Zeilen 2743–3123) |
| `PersistCursors` | private | Schreibt `ProgramRuleCursor` je Strang mit `LastPlaced` (Zeilen 2155–2187) |

### Ring-/Strang-Mechanik (Issue #297)

| Methode / Typ | Sichtbarkeit | Kurzbeschreibung |
|---------------|-------------|------------------|
| `StrandState` | private sealed class | Ein Ring je Strang: `Items`, `NextIndex`, `LastPlaced`, `PreservesOrder`, `PeekAt`, `Commit`, `CommitScan`, `RewindTo` (Zeilen 1265–1302) |
| `RuleState` | private sealed class | Regel-State: `Items`, `Strands`, `ClipGroups`, `IsInterleaved`, `ClipGrace`, Flat-Delegaten, `SetItems` (Strang-Aufbau + Ring-Mix) (Zeilen 1332–1419) |
| `ClipGroupState` | private sealed class | Clip-Gruppen-Ring, `StrandKey = "clipgroup:{id}"`, Trailer-Gruppen-Erkennung (Zeilen 1307–1330) |
| `StrandOrderKey` / `ItemOrderKey` / `Fnv1aHash` | internal/private static | Prozess-invariante FNV-1a-Ordnungsschlüssel (Zeilen 1436–1459) |
| `MixUnorderedRing` / `IsUnorderedRing` | private static | Deterministische Ringmischung ungeordneter Ringe (Issue #297, Zeilen 1465–1477) |
| `SelectStrandCursor` | internal static | Deterministischer Cursor-Sieger bei Duplikaten (Zeilen 1483–1488) |
| `StrandStartOffset` | private static | Start-Strang interleaveter Ausstrahlungen aus Real-Start-Tag (Zeilen 1493–1496) |
| `NextIndexFor` | private static | Ring-Index hinter Cursor-Position (Zeilen 1632–1639) |

### Expansion & Kontext

| Methode / Typ | Sichtbarkeit | Kurzbeschreibung |
|---------------|-------------|------------------|
| `ExpandMaterialAsync` | private | Dispatch je `MaterialType` auf die Expand*-Methoden (Zeilen 2191–2215) |
| `ExpandShowAsync`, `ExpandMovieAsync`, `ExpandMovieCollectionAsync`, `ExpandGenreAsync`, `ExpandPlaylistAsync`, `ExpandTrailersAsync`, `ExpandMediaSourceClipsAsync`, `ExpandClipGroupItemsAsync`, `FilterClipCandidatesAsync` | private | Materialexpansion je Typ → `SchedulableItem`-Liste (Zeilen 2306–2887) |
| `MovieMediaItemsAsync` / `EpisodeMediaItemsAsync` / `ToMediaItemChoices` | private | Repräsentanten-Auswahl + `MediaItemAlternates` (Issue #290, Zeilen 2234–2263) |
| `TryMovieItem` / `ToItem` | private | Kandidat → `SchedulableItem` (Dauer + Freigabe) (Zeilen 2285–2356) |
| `LoadAccessContextAsync` / `AccessContext` | private | Publikums-Freigabe-Snapshot (Zeilen 1542–1618) |
| `LoadAgeRuleAsync` | private | Jugendschutz-Regel (`ProgramScheduleAgeWindowRule`) (Zeilen 1625–1628) |
| `LoadMovieAiringsAsync` / `BuildMovieAiringsFromSlotsAsync` / `BuildMovieAiringsMap` / `TrailerMediaItemIdsAsync` | private | Film-Ausstrahlungskarte für Trailer-Vorlauf (Zeilen 2599–2665) |
| `ExpandTrailerStatesAsync` / `ExpandClipGroupStatesAsync` / `BuildClipGroupStates` | private | Nachexpansion des Trailer-Materials/-Rings (Zeilen 2513–2530, 2783–2821) |

Interne Records: `SchedulableItem` (inkl. `MediaItemAlternates`, Zeilen 1242–1255),
`MediaItemRef`, `MediaItemVariant`, `MediaItemChoice`, `MediaItemLink`, `SelectionResult`,
`StrandPick`, `FlatSelection`, `InterleavedSelection`, `Occurrence`, `Interval`,
`ClipFillContext` (inkl. `RoundRobin`), `ClipCandidate`, `ProxyEpisode`, `SummaryCounters`.

### Konfigurations- und Zeitkonstanten

- `OccurrenceLookback = 24 h` — Basis-Lookback des Reconcile-Fensters (Zeile 26).
- `DiagnosisOccurrenceHorizonDays = 9` — Horizont der Diagnose-Occurrence-Suche (Zeile 33).
- `_minStartRemain` ← `ProgramScheduleOptions.EffectiveMinStartRemain` (Default 30 s).
- `_trailerLead` ← `ProgramScheduleOptions.EffectiveTrailerLead` (Default 7 d).
- `_maxOccurrenceLookback` ← `ProgramScheduleOptions.EffectiveMaxOccurrenceLookback`
  (Default 7 d, Clamp 30 d) — `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs`.

## `ProgramScheduleWorker`

Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs`

`BackgroundService`; Takt aus `ProgramSettingsService.GetProgramScheduleIntervalAsync`
(Default-Fallback 15 min), Wake-up zusätzlich über `IProgramScheduleSignal.WaitAsync`.
Im `TvSenderEnabled`-Block hinter `IBackgroundProcessingGate`-Lease:
`PurgeExpiredRulesAsync` → `ReconcileAsync` → `EnsureCoverageAsync` (Zeilen 63–77).
`EnsureCoverageAsync` selbst verdrängt nicht.

## `ProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

Thread-sicherer Singleton: 1-Slot-`Channel<bool>` (DropWrite) zum Koaleszieren,
`_activeScans`/`_suppressedNotifications`-Zähler für Scan-Suppression.

## `ProgramScheduleAgeWindow`

Datei: `VideoWebPlayer/Services/ProgramScheduleAgeWindow.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsAllowedAt` | public static | Altersfenster-Check zum lokalen Slot-Start; gestartetes Item darf über die Fensterkante laufen (Zeilen 27–57) |
| `CountBlockedAt` | public static | Zählt blockierte Kandidaten (Diagnose) (Zeilen 63–65) |

## `TVSenderAdmin.razor` (UI-Beobachtungsort)

Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`

- „Verdrängt"-Chip: `slot.Status == ProgramSlotStatus.Displaced` → `admin-chip-warning`
  (Zeile 615), Kartenklasse `tvsender-slot-displaced` (Zeile 601).
- Aktion „Verdrängte bereinigen" (`tvsender-cleanup-displaced`, Zeile 510) →
  `DeleteDisplacedSlotsAsync` (Zeile 1568).
- „Jetzt generieren" (`tvsender-generate-now`, Zeile 509) → `GenerateNowAsync` →
  `ScheduleSignal.NotifyScheduleChanged()` (Zeilen 1532–1541) — triggert die Worker-Kette,
  nicht `ReconcileAsync` direkt.
- `BuildScheduleDisplayItems` gruppiert `IsClipFill && Scheduled`-Runs zu Clip-Blöcken;
  `Displaced`-Clip-Slots brechen die Gruppierung.
