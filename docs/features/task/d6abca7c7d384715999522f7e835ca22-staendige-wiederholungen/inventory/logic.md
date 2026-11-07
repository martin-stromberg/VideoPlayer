# Logik: `ProgramScheduleService` — Strang-Belegung, Commit und Re-Bewertung

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (3127 Zeilen, Stand Commit `3b2a616`).

## Datenfluss im Überblick

```
ExpandMaterialAsync (je MaterialType; ExpandGenreAsync vergibt StrandKey
    show:{id} / collection:{id} / "other")
  → RuleState.SetItems            — gruppiert zu Strands, sortiert per StrandOrderKey,
                                    je Strang Ring (MixUnorderedRing) + NextIndexFor(Cursor)
  → BuildOccurrences              — Occurrences je Regel/Tag (Recurrence + Validity)
  → MaterializeOccurrence | MaterializeOccurrenceEvicting
        airingStart = ShiftPastInvaders(occurrence.StartUtc, blocked, outranked)
        bound       = BoundaryAfter(boundStarts, occurrence.StartUtc, priority)
      → SelectItems
            flach:       SelectAiringItems(Items, NextIndex, ...)
            interleaved: SelectAiringItemsInterleaved(Strands, rule, airingStartUtc, ...)
                wanted      = EpisodeCount>0 ? n : (SlotDuration!=null ? int.MaxValue : 1)
                startOffset = StrandStartOffset(wanted, m, airingStartUtc)   // ← Fehlerursache
      → CommitTaken → je Strang CommitScan(scanned[s], lastTaken[s])
      → InsertClipFill → FillAiringSegments (Clips in n+1 Segmente, Titel verschoben)
      → FillGaps (Filler-Regeln)
      → PersistCursors → ProgramRuleCursors je Strang (LastPlaced)
      → SaveChangesAsync
Re-Bewertung: ReconcileWindow.Reload → Rebuild (Airings/Evidenz) → Validate
              (AnchorProvable + SlotStillValid je Slot) → Suffix-Displace
              → RewindCursor → MaterializeOccurrenceEvicting → … wie oben
```

## Einstiegspunkte und Aufrufpfade

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecutePassAsync` | public | Produktiver Pass: `PurgeExpiredRulesAsync` → `CleanupSelfDisplacedSlotsAsync` → `ReconcileAsync` → `EnsureCoverageAsync` (Z. 75–86). Einziger produktiver Einstieg — vom `ProgramScheduleWorker` (ServiceCollectionExtensions Z. 283, `ProgramScheduleWorker.cs:72`). |
| `EnsureCoverageAsync` | public | Extend-only Befüllung `[coverageStart, horizon]`; ruft `MaterializeOccurrence` je Occurrence (Z. 89–177, Aufruf Z. 159). |
| `ReconcileAsync` | public | Re-Bewertung geschriebener Slots + Neuplanung; ruft `MaterializeOccurrenceEvicting` je Occurrence des Basisfensters (Z. 180–460, Aufruf Z. 406). |
| `GetRuleDiagnosesAsync` | public | Regeldiagnose: nutzt nur `ExpandMaterialAsync` + Zähler — **keine** Strang-Auswahl (Z. 472–529). |
| `GetScheduleAsync` | public | Liest persistierte Slots (Z. 463–469). |
| `MaterializeOccurrence` | private | Schreibt Titel-Slots einer Occurrence (Coverage-Pfad, Z. 1645–1727). Berechnet `airingStart` (Z. 1658), `SelectItems` (Z. 1674), `CommitTaken` (Z. 1725). |
| `MaterializeOccurrenceEvicting` | private | Gleiche Auswahl wie `MaterializeOccurrence`, aber mit Verdrängung der Konkurrenten (Reconcile-Pfad, Z. 1071–1157; `SelectItems` Z. 1102, `CommitTaken` Z. 1154). |
| `SelectItems` | private static→inst. | Gemeinsame Auswahl-Front: flach → `SelectAiringItems`; interleaved (`state.IsInterleaved`) → `SelectAiringItemsInterleaved` (Z. 1761–1778). Ermittelt `clipGrace = state.ClipGrace` (Z. 1765). |

**Aufrufer außerhalb:** `TVSenderAdmin.razor` ruft `GetRuleDiagnosesAsync`/`GetScheduleAsync`/`DeleteDisplacedSlotsAsync`/`ResetScheduleAsync`; „Jetzt generieren" signalisiert nur den Worker (`ScheduleSignal.NotifyScheduleChangedNow`, Z. 1532–1541). Kein Vorschau-/Simulationspfad nutzt die Strang-Belegung.

## Strang-Aufbau und Ordnung

| Methode/Typ | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RuleState` | private class | Zustand einer Regel: `Items` (Expansionsliste), `Strands` (Ringe), `ClipGroups` (außerhalb von `Strands`), `IsInterleaved => Strands.Count > 1` (Z. 1258–1347). Flachmodus-Proxies (`NextIndex`, `Commit`, …) auf `Strands[0]` (Z. 1285–1291). |
| `RuleState.SetItems` | public (inner) | Baut die Stränge (Z. 1295–1343): Genre-Regeln gruppieren Items per `StrandKey` (`show:{id}`, `collection:{id}`, Fallback `OtherStrandKey`); bei >1 Gruppen Sortierung per `StrandOrderKey(Rule.Id, key)`, je Strang `MixUnorderedRing` + `NextIndexFor(ring, FindCursor(cursors, key))`. Flachpfad: ein Strang `Key == null` über derselben Ringliste wie `Items`. |
| `StrandState` | private class | Ein Ring je Strang (Z. 1190–1228): `Key`, `Items`, `NextIndex`, `LastPlaced`, `PreservesOrder` (alle Items `Sequenced` oder `TVShowEpisode`/`PlaylistEntry`, Z. 1203–1204), `PeekAt(offset)`, `Commit(count)`, `CommitScan(scanned, lastTaken)` (Z. 1220–1225: `NextIndex += scanned`, `LastPlaced` nur auf letzten Take), `RewindTo(index)`. |
| `ClipGroupState` | private class | Clip-Gruppen-Ring `clipgroup:{id}` außerhalb der Titel-Stränge (Z. 1233–1256); `IsTrailer` für die reservierte Gruppe; `SetItems` mischt per `MixUnorderedRing` und verankert per `SelectStrandCursor`. |
| `OtherStrandKey` | private const | `"other"` — geteilter Strang sammlungsloser Genre-Filme (Z. 1184). |
| `StrandOrderKey` | internal static | FNV-1a über `{ruleId}:{strandKey}` — deterministische, prozessinvariante Strang-Reihenfolge (Z. 1364–1365). |
| `ItemOrderKey` | internal static | FNV-1a über `{ruleId}:{strandKey}:{contentType}:{contentId}` — Hash-Mischung ungeordneter Ringe (Z. 1372–1373). |
| `Fnv1aHash` | private static | Hash-Kern beider Ordnungsschlüssel (Z. 1375–1387). |
| `IsUnorderedRing` | private static | Alle Items `!Sequenced` und `Movie`/`MediaItem` (Z. 1393–1395). |
| `MixUnorderedRing` | private static | Sortiert ungeordnete Ringe per `ItemOrderKey` + `ContentId`; geordnete Ringe unverändert (Z. 1400–1405). |
| `SelectStrandCursor` | internal static | Deterministischer Gewinner unter Cursor-Zeilen eines Strangs: höchstes `UpdatedAt`, Tie-Break höchste `Id` (Z. 1411–1416). |
| `NextIndexFor` | private static | Ringindex hinter dem Cursor-Inhalt; 0 bei fehlendem Cursor/Treffer (Z. 1597–1604). |
| `StrandStartOffset` | private static | **Korrekturstelle** (Z. 1421–1424): `wanted < strandCount` → `((startUtc.Date - UnixEpoch).Days % strandCount)`; sonst 0. Arbeitet auf dem **UTC-Datum des realen (ggf. verschobenen) airingStart**. |

## Titel-Auswahl (`SelectAiringItemsInterleaved` und Verwandte)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SelectAiringItemsInterleaved` | private static | **Zentrale Korrekturstelle** (Z. 1806–1897). `wanted` (Z. 1811–1812): `EpisodeCount > 0` → Wert; `SlotDuration != null` → `int.MaxValue`; sonst 1. `target` = `SlotDuration` wenn kein `EpisodeCount`. `startOffset = StrandStartOffset(wanted, m, airingStartUtc)` (Z. 1829). Lap-Schleife: `for i<m: s = (startOffset+i) % m` — je Lap Neustart beim Offset (Z. 1838–1840). Pro Strang: `done[s]` (ruht), `scanned[s]` (konsumierte Positionen), `skipsInRow[s]` (Umlauf-Wächter). Clip-Grace-Abbruch `target - accumulated < clipGrace` vor dem Strang-Scan (Z. 1844–1848, `graceHit`). Altersfenster: geordneter Strang → `done[s]=true` (Defer, Position nicht konsumiert, Z. 1857–1862); ungeordneter Ring → `scanned[s]++` (Skip), voller Umlauf → `done[s]` (Z. 1864–1869). Bound: Start verboten (`< minStartRemain`) oder hartes Bound ohne Abschluss → `done[s]` (Z. 1872–1876). Take: `picks.Add`, `scanned[s]++`, `slotStart`/`accumulated` weiter (Z. 1877–1884). Abbruch: `graceHit`, kein Fortschritt, `accumulated >= target`, oder `noProgressLaps > maxLap` (Z. 1889–1894). |
| `SelectAiringItems` | private static | Flachvariante mit identischer Semantik (`wanted`/`target`/Grace/Bound/Fenster, Z. 1908–1963); unverändert zu belassen (Scope-Ausschluss). |
| `CommitTaken` | private static | Verteilt die Selection auf die Stränge: interleaved je Strang `CommitScan(Scanned[i], lastTaken[i])` aus `Picks`; flach `CommitScan` auf Strang 0 (Z. 1782–1796). |
| `SelectionResult`/`StrandPick`/`InterleavedSelection`/`FlatSelection` | private records | Ergebnis-Container (Z. 1742–1759): `SelectionResult(Taken, Picks, Scanned, BlockedByAgeWindow)`; `StrandPick(Strand, Item)`; `InterleavedSelection(Picks, Scanned[], BlockedByAgeWindow)`; `FlatSelection(Taken, Scanned, BlockedByAgeWindow)`. |
| `RuleState.ClipGrace` | property | `PlaceholderGrace` nur wenn `ClipGroups.Count > 0 && SlotDuration != null` — sonst `null`, Auswahl wie zuvor ohne Grace (Z. 1279–1281). |
| `BoundaryAfter` | private static | Erste Bound-Occurrence nach `afterUtc`, die die Regel nicht outranked: `(StartUtc, Hard)` — `Hard` bei strikt höherer Priorität (Z. 1988–1994). |
| `BoundStarts` | private static | Sortierte Starts aller item-tragenden Occurrences (Z. 1969–1975). |
| `ShiftPastInvaders` | private static | Verschiebt den Start hinter belegte Intervalle (Fixpunkt); `outranked`-Intervalle höherpriorer Regeln binden schon bei Berührung (Z. 1426–1458). |
| `WindowLostToTakeover` | private static | Komplette Fenster-Übernahme durch höherpriore Kette → Occurrence verfällt (Z. 1471–1477). |

**Einflussgrößen am Pick:** `startOffset` (nur Strang-Reihenfolge der Lap-Starts), `done[s]`/`scanned[s]`/`skipsInRow[s]` (Strang-Rest), `slotStart`/`accumulated` (laufende Sendezeit; Age-Window und Grace dagegen), `bound` (Fensterende), `airingStartUtc` (Startbasis **und** Offset-Datum).

## Cursor-Fortschreibung und Rewind

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PersistCursors` | private | Schreibt je Strang mit `LastPlaced != null` eine `ProgramRuleCursors`-Zeile (`StrandKey`, `LastContentType`, `LastContentId`) — update oder insert (Z. 2150–2182); Clip-Gruppen-Ringe eingeschlossen. |
| `RewindCursor` | private static | Je verdrängter Regel: Titel-Slots → `RewindRing` je `Strands`, Clip-Slots → `RewindRing` je `ClipGroups` (Z. 868–883). |
| `RewindRing` | private static | Setzt `NextIndex` auf den frühesten verdrängten, noch auflösbaren Inhalt zurück (nur wenn der Ring darüber hinaus ist) und spiegelt den Vorgänger in die Cursor-Zeile (Z. 889–917). |

## Re-Bewertung (`ReconcileWindow`, `SlotStillValid`, Anker)

| Methode/Typ | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ReconcileWindow` | private class | Fensterzustand des Reconcile (Z. 925–1063): `Scheduled`, `Occurrences`, `BoundStarts`, `Airings`, `FillerSlots`, `InvalidGroups`, `HasRescuableInvalid`. `Reload` = atomar `Rebuild` + `Validate` (Z. 952–957). `Rebuild` leitet Evidenz, Occurrences, Airings (`GroupAirings`, an `coverageStart` geteilt) ab (Z. 959–984). |
| `ReconcileWindow.Validate` | private | Je Regel die Airings in Startreihenfolge; vollständig ausgestrahlte Gruppen übersprungen. Vorprüfung: `RuleMissing`/`RuleInactive`/`NoOccurrenceAnchor`/`TitlelessAiring`. Dann `AnchorProvable` am Gruppenkopf, danach `SlotStillValid` je Slot mit `lastIndex`-Array **je Gruppe neu** (Z. 1034–1035). Erste invalide Ausstrahlung je Regel → `InvalidGroups` (Z. 986–1062). |
| `SlotStillValid` | private static | Titelweise Prüfung eines Slots (Z. 681–747). Clip-Slot: Mitglied im Clip-Ring + Laufzeit + Fenster. Titel-Slot: `FindItemIndex` über alle `state.Strands` (Mitgliedschaft), `MatchingItemDuration`, `IsAllowedAt` am Slot-Start, dann **nur bei `strand.PreservesOrder`** Ring-Kontiguität `idx == (lastIndex[strandIndex]+1) % count` → sonst `StrandSequenceBroken`; `lastIndex[strandIndex]`-Baseline je Gruppe ungeprüft. |
| `AnchorProvable` | private static | Gruppenkopf zurückführbar auf nominalen Occurrence-Start ≤ Kopf: exakt, `ShiftProvable`-Kette oder `UnwrittenHeadroom` (Z. 790–819). |
| `UnwrittenHeadroom` | private static | `SlotDuration` (auch interleaved) bzw. `(wanted-1)·maxItemDuration` — Kopf-Luft einer teilausgestrahlten Occurrence (Z. 764–776). |
| `AiringRealStart`/`ShiftProvable`/`FindItemIndex` | private static | Rückfaltung eines boundary-geteilten Kopfs (Z. 826–832); Invader-Ketten-Beweis (Z. 841–847); Inhaltssuche MediaItem → Movie/Episode (Z. 853–863). |
| `GroupAirings` | private static | Lückenlose Ketten gleicher Regel; Teilung an `coverageStart` (Z. 628–646). |
| `ClipMediaItemIds` | private static | Clip-Vorklassifikationsmenge aus allen `ClipGroups`-Ringen (Z. 650–657). |

**Befund zur offenen Frage (Re-Bewertung vs. Pick-Reihenfolge):** `SlotStillValid` kennt die Pick-Reihenfolge nicht — es lokalisiert jeden Slot nur in *irgendeinem* Strang und prüft die Ring-Kontiguität geordneter Stränge **innerhalb einer Gruppe**. Da die Rotation nur die Reihenfolge *zwischen* Strängen ändert (+1-Schritte je Strang bleiben), entstehen durch rotierte Belegung keine `StrandSequenceBroken`-Fehlinvalidierungen. Umgekehrt validiert eine mit Offset-0 geschriebene Ausstrahlung denselben Prüflauf ebenfalls fehlerfrei — die „Ersetzung falsch belegter Tage" ist im heutigen Mechanismus nicht enthalten.

## Material-Expansion (StrangKey-Vergabe)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExpandMaterialAsync` | private | Dispatch je `MaterialType` (Z. 2186–2210). |
| `ExpandGenreAsync` | private | Genre-Union (Z. 2406–2512): Episoden → `StrandKey = "show:{id}"` (Z. 2510); Sammlungsfilme behalten `collection:{id}` aus `TryMovieItem`; sammlungslose Filme → `OtherStrandKey` (Z. 2506). |
| `TryMovieItem` | private | Vergibt `StrandKey = "collection:{id}"` und `Sequenced = true` bei `MovieCollectionId` (Z. 2299–2305) — Ursache der Strang-Explosion seit Issue #334. |
| `ToItem`/`ExpandShowAsync`/`ExpandMovieCollectionAsync`/`ExpandMovieAsync`/`ExpandPlaylistAsync`/`ExpandTrailersAsync`/`ExpandMediaSourceClipsAsync` | private | Übrige Expansionspfade (Z. 2308–2744); nur Genre-Material erzeugt >1 Strang. |
| `BuildClipGroupStates`/`ExpandClipGroupStatesAsync`/`ExpandClipGroupItemsAsync`/`FilterClipCandidatesAsync` | private | Clip-Gruppen-Ringe (Z. 2786–2890); nicht Teil von `Strands`. |

## Clip-Fill und Lücken

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InsertClipFill`/`FillAiringSegments`/`NextClipItem`/`TailLimit`/`ChainLimit`/`PeriodStart`/`FindLeadMatchOffset` | private | Verteilt Clip-Slots in die n+1 Segmente von `SlotDuration`-Ausstrahlungen mit Clip-Gruppen und verlegt die Titel-Slots hinter die Clips (Z. 2586–2600, 2897–3127). `PeriodStart` = letzter Occurrence-Start ≤ Kopf; `periodEnd = periodStart + SlotDuration`. |
| `FillGaps`/`GapSeconds` | private | Füller-Regeln in verbleibende Lücken (Z. 1996–2146). |

## Interne Typen (Auswahl)

| Typ | Zweck |
|-----|-------|
| `SchedulableItem` (record, Z. 1161–1179) | Planbarer Titel: `ContentType`, `ContentId`, `MediaItemId`, `MovieId`, `TVShowEpisodeId`, `Duration`, `StrandKey`, `AgeRatingMinAge`, `MediaItemAlternates`, `Sequenced`. |
| `Occurrence` (record, Z. 1349) | `(Rule, StartUtc)` — nominaler Occurrence-Start. |
| `Interval` (struct, Z. 1351–1354) | `(Start, End)` + `Overlaps`. |
| `ClipFillContext` (record, Z. 2753–2770) | Kontext der Clip-Pässe inkl. mutablem `RoundRobin` je Regel. |
| `AccessContext` (class, Z. 1505–1526) | Zuschauer-Freigabe-Snapshot. |
| `SummaryCounters` (class, Z. 1479–1500) | Laufzähler inkl. Diagnose-Counter. |
| `MediaItemRef`/`MediaItemVariant`/`MediaItemChoice`/`MediaItemLink`/`ClipCandidate`/`ProxyEpisode` | Hilfs-Records der Expansion. |

## Weitere beteiligte Dateien

- `VideoWebPlayer/Services/ProgramScheduleWorker.cs` — einziger produktiver Aufrufer (`ExecutePassAsync` im konfigurierten Intervall bzw. per Signal, Z. 57–80).
- `VideoWebPlayer/Services/ProgramScheduleAgeWindow.cs`/`ProgramScheduleAgeWindowRule.cs`/`ProgramScheduleAgeWindowSettings.cs` — Fensterprüfung `IsAllowedAt`/`CountBlockedAt`.
- `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs` — `TrailerLeadDays`, `MinStartRemainSeconds` (30 s), `MaxOccurrenceLookbackDays` (≤30 d), `ScheduleChangedDebounceSeconds`.
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Admin-UI (Diagnose, Plan-Anzeige, „Jetzt generieren" = Signal, Reset).
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs:283` — DI-Registrierung `IProgramScheduleService` (scoped).
