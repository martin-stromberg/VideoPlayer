← [Zurück zur Übersicht](index.md)

# Geräte — Ablauf für Anwender

## Voraussetzungen

- Sie sind als Administrator angemeldet; der Einrichtungsbereich ist nur für Administratoren sichtbar.
- Die Client-App ist auf dem zu koppelnden Gerät installiert und erreicht den Server im Netzwerk.

## Schritt-für-Schritt-Anleitung

### 1. Geräteverwaltung öffnen

Öffnen Sie `Einrichtung` und wählen Sie die Kachel `Geräte`. Die Seite zeigt die Bereiche `Pairing-Codes` und `Gekoppelte Geräte` sowie zwei Statistik-Karten.

### 2. Pairing-Code erzeugen

Klicken Sie auf `Pairing-Code erzeugen`. Der neue Code wird einmalig im Klartext angezeigt, zusammen mit dem Hinweis `Gueltig bis <Uhrzeit>` und der Gültigkeitsdauer in Minuten.

> **Hinweis:** Notieren oder übernehmen Sie den Code sofort — er wird nur einmal angezeigt und ist standardmäßig nur 5 Minuten gültig.

### 3. Code in der App eingeben

Geben Sie den Code in der Client-App auf dem Gerät ein. Die App löst den Code gegen ein Geräte-Token ein und meldet sich danach mit diesem Token an.

> **Hinweis:** Geben Sie den Code genau wie angezeigt ein. Schlägt das Einlösen wiederholt fehl, wird die IP-Adresse des Geräts vorübergehend gesperrt (sichtbar unter `Einrichtung` > `Sicherheit`).

### 4. Koppelung prüfen

Das Gerät erscheint in der Tabelle `Gekoppelte Geräte` mit `Name`, `Ausgestellt`, `Zuletzt verwendet` und dem Status `Aktiv`. Über `Aktualisieren` laden Sie die Liste neu.

### 5. Gerät umbenennen (optional)

Klicken Sie in der Zeile des Geräts auf `Umbenennen`, passen Sie den Namen im Eingabefeld an und bestätigen Sie mit `Speichern` — oder verwerfen Sie die Änderung mit `Abbrechen`.

### 6. Gerät widerrufen (bei Bedarf)

Klicken Sie auf `Widerrufen`. Es öffnet sich der Bereich `Widerruf bestaetigen` mit dem Hinweis, dass das Gerät sofort den Zugriff verliert, bereits ausgestellte Anmeldesitzungen aber bis zum Ablauf ihres Tokens gültig bleiben. Setzen Sie die Checkbox `Widerruf von <Name> serverseitig bestaetigen` und klicken Sie `Widerruf bestaetigen`. Mit `Abbrechen` verlassen Sie den Vorgang ohne Änderung.

## Ergebnis

Das Gerät ist gekoppelt und meldet sich mit seinem eigenen Geräte-Token an. In der Übersicht sehen Sie jederzeit, welche Geräte aktiv sind und wann sie zuletzt verwendet wurden; widerrufene Geräte bleiben mit dem Status `Zugriff entzogen` in der Liste sichtbar.

## Barrierefreiheit

Die Seite enthält keine eigenen Tastaturkürzel. Alle Aktionen sind über Schaltflächen erreichbar; der Widerruf erfordert eine zusätzliche Checkbox-Bestätigung, die auch per Tastatur bedienbar ist.
