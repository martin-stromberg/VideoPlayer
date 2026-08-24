# Anforderung: Aufräumen der Backupverwaltung

## Ausgangslage

Die vorhandene Admin-Seite `/admin/backups` ist funktional umfangreich, aber unstrukturiert aufgebaut. Die Bereiche Statistiken, Backup-Liste, Upload, Konfiguration und Historie sind durcheinander angeordnet, was die Bedienung erschwert.

## Ziel

Die Backup-Verwaltungsseite soll nach dem beigefügten Designentwurf (`stitch_private_media_library-backup`) neu strukturiert und visuell aufgeräumt werden. Dabei bleibt die bestehende Funktionalität erhalten.

## UI-Struktur aus dem Designentwurf

1. **Seitenkopf**
   - Kicker: „Verwaltung“
   - Titel: „System Backups“
   - Primäraktion: „Neues Backup" (roter Button mit Plus-Icon)

2. **Statistikleiste (3 Karten)**
   - „Total Backups" – Anzahl der vorhandenen Backups
   - „Speicherplatz" – Belegter Speicherplatz aller Backups
   - „Automatisch" – Status der automatischen Backups inkl. Zeitintervall

3. **Zweispaltiges Layout**
   - **Linke Spalte (2/3): „Letzte Backups"**
     - Liste der letzten Backups mit Dateiname, Datum, Größe, Status
     - Aktionen pro Backup: Wiederherstellen, Löschen
     - Link „Alle Backups anzeigen"
   - **Rechte Spalte (1/3): „Konfiguration"**
     - Speicherpfad
     - Aufbewahrungseinstellungen (GVS-Prinzip besteht weiter)
     - Automatisches Backup aktivieren
     - Speichern-Button

4. **Visuelle Gestaltung**
   - Dunkles, aufgeräumtes Layout entsprechend dem bestehenden Admin-Console-Design
   - Klare Karten-Struktur mit gut lesbarer Typografie
   - Icons für Aktionen und Status

## Funktionalität, die erhalten bleiben muss

- Anzeige nur für Administratoren
- Erstellen, Herunterladen, Hochladen, Wiederherstellen und Löschen von Backups
- Konfiguration des Speicherpfads und der automatischen Backups
- Anzeige des Wiederherstellungs- und Backup-Fortschritts
- Sicherheitsabfragen für Restore und Löschen
- Historie der Backup-Aktionen

## Nichtfunktionale Anforderungen

- Keine Änderung des Datenmodells oder der Backup-Logik
- Responsive Darstellung
- Barrierefreie Bedienung beibehalten
