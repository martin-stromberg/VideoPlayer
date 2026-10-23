# Inventar: Logikklassen und UI

Bestand der für Issue #258 relevanten Logik- und UI-Artefakte.

## `MediaDurationProbeService` (`VideoWebPlayer/Services/MediaDurationProbeService.cs`)

`public sealed class`, in DI als **scoped** registriert
(`ServiceCollectionExtensions.cs:245`); in `TVSenderAdmin.razor` bereits injiziert
(`@inject MediaDurationProbeService ProbeService`, Zeile 14).

Konstruktor-Abhängigkeiten: `ApplicationDbContext`, `IMediaSourceReader`,
`IMediaDurationProbe`, `IFfprobeLocator`, `IOptions<MediaProbeOptions>`,
`ILogger<MediaDurationProbeService>`, optional `TimeProvider`,
`IProgramScheduleSignal`, `IInternalMediaStreamTokenService`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `ProbePendingAsync(int batchSize, CancellationToken)` | `public` → `Task<int>` | wählt bis zu `batchSize` ausstehende Video-Items (`Duration == null` und (`DurationProbedAt == null` ODER `DurationProbeAttempts < maxAttempts && DurationProbedAt <= retryThreshold`), `VideoFileExtensions`-Prädikat, `OrderBy(mi => mi.Id)`), probt sie und wendet `ApplyOutcome` an; schreibt pro Item `SaveChangesAsync`; feuert `IProgramScheduleSignal.NotifyScheduleChanged()`, sobald mindestens eine `Duration` geschrieben wurde; liefert Anzahl verarbeiteter Items; liefert `0` ohne ffprobe (`_locator.Resolve() is null`, einmalige Warnung pro Prozess über statisches `_missingBinaryLogged`) |
| `GetProgressAsync(string userId, int recentCount = 10, int detailItemCount = 25, CancellationToken)` | `public` → `Task<MediaDurationProbeProgress>` | Admin-only (ruft `RequireAdminAsync`); aggregiert Zähler über `MediaItems` mit Video-Prädikat (`AsNoTracking`): `total`, `probed` (`Duration != null`), `failed` (`Duration == null && DurationProbeAttempts >= maxAttempts`), `LastActivityAt` (`Max(DurationProbedAt)`), `ProbedLastHour`; baut `RecentItems`, `PendingItems` (`Duration == null && Attempts < max`), `FailedItems` (`Duration == null && Attempts >= max`) — beide gekappt auf `detailItemCount`, Zähler bleiben vollständig |
| `DetailItemsAsync(IQueryable<MediaItem>, int take, CancellationToken)` | `private static` → `Task<List<MediaDurationProbeDetailItem>>` | projiziert Items zu Detail-Einträgen: `OrderByDescending(DurationProbedAt)`, `Take(take)`, `Select` auf `MediaDurationProbeDetailItem(Name, MediaCollection.MediaSource.Name, DurationProbedAt, DurationProbeAttempts)` — **keine Id-Projektion heute** |
| `RequireAdminAsync(string userId, CancellationToken)` | `private` (Z. 225-232) | lädt `Users` (`AsNoTracking`); nicht gefunden → `UnauthorizedAccessException("Sie sind nicht angemeldet.")`; `!IsAdmin` → `ForbiddenAccessException("Nur Administratoren dürfen die Laufzeitermittlung einsehen.")` |
| `RunProbesAsync(...)` | `private` | parallele ffprobe-Aufrufe, `SemaphoreSlim(EffectiveParallelProbes)`, kein DB-Zugriff in den Tasks |
| `ProbeItemAsync(...)` | `private` → `Task<ProbeOutcome>` | `LocalDirectory` → `ProbeFileAsync(item.Path)`, sonst `ProbeRemoteItemAsync`; bildet Ergebnis auf `ProbeOutcomeKind` ab; Exceptions (außer Abbruch) → `TransientFailure` |
| `ProbeRemoteItemAsync(...)` | `private` | Kaskade: HTTP-Loopback-URI (`_streamTokens.TryCreateProbeUri(item.Id)`) → `sftp://`-URI (`SftpProbeUriBuilder.TryBuildUri`) → `pipe:0`-Stream (`_reader.OpenFileStream`) |
| `ResolveRemotePath(MediaItem)` | `private static` | absoluten Remote-Pfad bestimmen |
| `ApplyOutcome(MediaItem, ProbeOutcome, DateTime probedAt, int maxAttempts)` | `private` (Z. 369-389) | Success: `Duration = outcome.Duration`, `DurationProbeAttempts = 0`; Transient: `Duration = null`, `DurationProbeAttempts++`; Permanent: `Duration = null`, `DurationProbeAttempts = max(+1, maxAttempts)`; immer `DurationProbedAt = probedAt` |
| `VideoExtensionPredicate()` | `private static` | Ausdrucksbaum: `Name.EndsWith(ext)` für alle `VideoFileExtensions.All` |

Internes Enum `ProbeOutcomeKind` (`Success`, `TransientFailure`, `PermanentFailure`) und
Record `ProbeOutcome(Kind, Duration)` — beide `private`.

Wichtig für den Reset: `ProbePendingAsync` selektiert Items mit `DurationProbedAt == null`
**immer** (Z. 103), sodass zurückgesetzte Items (`DurationProbedAt = null`,
`DurationProbeAttempts = 0`) beim nächsten Aufruf automatisch wieder aufgenommen werden.

**Nicht vorhanden heute:** keine Reset-/Verwaltungsmethode auf `MediaItem`-Ebene
(kein `ResetProbe...`, kein `ExecuteUpdate` auf `MediaItems` — `ExecuteDeleteAsync` wird
nur in `ProgramScheduleService` verwendet).

## Vorbild: `ProgramScheduleService` (`VideoWebPlayer/Services/ProgramScheduleService.cs`)

Das `RequireAdminAsync`-Vorbild-Muster für Admin-Aktionen:

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `DeleteDisplacedSlotsAsync(string userId, long channelId, CancellationToken)` (Z. 404-412) | `public` → `Task<int>` | `RequireAdminAsync`, dann `ExecuteDeleteAsync` auf `ProgramSlots` mit `Status == Displaced`; Rückgabe: Anzahl gelöschter Zeilen; `LogInformation` |
| `ResetScheduleAsync(string userId, long channelId, CancellationToken)` (Z. 415-430) | `public` → `Task<ProgramScheduleResetResult>` | `RequireAdminAsync`, `ExecuteDeleteAsync` auf allen `ProgramSlots` des Kanals + `ProgramRuleCursors`; Rückgabe-Record `ProgramScheduleResetResult(SlotsDeleted, CursorsDeleted)`; `LogWarning` |
| `RequireAdminAsync` (Z. 432-439) | `private` | identisches Muster wie im Probe-Service: `Users.AsNoTracking()`, `UnauthorizedAccessException` / `ForbiddenAccessException` |
| `GetRuleDiagnosesAsync` (Z. 368) | `public` | nutzt pro Item `DurationProbedAt`/`DurationProbeAttempts` zur Diagnose (`ProbePending`/`ProbeExhausted`, Filter Z. 1565: `DurationProbedAt is not null && DurationProbeAttempts >= _maxProbeAttempts`) |

Beide Admin-Aktionen sind im Interface `IProgramScheduleService` deklariert
(`IProgramScheduleService.cs:129`, `:139`). `MediaDurationProbeService` hat dagegen
**kein Interface** — wird direkt als konkrete Klasse injiziert.

## `MediaDurationProbeWorker` (`VideoWebPlayer/Services/MediaDurationProbeWorker.cs`)

`public sealed class : BackgroundService`, als `AddHostedService` registriert
(`ServiceCollectionExtensions.cs:246`). `ExecuteAsync`: initiale Verzögerung
(Default 10 s), dann Schleife im `EffectiveIntervalSeconds`-Takt (Default 60 s);
pro Tick Scope, `ProgramSettingsService.GetTvSenderEnabledAsync` als Feature-Flag,
optional `IBackgroundProcessingGate`-Lease (`EnterOperationAsync("Laufzeitermittlung")`),
dann `ProbePendingAsync(EffectiveBatchSize)` in `do…while (processed > 0)`-Schleife.
Kein eigenes Aufweck-Signal — reagiert nur auf den Timer.

## `MediaSourceScanner` (`VideoWebPlayer/Services/MediaSourceScanner.cs`)

Reset-Semantik-Vorbild in `ScanMediaCollectionInternalAsync` (Z. 267-279): erkennt der
Scan eine geänderte Datei (`existingItem.CreatedAt != file.CreatedAt`), setzt er
`CreatedAt`, `Changed = true`, `ClassifiedAt = null` und — für die erneute Laufzeitermittlung —
`Duration = null; DurationProbedAt = null; DurationProbeAttempts = 0` (Z. 274-276).

## `TVSenderAdmin.razor` (`VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`, 1462 Zeilen)

Route `/admin/tv-sender`, `@rendermode InteractiveServer`, `@implements IDisposable`.
Bereits injiziert: `DbContext`, `IProgramRuleService`, `IProgramScheduleService`,
`IProgramScheduleSignal`, `MediaDurationProbeService` (`ProbeService`),
`ProgramSettingsService`, `AuthenticationStateProvider`, `VideoWebPlayerClient`.

### Laufzeitermittlung-Tab (Z. 472-633)

- Karte `id="tvsender-probe-card"` (Z. 475).
- Statuszeile `id="tvsender-probe-status"` (`ProbeStatusLabel`, Z. 499).
- Zähler-Chips `id="tvsender-probe-probed"` / `tvsender-probe-pending` /
  `tvsender-probe-failed` (Z. 501-517); Chips enthalten die
  `chip-detail-btn`-Schaltflächen `tvsender-probe-pending-details` /
  `tvsender-probe-failed-details` zum Aufklappen.
- Fortschrittsbalken `.progress` (Z. 519-522).
- Pending-Detail: `<details class="probe-detail" id="tvsender-probe-pending-detail"
  open="@showPendingProbeDetails">` (Z. 531) mit Tabelle (Name | Quelle | Versuche |
  Letzter Versuch), Zeilen `class="tvsender-probe-detail-row"` (Z. 547); Restzähler-Zeile
  „… und N weitere" bei Kappung (Z. 554-557).
- Failed-Detail: analog `id="tvsender-probe-failed-detail"` (Z. 567), Summary
  „Fehlgeschlagene Dateien (@FailedCount) — dauerhaft nicht prüfbar", Zeilen
  `tvsender-probe-detail-row` (Z. 583). **Keine Aktions-Spalte, keine Schaltfläche je
  Zeile — die Tabelle ist heute rein informativ.**
- Rolling-Liste `id="tvsender-probe-recent"` (Z. 607), Zeilen `tvsender-probe-row`.
- Es gibt **kein eigenes Statusfeld** für die Probe-Karte (kein `probeStatus`) —
  Statusmeldungen existieren als `channelStatus` (`tvsender-channel-status`),
  `rulesStatus` (`tvsender-rules-status`), `scheduleStatus` (`tvsender-schedule-status`).

### Dialog- und Aktions-Muster

- Bestätigungsdialoge `admin-dialog-overlay` + `admin-dialog` mit
  `@onclick:stopPropagation`; Trigger-Flags `confirmCleanup` (Z. 635-649) und
  `confirmReset` (Z. 651-665); Lösch-Dialog über `deleteCandidate` (Z. 667-681).
- Aktionszellen-Muster `class="actions-cell"` mit `btn btn-sm`-Schaltflächen in der
  Regel-Tabelle (Z. 121-125).
- `CleanupDisplacedAsync` (Z. 914-932) / `ResetScheduleAsync` (Z. 934-951): Flag
  zurücksetzen, Service-Aufruf mit `userId`, Statusmeldung in `scheduleStatus`,
  `ReloadScheduleAsync`, Fehler → `ex.Message` ins Statusfeld. `ResetScheduleAsync`
  stößt zusätzlich `ScheduleSignal.NotifyScheduleChanged()` an.

### Datenfluss / Auto-Refresh

- `LoadProbeProgressAsync` (Z. 773-776): `ProbeService.GetProgressAsync(userId, RecentProbeCount = 10)` —
  `detailItemCount` wird nicht übergeben (Default 25).
- `ProbeProgressLoopAsync` (Z. 780-816): `PeriodicTimer(15 s)`, ruft `LoadProbeProgressAsync`
  über `InvokeAsync` (Dispatcher), schluckt transiente Fehler; Abbruch über `refreshCts`
  in `Dispose` (Z. 1086-1090).
- Initial-Laden in `OnInitializedAsync` (Z. 732-767), u. a. `userId` aus
  `ClaimTypes.NameIdentifier`.
- Flags `showPendingProbeDetails` / `showFailedProbeDetails` (Z. 717-718).
- Hilfsmethoden `ProbeStatusLabel`, `ProbePercent`, `RecentResultLabel`,
  `FormatDuration` (Z. 821-839).

### CSS (`VideoWebPlayer/wwwroot/app.css`)

Vorhandene Klassen: `.actions-cell` (Z. 617), `.chip-detail-btn` (Z. 866),
`.probe-detail` + `summary` + `.table-wrap-inner` (Z. 881-900).
