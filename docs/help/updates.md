# Updates im VideoWebPlayer

Diese Hilfe richtet sich an Administratoren. Die Update-Funktion ist im Verwaltungsbereich unter `Updates` verfügbar und erlaubt das Prüfen, Installieren und Konfigurieren von Programmupdates.

Nur angemeldete Benutzer mit Administrationsrechten können die Seite und die zugehörigen Aktionen verwenden.

## Update-Seite

Die Seite zeigt oben den aktuellen Updatestatus. Dort ist erkennbar, ob die Anwendung auf Updates prüft, ein Update gefunden wurde, ein Download oder eine Installation läuft, die letzte Aktion erfolgreich war oder ein Fehler aufgetreten ist.

Zusätzlich werden technische Details angezeigt, soweit sie verfügbar sind:

- installierte Version,
- verfügbare Version,
- Prerelease-Kennzeichen,
- Veröffentlichungsdatum,
- letzte Prüfung,
- letzter Download,
- letzte Installation,
- letztes Prüfergebnis,
- aktive Update-Sperre und Fehler.

Die Anzeige wird regelmäßig aktualisiert. Mit `Aktualisieren` kann der Status zusätzlich manuell neu geladen werden.

## Manuell auf Updates prüfen

Mit `Jetzt pruefen` startet ein Administrator sofort eine Prüfung auf neue Versionen. Die Aktion ist gesperrt, solange bereits eine Prüfung, ein Download, eine Installation oder eine Update-Sperre aktiv ist.

Nach Abschluss zeigt die Seite eine Erfolgsmeldung oder eine Fehlermeldung an. Wenn eine neue Version gefunden wurde, erscheint sie im Statusbereich und kann installiert werden.

## Update manuell installieren

Mit `Update installieren` wird eine bekannte neue Version installiert. Der Button ist nur aktiv, wenn eine installierbare Version bekannt ist.

Ist das Update gefunden, aber noch nicht heruntergeladen, startet die Anwendung zuerst den Download und danach die Installation. Während Download oder Installation laufen, sind weitere Update-Aktionen gesperrt.

Je nach Serverkonfiguration kann die Installation einen Neustart der Anwendung oder des Dienstes auslösen. Der konfigurierte Dienstname wird für diesen Neustart verwendet.

## Einstellungen

Im Bereich `Einstellungen` werden die Update-Einstellungen gespeichert. Änderungen gelten für neue Update-Aktionen ohne manuelle Änderung an der Konfigurationsdatei.

### Automatische Prüfung

`Automatische Pruefung aktivieren` legt fest, ob die Anwendung regelmäßig nach neuen Versionen sucht. Das `Pruefintervall in Minuten` bestimmt den Abstand zwischen diesen Prüfungen.

Manuelle Prüfungen über `Jetzt pruefen` bleiben unabhängig davon möglich.

### Prerelease-Versionen

`Prerelease-Versionen akzeptieren` erlaubt experimentelle Vorabversionen. Beim erstmaligen Aktivieren muss die zusätzliche Sicherheitsabfrage `Prerelease-Aktivierung bestaetigen` bestätigt werden.

Ohne diese Bestätigung wird die Einstellung nicht aktiviert.

### Automatische Installation

`Neue Version automatisch installieren` legt fest, ob eine gefundene neue Version automatisch installiert werden darf. Ist diese Einstellung deaktiviert, wird eine gefundene Version nur angezeigt und kann manuell installiert werden.

### Dienstname für Neustart

`Dienstname fuer Neustart` enthält den Namen des Dienstes, der im Installations- oder Neustartablauf verwendet wird. Der Wert muss zur tatsächlichen Serverinstallation passen.

### Backup vor Installation

`Backup vor Installation erstellen` erzeugt vor der Installation ein Backup über dieselbe Backup-Infrastruktur wie die manuelle Backup-Seite.

Die Generation dieser Sicherungen ist `ProgramUpdate`. Dadurch sind Update-Backups in der Backup-Historie von manuell erstellten und automatischen GVS-Backups unterscheidbar.

`Installation bei Backupfehler abbrechen` sollte im Normalbetrieb aktiviert bleiben. Dann startet die Installation nicht, wenn das Backup fehlschlägt. Der Fehler wird im Updatestatus angezeigt.

### Update-Backup-Pfad und Aufbewahrung

`Update-Backup-Pfad` ist der Backup-Speicherpfad, den die bestehende Backup-Infrastruktur für neue Backup-Operationen verwendet.

`Aufbewahrung Update-Backups` legt fest, wie viele Backups der Generation `ProgramUpdate` behalten werden. Diese Aufbewahrung betrifft nur Update-Backups. Manuelle Backups sowie Sohn-, Vater- und Großvater-Backups bleiben nach ihren eigenen Regeln erhalten.

## Typische Bedienabläufe

### Update kontrolliert installieren

1. `Jetzt pruefen` ausführen.
2. Prüfen, welche verfügbare Version angezeigt wird.
3. Sicherstellen, dass `Backup vor Installation erstellen` und `Installation bei Backupfehler abbrechen` aktiviert sind.
4. `Update installieren` ausführen.
5. Nach dem Neustart den Status erneut prüfen.

### Automatische Updates vorbereiten

1. `Automatische Pruefung aktivieren`.
2. Ein sinnvolles Prüfintervall setzen.
3. Optional `Neue Version automatisch installieren` aktivieren.
4. Dienstname für den Neustart prüfen.
5. Backup vor Installation aktiviert lassen.
6. Einstellungen speichern.

## Fehler und Sperren

Wenn eine Update-Aktion fehlschlägt, zeigt die Seite die Fehlermeldung im Statusbereich an. Häufige Ursachen sind fehlende Netzwerkverbindung zum Release-Repository, ein ungültiges Update-Paket, fehlende Schreibrechte im Update- oder Backup-Verzeichnis oder ein fehlgeschlagenes Backup.

Eine aktive Update-Sperre verhindert parallele Prüf-, Download- oder Installationsaktionen. In diesem Zustand sollten Administratoren warten, bis die laufende Aktion beendet ist, und den Status anschließend erneut laden.

## Verwandte Dokumentation

- [Backups](./backups.md)
- [Technische Dokumentation zu automatisierten Programmupdates](../TECH_Auto_Update.md)
