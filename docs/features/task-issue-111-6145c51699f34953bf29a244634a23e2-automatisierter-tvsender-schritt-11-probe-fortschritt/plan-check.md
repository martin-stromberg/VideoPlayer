# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

Anforderung (aus `requirement.md` / N6): Der Administrator erkennt auf der
TV-Sender-Adminseite ohne Log-/DB-Zugriff, ob die Laufzeitermittlung voranschreitet oder
hängt.

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Gesamt-/Ausstehend-/Erfolgreich-/Fehlgeschlagen-Zähler | `MediaDurationProbeProgress` mit `TotalCount`/`ProbedCount`/`PendingCount`/`FailedCount`, abgeleitet aus `Duration`/`DurationProbedAt`/`DurationProbeAttempts` + `EffectiveMaxAttempts` | `GetProgressAsync_CountsStatesSeparately` | Abgedeckt |
| Letzter Aktivitätszeitpunkt | `LastActivityAt` = `MAX(DurationProbedAt)` (indiziert) | `GetProgressAsync_ReportsLastActivityAndHourlyRate` | Abgedeckt |
| Rolling-Liste der zuletzt ermittelten Laufzeiten | `RecentItems`: `DurationProbedAt != null`, neueste zuerst, `Take(recentCount)`, mit Name/Quelle/Ergebnis/Versuchen | `GetProgressAsync_RecentItems_OrderedNewestFirstAndLimited` | Abgedeckt |
| „arbeitet" vs. „hängt" sichtbar | `ProbedLastHour` + Statushinweis-Logik `ProbeStatusLabel()` (u. a. „ausstehend, aber keine Aktivität in der letzten Stunde") + 15-s-Auto-Refresh | E2E-Erweiterung (Karte + Zähler sichtbar); Hinweislogik implizit über Zähler-Tests | Abgedeckt |
| Optional: Rate/Restlaufzeit | `ProbedLastHour` (Ermittlungen der letzten Stunde) | `GetProgressAsync_ReportsLastActivityAndHourlyRate` | Abgedeckt |
| Keine neue Persistenz | Nur Leseabfragen auf bestehenden `MediaItem`-Spalten; Plan sagt „Keine" Migration | — | Abgedeckt |
| Admin-only | Seiten-Gate `isAdmin` (bestehend) + `RequireAdminAsync` in `GetProgressAsync` (serverseitig) | `GetProgressAsync_NonAdmin_Throws`/`_UnknownUser_Throws`; `NonAdmin_SeesNotAuthorized` (E2E, bestehend) | Abgedeckt |
| Konsistentes UI-Design | `admin-card`/`admin-chip`/`admin-empty-state`/`table-dark`, ids `tvsender-probe-*` | E2E-Erweiterung | Abgedeckt |
| Gleiche Zählmenge wie Worker | Wiederverwendung `VideoExtensionPredicate()` (Video-Endungen) | `CountsStatesSeparately` (Nicht-Video-Item) | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Admin sieht Fortschrittsanzeige (Zähler + Rolling-Liste) auf `/admin/tv-sender` | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (erweitert — konsolidiert wie projektüblich) | Abgedeckt |
| Nicht-Admin erreicht die Anzeige nicht | `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized` (bestehend) + Service-Unit-Test | Abgedeckt |

## Hinweise

- Der Plan begrenzt bewusst den E2E-Nachweis auf die statische Anzeige (deterministisch
  seedbar). Der 15-s-Auto-Refresh und die „hängt"-Heuristik werden service-/designseitig
  abgesichert; ein E2E auf das Timer-Verhalten wäre inhärent instabil (Wartezeit), das
  Risiko ist dokumentiert.
- Die E2E-Seeds verwenden nur nicht-ausstehende Items (ermittelt/dauerhaft gescheitert),
  weil der echte `MediaDurationProbeWorker` in der Test-App mitläuft und ausstehende Items
  verändern könnte, falls ein ffprobe-Binary auffindbar ist.
