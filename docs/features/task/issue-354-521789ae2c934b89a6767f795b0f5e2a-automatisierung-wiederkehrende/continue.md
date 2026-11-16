# Offene Aufgaben

Erstellt am: 2026-11-14
Abbruchgrund: Kein weiterer Fortschritt im automatisierten Zyklus möglich — die offenen Punkte sind durch eine externe Voraussetzung blockiert (GitHub führt `schedule`- und `workflow_dispatch`-Workflows nur vom Default-Branch `main` aus; die neuen Workflows existieren dort erst nach Merge und Promotion `staging` → `main`).

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] Reale Verifikation der drei Workflows nach Promotion auf `main` (plan.md Umsetzungsschritt 8 / Akzeptanzszenarien 1–6): pro Workflow `workflow_dispatch` auslösen — erstes Issue mit Typ-Label wird erzeugt, sofortiger Zweitlauf meldet Skip (`::notice::`) ohne Duplikat. Beobachten, ob die `@softwarewerkstatt`-Erwähnung die externe Bearbeitung auslöst; falls nicht, `gh issue create --assignee softwarewerkstatt` ergänzen. Anwender wurde über die beiden Auflösungswege (manuelle Abnahme nach Promotion / dokumentierter Verzicht) per Issue-Kommentar informiert: https://github.com/martin-stromberg/VideoPlayer/issues/354#issuecomment-6885260802

## Code-Review-Befunde

Keine.

## Usability-Befunde

Keine.

## Fehlgeschlagene Tests

- [ ] Reale End-to-End-Abnahme Szenarien 1–6 — `Nicht durchführbar` vor Promotion (siehe `test-results.md`, Abschnitt „Reale End-to-End-Abnahme"); alle lokal möglichen Nachweise (YAML-Validierung, gh-Guard real gegen GitHub, Bash-Logik mit gh-Stub, wörtlicher Body-Abgleich) sind bestanden. Kein automatisierter Test ist fehlgeschlagen; zwei einmalige Playwright-Flakes (`LiveTvClipBlocksE2ETests.LiveTv_ClipRun_RendersAggregatedBlockRow_WithInlineDetail`, `TvSenderOnAirE2ETests.Admin_TogglesBackOnAir_StreamAndPlayerResume`) gingen bei Einzelwiederholung sofort grün.
