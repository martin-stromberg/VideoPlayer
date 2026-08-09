# Backups im VideoWebPlayer

Diese Hilfe richtet sich an Administratoren. Die Backup-Funktion ist im Verwaltungsbereich unter `Backups` verfügbar und erlaubt das Erstellen, Herunterladen, Hochladen, Löschen und Wiederherstellen von Backups.

## Backup-Seite

Die Seite zeigt vorhandene Backups mit Dateiname, Generation, Erstellzeit, Größe und Gültigkeitsstatus. Pro Backup stehen Symbolbuttons zum Herunterladen, Wiederherstellen und Löschen bereit.

Nur angemeldete Benutzer mit Administrationsrechten können die Seite und die Backup-Endpunkte verwenden.

## Manuelles Backup

Mit `Backup erstellen` wird sofort ein neues Backup erzeugt. Das Backup enthält die Daten aus der Anwendungsdatenbank inklusive Benutzer- und Identity-Daten. Genre-Icons aus `wwwroot/images/genres` werden mitgesichert, wenn das Verzeichnis vorhanden ist.

Manuell erstellte Backups werden nicht automatisch durch die GVS-Aufbewahrung gelöscht.

## Automatische GVS-Backups

Im Bereich `Einstellungen` können automatische Backups aktiviert werden. Die Aufbewahrung folgt dem Großvater-Vater-Sohn-Prinzip:

- `Sohn-Aufbewahrung`: tägliche Backups, Standard `7`
- `Vater-Aufbewahrung`: wöchentliche Backups, Standard `4`
- `Großvater-Aufbewahrung`: monatliche Backups, Standard `12`

Der Hintergrunddienst prüft regelmäßig, ob ein neues automatisches Backup fällig ist. Die Aufbewahrung löscht nur automatische Backups der Generationen Sohn, Vater und Großvater. Manuelle und hochgeladene Backups bleiben erhalten.

## Speicherpfad

Der Speicherpfad wird in den Backup-Einstellungen gesetzt. Standard ist `Data/Backups` relativ zum Content Root der Anwendung. Die Anwendung muss auf diesen Ordner lesend und schreibend zugreifen können.

Alle Backups werden als ZIP-Dateien gespeichert. Der Pfad kann über die Admin-Oberfläche geändert werden; neue Operationen verwenden danach den gespeicherten Wert.

## Download

Mit dem Herunterladen-Symbol kann ein vorhandenes Backup als ZIP-Datei heruntergeladen werden. Der Download ist serverseitig auf bekannte Backup-Dateien beschränkt und nur für Administratoren erlaubt.

## Löschen

Mit dem Löschen-Symbol kann ein vorhandenes Backup dauerhaft entfernt werden. Vor dem Löschen muss die Sicherheitsabfrage bestätigt werden.

## Upload

Im Bereich `Backup hochladen` kann eine Backup-ZIP-Datei importiert werden. Es werden nur gültige Backup-ZIPs übernommen. Dazu gehören ein lesbares ZIP, ein gültiges Manifest, ein passender Provider und die erwarteten Dateninhalte.

Das Upload-Limit ist in den Einstellungen sichtbar und änderbar. Standard ist `512 MB`.

## Restore

Ein Restore ersetzt die aktuellen Anwendungsdaten durch die Daten aus dem ausgewählten Backup. Deshalb erfolgt die Wiederherstellung zweistufig:

1. Das Wiederherstellen-Symbol beim gewünschten Backup auswählen.
2. Die Sicherheitsabfrage aktiv bestätigen und `Restore starten` ausführen.

Während des Restores werden schreibende Hintergrundprozesse in der Anwendung pausiert oder am Start neuer Schreiboperationen gehindert. Laufende Operationen werden abgewartet, bevor Daten gelöscht und aus dem Backup wiederhergestellt werden.

## Admin-Konto-Erhalt

Das Konto des Administrators, der den Restore ausführt, bleibt erhalten:

- Ist das Konto im Backup enthalten, werden die Werte aus dem Backup verwendet und die Administratorrolle bleibt sichergestellt.
- Ist das Konto nicht im Backup enthalten, wird das bestehende Administratorkonto wieder angelegt und ohne Quellenzuweisung gespeichert.

So bleibt die Anwendung nach einem Restore administrierbar.

## Historie

Die Seite zeigt eine Historie der letzten Backup-, Restore- und Löschaktionen. Dort stehen Startzeit, Aktion, Datei, Ergebnis und Meldung. Fehler werden ebenfalls protokolliert und auf der Seite als kurze Meldung angezeigt.

## Bekannte technische Hinweise

- Gesichert werden Datenbankdaten und optionale Genre-Icons. Echte Mediendateien aus Medienquellen, Logs, Demo-/Seed-Dateien und externe Speicherorte werden nicht gesichert.
- Das Backup-Format ist ein ZIP mit `manifest.json`, `data.json` und optionalen Dateien unter `files/`.
- Hochgeladene ZIPs werden gegen ungültige Manifestdaten und unsichere Pfade validiert.
- Die Restore-Sperre wirkt innerhalb der laufenden Anwendung. Sie ist keine Cluster- oder Mehrprozess-Sperre für mehrere App-Instanzen.
- Backups sind nicht verschlüsselt und nicht passwortgeschützt. Der Speicherpfad sollte entsprechend geschützt werden.
