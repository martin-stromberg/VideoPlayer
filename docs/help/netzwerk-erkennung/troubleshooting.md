← [Zurück zur Übersicht](index.md)

# Netzwerk-Erkennung — Fehlerbehebung

## Discovery meldet eine für Clients unbrauchbare Adresse

**Symptom:** Apps finden den Server per Broadcast, können die gemeldete Adresse aber nicht aufrufen (z. B. interner Port, `http` statt `https`, fehlender Unterpfad).

**Ursache:** Die automatische Ableitung sieht nur das interne Backend — unter IIS `OutOfProcess`, hinter einem Reverse-Proxy oder bei TLS-Terminierung außerhalb des Servers ist die externe Adresse anders. `DiscoveryResponseBuilder` kann sie nicht selbst ermitteln.

**Lösung:**
1. Externe Basis-URL im Admin-Feld `Öffentliche Basis-URL` (`/admin/program-settings`) eintragen oder `Discovery:PublicBaseUrl` setzen — z. B. `https://<site>/videoplayer/`.
2. Der Admin-Wert wirkt sofort, der Konfigurationswert ab dem nächsten Start.

## App findet den Server gar nicht

**Symptom:** Der Broadcast bleibt unbeantwortet — keine `VIDEOWEBPLAYER_SERVER`-Antwort.

**Ursache:** UDP-Port 5001 ist in der Firewall nicht offen, der Client liegt in einem anderen Netzwerksegment (Broadcasts überspringen keine Subnetzgrenzen) oder der Serverprozess läuft unter der Umgebung `Testing` — dort startet der Listener bewusst nicht (`Program.cs`).

**Lösung:**
1. Eingehende UDP-Regel für Port 5001 auf dem Server prüfen.
2. Prüfen, ob Client und Server im selben Subnetz liegen; andernfalls die Adresse manuell in der App eintragen.
3. In der Entwicklung sicherstellen, dass nicht die Umgebung `Testing` gesetzt ist.

## Server startet nicht — Validierungsfehler zu `Discovery:PublicBaseUrl`

**Symptom:** Der Start bricht mit einer Options-Validierungsfehlermeldung `Discovery:PublicBaseUrl muss eine absolute http- oder https-URL sein …` ab.

**Ursache:** `Discovery:PublicBaseUrl` ist gesetzt, aber keine absolute `http`-/`https`-URI — `DiscoveryOptionsValidator` läuft mit `ValidateOnStart` (fail-fast).

**Lösung:** Wert korrigieren (absolute URI inkl. Schema) oder auf `null` setzen bzw. entfernen.

## Speichern der Programmeinstellungen zeigt einen Fehler zur Basis-URL

**Symptom:** Auf `/admin/program-settings` erscheint beim Speichern eine `alert-danger`-Meldung `Die öffentliche Basis-URL muss eine absolute http- oder https-URL sein …`.

**Ursache:** Die serverseitige Prüfung in `ProgramSettingsService.UpdateGeneralSettingsAsync` hat den Wert über `DiscoveryUrlRules` abgelehnt und `DiscoveryUrlValidationException` geworfen — etwa, wenn die Formularvalidierung umgangen wurde.

**Lösung:** Feld korrigieren (absolute `http`-/`https`-Adresse) oder leeren und erneut speichern.

## Nach einem Restore meldet der Server die Adresse des alten Hosts

**Symptom:** Ein Backup wurde auf einem anderen Server wiederhergestellt; die Discovery-Antwort enthält noch die Basis-URL des Quellsystems.

**Ursache:** `Setups.DiscoveryPublicBaseUrl` wird in Backups mitgesichert und beim Restore übernommen.

**Lösung:** Das Feld `Öffentliche Basis-URL` an den neuen Server anpassen oder leeren — beim Leeren greifen `Discovery:PublicBaseUrl` bzw. die automatische Ableitung. Alt-Backups ohne die Spalte stellen sie als ungesetzt wieder her (`OptionalRestoreColumns`).

## Discovery-Antwort kommt, enthält aber nicht den eingetragenen Admin-Wert

**Symptom:** Trotz gesetztem Admin-Feld wird eine andere Adresse gemeldet.

**Ursache:** Der Admin-Wert ist ungültig (kein absoluter `http`/`https`-Wert) — `DiscoveryResponseBuilder` fällt dann defensiv auf die nächste Stufe —, oder der Datenbankzugriff schlug fehl; `DiscoveryBaseUrlResolver` loggt in dem Fall eine Warning `Admin-Basis-URL konnte nicht gelesen werden …` und arbeitet fail-open ohne Admin-Override.

**Lösung:**
1. Server-Log auf `[DiscoveryBaseUrlResolver]`-Warnings prüfen (DB- bzw. DNS-Fehler).
2. Den gespeicherten Wert im Admin-Feld kontrollieren.
