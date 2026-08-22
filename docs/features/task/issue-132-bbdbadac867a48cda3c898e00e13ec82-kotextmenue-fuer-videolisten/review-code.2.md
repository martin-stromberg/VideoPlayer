# Code-Review

Status: Befunde vorhanden

## Gepruefter Umfang

- Workspace nach Iteration 2 auf Branch `task/issue-132-bbdbadac867a48cda3c898e00e13ec82-kotextmenue-fuer-videolisten`.
- Diff in `MediaBox`, Home-Listen, Continue-Watching-API/Service, Favoriten-Service, EF-Migration, Client-Kapselung und neuen Tests.
- Fokus: MediaBox Long-Press, Fokus/Escape, Overlay-Randpositionierung, Reload-Fehlerbehandlung, Tests und API/Service-Diff.

## Befunde

1. `VideoWebPlayer/wwwroot/app.css:876` und `VideoWebPlayer/wwwroot/app.css:1058`: Die Overlay-Randpositionierung ist weiterhin nicht dynamisch korrigiert. Horizontale Listen setzen `overflow-y: hidden`, waehrend `.media-context-menu` nur absolut rechts unten in der jeweiligen Karte liegt und per `max-width: calc(100% - 1rem)` / `max-height: calc(100% - 1rem)` in die Karte hineingezwungen wird. Es gibt keine Messung oder Umschaltung fuer erste/letzte Karte, sichtbaren Listenbereich oder mobilen Viewport. Damit ist die Planforderung "an Viewport-/Listenraendern korrigiert" und "auf kleinen Viewports darf das Menue nicht abgeschnitten werden" nicht belastbar umgesetzt; das Menue wird im Zweifel geschrumpft oder intern scrollbar, statt sinnvoll am Rand umzupositionieren.

2. `VideoWebPlayer/Components/Shared/Home/FavoritesList.razor:96`: Reload-Fehler nach einer Favoriten-Mutation werden weiterhin anders behandelt als bei Continue-Watching. `RemoveFavoriteAsync` kann serverseitig erfolgreich sein; wenn anschliessend `LoadFavoritesAsync()` fehlschlaegt, landet der Code im allgemeinen `catch` und zeigt nur `Fehler: ...`, waehrend der alte lokale Favoritenzustand sichtbar bleibt. Anders als `ContinueWatchingList.razor:94` gibt es keinen booleschen Reload-Pfad mit differenzierter Rueckmeldung "Aktion erfolgreich, Reload fehlgeschlagen". Das verletzt die Planforderung, nach erfolgreicher Aktion keinen irrefuehrenden lokalen Zustand beziehungsweise keine unklare Rueckmeldung bei fehlgeschlagenem Reload stehen zu lassen.

3. `VideoWebPlayer/Components/Shared/Media/MediaBox.razor:43`: `MediaBox` registriert Pointer-, ContextMenu-, Click- und Keydown-Handler immer, auch wenn keine Aktionen uebergeben wurden. `RecentEntriesList.razor:133`, `:137`, `:141`, `:145` und `:149` rendert die Komponente ohne Aktionen; trotzdem laufen auf diesen Karten die neuen Handler bis zum `HasActions`-Guard. Der Plan fordert explizit, dass es ohne Aktivierungsparameter weder Long-Press-Handler noch Menue gibt. Funktional oeffnet zwar kein Menue, aber "Neu im Programm" bekommt unnoetige Pointer-/Tastaturereignisse auf der gemeinsamen Karte und ist gegen diesen Planpunkt nicht korrekt entkoppelt.

4. `VideoWebPlayer.Tests/Components/MediaBoxContextMenuInteractionTests.cs:83`: Die neuen MediaBox-Tests pruefen wesentliche UI-Pfade ueber Zustandsobjekte oder Quelltext-Regex, aber nicht als gerenderte Blazor-/Browser-Interaktion. Dadurch bleiben exakt 3 Sekunden Long-Press mit echter Zeitsteuerung, normale Link-Navigation nach kurzem Tap/Klick, Scroll-/Pointer-Cancel im Browser, Klick ausserhalb, Escape am fokussierten Menue und die responsive Overlayposition am Listenrand ungetestet. Gerade Befund 1 und Teile von Befund 3 koennen durch die aktuellen Tests nicht fehlschlagen.

## Nicht beanstandet

- `MediaBox.razor:128` oeffnet das Menue nicht mehr aus dem nativen `contextmenu`-Handler; der fruehere Long-Press-Regressionsbefund ist damit funktional adressiert.
- Fokus und Escape sind gegenueber Iteration 1 verbessert: erste Aktion wird fokussiert, Escape ist am Menuecontainer verdrahtet und der Linkfokus wird beim Schliessen wiederhergestellt.
- `ContinueWatchingList.razor:27` behandelt Reload-Fehler sichtbar und ersetzt die Liste bei Fehlern nicht durch einen leeren Erfolgzustand.
- Continue-Watching- und Favoriten-Serverpfade verwenden den authentifizierten Benutzerkontext; im geprueften API-/Service-Diff wurde keine Datenisolationsregression gefunden.
- EF-Migration, `ListOrder`-Persistenz und Servicepfade fuer Hide/Skip/Favorite-Remove sind durch die vorhandenen Service-Tests grundsaetzlich abgedeckt.

## Testbezug

- `dotnet test VideoWebPlayer.Tests\VideoWebPlayer.Tests.csproj --no-restore`: 183 bestanden, 0 fehlgeschlagen, 0 uebersprungen.
- `dotnet test --no-restore`: 14 `msTools.Backup.Tests`, 3 `VideoWebPlayer.Maui.Tests` und 183 `VideoWebPlayer.Tests` bestanden.
- Es bleiben bestehende Warnungen, unter anderem `NU1903` fuer `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, `SQLitePCLRaw.lib.e_sqlite3.android` 2.1.11 und `SSH.NET` 2025.1.0 sowie bestehende MAUI-Compilerwarnungen. Diese wirken nicht durch den aktuellen Feature-Diff verursacht.
