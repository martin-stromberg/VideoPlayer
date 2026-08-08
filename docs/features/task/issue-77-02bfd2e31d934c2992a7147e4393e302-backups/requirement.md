# Strukturierte Anforderung: Backups

## Ziel

Administratoren sollen im Webprojekt Backups der Datenbankdaten erstellen, verwalten, herunterladen, hochladen und wiederherstellen koennen. Die Backupfunktion soll in einer wiederverwendbaren Klassenbibliothek `msTools.Backup` gekapselt werden und spaeter auch in anderen Projekten nutzbar sein.

## Ausgangslage

Das Webprojekt benoetigt eine administrative Backupfunktion. Ein Backup ist ein Export der Daten aus der Datenbank. Die Backupdateien werden als ZIP-Dateien gespeichert.

## Rollen und Berechtigungen

- Nur Administratoren duerfen Backups erstellen.
- Nur Administratoren duerfen Backup-Einstellungen aendern.
- Nur Administratoren duerfen Backupdateien herunterladen.
- Nur Administratoren duerfen Backupdateien hochladen.
- Nur Administratoren duerfen Backups wiederherstellen.

## Funktionale Anforderungen

### Manuelle Backuperstellung

- Es muss im Administrationsbereich des Webprojekts eine sichtbare Aktion zum Erstellen eines Backups geben.
- Die Aktion muss ueber einen Button ausloesbar sein.
- Beim Ausloesen wird ein Datenbankexport erstellt.
- Das Ergebnis wird als ZIP-Datei gespeichert.

### Automatische Backuperstellung

- Administratoren muessen automatische, wiederholt ausgefuehrte Backups konfigurieren koennen.
- Die automatische Backuperstellung muss nach dem Grossvater-Vater-Sohn-Prinzip konfigurierbar sein.
- Die Konfiguration muss festlegen koennen, welche Backupgenerationen erhalten bleiben.
- Die automatische Backuperstellung muss die konfigurierten Aufbewahrungsregeln anwenden.

### Speicherort

- Backupdateien muessen in einem konfigurierbaren Dateisystempfad abgelegt werden.
- Der konfigurierte Pfad muss von der Anwendung fuer Lesen und Schreiben verwendet werden.
- Backupdateien muessen als ZIP-Dateien abgelegt werden.

### Download

- Administratoren muessen vorhandene Backupdateien auf der Webseite sehen koennen.
- Administratoren muessen vorhandene Backupdateien ueber die Webseite herunterladen koennen.

### Upload

- Administratoren muessen eine Backupdatei ueber die Webseite hochladen koennen.
- Hochgeladene Backupdateien muessen dem Backupbestand hinzugefuegt werden.
- Hochgeladene Dateien muessen als wiederherstellbare Backups verfuegbar sein.
- Die Uploadfunktion muss nur gueltige Backup-ZIP-Dateien akzeptieren.

### Wiederherstellung

- Administratoren muessen jedes verfuegbare Backup wiederherstellen koennen.
- Vor der Wiederherstellung muss eine Sicherheitsabfrage erfolgen.
- Nach Bestaetigung der Sicherheitsabfrage muessen Hintergrundprozesse beendet oder angehalten werden, die waehrend der Wiederherstellung Daten veraendern koennten.
- Danach muessen die vorhandenen Daten geloescht werden.
- Anschliessend muessen die Daten aus dem Backup wiederhergestellt werden.
- Die Wiederherstellung muss sicherstellen, dass das Benutzerkonto des Administrators, der die Aktion ausfuehrt, erhalten bleibt.
- Ist dieses Benutzerkonto im Backup enthalten, soll es mit den Einstellungen aus dem Backup aktualisiert werden.
- Ist dieses Benutzerkonto nicht im Backup enthalten, muss es als Konto ohne Quellenzuweisung erhalten bleiben.

### Wiederverwendbare Klassenbibliothek

- Fuer die Backupfunktionalitaet muss eine eigene Klassenbibliothek `msTools.Backup` erstellt werden.
- Die Bibliothek muss so gestaltet sein, dass sie spaeter auch in anderen Projekten verwendet werden kann.
- Die Registrierung der Backupfunktion muss einfach sein und sich am Muster `app.UseBackups(...)` orientieren.
- Die Bibliothek muss ein Interface bereitstellen oder verwenden, ueber das die hostende Anwendung die zu sichernden Daten bereitstellt.
- Die Anwendung muss eine Implementierung dieses Interfaces registrieren koennen.

## Nicht-funktionale Anforderungen

- Backup- und Restore-Vorgaenge muessen fuer Administratoren nachvollziehbar sein.
- Restore-Vorgaenge muessen gegen versehentliches Ausloesen durch eine Sicherheitsabfrage geschuetzt sein.
- Die Bibliothek `msTools.Backup` muss projektunabhaengig und wiederverwendbar gestaltet werden.
- Der Speicherpfad fuer Backups darf nicht fest im Code verdrahtet sein.
- Fehler bei Backup, Upload, Download oder Restore muessen kontrolliert behandelt und dem Administrator verstaendlich angezeigt werden.

## Daten und Artefakte

- Backup-Dateiformat: ZIP
- Backup-Inhalt: Export der Daten aus der Datenbank
- Speicherort: konfigurierbarer Pfad
- Neue Bibliothek: `msTools.Backup`
- Registrierungsbeispiel: `app.UseBackups(...)`
- Integrationsschnittstelle: Interface fuer Datenabruf und Wiederherstellung durch die Anwendung

## Akzeptanzkriterien

- Ein Administrator kann im Webprojekt per Button ein Backup erstellen.
- Ein manuell erstelltes Backup wird als ZIP-Datei im konfigurierten Pfad abgelegt.
- Ein Administrator kann automatische Backups konfigurieren.
- Die automatische Backupkonfiguration unterstuetzt das Grossvater-Vater-Sohn-Prinzip.
- Vorhandene Backupdateien werden dem Administrator auf der Webseite angezeigt.
- Ein Administrator kann vorhandene Backupdateien herunterladen.
- Ein Administrator kann eine gueltige Backup-ZIP-Datei hochladen.
- Eine hochgeladene Backupdatei steht anschliessend fuer Download und Wiederherstellung zur Verfuegung.
- Ein Administrator kann fuer jedes verfuegbare Backup eine Wiederherstellung starten.
- Vor der Wiederherstellung wird eine Sicherheitsabfrage angezeigt.
- Bei der Wiederherstellung werden relevante Hintergrundprozesse beendet oder angehalten, vorhandene Daten geloescht und Daten aus dem Backup wiederhergestellt.
- Das ausfuehrende Administratorkonto bleibt nach der Wiederherstellung erhalten.
- Ist das ausfuehrende Administratorkonto im Backup enthalten, wird es aus dem Backup aktualisiert.
- Ist das ausfuehrende Administratorkonto nicht im Backup enthalten, bleibt es ohne Quellenzuweisung erhalten.
- Die Backupfunktionalitaet liegt in einer separaten Klassenbibliothek `msTools.Backup`.
- Die Backupfunktion kann durch eine einfache Registrierung nach dem Muster `app.UseBackups(...)` in die Anwendung eingebunden werden.
- Die Anwendung kann ein Interface registrieren, ueber das die Backupkomponente Daten exportieren und wiederherstellen kann.

## Offene Punkte

- Welche konkreten Datenbankobjekte und Dateien gehoeren zum Backupumfang, falls neben Datenbankdaten weitere Artefakte benoetigt werden?
- Welche Intervalle und Aufbewahrungszahlen sollen fuer Grossvater-, Vater- und Sohn-Backups standardmaessig gelten?
- Welche Hintergrundprozesse muessen vor einer Wiederherstellung konkret beendet oder angehalten werden?
- Welche Validierung muss eine hochgeladene Backup-ZIP-Datei bestehen?
- Soll es eine Protokollierung oder Historie fuer Backup- und Restore-Aktionen geben?
- Soll der Download, Upload und Restore grosser Backupdateien besondere Fortschrittsanzeigen oder Zeitlimits erhalten?
