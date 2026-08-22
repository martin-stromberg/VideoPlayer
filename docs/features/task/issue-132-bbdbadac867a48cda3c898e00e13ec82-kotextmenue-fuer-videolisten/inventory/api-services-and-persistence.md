# API, Services und Persistenz

## Continue Watching

`VideoWebPlayer/Controllers/ContinueWatchingController.cs` stellt derzeit bereit:

- `GET /api/continue-watching` für die aktuelle Benutzerliste
- `POST /api/continue-watching/progress` für Wiedergabefortschritt

Beide Endpunkte sind über `BearerTokenCheck` geschützt. Es gibt keinen Endpunkt zum Ausblenden, Löschen oder manuellen Überspringen eines Eintrags.

`ContinueWatchingService` arbeitet mit `ContinueWatchingEntry` und identifiziert Einträge über Benutzer-ID plus `MovieId` oder `TVShowEpisodeId`. Die Liste wird in `GetListAsync()` nach `UpdatedAt` absteigend auf maximal 50 Einträge begrenzt.

## Favoriten

`VideoWebPlayer/Controllers/FavoritesController.cs` stellt bereits `POST /api/favorites/remove` bereit. `FavoritesService.RemoveFavoriteAsync()` sucht den Datensatz benutzerbezogen und vergleicht die jeweils gesetzte typabhängige Medien-ID. Nach erfolgreicher Löschung wird `NotifyFavoritesChangedAsync()` ausgelöst.

Die UI verwendet derzeit den vorhandenen Client für das Laden. Für das Menü muss geprüft werden, ob der Client bereits eine Remove-Methode kapselt; andernfalls sollte diese Kapselung ergänzt werden, statt HTTP-Aufrufe in der Razor-Komponente zu duplizieren.

## Datenmodell

`FavoriteEntry` kann als Ziel Filmsammlung, Serie, Staffel, Episode oder Film referenzieren. Das Menü „Entfernen“ muss alle in der Favoritenliste dargestellten Typen abdecken.

`ContinueWatchingEntry` enthält keine explizite Listenposition. Die aktuelle Reihenfolge ist fachlich eine absteigende Aktualisierungsreihenfolge (`UpdatedAt`). Für die neue Aktion „Überspringen“ reicht deshalb ein simples Entfernen und Einfügen nicht aus, wenn die ursprüngliche Position garantiert stabil bleiben soll.

## SignalR und Aktualisierung

`FavoritesService` und `ContinueWatchingService` verwenden `MediaUpdateNotificationService`, um Listenänderungen an den Benutzer zu signalisieren. Die Umsetzung muss festlegen, ob die Home-Komponente auf diese Benachrichtigung hört oder nach einer Menüaktion gezielt neu lädt. Wichtig ist, dass eine erfolgreiche Aktion nicht nur lokal aus dem DOM entfernt wird, sondern der persistierte Zustand und andere offene Ansichten konsistent bleiben.

## Sicherheitsgrenzen

Alle Mutationsendpunkte müssen den aktuell authentifizierten Benutzer aus dem Authentifizierungskontext verwenden. IDs aus dem Browser dürfen nie ausreichen, um fremde Continue-Watching- oder Favoriten-Datensätze zu verändern.
