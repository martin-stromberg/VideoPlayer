← [Zurück zur Übersicht](index.md)

# Netzwerk-Erkennung — Beschreibung

## Zweck

Client-Apps sollen den VideoWebPlayer im lokalen Netzwerk finden, ohne dass der Anwender die Serveradresse eintippen muss. Dafür gibt es zwei unabhängige Erkennungswege:

- **mDNS-Ankündigung:** Der Server meldet sich selbst im Netzwerk als Dienst an — Apps sehen ihn in der Dienstsuche. Siehe die Hilfe zur [Einrichtung](../einrichtung.md), Abschnitt „Netzwerk-Erkennung (mDNS)".
- **Broadcast-Erkennung:** Eine App sendet eine Suchanfrage als Rundruf ins lokale Netzwerk; der Server antwortet dem Absender mit der Adresse, unter der er erreichbar ist.

Dieser Hilfebereich beschreibt vor allem die Broadcast-Erkennung und die Adresse, die sie meldet.

## Funktionsweise

Die gemeldete Adresse ist die öffentliche Basis-URL des Servers — vollständig mit Protokoll, Host, Port und gegebenenfalls einem Pfad, zum Beispiel `https://videos.example.com/videoplayer/`. Welche Adresse gemeldet wird, entscheidet sich bei jeder Suchanfrage neu in dieser Reihenfolge:

1. Das Feld `Öffentliche Basis-URL` unter `Einrichtung` → `Allgemein` ist ausgefüllt → diese Adresse wird gemeldet.
2. Der Betreiber hat in der Serverkonfiguration eine Basis-URL hinterlegt → diese Adresse wird gemeldet.
3. Beides ist leer → der Server leitet die Adresse automatisch ab: Er nimmt seine LAN-Adresse und den Port, unter dem er tatsächlich erreichbar ist.

Bei der automatischen Ableitung werden Adressen, die auf dem Client nicht funktionieren würden, grundsätzlich nicht gemeldet — keine `localhost`- oder `127.*`-Adressen und keine Platzhalter-Adressen. Eine Änderung des Admin-Felds wirkt sofort bei der nächsten Suchanfrage, ein Neustart ist nicht nötig.

## Beispiele

- **Direkte Installation im Heimnetz:** Kein Eintrag nötig — der Server meldet automatisch seine LAN-Adresse, z. B. `http://192.168.1.20:5000`.
- **Server unter IIS mit Unterpfad:** Der Server ist extern unter `https://server.example.com/videoplayer/` erreichbar. Diese Adresse wird im Feld `Öffentliche Basis-URL` eingetragen, weil die automatische Ableitung nur das interne Backend sieht.
- **Reverse-Proxy oder TLS-Abschluss außerhalb des Servers:** Ebenfalls eine explizite Adresse eintragen — der Server kennt die externe HTTPS-Adresse nicht selbst.

## Einschränkungen

- Die Broadcast-Erkennung erreicht nur Clients im selben Netzwerksegment; über Subnetz- oder VPN-Grenzen hinweg funktionieren Rundrufe üblicherweise nicht. Die mDNS-Ankündigung unterliegt denselben Netzwerkgrenzen.
- Die automatische Ableitung sieht nur die interne Sicht des Servers. Unter IIS, hinter einem Reverse-Proxy oder bei TLS-Abschluss außerhalb des Servers ist eine explizit eingetragene Adresse erforderlich.
- Die Ableitung wählt bevorzugt IPv4-Adressen; reine IPv6-Netze werden über eine explizit eingetragene Adresse abgedeckt.
