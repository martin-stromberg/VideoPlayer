# Einrichtung im VideoWebPlayer

Diese Hilfe richtet sich an Administratoren. Der Einrichtungsbereich bündelt die administrativen Seiten der Webanwendung unter dem Menüpunkt `Einrichtung`.

## Bereiche

Die Startseite der Einrichtung zeigt Kacheln für die wichtigsten Verwaltungsaufgaben:

- `Quellen` für lokale, FTP- und SFTP-Medienquellen
- `Backups` für Backup, Upload, Restore und Aufbewahrung
- `Updates` für Programmupdates und Update-Einstellungen
- `Sicherheit` für blockierte IP-Adressen
- `Genres` für Genre-Metadaten, Synonyme und Icons
- `Allgemein` für Anwendungstitel und Scan-Intervalle
- `Anwender` für die Registrierung neuer Benutzer

Nur Administratoren können den Einrichtungsbereich öffnen.

## Allgemein

Unter `Allgemein` werden globale Anzeige- und Scan-Einstellungen gepflegt. Der Anwendungstitel steuert die Bezeichnung in der Navigation und auf der Startseite. Die Scan-Intervalle bestimmen, wie oft die Anwendung nach Prozess- und Medienänderungen sucht.

## Quellen löschen

Über `Quellen` kann eine Medienquelle dauerhaft entfernt werden. Der Löschvorgang läuft asynchron ab, damit die Seite nicht blockiert:

- Klick auf `Löschen` blendet die Aktionsbuttons der betroffenen Quelle aus und zeigt einen Fortschrittsbalken.
- Im Hintergrund werden alle zugehörigen Medien-Items, Collections, Verknüpfungen, Filme, Serien, Staffeln, Episoden und Genres sowie die Berechtigungen gelöscht.
- Nach erfolgreichem Löschen verschwindet die Zeile aus der Übersicht bzw. man wird auf die Übersicht zurückgeleitet.
- Bei einem Fehler wird dieser auf der Seite angezeigt und die Löschung wird nicht ausgeführt.

## Migration und Backups

Bestehende Installationen erhalten beim Datenbankupdate einen Anwendungstitel mit dem Standardwert `Martins Videosammlung`. Backups aus älteren Versionen können auch dann wiederhergestellt werden, wenn sie noch keine `UpdateSettings`-Tabelle oder keinen Anwendungstitel enthalten.
