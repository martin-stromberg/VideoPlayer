# Produktionsinstallation unter Linux (systemd)

> **Dokumenttyp**: Installationsanleitung  
> **Zielgruppe**: Betreiber, Administratoren  
> **Version**: 1.0
> **Letzte Aktualisierung**: 2026-11-23

Diese Anleitung führt von einem leeren Linux-Server zu einer laufenden, im Netzwerk per
Discovery auffindbaren VideoWebPlayer-Installation. Sie richtet sich an Betreiber ohne
Projektvorwissen; die Entwicklungseinrichtung ist separat in
[GUIDE_Installation.md](./GUIDE_Installation.md) beschrieben.

## Geltungsbereich und Betriebsarten

Die Anleitung gilt ausschließlich für **Linux mit systemd**. Windows-Produktivbetrieb
(IIS `OutOfProcess`, Windows-Dienst) ist an anderer Stelle teildokumentiert und nicht
Gegenstand dieser Anleitung.

Es gibt zwei vollständig beschriebene Betriebsarten; die Schritte 1–6 (gemeinsamer
Basispfad) gelten für beide:

| | Variante A — eigenständig | Variante B — hinter Reverse-Proxy |
|---|---|---|
| Zugriff | `http://<server>:5002` direkt | `https://<domain>` über nginx (TLS-terminierend) |
| Verschlüsselung | keine — nur für das eigene LAN gedacht | TLS mit Zertifikat über certbot (Let's Encrypt) |
| Geeignet für | privates LAN, direkter Zugriff | Exposition über fremde Netze, HTTPS-Anforderung |
| Discovery | automatische Ableitung genügt | `Discovery:PublicBaseUrl` ist Pflicht |
| mDNS-Advertisement | nutzbar (UDP 5353) | deaktivieren (`Mdns:Enabled=false`) |

## Voraussetzungen

- Linux-Server mit systemd (z. B. Debian, Ubuntu, Fedora).
- root- bzw. sudo-Zugang — die Anleitung nutzt `sudo` für Paket-, Benutzer-, Unit- und
  Firewall-Schritte.
- **Kein .NET SDK und keine .NET-Runtime** auf dem Zielserver nötig: Das Release-Artefakt
  `release-linux-x64.zip` ist ein self-contained Build.
- Bei Variante B: ein DNS-Eintrag, der die gewünschte (Sub-)Domain auf die Server-IP zeigt.
- Bei Variante B: der Reverse-Proxy läuft zwingend **auf demselben Host** (Begründung im
  Abschnitt „Vertrauensbasis").

### Schritt 1: Systempakete installieren

Das self-contained Build benötigt einige native OS-Bibliotheken: `libicu` (ohne sie
schlägt der erste Start fehl), OpenSSL/`libssl` und `ca-certificates` (u. a. für die
Update-Prüfung gegen GitHub) sowie `curl` und `unzip` für Download und Entpacken.
`python3` wird für den `ProtectedFiles`-Merge bei Update-Installationen gebraucht.

Debian/Ubuntu:

```bash
sudo apt update
sudo apt install -y curl unzip ca-certificates libssl3 python3
# libicu-Paketname ist release-abhängig (z. B. libicu72, libicu74, libicu76):
sudo apt install -y libicu74
```

Fedora:

```bash
sudo dnf install -y curl unzip ca-certificates openssl-libs libicu python3
```

> Steht `unzip` nicht zur Verfügung (Minimal-Installation), funktionieren alternativ
> `busybox unzip` oder `python3 -m zipfile -e release-linux-x64.zip /opt/videowebplayer/`.

## Schritt 2: Anwendung bereitstellen

Hauptweg: das fertige Release-Artefakt `release-linux-x64.zip` aus dem
[GitHub-Release](https://github.com/martin-stromberg/VideoPlayer/releases) laden und nach
`/opt/videowebplayer` entpacken (Beispielpfad, der im gesamten Dokument verwendet wird):

```bash
cd /tmp
curl -fSL -o release-linux-x64.zip \
  https://github.com/martin-stromberg/VideoPlayer/releases/latest/download/release-linux-x64.zip
sudo mkdir -p /opt/videowebplayer
sudo unzip -o release-linux-x64.zip -d /opt/videowebplayer
sudo chmod +x /opt/videowebplayer/VideoWebPlayer
```

Für einen bestimmten Release-Tag `releases/latest/download/` durch
`releases/download/<tag>/` ersetzen.

Alternativ auf einem Build-Host selbst veröffentlichen und die Publish-Ausgabe kopieren
(entspricht dem Aufruf der Release-Pipeline):

```bash
dotnet publish VideoWebPlayer/VideoWebPlayer.csproj -c Release -r linux-x64 \
  --self-contained true -o publish/linux-x64
# anschließend den Inhalt von publish/linux-x64/ nach /opt/videowebplayer kopieren
```

Die Publish-Ausgabe enthält `release-metadata.json` (wird automatisch erzeugt), über die
der Updater die installierte Version ermittelt — sie muss im Anwendungsverzeichnis liegen
bleiben.

## Schritt 3: Service-Account und Verzeichnisrechte

Dedizierter System-Benutzer ohne Login-Shell:

```bash
sudo useradd --system --user-group --no-create-home --shell /usr/sbin/nologin videowebplayer
sudo chown -R videowebplayer:videowebplayer /opt/videowebplayer
# Das Release-Paket enthält kein Data/-Verzeichnis und die Anwendung legt es
# nicht selbst an — ohne es schlägt der erste Start fehl:
sudo install -d -o videowebplayer -g videowebplayer /opt/videowebplayer/Data
```

`Data/` ist der einzige Pfad, den die Anwendung nicht selbst erzeugt: Die
SQLite-Datenbank liegt unter `Data Source=Data/WebVideoPlayer.db`, und die
Datenbank-Migration beim Start legt das Elternverzeichnis nicht an. Fehlt es,
scheitert der Start mit `SQLite Error 14 'unable to open database file'` und
die Unit läuft in eine Restart-Schleife.

Der Service-Account braucht auf folgende Verzeichnisse unterhalb des
Anwendungsverzeichnisses Schreibzugriff. Mit Ausnahme von `Data/` (oben
angelegt) erzeugt die Anwendung sie selbst — `Logs/` bereits beim Start,
`wwwroot/images/genres/` beim ersten Icon-Upload über die Admin-Oberfläche,
die `Data/`-Unterverzeichnisse und `tools/ffmpeg/` bei erster Nutzung, die
Update-Ablagen `Updates/`/`Backups/` beim ersten Update-Lauf; `chown -R` oben
deckt sie ab:

| Verzeichnis | Zugriff | Zweck |
|-------------|---------|-------|
| `Data/` | lesen + schreiben | SQLite-Datenbank `WebVideoPlayer.db` (inkl. `-wal`/`-shm`), `Data/Backups/`, `Data/LiveStream/`, `Data/LiveStreamStandby/` — vom Betreiber anzulegen (s. o.) |
| `Logs/` | schreiben | Serilog-Dateilog `Logs/web-.log` (beim Start angelegt) |
| `Updates/` | schreiben | Update-Pakete, Status-/Lock-Dateien, `update.log`, `Updates/backup/` |
| `Backups/` | schreiben | Update-Sicherungen der Generation `ProgramUpdate` |
| `tools/ffmpeg/` | schreiben | per Admin-Oberfläche nachladbare ffmpeg/ffprobe-Binaries |
| `wwwroot/images/genres/` | lesen + schreiben | Genre-Icons — wird beim ersten Icon-Upload über die Admin-Oberfläche angelegt; der Betreiber kann Icons auch manuell ablegen |
| Anwendungsverzeichnis insgesamt | schreiben | ein Self-Update tauscht Dateien im Verzeichnis selbst aus |
| `$TMPDIR` | schreiben | temporäre Upload-Chunks der Backup-Uploads — durch `PrivateTmp` in der Unit abgedeckt |

## Schritt 4: Betreiber-Konfiguration update-sicher ablegen

Deployment-seitige Werte gehören **nie** in die paketierten `appsettings*.json` —
sie überstehen Update-Installationen nicht zuverlässig. Zwei update-sichere Ebenen:

- **`appsettings.Local.json`** im Anwendungsverzeichnis für nicht-geheime Werte
  (Bindung, `PathBase`, `Mdns:*`, `Discovery:PublicBaseUrl`, `AutoUpdate:*`). Die Datei
  wird nie paketiert und übersteht Updates vollständig. Regeln: keine JSON-Kommentare
  (der Update-Merge muss sie lesen können), der `appsettings*.json`-Wildcard der
  `ProtectedFiles`-Schutzliste sichert sie bei jedem Update zusätzlich nach
  `Updates/backup/` und formatiert sie neu.
- **Umgebungsvariablen** über die `EnvironmentFile`-Direktive der systemd-Unit für
  Secrets (`Jwt:*`). Umgebungsvariablen sind die robusteste Secret-Ablage, weil sie
  außerhalb des Anwendungsverzeichnisses liegen — Details und Secret-Tabelle in
  [SECRETS_MANAGEMENT.md](./SECRETS_MANAGEMENT.md).

Vorrangkette: `appsettings.Local.json` überlagert die Paket-`appsettings*.json`, verliert
aber gegen Umgebungsvariablen und die Kommandozeile.

**Pflichtwerte in `Production`:** Der Start schlägt fehl, wenn `Jwt:Key`,
`Jwt:ApiToken:Web` (bzw. als Fallback `Jwt:ApiToken`) oder `Jwt:ApiToken:Maui` fehlen
(Fehlermeldung `Fehlende Konfiguration: Jwt:…`). Eigene Werte erzeugen — die folgenden
Platzhalter sind nicht zum Kopieren gedacht.

Environment-Datei `/etc/videowebplayer/videowebplayer.env` anlegen (liegt bewusst
außerhalb des Anwendungsverzeichnisses):

```bash
sudo mkdir -p /etc/videowebplayer
sudo tee /etc/videowebplayer/videowebplayer.env > /dev/null <<'EOF'
Jwt__Key=<PRODUKTIVER_JWT_KEY_BASE64_MIN_32_BYTES>
Jwt__ApiToken__Web=<PRODUKTIVER_WEB_API_TOKEN>
Jwt__ApiToken__Maui=<PRODUKTIVER_MAUI_API_TOKEN>
Jwt__Issuer=VideoWebPlayer
EOF
sudo chown root:videowebplayer /etc/videowebplayer/videowebplayer.env
sudo chmod 640 /etc/videowebplayer/videowebplayer.env
```

Die Datei `appsettings.Local.json` ist optional und wird erst angelegt, wenn
Betreiberwerte vom Default abweichen — die konkreten Inhalte je Betriebsart stehen in
den Abschnitten zu Variante A und B (z. B. `Kestrel:Endpoints:Http:Url`,
`Discovery:PublicBaseUrl`, `PathBase`, `Mdns:Enabled`, `AutoUpdate:*`). Sie liegt im
Anwendungsverzeichnis und gehört dem Service-Account (`videowebplayer`).

Beide Konfigurationsebenen liest die Anwendung nur beim Prozessstart ein: Werden
`appsettings.Local.json` oder die Environment-Datei bei bereits laufendem Dienst
angelegt oder geändert, greifen die Werte erst nach einem Neustart:

```bash
sudo systemctl restart videowebplayer
```

## Schritt 5: systemd-Unit anlegen

Unit-Datei `/etc/systemd/system/videowebplayer.service`:

```ini
[Unit]
Description=VideoWebPlayer
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=videowebplayer
Group=videowebplayer
WorkingDirectory=/opt/videowebplayer
Environment=ASPNETCORE_ENVIRONMENT=Production
EnvironmentFile=/etc/videowebplayer/videowebplayer.env
ExecStart=/opt/videowebplayer/VideoWebPlayer
Restart=on-failure
RestartSec=10

# Härtung
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=/opt/videowebplayer
ProtectHome=true
ProtectKernelTunables=true
ProtectKernelModules=true
ProtectControlGroups=true
RestrictSUIDSGID=true

[Install]
WantedBy=multi-user.target
```

Hinweise zur Unit:

- `ASPNETCORE_ENVIRONMENT=Production` aktiviert `appsettings.Production.json` — darin
  steckt die Produktions-Bindung `http://*:5002` (`Kestrel:Endpoints:Http:Url`) und das
  Logging-Level.
- `ProtectSystem=strict` macht das Dateisystem schreibgeschützt; `ReadWritePaths`
  gibt allein das Anwendungsverzeichnis frei — das deckt sämtliche
  Laufzeit-Schreibverzeichnisse **und** das Self-Update ab, weil alle unterhalb von
  `/opt/videowebplayer` liegen. `PrivateTmp` deckt `$TMPDIR` (Upload-Chunks) ab.
- `MemoryDenyWriteExecute` ist bewusst nicht gesetzt (Risiko für self-contained
  .NET-Builds).
- Ohne die `[Install]`-Sektion schlägt `systemctl enable` fehl.

Aktivieren und starten:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now videowebplayer
```

Verifikation:

```bash
systemctl status videowebplayer
journalctl -u videowebplayer -f
curl http://127.0.0.1:5002/api/health   # Antwort: OK
```

## Schritt 6: Firewall freigeben

Beispiele für `ufw` (Debian/Ubuntu); bei `firewalld` (Fedora) entsprechend
`sudo firewall-cmd --permanent --add-port=<port>/<proto>` gefolgt von
`sudo firewall-cmd --reload` verwenden.

Variante A — eigenständig (App-Port, UDP-Broadcast-Discovery, mDNS):

```bash
sudo ufw allow 5002/tcp
sudo ufw allow 5001/udp
sudo ufw allow 5353/udp
```

Variante B — hinter Reverse-Proxy (nur Proxy-Ports und Discovery; TCP 5002 bleibt
Loopback-only und wird **nicht** extern geöffnet):

```bash
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 5001/udp
```

Damit ist der gemeinsame Basispfad abgeschlossen. Weiter mit Variante A oder B.

## Variante A — Eigenständiger Betrieb ohne Reverse-Proxy

### Bindung und Erreichbarkeit

Die Produktions-Bindung `http://*:5002` aus `appsettings.Production.json` ist direkt im
Netz erreichbar — in dieser Variante gewollt. Von einem anderen Rechner im LAN:

```bash
curl http://<lan-ip>:5002/api/health   # Antwort: OK
```

### Netzwerk-Erkennung (Discovery)

Die UDP-Broadcast-Erkennung (Port 5001) ist ohne Konfiguration aktiv und leitet die
gemeldete Basis-URL automatisch aus der LAN-Adresse und dem gebundenen Port ab;
Loopback- (`localhost`, `127.*`, `::1`) und Wildcard-Adressen werden nie gemeldet.
`Discovery:PublicBaseUrl` ist hier optional; `Host:Address`/`Host:Port` können die
Ableitung steuern — Details und Vorrangkette (Admin-Feld → Betreiber-Konfiguration →
Ableitung) siehe
[Netzwerk-Erkennung — Installation](./help/netzwerk-erkennung/installation.md).

### mDNS

Das mDNS-Advertisement ist standardmäßig aktiv (`Mdns:Enabled=true`): Diensttyp
`_videowebplayer._tcp.local.`, Instanzname `VideoWebPlayer`, TXT-Records `path=/` und
`app=VideoWebPlayer`, UDP-Multicast 5353. Voraussetzung ist die geöffnete Firewall
(Schritt 6).

Läuft auf demselben Host zusätzlich ein `avahi-daemon` mit statischer Dienstdatei,
entsteht ein Doppel-Advertisement — Vorgehen siehe
[INSTALL_AVAHI.md](./INSTALL_AVAHI.md).

### Hinweise zum HTTP-Betrieb

- Das Log zeigt einmalig die harmlose Warnung `Failed to determine the https port for
  redirect`: Die HTTPS-Umleitungs-Middleware ist immer in der Pipeline, findet aber im
  reinen HTTP-Betrieb keinen HTTPS-Endpunkt — der Betrieb läuft normal weiter, es findet
  kein Redirect statt.
- In `Production` sendet die Anwendung einen HSTS-Header — im reinen HTTP-Betrieb ohne
  praktische Wirkung (Browser werten HSTS nur über HTTPS).
- Der Verkehr ist unverschlüsselt. Diese Variante ist für den Betrieb im eigenen LAN
  gedacht; wer den Server über fremde Netze erreichbar machen will, nutzt Variante B.

## Variante B — Betrieb hinter TLS-terminierendem Reverse-Proxy

### Vertrauensbasis: nur Loopback

`X-Forwarded-Proto`- und `X-Forwarded-For`-Header werden ausschließlich von
Loopback-Absendern akzeptiert (`KnownProxies` ist fest auf `127.0.0.1` und `::1`
gesetzt). Der Reverse-Proxy muss daher **auf demselben Host** laufen wie die Anwendung;
Header anderer Absender werden still ignoriert — ein Proxy auf einem anderen Host wird
nicht unterstützt.

### Loopback-Bindung der Anwendung

Damit die App am Proxy vorbei nicht erreichbar ist, die Bindung in
`appsettings.Local.json` auf Loopback einschränken (alternativ als Umgebungsvariable
`Kestrel__Endpoints__Http__Url`; der Schlüssel steht in `JsonKeys` und übersteht
Update-Installationen):

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://127.0.0.1:5002"
      }
    }
  }
}
```

Die geänderte Bindung greift — wie alle `appsettings.Local.json`-Werte — erst nach
einem Neustart des Dienstes (Endpunkte werden beim Start gebunden, siehe Schritt 4).

### nginx-Serverblock

nginx installieren (`sudo apt install nginx` bzw. `sudo dnf install nginx`), dann z. B.
`/etc/nginx/sites-available/videowebplayer` anlegen und per Symlink nach
`sites-enabled/` aktivieren (unter Fedora: Datei unter `/etc/nginx/conf.d/`):

```nginx
# WebSocket-Unterstützung (muss im http-Kontext stehen — auf Debian/Ubuntu ist dieser
# Dateianfang innerhalb des http-Blocks eingebunden und damit zulässig)
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 80;
    server_name videos.example.com;

    # Kestrel-Limit MaxRequestBodySize ist in Production unbegrenzt (große
    # Backup-Uploads); das nginx-Limit muss über Backups:MaxUploadSizeBytes
    # (Standard 5 GiB) liegen.
    client_max_body_size 6g;

    location / {
        proxy_pass http://127.0.0.1:5002;
        proxy_http_version 1.1;

        # Pflicht: die Anwendung lehnt Requests mit leerem/unparsbarem
        # Host-Header mit 400 ab.
        proxy_set_header Host $host;

        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;

        # WebSocket-Header — serverweit bzw. in allen location-Blöcken setzen:
        # zwei Endpunkte nutzen WebSockets, der Blazor-Server-Circuit /_blazor
        # (ohne Upgrade bleibt die interaktive UI funktionslos) und der
        # SignalR-Hub /hubs/mediaupdate.
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
    }
}
```

Wichtig: `proxy_pass` ohne URI-Teil — der Request-Pfad bleibt erhalten (zwingend für die
`PathBase`-Variante B2). Die WebSocket-Header nicht auf einen einzelnen `location`-Block
beschränken, sonst bleibt mindestens einer der beiden Endpunkte ohne Upgrade.

### TLS-Zertifikat über certbot

```bash
sudo apt install certbot python3-certbot-nginx    # Debian/Ubuntu
sudo dnf install certbot python3-certbot-nginx    # Fedora

sudo certbot --nginx -d videos.example.com
```

certbot trägt die TLS-Direktiven (`listen 443 ssl`, Zertifikatspfade, HTTP→HTTPS-Redirect)
direkt in den Serverblock ein. Die Erneuerung läuft automatisch über den
certbot-systemd-Timer — ein eigenes Renewal-Setup ist nicht nötig.

Als kurze Alternative eignet sich auch **Caddy** (automatisches TLS eingebaut); diese
Anleitung beschreibt bewusst nur den nginx-Weg.

### Routing-Variante B1 — eigene (Sub-)Domain (Hauptweg)

Die (Sub-)Domain zeigt per DNS auf den Server; nginx routet `location /` auf das Backend;
`PathBase` bleibt leer. Der obige Serverblock gilt unverändert.

Pflicht: `Discovery:PublicBaseUrl` setzen, weil die automatische Ableitung nur das
Loopback-Backend sieht (`http`, interner Port 5002, kein Pfad). Entweder als
Admin-Feld „Öffentliche Basis-URL" unter `Einrichtung` → `Allgemein` (wirkt ohne
Neustart, hat Vorrang) oder als Betreiber-Konfiguration in `appsettings.Local.json`:

```json
{
  "Discovery": {
    "PublicBaseUrl": "https://videos.example.com"
  }
}
```

### Routing-Variante B2 — Unterpfad via `PathBase`

Die Anwendung läuft unter `https://example.com/videoplayer/`. `PathBase` wird
**ausschließlich** über `appsettings.Local.json` oder die Umgebungsvariable `PathBase`
gesetzt — der Schlüssel steht nicht in `AutoUpdate:ProtectedFiles`/`JsonKeys` und darf
deshalb nie in paketierte `appsettings*.json` (Einschränkung; eine Aufnahme in die
Schutzliste ist als Folge-Issue vorgesehen):

```json
{
  "PathBase": "/videoplayer",
  "Discovery": {
    "PublicBaseUrl": "https://example.com/videoplayer/"
  }
}
```

Der nginx-`location`-Block ändert sich zu (übrige Direktiven wie im Serverblock oben):

```nginx
location = /videoplayer {
    return 308 /videoplayer/;
}

location /videoplayer/ {
    proxy_pass http://127.0.0.1:5002;
    proxy_http_version 1.1;

    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection $connection_upgrade;
}
```

Der Request-Pfad bleibt durch das URI-lose `proxy_pass` erhalten; die `PathBase`-
Middleware der Anwendung entfernt den Präfix vor dem Routing. `Discovery:PublicBaseUrl`
muss eine absolute `http`-/`https`-URL mit Schema und Host sein — bei Unterpfad-Betrieb
inklusive des Pfads; ein expliziter Port ist nur nötig, wenn er vom Standardport des
Schemas abweicht.

### Discovery im Proxy-Betrieb

Eine explizite Basis-URL ist **Pflichtkonfiguration** — die Ableitung sieht nur das
interne Backend. Vorrang und Format:

1. Admin-Feld „Öffentliche Basis-URL" unter `Einrichtung` → `Allgemein` (wirkt sofort,
   kein Neustart),
2. `Discovery:PublicBaseUrl` in der Betreiber-Konfiguration,
3. automatische Ableitung (im Proxy-Betrieb ungeeignet).

Der Wert muss eine absolute `http`-/`https`-URL sein — sonst schlägt der Start fehl.
Details siehe
[Netzwerk-Erkennung — Installation](./help/netzwerk-erkennung/installation.md) und
[Netzwerk-Erkennung — Einrichtung](./help/netzwerk-erkennung/einrichtung-anwender.md).

### mDNS im Proxy-Betrieb

Das eingebaute mDNS-Advertisement ist hinter einem TLS-Proxy ungeeignet: Der TXT-Record
`path=/` ist fest verdrahtet (stimmt bei `PathBase`-Betrieb nicht), und DNS-SD transportiert
kein URL-Schema — das Advertisement würde eine falsche, unverschlüsselte Adresse auf dem
internen Port vermitteln. Daher im Proxy-Betrieb deaktivieren und auf die
UDP-Broadcast-Discovery mit gesetzter `Discovery:PublicBaseUrl` setzen:

```json
{
  "Mdns": {
    "Enabled": false
  }
}
```

`Mdns:Enabled` ist der Betreiber-Master-Switch — der Admin-Schalter in der Oberfläche
kann ihn nicht überstimmen.

Randnotiz: Nur wo ein direktes HTTP-Advertisement bewusst gewollt ist (z. B. parallele
LAN-Erreichbarkeit des Backends), kann `Mdns:Port` als Override auf den tatsächlich
erreichbaren Port gesetzt werden — für den beschriebenen TLS-Betrieb nicht empfohlen.

### Wechselwirkungen im Proxy-Betrieb

- `X-Forwarded-Proto` macht `UseHttpsRedirection` unproblematisch: Die Middleware
  entscheidet anhand des weitergegebenen Schemas; der eigentliche HTTP→HTTPS-Redirect
  geschieht in nginx (certbot richtet ihn ein).
- HSTS ist in `Production` aktiv (Standard `max-age=30 days`) — Browser erzwingen danach
  HTTPS für die Domain.
- Firewall: nur 80/tcp, 443/tcp und 5001/udp extern; TCP 5002 bleibt Loopback-only.

Zum Abschluss der Variante den Dienst neu starten, damit alle gesetzten
`appsettings.Local.json`-Werte greifen (Loopback-Bindung, `PathBase`,
`Discovery:PublicBaseUrl`, `Mdns:Enabled`):

```bash
sudo systemctl restart videowebplayer
```

## Updates der Installation

### AutoUpdate unterstützen (empfohlener Weg)

AutoUpdate ist Kernfunktion des VideoWebPlayers (Prüfung, Download und Installation neuer
GitHub-Releases inklusive Datensicherung vor der Installation) und daher der unterstützte
Hauptweg. Unter der gehärteten Unit braucht er vier dokumentierte Voraussetzungen:

1. **Schreibzugriff des Service-Accounts aufs Anwendungsverzeichnis** — bereits durch
   Besitz (`chown -R`) und `ReadWritePaths=/opt/videowebplayer` gegeben; nötig, weil das
   Self-Update Dateien im Anwendungsverzeichnis austauscht.
2. **`python3` auf dem Zielsystem** — sonst wird der `ProtectedFiles`-Merge bei
   Update-Installationen mit `update.log`-Eintrag übersprungen (Details siehe
   [TECH_Auto_Update.md](./TECH_Auto_Update.md)).
3. **Passende Update-Einstellungen** in `appsettings.Local.json`:
   - `AutoUpdate:ServiceName` = Unit-Name **inklusive `.service`-Suffix**, also
     `videowebplayer.service` — das Installationsskript führt `systemctl stop`,
     `start` und `is-active` damit aus; die Validierung verlangt einen `*.service`-Namen.
     Derselbe Wert steht im Admin-Feld „Dienstname für Neustart" (siehe
     [help/updates.md](./help/updates.md)).
   - `AutoUpdate:WorkspaceUser`/`AutoUpdate:WorkspaceGroup` = `videowebplayer` — das
     Installationsskript normalisiert damit den Besitz des `Updates/`-Verzeichnisses per
     `chown` (die Update-Unit läuft als root, s. u.).
   - `AutoUpdate:DownloadPath` = Ablage für Update-Pakete, Status-/Lock-Dateien,
     `update.log` und die `ProtectedFiles`-Sicherungen (`Updates/backup/`) — Standard
     `Updates`, relativ zum Anwendungsverzeichnis. Der Schlüssel steht in `JsonKeys`;
     ein Override ist nur nötig, wenn die Ablage an einem anderen Ort liegen soll
     (dann Besitz und `ReadWritePaths` entsprechend anpassen).

   ```json
   {
     "AutoUpdate": {
       "ServiceName": "videowebplayer.service",
       "WorkspaceUser": "videowebplayer",
       "WorkspaceGroup": "videowebplayer"
     }
   }
   ```

   Auch diese Werte greifen erst nach einem Dienst-Neustart (Schritt 4) — das erste
   Update nach dem Setzen scheitert sonst am für den laufenden Prozess unbekannten
   `ServiceName`.
4. **`systemd-run`-Berechtigung per polkit:** Die Update-Installation läuft unter Linux
   als transiente **System**-Unit `VideoWebPlayer-AutoUpdate`, die der App-Prozess über
   `systemd-run` registriert (ohne `--wait`; die Unit läuft als root und überlebt den
   `StopHostAfterScriptStart`-Shutdown). Weil der Service-Account `videowebplayer` dazu
   eine System-Unit anlegen muss, braucht er die polkit-Aktion
   `org.freedesktop.systemd1.manage-units` — ein sudoers-Eintrag greift nicht, da kein
   `sudo` im Spiel ist.

   Beispiel-Regel `/etc/polkit-1/rules.d/50-videowebplayer-autoupdate.rules`:

   ```javascript
   polkit.addRule(function(action, subject) {
       if (action.id === "org.freedesktop.systemd1.manage-units" &&
           subject.user === "videowebplayer") {
           return polkit.Result.YES;
       }
   });
   ```

   Voraussetzung ist ein installierter polkit-Daemon (Paket `polkitd`, auf älteren
   Releases `policykit-1`) — auf Minimal-Installationen nicht garantiert.

   **Tragweite offen benannt:** Die Freigabe erlaubt dem Service-Account grundsätzlich
   das Verwalten beliebiger System-Units — eine weitreichende Ausnahme. Wer das nicht
   will, nutzt die Alternative im nächsten Abschnitt.

### Alternative: AutoUpdate deaktivieren und manuell aktualisieren

```json
{
  "AutoUpdate": {
    "Enabled": false
  }
}
```

Manueller Update-Pfad (identisch zur Erstinstallation):

```bash
cd /tmp
curl -fSL -o release-linux-x64.zip \
  https://github.com/martin-stromberg/VideoPlayer/releases/latest/download/release-linux-x64.zip
sudo systemctl stop videowebplayer
sudo unzip -o release-linux-x64.zip -d /opt/videowebplayer
sudo chown -R videowebplayer:videowebplayer /opt/videowebplayer
sudo systemctl start videowebplayer
```

Deployment-seitige Werte bleiben erhalten, weil sie in `appsettings.Local.json` bzw.
Umgebungsvariablen liegen — beide sind nicht im Paket enthalten. Diagnose bei
Update-Problemen: `Updates/update.log` im Anwendungsverzeichnis.

## Erstlauf und Verifikation

1. `systemctl status videowebplayer` — Unit `active (running)`.
2. `journalctl -u videowebplayer -f` — Start ohne Exceptions; die einmalige Warnung
   `Failed to determine the https port for redirect` ist bei Variante A normal.
3. `curl http://127.0.0.1:5002/api/health` liefert `OK` (Variante A zusätzlich
   `curl http://<lan-ip>:5002/api/health` von einem anderen Rechner).
4. Web-UI im Browser öffnen (`http://<server>:5002` bzw. `https://<domain>`) und den
   ersten Benutzer anlegen — der erste registrierte Benutzer wird Administrator.
5. Discovery-Test: Die Client-App findet den Server per UDP-Broadcast (Port 5001) —
   bei Variante A zusätzlich per mDNS.

## Troubleshooting

| Symptom | Ursache und Abhilfe |
|---------|---------------------|
| Dienst startet nicht, `Fehlende Konfiguration: Jwt:…` im Journal | In `Production` sind `Jwt:Key`, `Jwt:ApiToken:Web` (bzw. `Jwt:ApiToken`) und `Jwt:ApiToken:Maui` Pflicht — Environment-Datei prüfen und Werte setzen. |
| Dienst startet nicht, Port 5002 belegt | Anderen Port wählen: `Kestrel:Endpoints:Http:Url` in `appsettings.Local.json` (beim Proxy-Betrieb den `proxy_pass`-Zielport anpassen). |
| Dienst startet nicht, `SQLite Error 14 'unable to open database file'` im Journal | `Data/` fehlt oder gehört nicht dem Service-Account — Verzeichnis wie in Schritt 3 anlegen (`install -d -o videowebplayer -g videowebplayer /opt/videowebplayer/Data`). |
| Dienst startet nicht, ICU-Fehler beim ersten Start | `libicu` fehlt — Paketliste aus Schritt 1 installieren. |
| `systemctl enable` meldet „unit file has no installation config" | `[Install]`-Sektion in der Unit fehlt — Unit aus Schritt 5 übernehmen. |
| HTTP 400 hinter dem Proxy | Der Proxy setzt keinen (gültigen) `Host`-Header — `proxy_set_header Host $host;` ergänzen. |
| Interaktive UI lädt nicht / Hub-Verbindung schlägt fehl | WebSocket-Upgrade-Header fehlen — `Upgrade`/`Connection` in **allen** `location`-Blöcken setzen (`/_blazor` und `/hubs/mediaupdate`). |
| Discovery meldet keine oder eine unbrauchbare Adresse | `Discovery:PublicBaseUrl` bzw. Admin-Feld prüfen — Details siehe [troubleshooting.md](./help/netzwerk-erkennung/troubleshooting.md). |
| Server erscheint doppelt in der Dienstsuche | Avahi-Dienstdatei parallel zum eingebauten Advertisement aktiv — siehe [INSTALL_AVAHI.md](./INSTALL_AVAHI.md). |
| Update-Installation schlägt fehl | `Updates/update.log` lesen; polkit-Regel, `python3`, `AutoUpdate:ServiceName` (`*.service`-Suffix) und `WorkspaceUser`/`WorkspaceGroup` prüfen. Nach einem Fehlschlag liegt ein `update.failed`-Marker im Update-Verzeichnis. |
| Start schlägt mit Validierungsfehler zu `Discovery:PublicBaseUrl` fehl | Wert muss eine absolute `http`-/`https`-URL sein (inkl. Schema, Host, ggf. Pfad). |

## Siehe auch

- [GUIDE_Installation.md](./GUIDE_Installation.md) — Entwicklungseinrichtung
- [INSTALL_AVAHI.md](./INSTALL_AVAHI.md) — mDNS-Alternativweg über den Avahi-Daemon
- [Netzwerk-Erkennung](./help/netzwerk-erkennung/index.md) — Discovery-Details
- [Updates](./help/updates.md) — Update-Verwaltung in der Admin-Oberfläche
- [TECH_Auto_Update.md](./TECH_Auto_Update.md) — technischer Update-Ablauf
- [SECRETS_MANAGEMENT.md](./SECRETS_MANAGEMENT.md) — Secret-Ablage
