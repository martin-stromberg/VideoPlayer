# Anforderung

## Original

"Meine Backupdatei ist 3,9GB groß. Die Wiederherstellung ist zu aufwändig. Auch sie muss in einen Hintergrundprozess gesetzt werden. Auf der Webseite soll während der Wiederherstellung der Fortschritt zweistufig gezeigt werden: Datenbestand w von x sowie Datensatz y von z. Währenddessen sollen sämtliche Inhaltsseiten keine Inhalte laden, bzw. nur eine Statusantwort über die API erhalten, die Auskunft darüber gibt, dass gerade eine Wiederherstellung passiert."

## Ziel

Die Wiederherstellung großer Backup-Dateien darf den HTTP-/Blazor-Request nicht blockieren. Ein Restore wird im Hintergrund ausgeführt und sein Fortschritt wird auf der Backup-Administrationsseite angezeigt.

## Funktionale Anforderungen

- Restore eines Backup-Eintrags startet als Hintergrundprozess.
- Die Backup-Seite zeigt während des Restores einen zweistufigen Fortschritt:
  - Datenbestand `w` von `x`
  - Datensatz `y` von `z`
- Während ein Restore läuft, dürfen Inhaltsseiten keine regulären Inhalte laden.
- Während ein Restore läuft, sollen API-Anfragen statt regulärer Daten eine Statusantwort erhalten, die auf den laufenden Restore hinweist.
- Admin-Backup-Seite und notwendige Backup-Status-Endpunkte bleiben während des Restores erreichbar.

## Nicht-Ziele

- Keine Fallback-Lösung für ältere Backup-ZIP-Strukturen.
- Keine persistente Job-Wiederaufnahme nach Prozessneustart.
- Keine parallelen Restores.

## Akzeptanzkriterien

- Ein bestätigter Restore kehrt unmittelbar zur Backup-Seite zurück und startet die Wiederherstellung im Hintergrund.
- Die Backup-Seite aktualisiert den Restore-Status automatisch.
- Fortschritt enthält Tabellen-/Datenbestandsebene und Datensatzebene.
- Ein paralleler Restore wird abgelehnt.
- API-Requests auf Inhaltsdaten erhalten während des Restores eine maschinenlesbare Statusantwort.
- Nicht-Admin-Inhaltsseiten laden während des Restores keine Inhalte.
- Automatisierte Tests decken Hintergrund-Restore, Fortschritt und Inhalts-/API-Blockade ab.
