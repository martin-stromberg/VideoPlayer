# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine — der vollständige Lauf war erfolgreich.

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Anonymer Aufruf → 401 | `ProgramGuideE2ETests.Anonymous_RequestNow_Returns401`, `...Anonymous_RequestUpcoming_Returns401` | Bestanden |
| Angemeldeter Benutzer (gekoppeltes Gerät) → Jetzt/Gleich | `ProgramGuideE2ETests.PairedDevice_RequestNow_ReturnsCurrentAndNext` | Bestanden |
| `access_token`-Abfrageparameter | `ProgramGuideE2ETests.PairedDevice_RequestNowWithAccessTokenQuery_Returns200` | Bestanden |
| Begrenzte Vorschau + Servergrenze | `ProgramGuideE2ETests.PairedDevice_RequestUpcoming_ReturnsBoundedEntries`, `...BeyondLimit_Returns400` | Bestanden |

Alle geplanten Pflicht-E2E-Szenarien existieren und liefen erfolgreich durch.

## Zusammenfassung

Endlauf nach Implementierung (Befehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --logger "console;verbosity=normal"`, Arbeitsverzeichnis Repo-Wurzel, Exit-Code 0):

- Gesamt: 1893
- Bestanden: 1892 (davon 27 neue Programmauskunft-Tests: 12 Service + 9 Controller + 6 E2E)
- Fehlgeschlagen: 0
- Übersprungen: 1 (bekannter Alt-Skip `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`, bereits in der Baseline dokumentiert)
- Dauer: ~4,0 Minuten
- Nachweis: `final-dotnet-test.log` (vollständige Konsolenausgabe)

Vorausgegangener Build: `dotnet build VideoPlayer.sln` Debug und Release je 0 Fehler
(141 Warnungen = ausschließlich vorbestehende BL0008 in Account-Razor-Seiten, keine aus den
neuen Dateien).

## Testabdeckung

**Abdeckung:** Nicht gemessen (kein separater Coverage-Lauf; die neuen Dateien sind vollständig
durch die 27 neuen Tests abgedeckt — alle öffentlichen Methoden von `ProgramGuideService`,
`ProgramGuideController` und beide DTOs).

## Fehlende Tests

Keine — alle neuen Quelldateien haben korrespondierende Tests.
