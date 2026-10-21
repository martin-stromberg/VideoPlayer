# Logik / Services — Bestandsaufnahme

## `ProgramScheduleService` (`IProgramScheduleService`)
Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (708 Zeilen) · scoped: `AddScoped<IProgramScheduleService, ProgramScheduleService>()`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync(utcNow, ct)` | public (Interface) | Einziger öffentlicher Einstieg. Extend-only: `coverageStart = max(now, max(ProgramSlots.End))`; belegt `[coverageStart, now + Horizon]`; bestehende Slots werden nie geändert |
| `LoadAccessContextAsync` | private | `AccessContext`-Snapshot: planbar = für **jeden** Nicht-Admin sichtbar (`MediaSourceUsers` vollständig ODER `UnlockedMediaEntries` für Show/Collection vollständig); keine Nicht-Admins → alles freigegeben |
| `NextIndexFor(items, cursor)` | private static | Cursor → nächster Index (`pos+1`; nicht gefunden → 0) |
| `BuildOccurrences` | private | Ausstrahlungstermine je Regel im Fenster (Daily/Weekly/Once/Holiday via `GermanHolidayCalendar`, `TimeZoneInfo.Local`, 24-h-Lookback) |
| `MaterializeOccurrence` | private | Ausstrahlung atomar materialisieren: `EpisodeCount` gewinnt, sonst `SlotDuration`, sonst 1 Element; Konflikt mit `blocked`/neuen Slots → ganze Ausstrahlung verworfen (`DroppedConflicts++`), Cursor bleibt |
| `FillGaps` | private | Restlücken mit Füller-Regeln (Priorität desc) füllen; Füller muss in Lücke passen, letzter darf Horizont-Ende überschreiten |
| `GapSeconds` | private static | Restlücken-Sekunden für Summary |
| `PersistCursors` | private | `ProgramRuleCursor` je Regel mit `LastPlaced` upserten |
| `ExpandMaterialAsync` | private | Regel-Material → geordnete `SchedulableItem`-Liste (TVShow/Movie/MovieCollection/Genre/Playlist); filtert fehlende `Duration` und fehlende Vollfreigabe; leere Liste → `OrphanedRules++` + Log |
| `ExpandShowAsync`/`ExpandMovieAsync`/`ExpandMovieCollectionAsync`/`ExpandGenreAsync`/`ExpandPlaylistAsync` | private | Material-Expansion je Art; Serien-Episoden sortiert nach `TVShowSeason.Id` dann `Number` |
| `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` | private | MediaItem-Verknüpfung je Inhalt (erstes Item mit `Duration != null`) |
| `TryMovieItem`/`TryEpisodeItem`/`ToItem` | private | Filter + `SchedulableItem`-Erzeugung |

Interne Typen: `SchedulableItem` (ContentType, ContentId, MediaItemId, MovieId, TVShowEpisodeId, Duration), `RuleState` (Items + `NextIndex` + `Commit`), `Occurrence`, `Interval` (mit `Overlaps`), `SummaryCounters` → `ProgramScheduleRunSummary`, `AccessContext`.

**Für Schritt 4 relevant:** Die gesamte Maschinerie (Occurrences, atomare Materialisierung mit `blocked`-Overlaps, `FillGaps`, `PersistCursors`, Expansion mit Filtern) ist wiederverwendbar — ein Re-Bewertungslauf kann als „Materialisierung ab `now` über nur noch geplante (nicht verdrängte) Slots als `blocked`" laufen.

## `ProgramScheduleWorker`
Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs` · `AddHostedService`

- `ExecuteAsync`: 15-s-Startverzögerung (konfigurierbar via ctor `initialDelay`), dann Schleife: Intervall aus `ProgramSettingsService.GetProgramScheduleIntervalAsync` (Standard 15 min), `IBackgroundProcessingGate.EnterOperationAsync("Sendeplan")`, Scope-Factory → `IProgramScheduleService.EnsureCoverageAsync(_timeProvider.GetUtcNow())`.
- Fehler werden geloggt, Schleife läuft weiter. Wartet rein per `Task.Delay(interval)` — **kein Signal/Wakeup** außer Timer.

## `ProgramRuleService` (`IProgramRuleService`)
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs` · scoped; ctor nur `ApplicationDbContext` — kann ein optionales Signal aufnehmen (Muster: `PlaylistBackfillCoordinator` via `IServiceProvider`? — sauberer: optionale Signal-Param direkt, Tests instanziieren `new ProgramRuleService(Db)`).

| Methode | Beschreibung | Trigger-Punkt für Schritt 4 |
|---------|--------------|------------------------------|
| `GetOrCreateChannelAsync` | Einzelnen `TVChannel` liefern/anlegen | — |
| `RenameChannelAsync` | Umbenennen (nicht planrelevant) | nein |
| `GetRulesAsync` | Regeln lesen | nein |
| `CreateRuleAsync` | Regel anlegen (validiert, `IsActive` aus Input) | **ja** — neue Ausstrahlungen einplanen |
| `UpdateRuleAsync` | Alle planrelevanten Felder überschreiben | **ja** — Re-Bewertung; Vergleich Alt/Neu möglich (Entität vor `SaveChanges` im Tracker) |
| `SetRuleActiveAsync` | `IsActive` togglen | **ja** — Deaktivierung verdrängt künftige Sendeplätze; Aktivierung plant neu |
| `DeleteRuleAsync` | Regel löschen (Slots behalten weiche `ProgramRuleId`) | **ja** — künftige Sendeplätze verdrängen |
| `RequireAdminAsync` / `ValidateRuleInputAsync` / `ValidateChannelName` | Admin-Erzwingung + Fachvalidierung | — |

## Signal-/Trigger-Muster (Referenz)

### `IPlaylistBackfillSignal` / `PlaylistBackfillSignal`
Datei: `VideoWebPlayer/Services/PlaylistBackfillSignal.cs` — Singleton (`AddSingleton<PlaylistBackfillSignal>` + Interface-Alias). 1-Slot-`Channel<bool>` (DropWrite, SingleReader): `NotifyMarkersWritten()` weckt (außer während Scan), `BeginScan()` zählt offene Scan-Scopes (Ende des letzten weckt einmal), `WaitAsync(ct)` blockiert bis Signal. Mehrere Signale koaleszieren.

### `ApplicationDbContext.PlaylistBackfillMarking` (SaveChanges-Hook)
Überschreibt `SaveChanges(bool)` + `SaveChangesAsync(bool, ct)`: sammelt Mark-Kandidaten aus dem Change-Tracker (Added/re-assigned `Movie`, `TVShowSeason`, `TVShowEpisode`), upsertet Marker in derselben Transaktion (relationale DB) bzw. tracked (InMemory), ruft danach `_backfillSignal?.NotifyMarkersWritten()`. **Sieht nur Added/Modified — keine Deletes, keine `ExecuteDeleteAsync`-Bulk-Pfade.**

### `PlaylistBackfillWorker`
Datei: `VideoWebPlayer/Services/PlaylistBackfillWorker.cs` — Referenzmuster für „auf Signal ODER Timer warten": `Task.WhenAny(_signal.WaitAsync(ct).AsTask(), Task.Delay(...))` in der Schleife; Settle-Zeit nach Signal; Safety-Sweep mit persistiertem `DueAt`.

## Medien-Trigger-Punkte

- `MediaSourceScanService` (`VideoWebPlayer/Services/MediaSourceScanService.cs`): periodischer Scan-Loop; öffnet `_serviceProvider.GetService<IPlaylistBackfillSignal>()?.BeginScan()` um den Scan herum (Zeile ~101) — analoger Platz für ein Sendeplan-`BeginScan`/End-Notify. Neue Episoden entstehen via `MediaSourceScanner`/`MediaSourceClassifier` → `SaveChanges`-Hook (Marker); ein SaveChanges-basierter Sendeplan-Trigger sähe dieselben Adds.
- `ApplicationDbContext.DeleteMediaSourceAsync` / `DeleteMediaCollectionsForSourceAsync`: rekursive Quell-/Collection-Löschung mit **Bulk-`ExecuteDeleteAsync`** — kein Hook-Zugriff; explizites Signal nach Commit oder periodischer Sweep nötig. `MediaSourceDeletedEvent` wird publiziert, hat **keine** Subscriber (`EventManager.Subscribe`-Mechanismus vorhanden).
- `MediaMetadataEditorService`: keine MediaItem-Löschungen (nur `MovieGenres`/`TVShowGenres`-RemoveRange).
- Kein Einzel-Video-Löschpfad in der UI gefunden — Medienlöschung = Quelle/Collection löschen.

## `IBackgroundProcessingGate` / `BackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs` — Singleton; `EnterOperationAsync(name, ct)` liefert `IAsyncDisposable`-Lease; Restore pausiert alle Operationen. Bereits in `ProgramScheduleWorker`/`MediaSourceScanService` genutzt — serialisiert Sendeplan-Schreibzugriffe, verhindert Restore-Kollision. **Nicht** reentrant-gesichert gegen gleichzeitige Reconcile-Läufe aus UI-Scope und Worker — ein UI-seitiger Direktaufruf müsste dieselbe Lease nehmen; alternativ lösen UI-Pfade nur das Signal aus und der Worker arbeitet (empfohlen).

## `GermanHolidayCalendar`
Datei: `VideoWebPlayer/Services/GermanHolidayCalendar.cs` — `IsHoliday(DateOnly, string? federalState)`; Computus + Landesfeiertage.

## `IUnlockedMediaService` / `UnlockedMediaService`
Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs` — Einzelfreischaltungen (`IsUnlockedAsync`, `GetUnlockedUserIdsAsync`, `SetUnlockedUsersAsync`, `GetUnlockedMovieCollectionIdsAsync` o. ä.); `ProgramScheduleService` nutzt stattdessen direkten `UnlockedMediaEntries`-Zugriff im `AccessContext`.

## `MediaUpdateHub` / `MediaUpdateNotificationService`
`VideoWebPlayer/Hubs/MediaUpdateHub.cs`, `VideoWebPlayer/Services/MediaUpdateNotificationService.cs` — SignalR-Client-Benachrichtigungen (`NotifyNewVideosScannedAsync` u. a.); für Schritt 4 nicht zwingend nötig (Admin-Seite lädt bei Aufruf).

## `ProgramSettingsService`
Getter/Updater für `ProgramScheduleHorizonHours`/`ProgramScheduleIntervalMinutes`/`HolidayFederalState` (s. models.md).

## `ServiceCollectionExtensions` (DI)
`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`: `IProgramRuleService`/`IProgramScheduleService` scoped, `ProgramScheduleWorker` als HostedService, `PlaylistBackfillSignal`+`IPlaylistBackfillSignal` singleton, `IBackgroundProcessingGate` singleton — ein neues `IProgramScheduleSignal` folgt demselben Muster.

## `TVSenderAdmin.razor`
`VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (`/admin/tv-sender`, `InteractiveServer`, Admin-Gate über `IsAdmin`-Claim + serverseitige `RequireAdminAsync`): Karten „Sender" (Name, Bundesland, Horizont, Intervall) und „Programmregeln" (CRUD-Tabelle + Dialoge), injiziert `ApplicationDbContext` (für `MaterialLabel`-Lookups), `IProgramRuleService`, `ProgramSettingsService`. **Kein Sendeplan-Bereich** — verdrängte Sendeplätze sind aktuell nirgends sichtbar.
