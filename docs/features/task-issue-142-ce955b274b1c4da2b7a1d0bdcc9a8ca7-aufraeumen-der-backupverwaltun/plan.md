# Umsetzungsplan – Aufräumen der Backupverwaltung

## Ziel

Die Seite `/admin/backups` wird nach dem Designentwurf neu strukturiert, ohne die bestehende Backup-Logik oder das Datenmodell zu ändern.

## Entscheidungen

- **Layout**: Seite erhält einen Header mit Kicker, Titel und primärem CTA, gefolgt von drei Statistikkarten und einem 2/3 + 1/3-Grid.
- **Linke Spalte (2/3)**: Letzte Backups als Liste, gefolgt von Upload und Historie.
- **Rechte Spalte (1/3)**: Konfiguration mit Speicherpfad, GVS-Aufbewahrung, automatischem Backup und Speichern-Button.
- **Datenmodell**: Keine Änderungen. `BackupSettings` bleibt unverändert.
- **Farbschema**: Bleibt im bestehenden Admin-Console-Design; Karten-/Layout-Struktur wird an den Entwurf angeglichen.
- **Upload/Historie**: Werden behalten und unter der Backup-Liste positioniert, um Datenverluste oder Funktionseinschränkungen zu vermeiden.

## Offene Punkte

Keine. Die Entscheidungen sind getroffen und im Plan dokumentiert.

## Schritte

1. **`Backups.razor` neu strukturieren**
   - Header mit Kicker, Titel und „Neues Backup"-Button oben.
   - Statistikkarten in einer Reihe (Total Backups, Speicherplatz, Automatisch).
   - Hauptgrid: 2/3 + 1/3.
   - Linke Spalte:
     - „Letzte Backups"-Sektion als Liste statt Karten-Grid.
     - Pro Backup: Icon/Thumbnail, Dateiname, Datum, Größe, Status, Aktionen.
     - Link „Alle Backups anzeigen" (optional, falls Pagination geplant; aktuell werden alle angezeigt).
     - Upload-Bereich darunter.
     - Historie ganz unten links.
   - Rechte Spalte:
     - „Konfiguration"-Sektion mit Formular.
     - Speicherpfad-Eingabe.
     - GVS-Aufbewahrung als kompaktes Grid (Sohn/Vater/Großvater).
     - Automatisches Backup Toggle.
     - Upload-Limit (ausgeblendet oder als Expertenfeld, je nach Raum).
     - Speichern-Button.

2. **`app.css` anpassen**
   - Neue/ergänzte Klassen für das Zweispalten-Layout.
   - Stile für die Backup-Liste (Listeneinträge mit Icon und Aktionen).
   - Stile für Statistikkarten so anpassen, dass sie visuell dem Entwurf entsprechen.

3. **Dokumentation aktualisieren**
   - `docs/help/backups.md` an die neue UI-Struktur anpassen.

4. **Tests ausführen**
   - `dotnet test` für relevante Backup-Projekte.
   - Bestehende Tests dürfen nicht brechen.

## E2E-/UI-Tests

- Navigationspfad: Admin-Login → `/admin/backups` lädt ohne Fehler.
- Interaktion: Klick auf „Neues Backup" startet Backup.
- Interaktion: Einstellungen speichern aktualisiert `BackupSettings`.
- Interaktion: Backup-Liste zeigt Restore-/Delete-Buttons und Sicherheitsabfragen.

## Risiken

- Verlust von Funktionalität durch Refactoring der Razor-Seite. Abhilfe: Code-Behavior gezielt prüfen.
- Unterschiedliche Bildschirmgrößen. Abhilfe: Responsive Grid mit `grid-template-columns` und `flex-wrap`.
