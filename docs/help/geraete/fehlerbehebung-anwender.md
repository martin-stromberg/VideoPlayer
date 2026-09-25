← [Zurück zur Übersicht](index.md)

# Geräte — Fehlerbehebung

## Der Pairing-Code wird nicht akzeptiert

**Symptom:** Die App meldet beim Einlösen des Codes einen Fehler.

**Ursache:** Der Code ist abgelaufen (standardmäßig 5 Minuten gültig), wurde bereits verwendet oder wurde nicht genau wie angezeigt eingegeben.

**Lösung:**
1. Unter `Einrichtung` > `Geräte` mit `Pairing-Code erzeugen` einen neuen Code erstellen.
2. Den Code exakt wie angezeigt und innerhalb der Gültigkeitsdauer eingeben.
3. Jeder Code ist nur einmal verwendbar — für ein weiteres Gerät einen neuen Code erzeugen.

## Die App kann sich nicht koppeln — zu viele Fehlversuche

**Symptom:** Nach mehreren fehlgeschlagenen Versuchen schlägt jede Anfrage des Geräts fehl.

**Ursache:** Die IP-Adresse des Geräts wurde wegen wiederholter Fehlversuche gesperrt.

**Lösung:**
1. `Einrichtung` > `Sicherheit` öffnen.
2. Die betroffene IP-Adresse in der Liste der gesperrten Adressen suchen und entsperren.
3. Anschließend einen neuen Pairing-Code erzeugen und das Koppeln wiederholen.

## Ein Gerät hat keinen Zugriff mehr

**Symptom:** Eine zuvor funktionierende App kann sich nicht mehr anmelden.

**Ursache:** Das Geräte-Token wurde widerrufen, oder das Gerät wurde entfernt.

**Lösung:**
1. In `Einrichtung` > `Geräte` den `Status` des Geräts prüfen — `Zugriff entzogen` bedeutet, der Zugriff wurde gesperrt.
2. Bei Widerruf bleibt die Sperre dauerhaft bestehen; für erneuten Zugriff das Gerät mit einem neuen Pairing-Code koppeln.
3. Beachten: Eine bereits laufende Anmeldesitzung bleibt nach dem Widerruf noch bis zum Ablauf ihres Anmeldetokens gültig.

## Gerät lässt sich in der Liste nicht zuordnen

**Symptom:** Mehrere Einträge in `Gekoppelte Geräte` haben ähnliche oder automatisch vergebene Namen.

**Lösung:**
1. In der betreffenden Zeile `Umbenennen` wählen und einen sprechenden Namen vergeben.
2. Anhand der Spalten `Ausgestellt` und `Zuletzt verwendet` das richtige Gerät identifizieren.

## Wann Hilfe nötig ist

Wenden Sie sich an den Betreiber der Anwendung, wenn:

- die Kachel `Geräte` unter `Einrichtung` nicht erscheint oder die Seite `Nicht autorisiert.` meldet (fehlende Administrator-Rechte),
- IP-Sperren wiederholt auftreten, obwohl der Code korrekt eingegeben wurde,
- nach einem Update die Geräteverwaltung oder die App-Anmeldung generell nicht funktioniert.
