# Bestandsaufnahme — Logik

## `MediaDurationProbeService`
Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs` — `sealed`, `scoped` registriert
(`ServiceCollectionExtensions`, Zeile 242). Direkt injizierbar in Blazor-Komponenten.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProbePendingAsync(int, CancellationToken)` | `public` | Prüft bis zu `batchSize` ungeprüfte Videodateien; selektiert `Duration == null && (DurationProbedAt == null \|\| (Attempts < max && DurationProbedAt <= now - RetryDelay))` über `VideoExtensionPredicate()`; wendet Ergebnisse via `ApplyOutcome` an; feuert `IProgramScheduleSignal.NotifyScheduleChanged()` wenn ≥1 `Duration` geschrieben wurde. |
| `RunProbesAsync` | `private` | Führt `ProbeItemAsync` je Item parallel via `SemaphoreSlim(EffectiveParallelProbes)` aus; keine DB-Zugriffe in den Tasks. |
| `ProbeItemAsync` | `private` | `LocalDirectory` → `ProbeFileAsync`, sonst `OpenFileStream` + `ProbeStreamAsync`; bildet `MediaDurationProbeResult` auf `ProbeOutcome` (Success/TransientFailure/PermanentFailure) ab. |
| `ApplyOutcome` | `private` | Success → `Duration` + `Attempts = 0`; Transient → `Attempts++`; Permanent → `Attempts = max(Attempts+1, max)`; setzt immer `DurationProbedAt`. |
| `VideoExtensionPredicate` | `private static` | Expression-Filter `Name.EndsWith(ext)` über `VideoFileExtensions.All` — identische Teilmenge für Anzeige erforderlich. |

Konstruktor-Abhängigkeiten: `ApplicationDbContext`, `IMediaSourceReader`, `IMediaDurationProbe`,
`IFfprobeLocator`, `IOptions<MediaProbeOptions>`, `ILogger<MediaDurationProbeService>`,
optional `TimeProvider` und `IProgramScheduleSignal`. `_timeProvider` ist für Tests steuerbar.

## `MediaDurationProbeWorker`
Datei: `VideoWebPlayer/Services/MediaDurationProbeWorker.cs` — `BackgroundService`,
`AddHostedService` (Zeile 243 der Registrierung).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync(CancellationToken)` | `protected override` | Initial delay 10 s, dann Schleife: Scope → `MediaDurationProbeService`, optional `IBackgroundProcessingGate`-Lease, `ProbePendingAsync(EffectiveBatchSize)` bis 0, dann `Task.Delay(EffectiveIntervalSeconds)`. Fehler werden geloggt, die Schleife läuft weiter. |

Der Worker hält **keinen** Fortschrittszustand im Speicher — der gesamte Stand liegt in den
`MediaItem`-Spalten. Die Anzeige kann daher rein aus der DB aggregiert werden.

## `ProgramScheduleService` (Referenzmuster)
Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRuleDiagnosesAsync(userId, channelId, ct)` | `public` | Referenz für admin-gegate On-Demand-Aggregation: `RequireAdminAsync`, dann Regel-Expansion + `SummaryCounters` → `IReadOnlyDictionary<long, ProgramRuleDiagnosis>`. |
| `RequireAdminAsync(userId, ct)` | `private` | `_db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)` → `UnauthorizedAccessException` („Sie sind nicht angemeldet.") wenn nicht gefunden, `ForbiddenAccessException` wenn `!user.IsAdmin`. Identisches Muster in `ProgramRuleService` (Zeile 219). |

## `TVSenderAdmin.razor`
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — `@page "/admin/tv-sender"`,
`@rendermode InteractiveServer`, `@implements IDisposable`.

- Admin-Gate: `OnInitializedAsync` prüft `IsAuthenticated && HasClaim("IsAdmin", "True")`;
  Nicht-Admins sehen nur „Nicht autorisiert." — alle Karten rendern nur im `isAdmin`-Zweig.
- Aufbau: drei `admin-card admin-card-wide`-Abschnitte (Sender-Einstellungen, Programmregeln
  mit `tvsender-rule-diagnosis`-Spalte, Sendeplan mit `#tvsender-generate-now`),
  Statuszeilen via `form-text`+id, `admin-chip`-Badges, `admin-table-wrap` +
  `table table-dark table-striped`.
- Datenladen: `OnInitializedAsync` → `ProgramRuleService`/`ProgramSettings`/`DbContext`/
  `Client`; `ReloadRulesAsync` ruft zusätzlich `ReloadDiagnosesAsync`
  (`GetRuleDiagnosesAsync`). `GenerateNowAsync` stößt `ScheduleSignal.NotifyScheduleChanged()`
  an und lädt verzögert neu.
- `MediaDurationProbeService` ist **noch nicht** injiziert.

## Zustandsklassen der Probezustände (aus `ProbePendingAsync`-Selektion + `ApplyOutcome`)

| Zustand | Bedingung |
|---------|-----------|
| Erfolgreich ermittelt | `Duration != null` |
| Ausstehend (nie geprüft) | `Duration == null && DurationProbedAt == null` |
| Ausstehend (Wiederholung läuft) | `Duration == null && DurationProbedAt != null && DurationProbeAttempts < EffectiveMaxAttempts` |
| Dauerhaft gescheitert | `Duration == null && DurationProbeAttempts >= EffectiveMaxAttempts` |
