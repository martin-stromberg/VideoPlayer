# Übersetzte Anforderung

## Metadaten

- **Aufgaben-ID:** 4d6054962a4c42ae-ad62-5a8be27607ea
- **Branch:** `task/4d6054962a4c42aead625a8be27607ea-projekt-aufraeumen`
- **Titel:** Nicht benötigte Projekte und Dateien aus der Solution entfernen

## Ziel

Die Solution und das Repository sollen von alten oder nicht mehr benötigten Projekten und Dateien bereinigt werden. Die Bereinigung darf die weiterhin benötigte Videoverwaltung und deren mobile Zugriffsmöglichkeit nicht beeinträchtigen.

## Funktionale Anforderungen

1. Das Projekt `VideoWebPlayer` bleibt als Webanwendung der Videoverwaltung erhalten.
2. Das Projekt `VideoWebPlayer.Maui` bleibt als mobile Anwendung für den Zugriff auf die Videoverwaltung erhalten.
3. Die mobile Anwendung muss weiterhin auf die von `VideoWebPlayer` bereitgestellte API zugreifen können.
4. Nicht mehr benötigte Projekte und Dateien dürfen aus der Solution beziehungsweise dem Repository entfernt werden.
5. Projektdateien, Solution-Einträge und Referenzen müssen nach der Bereinigung konsistent sein.

## Nichtfunktionale Anforderungen

- Die bestehenden Funktionen der Webanwendung und der mobilen Anwendung dürfen durch die Bereinigung nicht verändert werden.
- Die Build- und Startfähigkeit der verbleibenden Kernprojekte muss erhalten bleiben.
- Abhängigkeiten der beiden verbleibenden Anwendungen, einschließlich gemeinsam genutzter Code- oder Testprojekte, dürfen nur entfernt werden, wenn ihre Nichtbenötigung nachgewiesen ist.
- Die Bereinigung soll auf die nicht mehr benötigten Artefakte begrenzt und nachvollziehbar umgesetzt werden.

## Akzeptanzkriterien

- [ ] `VideoWebPlayer` ist weiterhin als Webanwendung der Videoverwaltung in der Solution beziehungsweise im Repository vorhanden.
- [ ] `VideoWebPlayer.Maui` ist weiterhin als mobile Anwendung in der Solution beziehungsweise im Repository vorhanden.
- [ ] Die API-Kommunikation zwischen `VideoWebPlayer.Maui` und `VideoWebPlayer` bleibt funktionsfähig.
- [ ] Nicht mehr benötigte Projekte und Dateien sind entfernt.
- [ ] Es bestehen keine verwaisten Solution-Einträge oder ungültigen Projektverweise aufgrund der Bereinigung.
- [ ] Die verbleibenden relevanten Projekte lassen sich erfolgreich bauen und testen.

## Betroffene Klassen und Komponenten

- Solution-Datei `VideoPlayer.sln` und darin enthaltene Projekt- und Konfigurationseinträge.
- Projekt `VideoWebPlayer` einschließlich seiner API-Endpunkte und Backend-Abhängigkeiten.
- Projekt `VideoWebPlayer.Maui` einschließlich der API-Clients und mobilen Zugriffskomponenten.
- Potenziell gemeinsam genutzte Projekte wie `VideoWebPlayer.Client` und `WebPlayerApi.Common`, sofern sie von den verbleibenden Anwendungen benötigt werden.
- Potenziell zugehörige Testprojekte und Testressourcen.
- Potenziell veraltete Projekte und Dateien außerhalb der beiden Kernprojekte, sofern keine Referenz oder fachliche Nutzung mehr besteht.

## Implementierungsansatz

Zuerst werden Solution-Einträge, Projektverweise, Paketabhängigkeiten und Datei- beziehungsweise Laufzeitreferenzen der beiden zu erhaltenden Anwendungen ermittelt. Anschließend werden ausschließlich nicht mehr benötigte Projekte und Dateien entfernt und alle dadurch betroffenen Solution- und Projektdateien bereinigt. Abschließend werden die verbleibenden Projekte sowie die API-Kommunikation der MAUI-Anwendung durch Build- und geeignete Tests verifiziert.

## Konfiguration

Es ist keine neue Laufzeit- oder Benutzerkonfiguration erforderlich. Die bestehende Solution- und Projektkonfiguration muss an die verbleibenden Projekte angepasst werden.

## Offene Fragen

- Welche konkreten Projekte und Dateien gelten neben `VideoWebPlayer` und `VideoWebPlayer.Maui` als veraltet und dürfen entfernt werden?
- Müssen `VideoWebPlayer.Client`, `WebPlayerApi.Common`, `VideoWebPlayer.Tests` und `VideoWebPlayer.Maui.Tests` erhalten bleiben, sofern sie von den Kernprojekten verwendet werden?
- Sollen ausschließlich nicht referenzierte Artefakte entfernt werden, oder dürfen auch historisch vorhandene, aber noch referenzierte Altprojekte wie `WebPlayer` und `WebPlayerApi` ersetzt beziehungsweise entkoppelt werden?
- Welche Build-Ziele und Plattformen müssen nach der Bereinigung verbindlich erfolgreich geprüft werden?

## Abgrenzung

- Eine fachliche Erweiterung der Videoverwaltung ist nicht Bestandteil dieser Anforderung.
- Eine Änderung der API-Verträge oder der mobilen Benutzeroberfläche ist nicht Bestandteil dieser Anforderung.
- Die Anforderung umfasst keine Bereinigung von Medien-, Benutzer- oder Laufzeitdaten.
