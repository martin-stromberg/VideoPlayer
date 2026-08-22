# Bestandsaufnahme: Kontextmenü für Videolisten

## Zusammenfassung

Die Anforderung betrifft die Blazor-Webanwendung `VideoWebPlayer` und deren Home-Listen. Die drei fachlich relevanten Listen sind getrennt implementiert:

- `ContinueWatchingList.razor` rendert „Weiterschauen“ aus `GET /api/continue-watching`.
- `FavoritesList.razor` lädt Favoriten über `VideoWebPlayerClient.RequestFavoritesAsync()`.
- `RecentEntriesList.razor` rendert „Neu im Programm“.

Die Karten werden aktuell über die gemeinsame Komponente `Components/Shared/Media/MediaBox.razor` als einfacher Link ausgegeben. Eine Halteinteraktion oder ein Kontextmenü ist dort noch nicht vorhanden. Die gewünschte Funktion sollte deshalb listenbezogen gesteuert werden, damit „Neu im Programm“ unverändert ohne Kontextmenü bleibt.

## Relevante Detaildokumente

- [UI-Oberflächen und Interaktionspunkte](inventory/ui-surfaces-and-interaction.md)
- [API, Services und Persistenz](inventory/api-services-and-persistence.md)
- [Weiterblättern und Positionsstabilität](inventory/continue-watching-domain.md)
- [Risiken, Testbasis und offene Entscheidungen](inventory/risks-tests-and-questions.md)

## Betroffene Architekturgrenzen

| Bereich | Bestand | Bedeutung für die Umsetzung |
|---|---|---|
| Home-UI | `VideoWebPlayer/Components/Shared/Home/` | Kontextmenü wird nur in zwei Listen aktiviert. |
| Kartenkomponente | `VideoWebPlayer/Components/Shared/Media/MediaBox.razor` | Link-, Fokus- und Overlay-Verhalten dürfen durch Long-Press nicht verloren gehen. |
| Continue Watching | `ContinueWatchingService`, `ContinueWatchingController` | Bestehende automatische Next-Episode-Logik ist intern und sortiert nach `UpdatedAt`. |
| Favoriten | `FavoritesService`, `FavoritesController` | Entfernen ist als Service-/API-Operation bereits vorhanden. |
| Echtzeitaktualisierung | `MediaUpdateNotificationService` und SignalR | Listen müssen nach Mutationen zuverlässig neu geladen oder aktualisiert werden. |
| Styling | `VideoWebPlayer/wwwroot/app.css` | Horizontale Listen, Karten, Fokuszustände und mobile Breakpoints sind bereits zentral geregelt. |
| Tests | `VideoWebPlayer.Tests/` | Continue-Watching-Service- und E2E-Tests existieren; UI-/Long-Press-Tests sind nicht erkennbar. |

## Aus dem Bestand ableitbare Leitplanken

1. Die Haltezeit muss auf dem einzelnen Kartenelement gestartet und bei Pointer-/Touch-Abbruch vor Ablauf abgebrochen werden. Scrollbewegungen dürfen nicht versehentlich ein Menü öffnen.
2. Für „Neu im Programm“ darf weder `MediaBox` global noch die umgebende Liste mit einem Long-Press-Handler versehen werden.
3. Mutationen müssen benutzerbezogen autorisiert werden und die bestehende SignalR-/Reload-Logik berücksichtigen.
4. „Überspringen“ muss die vorhandene Episode-Reihenfolge wiederverwenden, aber die resultierende Continue-Watching-Position gezielt an der ursprünglichen Listenposition halten. Eine reine Aktualisierung der `UpdatedAt`-Sortierung erfüllt dieses Kriterium nicht.
5. Die reguläre Navigation muss als Fallback erhalten bleiben; Menüaktionen sollten den Link nicht auslösen.

## Erfassungsstand

Die Bestandsaufnahme basiert auf dem aktuellen Branch und den Dateien unter `VideoWebPlayer/`, `VideoWebPlayer.Tests/` sowie der vorhandenen technischen Dokumentation zu „Weiterschauen“. Die konkreten UI-Komponenten, die Behandlung von Scroll-Abbruch und die Rückmeldung nach Aktionen sind in der Anforderung noch offen und müssen in der Planung entschieden werden.
