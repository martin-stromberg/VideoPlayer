# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine verbleibenden. Zwei Playwright-E2E-Ausfälle im Volllauf waren Flakes und sind im
isolierten Retry beide grün (siehe Abschnitt „Flaky-Beobachtungen").

## E2E-Abdeckung

Keine geplanten E2E-Szenarien — die Anforderung berührt keinen Benutzerfluss
(Testhärtung auf Service-/Helper-Ebene, siehe plan.md).

## Reale End-to-End-Abnahme

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| Testhärtung Sendeplan (kein Benutzerfluss; Nachweis = verstärkte Assertions) | – | Verzichtet (Anwender, 2026-11-09, reine Teständerung ohne Auslieferungspfad) |

## Zusammenfassung

Volllauf `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug` (unfiltriert,
inkl. aller E2E-Tests): Gesamt **2933**, Bestanden **2929**, Fehlgeschlagen **2 (beide im
Retry grün — Flakes)**, Übersprungen **2** (davon 1 plattformbedingt Unix, 1 xUnit-intern).

Gefilterte Verifikationsläufe je Befund: alle 31 gehärteten/neuen Tests grün
(Nachweis: `dotnet test --filter` über alle geänderten Testklassen, 63 AssertGapless-
Aufrufer-Tests, 31 Befund-Tests).

## Flaky-Beobachtungen

- `LiveTvClipBlocksE2ETests.LiveTv_ClipRun_RendersAggregatedBlockRow_WithInlineDetail` —
  Playwright-Timeout im Volllauf; isolierter Retry: **Bestanden**.
- `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` — Timeout 30 s auf
  `#playlist-detail-selected-entry` im Volllauf; isolierter Retry: **Bestanden**.
- Bereits im Basis-Lauf (unveränderter Code) flackerte
  `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` — ebenfalls im Retry grün.
  Alle drei betreffen Playwright-Seiteneffekte, nicht die geänderten Dateien.

## Verifizierte Verstärkungen (Auswahl)

| Befund | Verifikation |
|--------|--------------|
| B1 AssertGapless | 63 Aufrufer-Tests grün; `ProgramScheduleTestDbAssertGaplessTests` (6 Gegenprobe-Fälle: Überlappung, Enthaltensein, Lücke, Fensterkante, Unterdeckung, korrekte Kette) beweist, dass die Prüfung feuert — kein Produktiv-Bug aufgedeckt |
| B2–B15 | je Einzeltest grün gegen Ist-Code — keine Produktiv-Bugs |
| B14 | 4 neue Worker-Tests mit Fakes grün (Signal, Gate-Lease, Intervall-Neulesung) |

## Testabdeckung

**Abdeckung:** Nicht messbar (Coverage-Lauf nicht ausgeführt; die Änderung betrifft nur
Testcode, Deckungsziel ist die Assertion-Härtung selbst).

## Fehlende Tests

Keine neu aufgetretenen. Dokumentierter Nicht-Befund: B9 (Zieltest existiert nur auf dem
ungemergten #343-Branch) — beim Merge von `task/…-strang-ueberlappung` nachrüsten.
