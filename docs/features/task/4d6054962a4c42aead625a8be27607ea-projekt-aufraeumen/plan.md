# Umsetzungsplan

## Ziel

Die Solution und das Repository werden um die nicht mehr benötigten Altprojektbereiche bereinigt. `VideoWebPlayer`, `VideoWebPlayer.Maui`, `VideoWebPlayer.Client` sowie die zugehörigen Testprojekte und Laufzeitabhängigkeiten bleiben erhalten. Die API-Kommunikation zwischen Webanwendung und MAUI-Anwendung wird nicht verändert.

## Verbindliche Entscheidungen und Abgrenzung

Externe Deployment-, CI- oder Startskripte verwenden die Altbereiche nicht. Nach der Prüfung der Repository-internen Referenzen dürfen daher folgende Bereiche vollständig entfernt werden:

- `Videos/`
- `WebPlayer/` einschließlich `WebPlayer/WebPlayer.Client/`
- `WebPlayerApi/`
- `WebPlayerApi.Common/`

Erhalten und unverändert bleiben:

- `VideoPlayer.sln`
- `VideoWebPlayer/`
- `VideoWebPlayer.Client/`
- `VideoWebPlayer.Maui/`
- `VideoWebPlayer.Tests/`
- `VideoWebPlayer.Maui.Tests/`
- `lib/msTools.Updater/` einschließlich der referenzierten DLL
- `Images/`, sofern keine konkrete Referenzprüfung ihre Entbehrlichkeit belegt

Die MAUI-Anwendung bleibt Bestandteil der Solution und des Repositories. Ein Start der MAUI-App sowie plattformspezifische Laufzeitprüfungen sind ausdrücklich nicht Bestandteil dieser Aufgabe.

## Umsetzungsschritte

### 1. Referenzen und Nutzung vor dem Löschen prüfen

1. Repositoryweit nach Pfaden, Projekt- und Assembly-Namen sowie Build-, Start-, Deployment-, CI- und Dokumentationsverweisen auf `Videos`, `WebPlayer`, `WebPlayerApi` und `WebPlayerApi.Common` suchen.
2. Die aktuelle Solution und alle erhaltenen Projektdateien auf `ProjectReference`, Datei- und Paketabhängigkeiten prüfen.
3. Bestätigen, dass die erhaltenen Projekte ausschließlich den aktuellen Projektgraphen verwenden und `VideoWebPlayer.Client` weiterhin von Web und MAUI referenziert wird.
4. Die Prüfungsergebnisse einschließlich der bestätigten externen Nichtverwendung nachvollziehbar im Implementierungs- beziehungsweise Testprotokoll festhalten.

### 2. Altprojektbereiche entfernen

1. Die vier Altbereiche einschließlich verschachtelter Projektdateien, Quellen und Ressourcen entfernen.
2. `VideoPlayer.sln` nur anpassen, falls die Referenzprüfung wider Erwarten noch verwaiste Einträge für diese Bereiche findet.
3. Keine Änderungen an API-Endpunkten, gemeinsamen Modellen, MAUI-Clientregistrierung, Basisadressauflösung oder UDP-Discovery vornehmen.
4. Dateien außerhalb der eindeutig zugeordneten Altbereiche nicht löschen.

### 3. Konsistenzprüfung

1. Die Solution-Datei auf vorhandene Projektpfade und verwaiste Einträge prüfen.
2. Alle `ProjectReference`-Pfade der erhaltenen Projekte auflösen und sicherstellen, dass kein Verweis auf einen entfernten Bereich zeigt.
3. Paket- und Dateiabhängigkeiten der erhaltenen Projekte kontrollieren, insbesondere `lib/msTools.Updater/msTools.Updater.dll` sowie die MAUI-Ressourcen.
4. Sicherstellen, dass weder das MAUI-Projekt noch `VideoWebPlayer.Maui.Tests` durch entfernte Projekt- oder Dateireferenzen beeinträchtigt werden.

### 4. Build- und Testverifikation

1. `VideoPlayer.sln` beziehungsweise die relevanten verbleibenden Projekte für die in der Umgebung verfügbaren Konfigurationen bauen.
2. `VideoWebPlayer.Tests` ausführen.
3. `VideoWebPlayer.Maui.Tests` ausführen, sofern die vorhandenen Testabhängigkeiten dies in der Zielumgebung erlauben.
4. Das MAUI-Projekt nur so weit prüfen, dass entfernte Projekte oder Dateien keine Build- oder Testfehler verursachen. Kein Start der MAUI-App und keine plattformspezifische Laufzeitprüfung durchführen.
5. Vorhandene Tests zur API-Kommunikation beziehungsweise zum gemeinsamen Client ausführen, ohne API-Verträge oder Anwendungscode zu ändern.
6. Fehlende Workloads oder nicht verfügbare Testvoraussetzungen als Umgebungsgrenze dokumentieren.

## Betroffene Artefakte

Voraussichtliche Änderungen:

- Löschung der Verzeichnisse `Videos/`, `WebPlayer/`, `WebPlayerApi/` und `WebPlayerApi.Common/`
- gegebenenfalls Entfernung verwaister Altprojekt-Einträge aus `VideoPlayer.sln`

Schutzbereiche:

- die fünf aktuellen Solution-Projekte und ihre Projektverweise
- API- und Clientverträge
- MAUI-Runtime-Konfiguration und Discovery
- `lib/msTools.Updater/`

## Akzeptanz- und Prüfkriterien

- `VideoWebPlayer` und `VideoWebPlayer.Maui` sind weiterhin in der Solution und im Repository vorhanden.
- `VideoWebPlayer.Client` bleibt als gemeinsamer API-Client referenziert.
- Die vier festgelegten Altbereiche sind entfernt.
- Es gibt keine Solution-Einträge oder Projektverweise auf entfernte Projekte.
- Das MAUI-Projekt und seine Tests werden durch keine entfernten Referenzen beeinträchtigt.
- Die relevanten verbleibenden Projekte und Tests bauen beziehungsweise laufen erfolgreich, oder Einschränkungen sind konkret dokumentiert.
- Es wurden keine API-Verträge, fachlichen Funktionen oder MAUI-Laufzeitpfade geändert.

## Offene Punkte

Keine. Die externe Nichtverwendung der Altbereiche ist bestätigt; die MAUI-Verifikation ist auf Referenz- und Build-/Testkonsistenz begrenzt und umfasst keinen App-Start sowie keine plattformspezifische Laufzeitprüfung.

## Rückfallstrategie

Werden bei der Umsetzung unerwartete interne Verweise oder durch die Löschung verursachte Build-/Testfehler gefunden, wird der betroffene Löschumfang zunächst ausgesetzt. Es werden ausschließlich die Referenzfehler der Bereinigung korrigiert; fachliche Änderungen an Web-, Client- oder MAUI-Code sind nicht Bestandteil dieser Aufgabe.
