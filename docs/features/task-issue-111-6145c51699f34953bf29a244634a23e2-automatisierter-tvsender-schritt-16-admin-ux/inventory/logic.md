# Logik

## `MediaDurationProbeService`
Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs` (sealed, scoped)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProbePendingAsync(int, CancellationToken)` | public | probt bis zu `batchSize` ausstehende Video-Items, schreibt `Duration`/`DurationProbedAt`/`DurationProbeAttempts`; benachrichtigt `IProgramScheduleSignal` bei neuen Laufzeiten |
| `GetProgressAsync(string userId, int recentCount = 10, CancellationToken)` | public | aggregiert Fortschritt für die Admin-Ansicht: Zähler + `LastActivityAt` + `ProbedLastHour` + `RecentItems`; ruft zuerst `RequireAdminAsync` |
| `RequireAdminAsync` | private | wirft `UnauthorizedAccessException` (kein User) bzw. `ForbiddenAccessException` (kein Admin) |
| `RunProbesAsync` / `ProbeItemAsync` / `ProbeRemoteItemAsync` / `ApplyOutcome` | private | ffprobe-Ausführung, Retry-Klassifikation (Transient/Permanent), Ergebnisübertrag |
| `VideoExtensionPredicate` | private static | Ausdruck `Name.EndsWith(ext)` über `VideoFileExtensions.All`, geteilt von `ProbePendingAsync` und `GetProgressAsync` |

`ApplyOutcome`: Success → `Duration` gesetzt + `Attempts = 0`; TransientFailure →
`Attempts++`; PermanentFailure → `Attempts = max(Attempts + 1, maxAttempts)`;
`DurationProbedAt` wird immer gesetzt.

## `TVSenderAdmin` (Razor `@code`)
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (`@page "/admin/tv-sender"`, `InteractiveServer`, `IDisposable`)

Injizierte Dienste: `ApplicationDbContext`, `IProgramRuleService`,
`IProgramScheduleService`, `IProgramScheduleSignal`, `MediaDurationProbeService`
(konkret), `ProgramSettingsService`, `AuthenticationStateProvider`,
`VideoWebPlayerClient`.

| Methode/Feld | Zweck |
|--------------|-------|
| `OnInitializedAsync` | Admin-Claim prüfen (`IsAdmin=True`), Einstellungen/Quellen/Genres/Playlists/Regeln/Sendeplan/Probe-Fortschritt laden, Refresh-Loop starten |
| `LoadProbeProgressAsync` | `ProbeService.GetProgressAsync(userId, RecentProbeCount=10)` |
| `ProbeProgressLoopAsync` | `PeriodicTimer` 15 s; DbContext-Zugriff über `InvokeAsync` auf dem Dispatcher (Concurrency-Fix aus Schritt 11 — nicht rückbauen); einzelne Fehlschläge beenden die Schleife nicht |
| `ProbeStatusLabel` / `ProbePercent` / `RecentResultLabel` / `FormatDuration` | Darstellung der Fortschrittskarte |
| `ReloadGenreOptionsAsync` | Genre-Auswahl des Editors, Quell-Filter-abhängig; behält bereits gewählte, außerhalb liegende Genres sichtbar |
| `ReloadRulesAsync` / `ReloadDiagnosesAsync` | Regeln + `GetRuleDiagnosesAsync` (Planbar-Spalte) |
| `GenerateNowAsync` | `ScheduleSignal.NotifyScheduleChanged()`, 4 s warten, Sendeplan + Diagnosen neu laden; `generatePending`-Guard |
| `ReloadScheduleAsync` | `GetScheduleAsync(channel.Id, now-2h, 200)` + Titel-/Regelnamen-Dictionaries |
| `SaveChannelSettingsAsync` | `RenameChannelAsync` + `UpdateHolidayFederalStateAsync` + `UpdateProgramScheduleSettingsAsync` + `UpdateLiveStreamSettingsAsync` |
| `StartCreate` / `StartEdit` / `CancelEdit` / `SaveRuleAsync` | Regel-Editor (`editRule`-State, `RuleEditModel`); Erfolg → `rulesStatus` + `ReloadRulesAsync` |
| `ToggleActiveAsync` / `AskDelete` / `DeleteRuleAsync` | (De)Aktivieren, Löschen mit `deleteCandidate`-Bestätigungsdialog |
| `OnRuleTypeChanged` / `OnRecurrenceChanged` / `OnMaterialSelected` / `OnGenreToggled` / `OnSourceToggledAsync` / `OnPlaylistSelected` / `ClearMaterial` / `UpdateGenreMaterialLabel` | Editor-Interaktionen inkl. `MediaSearchSelector`-Einbindung |
| `MaterialLabel` / `RuleTypeLabel` / `RecurrenceLabel` / `DurationLabel` / `WeekdayLabel` / `DiagnosisLabel` / `SlotTitle` / `RuleName` | Anzeige-Helper |
| `Dispose` | `disposed = true`, `refreshCts.Cancel()` |

## `IProgramScheduleSignal` / `ProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` — `NotifyScheduleChanged()`,
`BeginScan()`, `WaitAsync(CancellationToken)`; parameterloser Konstruktor.

## Weitere für die Seite relevante Services

- `ProgramRuleService` (`IProgramRuleService`): `(ApplicationDbContext, IProgramScheduleSignal? = null)`;
  `GetOrCreateChannelAsync`, `GetRulesAsync`, `CreateRuleAsync`, `UpdateRuleAsync`,
  `DeleteRuleAsync`, `SetRuleActiveAsync`, `RenameChannelAsync`,
  `GetGenreMaterialOptionsAsync`, `GetAllowedMaterialTypes`.
- `ProgramScheduleService` (`IProgramScheduleService`): `(db, IProgramRuleService,
  ProgramSettingsService, ILogger, IOptions<MediaProbeOptions>? = null)`;
  `GetScheduleAsync`, `GetRuleDiagnosesAsync`, `EnsureCoverageAsync`.
- `ProgramSettingsService`: `(ApplicationDbContext, ILogger<ProgramSettingsService>)`;
  Getter/Updater für Bundesland, Horizont, Intervall, Segment-Parameter;
  `HolidayFederalStates`.
- `VideoWebPlayerClient` (`VideoWebPlayer.Client`, konkret): `(HttpClient, ILogger)`;
  hier genutzt: `EnsureAuthorizationTokenSilentlyAsync` (schluckt Fehler),
  `RequestPlaylistsAsync` + `RequestPublicPlaylistsAsync` (HTTP-GET `api/playlists*`).
