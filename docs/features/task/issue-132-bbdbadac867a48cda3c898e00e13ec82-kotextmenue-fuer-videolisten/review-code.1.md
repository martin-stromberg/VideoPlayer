# Code-Review

Status: Befunde vorhanden

## Gepruefter Umfang

- Anforderung, Plan und Plan-Review unter `docs/features/task/issue-132-bbdbadac867a48cda3c898e00e13ec82-kotextmenue-fuer-videolisten/`.
- Workspace-Diff fuer `MediaBox`, `ContinueWatchingList`, `FavoritesList`, Continue-Watching-API, Services, EF-Migration, Client-Methoden und neue Service-Tests.
- Fokus: Bugs/Regressionen, Sicherheits- und Datenisolationsrisiken, EF-Migration/API-Vertraege, Blazor-Interaktion und fehlende Tests.

## Befunde

1. `VideoWebPlayer/Components/Shared/Media/MediaBox.razor:30` und `VideoWebPlayer/Components/Shared/Media/MediaBox.razor:118`: Das Kontextmenue wird ueber das native `contextmenu`-Event sofort geoeffnet. Damit umgehen Rechtsklick auf Desktop und browserseitige Touch-Kontextmenues die geforderte Drei-Sekunden-Haltezeit. Fuer Karten mit Aktionen reicht dann ein normales Kontextmenue-Event, ohne dass `OpenMenuAfterDelayAsync` abgelaufen ist. Das verletzt das Akzeptanzkriterium "darf erst nach einer Haltezeit von drei Sekunden geoeffnet werden" und kann auf Mobilgeraeten je nach Browser deutlich vor 3 Sekunden ausloesen. Empfehlung: `contextmenu` fuer Aktionskarten nur unterdruecken oder nur dann auswerten, wenn ein zuvor gesetzter Long-Press-Zustand aktiv ist; Tastaturzugriff separat ueber `ContextMenu`/`Shift+F10` abbilden.

2. `VideoWebPlayer/Components/Shared/Media/MediaBox.razor:12` und `VideoWebPlayer/Components/Shared/Media/MediaBox.razor:124`: Escape ist nur am Link verdrahtet, nicht am geoeffneten Menue oder einem dokument-/containerweiten Handler. Sobald der Fokus auf einem Menuebutton liegt oder das Menue per Pointer ohne fokussierten Link geoeffnet wurde, erreicht Escape `HandleKeyDown` nicht zuverlaessig. Der im Plan verbindliche Schliesspfad "Escape schliesst das Menue" ist dadurch nicht robust umgesetzt. Empfehlung: Fokus beim Oeffnen ins Menue setzen und Escape am Menuecontainer beziehungsweise ueber JS/Blazor-Fokus-Management behandeln.

3. `VideoWebPlayer.Tests/Services/ContinueWatchingContextMenuActionTests.cs:14` und `VideoWebPlayer.Tests/Services/FavoritesServiceContextMenuActionTests.cs:14`: Die neuen Tests decken die Serveraktionen ab, aber nicht die risikoreiche Blazor-Interaktion. Es fehlen Komponenten- oder Browser-Tests fuer exakt 3 Sekunden Haltezeit, vorzeitiges Loslassen, Pointer-Cancel, Bewegung/Scroll-Abbruch, natives `contextmenu` ohne Long-Press, Escape-Schliessen, normale Link-Navigation und die Negativpruefung, dass "Neu im Programm" kein Menue rendert. Gerade Befund 1 waere durch einen solchen Test sichtbar. Empfehlung: bUnit-Tests fuer Parameter-/Event-Verhalten und mindestens ein Browser-/Playwright-Test fuer Pointer-Timing und Scrollbewegung ergaenzen.

## Nicht beanstandet

- Continue-Watching- und Favoriten-Mutationen verwenden serverseitig den authentifizierten Benutzerkontext; keine direkte Datenisolationsverletzung im geprueften Code gefunden.
- Die neue EF-Migration und der Model-Snapshot bauen erfolgreich; `ListOrder` wird in Service-Tests fuer Skip-Ersetzungen geprueft.
- Die API-Client-Methoden kapseln die neuen Continue-Watching-Endpunkte und den Favoriten-Remove-Aufruf, statt direkte HTTP-Aufrufe in Razor-Komponenten zu duplizieren.

## Testbezug

- `dotnet test`: erfolgreich, 174 `VideoWebPlayer.Tests` und 14 `msTools.Backup.Tests` bestanden.
- Beim Testlauf traten weiterhin `NU1903`-Warnungen fuer bekannte hoch schwere Schwachstellen in `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, `SQLitePCLRaw.lib.e_sqlite3.android` 2.1.11 und `SSH.NET` 2025.1.0 auf. Diese wirken nicht neu durch den Feature-Diff, bleiben aber ein aktuelles Sicherheitsrisiko im Workspace.
