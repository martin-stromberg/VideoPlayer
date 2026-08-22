# Offene Aufgaben

Erstellt am: 2026-08-22
Abbruchgrund: Maximale Iterationsanzahl erreicht; Playwright-E2E-Tests koennen in der aktuellen Umgebung nicht belastbar ausgefuehrt werden.

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden und muessen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] Browserbasierte Pointer-/Touch-Abnahme und responsive Randpositionierung des Kontextmenues final validieren (3 Sekunden Haltezeit, Scroll-Abbruch, Klick ausserhalb, Escape, erste/letzte Karte, mobiler Viewport).

## Code-Review-Befunde

- [ ] Playwright-E2E-Tests (`MediaBoxContextMenuInteractionE2ETests`, `MediaBoxContextMenuPositionE2ETests`) muessen in einer eingerichteten Browser-/CI-Umgebung gruen werden; aktuell scheitert die Seitenabnahme mit einem Timeout.

## Fehlgeschlagene Tests

- [ ] `MediaBoxContextMenuInteractionE2ETests` und `MediaBoxContextMenuPositionE2ETests` — in der lokalen Agenten-Umgebung nicht erfolgreich ausfuehrbar (Timeout beim Laden der Startseite).
