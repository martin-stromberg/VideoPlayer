← [Zurück zur Übersicht](index.md)

# Netzwerk-Erkennung — Fehlerbehebung für Anwender

Diese Hilfe richtet sich an Administratoren, die die Server-Erkennung betreuen.

## Die App findet den Server nicht

- Prüfen, ob die App und der Server im selben Netzwerk sind — die Erkennung funktioniert nicht über Subnetz- oder VPN-Grenzen hinweg.
- Beim Betreiber rückfragen, ob der UDP-Port 5001 auf dem Server in der Firewall freigegeben ist (für die mDNS-Erkennung zusätzlich Port 5353).
- Alternativ kann die Serveradresse in der App meist auch manuell eingetragen werden.

## Die gemeldete Adresse funktioniert nicht

- Öffnen Sie `Einrichtung` → `Allgemein` und tragen Sie im Feld `Öffentliche Basis-URL` die Adresse ein, unter der die App den Server tatsächlich erreicht — zum Beispiel die externe Adresse Ihrer IIS-Site inklusive Unterpfad.
- Das ist regelmäßig nötig, wenn der Server hinter IIS, einem Reverse-Proxy oder einer Firewall mit TLS-Abschluss betrieben wird: Die automatische Ermittlung kennt nur die interne Adresse.
- Nach dem Speichern gilt die Adresse sofort; die App muss die Suche lediglich erneut starten.

## Beim Speichern erscheint eine Fehlermeldung zur Adresse

- Die Eingabe muss eine vollständige Adresse mit `http://` oder `https://` sein — zum Beispiel `https://videos.example.com/videoplayer/`.
- Reine Rechnernamen oder IP-Adressen ohne Protokoll werden nicht angenommen.
- Soll keine feste Adresse gemeldet werden, das Feld einfach leer lassen — dann wird die Adresse automatisch ermittelt.

## Nach der Übernahme eines Backups zeigt die Adresse auf den alten Server

- Die eingetragene Adresse wird im Backup mitgespeichert. Nach einer Wiederherstellung auf einem anderen Gerät das Feld `Öffentliche Basis-URL` unter `Einrichtung` → `Allgemein` an die neue Adresse anpassen oder leeren.
