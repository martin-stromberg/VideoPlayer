# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `MediaDurationProbeProgress` (sealed record) — angelegt in `VideoWebPlayer/Services/MediaDurationProbeProgress.cs` mit `TotalCount`, `ProbedCount`, `PendingCount`, `FailedCount`, `LastActivityAt`, `ProbedLastHour`, `RecentItems`
- [x] `MediaDurationProbeRecentItem` (sealed record) — angelegt mit `Name`, `SourceName`, `ProbedAt`, `Duration`, `Attempts`
- [x] Methode `GetProgressAsync(string userId, int recentCount = 10, CancellationToken)` in `MediaDurationProbeService` — vorhanden; Admin-Gate, `VideoExtensionPredicate()`-Teilmenge, drei `CountAsync` + `MaxAsync` + Top-N-Projektion, `Pending = Total − Probed − Failed`, Rate der letzten Stunde via `_timeProvider`, `recentCount <= 0` geclamped
- [x] Methode `RequireAdminAsync` in `MediaDurationProbeService` — `private`, gleiches Muster wie `ProgramRuleService`/`ProgramScheduleService` (`UnauthorizedAccessException`/`ForbiddenAccessException`)
- [x] `TVSenderAdmin.razor`: Injektion `MediaDurationProbeService ProbeService` — vorhanden
- [x] `TVSenderAdmin.razor`: Felder `probeProgress`, `refreshCts`, Konstanten `RecentProbeCount`/`ProbeRefreshInterval` — vorhanden
- [x] `TVSenderAdmin.razor`: `LoadProbeProgressAsync`, `ProbeProgressLoopAsync` (PeriodicTimer 15 s, Einzelfehler tolerant, Abbruch via Cancellation), `ProbeStatusLabel` (alle 5 Zustände), `RecentResultLabel`, `FormatDuration`, `ProbePercent` — vorhanden
- [x] `TVSenderAdmin.razor`: `OnInitializedAsync` lädt Fortschritt + startet Loop (nur `isAdmin`-Zweig); `Dispose` bricht Timer ab (`disposed` + `refreshCts.Cancel()`)
- [x] Markup: `admin-card` „Laufzeitermittlung" (`#tvsender-probe-card`), Status `#tvsender-probe-status`, Zähler `#tvsender-probe-counts` (`-probed`/`-pending`/`-failed`), Fortschrittsbalken (`progress`/`progress-bar`, aria-Attribute), `#tvsender-probe-last-activity`, `#tvsender-probe-rate`, Tabelle `#tvsender-probe-recent` mit `.tvsender-probe-row`, Empty-State `#tvsender-probe-empty` bei `TotalCount == 0` — alle vorhanden
- [x] Tests `MediaDurationProbeServiceTests_Progress` — alle 5 geplanten Fälle vorhanden und grün (Zählung, Reihenfolge/Limit, Aktivität/Rate, Leerstand, Admin-Erzwingung)
- [x] E2E-Erweiterung `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` — Seeds (1 ermittelt, 1 dauerhaft gescheitert, beide nicht ausstehend) + Assertions auf Karte/Zähler/Rolling-Liste, grün

## Hinweise

- Der Plan nannte `GetProgressAsync_NonAdmin_Throws` und `_UnknownUser_Throws` als zwei Zeilen;
  implementiert ist ein konsolidierter Test `GetProgressAsync_NonAdmin_Throws`, der beide
  Ausnahmen prüft — identisch zum Bestandsmuster `Diagnosis_NonAdmin_Throws`.
- Dokumentation (Tasks 8–9) gehört zum Lifecycle-Schritt 12 und ist im Plan-Review nicht Teil
  der Code-Prüfung.
