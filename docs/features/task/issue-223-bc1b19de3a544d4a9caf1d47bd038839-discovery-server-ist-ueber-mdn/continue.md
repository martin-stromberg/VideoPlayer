# Offene Aufgaben

Erstellt am: 2026-10-04
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

Hinweis: Alle Code-Planelemente sind vollständig umgesetzt und alle Reviews/Tests grün.
Die offenen Punkte sind Dokumentationsaufgaben (Lifecycle-Schritte 12/12b/12c dieses
Laufs) und eine manuelle Verifikation, die nicht automatisierbar ist.

## Offene Planelemente

- [x] Task 23: `docs/INSTALL_AVAHI.md` aktualisieren (In-App-Advertisement als Standardweg, Avahi als Alternative, Doppel-Advertisement-Hinweis) — erledigt in Lifecycle-Schritt 12
- [x] Task 24: `docs/GUIDE_Installation.md`, `docs/help/einrichtung.md`, `docs/API.md`, `README.md`, `docs/INDEX.md`, `docs/RELEASE_NOTES.md` aktualisieren inkl. Hinweis auf MAUI-App-Umstellung — erledigt in Lifecycle-Schritten 12/12b/12c
- [ ] Task 26: Manuelle mDNS-Verifikation per `avahi-browse`/`dns-sd` (Instanzname, Port, TXT-Records; Verschwinden nach Admin-Ausschalten ≤ ~60 s; Shutdown-Goodbye) — manuelle Abnahme, nicht automatisierbar

## Code-Review-Befunde

Keine — `review-code.md` trägt den Status „Keine Befunde" (alle Befunde aus
`review-code.1.md` und `review-code.2.md` behoben).

## Usability-Befunde

Keine — `review-usability.md` trägt den Status „Keine Befunde".

## Fehlgeschlagene Tests

Keine — `test-results.md` trägt den Status „Keine Fehler" (1665/1666 grün,
1 dokumentierter Baseline-Skip).
