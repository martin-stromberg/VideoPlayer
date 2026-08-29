# Anforderung: Strukturierte Updates-Seite

## Ziel

Die bestehende Seite fuer Programmupdates soll entsprechend dem Stitch-Entwurf neu strukturiert werden. Anwender sollen den Update-Status schnell erfassen, Update-Aktionen ausfuehren und die automatischen Update- sowie Backup-Einstellungen an einer klar gegliederten Stelle verwalten koennen.

## Umfang

### Seitenaufbau

- Eine eigenstaendige Seite mit dem Titel "System Updates" in der bestehenden Anwendung bereitstellen.
- Die Seite in drei Bereiche gliedern:
  1. Update-Status mit aktuellem Versionsstand, Zeitpunkt der letzten Pruefung und Status-/Fehlermeldung.
  2. Version Details mit installierter Version, verfuegbarer Version, Prerelease-Kanal, Release-Datum und letztem Download.
  3. Configuration mit den Bereichen Automation sowie Safety & Backups.
- Die bestehende Navigation und das bestehende Anwendungslayout weiterverwenden; die Updates-Seite muss auf Desktop und mobilen Ansichten nutzbar sein.

### Update-Status und Aktionen

- Den aktuellen Versionsstand und den Zeitpunkt der letzten Update-Pruefung anzeigen.
- Fehler beim Abruf von Update- oder Release-Informationen sichtbar und verstaendlich darstellen.
- Eine Aktion "Check for Updates" anbieten, die eine erneute Update-Pruefung ausloest.
- Eine Aktion "Refresh Data" anbieten, die die angezeigten Update-Daten neu laedt.
- Eine Aktion "Install Update" anzeigen und deaktivieren, wenn kein installierbares Update verfuegbar ist oder die Voraussetzungen nicht erfuellt sind.

### Versionsdetails

- Installierte Version und verfuegbare Version anzeigen.
- Kennzeichnen, ob der Prerelease-Kanal aktiv ist.
- Release-Datum und letzten Download anzeigen; fuer einen nicht vorhandenen Download einen eindeutigen Leerzustand darstellen.

### Konfiguration

- Folgende Automatisierungsoptionen als einstellbare Schalter anbieten:
  - Automatic Check
  - Allow Prerelease
  - Auto-Install
- Das Pruefintervall in Minuten als numerischen Wert bearbeiten lassen.
- Den Namen des Dienstes fuer den Neustart bearbeiten lassen.
- Folgende Sicherheits- und Backup-Optionen als einstellbare Schalter anbieten:
  - Pre-install Backup
  - Abort on Backup Failure
- Den Speicherpfad fuer Backups bearbeiten lassen.
- Die Anzahl aufzubewahrender Backups als numerischen Wert mit einem gueltigen Bereich von 1 bis 10 bearbeiten lassen.
- Eine Aktion "Reset Defaults" zum Zuruecksetzen der Konfiguration anbieten.
- Eine Aktion "Save Configuration" zum Speichern der geaenderten Einstellungen anbieten.

### Gestaltung

- Den visuellen Entwurf mit dunkler, kontrastreicher Oberflaeche, roten primaeren Aktionen und blauen Informationsakzenten umsetzen.
- Status-, Detail- und Konfigurationsbereiche als klar voneinander getrennte Flaechen mit dezenter Umrandung darstellen.
- Interaktive Bedienelemente mit eindeutigen Beschriftungen, sichtbaren deaktivierten Zustaenden und Tastaturbedienbarkeit ausstatten.
- Die Inhalte auf kleinen Bildschirmen untereinander anordnen, ohne horizontales Abschneiden oder ueberlappende Texte.

## Nicht-Ziele

- Keine Aenderung am eigentlichen Update-Backend, Release-Download oder Installationsprozess, sofern dies fuer die neue Darstellung nicht erforderlich ist.
- Keine Einfuehrung neuer externer Abhaengigkeiten.
- Keine Aenderung an Authentifizierung, Medienverwaltung oder Datenmodell ausserhalb der fuer Update-Konfiguration und Statusdarstellung erforderlichen Schnittstellen.
- Keine Erweiterung um weitere Update-Kanaele oder Backup-Strategien, die im Entwurf nicht vorgesehen sind.

## Akzeptanzkriterien

- Die Updates-Seite ist ueber die bestehende Navigation erreichbar und rendert ohne Fehler.
- Die Seite entspricht der im Stitch-Entwurf vorgegebenen Informationshierarchie mit den Bereichen Update Status, Version Details und Configuration.
- Aktuelle Version, letzte Pruefung, installierte/verfuegbare Version, Prerelease-Status, Release-Datum und letzter Download werden korrekt aus den vorhandenen Daten angezeigt.
- Abruffehler werden als sichtbarer Status mit einer verstaendlichen Fehlermeldung dargestellt, ohne die restliche Seite unbenutzbar zu machen.
- "Check for Updates" und "Refresh Data" loesen die jeweils erwartete Aktion aus.
- "Install Update" ist bei fehlendem oder nicht installierbarem Update deaktiviert und wird bei gueltiger Installationsmoeglichkeit nutzbar.
- Alle im Abschnitt Configuration genannten Schalter und Eingabefelder koennen bearbeitet werden.
- Numerische Eingaben validieren das Pruefintervall sowie die Backup-Aufbewahrung; die Retention Count akzeptiert nur Werte von 1 bis 10.
- "Reset Defaults" setzt die editierbaren Werte auf die vorgesehenen Standardwerte zurueck; "Save Configuration" speichert sie dauerhaft.
- Die Seite ist auf Desktop- und mobilen Viewports responsiv und ohne ueberlappende oder abgeschnittene Inhalte nutzbar.
- Relevante automatisierte Tests, einschliesslich des Benutzerflusses fuer Pruefen, Konfigurieren, Zuruecksetzen und Speichern, laufen erfolgreich.

## Offene Punkte

- Welche bestehenden Routen- und Komponentenbezeichnungen bilden die Updates-Seite aktuell ab?
- Welche Standardwerte und Persistenzschnittstellen gelten fuer die neuen Konfigurationsfelder ausser den im Entwurf sichtbaren Beispielwerten?
- Unter welchen fachlichen Bedingungen darf "Install Update" aktiviert werden und welcher bestehende Installationsworkflow wird dabei aufgerufen?
