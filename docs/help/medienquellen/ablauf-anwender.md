← [Zurück zur Übersicht](index.md)

# Medienquellen — Ablauf für Anwender

## Voraussetzungen

- Sie sind als Administrator angemeldet.
- Bei einer lokalen Quelle: Das Verzeichnis existiert auf dem Server und ist für die Anwendung lesbar.

## Schritt-für-Schritt-Anleitung

### 1. Quellenübersicht öffnen

Öffnen Sie `Einrichtung` und wählen Sie die Kachel `Quellen`. Die Übersicht listet alle vorhandenen Quellen mit `Name`, `Typ` (`SFTP` oder `Lokal`), `Host`, `Port`, `Pfad` und `Benutzername`.

### 2. Neue Quelle anlegen

Klicken Sie auf `Neu`. Das Formular `Neue Quelle` wird angezeigt.

### 3. Quelltyp wählen

Wählen Sie im Feld `Quelltyp` zwischen `SFTP-Server` und `Lokales Verzeichnis`. Das Formular zeigt daraufhin nur die zum Typ passenden Felder.

> **Hinweis:** Der Quelltyp ist nur beim Anlegen wählbar. Beim späteren Bearbeiten ist das Feld gesperrt.

### 4. Felder ausfüllen

- Bei `SFTP-Server`: `Name`, `Host`, `Port`, `Pfad`, `Benutzername` und `Passwort` ausfüllen.
- Bei `Lokales Verzeichnis`: `Name` vergeben und unter `Lokales Verzeichnis` den Verzeichnispfad auf dem Server eintragen, z. B. `D:\Videos` oder `\\server\share`. Die Felder für Host, Port und Zugangsdaten entfallen.

Optional können Sie ein `Icon-Bild` hochladen und unter `Freigegebene Benutzer` die Benutzer auswählen, die die Quelle sehen dürfen.

### 5. Speichern

Klicken Sie auf `Anlegen`. Bei einer lokalen Quelle wird der Pfad dabei geprüft: Ist er ungültig, das Verzeichnis nicht vorhanden oder nicht lesbar, erscheint eine Fehlermeldung unter dem Formular und die Quelle wird nicht gespeichert. Andernfalls kehren Sie zur Übersicht zurück.

### 6. Einlesen anstoßen oder abwarten

Die Inhalte der neuen Quelle werden automatisch im Scan-Intervall eingelesen. Für ein sofortiges Einlesen klicken Sie in der Übersicht auf `Komplettscan aller Quellen`.

## Ergebnis

Die Quelle erscheint in der Übersicht mit der Typ-Kennzeichnung `SFTP` oder `Lokal`. Nach dem Einlesen stehen die gefundenen Videos, Serien und Metadaten den freigegebenen Benutzern in der Medienbibliothek zur Verfügung — unabhängig davon, ob die Quelle per SFTP oder aus einem lokalen Verzeichnis gelesen wurde.

## Barrierefreiheit

Das Formular ist ein klassisches Web-Formular mit beschrifteten Feldern und lässt sich mit der Tastatur bedienen (Tabulatortaste zwischen den Feldern, Eingabetaste zum Absenden). Eine gesperrte Auswahl wie der Quelltyp beim Bearbeiten wird als deaktiviertes Auswahlfeld dargestellt.
