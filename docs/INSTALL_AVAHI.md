# Avahi-Integration für VideoWebPlayer (Linux)

Diese Anleitung beschreibt, wie du Avahi auf einem Linux-Server installierst und konfigurierst, damit der VideoWebPlayer-Webserver per mDNS (Bonjour/Zeroconf) im lokalen Netzwerk automatisch gefunden werden kann.

## Voraussetzungen
- Linux-Server (z. B. Ubuntu, Debian, Fedora)
- VideoWebPlayer läuft als Webserver (z. B. auf Port 5000)

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
    <type>_http._tcp</type>
    <port>5000</port>
    <txt-record>path=/</txt-record>
  </service>
</service-group>
```

Passe `<port>` ggf. an den tatsächlichen Port deines Webservers an.

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
+   eth0 IPv4 VideoWebPlayer _http._tcp local
```

## Hinweise
- Der Service ist jetzt per mDNS im lokalen Netzwerk sichtbar und kann von Clients gefunden werden.
- Firewall: UDP-Port 5353 muss für mDNS offen sein (meist Standard).
- Für eigene Service-Typen (z. B. `_videowebplayer._tcp`) einfach `<type>` anpassen und Client entsprechend konfigurieren.

---

**Nächste Schritte:**
- Implementiere die mDNS-Discovery in externen Clients.
- Implementiere optional einen UDP-Listener für Discovery als Fallback
