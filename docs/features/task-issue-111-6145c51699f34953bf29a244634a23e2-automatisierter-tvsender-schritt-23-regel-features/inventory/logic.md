# Logik

## `ProgramScheduleService`
Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (1645 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` | public | Extend-only-Lauf: `coverageStart = max(now, lastEnd)`, füllt bis `now+horizon`. Baut `RuleState` je Regel, `BuildOccurrences`, materialisiert nach Priorität, `FillGaps`, `PersistCursors`. |
| `ReconcileAsync` | public | Bewertet geplante Slots neu; verdrängt invalid gewordene Airings (`AiringReproducible`), `RewindCursor` je Regel, `MaterializeOccurrenceEvicting`, `FillGaps`, `PersistCursors`. |
| `GetScheduleAsync`/`GetRuleDiagnosesAsync`/`DeleteDisplacedSlotsAsync`/`ResetScheduleAsync` | public | Abfrage/Diagnose/Bereinigung; Admin-Zwang serverseitig. `ResetScheduleAsync` löscht Slots UND alle Cursor der Regeln. |
| `GroupAirings` | private static | Gruppiert Slots zu Airings (gleiche Regel, lückenlos). |
| `AiringReproducible` | private static | Prüft, ob ein Airing exakt so von der Regel reproduziert würde; Ring-Rückwärtslauf vom Kopf-Item über `SelectAiringItems`, Occurrence-Start-Match, `writtenOffset` für bereits vergangene Kopf-Items. |
| `FindItemIndex` | private static | Slot → Index in `state.Items` (per `MediaItemId`, sonst `MovieId`/`TVShowEpisodeId`). |
| `RewindCursor` | private static | Spult den Regel-Cursor auf den Vorgänger des frühesten verdrängten Inhalts zurück. |
| `MaterializeOccurrence` / `MaterializeOccurrenceEvicting` | private | Wählt Items ab `state.NextIndex` via `SelectAiringItems`, committed `consumed` (überlange Kopf-Items) bzw. `taken`, schreibt Slots (atomar; Konflikt → `DroppedConflicts++`). |
| `SelectAiringItems` | private static | Zieht `wanted` (EpisodeCount; sonst SlotDuration; sonst 1) Items ab `headIndex` aus EINEM Ring; Items, die den `softEndUtc`-Bound überschreiten, beenden die Auswahl (Tail) bzw. werden als überlanger Kopf konsumiert (`consumed`, `consumed <= count`-Begrenzung). |
| `BoundStarts` / `BoundaryAfter` | private static | Sortierte Occurrence-Starts aller item-tragenden Regeln; Fenster-`softEnd` = nächster Occurrence-Start einer gleich-/niedriger-prioren Regel. |
| `FillGaps` | private | Füllt Lücken mit Füller-Regeln (`filler.PeekAt`/`Commit`, Trailer-Vorlauf-Logik). Nutzt `RuleState.Items`/`NextIndex` direkt — nur Flach-Ring-Semantik. |
| `PersistCursors` | private | Schreibt `LastPlaced` je Regel in `ProgramRuleCursors` (eine Zeile je Regel). |
| `NextIndexFor` | private static | Index hinter dem Cursor-Element; 0 ohne Cursor. |
| `BuildOccurrences` | private | `switch` über `rule.Recurrence` (Daily/Weekly/Once/Holiday); `day`-Schleife über das Fenster + 24 h `OccurrenceLookback`. **Hier fehlt `Weekday`/`Weekend`.** |
| `ExpandMaterialAsync` | private | Dispatcher je `MaterialType`. |
| `ExpandGenreAsync` | private | Genre-Union: Filme (Release, Name, Id) ++ Episoden (Serienname, ShowId, Staffel, Nummer). Ergebnis = eine Liste — Episoden EINER Serie stehen direkt hintereinander ⇒ Stripping. |
| `ExpandShowAsync`/`ExpandMovieAsync`/`ExpandMovieCollectionAsync`/`ExpandPlaylistAsync`/`ExpandTrailersAsync`/`ExpandMediaSourceClipsAsync` | private | Material-Expansion je Typ. |

Interne Typen:
- `SchedulableItem` (record): `ContentType`, `ContentId`, `MediaItemId`, `MovieId?`,
  `TVShowEpisodeId?`, `Duration` — **kein Serien-/Strang-Bezug**.
- `RuleState` (class): `Rule`, `Items` (flacher Ring), `NextIndex`, `LastPlaced`;
  `PeekAt(offset)`, `Commit(count)`, `RewindTo(index)`.
- `Occurrence` (record): `Rule`, `StartUtc`.
- `AccessContext`, `SummaryCounters`, `MediaItemRef`, `ProxyEpisode`, `Interval`.

## `ProgramRuleService`
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs` (317 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllowedMaterialTypes` | public | Regelart → erlaubte Materialarten (GenreRotation → nur Genre; Repeat/HolidaySpecial/Filler → alle außer Trailer/Clips). |
| `GetOrCreateChannelAsync`/`RenameChannelAsync`/`GetRulesAsync`/`GetGenreMaterialOptionsAsync` | public | Sender/Regel-Lesen, Admin-Zwang. |
| `CreateRuleAsync`/`UpdateRuleAsync` | public | `ValidateRuleInputAsync` + Persistenz; `DayOfWeek` nur bei `Weekly`, `SingleDate` nur bei `Once` gesetzt (sonst `null`). |
| `SetRuleActiveAsync`/`DeleteRuleAsync` | public | Admin-Zwang + `IProgramScheduleSignal.NotifyScheduleChanged()`. |
| `ValidateRuleInputAsync` | private | `Enum.IsDefined(Recurrence)` (neue Werte automatisch zulässig), Pflichtfelder `DayOfWeek`/`SingleDate` nur bei Weekly/Once, `EpisodeCount`/`SlotDuration`-Mindestregeln, Materialart-Whitelist, Genre-Validierung. |

## `ProgramRuleInput`
Datei: `VideoWebPlayer/Services/ProgramRuleInput.cs` — Record mit allen Editor-Feldern;
keine Änderung für N20 nötig (Recurrence-Wert reicht).

## `LiveStreamCoordinator`
Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` — konsumiert den fertigen
Sendeplan (Slots), nicht die Regel-Logik; von N19–N21 nicht direkt betroffen.

## `ProgramScheduleWorker`/`IProgramScheduleSignal`
Wird bei Regeländerungen geweckt; kein Eingriff nötig.
