# Backups im VideoWebPlayer

Diese Hilfe richtet sich an Administratoren. Die Backup-Funktion ist im Einrichtungsbereich unter `Einrichtung` > `Backups` verfügbar und erlaubt das Erstellen, Herunterladen, Hochladen, Löschen und Wiederherstellen von Backups.

## Backup-Seite

Die Seite `/admin/backups` ist in drei Bereiche gegliedert:

1. **Seitenkopf**: Zeigt den Titel „System Backups" und den Button „Neues Backup" zum Erstellen eines manuellen Backups.
2. **Statistikleiste**: Zeigt die Anzahl vorhandener Backups, den belegten Speicherplatz und den Status der automatischen Backups.
3. **Zweispaltiges Layout**:
   - **Linke Spalte**: Liste der letzten Backups mit Dateiname, Erstellzeit, Größe und Status. Pro Backup stehen Symbolbuttons zum Herunterladen, Wiederherstellen und Löschen bereit. Darunter befinden sich der Bereich „Backup hochladen" und die „Historie".
   - **Rechte Spalte**: Konfiguration mit Speicherpfad, Aufbewahrungseinstellungen (Sohn/Vater/Großvater), automatischem Backup und Upload-Limit.

Nur angemeldete Benutzer mit Administrationsrechten können die Seite und die Backup-Endpunkte verwenden.

## Manuelles Backup

Mit `Backup erstellen` wird ein neues Backup im Hintergrund gestartet. Die Seite zeigt den Status des laufenden Backups an und aktualisiert ihn automatisch. Das Backup enthält die Daten aus der Anwendungsdatenbank inklusive Benutzer- und Identity-Daten. Genre-Icons aus `wwwroot/images/genres` werden mitgesichert, wenn das Verzeichnis vorhanden ist.

Manuell erstellte Backups werden nicht automatisch durch die GVS-Aufbewahrung gelöscht.

## Automatische GVS-Backups

Im Bereich `Einstellungen` können automatische Backups aktiviert werden. Die Aufbewahrung folgt dem Großvater-Vater-Sohn-Prinzip:

- `Sohn-Aufbewahrung`: tägliche Backups, Standard `7`
- `Vater-Aufbewahrung`: wöchentliche Backups, Standard `4`
- `Großvater-Aufbewahrung`: monatliche Backups, Standard `12`

Der Hintergrunddienst prüft regelmäßig, ob ein neues automatisches Backup fällig ist. Die Aufbewahrung löscht nur automatische Backups der Generationen Sohn, Vater und Großvater. Manuelle und hochgeladene Backups bleiben erhalten.

## Speicherpfad

Der Speicherpfad wird in den Backup-Einstellungen gesetzt. Standard ist `Data/Backups` relativ zum Content Root der Anwendung. Die Anwendung muss auf diesen Ordner lesend und schreibend zugreifen können.

Alle Backups werden als `.bak`-Dateien gespeichert. Der Pfad kann über die Admin-Oberfläche geändert werden; neue Operationen verwenden danach den gespeicherten Wert.

## Download

Mit dem Herunterladen-Symbol kann ein vorhandenes Backup als `.bak`-Datei heruntergeladen werden. Der Download ist serverseitig auf bekannte Backup-Dateien beschränkt und nur für Administratoren erlaubt.

## Löschen

Mit dem Löschen-Symbol kann ein vorhandenes Backup dauerhaft entfernt werden. Vor dem Löschen muss die Sicherheitsabfrage bestätigt werden.

## Upload

Im Bereich `Backup hochladen` kann eine Backup-Datei (`.bak`) importiert werden. Es werden nur gültige `.bak`-Backups übernommen. Dazu gehören ein lesbares Archiv, ein gültiges `manifest.json`, ein passender Provider und die erwarteten Dateninhalte.

Das Upload-Limit ist in den Einstellungen sichtbar und änderbar. Standard ist `512 MB`.

## Restore

Ein Restore ersetzt die aktuellen Anwendungsdaten durch die Daten aus dem ausgewählten Backup. Deshalb erfolgt die Wiederherstellung zweistufig:

1. Das Wiederherstellen-Symbol beim gewünschten Backup auswählen.
2. Die Sicherheitsabfrage aktiv bestätigen und `Restore starten` ausführen.

Der Restore läuft im Hintergrund. Die Backup-Seite zeigt währenddessen den Fortschritt zweistufig an: aktueller Datenbestand `w von x` und aktueller Datensatz `y von z`.

Während des Restores werden schreibende Hintergrundprozesse in der Anwendung pausiert oder am Start neuer Schreiboperationen gehindert. Laufende Operationen werden abgewartet, bevor Daten gelöscht und aus dem Backup wiederhergestellt werden. Inhaltsseiten werden während der Wiederherstellung nicht regulär geladen. API-Anfragen auf Inhaltsdaten erhalten stattdessen eine Statusantwort mit Hinweis auf den laufenden Restore.

## Admin-Konto-Erhalt

Das Konto des Administrators, der den Restore ausführt, bleibt erhalten:

- Ist das Konto im Backup enthalten, werden die Werte aus dem Backup verwendet und die Administratorrolle bleibt sichergestellt.
- Ist das Konto nicht im Backup enthalten, wird das bestehende Administratorkonto wieder angelegt und ohne Quellenzuweisung gespeichert.

So bleibt die Anwendung nach einem Restore administrierbar.

## Historie

Die Seite zeigt eine Historie der letzten Backup-, Restore- und Löschaktionen. Dort stehen Startzeit, Aktion, Datei, Ergebnis und Meldung. Fehler werden ebenfalls protokolliert und auf der Seite als kurze Meldung angezeigt.

## Bekannte technische Hinweise

- Gesichert werden Datenbankdaten und optionale Genre-Icons. Echte Mediendateien aus Medienquellen, Logs, Demo-/Seed-Dateien und externe Speicherorte werden nicht gesichert.
- Das Backup-Format ist eine `.bak`-Datei, die ein objektbasiertes Archiv enthält. Sie besteht aus einem `manifest.json` und einem oder mehreren Backup-Objekten. Das VideoWebPlayer-Datenbank-Objekt trägt den Namen `videowebplayer/database` und den Content-Type `VideoWebPlayer:Database`; es enthält wiederum ein `index.json` sowie die Tabellen-Payloads der Anwendungsdatenbank.
- Backups aus älteren Versionen ohne `UpdateSettings`-Tabelle oder ohne Anwendungstitel in `Setups` können wiederhergestellt werden. Fehlende Werte werden beim Restore mit aktuellen Standardwerten ergänzt.
- Hochgeladene `.bak`-Dateien werden gegen ungültige Manifestdaten und unsichere Pfade validiert.
- Die Restore-Sperre wirkt innerhalb der laufenden Anwendung. Sie ist keine Cluster- oder Mehrprozess-Sperre für mehrere App-Instanzen.
- Backups sind nicht verschlüsselt und nicht passwortgeschützt. Der Speicherpfad sollte entsprechend geschützt werden.
