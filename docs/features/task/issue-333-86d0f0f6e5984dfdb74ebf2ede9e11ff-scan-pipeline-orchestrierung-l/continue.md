# Offene Aufgaben

Erstellt am: 2026-11-03
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (jeweils 4 offene Punkte)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` meldet Status „Vollständig umgesetzt".

## Code-Review-Befunde

- [ ] `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`: Die private Methode `BuildScanRunServices` (ca. Zeilen 346–378) baut den Service-Provider praktisch identisch zu `MediaSourceScanCoordinatorTestServices.Build` (`VideoWebPlayer.Tests/Helpers/MediaSourceScanCoordinatorTestServices.cs`, Zeilen 32–86) erneut auf. Empfehlung: `MediaSourceScanCoordinatorTestServices.Build` um optionale Parameter für `TimeProvider` und `IMediaSourceReader` erweitern (Default: `TimeProvider.System` bzw. `LocalMediaSourceReader`) und `BuildScanRunServices` durch einen Aufruf dieser Methode ersetzen.
- [ ] `VideoWebPlayer/Services/MediaSourceScanService.cs`, Zeile 100: `await coordinator.RunFullScanAsync("Automatischer Scanprozess", 64, stoppingToken);` verwendet die frühere benannte Konstante `maxCollectionsPerRun = 64` jetzt als unbenannte Magic Number. Empfehlung: Obergrenze wieder als benannte Konstante führen und an der Aufrufstelle verwenden.
- [ ] `VideoWebPlayer.Tests/Services/MediaSourceScanCoordinatorScanTests.cs`: `RunFullScanAsync_RespectsCap_WhenProvided` (Zeilen 100–137) prüft zwei unabhängige fachliche Fälle (gedeckelt/ungedeckelt) in einem Testfall. Empfehlung: in zwei Testmethoden aufteilen, z. B. `RunFullScanAsync_RespectsCap_WhenProvided` (nur gedeckelt) und `RunFullScanAsync_ScansAllCollections_WhenCapNotProvided` (nur ungedeckelt).
- [ ] `VideoWebPlayer/Services/MediaSourceScanCoordinator.cs`: `ClassifyPendingCollectionsAsync` (Zeilen 165–179) protokolliert zwei inhaltlich überlappende Meldungen direkt hintereinander ("Klassifizierung der Collections abgeschlossen." / "Klassifizierung abgeschlossen."). Empfehlung: eine der beiden Logzeilen entfernen bzw. zusammenführen.

## Fehlgeschlagene Tests

Keine — der letzte Testlauf (`test-results.md`) meldet Status „Keine Fehler" (2901 gesamt, 2899 bestanden, 0 fehlgeschlagen, 2 übersprungen).

## Hinweis

`test-results.md` weist zusätzlich darauf hin, dass die reale End-to-End-Abnahme (manuelles Durchspielen der Anforderungs-Szenarien an der laufenden Anwendung) noch nicht erfolgt ist — das ist kein automatisiert zählbarer Punkt, sollte aber bei der Abnahme berücksichtigt werden.
