# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Der Plan ist inhaltlich sehr gründlich: Alle sieben Akzeptanzkriterien sind abgedeckt, die
`Scope-Klärung` ist vollständig, alle sechs Scope-Ausschlüsse tragen Begründung und Status
`bestätigt`, und die Akzeptanzszenarien enthalten vollständige Happy Paths (leerer Server →
laufende, auffindbare App) sowie den Fall gewünschter, aber nicht unterstützter Ausgangslagen
(Proxy auf fremdem Host). Die dokumentierten technischen Fakten wurden stichprobenartig im Code
verifiziert und stimmen überein (siehe Hinweise). **Lücke:** Die Voraussetzungen des geplanten
Dokuments lassen auf einem frischen Server zwei harte Abhängigkeiten unerwähnt — die nativen
OS-Bibliotheken der self-contained Builds (u. a. `libicu`, da kein `InvariantGlobalization`
gesetzt ist) und ein Entpack-Werkzeug für das Release-ZIP. Damit ist der Kernanspruch „leerer
Server → funktionierende Installation" im Plan noch nicht lückenlos vorgegeben.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Produktions-/Server-Installationsschritte ergänzt (`dotnet publish`/Deployment, Service-Account, systemd-Unit inkl. Härtung, Verzeichnisrechte `Data/`, `Logs/`, `Updates/`, `Backups/`) | Basispfad Schritte 3–6 (`release-linux-x64.zip` + alternativer `dotnet publish`-Weg, System-User `videowebplayer`, gehärtete Unit, Rechte-Tabelle inkl. `tools/ffmpeg/`); Umsetzungsreihenfolge Schritt 1 | Akzeptanzszenario 1 (Happy Path leerer Server) + gedankliches Durchspielen in Qualitätsprüfung Schritt 7 | Abgedeckt — mit Einschränkung: Voraussetzungen-Liste des Dokuments unvollständig (siehe unten) |
| Pfad „ohne Reverse-Proxy" vollständig (Bindung/Port, Firewall UDP 5353/5001, Discovery) | Variante A Schritte 1–6 (Bindung `http://*:5002`, ufw, Discovery-Ableitung, mDNS, `UseHttpsRedirection`-Warnung) | Akzeptanzszenario 1 | Abgedeckt |
| Pfad „mit Reverse-Proxy" vollständig (TLS-Terminierung, `PathBase`, `Discovery:PublicBaseUrl`/Admin-Feld, Loopback-Vertrauen, mDNS-Umgang) | Variante B Schritte 1–9 (nginx + certbot, B1 Subdomain + B2 Unterpfad, `KnownProxies`-Erklärung, `Mdns:Enabled=false`) | Akzeptanzszenarien 2, 3 und 6 | Abgedeckt |
| Betreiber-Konfigurationswerte update-sicher platziert (`appsettings.Local.json`/Umgebungsvariablen außerhalb `ProtectedFiles`) | Basispfad Schritt 5 + Variante B Schritte 2/6 (`PathBase` zwingend via `appsettings.Local.json`) | Akzeptanzszenarien 3 und 5 | Abgedeckt |
| `INSTALL_AVAHI.md` und `docs/help/netzwerk-erkennung/*` verlinkt statt dupliziert | Variante A Schritt 4, Variante B Schritt 7, „Siehe auch"-Block | Akzeptanzszenario 7 (MarkdownLinkCheck inkl. Fragment-Anker) | Abgedeckt |
| Keine Verweise auf andere/private Repositories | Scope-Ausschluss 4 (bestätigt) | Akzeptanzszenario 7 (Review) | Abgedeckt |
| Gedanklich/real mit frischem Server durchgespielt | Qualitätsprüfung Schritt 7 + Akzeptanzszenarien 1–5 | Eigener Nachweisschritt im Plan | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine. Der geplante Testumfang (Markdown-Link-Check inkl. Fragment-Ankern, SecretScan über den
Pre-Commit-Hook, gedankliches Durchspielen beider Pfade) ist für eine reine
Dokumentationsaufgabe angemessen und vollständig. Betroffene bestehende Tests gibt es nicht
(keine Code-Änderung); der Verzicht auf Code-/UI-Tests ist im Plan korrekt begründet.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Betreiber installiert und betreibt den Server (alle Akzeptanzkriterien) | Kein E2E-Test geplant | Nicht erforderlich mit Begründung: Der „Benutzerfluss" liegt außerhalb der Anwendung (Markdown-Dokumente + Server-Shell); kein UI- oder Nutzeraktions-Ereignis in der App betroffen. Ersatznachweis: gedankliches Durchspielen gegen frischen Server + MarkdownLinkCheck — im Plan explizit und nachvollziehbar begründet |

## Fehlende oder unvollständige Planbestandteile

- [ ] **Native OS-Abhängigkeiten der self-contained Builds fehlen in den Voraussetzungen** (Basispfad Schritt 2 / Tasks-Nr. 2): `VideoWebPlayer.csproj` setzt kein `InvariantGlobalization` und bündelt kein ICU-Paket — ein `linux-x64`-self-contained-Build braucht auf einem frischen Linux mindestens `libicu` sowie `libssl`/`ca-certificates` (OpenSSL), sonst schlägt der erste Start fehl. Die Bestandsaufnahme nennt die benötigten OS-Bibliotheken bereits (`inventory/code-facts.md` Abschnitt „Publish-/Release-Artefakte"), der Plan übernimmt sie aber nicht in die Voraussetzungen-Liste — ohne sie ist der Happy Path „leerer Server → läuft" nicht lückenlos.
- [ ] **Entpack-Werkzeug für den Hauptweg fehlt:** Der dokumentierte Hauptweg „`release-linux-x64.zip` herunterladen und entpacken" benötigt `unzip` (oder `busybox unzip`/`python3 -m zipfile`), das in den genannten Voraussetzungen (`systemd`, `python3`, `curl`) nicht steht und auf Minimal-Installationen nicht garantiert ist.

## Hinweise

- **Verifizierte Code-Fakten (Stichprobe, alle bestätigt):** `PathBase`→`UsePathBase`
  (`WebApplicationExtensions.cs` Z. 47–51); `UseForwardedHeaders` mit festem Loopback-
  `KnownProxies` (Z. 58–64); Host-Header-400 (Z. 69–78); `UseHsts`/`UseExceptionHandler` nur
  außerhalb Development (Z. 81–89); `UseHttpsRedirection` (Z. 91); Hub `/hubs/mediaupdate`
  (Z. 130). Bindung `http://*:5002` und `MaxRequestBodySize=0` (`appsettings.Production.json`
  Z. 20–24). `PathBase` fehlt nachweislich in `JsonKeys` (`appsettings.json` Z. 155–216);
  `Kestrel:Endpoints:Http:Url`, `Discovery:PublicBaseUrl`, `Mdns:*`, `AutoUpdate:ServiceName`,
  `WorkspaceUser`/`WorkspaceGroup` und `Host:*` sind enthalten. UDP-Discovery-Port 5001 fest
  (`Program.cs` Z. 53); `appsettings.Local.json` optional/`ReloadOnChange` und nie paketiert
  (`VideoWebPlayer.csproj` Z. 64–67). mDNS-TXT `path=/` fest verdrahtet, Port-Kette mit
  Fallback 5000 (`MdnsServiceProfileBuilder.cs` Z. 38–68). `Mdns:Port`-Validierung 1–65535
  (`MdnsOptionsValidator.cs` Z. 21). `Discovery:PublicBaseUrl`-Validierung absolute
  http/https-URL mit `ValidateOnStart` (`DiscoveryUrlRules.cs`, `ServiceCollectionExtensions.cs`
  Z. 313–321). Unit `VideoWebPlayer-AutoUpdate` (`AutoUpdateExtensions.cs` Z. 15);
  `systemd-run`-Aufruf ohne `--user` (System-Unit), `systemctl stop/start/is-active`,
  `chown` über `WorkspaceUser`/`WorkspaceGroup`, `python3`-Merge und `*.service`-Validierungs-
  Regex in `msTools.Updater.dll` verifiziert — die polkit-Analyse des Plans ist korrekt.
  Release-Artefakt `release-linux-x64.zip` self-contained (`build-and-package/action.yml`
  Z. 33/85). `/api/health` existiert (`HealthController.cs`). DLL-Drift-Nebenbefund bestätigt
  (`GUIDE_Installation.md` Z. 16/230–232 vs. `PackageReference` im csproj) und ist sauber als
  eigener Commit außerhalb des Kernauftrags eingeordnet.
- **Unit-Beispiel:** Beim „vollständigen" systemd-Unit-Beispiel darauf achten, dass
  `[Install] WantedBy=multi-user.target` enthalten ist — sonst schlägt das geplante
  `systemctl enable` fehl; sinnvoll außerdem `After=network-online.target` (+ `Wants=`).
- **WebSocket-Abdeckung im nginx-Block:** Der Plan begründet die `Upgrade`/`Connection`-Header
  nur mit `/hubs/mediaupdate`; die interaktive Blazor-Server-Verbindung (`/_blazor`) benötigt
  dieselben Header — im Dokument mitnennen, damit der Leser sie nicht versehentlich auf einen
  Standort beschränkt.
- **Tasks-Datei:** In `…-tasks.md` (Nr. 2) fehlt `sudo`/root-Zugang aus der Voraussetzungen-
  Aufzählung des Plans — bei der Aktualisierung der Voraussetzungen mitergänzen.
