← [Zurück zur Übersicht](index.md)

# Geräte — Beschreibung

## Zweck

Das Geräte-Pairing koppelt Client-Apps (z. B. die TV-App) individuell an den VideoWebPlayer. Statt eines gemeinsamen Zugangsschlüssels für alle Geräte erhält jedes gekoppelte Gerät ein eigenes Geräte-Token, das sich gezielt widerrufen lässt — etwa wenn ein Gerät verloren geht oder ausgemustert wird.

## Funktionsweise

Die Verwaltung liegt im Einrichtungsbereich unter `Einrichtung` > `Geräte` und ist nur für Administratoren sichtbar.

- Über `Pairing-Code erzeugen` wird ein kurzlebiger Einmal-Code erstellt. Er wird genau einmal im Klartext angezeigt — zusammen mit der Angabe, bis wann er gültig ist (standardmäßig 5 Minuten). Danach ist der Code nicht mehr einsehbar.
- Der Code wird in der App eingegeben. Die App tauscht ihn selbstständig gegen ein Geräte-Token; die Übertragung ist dabei auf Anwendungsebene verschlüsselt.
- Die Tabelle `Pairing-Codes` zeigt aktive Codes nur mit ihren Metadaten (`Erstellt`, `Gueltig bis`, `Ersteller`).
- Die Tabelle `Gekoppelte Geräte` listet jedes Gerät mit `Name`, `Ausgestellt`, `Zuletzt verwendet` und `Status` (`Aktiv` oder `Widerrufen`). Über `Umbenennen` lässt sich der Anzeigename anpassen, über `Widerrufen` wird der Zugriff des Geräts gesperrt.
- Der Widerruf muss über eine Bestätigungs-Checkbox (`Widerruf von <Name> serverseitig bestaetigen`) und die Schaltfläche `Widerruf bestaetigen` explizit bestätigt werden.
- Zwei Statistik-Karten oben auf der Seite zeigen die Anzahl gekoppelter Geräte (mit der Zahl der aktiven) und die Anzahl aktiver Codes.

## Beispiele

- Eine neue TV-App soll Zugriff erhalten: Administrator erzeugt einen Pairing-Code, gibt ihn in der App ein — das Gerät erscheint anschließend in der Liste `Gekoppelte Geräte`.
- Ein Tablet geht verloren: Der Administrator widerruft das Gerät in der Liste; das Gerät kann sich danach nicht mehr anmelden.
- Mehrere namenlose Geräte sind schwer zu unterscheiden: Über `Umbenennen` erhält jedes Gerät einen sprechenden Namen.

## Einschränkungen

- Ein Pairing-Code kann nur einmal eingelöst werden und läuft standardmäßig nach 5 Minuten ab. Danach muss ein neuer Code erzeugt werden.
- Der Klartext-Code ist nur direkt nach dem Erzeugen sichtbar; ein verlorener Code kann nicht erneut angezeigt werden.
- Der Widerruf sperrt das Geräte-Token sofort. Bereits angemeldete App-Sitzungen bleiben jedoch bis zum Ablauf ihres Anmeldetokens (12 Stunden) gültig.
- Zu viele Fehlversuche beim Einlösen sperren die IP-Adresse des Geräts; die Sperre ist unter `Einrichtung` > `Sicherheit` sichtbar und kann dort aufgehoben werden.
- Geräte-Tokens gelten ausschließlich für die App-Anmeldung; sie ersetzen keine Benutzeranmeldung.
