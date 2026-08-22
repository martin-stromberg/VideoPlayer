# Plan-Review: Kontextmenü für Videolisten

## Status

Offene Aufgaben vorhanden

## Prüfung

Der aktuelle Workspace wurde gegen `plan.md`, `inventory.md` und die Detaildokumente geprüft. Die erwarteten Änderungen für Datenmodell, Migration, Continue-Watching-Service, API, Favoriten-Service, `MediaBox` sowie die beiden Home-Listen sind vorhanden. `RecentEntriesList.razor` verwendet weiterhin keinen Aktivierungsparameter und erhält damit kein Kontextmenü.

Der Testlauf `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore` war erfolgreich: 174 Tests bestanden, 0 fehlgeschlagen. Die neuen Tests decken Servicepfade ab, aber nicht die Browser-/Komponenteninteraktion.

## Offene Aufgaben

1. **Tastaturfokus des Kontextmenüs vervollständigen**  
   `MediaBox.razor` öffnet das Menü bei `ContextMenu` beziehungsweise `Shift+F10`, verschiebt den Fokus danach aber nicht auf den Menücontainer oder den ersten Aktionsbutton (`MediaBox.razor:12-20, 124-136`). Der Fokus bleibt auf dem Link; die Aktionsbuttons sind damit nicht zuverlässig per Tab und Enter/Space erreichbar. Nach dem Öffnen muss der Fokus gezielt auf die erste Aktion gesetzt und beim Schließen sinnvoll zum Kartenlink zurückgeführt werden. Escape muss auch funktionieren, wenn ein Aktionsbutton fokussiert ist.

2. **Fehler beim Reload nicht als leere Continue-Watching-Liste darstellen**  
   `VideoWebPlayerClient.RequestContinueWatchingAsync()` fängt alle Fehler ab und liefert eine leere Liste (`VideoWebPlayerClient.cs:267-275`). `ContinueWatchingList.LoadItemsAsync()` übernimmt dieses Ergebnis direkt. Wenn die Mutation bereits erfolgreich war, der anschließende Reload aber fehlschlägt, wird dadurch fälschlich eine leere Liste angezeigt und anschließend eine Erfolgsmeldung veröffentlicht. Reload-Fehler müssen sichtbar behandelt werden, ohne den bisher bekannten lokalen Zustand als erfolgreich geladen zu ersetzen.

3. **Overlayposition an Listen- und Viewporträndern nachweisen und korrigieren**  
   Das Overlay ist ausschließlich absolut mit `right: 0.5rem` und `bottom: 0.5rem` innerhalb der Karte positioniert (`app.css:1047-1068`). Eine Messung oder Korrektur für tatsächliche Viewport-/Listenränder ist nicht vorhanden. Die horizontale Listenfläche blendet vertikalen Überstand aus (`app.css:876-886`); deshalb müssen mindestens die erste/letzte Karte sowie schmale mobile Viewports per Browser- oder Komponententest geprüft und bei Bedarf nach oben beziehungsweise links positioniert werden.

4. **UI-Abnahmetests ergänzen**  
   Es fehlen Tests für die verbindlichen Interaktionen: exakt drei Sekunden Haltezeit, vorzeitiges Loslassen, Pointer-Cancel, Bewegung über 10 px, normale Navigation, Escape, Klick außerhalb, Aktionsauswahl, „Neu im Programm“ ohne Menü sowie mobile Randpositionierung. Die vorhandenen neuen Tests (`ContinueWatchingContextMenuActionTests.cs` und `FavoritesServiceContextMenuActionTests.cs`) prüfen nur Service-/Persistenzpfade.

## Umgesetzte Planpunkte

- `ContinueWatchingEntry.ListOrder` inklusive EF-Migration und initialer Belegung ist vorhanden.
- Continue-Watching-Ausblenden und -Überspringen sind benutzerbezogen implementiert; Film- und Episodenpfade sowie die Positionsübernahme sind durch Tests abgedeckt.
- Favoriten-Entfernen verwendet die Persistenz-ID und unterstützt die fünf im Plan genannten Favoritentypen.
- Das Kontextmenü ist nur für Continue Watching und Favoriten aktiviert; die Recent-Liste bleibt unverändert.
- Mutationen lösen die vorhandenen Continue-Watching- beziehungsweise Favoriten-Benachrichtigungen aus und die Listen laden nach erfolgreicher Aktion neu.

## Restliches Risiko

Der erfolgreiche Testlauf belegt keine tatsächliche Pointer-/Touch- oder Fokusinteraktion im Browser. Bis die offenen UI-Punkte geprüft und behoben sind, kann die Anforderung nicht als vollständig umgesetzt bewertet werden.
