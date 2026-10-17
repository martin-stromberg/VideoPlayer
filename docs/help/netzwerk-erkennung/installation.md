← [Zurück zur Übersicht](index.md)

# Netzwerk-Erkennung — Installation und Konfiguration

## Konfigurationsparameter

Die Broadcast-Erkennung ist ohne Konfiguration aktiv. Die folgenden Schlüssel steuern, welche Adresse gemeldet wird (Defaults in `appsettings.json`, übersteuerbar per Umgebungsvariablen oder `appsettings.Local.json`):

| Schlüssel | Standard | Beschreibung |
|-----------|----------|--------------|
| `Discovery:PublicBaseUrl` | `null` | Explizite öffentliche Basis-URL für die Discovery-Antwort: absolute `http`-/`https`-URL inkl. optionalem Pfad, z. B. `https://example.com/videoplayer/`. `null` = Admin-Wert bzw. automatische Ableitung. Ein ungültiger Wert lässt den Start fehlschlagen. |
| `Host:Address` | – | Expliziter Host für die Ableitung (LAN-IP oder Hostname). Loopback-, Any- und Wildcard-Werte sowie IPv6-Literale werden ignoriert. |
| `Host:Port` | – | Port-Fallback der Ableitung, wenn weder die gebundenen Serveradressen noch `Kestrel:Endpoints` einen Port liefern (Schema dabei `http`). |
| `Kestrel:Endpoints:Http:Url` / `Kestrel:Endpoints:Https:Url` | – | Dienen der Ableitung als Quelle für Schema, Port und ggf. literalen Host. |

Umgebungsvariable für `Discovery:PublicBaseUrl`: `Discovery__PublicBaseUrl` (doppelter Unterstrich). Der Schlüssel steht in `AutoUpdate:ProtectedFiles` unter `JsonKeys` und übersteht damit Programmupdates.

Der Admin-Wert „Öffentliche Basis-URL" (`Setups.DiscoveryPublicBaseUrl`, siehe [Einrichtung](einrichtung-anwender.md)) hat in der Vorrangkette oberste Priorität und kann den hier konfigurierten Betreiberwert übersteuern.

## Netzwerk-Voraussetzungen

- Die Firewall auf dem Server muss **UDP-Port 5001 eingehend** erlauben (für die mDNS-Ankündigung zusätzlich UDP 5353, siehe `docs/INSTALL_AVAHI.md` bzw. `docs/GUIDE_Installation.md`).
- Broadcasts erreichen nur Clients im selben Netzwerksegment.

## IIS `OutOfProcess` / Reverse-Proxy / TLS-Terminierung

Die automatische Ableitung sieht nur das interne Backend (`http`, interner Port, kein Pfad). Für diese Deployments ist eine explizite Basis-URL **Pflichtkonfiguration** — entweder als Admin-Wert oder als `Discovery:PublicBaseUrl`, z. B.:

```json
"Discovery": {
  "PublicBaseUrl": "https://server.example.com/videoplayer/"
}
```

Der Wert muss eine absolute `http`-/`https`-URI sein; andere Werte lassen den Start mit einer Validierungsfehlermeldung fehlschlagen.

## Migration

Die Migration `AddSetupDiscoveryPublicBaseUrl` fügt die nullable Spalte `Setups.DiscoveryPublicBaseUrl` hinzu und läuft beim Anwendungsstart automatisch; Eingriffe sind nicht nötig.
