← [Zurück zur Übersicht](index.md)

# Netzwerk-Erkennung — API

## Übersicht

Die Broadcast-Erkennung ist eine öffentliche, unauthentifizierte UDP-Schnittstelle — ein reines Netzwerk-Protokoll ohne HTTP-Endpunkt, `X-API-Key` oder `Authorization`. Der Kanal ist unabhängig von den mDNS-Schaltern dauerhaft aktiv (außer unter der Umgebung `Testing`, siehe [Technischer Ablauf](ablauf-technisch.md)).

## Protokoll

| Richtung | Transport | Inhalt |
|----------|-----------|--------|
| Client → Server | UDP-Broadcast an Port `5001` | UTF-8-Text `VIDEOWEBPLAYER_DISCOVERY` |
| Server → Client | UDP an den Absender-Endpunkt | UTF-8-Text `VIDEOWEBPLAYER_SERVER:<basis-url>` |

Der Server beantwortet ausschließlich Datagramme, deren Text exakt `VIDEOWEBPLAYER_DISCOVERY` entspricht; andere Nachrichten werden ignoriert. Die Antwort geht an `RemoteEndPoint` der Anfrage.

## Inhalt der Antwort

`<basis-url>` ist die aus Clientsicht erreichbare öffentliche Basis-URL des Servers **vollständig inklusive Schema, Host, Port und Pfadanteil**, z. B. `https://videos.example.com/videoplayer/`. Für den Server ist der Wert eine opaque Zeichenkette; die Auswertung liegt beim Client.

Auflösung pro Anfrage in dieser Vorrangreihenfolge:

1. Admin-Wert `Setups.DiscoveryPublicBaseUrl` (Feld `Öffentliche Basis-URL` unter `/admin/program-settings`), sofort wirksam ohne Neustart;
2. Konfiguration `Discovery:PublicBaseUrl` (absolute `http`-/`https`-URL, Start-Validierung);
3. automatische Ableitung `scheme://host:port` ohne Pfad — siehe [Business Rules](business-rules.md).

Loopback- (`localhost`, `127.*`, `::1`), Any- (`0.0.0.0`, `[::]`) und Wildcard-Adressen (`*`, `+`) werden nie gemeldet; die Ableitung wählt keine IPv6-Adresse.

## Client-Hinweis

Clients, die die Antwort verarbeiten, müssen einen Pfadanteil in der URL tolerieren (bei IIS-/Reverse-Proxy-Deployments kann die gemeldete Basis-URL z. B. `https://<site>/videoplayer/` lauten). Die Umstellung der MAUI-App `VideoPlayer.Maui` erfolgt im App-Repository.

## Fehlerverhalten

- Keine Antwort auf andere Nachrichtentexte.
- Fehler in der URL-Auflösung oder beim Versand werden geloggt; der Listener läuft weiter und beantwortet die nächste Anfrage — es gibt keine Fehlerantwort im Protokoll.
