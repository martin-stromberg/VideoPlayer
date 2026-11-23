# Umsetzungsplan: Produktions-/Server-Installationsdokumentation für Linux/systemd

## Übersicht

Die Installationsdokumentation wird um einen vollständigen Produktions-Installationspfad für
Linux/systemd ergänzt. Ein Betreiber ohne Projektvorwissen soll anhand der Anleitung von einem
leeren Server zu einer laufenden, per Discovery auffindbaren Installation gelangen — in zwei
vollständig dokumentierten Betriebsarten: eigenständig (direkt erreichbar) und hinter einem
TLS-terminierenden Reverse-Proxy (nginx + certbot; Hauptweg eigene (Sub-)Domain, zusätzlich
pfadbasiertes Routing via `PathBase`). Reine Dokumentationsaufgabe — keine Code-Änderungen.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Eine Produktionsinstallation des VideoWebPlayer-Servers auf einem Linux-System — als Dokumentationsgegenstand der vollständige Installationsweg je Betriebsart (ohne bzw. mit Reverse-Proxy) von „leerer Server" bis „Anwendung läuft und per Discovery auffindbar ist". |
| Auslösende Ereignisse | Kein Laufzeit-Ereignis — Einzelmaßnahme (Dokumentationsüberarbeitung zu Issue #370). |
| Erwartete Wirkung | Ein Betreiber kann anhand der Dokumentation einen Linux-Server so konfigurieren, dass der VideoWebPlayer funktionsfähig läuft — eigenständig erreichbar oder hinter TLS-terminierendem Proxy — inkl. korrektem Discovery-Verhalten, gehärteter systemd-Unit und update-sicherer Betreiber-Konfiguration. |
| Umgang mit fehlenden Objekten | **Produktions-Installationsanleitung** (fehlt): **erzeugen** als eigenes `docs/`-Dokument mit Verlinkung aus `GUIDE_Installation.md` (Entscheidung siehe Designentscheidungen). **Betreiber-Werte außerhalb von `AutoUpdate:ProtectedFiles`/`JsonKeys`** (konkret `PathBase`): **update-sicher ablegen über `appsettings.Local.json`/Umgebungsvariablen** — nie in paketierte `appsettings*.json`. **mDNS hinter TLS-Proxy**: **deaktivieren** — dokumentierter Umgang `Mdns:Enabled=false` + UDP-Broadcast-Discovery mit gesetzter `Discovery:PublicBaseUrl`. **AutoUpdate unter gehärteter Unit**: **unterstützen** (Rechte-Ausnahmen dokumentieren) und zusätzlich den Pfad „bewusst deaktiviert + manuelles Update" zeigen. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Windows-Produktivbetrieb (IIS `OutOfProcess`, Windows-Dienst) wird nicht dokumentiert; die neue Anleitung nennt den Ausschluss an passender Stelle | Auftraggeber-Entscheidung: ausschließlich Linux/systemd; Windows ist an anderer Stelle teildokumentiert | bestätigt |
| 2 | Keine Code-Änderungen: konfigurierbares `KnownProxies` (fest Loopback), konfigurierbarer mDNS-`path`-TXT-Record, Aufnahme von `PathBase` in `AutoUpdate:ProtectedFiles`/`JsonKeys` | Potenzielle Folge-Issues; die Doku benennt die Einschränkungen offen statt sie zu beheben | bestätigt |
| 3 | Reverse-Proxy auf einem anderen Host wird nicht dokumentiert/unterstützt | `KnownProxies` ist fest auf `127.0.0.1`/`::1` gesetzt — `X-Forwarded-*` anderer Absender würden ignoriert | bestätigt |
| 4 | Keine Verweise auf andere Repositories, insbesondere keine privaten Repos/externen Pattern-Sammlungen | Dokumentation muss für sich allein stehen | bestätigt |
| 5 | Caddy wird höchstens als kurze Alternative erwähnt, nicht als eigener dokumentierter Pfad | Auftraggeber-Entscheidung: nginx + certbot ist das Hauptbeispiel | bestätigt |
| 6 | Der bestehende entwicklungsorientierte Inhalt von `GUIDE_Installation.md` bleibt erhalten; `INSTALL_AVAHI.md` und `docs/help/netzwerk-erkennung/*` werden verlinkt, nicht dupliziert | Bewusste Aufgaben-Abgrenzung gegen Inhaltsduplikation | bestätigt |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ablageort der Anleitung | Neues Dokument `docs/INSTALL_Server.md`; aus `GUIDE_Installation.md` wird verlinkt | Umfang (zwei vollständige Installationspfade, systemd-Unit, nginx/certbot, AutoUpdate-Rechte, Firewall — geschätzt 350–450 Zeilen) würde den 250-zeiligen Entwicklungs-Guide sprengen und mischt Zielgruppen (Entwickler vs. Betreiber). `GUIDE_Installation.md` bleibt Entwicklungsanleitung; `INDEX.md` führt das Dokument als Installations-Einstieg. |
| Beispiel-Proxy | nginx + certbot (certbot erneuert über seinen systemd-Timer automatisch) | Auftraggeber-Entscheidung; Caddy nur als einzeilige Alternative. |
| `systemd-run`-Berechtigung für AutoUpdate | polkit-Regel für den Service-Account auf `org.freedesktop.systemd1.manage-units` (Beispiel-`.rules`-Datei in der Doku) | `msTools.Updater` ruft `systemd-run` direkt auf (kein `sudo`-Präfix) und registriert eine **System**-Unit (`VideoWebPlayer-AutoUpdate`, läuft als root); ein sudoers-Eintrag greift deshalb nicht. Die polkit-Freigabe ist weitreichend (Unit-Verwaltung) — die Doku benennt das ehrlich und zeigt `AutoUpdate:Enabled=false` + manuelles Update als Alternative. |
| Ablage der Betreiber-Konfiguration | Hauptweg `appsettings.Local.json` im Anwendungsverzeichnis; Secrets als Umgebungsvariablen über `EnvironmentFile` der Unit | `appsettings.Local.json` wird nie paketiert, übersteht Updates, ist `JsonKeys`-unabhängig (zwingend für `PathBase`); Umgebungsvariablen sind laut `SECRETS_MANAGEMENT.md` die robusteste Secret-Ablage. Regeln (keine Kommentare, `Updates/backup/`-Sicherung) werden benannt statt wiederholt (Verweis). |
| Installationsquelle | Hauptweg: Release-Artefakt `release-linux-x64.zip` aus dem GitHub-Release; Alternative: `dotnet publish -c Release -r linux-x64 --self-contained true` auf einem Build-Host | Self-contained ⇒ kein .NET SDK/Runtime auf dem Zielserver; entspricht der Release-Pipeline (`.github/actions/build-and-package`). |
| systemd-Unit-Härtung | `ProtectSystem=strict` mit `ReadWritePaths=<Anwendungsverzeichnis>`, `PrivateTmp`, `NoNewPrivileges`, `ProtectHome` u. a. | Alle Schreibverzeichnisse (`Data/`, `Logs/`, `Updates/`, `Backups/`, `tools/ffmpeg/`) liegen unter dem Anwendungsverzeichnis — ein einziger `ReadWritePaths`-Eintrag deckt Laufzeit-Schreibzugriff und Self-Update ab. `PrivateTmp` deckt `%TMPDIR%` (Upload-Chunks). `MemoryDenyWriteExecute` wird bewusst nicht gesetzt (Risiko für self-contained .NET). |
| mDNS im Proxy-Betrieb | `Mdns:Enabled=false` (Betreiber-Master-Switch) + UDP-Broadcast-Discovery (Port 5001) mit gesetzter `Discovery:PublicBaseUrl`; `Mdns:Port`-Override nur als Randnotiz | TXT `path=/` ist fest verdrahtet und DNS-SD transportiert kein Schema — das Advertisement würde hinter TLS/Unterpfad eine falsche Adresse vermitteln. |

## Programmabläufe

Keine Programmabläufe im Code — zu dokumentierende Installationsabläufe als Inhalt des neuen
Dokuments `docs/INSTALL_Server.md` (Metadaten-Blockquote nach Konvention von
`GUIDE_Installation.md`: Dokumenttyp, Zielgruppe „Betreiber/Administratoren", Version,
Letzte Aktualisierung; deutsche Sprache, relative Links, Konfigurationsnamen in Backticks).

### Gemeinsamer Basispfad (beide Betriebsarten)

1. **Geltungsbereich/Übersicht:** Nur Linux/systemd; Windows-Produktivbetrieb explizit außerhalb des Scopes (teildokumentiert an anderer Stelle); Überblick über die zwei Betriebsarten mit Auswahlhilfe.
2. **Voraussetzungen:** Linux-Server mit systemd, root-/sudo-Zugang (die Anleitung nutzt `sudo` für Paket-, User-, Unit- und Firewall-Schritte — als Voraussetzung nennen), `curl`, `unzip` (Hauptweg entpackt `release-linux-x64.zip`; auf Minimal-Installationen nicht garantiert — Alternative `busybox unzip`/`python3 -m zipfile` als Fußnote) sowie die nativen OS-Bibliotheken des self-contained `linux-x64`-Builds: mindestens `libicu` (csproj setzt kein `InvariantGlobalization` und bündelt kein ICU-Paket — ohne `libicu` schlägt der erste Start fehl), `libssl`/OpenSSL und `ca-certificates` (TLS-Aufrufe, u. a. Update-Prüfung gegen GitHub) — als konkrete Paketliste je Beispiel-Distribution (apt/dnf) im Dokument. Zusätzlich `python3` (für den `ProtectedFiles`-Merge bei Update-Installationen); **kein** .NET SDK/Runtime nötig (self-contained Artefakte).
3. **Anwendung bereitstellen:** `release-linux-x64.zip` aus dem GitHub-Release herunterladen und nach `/opt/videowebplayer` entpacken (Beispielpfad, konsistent im ganzen Dokument); Ausführbarbit auf dem Hauptbinary `VideoWebPlayer`. Alternativer Weg: `dotnet publish VideoWebPlayer/VideoWebPlayer.csproj -c Release -r linux-x64 --self-contained true` auf einem Build-Host und Publish-Ausgabe kopieren.
4. **Dedizierter Service-Account:** System-User `videowebplayer` (nologin); `chown -R` auf das Anwendungsverzeichnis; Verzeichnisrechte-Tabelle (`Data/` inkl. SQLite-DB/`Backups`/`LiveStream*`, `Logs/`, `Updates/`, `Backups/`, `tools/ffmpeg/` schreibend; `wwwroot/images/genres/` lesend; Anwendungsverzeichnis insgesamt beschreibbar, weil ein Self-Update darin Dateien austauscht).
5. **Betreiber-Konfiguration update-sicher ablegen:** `appsettings.Local.json` (nie paketiert, übersteht Updates, keine Kommentare, wird bei Updates nach `Updates/backup/` gesichert) für nicht-geheime Werte; `EnvironmentFile` der Unit für `Jwt:*`-Secrets (Verweis auf `SECRETS_MANAGEMENT.md`); Vorrangkette kurz erklärt (Local.json überlagert Paket-`appsettings*.json`, verliert gegen Umgebungsvariablen).
6. **systemd-Unit anlegen:** vollständiges Beispiel `/etc/systemd/system/videowebplayer.service` (`User`/`Group`, `WorkingDirectory`, `Environment=ASPNETCORE_ENVIRONMENT=Production`, `EnvironmentFile`, `ExecStart`, `Restart=on-failure`, `After=network-online.target` + `Wants=network-online.target`, `[Install]`-Sektion mit `WantedBy=multi-user.target` — ohne `[Install]` schlägt das dokumentierte `systemctl enable` fehl —, Härtungsset); `systemctl daemon-reload`, `enable --now`, Verifikation `curl http://127.0.0.1:5002/api/health` und `journalctl -u videowebplayer`.
7. **Firewall:** `ufw`-Beispiele (mit firewalld-Hinweis): eigenständig TCP 5002, UDP 5001, UDP 5353; im Proxy-Betrieb 80/443 + UDP 5001, TCP 5002 nicht extern.

Beteiligte Bestandsdokumente (verlinken, nicht duplizieren): `docs/help/updates.md`,
`docs/TECH_Auto_Update.md`, `docs/SECRETS_MANAGEMENT.md`.

### Variante A — Eigenständiger Betrieb ohne Reverse-Proxy

1. Produktions-Bindung `http://*:5002` aus `appsettings.Production.json` — direkt erreichbar, in dieser Variante gewollt; Firewall TCP 5002 + UDP 5001 + UDP 5353.
2. Verifikation von außen: `curl http://<lan-ip>:5002/api/health`.
3. Discovery: UDP-Broadcast-Erkennung (Port 5001) mit automatischer Ableitung aus LAN-Adresse/Port — Loopback- und Wildcard-Adressen werden nie gemeldet; `Discovery:PublicBaseUrl` optional; `Host:Address`/`Host:Port` kurz erwähnt, Details verlinkt (`docs/help/netzwerk-erkennung/installation.md`).
4. mDNS aktiv (UDP 5353); Doppel-Advertisement-Hinweis mit Verweis auf `docs/INSTALL_AVAHI.md`.
5. Bekannte harmlose Laufzeitwarnung benennen: `Failed to determine the https port for redirect` (einmalig; reiner HTTP-Betrieb läuft, kein Redirect möglich); HSTS-Header ohne praktische Wirkung im HTTP-Betrieb.
6. Hinweis: Verkehr unverschlüsselt — für LAN-Betrieb gedacht; Exposition über fremde Netze → Variante B.

Beteiligte Komponenten/Referenzen: `WebApplicationExtensions.UseVideoWebPlayer` (`UseHttpsRedirection`,
`UseHsts`), `UdpDiscoveryListener`, `MdnsAdvertiserWorker`.

### Variante B — Betrieb hinter TLS-terminierendem Reverse-Proxy (nginx + certbot)

1. **Vertrauensbasis erklären:** `X-Forwarded-Proto`/`X-Forwarded-For` werden ausschließlich von Loopback-Absendern akzeptiert (`KnownProxies` fest `127.0.0.1`/`::1`) — der Proxy **muss auf demselben Host** laufen; Proxy auf fremdem Host ist nicht unterstützt.
2. **Loopback-Bindung:** `Kestrel:Endpoints:Http:Url = "http://127.0.0.1:5002"` in `appsettings.Local.json` (in `JsonKeys` geschützt) oder `Kestrel__Endpoints__Http__Url` — die App ist dann am Proxy vorbei nicht erreichbar.
3. **nginx-Serverblock** (vollständig): `proxy_pass http://127.0.0.1:5002;` (ohne URI-Teil, Pfad bleibt erhalten), `proxy_set_header Host $host` (Host-Header-Prüfung lehnt leere/unparsbare Hosts mit 400 ab), `X-Forwarded-Proto $scheme`, `X-Forwarded-For $proxy_add_x_forwarded_for`, WebSocket-`Upgrade`/`Connection`-Header **serverweit bzw. in allen `location`-Blöcken** — zwei WebSocket-Endpunkte existieren: der Blazor-Server-Circuit `/_blazor` (`AddInteractiveServerRenderMode`, ohne Upgrade bleibt die interaktive UI funktionslos) und der SignalR-Hub `/hubs/mediaupdate`; nicht auf einen `location` beschränken. `client_max_body_size` für große Backup-Uploads (`Kestrel:Limits:MaxRequestBodySize` ist unbegrenzt).
4. **TLS über certbot:** `certbot --nginx -d <domain>`; Erneuerung läuft über den certbot-systemd-Timer automatisch (kein eigenes Renewal-Setup nötig). Caddy als kurze Alternativ-Erwähnung (automatisches TLS eingebaut).
5. **Routing-Variante B1 — eigene (Sub-)Domain (Hauptweg):** `location / { … }`, `PathBase` bleibt leer; `Discovery:PublicBaseUrl=https://videos.example.com` als Pflicht (Ableitung sieht nur das Loopback-Backend).
6. **Routing-Variante B2 — Unterpfad:** `PathBase=/videoplayer` **ausschließlich** über `appsettings.Local.json`/Umgebungsvariable `PathBase` (nicht in `AutoUpdate:ProtectedFiles`/`JsonKeys` — Einschränkung offen benennen inkl. Hinweis auf das geplante Folge-Issue); nginx `location /videoplayer/ { proxy_pass http://127.0.0.1:5002; }` (Pfad bleibt erhalten, `UsePathBase` entfernt ihn vor dem Routing); `Discovery:PublicBaseUrl=https://example.com/videoplayer/` vollständig inkl. Schema, Host, Port, Pfad.
7. **Discovery im Proxy-Betrieb:** Pflicht-`Discovery:PublicBaseUrl` — als Admin-Feld „Öffentliche Basis-URL" (`Einrichtung` → `Allgemein`, wirkt ohne Neustart) oder Betreiber-Konfiguration; Validierung (absolute `http`-/`https`-URL, fail-fast) benennen; Details verlinkt (`docs/help/netzwerk-erkennung/`).
8. **mDNS-Umgang (dokumentierte Entscheidung):** `Mdns:Enabled=false` im Proxy-Betrieb + UDP-Broadcast-Discovery mit `PublicBaseUrl`; Begründung (festes `path=/`, kein Schema in DNS-SD) und Randnotiz `Mdns:Port`-Override nur dort, wo ein direktes HTTP-Advertisement bewusst gewollt ist.
9. **Wechselwirkungen benennen:** `X-Forwarded-Proto` macht `UseHttpsRedirection` hinter dem Proxy unproblematisch; HSTS aktiv (Standard `max-age=30 days`) — Browser erzwingen danach HTTPS; Firewall 80/443 + UDP 5001, TCP 5002 nur Loopback.

Beteiligte Komponenten/Referenzen: `UsePathBase`, `UseForwardedHeaders`/`KnownProxies`,
Host-Header-Prüfung (400), `DiscoveryBaseUrlResolver`, `MdnsServiceProfileBuilder`.

### AutoUpdate unter der gehärteten Unit

1. **Grundsatz:** AutoUpdate ist Kernfunktion → unterstützter Hauptweg; Begründung in der Doku.
2. **Dokumentierte Rechte-Ausnahmen:**
   - Schreibzugriff des Service-Accounts aufs Anwendungsverzeichnis (bereits über Besitz + `ReadWritePaths` gegeben — explizit mit Begründung benennen).
   - `python3` auf dem Zielsystem (sonst wird der `ProtectedFiles`-Merge übersprungen — Verweis `docs/TECH_Auto_Update.md`).
   - `AutoUpdate:ServiceName` = Unit-Name **inkl. `.service`-Suffix** (`videowebplayer.service` — das Installationsskript führt `systemctl stop/start/is-active` damit aus; Validierung verlangt `*.service`; Admin-Feld „Dienstname für Neustart" identisch, Verweis `docs/help/updates.md`) sowie `AutoUpdate:WorkspaceUser`/`WorkspaceGroup` = `videowebplayer` (Skript normalisiert `Updates/`-Besitz per `chown`).
   - **`systemd-run`-Berechtigung:** Die Update-Installation läuft als transiente **System**-Unit `VideoWebPlayer-AutoUpdate` (gestartet aus dem App-Prozess, läuft als root); der Service-Account braucht polkit-Freigabe `org.freedesktop.systemd1.manage-units` (Beispiel-`.rules`-Snippet). Tragweite offen benennen (Account darf Units verwalten) — wer das nicht will, nutzt die Alternative unten.
3. **Bewusste Deaktivierung + manuelles Update:** `AutoUpdate:Enabled=false`; manueller Pfad: `release-linux-x64.zip` herunterladen → `systemctl stop videowebplayer` → Paketinhalt über das Anwendungsverzeichnis entpacken (deployment-seitige Werte bleiben erhalten, weil sie in `appsettings.Local.json`/Umgebungsvariablen liegen) → `chown` → `systemctl start`; Diagnosepfad `Updates/update.log` benennen.

Beteiligte Komponenten/Referenzen: `AutoUpdateExtensions` (`UpdateUnitName`),
`msTools.Updater` (transiente Unit, `ProtectedFiles`-Merge), `docs/help/updates.md`,
`docs/TECH_Auto_Update.md`.

### Erstlauf, Verifikation und Troubleshooting

1. Erstlauf-Checkliste: `systemctl status`, `journalctl -u videowebplayer -f`, `curl /api/health`, erste Admin-Anmeldung, Discovery-Test über Client-App.
2. Troubleshooting-Tabelle: Port belegt; HTTP 400 bei fehlendem/ungültigem `Host`-Header (Proxy muss `Host` setzen); Discovery antwortet nicht/falsch → `PublicBaseUrl` prüfen (Verweis `docs/help/netzwerk-erkennung/troubleshooting.md`); Doppel-Advertisement (Verweis `INSTALL_AVAHI.md`); Update-Fehlschlag → `Updates/update.log`, polkit/`python3`/`ServiceName` prüfen; `update.failed`-Marker erwähnen.
3. „Siehe auch"-Block: `GUIDE_Installation.md` (Entwicklung), `INSTALL_AVAHI.md`, `docs/help/netzwerk-erkennung/`, `docs/help/updates.md`, `docs/TECH_Auto_Update.md`, `docs/SECRETS_MANAGEMENT.md`.

## Neue Klassen

Keine — reine Dokumentationsaufgabe.

## Änderungen an bestehenden Klassen

Keine — reine Dokumentationsaufgabe.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — keine neuen Eingaben. Die Dokumentation **beschreibt** vorhandene Validierungen, die für
den Betreiber sichtbar sind (kein Code zu schreiben): `Discovery:PublicBaseUrl` muss eine
absolute `http`-/`https`-URL sein (Start schlägt sonst fehl); `AutoUpdate:ServiceName` muss auf
`*.service` enden; `Mdns:Port` muss im Bereich 1–65535 liegen.

## Konfigurationsänderungen

Keine Änderungen an Konfigurationsklassen oder `appsettings`. Die Dokumentation muss folgende
vorhandene Schlüssel korrekt erklären (alle Betreiber-Ebene, update-sicher):

| Schlüssel | Rolle in der Anleitung |
|-----------|------------------------|
| `Kestrel:Endpoints:Http:Url` | `http://*:5002` (eigenständig, Default aus `appsettings.Production.json`) vs. `http://127.0.0.1:5002` (hinter Loopback-Proxy) |
| `PathBase` | URL-Unterpfad hinter Proxy; **nicht** in `JsonKeys` → zwingend `appsettings.Local.json`/Umgebungsvariable |
| `Discovery:PublicBaseUrl` bzw. Admin-Feld „Öffentliche Basis-URL" | Pflicht hinter TLS-Proxy; absolute URL inkl. Pfad |
| `Mdns:Enabled`, `Mdns:Port` (+ `InstanceName`, `ServiceType`) | `Enabled=false` im Proxy-Betrieb; `Port`-Override als Randnotiz |
| `AutoUpdate:Enabled`, `AutoUpdate:ServiceName`, `AutoUpdate:WorkspaceUser`, `AutoUpdate:WorkspaceGroup`, `AutoUpdate:DownloadPath` | Self-Update unter systemd; `ServiceName` = Unit-Name inkl. `.service`; Deaktivierung als dokumentierte Alternative |
| `Host:Address`/`Host:Port` | Steuerung der Discovery-Ableitung (eigenständiger Betrieb) |
| `Jwt:*` (`Key`, `Issuer`, `ApiToken:Web`, `ApiToken:Maui`) | Produktive Secrets via `EnvironmentFile`/Umgebungsvariablen — Verweis `SECRETS_MANAGEMENT.md` |
| `ASPNETCORE_ENVIRONMENT=Production` | Aktiviert `appsettings.Production.json` (Port 5002, Logging-Level) |

## Seiteneffekte und Risiken

- **`GUIDE_Installation.md` (Nebenbefund, vorhandene Doku-Drift):** verlangt noch die
  Dateireferenz `lib/msTools.Updater/msTools.Updater.dll` (Zeilen 16/230 ff. „Häufige Fehler") —
  tatsächlich ist `msTools.Updater` seit 0.11.0-rc.6 `PackageReference` aus `lib/packages`.
  Korrektur ist sinnvoll, weil die neue Doku denselben Gegenstand beschreibt; läuft als
  **eigener Commit** außerhalb des Kernauftrags (AGENTS.md §7) und wird im Handover benannt.
  `TECH_Auto_Update.md` hat denselben veralteten Absatz — bleibt unverändert (nicht angefasst,
  um den Scope klein zu halten; ggf. Folge-Issue).
- **Konsistenzrisiko:** Die neue Anleitung muss exakt das Code-Verhalten abbilden
  (`KnownProxies` Loopback, `PathBase`-Middleware, Discovery-Vorrangkette, mDNS-Port-Ableitung,
  `ProtectedFiles`/`JsonKeys` ohne `PathBase`). Abweichungen sind durch die Akzeptanzszenarien
  und das gedankliche Durchspielen abzufangen.
- **SecretScan (Pre-Commit-Hook):** Alle Beispielwerte müssen synthetische Platzhalter sein
  (`<videos.example.com>`, `<PRODUKTIVER_JWT_KEY>` etc.) — keine echten Tokens/Keys.
- **MarkdownLinkCheck:** Neue Links auf `help/netzwerk-erkennung/*` etc. inkl. Fragment-Ankern
  müssen exakt existieren (Groß-/Kleinschreibung, GitHub-Anchor-Regeln).

## Umsetzungsreihenfolge

1. **`docs/INSTALL_Server.md` neu anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Vollständiges Dokument nach den Abschnitten in „Programmabläufe" — gemeinsamer Basispfad, Variante A, Variante B (B1 + B2), AutoUpdate (unterstützt + deaktiviert/manuell), Erstlauf/Verifikation, Troubleshooting, „Siehe auch". Nur im Code vorhandene Mechanismen beschreiben; Einschränkungen offen benennen; synthetische Platzhalter; relative Links.

2. **`docs/GUIDE_Installation.md` verlinken**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Kurzer Abschnitt „Produktionsinstallation (Linux-Server)" — Zielgruppe und Inhalt in zwei Sätzen + Link `./INSTALL_Server.md`; bestehender Entwicklungsinhalt bleibt unverändert.

3. **DLL-Drift-Korrektur in `GUIDE_Installation.md` (eigener Commit, außerhalb Kernauftrag)**
   - Voraussetzungen: Keine.
   - Beschreibung: `lib/msTools.Updater/msTools.Updater.dll`-Referenzen (Voraussetzungen + „Häufige Fehler") auf den tatsächlichen Einbindungsweg (`PackageReference` aus `lib/packages` via `NuGet.config`) korrigieren.

4. **`docs/INDEX.md` ergänzen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Eintrag für `INSTALL_Server.md` in der Tabelle „Einstieg" (Produktionsinstallation unter Linux/systemd).

5. **`README.md` ergänzen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Link auf `docs/INSTALL_Server.md` in der Dokumentations-Linkliste (und ggf. ein Satz im Konfigurations-/Betriebskontext).

6. **`docs/RELEASE_NOTES.md` ergänzen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Je ein Bullet in „What's New" (EN) und „Neuerungen" (DE), neueste oben: neue Produktions-Installationsanleitung `docs/INSTALL_Server.md` (Issue #370).

7. **Qualitätsprüfung**
   - Voraussetzungen: Schritte 1–6.
   - Beschreibung: `dotnet run --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .` muss grün bleiben (inkl. Fragment-Anker); SecretScan im Pre-Commit-Hook ohne Befund; gedankliches Durchspielen beider Pfade gegen einen frischen Server (Akzeptanzszenarien).

## Tests

### Neue Tests

Keine neuen Code-Tests — keine Code-Änderung.

| Prüfung | Werkzeug | Was wird geprüft |
|---------|----------|-------------------|
| Markdown-Link-Check über das Repo | `dotnet run --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .` | Alle neuen/geänderten lokalen Links inkl. Fragment-Anker und Casing |
| SecretScan (implizit über Pre-Commit-Hook) | `dotnet run --project tools/SecretScan/SecretScan.csproj -- --root .` | Keine echten Secrets in den Dokumentationsbeispielen |
| Gedankliches Durchspielen | manuell, gegen Akzeptanzszenarien | Lückenlosigkeit beider Installationspfade von „leerer Server" bis „läuft" |

### Betroffene bestehende Tests

Keine.

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich. **Begründung:** Die Anforderung berührt keinen über UI oder
Nutzeraktion erreichbaren Ablauf — es werden ausschließlich Markdown-Dokumente erstellt/geändert.
Der „Benutzerfluss" der Anforderung (Betreiber installiert einen Server) läuft außerhalb der
Anwendung und kann nicht per Playwright abgedeckt werden; der vorgesehene Funktionsnachweis ist
das gedankliche (oder reale) Durchspielen der Anleitung gegen einen frischen Server plus der
lokale Markdown-Link-Check — beides ist in den Akzeptanzszenarien verankert.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path eigenständig: leerer Server → laufende, auffindbare App | Betreiber folgt `INSTALL_Server.md` Basispfad + Variante A | Frischer Linux-Server mit systemd; noch kein Account, kein Verzeichnis, keine Unit, keine Konfiguration, keine OS-Pakete über Minimal-Installation hinaus | Systempakete installieren (`unzip`, `libicu`, `libssl`, `ca-certificates`, `python3`) → Artefakt ablegen/entpacken → Service-Account → Rechte → Local.json/Secrets → Unit enable → Firewall → Health-Check | `curl http://<lan-ip>:5002/api/health` antwortet; Client-App findet den Server per UDP-Broadcast (5001) und mDNS (5353) | Gedankliches Durchspielen: jeder Schritt ohne ungedeckte Voraussetzung (inkl. nativer Bibliotheken — `libicu`-Fehlerbild beim Erststart ist in der Paketliste abgedeckt); MarkdownLinkCheck grün |
| 2 | Happy Path Proxy + (Sub-)Domain: leerer Server → https per nginx | Betreiber folgt Basispfad + Variante B1 | Wie Szenario 1; DNS-Eintrag auf die Server-IP zeigt bereits | Loopback-Bindung → nginx-Serverblock → `certbot --nginx` → `Discovery:PublicBaseUrl` (Admin-Feld oder Konfiguration) → `Mdns:Enabled=false` → Firewall 80/443 + UDP 5001 | `https://videos.example.com` lädt die Web-UI; Redirect auf https wirkt; Discovery meldet die https-URL; kein doppeltes/falsches mDNS | Gedankliches Durchspielen; Dokument enthält vollständigen nginx-Block und certbot-Schritt ohne Lücke |
| 3 | Happy Path Unterpfad: App unter `https://example.com/videoplayer/` | Betreiber folgt Variante B2 | Wie Szenario 2 | `PathBase` in `appsettings.Local.json` → `location /videoplayer/` → `Discovery:PublicBaseUrl` inkl. Pfad → Einschränkungen gelesen | Web-UI unter dem Unterpfad erreichbar; Discovery meldet URL inkl. Pfad; Doku benennt offen: `PathBase` nicht in `ProtectedFiles` (deswegen Local.json), mDNS-`path=/` ungeeignet | Gedankliches Durchspielen; Dokument deckt alle Einschränkungen ab |
| 4 | Self-Update unter gehärteter Unit | Ein Release-Update wird installiert (automatisch oder ausgelöst) | Installation nach Szenario 2 inkl. AutoUpdate-Abschnitt | polkit-Regel + `python3` + `ServiceName=videowebplayer.service` + `WorkspaceUser`/`Group` gesetzt → Update läuft als transiente Unit `VideoWebPlayer-AutoUpdate` | Neue Version läuft nach `systemctl start` durch das Installationsskript; deployment-seitige Werte überleben (`Local.json`/`JsonKeys`-Merge); Diagnosepfad `Updates/update.log` benannt | Dokument listet alle vier Rechte-Voraussetzungen; Gedankenspiel gegen `TECH_Auto_Update.md`-Ablauf |
| 5 | Bewusst deaktiviertes AutoUpdate + manuelles Update | Betreiber entscheidet sich gegen die polkit-Ausnahme | Installation nach Szenario 1 oder 2 mit `AutoUpdate:Enabled=false` | Manueller Pfad: ZIP laden → `systemctl stop` → entpacken → `chown` → `systemctl start` | App läuft auf neuer Version; `appsettings.Local.json` und Unit unverändert erhalten | Dokument enthält den manuellen Ablauf lückenlos |
| 6 | Fehlerfall: Reverse-Proxy auf anderem Host gewünscht | Betreiber plant getrennten Proxy-Host | – | Doku-Abschnitt „Vertrauensbasis" lesen | Ausschluss ist eindeutig dokumentiert (`KnownProxies` Loopback; `X-Forwarded-*` würden ignoriert) — kein falscher Weg beschrieben | Sichtprüfung der Ausschluss-Formulierung |
| 7 | Link-/Platzhalter-Integrität | Pre-Commit/Review | Alle Dokumente geändert | `MarkdownLinkCheck --root .`, SecretScan | Exit-Code 0; keine echten Secrets; Fragment-Anker auf `help/netzwerk-erkennung/*` etc. auflösbar | Tool-Lauf (Lokalnachweis) |

## Offene Punkte

Keine — die sechs Issue-Fragen sind durch die Auftraggeber-Entscheidungen beantwortet und als
Designentscheidungen bzw. bestätigte Scope-Ausschlüsse eingearbeitet.
