# Logik-Bestandsaufnahme: Sendeplan-Generierung und Material-Expansion

Analysiert wurde `VideoWebPlayer/Services/ProgramScheduleService.cs` (2967 Zeilen) sowie die
aufrufenden/angrenzenden Klassen. Schwerpunkt: die Reihenfolge, in der `SchedulableItem`-Ringe
aufgebaut und entnommen werden — Ursache des beobachteten Alphabet-/Id-Bias.

## `ProgramScheduleService`

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProgramScheduleService(...)` | public ctor | Injiziert `ApplicationDbContext`, `IProgramRuleService`, `ProgramSettingsService`, `ILogger`, optional `MediaProbeOptions`/`ProgramScheduleOptions` (Trailer-Vorlauf, MinStartRemain, MaxOccurrenceLookback). |
| `EnsureCoverageAsync` | public | Extend-only-Lauf: lädt Regeln + `ProgramRuleCursor`s, expandiert Material (Trailer erst nach Materialisierung), baut `RuleState`s, materialisiert Occurrences nach Priorität, füllt Lücken (`FillGaps`, `InsertClipFill`), persistiert Cursor + Slots (Z. 75-155). |
| `ReconcileAsync` | public | Re-Bewertung: gleiche Expansion für alle Regeln (auch inaktive), `ReconcileWindow`-Validierung, Suffix-Verdrängung, `RewindCursor`, Re-Materialisierung mit Eviction, Clip-Fill + `FillGaps`, `PersistCursors` (Z. 158-397). |
| `GetScheduleAsync` | public | Slots ab `fromUtc` sortiert nach `Start` (Z. 400-406). |
| `GetRuleDiagnosesAsync` | public | Admin-Diagnose: ruft pro Regel `ExpandMaterialAsync` auf und zählt Skip-Gründe (Z. 409-466). |
| `DeleteDisplacedSlotsAsync` | public | Löscht `Displaced`-Slots (Admin) (Z. 469-477). |
| `ResetScheduleAsync` | public | Löscht alle Slots + Cursor (Admin) (Z. 480-495). |
| `RequireAdminAsync` | private | Admin-Prüfung (Z. 497-504). |
| `GroupAirings` | private static | Gruppiert sortierte Slots zu Ausstrahlungs-Ketten (Z. 510-526). |
| `ReproContext` (record) | private | Kontext der Reproduzierbarkeitsprüfung: `RuleState`, OccurrenceStarts, BoundStarts, InvaderStarts, MinStartRemain, AgeRule, Memo (Z. 531-538). |
| `ClipMediaItemIds` | private static | MediaItem-Ids aller Clip-Gruppen-Ringe einer Regel (Z. 557-564). |
| `AiringReproducible` (2 Überladungen) | private static | Prüft, ob eine Ausstrahlung unter aktueller Expansion/Ordnung identisch reproduziert würde; clip-aware Vorfilter; delegiert an Flat-/Interleaved-Tail-Checks (Z. 566-627). **Ordnungskritisch: re-simuliert `SelectAiringItems`/`SelectAiringItemsInterleaved` über die exakt gleiche Ringordnung.** |
| `FlatAiringTailReproducible` | private static | Rückwärts-Hypothesen über den Ring (k übersprungene Kopfelemente), Positionsvergleich über `expected.Positions[k] == idx` — direkt abhängig von der Ringreihenfolge (Z. 629-742). |
| `InterleavedAiringTailReproducible` | private static | Interleaved-Pendant: Strang-Kontiguität bei `PreservesOrder`-Strängen streng (`idx != (lastIndex+1) % count` → ungültig), ungeordnete Stränge mit Skip-Distanz-Check (Z. 754-948). |
| `FindItemIndex` | private static | Lokalisiert Slot-Inhalt in der Expansionsliste: zuerst `MediaItemId`, dann Content-Referenz (Z. 952-962). |
| `RewindCursor` | private static | Verteilt verdrängte Slots auf Titel-Stränge und Clip-Ringe, ruft `RewindRing` (Z. 967-982). |
| `RewindRing` | private static | Setzt `NextIndex` eines Rings auf den frühesten verdrängten, noch auflösbaren Index zurück; schreibt Cursor-Row auf Vorgänger (Z. 988-1016). |
| `ReconcileWindow` (Klasse) | private | Abgeleiteter Zustand des geladenen Slot-Fensters: `Reload`/`Rebuild`/`Validate`, `InvalidGroups`, `HasRescuableInvalid`, `FillerSlots` (Z. 1024-~1188). |
| `SchedulableItem` (record) | private | Ringelement: `ContentType`, `ContentId`, `MediaItemId`, `MovieId?`, `TVShowEpisodeId?`, `Duration`, `StrandKey`, `AgeRatingMinAge?` (Z. 1193-1201). |
| `OtherStrandKey` | private const | `"other"` — gemeinsamer Strang aller Nicht-Episoden einer Genre-Expansion (Z. 1205). |
| `StrandState` (Klasse) | private | Ein Ring je Strang: `Items`, `NextIndex`, `LastPlaced`, `PeekAt(offset)`, `Commit`, `CommitScan`, `RewindTo`, **`PreservesOrder` = `items.Count > 0 && items.All(i => i.ContentType is TVShowEpisode or PlaylistEntry)`** (Z. 1211-1248). Die `PreservesOrder`-Unterscheidung ist die vorhandene Grenze geordnet/ungeordnet. |
| `ClipGroupState` (Klasse) | private | Clip-Ring einer `ProgramClipGroup`-Zuordnung: `StrandKey = "clipgroup:{id}"`, `IsTrailer`, `SetItems` baut `StrandState` mit `NextIndexFor` (Z. 1253-1268). |
| `RuleState` (Klasse) | private | Laufzustand einer Regel: `Items`, `Strands`, `ClipGroups`, `IsInterleaved`, `ClipGrace`, Flat-Delegaten (`NextIndex`, `PeekAt`, `Commit`, `CommitScan`, `RewindTo`, `PreservesOrder`) (Z. 1270-1345). |
| `RuleState.SetItems` | public | **Ringaufbau.** Genre + Nicht-Filler: gruppiert Items nach `StrandKey` (leer → `OtherStrandKey`); bei >1 Strang wird die **Strang-Reihenfolge** per `StrandOrderKey(Rule.Id, key)` deterministisch gemischt (FNV-1a, Z. 1325-1336) — die Reihenfolge *innerhalb* eines Strangs bleibt jedoch die Expansionsreihenfolge. Sonst: ein Strang `Key == null` in Expansionsreihenfolge (Z. 1340). |
| `SelectStrandCursor` | internal static | Deterministische Cursor-Auswahl je Strang: höchstes `UpdatedAt`, Tiebreak höchste `Id` (Z. 1380-1385). |
| `StrandOrderKey` | internal static | **Vorhandenes deterministisches Mischmuster:** FNV-1a über `"{ruleId}:{strandKey}"` → `int` (Z. 1358-1374). Kommentar begründet Prozess-Invarianz vs. `string.GetHashCode`. |
| `StrandStartOffset` | private static | Rotierender Start-Strang je Tag für `EpisodeCount < Strangzahl` (Z. 1390-1393). |
| `ShiftPastInvaders` | private static | Schiebt Occurrence-Start hinter belegte Intervalle (Z. 1395-1411). |
| `SummaryCounters` (Klasse) | private | Lauf-/Diagnose-Zähler inkl. `ToSummary` (Z. 1413-1434). |
| `AccessContext` (Klasse) | private | Publikums-Freigabe-Snapshot, `Allows(sourceId, showId, collectionId)` (Z. 1439-1451). |
| `LoadAccessContextAsync` | private | Baut `AccessContext` (ContentScope `AllContent` → Everything) (Z. 1462-1515). |
| `LoadAgeRuleAsync` | private | Lädt Jugendschutz-Regelung (Z. 1522-1525). |
| `NextIndexFor` | private static | **Cursor-Ankerung:** `items.FindIndex(ContentType+ContentId)`; `pos < 0` oder kein Cursor → Index **0** = Ringanfang (Z. 1529-1536). Zentrale Bias-Stelle: ohne Cursor-Anker startet jeder Lauf am sortierten Ringanfang. |
| `BuildOccurrences` | private static | Occurrence-Starts aus Recurrence/Feiertagskalender (Z. 1538-1570). |
| `MaterializeOccurrence` | private | Wählt Items (`SelectItems`), Kollisionscheck, schreibt Slots, `CommitTaken` (Z. 1572-1629). |
| `SelectionResult`/`StrandPick`/`FlatSelection`/`InterleavedSelection` (records) | private | Ergebnis-Träger der Auswahl (Z. 1644-1662). |
| `SelectItems` | private | Dispatch flat vs. interleaved (Z. 1664-1681). |
| `CommitTaken` | private static | Commit nach Kollisionscheck: `CommitScan` je Strang (Z. 1685-1699). |
| `SelectAiringItemsInterleaved` | private static | Round-robin über Stränge ab `StrandStartOffset`; ungeordnete Stränge überspringen Fenster-blockierte Köpfe (`scanned[s]++`), geordnete Stränge vertagen sich (Z. 1709-1799). |
| `SelectAiringItems` | private static | Flache Ring-Entnahme ab `headIndex`: sequenziell, Fenster-blockierte Positionen werden bei ungeordneten Ringen übersprungen (Ringposition verbraucht), bei `preservesOrder` abgebrochen (Z. 1809-1866). |
| `BoundStarts`/`BoundaryAfter` | private static | Fenstergrenzen je Occurrence (Z. 1871-1891). |
| `FillGaps` | private | Lückenfüllung über Filler-Regeln: zieht **`PeekAt(0)`** (Ringkopf) des jeweiligen Fillers bzw. `PeekAt(offset)` beim Trailer-Lead-Match; `Commit(take)` (Z. 1893-2005). |
| `GapSeconds` | private static | Restlücken-Sekunden (Z. 2007-2021). |
| `PersistCursors` | private | Schreibt je Strang mit `LastPlaced` eine Cursor-Row (`StrandKey`, `LastContentType`, `LastContentId`) (Z. 2025-2057). |
| `ExpandMaterialAsync` (2 Überladungen) | private | Dispatch auf die Einzel-Expansionen je `MaterialType` (Z. 2061-2088). |
| `MediaItemRef` (record) | private | Repräsentativer Media-Item-Link inkl. Probe-Status (Z. 2093). |
| `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` | private | Repräsentativer Link je Inhalt: `OrderBy(Duration is null).ThenBy(MediaItemId)` (Z. 2095-2119). |
| `CountNoDuration` | private | Skip-Zähler (Z. 2124-2139). |
| `TryMovieItem` | private | Film → `SchedulableItem` (`ContentType Movie`, `ContentId = movie.Id`) (Z. 2141-2156). |
| `ExpandShowAsync` | private | Episoden `OrderBy(TVShowSeason.Id).ThenBy(Number)` — **geordnet** (Sendedreihenfolge), `ContentType TVShowEpisode` (Z. 2158-2184). |
| `ToItem` | private | Episode → `SchedulableItem` (`ContentType TVShowEpisode`) (Z. 2189-2204). |
| `ExpandMovieAsync` | private | Einzelfilm (Z. 2206-2218). |
| `ExpandMovieCollectionAsync` | private | **Ungeordneter Ring, sortiert:** `OrderBy(ReleaseDate ?? PremieredAt).ThenBy(Id)`, `ContentType Movie` → `PreservesOrder == false` (Z. 2220-2240). |
| `ExpandGenreAsync` | private | Filme `OrderBy(ReleaseDate ?? PremieredAt).ThenBy(Name).ThenBy(Id)` → `StrandKey = "other"` (**ungeordneter Ring**); Episoden `OrderBy(TVShow.Name).ThenBy(TVShowId).ThenBy(Season.Id).ThenBy(Number)` → `StrandKey = "show:{id}"` (**geordnet**, `ContentType TVShowEpisode`) (Z. 2251-2352). |
| `ExpandTrailerStatesAsync` | private | Nachträgliche Expansion der Trailer-Material-Regeln, ruft `state.SetItems` (Z. 2356-2373). |
| `ExpandTrailersAsync` | private | Trailer-Links gruppiert je `MediaItemId`, sortiert nach nächster Ausstrahlung des Films (`airingsByMovie`) — fachliche Vorlauf-Ordnung, `ContentType Trailer` → `PreservesOrder == false` (Z. 2379-2422). |
| `FindLeadMatchOffset` (2 Überladungen) | private static | Erster Ring-Offset, dessen Film im Vorlauf-Fenster ausgestrahlt wird (Z. 2426-2440). |
| `TrailerMediaItemIdsAsync` | private | Trailer-Item-Ids (Z. 2442-2447). |
| `ExpandMediaSourceClipsAsync` | private | Unklassifizierte Video-Items einer Quelle → `FilterClipCandidatesAsync` (Z. 2454-2470). |
| `LoadMovieAiringsAsync`/`BuildMovieAiringsFromSlotsAsync`/`BuildMovieAiringsMap` | private | Film-Ausstrahlungs-Map (Z. 2472-2508). |
| `ExpandPlaylistAsync` | private | `SortMode.Manual` → `OrderBy(SortOrder).ThenBy(AddedAt)`; sonst `OrderBy(ReleaseDate).ThenBy(AddedAt).ThenBy(Id)` — **beide Varianten ergeben `ContentType PlaylistEntry` → `PreservesOrder == true`** (Z. 2510-2584). |
| `ClipFillContext` (record) | private | Kontext der Clip-Pässe inkl. `RoundRobin`-Zähler je Regel (Z. 2593-2610). |
| `ClipCandidate` (record) | private | Clip-Kandidaten-Zeile (Z. 2615-2617). |
| `BuildClipGroupStates` | private | Baut `ClipGroupState`s je Zuordnung, sortiert nach `ProgramClipGroupId` (Round-robin-Reihenfolge); Trailer-Gruppe expandiert nur mit `AiringsByMovie` (Z. 2626-2648). |
| `ExpandClipGroupStatesAsync` | private | Nachträgliche Expansion der Trailer-Clip-Gruppe (Z. 2653-2664). |
| `ExpandClipGroupItemsAsync` | private | Mitglieder einer Nutzer-Clip-Gruppe → `FilterClipCandidatesAsync` (Z. 2670-2686). |
| `FilterClipCandidatesAsync` | private | **Ungeordneter Ring, sortiert:** `candidates.OrderBy(i => i.MediaItemId)`; Filter Video-Endung, nicht klassifiziert, Dauer, Freigabe → `ContentType MediaItem` → `PreservesOrder == false` (Z. 2691-2730). |
| `NextClipItem` | private | Zieht Ringkopf (`PeekAt(0)`) der nächsten Clip-Gruppe round-robin bzw. `PeekAt(offset)` bei Trailer-Lead-Match; `Commit(take)` (Z. 2737-2775). |
| `InsertClipFill` | private | Verteilt Clips in die Segmente der SlotDuration-Ausstrahlungen (Z. 2787-2811). |
| `TailLimit`/`ChainLimit`/`PeriodStart` | private static | Geometrie-Grenzen der Clip-Füllung (Z. 2815-2864). |
| `FillAiringSegments` | private | Re-layout einer Ausstrahlung mit Clip-Segmenten; `NextClipItem`-Zug (Z. 2876-2965). |

Abonnierte Events: keine.
Publizierte Events: keine (Signale laufen über `ProgramScheduleWorker`-Sleep/Signal, nicht über Events des Service).

## `ProgramScheduleWorker`

Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs`

- `BackgroundService`; ruft im konfigurierten Intervall `ReconcileAsync` gefolgt von
  `EnsureCoverageAsync` auf (Z. 71-72). Bei deaktiviertem Feature-Flag pausiert der Plan.

## Angrenzend (nur referenziert, nicht geändert-relevant)

- `GenreCanonicalResolver.ResolveCanonicalNames` (`VideoWebPlayer/Services/GenreCanonicalResolver.cs`) —
  kanonische Genre-Namen der Genre-Expansion.
- `ProgramScheduleAgeWindow.IsAllowedAt` / `CountBlockedAt`
  (`VideoWebPlayer/Services/ProgramScheduleAgeWindow.cs`) — Jugendschutz-Fenster-Check,
  steuert die Skip-Semantik ungeordneter Ringe.
- `MediaHierarchyRegistry` — liefert in `ExpandPlaylistAsync` die Releasedaten je Medientyp.
- `ProgramSettingsService` — Horizont, Bundesland, ContentScope, Jugendschutz-Einstellungen.
- `IProgramRuleService`/`ProgramRuleService` — `GetOrCreateChannelAsync`.
