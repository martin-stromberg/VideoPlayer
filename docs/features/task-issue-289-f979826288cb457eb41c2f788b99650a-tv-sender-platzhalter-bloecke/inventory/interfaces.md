# Bestandsaufnahme: Interfaces und DTOs

## `IProgramScheduleService`

Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs` —
Implementierung `ProgramScheduleService` (`VideoWebPlayer/Services/ProgramScheduleService.cs`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `utcNow`, `cancellationToken` | `Task<ProgramScheduleRunSummary>` | Sendeplan bis zum Horizont auffüllen (nur anhängend; ruft intern `InsertClipFill`) |
| `ReconcileAsync` | `utcNow`, `cancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Plan gegen aktuelle Regeln/Medien neu bewerten; ungültige Slots → `Displaced` (nie gelöscht), Neu-Materialisierung inkl. `InsertClipFill` |
| `GetScheduleAsync` | `channelId`, `fromUtc`, `maxCount`, `cancellationToken` | `Task<List<ProgramSlot>>` | Slots (Scheduled **und** Displaced) ab `fromUtc` nach `Start` sortiert — Datenquelle des Admin-Tabs „Sendeplan" |
| `DeleteDisplacedSlotsAsync` | `userId`, `channelId`, `cancellationToken` | `Task<int>` | Alle `Displaced`-Slots löschen (Admin) |
| `ResetScheduleAsync` | `userId`, `channelId`, `cancellationToken` | `Task<ProgramScheduleResetResult>` | Gesamten Plan + Cursors löschen (Admin) |
| `GetRuleDiagnosesAsync` | `userId`, `channelId`, `cancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Planungsdiagnose je Regel (Admin) |

Zugehörige Records in derselben Datei: `ProgramScheduleRunSummary`,
`ProgramScheduleReconcileSummary`, `ProgramRuleDiagnosis`, `ProgramScheduleResetResult`.

**Bedeutung:** `GetScheduleAsync` liefert flache `ProgramSlot`-Listen ohne Inhaltstitel oder
Clip-Markierung — die Gruppierung muss entweder im Aufrufer (`TVSenderAdmin`) oder in einer
Erweiterung der Abfrage entstehen. `maxCount` wird im Admin-Tab mit 200 aufgerufen.

## `IProgramGuideService`

Datei: `VideoWebPlayer/Services/IProgramGuideService.cs` —
Implementierung `ProgramGuideService` (`VideoWebPlayer/Services/ProgramGuideService.cs`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetNowAsync` | `utcNow`, `cancellationToken` | `Task<DtoProgramGuideNow>` | Laufender Slot (+ Position/Rest) und nächster Slot — nur `Scheduled`. **Achtung:** wird auch von `LiveStreamCoordinator` (Z. 99) für die Auslieferung genutzt — dort darf nicht gruppiert werden |
| `GetUpcomingAsync` | `utcNow`, `preview`, `cancellationToken` | `Task<DtoProgramGuideEntry[]>` | Begrenzte Programmvorschau (max. `MaxPreviewHours` = 12 h), nur `Scheduled`, liefert einen Eintrag **je Slot** — keine Gruppierung |

## `IProgramGuideApiClient`

Datei: `VideoWebPlayer.Client/IProgramGuideApiClient.cs` — implementiert von
`VideoWebPlayerClient` (`VideoWebPlayerClient.ProgramGuide.cs`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RequestProgramGuideNowAsync` | `cancellationToken` | `Task<DtoProgramGuideNow>` | `GET /api/program-guide/now` |
| `RequestProgramGuideUpcomingAsync` | `hours`, `cancellationToken` | `Task<DtoProgramGuideEntry[]>` | `GET /api/program-guide/upcoming?hours=` |

## `DtoProgramGuideEntry`

Datei: `VideoWebPlayer.Client/Models/DtoProgramGuideEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SlotId` | `long` | `ProgramSlot.Id` |
| `Title` | `string` | Anzeigetitel (Filmname bzw. „Show — Staffel, E{n}: Titel"), „Unbekannt"-Fallback |
| `MediaType` | `string?` | `MediaTypeValues.Movie`/`TVShowEpisode`, `null` bei Bare-Item-Slots |
| `MediaId` | `long?` | Film-/Episoden-Id, `null` bei Bare-Item-Slots |
| `MediaItemId` | `long` | Medien-Item (für Live-Stream) |
| `IsFiller` | `bool` | `slot.RuleType == ProgramRuleType.Filler` — Clip-Füll-Slots sind darüber **nicht** markiert |
| `StartUtc`/`EndUtc` | `DateTimeOffset` | Sendefenster |
| `DurationSeconds` | `double` | `End - Start` |
| `PositionSeconds`/`RemainingSeconds` | `double?` | Fortschritt nur bei laufender Sendung |
| `PosterPictureId` | `long?` | Poster (Film; Episode: Episode→Staffel→Serie-Fallback) |
| `Description` | `string?` | Plot (Episode: Show-Plot-Fallback) |
| `GenreName` | `string?` | Erster Genre-Name |
| `ShowName`/`SeasonName`/`SeasonNumber`/`EpisodeNumber`/`EpisodeName` | div. nullable | Serien-Metadaten bei Episoden-Slots |

Kein Clip-/Block-Marker vorhanden; `DtoProgramGuideNow` (gleiches Modell-Paket) trägt
`ServerTimeUtc`, `Now`, `Next`.

## `IProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | – | `void` | Weckt `ProgramScheduleWorker` (außer während `BeginScan`-Scope) |
| `BeginScan` | – | `IDisposable` | Scan-Scope; dessen Ende weckt einmal |
| `WaitAsync` | `cancellationToken` | `ValueTask` | Warten auf Signal |

Wird in `TVSenderAdmin.razor` injiziert (`ScheduleSignal`) und nach „Jetzt generieren" /
„Plan zurücksetzen" ausgelöst — für die rein darstellerische Gruppierung nicht betroffen.

## `IProgramClipGroupService`, `IProgramRuleService`

- `VideoWebPlayer/Services/IProgramClipGroupService.cs` — Gruppen-CRUD, Mitgliedschaft,
  Kandidatensuche (für den Tab „Clip-Gruppen"; nicht direkt betroffen).
- `VideoWebPlayer/Services/IProgramRuleService.cs` — Regel-CRUD, `GetOrCreateChannelAsync`
  (liefert den einen Sender für `GetScheduleAsync`).
