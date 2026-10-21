# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine.

## E2E-Abdeckung

Keine geplanten E2E-Szenarien — die Anforderung enthält keinen Benutzerfluss (vollautomatische Hintergrundfunktion, keine UI-/Endpunkt-Änderungen). Begründung siehe `plan.md` Abschnitt „E2E-Tests".

## Zusammenfassung

- Gesamt: 1796
- Bestanden: 1795
- Fehlgeschlagen: 0
- Übersprungen: 1 (`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — `[Fact(Skip=…)]`, dokumentierter bekannter Alt-Befund, bereits im Basislauf übersprungen)

Läufe:

| Lauf | Befehl | Ergebnis | Nachweis |
|------|--------|----------|----------|
| Build Debug | `dotnet build VideoPlayer.sln -c Debug` | 0 Fehler (140 bestehende Warnungen) | – |
| Build Release | `dotnet build VideoPlayer.sln -c Release` | 0 Fehler | – |
| Baseline (vor Umsetzung) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug` | 1760 bestanden / 0 Fehler / 1 übersprungen | `inventory/test-results/baseline-test-results.trx` |
| Final | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug` | 1795 bestanden / 0 Fehler / 1 übersprungen | `test-results/final-test-results.trx` |

Delta Baseline→Final: +35 bestandene Tests (alle neuen Tests dieses Schritts).

## Testabdeckung

**Abdeckung:** Nicht messbar (Lauf ohne Coverage-Collector; die neuen Elemente sind durch die oben genannten 35 Tests abgedeckt — Probe-Service, ffprobe-Kapselung, Locator, Worker, Scanner-Reset, Backup-Regression).

## Fehlende Tests

Keine bekannten Lücken für diesen Schritt.
