# Bestandsaufnahme: Kennzeichen fuer bereits gesehene Titel

## Ausgangslage

Die Anwendung ist eine ASP.NET-Core-/Blazor-Anwendung. Filme und Episoden werden als getrennte Entitaeten modelliert, teilen sich aber `MediaBaseEntry` fuer gemeinsame Metadaten. Die Wiedergabe speichert bereits benutzerbezogene Continue-Watching-Fortschritte. Ein Gesehen-Status mit eigenem Zeitpunkt ist im aktuellen Modell nicht vorhanden.

## Relevante Bereiche

| Bereich | Bestehende Anknuepfung | Relevanz |
|---|---|---|
| Persistenz | `ApplicationDbContext`, `ContinueWatchingEntry`, EF-Konfigurationen und Migrationen | Benutzer-/Titel-Schluessel, Zeitpunkt und Migration muessen erweitert oder getrennt modelliert werden. |
| Wiedergabe | `VideoPlayer.razor`, `continueWatching.js`, `ContinueWatchingController`, `ContinueWatchingService` | Endschwelle wird bereits ausgewertet; dort muss das Setzen des Gesehen-Zeitpunkts integriert werden. |
| Titelkarten | `MediaBox.razor` und zugehoerige CSS-Dateien | Zentrale Stelle fuer das Auge-Symbol in der rechten oberen Ecke. |
| Quellenlisten | `MediaBaseEntryList.razor`, `MediaSourceDetails.razor` | Filme/Episoden werden als `MediaBaseEntry` an gemeinsame Listen gerendert. |
| Startseite | `Home.razor` sowie `FavoritesList`, `RecentEntriesList`, `SeasonalGenreList`, `ContinueWatchingList` | Mehrere Listen verwenden `MediaBox`; Statusdaten muessen fuer alle betroffenen Titel bereitstehen. |
| API/DTOs | Controller, `VideoWebPlayer.Client.Models`, `VideoWebPlayerClient` | Status kann serverseitig am aktuellen Benutzer ermittelt und an die UI geliefert werden. |
| Tests | `VideoWebPlayer.Tests` mit Continue-Watching-Service- und E2E-Tests | Bestehende Endschwellen-/Benutzerisolations-Tests muessen um den neuen Status erweitert werden. |

## Detailinventar

- [Datenmodell und Persistenz](inventory/data-persistence.md)
- [Wiedergabe und Endschwelle](inventory/playback-threshold.md)
- [Listen und Anzeige](inventory/lists-and-display.md)
- [Tests und Absicherung](inventory/tests.md)

## Festgestellte Luecken

1. `ContinueWatchingEntry` enthaelt `UserId`, Movie-/Episode-Referenz, Position, Dauer und `UpdatedAt`, aber keinen Gesehen-Zeitpunkt.
2. `ContinueWatchingService.ProcessBufferedEntryAsync` entfernt bei `duration - position <= endThreshold` den Continue-Watching-Eintrag und legt optional das naechste Medium an. Ein Gesehen-Status wird dabei nicht gespeichert.
3. `continueWatching.js` sendet Fortschritt bei `timeupdate`, `pause` und `ended`; die serverseitige Endschwelle ist der fachliche Ort fuer die Markierung.
4. `MediaBox` rendert aktuell Poster, Schnellstart und Titeltext, aber kein Status-Overlay.
5. Die vorhandenen DTOs fuer Titel enthalten nach aktueller Sichtung kein Feld fuer den Gesehen-Status. Die Datenbeschaffung fuer Quellen- und Startseitenlisten muss deshalb angepasst oder um eine zentrale Statusanreicherung ergaenzt werden.
6. Das vorhandene Auge-Bild `Images/gesehen64x64.png` kann als bestehendes Asset geprueft und wiederverwendet werden; eine konkrete Verwendung im UI wurde nicht gefunden.

## Randbedingungen und Risiken

- Der Status muss strikt ueber `UserId` und Titel-ID isoliert werden; ein Feld am globalen Film-/Episodenobjekt waere fachlich falsch.
- Movie und Episode haben unterschiedliche Fremdschluessel. Die Modellierung muss trotzdem eine eindeutige Kombination aus Benutzer und genau einem Titel erlauben.
- Das Entfernen von Continue-Watching bei Erreichen der Schwelle darf die neue Gesehen-Markierung nicht verhindern.
- Die Listen enthalten auch Sammlungen, Serien und Staffeln. Der neue Status ist laut Anforderung fuer Filme und Episoden relevant; die Darstellung anderer Typen muss unveraendert bleiben.
- Bestehende E2E-Tests decken Wiedergabeabschluss und benutzerbezogene Continue-Watching-Daten ab, aber noch nicht das Auge-Symbol oder den persistierten Gesehen-Zeitpunkt.

## Abweichung vom Lifecycle-Ablauf

In dieser Codex-Umgebung war kein Unteragent mit dem im Lifecycle genannten Modellaufruf verfuegbar. Die Bestandsaufnahme wurde deshalb direkt anhand des Repository-Inhalts erstellt.
