# Updates im VideoWebPlayer

Diese Hilfe richtet sich an Administratoren. Die Update-Funktion ist im Einrichtungsbereich unter `Einrichtung` > `Updates` verfügbar und erlaubt das Prüfen, Installieren und Konfigurieren von Programmupdates.

Nur angemeldete Benutzer mit Administrationsrechten können die Seite und die zugehörigen Aktionen verwenden.

## Update-Seite

Die Seite `Systemupdates` ist in drei Bereiche gegliedert: `Update-Status`, `Versionsdetails` und `Konfiguration`. Der Aufbau ist für Desktop- und Mobilansichten optimiert, sodass Status, Aktionen und Einstellungen ohne horizontales Scrollen erreichbar bleiben.

Im Bereich `Update-Status` ist erkennbar, ob die Anwendung auf Updates prüft, ein Update gefunden wurde, ein Download oder eine Installation läuft, die letzte Aktion erfolgreich war oder ein Fehler aufgetreten ist. Abruffehler werden direkt im Statusbereich mit verständlicher Meldung und, falls vorhanden, Fehlercode angezeigt.

Im Bereich `Versionsdetails` werden technische Details angezeigt, soweit sie verfügbar sind:

- installierte Version,
- verfügbare Version,
- Prerelease-Kennzeichen,
- Veröffentlichungsdatum,
- letzter Download,
- letzte Installation.

Die Anzeige wird regelmäßig aktualisiert. Mit `Daten aktualisieren` kann der Status zusätzlich manuell neu geladen werden, ohne ungespeicherte Formularwerte zu speichern.

## Manuell auf Updates prüfen

Mit `Nach Updates suchen` startet ein Administrator sofort eine Prüfung auf neue Versionen. Die Aktion ist gesperrt, solange bereits eine Prüfung, ein Download, eine Installation oder eine Update-Sperre aktiv ist. Beim Start einer neuen manuellen Prüfung bereinigt der Updater eine zuvor gespeicherte Fehlermeldung, damit kein veralteter Fehler mehr im Statusbereich stehen bleibt.

Nach Abschluss zeigt die Seite die Erfolgsmeldung oder Fehlermeldung im Bereich `Update-Status` an. Wenn eine neue Version gefunden wurde, erscheint sie ebenfalls im Statusbereich und kann installiert werden.

## Update manuell installieren

Mit `Update installieren` wird eine bekannte neue Version installiert. Der Button ist nur aktiv, wenn eine installierbare Version bekannt ist.

Ist das Update gefunden, aber noch nicht heruntergeladen, startet die Anwendung zuerst den Download und danach die Installation. Während Download oder Installation laufen, sind weitere Update-Aktionen gesperrt.

Je nach Serverkonfiguration kann die Installation einen Neustart der Anwendung oder des Dienstes auslösen. Der konfigurierte Dienstname wird für diesen Neustart verwendet.

**Wichtig — deployment-seitige Dateien:** Eine Update-Installation schützt deployment-seitige Anpassungen an `web.config` und `appsettings*.json` über die ausgelieferte `AutoUpdate:ProtectedFiles`-Schutzliste: Das Installationsskript sichert die Dateien vor dem Paketkopiervorgang unter `Updates/backup/` (manuelle Restore-Ablage, wird nicht automatisch aufgeräumt) und übernimmt danach die konfigurierten Bereiche — XML-Elemente/-Attribute wie `<environmentVariables>` und `security/ipSecurity` sowie aufgezählte JSON-Schlüssel — aus der Bestandsdatei in die Paketversion. Der Schutz greift erst ab dem ersten Update **nach** dieser Version; das Update auf diese Version selbst ersetzt die Dateien noch vollständig. Zusätzlich angelegte Dateien, die nicht im Paket enthalten sind, bleiben weiterhin erhalten — darunter die optionale `appsettings.Local.json` im Anwendungsverzeichnis, die für beliebige deployment-seitige Konfigurationswerte außerhalb der `JsonKeys`-Liste verwendet werden kann. Secrets sollten weiterhin bevorzugt als maschinenweite Umgebungsvariablen (`Jwt__*`) abgelegt werden.

Randbedingungen des Schutzes: Unter Linux benötigt der Merge `python3` auf dem Zielsystem — fehlt es, wird der Merge mit einem Eintrag in `update.log` übersprungen und das Update läuft weiter (die Sicherungen bleiben unter `Updates/backup/`). Sicherungs- oder Merge-Fehler brechen die Installation grundsätzlich nicht ab. Deployment-seitig angepasste `appsettings*.json`-Dateien sollten kommentarfrei bleiben — JSON-Kommentare führen zu einem Lesefehler, und der Merge wird für die betroffene Datei übersprungen. Die `appsettings.Local.json` wird bei Änderungen ohne Neustart neu eingelesen, sollte ebenfalls kommentarfrei bleiben (der `appsettings*.json`-Wildcard-Eintrag erfasst auch sie) und wird bei jedem Update zusätzlich unter `Updates/backup/` gesichert. Wer die Schutzliste deployment-seitig erweitern will, nutzt Umgebungsvariablen (`AutoUpdate__ProtectedFiles__{n}__Path` usw.) — Änderungen an der Liste in `appsettings.json` selbst überstehen kein Update.

## Konfiguration

Im Bereich `Konfiguration` werden die Update-Einstellungen gespeichert. Änderungen gelten für neue Update-Aktionen ohne manuelle Änderung an der Konfigurationsdatei. `Standards zurücksetzen` lädt die zentralen Standardwerte nur in das Formular; dauerhaft übernommen werden sie erst mit `Konfiguration speichern`.

### Automatische Prüfung

`Automatische Prüfung` legt fest, ob die Anwendung regelmäßig nach neuen Versionen sucht. Das `Prüfintervall in Minuten` bestimmt den Abstand zwischen diesen Prüfungen. Zulässig sind Werte von 1 bis 1440 Minuten.

Manuelle Prüfungen über `Nach Updates suchen` bleiben unabhängig davon möglich.

### Prerelease-Versionen

`Vorabversionen akzeptieren` erlaubt experimentelle Prerelease-Versionen. Beim erstmaligen Aktivieren muss die zusätzliche Sicherheitsabfrage `Prerelease-Aktivierung bestätigen` bestätigt werden.

Ohne diese Bestätigung wird die Einstellung nicht aktiviert.

### Automatische Installation

`Automatische Installation` legt fest, ob eine gefundene neue Version automatisch installiert werden darf. Ist diese Einstellung deaktiviert, wird eine gefundene Version nur angezeigt und kann manuell installiert werden.

### Dienstname für Neustart

`Dienstname für Neustart` enthält den Namen des Dienstes, der im Installations- oder Neustartablauf verwendet wird. Der Wert muss zur tatsächlichen Serverinstallation passen. Läuft die Anwendung unter IIS, ist das Feld gesperrt und ein eingetragener Wert wirkungslos — ein Dienstname gilt nur für Windows-Dienste oder systemd (siehe nächster Abschnitt).

### IIS-App-Pool (nur Konfigurationsdatei)

Läuft die Anwendung als IIS-Site, benötigt die Update-Installation die Skriptvariante für App-Pools. Die automatische Erkennung findet nur Windows-Dienste, keine IIS-App-Pools — der App-Pool muss deshalb über die Konfigurationsdatei benannt werden:

- `AutoUpdate:AppPoolName` — Name des IIS-Anwendungspools,
- `AutoUpdate:SiteName` — Name der IIS-Site (optional, ergänzt `AppPoolName`).

Beide Schlüssel werden in einer `appsettings*.json`-Datei oder als Umgebungsvariablen (`AutoUpdate__AppPoolName`, `AutoUpdate__SiteName`) gesetzt — die Bibliothek bietet zusätzlich die Fluent-API `WithIisApplicationPool`, die der VideoWebPlayer nicht nutzt; in der Update-Oberfläche gibt es dafür kein Feld. Beide Schlüssel stehen in der `JsonKeys`-Schutzliste: In `appsettings*.json` gepflegte Werte überstehen Update-Installationen daher per `Merge` (ab dem Folge-Update, siehe Hinweis zu deployment-seitigen Dateien oben); Umgebungsvariablen bleiben die robusteste Variante.

### Backup vor Installation

`Backup vor Installation` erzeugt vor der Installation ein Backup über dieselbe Backup-Infrastruktur wie die manuelle Backup-Seite.

Das Update-Backup sichert nur die Anwendungsdaten — Konfigurationsdateien wie `web.config` oder `appsettings*.json` sind nicht enthalten. Der Schutz dieser Dateien läuft getrennt davon über die `AutoUpdate:ProtectedFiles`-Schutzliste im Installationsskript (siehe Hinweis zu deployment-seitigen Dateien oben; Dateisicherungen unter `Updates/backup/`).

Die Generation dieser Sicherungen ist `ProgramUpdate`. Dadurch sind Update-Backups in der Backup-Historie von manuell erstellten und automatischen GVS-Backups unterscheidbar.

`Bei Backupfehler abbrechen` sollte im Normalbetrieb aktiviert bleiben. Dann startet die Installation nicht, wenn das Backup fehlschlägt. Der Fehler wird im Updatestatus angezeigt.

### Update-Backup-Pfad und Aufbewahrung

`Update-Backup-Pfad` ist der Backup-Speicherpfad, den die bestehende Backup-Infrastruktur für neue Backup-Operationen verwendet.

`Aufzubewahrende Update-Backups` legt fest, wie viele Backups der Generation `ProgramUpdate` behalten werden. Zulässig sind Werte von 1 bis 10. Diese Aufbewahrung betrifft nur Update-Backups. Manuelle Backups sowie Sohn-, Vater- und Großvater-Backups bleiben nach ihren eigenen Regeln erhalten.

## Typische Bedienabläufe

### Update kontrolliert installieren

1. `Nach Updates suchen` ausführen.
2. Prüfen, welche verfügbare Version angezeigt wird.
3. Sicherstellen, dass `Backup vor Installation erstellen` und `Installation bei Backupfehler abbrechen` aktiviert sind.
4. `Update installieren` ausführen.
5. Nach dem Neustart den Status erneut prüfen.

### Automatische Updates vorbereiten

1. `Automatische Prüfung` aktivieren.
2. Ein sinnvolles Prüfintervall setzen.
3. Optional `Automatische Installation` aktivieren.
4. Dienstname für den Neustart prüfen.
5. Backup vor Installation aktiviert lassen.
6. `Konfiguration speichern`.

## Fehler und Sperren

Wenn eine Update-Aktion fehlschlägt, zeigt die Seite die Fehlermeldung im Statusbereich an. Häufige Ursachen sind fehlende Netzwerkverbindung zum Release-Repository, ein ungültiges Update-Paket, fehlende Schreibrechte im Update- oder Backup-Verzeichnis oder ein fehlgeschlagenes Backup.

Eine aktive Update-Sperre verhindert parallele Prüf-, Download- oder Installationsaktionen. In diesem Zustand sollten Administratoren warten, bis die laufende Aktion beendet ist, und den Status anschließend erneut laden.

## Verwandte Dokumentation

- [Einrichtung](./einrichtung.md)
- [Backups](./backups.md)
- [Technische Dokumentation zu automatisierten Programmupdates](../TECH_Auto_Update.md)
