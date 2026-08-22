# Risiken, Testbasis und offene Entscheidungen

## Risiken

| Risiko | Auswirkung | Prüfansatz |
|---|---|---|
| Long-Press auf horizontal scrollenden Karten | Scrollen öffnet versehentlich ein Menü oder Navigation wird ausgelöst | Pointer-/Touch-Tests mit Bewegung und Abbruch vor 3 Sekunden |
| Globale Änderung an `MediaBox` | „Neu im Programm“ erhält entgegen der Anforderung ein Menü | Render-/Komponententest je Liste |
| Continue-Watching-Reihenfolge nur über `UpdatedAt` | Überspringen verschiebt den Ersatz an den Listenanfang | Service-/Integrationstest mit mehreren Einträgen |
| Favoriten referenzieren verschiedene Medientypen | Entfernen funktioniert nur für einzelne Kartenarten | Tests für Film, Sammlung, Serie, Staffel und Episode |
| SignalR und lokale Aktualisierung | Anzeige kann veraltet oder doppelt aktualisiert werden | Test der Notification- und Reload-Strategie |
| Overlay am Rand oder auf Mobile | Menü wird abgeschnitten oder überlappt unbedienbar | Desktop- und Mobile-Viewport-Abnahme |
| Fehlende UI-Testinfrastruktur | Long-Press-Regressionsschutz fehlt | Vorhandene Testprojekte prüfen, ggf. gezielte Browsertests ergänzen |

## Vorhandene Tests

Im Projekt existieren insbesondere:

- `VideoWebPlayer.Tests/Services/ContinueWatchingServiceGetNextEpisodeTests.cs`
- `VideoWebPlayer.Tests/Services/ContinueWatchingServiceSignalRTests.cs`
- `VideoWebPlayer.Tests/ContinueWatchingE2ETests.cs`

Die Testbasis deckt Continue-Watching-Domänenlogik und E2E-Pfade ab. Dedizierte Tests für Razor-Long-Press, Kontextmenüaktionen oder die Favoritenliste sind im Bestand nicht erkennbar. Die neue Funktion braucht daher mindestens Service-/Controller-Tests und einen UI-Test bzw. manuellen Abnahmeschritt für die Halteinteraktion.

## Offene Entscheidungen aus Anforderung und Bestand

1. Welche konkrete Menükomponente wird verwendet: eigenes Blazor-Overlay, Bootstrap-Dropdown/Popover oder eine bestehende Komponente?
2. Wird Scroll-/Pointer-Bewegung mit einem Toleranzradius behandelt, und wann genau wird die Haltezeit abgebrochen?
3. Soll die Rückmeldung als Toast, StatusTicker oder ausschließlich durch die aktualisierte Liste erfolgen?
4. Gilt „Überspringen“ für Film-Einträge in „Weiterschauen“ oder ausschließlich für Episoden?
5. Muss die Ersatzposition nur innerhalb des aktuellen UI-Renders stabil bleiben oder auch nach API-Neuladen, SignalR-Update und Seitenwechsel?
6. Ist eine Änderung am Datenmodell für eine persistente Listenposition zulässig?
7. Welche Tastaturalternative ist neben Long-Press erforderlich, damit die Aktionen auch ohne Touch bedienbar sind?

## Nicht im Scope

`RecentEntriesList.razor` erhält kein Kontextmenü. Die regulären Klick-, Navigations- und Wiedergabeabläufe aller Listen sollen unverändert erhalten bleiben.
