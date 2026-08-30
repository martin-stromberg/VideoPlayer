# Tests und Absicherung

## Bestehende Testbasis

- `VideoWebPlayer.Tests/ContinueWatchingE2ETests.cs` prueft Abschlussverhalten, Folgemedien und das Ausbleiben eines Continue-Watching-Eintrags am Serienende.
- `VideoWebPlayer.Tests/Services/ContinueWatchingContextMenuActionTests.cs` prueft benutzerbezogenes Entfernen und Ersetzen von Continue-Watching-Eintraegen.
- `VideoWebPlayer.Tests/Services/ContinueWatchingServiceGetNextEpisodeTests.cs` prueft die Episodenfolge.
- `VideoWebPlayer.Tests/Services/ContinueWatchingServiceSignalRTests.cs` prueft Aktualisierungsbenachrichtigungen.
- `VideoWebPlayer.Tests/ApplicationDbContextTests.cs` prueft Persistenz und Loeschung bestehender Continue-Watching-Daten.
- `VideoWebPlayer.Tests/MediaBoxContextMenuInteractionE2ETests.cs` und `MediaBoxContextMenuPositionE2ETests.cs` pruefen gerenderte Titelkarten im Browser.

## Erforderliche neue Abdeckung

1. Persistenz eines Gesehen-Zeitpunkts fuer einen Film.
2. Persistenz eines Gesehen-Zeitpunkts fuer eine Episode.
3. Wiederholte Endschwellen-Requests bleiben idempotent und veraendern den Status fachlich korrekt.
4. Der konfigurierte `ContinueWatchingEndThresholdSeconds`-Wert steuert die Markierung.
5. Benutzer A sieht den Status von Benutzer A, aber nicht den von Benutzer B.
6. Quelleninhalt zeigt das Auge fuer gesehene Filme und Episoden und nicht fuer ungesehene Titel.
7. Startseitenlisten zeigen dieselbe Kennzeichnung.
8. Das Symbol bleibt in Desktop- und Mobile-Ansichten in der rechten oberen Ecke sichtbar, ohne Titel oder Klickziel unbrauchbar zu machen.
9. Bestehende Continue-Watching-Abschlussfaelle fuer Folgemedien und Serienende bleiben erhalten.

## Testschwerpunkte

Die Endschwellenlogik sollte bevorzugt als Service-/Integrationstest mit einer kontrollierten Setup-Konfiguration abgesichert werden. Die sichtbare Kennzeichnung ist ein Benutzerfluss und benoetigt zusaetzlich einen Browser-/E2E-Nachweis fuer Quelleninhalt und Startseite; reine Service- oder DTO-Tests reichen dafuer nicht aus.
