← [Zurück zur Übersicht](index.md)

# Medienquellen — Einrichtung

## Zweck

Administratoren legen Medienquellen an und pflegen sie. Je nach Speicherort der Videodateien steht der Quelltyp `SFTP-Server` oder `Lokales Verzeichnis` zur Verfügung.

## Einstellungen

| Einstellung | Bedeutung |
|-------------|-----------|
| `Name` | Anzeigename der Quelle in der Navigation und auf der Startseite. |
| `Icon-Bild` | Optionales Bild, das als Symbol der Quelle angezeigt wird. |
| `Quelltyp` | `SFTP-Server` (entfernte Quelle) oder `Lokales Verzeichnis` (Verzeichnis auf dem Server). Nur beim Anlegen wählbar. |
| `Host` / `Port` | Serveradresse und Port der SFTP-Quelle (nur bei `SFTP-Server`; Port-Vorgabe `22`). |
| `Pfad` | Startverzeichnis auf dem SFTP-Server (nur bei `SFTP-Server`). |
| `Lokales Verzeichnis` | Absoluter Verzeichnispfad auf dem Server (nur bei `Lokales Verzeichnis`), z. B. `D:\Videos` oder `\\server\share`. |
| `Benutzername` / `Passwort` | Zugangsdaten für den SFTP-Server (nur bei `SFTP-Server`). |
| `Freigegebene Benutzer` | Benutzer, denen die Inhalte dieser Quelle angezeigt werden. |

## Vorgehen

1. Im Menü `Einrichtung` öffnen und die Kachel `Quellen` wählen.
2. `Neu` klicken.
3. `Name` vergeben und im Feld `Quelltyp` die passende Art wählen.
4. Je nach Typ die verbindungsbezogenen Felder ausfüllen:
   - `SFTP-Server`: `Host`, `Port`, `Pfad`, `Benutzername`, `Passwort`.
   - `Lokales Verzeichnis`: Verzeichnispfad im Feld `Lokales Verzeichnis` eintragen.
5. Unter `Freigegebene Benutzer` die Benutzer markieren, die Zugriff erhalten sollen.
6. `Anlegen` klicken. Die Quelle erscheint in der Übersicht mit der Typ-Kennzeichnung `SFTP` oder `Lokal`.
7. Über `Komplettscan aller Quellen` in der Übersicht wird das Einlesen der Inhalte angestoßen; ansonsten läuft es automatisch im eingestellten Scan-Intervall.

## Hinweise

- Der `Quelltyp` lässt sich nach dem Anlegen nicht mehr ändern — die Auswahl ist im Bearbeitungsdialog gesperrt. Soll der Typ gewechselt werden, muss die Quelle gelöscht und neu angelegt werden.
- Bei `Lokales Verzeichnis` prüft die Anwendung beim Speichern, ob der Pfad absolut ist, das Verzeichnis existiert und lesbar ist. Der Pfad gilt aus Sicht des Servers, nicht des eigenen Rechners — ein Pfad wie `C:\Videos` muss auf dem Server existieren.
- UNC-Pfade (`\\server\share`) sind erlaubt; der Serverprozess benötigt lesenden Zugriff auf die Freigabe.
- Über `Scan zurücksetzen` in der Übersicht kann das Einlesen einer einzelnen Quelle erneut angestoßen werden; `Löschen` entfernt die Quelle mitsamt aller eingelesenen Inhalte.
