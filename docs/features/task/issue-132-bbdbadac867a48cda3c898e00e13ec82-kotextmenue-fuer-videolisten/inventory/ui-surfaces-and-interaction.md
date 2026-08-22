# UI-Oberflächen und Interaktionspunkte

## Listen

### Weiterschauen

`VideoWebPlayer/Components/Shared/Home/ContinueWatchingList.razor` lädt eine `List<ContinueWatchingDto>` aus `api/continue-watching`, baut Titel, Bild- und Detail-URLs und rendert je Eintrag eine `MediaBox`. Die Liste nutzt die CSS-Klasse `continue-watching-list` und erhält nach dem ersten Render eine horizontale Wheel-Scroll-Unterstützung über `enableHorizontalWheelScroll`.

Die Komponente kennt aktuell weder einen stabilen Schlüssel am gerenderten Eintrag noch einen Event-Callback für Aktionen. Für das Ersetzen an derselben Position muss die View-Model-Liste entweder gezielt per Index aktualisiert oder nach der Serverantwort in derselben Reihenfolge neu geladen werden.

### Favoriten

`VideoWebPlayer/Components/Shared/Home/FavoritesList.razor` lädt `DtoFavoriteEntry`-Objekte und rendert ebenfalls `MediaBox`. Die dargestellten Typen können Film, Filmsammlung, Serie, Staffel oder Episode sein. Die Favoriten-ID (`fav.Id`) ist der Persistenzdatensatz; die eigentliche Medien-ID liegt typabhängig im `FavoriteEntry`/`DtoMediaEntry`.

Das Menü muss daher eine typunabhängige Remove-Anforderung unterstützen, ohne aus dem Anzeige-Link auf den Medientyp schließen zu müssen.

### Neu im Programm

`VideoWebPlayer/Components/Shared/Home/RecentEntriesList.razor` rendert die Liste mit der Überschrift „Neu im Programm“ und mehreren DTO-Typen. Diese Komponente darf keinen Long-Press-Handler und kein Kontextmenü erhalten. Die gemeinsame `MediaBox` darf deshalb nur mit einem expliziten Aktivierungsparameter für die beiden erlaubten Listen erweitert werden.

## Gemeinsame Kartenkomponente

`VideoWebPlayer/Components/Shared/Media/MediaBox.razor` besteht derzeit aus einem `<a>` mit Poster, Quick-Play-Zeichen und Titel-Overlay. Es gibt keinen Button, keinen `@on...`-Handler und keinen eigenen Kontextmenü-Zustand. Globale CSS-Regeln in `VideoWebPlayer/wwwroot/app.css` definieren Hover-, Fokus- und Kartenmaße; die Continue-Watching-Karten verwenden 16:9, übrige Karten standardmäßig 2:3.

Bei einer Erweiterung sind folgende Zustände zu erhalten oder explizit zu ergänzen:

- normale Link-Navigation per Klick/Tastatur
- Fokus sichtbar über `:focus-visible`
- Long-Press-Fortschritt ohne Layoutverschiebung
- offenes Menü innerhalb des sichtbaren Viewports bzw. mit korrigierter Position am Rand
- Abbruch bei Pointer-Up, Pointer-Cancel, Pointer-Leave oder Scrollbewegung
- kein Menü in „Neu im Programm“

## Bestehende Layoutkonventionen

Die Listen sind horizontale CSS-Grids mit festen `grid-auto-columns`; auf mobilen Viewports wird die Kartenspalte über einen Breakpoint breiter gesetzt. Das Kontextmenü darf den horizontalen Scrollbereich nicht unbedienbar machen und darf keine Inhalte außerhalb eines kleinen, klar abgegrenzten Overlays überdecken.
