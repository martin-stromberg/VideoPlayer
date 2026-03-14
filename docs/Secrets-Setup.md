# Secrets/Keys einrichten (API-Key, JWT)

Diese App erwartet folgende Konfigurationsschlüssel:
- Jwt:Key — Base64-codierter Schlüssel mit 64 Bytes Entropie (z. B. für HMAC-Signaturen)
- Jwt:ApiToken — API-Schlüssel für interne Requests (Header: `X-API-Key`)
- Jwt:Issuer — z. B. `VideoWebPlayer`

Hinweis: In .NET werden Doppelpunkte in Umgebungsvariablen mit doppeltem Unterstrich ersetzt, z. B. `Jwt__Key`.

---

## Lokale Entwicklung (Windows/Visual Studio)

Empfehlung: User-Secrets verwenden. Diese liegen außerhalb des Repos und werden nicht eingecheckt.

### Visual Studio
1) Projekt im Solution Explorer rechtsklicken → __Manage User Secrets__.
2) In der geöffneten `secrets.json` folgendes eintragen:

```
{ 
	"Jwt:Key": "BASE64_64BYTE_KEY_HIER", 
	"Jwt:ApiToken": "DEIN_API_KEY", 
	"Jwt:Issuer": "VideoWebPlayer" 
}
```

Visual Studio legt bei Bedarf automatisch eine `UserSecretsId` im Projekt an.

---

## Produktion (Linux, systemd)

Setze Secrets als Umgebungsvariablen in der systemd-Unit oder in einer ausgelagerten Environment-Datei.

### Option 1: Direkt in der Unit
Datei: `/etc/systemd/system/videowebplayer.service`

```
[Unit] 
Description=VideoWebPlayer 
After=network.target

[Service] 
WorkingDirectory=/var/www/videowebplayer 
ExecStart=/usr/bin/dotnet VideoWebPlayer.dll 
Environment=ASPNETCORE_ENVIRONMENT=Production 
Environment=Jwt__Key=PASTE_BASE64_KEY 
Environment=Jwt__ApiToken=PASTE_API_KEY 
Environment=Jwt__Issuer=VideoWebPlayer 
User=www-data Group=www-data 
Restart=always

[Install] 
WantedBy=multi-user.target

```

### Option 2: Ausgelagerte Environment-Datei (empfohlen)
In der Unit:
```
# /etc/systemd/system/videowebplayer.service
[Service]
EnvironmentFile=/etc/videowebplayer/env
...
```

Environment-Datei anlegen (mit restriktiven Rechten):

```
# /etc/videowebplayer/env (Besitz/Perms strengen)
sudo install -m 600 -o www-data -g www-data -D /etc/videowebplayer/env
sudo bash -c 'cat >/etc/videowebplayer/env <<EOF
ASPNETCORE_ENVIRONMENT=Production
Jwt__Key=$(head -c 64 /dev/urandom | base64)
Jwt__ApiToken=DEIN_API_KEY
Jwt__Issuer=VideoWebPlayer
EOF'
```


Aktualisieren:
```
sudo systemctl daemon-reload 
sudo systemctl enable videowebplayer --now
```

---

## GitHub-Hygiene

- Keine Secrets in Code oder `appsettings*.json` einchecken.
- User-Secrets liegen außerhalb des Repos und werden nicht versioniert.
- Produktions-Secrets als Umgebungsvariablen oder in `/etc/...` pflegen (nicht im Repo).

Optional: Prüfe `.gitignore`, falls du zusätzliche lokale Dateien verwendest.

---

## Verifikation

- Lokal: App starten. Bei fehlenden Secrets (nur in Production erzwungen) erscheint ein Konfigurationsfehler.
- Server: `sudo systemctl status videowebplayer` prüfen.
- Header-Check: Interne HTTP-Calls tragen `X-API-Key` (sofern konfiguriert).

Tipp: Der Wert von `Jwt:Key` muss Base64-codiert sein. Er sollte stabil bleiben (nicht pro Neustart neu generieren), sonst werden bestehende Tokens ungültig.
