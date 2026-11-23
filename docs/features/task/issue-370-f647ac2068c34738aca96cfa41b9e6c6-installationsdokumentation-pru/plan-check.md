# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Zweite Prüfrunde, vollständige Neuprüfung des aktualisierten Plans. Die beiden Befunde
aus Runde 1 (`plan-check.1.md`) sind eingearbeitet: Die Voraussetzungen enthalten jetzt
die nativen OS-Bibliotheken des self-contained Builds (`libicu`, `libssl`/OpenSSL,
`ca-certificates`), das Entpack-Werkzeug `unzip` inkl. Alternativen (`busybox unzip`,
`python3 -m zipfile`), `python3` für den ProtectedFiles-Merge sowie den zuvor fehlenden
root-/sudo-Zugang (Basispfad Schritt 2, Tasks-Nr. 2). Die Hinweise aus Runde 1 sind
ebenfalls umgesetzt: `After=`/`Wants=network-online.target` und
`[Install] WantedBy=multi-user.target` sind explizit im Unit-Beispiel gefordert
(Basispfad Schritt 6, Tasks-Nr. 6), und die WebSocket-Abdeckung im nginx-Block nennt
beide Endpunkte — den Blazor-Server-Circuit `/_blazor` und den SignalR-Hub
`/hubs/mediaupdate` — mit ausdrücklicher Warnung vor Beschränkung auf einen `location`
(Variante B Schritt 3, Tasks-Nr. 10).

Alle sieben Akzeptanzkriterien sind abgedeckt, die `Scope-Klärung` ist vollständig,
alle sechs Scope-Ausschlüsse tragen Begründung und Status `bestätigt`, und die
Akzeptanzszenarien enthalten vollständige Happy Paths (leerer Server → laufende,
per Discovery auffindbare App, beide Betriebsarten plus Unterpfad-Variante) sowie
Fehler-/Ausschlussfälle (Proxy auf fremdem Host, deaktiviertes AutoUpdate). Die im
Plan genannten technischen Fakten wurden in dieser Runde erneut stichprobenartig im
Code verifiziert und stimmen überein (siehe Hinweise). Der Testumfang ist für eine
reine Dokumentationsaufgabe angemessen und begründet; kein Benutzerfluss in der
Anwendung ist betroffen.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Produktions-/Server-Installationsschritte ergänzt (`dotnet publish`/Deployment der Publish-Artefakte, dedizierter Service-Account, systemd-Unit inkl. Härtung, Verzeichnisrechte `Data/`, `Logs/`, `Updates/`, `Backups/`) | Basispfad Schritte 2–6: `release-linux-x64.zip` als Hauptweg + alternativer `dotnet publish -r linux-x64 --self-contained true`-Weg (Schritt 3); System-User `videowebplayer` mit `chown` und Verzeichnisrechte-Tabelle inkl. `tools/ffmpeg/` und `wwwroot/images/genres/` (Schritt 4); vollständige gehärtete Unit mit `ProtectSystem=strict`+`ReadWritePaths`, `PrivateTmp`, `NoNewPrivileges`, `ProtectHome`, `After=`/`Wants=network-online.target`, `[Install] WantedBy=multi-user.target` (Schritt 6) | Akzeptanzszenario 1 (Happy Path leerer Server) + gedankliches Durchspielen in Qualitätsprüfung Schritt 7 | Abgedeckt |
| Pfad „ohne Reverse-Proxy" vollständig und für sich lauffähig dokumentiert (Bindung/Port, Firewall inkl. UDP 5353/5001, Discovery-Verhalten) | Variante A Schritte 1–6: Bindung `http://*:5002` aus `appsettings.Production.json`, ufw-Beispiele TCP 5002/UDP 5001/UDP 5353, Discovery-Ableitung inkl. Loopback-/Wildcard-Ausschluss, mDNS aktiv, `UseHttpsRedirection`-Warnung und HSTS-Hinweis benannt | Akzeptanzszenario 1 | Abgedeckt |
| Pfad „mit Reverse-Proxy" vollständig (TLS-Terminierung, `PathBase`, `Discovery:PublicBaseUrl`/Admin-Feld, Loopback-Proxy-Vertrauen, dokumentierter mDNS-Umgang) | Variante B Schritte 1–9: `KnownProxies`-Loopback-Erklärung, Loopback-Bindung via `appsettings.Local.json`, vollständiger nginx-Serverblock (inkl. WebSocket-Header für `/_blazor` **und** `/hubs/mediaupdate`, `client_max_body_size`), `certbot --nginx`, B1 (Sub-)Domain + B2 Unterpfad via `PathBase`, Pflicht-`Discovery:PublicBaseUrl` (Admin-Feld oder Konfiguration), `Mdns:Enabled=false` mit Begründung | Akzeptanzszenarien 2, 3 und 6 | Abgedeckt |
| Betreiber-Konfigurationswerte update-sicher platziert (`appsettings.Local.json`/Umgebungsvariablen, wo nicht in `AutoUpdate:ProtectedFiles`) | Basispfad Schritt 5 (`appsettings.Local.json` nie paketiert/übersteht Updates + `EnvironmentFile` für `Jwt:*`-Secrets, Vorrangkette) + Variante B Schritte 2/6 (`PathBase` zwingend via `appsettings.Local.json`/Umgebungsvariable, `JsonKeys`-Einschränkung offen benannt) | Akzeptanzszenarien 3 und 5 | Abgedeckt |
| `INSTALL_AVAHI.md` und `docs/help/netzwerk-erkennung/*` verlinkt statt dupliziert | Variante A Schritt 4 (Doppel-Advertisement → `INSTALL_AVAHI.md`), Variante B Schritt 7 (Discovery-Details → `help/netzwerk-erkennung/`), „Siehe auch"-Block; Scope-Ausschluss 6 (bestätigt) | Akzeptanzszenario 7 (MarkdownLinkCheck inkl. Fragment-Anker und Casing) | Abgedeckt |
| Keine Verweise auf andere bzw. private Repositories | Scope-Ausschluss 4 (bestätigt); Referenz bleibt das eigene GitHub-Release | Akzeptanzszenario 7 (Review) | Abgedeckt |
| Gedanklich (oder real) mit frischem Server durchgespielt | Qualitätsprüfung Schritt 7 + Akzeptanzszenarien 1–5 (Ausgangszustand „frischer Linux-Server, keine Account/Unit/Konfiguration/OS-Pakete" explizit) | Eigener Nachweisschritt im Plan | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine. Der geplante Nachweisumfang (Markdown-Link-Check inkl. Fragment-Ankern und
Casing, SecretScan über den Pre-Commit-Hook, gedankliches Durchspielen beider Pfade
gegen einen frischen Server anhand der Akzeptanzszenarien) ist für eine reine
Dokumentationsaufgabe ohne Code-Änderung angemessen und vollständig. Betroffene
bestehende Tests gibt es nicht; der Verzicht auf Code-Tests ist korrekt begründet.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Betreiber installiert und betreibt den Server (alle Akzeptanzkriterien) | Kein E2E-Test geplant | Nicht erforderlich mit Begründung: Der „Benutzerfluss" liegt außerhalb der Anwendung (Markdown-Dokumente + Server-Shell); kein UI- oder Nutzeraktions-Ereignis in der App betroffen. Ersatznachweis: gedankliches (oder reales) Durchspielen gegen frischen Server + MarkdownLinkCheck — im Plan explizit und nachvollziehbar begründet |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- **Verifizierte Code-Fakten (Stichprobe dieser Runde, alle bestätigt):**
  `PathBase`→`UsePathBase` als frühe Middleware (`WebApplicationExtensions.cs`
  Z. 47–51); `UseForwardedHeaders` mit `XForwardedProto|XForwardedFor` und
  `KnownProxies` fest `127.0.0.1`/`::1` (Z. 58–64); Host-Header-Prüfung mit 400
  (Z. 69–78); `UseExceptionHandler`/`UseHsts` nur außerhalb Development (Z. 81–89);
  `UseHttpsRedirection` (Z. 91); **beide WebSocket-Endpunkte im Code belegt**:
  `AddInteractiveServerRenderMode` (Z. 124–125 → `/_blazor`) und
  `MapHub<MediaUpdateHub>("/hubs/mediaupdate")` (Z. 130). Produktions-Bindung
  `http://*:5002` und `MaxRequestBodySize=0` (`appsettings.Production.json`
  Z. 18–26). UDP-Discovery-Port 5001 fest (`Program.cs` Z. 53).
  `appsettings.Local.json` nie paketiert (`VideoWebPlayer.csproj` Z. 64–67).
  `PathBase` fehlt nachweislich in `JsonKeys` (`appsettings.json` Z. 155–216);
  `Kestrel:Endpoints:Http:Url`, `Host:Address`/`Host:Port`, `AutoUpdate:ServiceName`,
  `AutoUpdate:WorkspaceUser`/`WorkspaceGroup`, `Mdns:*` und
  `Discovery:PublicBaseUrl` sind enthalten. mDNS-TXT `path=/` fest verdrahtet
  (`MdnsServiceProfileBuilder.cs` Z. 40). Transiente Unit `VideoWebPlayer-AutoUpdate`
  (`AutoUpdateExtensions.cs` Z. 15). Release-Artefakt `release-linux-x64.zip`
  self-contained (`build-and-package/action.yml` Z. 33/85). `/api/health`
  vorhanden (`HealthController.cs` Z. 9). DLL-Drift-Nebenbefund bestätigt
  (`GUIDE_Installation.md` Z. 16/230–232) und sauber als eigener Commit außerhalb
  des Kernauftrags eingeordnet.
- **Neu verifiziert (Runde-1-Nachbesserungen):** `VideoWebPlayer.csproj` setzt kein
  `InvariantGlobalization` und referenziert kein ICU-Paket — der `libicu`-Bedarf im
  Plan ist korrekt begründet. `unzip`-/`busybox`-/`python3 -m zipfile`-Angaben und
  die `After=`/`Wants=`/`[Install]`-Anforderungen sind fachlich korrekt.
- **`Jwt:*`-Pflicht in Production präzisieren (Empfehlung, kein Befund):** In
  `Production` schlägt der Start fehl, wenn `Jwt:Key`, `Jwt:ApiToken:Web` (bzw.
  Fallback `Jwt:ApiToken`) **oder** `Jwt:ApiToken:Maui` fehlen
  (`ServiceCollectionExtensions.cs` Z. 54–60). Der Plan fordert die Ablage über
  `EnvironmentFile` bereits verbindlich — im Dokument sollte die Pflicht (statt
  „optional") dennoch explizit benannt werden, idealerweise mit dem Fehlerbild
  in der Troubleshooting-Tabelle.
- **polkit-Verfügbarkeit (Empfehlung, kein Befund):** Die dokumentierte polkit-Regel
  setzt einen installierten `polkitd`/`policykit-1` voraus — auf typischen
  systemd-Installationen vorhanden (Recommends), auf Minimal-Installationen nicht
  garantiert. Ein Satz im AutoUpdate-Abschnitt genügt.
- **Tasks-Datei konsistent:** `…-tasks.md` (Nr. 1–23) spiegelt den Plan vollständig
  und korrekt wider, inklusive aller Runde-1-Nachbesserungen und der Markierung der
  DLL-Drift-Korrektur als eigener Commit.
