# Listen und Anzeige

## Zentrale Darstellung

`VideoWebPlayer/Components/Shared/Media/MediaBox.razor` ist die gemeinsame Titelkarte. Sie rendert Poster, Titel und Schnellstart innerhalb von `.media-box`. Ein Statusindikator kann hier einheitlich in die obere rechte Ecke integriert werden, sofern die Komponente ein Statussignal als Parameter erhaelt.

## Betroffene Listen

- `MediaBaseEntryList.razor` rendert eine Liste von `MediaBaseEntry`-Objekten und wird fuer gemeinsame Titelauflistungen verwendet.
- `MediaSourceDetails.razor` ist der zentrale Quelleninhalt und verwendet die Listenkomponente bzw. deren Titelobjekte.
- `Home.razor` bindet die Startseitenlisten ein.
- `SeasonalGenreList.razor` rendert `MediaBox` direkt aus `MediaBaseEntry`.
- `FavoritesList.razor` und `RecentEntriesList.razor` bauen Titel-, Bild- und Linkdaten aus Client-DTOs und rendern anschliessend `MediaBox`.
- `ContinueWatchingList.razor` verwendet `ContinueWatchingDto` und rendert ebenfalls `MediaBox`.

## Datenfluss-Risiko

Nicht alle Listen verwenden denselben DTO-Typ oder dieselbe Datenquelle. Eine reine Erweiterung einer einzelnen Quellen-DTO-Klasse deckt die Startseite daher nicht automatisch ab. Zu klaeren ist, ob der Status zentral in der Serverantwort fuer alle Titelkarten angereichert wird oder ob jede Liste eine benutzerbezogene Statusabfrage benoetigt. Die zweite Variante wuerde zusaetzliche Requests und Synchronisationsprobleme erzeugen.

## Vorhandenes Asset und Styling

Im Repository existiert `Images/gesehen64x64.png`. Die CSS-Regeln fuer `.media-box`, `.media-poster` und Overlays liegen unter `VideoWebPlayer/wwwroot/css/` und muessen auf Positionierung, Kontrast, Skalierung und mobile Darstellung des Symbols geprueft werden. Das Symbol darf Titel und Interaktionsflaeche nicht verdecken.

## Abgrenzung

Die Anforderung nennt Filme und Episoden. Serien, Staffeln und Filmsammlungen werden in einigen Listen ebenfalls angezeigt, sollen aber nicht versehentlich als gesehen markiert oder mit einem Status versehen werden, sofern keine fachliche Zuordnung definiert ist.
