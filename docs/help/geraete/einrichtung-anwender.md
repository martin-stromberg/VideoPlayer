← [Zurück zur Übersicht](index.md)

# Geräte — Einrichtung

## Zweck

Auf der Seite `Einrichtung` > `Geräte` richten Administratoren die Kopplung von Client-Apps ein und verwalten bereits gekoppelte Geräte. In der Oberfläche selbst gibt es keine dauerhaften Einstellungen — Länge und Gültigkeitsdauer der Codes sind vom Betreiber vorgegeben.

## Einstellungen

| Einstellung | Bedeutung |
|-------------|-----------|
| `Pairing-Code erzeugen` | Erstellt einen neuen Einmal-Code; der Klartext wird nur einmal angezeigt. |
| `Umbenennen` | Ändert den Anzeigenamen eines gekoppelten Geräts (leere Namen sind nicht zulässig). |
| `Widerrufen` | Sperrt das Geräte-Token des Geräts sofort; erfordert eine Bestätigung per Checkbox. |
| `Aktualisieren` | Lädt die Listen der aktiven Codes und gekoppelten Geräte neu. |

## Vorgehen

1. Als Administrator `Einrichtung` > `Geräte` öffnen.
2. `Pairing-Code erzeugen` klicken und den angezeigten Code notieren.
3. Den Code innerhalb der angezeigten Gültigkeitsdauer in der App eingeben.
4. In der Tabelle `Gekoppelte Geräte` prüfen, ob das Gerät mit Status `Aktiv` erscheint.

## Hinweise

- Ein Pairing-Code ist nur einmal verwendbar und läuft nach kurzer Zeit ab (standardmäßig 5 Minuten). Bei Ablauf einfach einen neuen Code erzeugen.
- Wiederholte Fehlversuche beim Einlösen sperren die IP-Adresse des Geräts. Gesperrte Adressen werden unter `Einrichtung` > `Sicherheit` angezeigt und können dort entsperrt werden.
- Der Widerruf wirkt sofort auf das Geräte-Token; bereits angemeldete App-Sitzungen bleiben bis zum Ablauf ihres Anmeldetokens gültig.
