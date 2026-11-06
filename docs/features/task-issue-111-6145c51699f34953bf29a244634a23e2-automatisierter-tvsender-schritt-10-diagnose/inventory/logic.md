# Logik — Bestandsaufnahme

## `ProgramScheduleService`
Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (registriert scoped, `ServiceCollectionExtensions.cs:266`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` | public | Extend-only-Lauf; expandiert alle aktiven Regeln, materialisiert Vorkommen, füllt Lücken mit Füllern. |
| `ReconcileAsync` | public | Bewertet geplante Slots neu; verdrängt Ungültige, re-materialisiert. |
| `GetScheduleAsync` | public | Kommende Slots ab `fromUtc` für die Admin-Ansicht. |
| `ExpandMaterialAsync` | private | Dispatch auf MaterialType; loggt „liefert kein planbares Material (verwaist, ohne Laufzeit oder nicht freigegeben)" bei leerem Ergebnis, zählt `OrphanedRules++`. |
| `ExpandShowAsync` / `ExpandMovieAsync` / `ExpandMovieCollectionAsync` / `ExpandGenreAsync` / `ExpandPlaylistAsync` | private | Material-Expansion je Typ; leere Liste sowohl bei verwaister Referenz als auch bei „alles übersprungen". |
| `MovieMediaItemsAsync` / `EpisodeMediaItemsAsync` | private | Pro Inhalt genau ein MediaItem (nur `Duration != null`), geordnet nach `MediaItemId`. |
| `TryMovieItem` / `ToItem` / `TryEpisodeItem` | private | Kandidat → `SchedulableItem?`; Zählreihenfolge: erst Laufzeit, dann `access.Allows`. |
| `LoadAccessContextAsync` | private | Freigabe-Schnappschuss: alles freigegeben bei 0 Nicht-Admin-Usern, sonst vollständig geteilte Quellen + für alle freigeschaltete Serien/Sammlungen. |
| `SummaryCounters` | private class | `SlotsCreated`, `FillerSlotsCreated`, `SkippedNoDuration`, `SkippedNoAccess`, `OrphanedRules`, `DroppedConflicts`, `RemainingGapSeconds` → `ProgramScheduleRunSummary`/`ProgramScheduleReconcileSummary`. |

Genre-Pfad (`ExpandGenreAsync`): `GenreCanonicalResolver.ResolveCanonicalNames` löst versteckte/
saisonale Genres und Alternativnamen auf; drei Early-Returns ohne Inhalt (keine gespeicherten
Genres gefunden, keine kanonischen Namen, keine wirksamen Genre-Ids im Quell-Filter).

Konstruktor: `(ApplicationDbContext, IProgramRuleService, ProgramSettingsService, ILogger)`.

## `ProgramScheduleWorker`
Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs` (HostedService)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` | protected | Loop: `ReconcileAsync` + `EnsureCoverageAsync`, dann `Task.WhenAny(intervalDelay, signal.WaitAsync)` — ein Signal weckt sofort. |

## `ProgramRuleService`
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RequireAdminAsync` | private | `UnauthorizedAccessException`/`ForbiddenAccessException` — Muster für serverseitige Absicherung neuer Admin-Methoden. |
| `GetRulesAsync` | public | Regeln des Senders inkl. `Genres`+`Genre`, `MediaSourceFilters`+`MediaSource` (AsNoTracking). |
| CRUD-Methoden | public | feuern jeweils `_scheduleSignal?.NotifyScheduleChanged()`. |

## `MediaDurationProbeService`
Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProbePendingAsync` | public | Batch-Probe; Retry-Filter `DurationProbedAt == null || (Attempts < max && DurationProbedAt <= retryThreshold)`; `PermanentFailure` setzt `Attempts = max`. |

## `ProgramScheduleSignal` / `IProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` (Singleton)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `NotifyScheduleChanged` | public | Koaleszierender Weckruf; während `BeginScan`-Scopes unterdrückt (Scan-Ende weckt). |

## `TVSenderAdmin.razor`
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`

| Element | Kurzbeschreibung |
|---------|------------------|
| `OnInitializedAsync` | Admin-Gate (`IsAdmin`-Claim), lädt Einstellungen, `mediaSources`, `genreOptions`, `playlistOptions`, `ReloadRulesAsync`, `ReloadScheduleAsync`. |
| `ReloadRulesAsync` | `ProgramRuleService.GetRulesAsync(userId, channel.Id)` → `rules`. |
| `ReloadScheduleAsync` | `ProgramScheduleService.GetScheduleAsync` + Titel-/Namensauflösung. |
| Regel-Tabelle | Spalten Name/Regelart/Wiederholung/Sendezeit/Umfang/Material/Priorität/Status/Aktionen; `.tvsender-rule-row` mit `data-rule-id`. |
| Sendeplan-Karte | `.tvsender-slot-row`, Status-Chips „Geplant"/„Verdrängt"; „keine Einträge"-Leerstand ohne Begründung (genau die N5-Beobachtung). |
