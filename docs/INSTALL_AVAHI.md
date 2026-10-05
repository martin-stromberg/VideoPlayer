# Avahi-Integration für VideoWebPlayer (Linux)

> **Hinweis:** Seit der Einführung des eingebauten mDNS-Advertisement ist Avahi **nicht mehr der
> Standardweg**. Der VideoWebPlayer-Server kündigt sich ohne Zusatzsoftware selbst per mDNS
> (Diensttyp `_videowebplayer._tcp.local.`, UDP-Port 5353) im lokalen Netzwerk an — plattformübergreifend
> unter Windows und Linux, gesteuert über die Konfiguration `Mdns:*` und den Admin-Schalter
> „Netzwerk-Erkennung (mDNS)" unter `Einrichtung` → `Allgemein`. Diese Anleitung bleibt als
> **Alternativweg** bestehen, z. B. wenn das Advertisement bewusst über den OS-Daemon laufen soll.

Diese Anleitung beschreibt, wie du Avahi auf einem Linux-Server installierst und konfigurierst, damit der VideoWebPlayer-Webserver per mDNS (Bonjour/Zeroconf) im lokalen Netzwerk automatisch gefunden werden kann.

## Voraussetzungen
- Linux-Server (z. B. Ubuntu, Debian, Fedora)
- VideoWebPlayer läuft als Webserver (z. B. auf Port 5000)

## Schritt 0: Doppel-Advertisement vermeiden

Das eingebaute mDNS-Advertisement des Servers ist standardmäßig aktiv (`Mdns:Enabled`, Default `true`,
zusätzlich der Admin-Schalter „Server per mDNS im Netzwerk ankündigen"). Wird zusätzlich eine statische
Avahi-Dienstdatei für denselben Server eingerichtet, erscheint der Server **zweimal** in der
Dienstsuche der Clients. Vor dem Fortfahren daher eine der beiden Varianten wählen:

- **In-App-Advertisement nutzen (empfohlen):** Keine Avahi-Dienstdatei anlegen bzw. eine vorhandene
  `/etc/avahi/services/videowebplayer.service` entfernen und `avahi-daemon` neu laden. Diese Anleitung
  ist dann nicht weiter nötig.
- **Avahi-Advertisement nutzen:** Das In-App-Advertisement abschalten — `Mdns:Enabled=false` in
  `appsettings.Local.json` (bzw. Umgebungsvariable `Mdns__Enabled=false`) setzen. Der Admin-Schalter
  allein genügt hierfür nicht zuverlässig, da ein Administrator ihn jederzeit wieder aktivieren kann;
  `Mdns:Enabled` ist der Betreiber-Master-Switch.

## Schritt 1: Avahi installieren

### Ubuntu/Debian
```sh
sudo apt update
sudo apt install avahi-daemon avahi-utils
```

### Fedora
```sh
sudo dnf install avahi avahi-tools
```

## Schritt 2: Avahi-Dienst aktivieren und starten
```sh
sudo systemctl enable avahi-daemon
sudo systemctl start avahi-daemon
```

## Schritt 3: mDNS-Service für VideoWebPlayer registrieren

Erstelle die Datei `/etc/avahi/services/videowebplayer.service` mit folgendem Inhalt:

```xml
<service-group>
  <name>VideoWebPlayer</name>
  <service>
    <type>_videowebplayer._tcp</type>
    <port>5000</port>
    <txt-record>path=/</txt-record>
    <txt-record>app=VideoWebPlayer</txt-record>
  </service>
</service-group>
```

Passe `<port>` ggf. an den tatsächlichen Port deines Webservers an. Der Diensttyp
`_videowebplayer._tcp` und die TXT-Records entsprechen dem eingebauten Advertisement, damit sich
beide Wege für Clients gleich verhalten.

## Schritt 4: Avahi-Dienst neu laden
```sh
sudo systemctl restart avahi-daemon
```

## Schritt 5: Überprüfung

Führe auf einem anderen Rechner im Netzwerk aus:
```sh
avahi-browse -a | grep VideoWebPlayer
```

Du solltest den Dienst sehen, z. B.:
```
+   eth0 IPv4 VideoWebPlayer _videowebplayer._tcp local
```

## Hinweise
- Der Service ist jetzt per mDNS im lokalen Netzwerk sichtbar und kann von Clients gefunden werden.
- Firewall: UDP-Port 5353 muss für mDNS offen sein (eingehend und ausgehend, Multicast 224.0.0.251; meist Standard).
- Als Fallback-Erkennung bleibt zusätzlich der UDP-Broadcast-Listener des Servers auf Port 5001
  aktiv (`VIDEOWEBPLAYER_DISCOVERY` → `VIDEOWEBPLAYER_SERVER:<adresse>`) — unabhängig davon, welcher
  mDNS-Weg gewählt wird.
- Für eigene Service-Typen einfach `<type>` anpassen und Client entsprechend konfigurieren.

---

**Nächste Schritte:**
- Implementiere die mDNS-Discovery in externen Clients.
- Implementiere optional einen UDP-Listener für Discovery als Fallback
