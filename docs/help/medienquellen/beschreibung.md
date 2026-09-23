← [Zurück zur Übersicht](index.md)

# Medienquellen — Beschreibung

## Zweck

Medienquellen definieren, woher der VideoWebPlayer seine Videos, Serien und zugehörigen Metadaten (NFO-Dateien, Poster, Schauspielerbilder) bezieht. Jede Quelle wird einmal in der Verwaltung angelegt und danach regelmäßig eingelesen; die gefundenen Inhalte stehen den freigegebenen Benutzern in der Medienbibliothek zur Verfügung.

## Funktionsweise

Quellen werden im Einrichtungsbereich unter `Einrichtung` > `Quellen` verwaltet (nur für Administratoren). Beim Anlegen einer Quelle über `Neu` wird im Feld `Quelltyp` zwischen zwei Arten gewählt:

- `SFTP-Server`: Die Quelle liegt auf einem entfernten Server, der per SFTP erreicht wird. Erforderlich sind `Host`, `Port`, `Pfad` auf dem Server sowie `Benutzername` und `Passwort`.
- `Lokales Verzeichnis`: Die Quelle ist ein Verzeichnis im Dateisystem des Servers, auf dem der VideoWebPlayer läuft. Erforderlich sind nur `Name` und das Feld `Lokales Verzeichnis` mit dem Verzeichnispfad; die Felder für Host, Port, Benutzername und Passwort werden ausgeblendet. Neben lokalen Laufwerkspfaden sind auch Netzwerkpfade (UNC-Pfade wie `\\server\share`) zulässig.

Das Formular zeigt je nach gewähltem Quelltyp nur die passenden Felder an. Beim Speichern eines lokalen Verzeichnisses wird geprüft, ob der Pfad ein absoluter Pfad ist, das Verzeichnis existiert und darauf zugegriffen werden kann; andernfalls erscheint eine Fehlermeldung und die Quelle wird nicht gespeichert.

In der Quellenübersicht kennzeichnet die Spalte `Typ` jede Quelle als `SFTP` oder `Lokal`. Für lokale Quellen bleiben die Spalten `Host`, `Port` und `Benutzername` leer (Anzeige `—`), die Spalte `Pfad` enthält den Verzeichnispfad.

Nach dem Einlesen verhalten sich beide Quelltypen für den Anwender identisch: Scan, Klassifizierung (NFO-Dateien, Poster, Schauspielerbilder), Streaming und Download laufen über denselben Ablauf.

## Beispiele

- Ein NAS im Heimnetz ist per Netzwerkfreigabe erreichbar: Als Quelltyp `Lokales Verzeichnis` wählen und den UNC-Pfad `\\nas\videos` eintragen.
- Die Videodateien liegen direkt auf dem Server, auf dem der VideoWebPlayer installiert ist: `Lokales Verzeichnis` mit Pfad `D:\Videos`.
- Die Videodateien liegen auf einem entfernten Linux-Server: Quelltyp `SFTP-Server` mit Host, Port, Pfad und Zugangsdaten.

## Einschränkungen

- Der Quelltyp kann nur beim Anlegen gewählt werden. Bei bestehenden Quellen ist die Auswahl gesperrt; ein Wechsel des Typs ist nicht vorgesehen.
- Lokale Verzeichnisse werden mit den Rechten des Serverprozesses gelesen. Das Verzeichnis muss für den Dienstbenutzer der Anwendung lesbar sein.
- Einträge, deren Name mit einem Punkt beginnt (z. B. `.actors` oder versteckte Dateien), sowie Verzeichnis-Verknüpfungen (Junctions/Symlinks) werden beim Einlesen übersprungen.
- Dateizugriffe sind auf das konfigurierte Quellverzeichnis beschränkt; Pfade, die aus dem Verzeichnis herausführen, werden nicht gelesen.
- Ein entfernter Verzeichnistyp „FTP" ist nicht verfügbar; für Fernquellen steht ausschließlich `SFTP-Server` zur Verfügung.
