# Bestandsaufnahme: Logik

## `ProgramScheduleService`

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (2692 Zeilen)

Erzeugt und pflegt den rollierenden, lückenlosen Sendeplan. Konstruktor-Abhängigkeiten:
`ApplicationDbContext`, `IProgramRuleService`, `ProgramSettingsService`,
`ILogger<ProgramScheduleService>`, optional `IOptions<MediaProbeOptions>`,
`IOptions<ProgramScheduleOptions>` (`EffectiveTrailerLead`, Default 7 Tage).

### Öffentliche Methoden (Auswahl)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` | public | Füllt den Plan bis zum Horizont; ruft nach Materialisierung `InsertClipFill` (Z. 135) und danach `FillGaps` |
| `ReconcileAsync` | public | Bewertet den Plan neu; verdrängt ungültige Slots (`Status = Displaced`), materialisiert neu, ruft `InsertClipFill` (Z. 365) auf |
| `GetScheduleAsync` | public | Flache Slot-Liste (`Scheduled` + `Displaced`) ab `fromUtc`, nach `Start`, `Take(maxCount)` — einzige Datenquelle des Admin-Sendeplans |
| `DeleteDisplacedSlotsAsync`, `ResetScheduleAsync`, `GetRuleDiagnosesAsync` | public | Admin-Hauskeeping bzw. Diagnose |

### Clip-Füll-Machinerie (intern)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InsertClipFill` (Z. 2514) | private | Post-Pass nach der Materialisierung: gruppiert neue Slots zu Airings (`GroupAirings` — gleiche `ProgramRuleId` + lückenlose Kette) und ruft je Airing `FillAiringSegments`; überspringt Airings ohne Regel/Clip-Gruppen/`SlotDuration` |
| `FillAiringSegments` (Z. 2603) | private | Verteilt die Restzeit bis Periodenende (`periodStart + rule.SlotDuration`) gleichmäßig auf n+1 Segmente (vor/zwischen/nach den Titeln); zieht Clips per `NextClipItem` Round-Robin über die Gruppenringe; **erzeugt `ProgramSlot` mit `RuleType = rule.RuleType`, `ProgramRuleId = rule.Id`, `MovieId`/`TVShowEpisodeId` aus dem Clip-Item (Trailer-Clips tragen `MovieId`), `Status` implizit `Scheduled`**; letzter Clip darf über Periodenende hinausragen |
| `NextClipItem` (Z. 2464) | private | Sequenzieller Round-Robin-Zug über `ClipGroupState.Ring`s; Trailer-Gruppe nur bei Lead-Match (`FindLeadMatchOffset`) |
| `BuildClipGroupStates` (Z. 2353) | private | Baut `ClipGroupState`s je `ProgramRuleClipGroup` (Sortierung `ProgramClipGroupId`); Trailer-Gruppe expandiert erst nach Materialisierung |
| `ExpandClipGroupItemsAsync` (Z. 2397) | private | Gruppenmitglieder → `ClipCandidate`s → `FilterClipCandidatesAsync` |
| `FilterClipCandidatesAsync` (Z. 2418) | private | Clip-Definition: nur **uneingeordnete Videodateien** (kein Movie-/Episoden-Link) mit probierter Dauer und Voll-Publikums-Freigabe; Sortierung `MediaItemId` |
| `ClipMediaItemIds` (Z. 523) | private static | **Die einzige heutige Clip-Klassifikation:** `HashSet<long>` aller `MediaItemId`s der Clip-Ringe einer Regel — „Clip zuerst": Slot ist Füll-Clip, wenn `clipIds.Contains(slot.MediaItemId)`, sonst Titel |
| `AiringReproducible` (Z. 532) | private static | Reconcile-Prüfung: clip-bewusster Vorfilter (Clip-Slots nur über `clipIds`-Mitgliedschaft + lückenlose Kette, Titel auf clip-freie Zeitachse projiziert) |
| `RewindCursor` (Z. 839) | private static | Trennt verdrängte Slots per `clipIds` in Titel-/Clip-Slots und spult Titel-Stränge bzw. Clip-Ringe (`clipgroup:{id}`-StrandKey) getrennt zurück |
| `GroupAirings` (Z. 477) | private static | Gruppiert Slots zu Airings: gleiche `ProgramRuleId` **und** `slot.Start == prev.End` — nahezu identisch mit der geforderten Block-Erkennung, aber privat und regelbasiert |

### Interne Typen

- `record ClipFillContext` (Z. 2321): Laufkontext (Access, AiringsByMovie, CoverageStart,
  BoundStarts, HorizonEnd, Summary, OccurrenceStarts) + mutables `RoundRobin`-Dictionary.
- `record ClipCandidate` (Z. 2342), `class ClipGroupState` (Z. 1101, `StrandKey =
  $"clipgroup:{Group.Id}"`, `IsTrailer` über `TrailerGroupId`), `class RuleState` (Z. 1118,
  `ClipGroups`, `ClipGrace` = `PlaceholderGrace` nur bei Gruppen + `SlotDuration`).
- `class ReconcileWindow` (Z. 896): geladenes Slot-Fenster des Reconcile-Laufs.
- `record SchedulableItem`, `class StrandState`: Items/Ringe der Material- und Clip-Auswahl.

**Clip-Erkennung für die Anzeige (Ist-Zustand):** Es gibt **kein persistiertes
Clip-Kennzeichen**. Die Identifikation existiert nur zur Plan-/Reconcile-Laufzeit über die
gerade expandierten Gruppenringe (`clipIds`). Für die Anzeige stehen nur Indizien:
`ProgramRuleId` der erzeugenden Regel, `RuleType != Filler`, weiche Inhaltsreferenzen
(`MovieId`/`TVShowEpisodeId` meist `null`, bei Trailer `MovieId` gesetzt) — und die
DB-Abfragbarkeit über `ProgramClipGroupItems` ⨝ `ProgramRuleClipGroups` (abhängig vom
aktuellen, veränderlichen Gruppenbestand — Variante B der Anforderung).

## `ProgramGuideService`

Datei: `VideoWebPlayer/Services/ProgramGuideService.cs` (203 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetNowAsync` | public | `Scheduled`-Slot, der `now` umfasst (+ nächster Slot ab `current.End` bzw. `now`); `ToEntry` mit `PositionSeconds`/`RemainingSeconds` — **wird auch von `LiveStreamCoordinator` (Z. 99) genutzt** |
| `GetUpcomingAsync` | public | Alle `Scheduled`-Slots mit `End > now && Start < now + preview`, je Slot ein `DtoProgramGuideEntry`; `preview` ≤ 12 h (`MaxPreviewHours`) |
| `LoadDetailsAsync` | private | Batch-Auflösung Movies/Episoden/Bare-Item-Namen für beliebige Slot-Mengen (Bare-Item: `MediaItems.Name` — so heißen Clips in Auskunft/Live-TV) |
| `ToEntry` | private static | `IsFiller = RuleType == Filler`, Titel-Fallbacks (`UnknownTitle = "Unbekannt"`, Bare-Item → `MediaItem.Name`) |
| `GetChannelIdAsync` | private | Erster `TVChannel` — reiner Lesedienst, legt den Sender nicht an |

## `LiveStreamCoordinator` (Randbedingung)

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` — ruft
`IProgramGuideService.GetNowAsync(utcNow, …)` (Z. 99), um den On-Air-Slot für die
Segment-Auslieferung zu bestimmen. Eine Gruppierung darf diesen Pfad **nicht** verändern
(Explizite Randbedingung der Anforderung).

## `ProgramGuideController`

Datei: `VideoWebPlayer/Controllers/ProgramGuideController.cs` — `api/program-guide`,
`BearerTokenCheck`, Feature-Gate `GetTvSenderEnabledAsync` (404) +
`IsTvSenderAccessGrantedAsync` (403). Endpunkte `GET now` und `GET upcoming?hours=`
(1–12 h, sonst 400) → `IProgramGuideService`. Fehler-Mapping über `ExecuteAsync` nach
`PlaylistsController`-Muster.

## `TVSenderAdmin` (Code-Behind in `TVSenderAdmin.razor` `@code`)

| Methode/Feld | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync` | protected override | Admin-/Feature-Gate; lädt Kanal, Einstellungen, `ReloadRulesAsync`, `ReloadScheduleAsync`, `ReloadClipGroupsAsync`, Sondenstand |
| `SelectTab(string)` | private | Setzt `activeTab` (kein Neu-Laden — alle Panes bleiben im DOM) |
| `ReloadScheduleAsync` (Z. 1369) | private | `schedule = GetScheduleAsync(channel.Id, UtcNow-2h, 200)`; lädt `slotRuleNames` (`ProgramRules`-Namen) und baut je Slot ein `ScheduleCardContent` (Movie-/Episoden-Lookup per EF + Bare-`MediaItem`-Namen-Fallback — selbe Logik wie `ProgramGuideService.LoadDetailsAsync`, aber direkt per EF, da auch `Displaced` gezeigt wird) |
| `GenerateNowAsync`, `CleanupDisplacedAsync`, `ResetScheduleAsync` | private | Stoßen Worker/Deletes an, danach `ScheduleSignal.NotifyScheduleChanged()` + `ReloadScheduleAsync` |
| `SlotCard(ProgramSlot)` | private | `slotCards`-Lookup, Fallback `"(Inhalt nicht gefunden)"` |
| `IsRunning(ProgramSlot)` | private static | `Scheduled && Start <= now < End` — LIVE-Badge/Fortschritt |
| `ProgressPercent(ProgramSlot)` | private static | `(now - Start)/(End - Start)` geklemmt 0–100 |
| `RuleName(ProgramSlot)` | private | Regelname aus `slotRuleNames`, Fallback `"(Regel gelöscht)"` |
| `PosterUrl(ScheduleCardContent)` | private | `MediaPictureUrlResolver.Resolve` |
| `record ScheduleCardContent` (Z. 1441) | private sealed | `Title, ShowName, Subtitle, Description, GenreName, PosterPictureId` — das heutige Karten-View-Model je `slot.Id` |
| `record MovieCardData`, `record EpisodeCardData` | private sealed | Lookup-Projektionen |

Bestätigungsdialog-State: `confirmCleanup`, `confirmReset`, `confirmProbeReset`,
`deleteCandidate`, `confirmFfmpegDownload` — jeweils `admin-dialog-overlay` (Markup
Z. 843–923).

## `LiveTv` (Code-Behind in `LiveTv.razor` `@code`)

| Methode/Feld | Kurzbeschreibung |
|---------|------------------|
| `OnInitializedAsync` | Auth-, Feature- und Access-Gates; `RefreshGuideAsync(isInitial: true)` |
| `RefreshGuideAsync` | `RequestProgramGuideNowAsync` + `RequestProgramGuideUpcomingAsync(PreviewHours = 6)` getrennt mit stillem Retry |
| `PollLoopAsync` | `PeriodicTimer` 15 s → Refresh |
| `upcoming: DtoProgramGuideEntry[]` | Liste hinter `#live-tv-upcoming` — **ein `li.program-card` je Slot** (`data-slot-id`) |
| `CardTitle`/`CardSub`/`Desc`/`PosterUrl`/`FormatTime`/`ProgressPercent` | Anzeige-Hilfen (Serien-Titel als Hauptzeile, `ProgramGuideEntryText.NoDescriptionText`-Fallback) |

## `ProgramScheduleWorker`, `ProgramScheduleSignal`

`VideoWebPlayer/Services/ProgramScheduleWorker.cs` — Hintergrundlauf (Intervall + Signal),
ruft `EnsureCoverageAsync`/`ReconcileAsync`. `ProgramScheduleSignal` s. interfaces.md. Für
die Anzeige-Gruppierung nicht betroffen.

## `VideoWebPlayerClient` (Program-Guide-Teil)

`VideoWebPlayer.Client/VideoWebPlayerClient.ProgramGuide.cs` — implementiert
`IProgramGuideApiClient` über `GET /api/program-guide/now|upcoming`.
