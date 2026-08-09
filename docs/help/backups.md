# Backups im VideoWebPlayer

Diese Hilfe richtet sich an Administratoren. Die Backup-Funktion ist im Verwaltungsbereich unter `Backups` verfuegbar und erlaubt das Erstellen, Herunterladen, Hochladen und Wiederherstellen von Backups.

## Backup-Seite

Die Seite zeigt vorhandene Backups mit Dateiname, Generation, Erstellzeit, Groesse und Gueltigkeitsstatus. Pro Backup stehen die Aktionen `Download` und, bei gueltigen Backups, `Restore` bereit.

Nur angemeldete Benutzer mit Administrationsrechten koennen die Seite und die Backup-Endpunkte verwenden.

## Manuelles Backup

Mit `Backup erstellen` wird sofort ein neues Backup erzeugt. Das Backup enthaelt die Daten aus der Anwendungsdatenbank inklusive Benutzer- und Identity-Daten. Genre-Icons aus `wwwroot/images/genres` werden mitgesichert, wenn das Verzeichnis vorhanden ist.

Manuell erstellte Backups werden nicht automatisch durch die GVS-Aufbewahrung geloescht.

## Automatische GVS-Backups

Im Bereich `Einstellungen` koennen automatische Backups aktiviert werden. Die Aufbewahrung folgt dem Grossvater-Vater-Sohn-Prinzip:

- `Sohn-Aufbewahrung`: taegliche Backups, Standard `7`
- `Vater-Aufbewahrung`: woechentliche Backups, Standard `4`
- `Grossvater-Aufbewahrung`: monatliche Backups, Standard `12`

Der Hintergrunddienst prueft regelmaessig, ob ein neues automatisches Backup faellig ist. Die Aufbewahrung loescht nur automatische Backups der Generationen Sohn, Vater und Grossvater. Manuelle und hochgeladene Backups bleiben erhalten.

## Speicherpfad

Der Speicherpfad wird in den Backup-Einstellungen gesetzt. Standard ist `Data/Backups` relativ zum Content Root der Anwendung. Die Anwendung muss auf diesen Ordner lesend und schreibend zugreifen koennen.

Alle Backups werden als ZIP-Dateien gespeichert. Der Pfad kann ueber die Admin-Oberflaeche geaendert werden; neue Operationen verwenden danach den gespeicherten Wert.

## Download

Mit `Download` kann ein vorhandenes Backup als ZIP-Datei heruntergeladen werden. Der Download ist serverseitig auf bekannte Backup-Dateien beschraenkt und nur fuer Administratoren erlaubt.

## Upload

Im Bereich `Backup hochladen` kann eine Backup-ZIP-Datei importiert werden. Es werden nur gueltige Backup-ZIPs uebernommen. Dazu gehoeren ein lesbares ZIP, ein gueltiges Manifest, ein passender Provider und die erwarteten Dateninhalte.

Das Upload-Limit ist in den Einstellungen sichtbar und aenderbar. Standard ist `512 MB`.

## Restore

Ein Restore ersetzt die aktuellen Anwendungsdaten durch die Daten aus dem ausgewaehlten Backup. Deshalb erfolgt die Wiederherstellung zweistufig:

1. `Restore` beim gewuenschten Backup auswaehlen.
2. Die Sicherheitsabfrage aktiv bestaetigen und `Restore starten` ausfuehren.

Waehrend des Restores werden schreibende Hintergrundprozesse in der Anwendung pausiert oder am Start neuer Schreiboperationen gehindert. Laufende Operationen werden abgewartet, bevor Daten geloescht und aus dem Backup wiederhergestellt werden.

## Admin-Konto-Erhalt

Das Konto des Administrators, der den Restore ausfuehrt, bleibt erhalten:

- Ist das Konto im Backup enthalten, werden die Werte aus dem Backup verwendet und die Administratorrolle bleibt sichergestellt.
- Ist das Konto nicht im Backup enthalten, wird das bestehende Administratorkonto wieder angelegt und ohne Quellenzuweisung gespeichert.

So bleibt die Anwendung nach einem Restore administrierbar.

## Historie

Die Seite zeigt eine Historie der letzten Backup- und Restore-Aktionen. Dort stehen Startzeit, Aktion, Datei, Ergebnis und Meldung. Fehler werden ebenfalls protokolliert und auf der Seite als kurze Meldung angezeigt.

## Bekannte technische Hinweise

- Gesichert werden Datenbankdaten und optionale Genre-Icons. Echte Mediendateien aus Medienquellen, Logs, Demo-/Seed-Dateien und externe Speicherorte werden nicht gesichert.
- Das Backup-Format ist ein ZIP mit `manifest.json`, `data.json` und optionalen Dateien unter `files/`.
- Hochgeladene ZIPs werden gegen ungueltige Manifestdaten und unsichere Pfade validiert.
- Die Restore-Sperre wirkt innerhalb der laufenden Anwendung. Sie ist keine Cluster- oder Mehrprozess-Sperre fuer mehrere App-Instanzen.
- Backups sind nicht verschluesselt und nicht passwortgeschuetzt. Der Speicherpfad sollte entsprechend geschuetzt werden.
