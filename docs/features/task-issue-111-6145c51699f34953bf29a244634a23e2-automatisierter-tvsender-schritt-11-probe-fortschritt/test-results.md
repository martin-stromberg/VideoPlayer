# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

### VideoWebPlayer.Tests (voller Lauf, Release)

- **PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest** — `TimeoutException` beim
  `WaitForSelectorAsync("#playlist-detail-selected-entry")` (Playwright-Timing, bekannter
  E2E-Flake ohne Bezug zur Änderung). **Einzelwiederholung: grün** (10 s).

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Admin sieht Karte „Laufzeitermittlung" mit Zählern, Status und Rolling-Liste | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (erweitert) | Bestanden |
| Nicht-Admin erreicht die Seite nicht | `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized` | Bestanden |
| Service-Admin-Gate | `MediaDurationProbeServiceTests_Progress.GetProgressAsync_NonAdmin_Throws` | Bestanden |

## Zusammenfassung

Voller Lauf `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release`
(5 min 18 s, Nachweis: `full-suite-schritt11.trx`):

- Gesamt: 2014
- Bestanden: 2012 (+1 bei Einzelwiederholung → 2013 effektiv grün)
- Fehlgeschlagen: 1 (bekannter Flake, Wiederholung grün)
- Übersprungen: 1 (bekannter Alt-Skip `PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`)

## Testabdeckung

**Abdeckung:** Nicht messbar (kein Coverage-Collector ausgeführt).

## Umgebungshinweis (Debug-Ausgabe gesperrt)

Der Debug-Build `dotnet build VideoPlayer.sln` schlägt derzeit ausschließlich am Kopieren in
`VideoWebPlayer/bin/Debug/net10.0/` fehl (MSB3027) — die Ausgabedateien sind durch die laufende
Benutzer-Instanz `VideoWebPlayer.exe` (PID 55720, gestartet 16:35 aus Visual Studio) gesperrt.
Der Prozess wurde nicht beendet (laufende Anwender-Instanz). Die Debug-Kompilierung wurde
stattdessen vollständig über `dotnet build VideoPlayer.sln -p:OutDir=<Scratch>` nachgewiesen:
**0 Fehler**, Warnungsstand unverändert (142 = Bestand). Release-Build `dotnet build
VideoPlayer.sln -c Release`: 0 Fehler. Sobald die Instanz geschlossen ist, ist der normale
Debug-Build ohne weitere Änderung wieder möglich.
