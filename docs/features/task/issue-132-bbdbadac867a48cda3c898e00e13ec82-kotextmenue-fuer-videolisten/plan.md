# Umsetzungsplan: Kontextmenue fuer Videolisten

## Ziel und Ergebnis

Eintraege in „Weiterschauen“ und „Favoriten“ erhalten ein per dreisekuendigem Gedrueckthalten erreichbares Kontextmenue. „Neu im Programm“ bleibt unveraendert. Die normalen Links, Fokuszustaende, horizontale Scrollfunktion und Wiedergabe bleiben erhalten.

## Verbindliche Entscheidungen

- Das Menue wird als kleines eigenes Blazor-Overlay in `MediaBox.razor` umgesetzt. Die gemeinsame Komponente bekommt einen expliziten Aktivierungsmodus beziehungsweise Aktionsparameter; ohne diesen Parameter gibt es weder Long-Press-Handler noch Menue.
- Der Long-Press startet auf dem einzelnen Kartenelement mit einer Haltezeit von exakt 3 Sekunden. Pointer-Up, Pointer-Cancel, Pointer-Leave und eine Bewegung oberhalb eines kleinen Toleranzradius (10 px) brechen den Timer ab. Die Bewegung gilt als Scrollabsicht; sie darf weder ein Menue oeffnen noch die normale Navigation stoeren.
- Nach dem Oeffnen wird die Link-Navigation fuer diese Interaktion unterdrueckt. Escape, Klick ausserhalb, Auswahl einer Aktion und erneutes Pointer-Abbrechen schliessen das Menue. Die Aktionen werden zusaetzlich per Tastatur erreichbar gemacht, damit die Kartenbedienung nicht ausschliesslich Touch voraussetzt.
- Das Overlay wird innerhalb der Karte positioniert und an Viewport-/Listenraendern korrigiert. Es verwendet die bestehenden UI- und Fokuskonventionen, erzeugt keine Layoutverschiebung und bleibt auf Desktop sowie mobilen Breakpoints bedienbar.
- Nach einer erfolgreichen Aktion wird die betroffene Liste neu geladen; die Mutation wird serverseitig persistiert und die bestehende Benachrichtigungsinfrastruktur wird verwendet. Der StatusTicker zeigt Erfolg oder Fehler an, damit auch ein unveraendertes Ergebnis (z. B. keine Folge) nachvollziehbar ist.
- „Ueberspringen“ gilt fuer Episoden und Filme in „Weiterschauen“. Bei Episoden wird die bestehende `GetNextEpisodeAsync`-Regel wiederverwendet, bei Filmen die vorhandene `GetNextMovieAsync`-Regel. Gibt es kein Folgemedium, wird der Eintrag entfernt.
- Die Listenposition von Continue-Watching wird dauerhaft ueber ein neues Ordnungsfeld am `ContinueWatchingEntry` repraesentiert. Eine Migration initialisiert bestehende Eintraege in der bisherigen `UpdatedAt`-Reihenfolge. Beim Ersetzen wird die Ordnungsposition des alten Eintrags uebernommen; normale Wiedergabe-Updates behalten das bestehende Sortierverhalten, sofern keine fachliche Positionsaenderung erforderlich ist.

## Umsetzungsschritte

### 1. Continue-Watching-Datenmodell und Service

- `ContinueWatchingEntry` um ein benutzerbezogenes Ordnungsfeld erweitern und in `ContinueWatchingEntryConfiguration` passend indizieren.
- EF-Core-Migration mit Rueckwaertskompatibilitaet und deterministischer Initialbelegung der vorhandenen Reihenfolge erstellen.
- `GetListAsync` auf die neue Reihenfolge umstellen und dabei einen stabilen Tie-Breaker verwenden.
- Oeffentliche Serviceoperationen fuer `Hide` und `Skip` ergaenzen. Beide laden den Eintrag ausschliesslich ueber den authentifizierten Benutzer.
- `Skip` atomar ausfuehren: alten Eintrag lesen, naechstes Medium ueber die bestehenden privaten Regeln ermitteln, Ersatz mit gleicher Ordnungsposition anlegen oder bei `null` nur loeschen, speichern und genau eine Continue-Watching-Benachrichtigung ausloesen.
- Bestehendes Abschlussverhalten von `ProcessBufferedEntryAsync` unveraendert lassen; die neue Positionslogik darf den dokumentierten normalen Abschluss nicht ungewollt aendern.

### 2. API und Client

- `ContinueWatchingController` um authentifizierte Endpunkte fuer Ausblenden und Ueberspringen erweitern. Requestmodelle enthalten nur Medientyp und Medien-ID; die Benutzer-ID kommt ausschliesslich aus dem Authentifizierungskontext.
- Fehlerfaelle wie ungueltige IDs, falschen Medientyp und nicht gefundenen eigenen Eintrag mit klaren HTTP-Statuscodes behandeln.
- Im `VideoWebPlayerClient` vorhandene Favoriten-Remove-Kapselung pruefen und verwenden; falls sie fehlt, eine typisierte Methode fuer `/api/favorites/remove` ergaenzen. Keine direkten HTTP-Aufrufe aus den Razor-Komponenten duplizieren.

### 3. Karten- und Listen-UI

- `MediaBox.razor` um optionalen Long-Press-Handler, Aktionsliste, Menuezustand und Tastatur-/Escape-Behandlung erweitern. Fuer die Aktivierung werden eindeutige Karten-Schluessel und `EventCallback`s genutzt.
- Timer und Pointer-Tracking sauber entsorgen beziehungsweise abbrechen, wenn die Komponente neu gerendert oder entfernt wird. Der normale Link bleibt der Standardpfad.
- `ContinueWatchingList.razor` aktiviert die Aktionen „Ausblenden“ und „Ueberspringen“, leitet die typisierten IDs aus dem DTO ab und laedt die Liste nach Erfolg neu.
- `FavoritesList.razor` aktiviert nur „Entfernen“ und uebergibt die Persistenz-ID `fav.Id`, unabhaengig davon, ob der Favorit Film, Sammlung, Serie, Staffel oder Episode ist.
- `RecentEntriesList.razor` unveraendert ohne Aktivierungsparameter rendern. Sicherstellen, dass die gemeinsame `MediaBox` dadurch keinen Long-Press-Handler registriert.
- `app.css` um Overlay-, Fokus-, Status- und Randpositionierungsregeln ergaenzen. Kartenmasse und horizontales Grid duerfen sich nicht verschieben; auf kleinen Viewports darf das Menue nicht abgeschnitten werden.

### 4. Aktualisierung und Rueckmeldung

- Bestehende SignalR-Notifications fuer Continue-Watching und Favoriten in die vorhandene Home-Aktualisierungslogik einbinden oder, falls dort nicht vorhanden, nach erfolgreicher Aktion einen gezielten Reload ausloesen.
- Menue nach erfolgreicher Mutation schliessen, lokale Fehler anzeigen und bei fehlgeschlagenem Reload keinen falschen lokalen Zustand stehen lassen.
- Doppelte Reloads durch lokale Aktualisierung und SignalR-Ereignis vermeiden.

### 5. Tests und Abnahme

- Service-/Integrationstests fuer Ausblenden, Ueberspringen, fehlendes Folgemedium, Positionsuebernahme, Benutzerisolierung und Film-/Episodenpfade ergaenzen.
- Favoriten-Remove fuer alle fuenf unterstuetzten Favoritentypen testen.
- Komponenten- oder Browser-Tests fuer exakt 3 Sekunden Haltezeit, vorzeitiges Loslassen, Pointer-Cancel, Bewegung/Scrollen, Menueaktionen, Escape und normale Link-Navigation erstellen.
- Sicherstellen, dass „Neu im Programm“ kein Menue rendert.
- Responsive Abnahme bei Desktop- und mobilem Viewport sowie Overlayposition am Listenrand durchfuehren.
- Bestehende Continue-Watching-, SignalR- und E2E-Tests ausfuehren und bei Regressionen zuerst die gemeinsame Kartenkomponente und die Reload-Strategie pruefen.

## Betroffene Dateien und Bereiche

- `VideoWebPlayer/Components/Shared/Media/MediaBox.razor`
- `VideoWebPlayer/Components/Shared/Home/ContinueWatchingList.razor`
- `VideoWebPlayer/Components/Shared/Home/FavoritesList.razor`
- `VideoWebPlayer/Components/Shared/Home/RecentEntriesList.razor` (nur Verifikation)
- `VideoWebPlayer/wwwroot/app.css`
- `VideoWebPlayer/Controllers/ContinueWatchingController.cs`
- `VideoWebPlayer/Controllers/FavoritesController.cs` beziehungsweise Client-Kapselung
- `VideoWebPlayer/Services/ContinueWatchingService.cs`
- `VideoWebPlayer/Services/FavoritesService.cs` beziehungsweise `IFavoritesService`
- `VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs`
- `VideoWebPlayer/Data/Configurations/ContinueWatchingEntryConfiguration.cs`
- neue EF-Migration sowie relevante Dateien unter `VideoWebPlayer.Tests/`

## Akzeptanzpruefung

- [ ] 3-Sekunden-Halten in „Weiterschauen“ oeffnet das Menue mit „Ausblenden“ und „Ueberspringen“.
- [ ] Ausblenden entfernt den eigenen Eintrag dauerhaft.
- [ ] Ueberspringen ersetzt Episode beziehungsweise Film an derselben Position; ohne Folgemedium wird entfernt.
- [ ] 3-Sekunden-Halten in „Favoriten“ oeffnet nur „Entfernen“ und entfernt alle unterstuetzten Favoritentypen.
- [ ] „Neu im Programm“ oeffnet kein Menue.
- [ ] Scrollen, normale Navigation, Wiedergabe, Fokusbedienung und SignalR-Aktualisierung bleiben funktionsfaehig.
- [ ] Menue und Rueckmeldung sind auf den unterstuetzten Bildschirmgroessen sichtbar und ohne unbedienbare Ueberlappung.

## Offene Punkte

Keine. Die UI-Komponente, Abbruchschwelle, Rueckmeldung, Filmbehandlung, Persistenz der Position und Tastaturalternative sind oben verbindlich festgelegt.
