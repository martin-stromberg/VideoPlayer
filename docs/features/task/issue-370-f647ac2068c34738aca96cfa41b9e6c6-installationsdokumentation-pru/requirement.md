# Übersetzte Anforderung – GitHub Issue #370

## Fachliche Zusammenfassung

Die Installationsdokumentation des VideoWebPlayers wird um einen echten Produktions-/Server-Installationspfad erweitert bzw. neu strukturiert. Bisher beschreibt `docs/GUIDE_Installation.md` nur den Entwicklungs-Workflow (`git clone`, `dotnet restore`/`build`, `dotnet run`, User Secrets); es fehlen `dotnet publish`/Deployment der Publish-Artefakte, dedizierter Service-Account, systemd-Unit inkl. Härtung, Verzeichnisrechte und Firewall-Freigaben. Die überarbeitete Dokumentation führt einen Leser ohne Vorwissen Schritt für Schritt von einem leeren Linux-Server zu einer funktionsfähigen Installation — in zwei klar getrennten, jeweils vollständig beschriebenen Betriebsarten: eigenständig (eigener Port, direkt erreichbar) und hinter einem Reverse-Proxy (externe TLS-Terminierung, `X-Forwarded-*`-Header, wahlweise Unterpfad via `PathBase` oder eigene (Sub-)Domain). Es werden ausschließlich im Code vorhandene Mechanismen dokumentiert; bestehende Einschränkungen (Loopback-only-Proxy-Vertrauen, mDNS-Limitierungen hinter TLS-Proxy, `PathBase` nicht in `AutoUpdate:ProtectedFiles`) werden offen benannt.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine Produktionsinstallation des VideoWebPlayer-Servers auf einem Linux-System — als Dokumentationsgegenstand: der vollständige Installationsweg einer Betriebsart (ohne Reverse-Proxy bzw. mit Reverse-Proxy) von „leerer Server" bis „Anwendung läuft und per Discovery auffindbar ist". Zum Scope gehören alle Schritte, die auf diesem Weg nötig sind: Veröffentlichung/Ablage der Binärdateien, Service-Account, systemd-Unit (gehärtet), Dateisystemrechte (`Data/`, `Logs/`, `Updates/`, `Backups/`), Betreiber-Konfiguration (`appsettings.Local.json`/Umgebungsvariablen), Firewall-Ports, Discovery-Konfiguration (`Discovery:PublicBaseUrl`, `Mdns:*`) sowie — im Proxy-Pfad — Reverse-Proxy-Konfiguration mit TLS-Terminierung und `PathBase` bei Unterpfad-Routing.
- **Auslösende Ereignisse:** Kein Laufzeit-Ereignis — Einzelmaßnahme. Auslöser ist die Überarbeitung der Produktionsinfrastruktur in einem separaten Projekt (gehärtete Service-Accounts, gehärtete systemd-Units, optionaler Reverse-Proxy mit automatischem TLS); die Anwendungs-Dokumentation muss eigenständig, ohne Verweis auf dieses Projekt, den Betrieb in beiden Varianten abbilden.
- **Erwartete Wirkung:** Ein Betreiber ohne Projektvorwissen kann anhand der Dokumentation einen Server so konfigurieren, dass der VideoWebPlayer funktionsfähig läuft — eigenständig erreichbar oder hinter einem TLS-terminierenden Reverse-Proxy — inkl. korrektem Discovery-Verhalten (UDP-Broadcast Port 5001 mit korrekter `PublicBaseUrl`, bewusster Umgang mit mDNS) und update-sicherer Ablage der Betreiber-Konfiguration. Die Anleitung wurde gedanklich oder real gegen einen frischen Server durchgespielt.
- **Umgang mit fehlenden Objekten:**
  - **Produktions-Installationsanleitung** (fehlt bislang; `GUIDE_Installation.md` ist entwicklungsorientiert): **erzeugen** — entweder als Erweiterung des bestehenden Guides oder als eigenes `docs/`-Dokument mit Verlinkung aus dem Guide; Entscheidung fällt bei der Umsetzung, Ergebnis muss konsistent sein.
  - **Betreiber-Konfigurationswerte, die `AutoUpdate:ProtectedFiles`/`JsonKeys` nicht abdeckt** (konkret `PathBase`): **umgehen über `appsettings.Local.json` oder Umgebungsvariablen** — nie in paketierte `appsettings*.json` schreiben, da sie Update-Installationen nicht überstehen. Ergänzung von `PathBase` zu `JsonKeys` wäre eine separate Code-Änderung (außerhalb des Scopes).
  - **mDNS-Advertisement hinter TLS-terminierendem Proxy** (TXT `path=/` fest verdrahtet, kein Schema in DNS-SD möglich): **deaktivieren bzw. umkonfigurieren** — dokumentierter Umgang: z. B. `Mdns:Enabled=false` im Proxy-Betrieb und Discovery über UDP-Broadcast mit gesetzter `PublicBaseUrl`; wo sinnvoll `Mdns:Port`-Override. Wahl des Umgangs ist in der Doku zu entscheiden und zu begründen.
  - **AutoUpdate unter gehärteter systemd-Unit:** **entweder unterstützen** (Schreibrechte des Service-Accounts auf dem Anwendungsverzeichnis, `python3` für den `ProtectedFiles`-Merge, `AutoUpdate:ServiceName` passend zum Unit-Namen, `systemd-run`-Aufruf muss vom Service-Account erlaubt sein) **oder bewusst deaktivieren** und manuelle Updates beschreiben — Entscheidung mit Begründung in der Doku.
- **Scope-Ausschlüsse:**
  - Keine Code-Änderungen: konfigurierbares `KnownProxies` (aktuell fest `127.0.0.1`/`::1` — Proxy muss auf demselben Host laufen), konfigurierbarer mDNS-`path`-TXT-Record, Aufnahme von `PathBase` in `AutoUpdate:ProtectedFiles`/`JsonKeys` sind allesamt potenzielle Folge-Issues, nicht Teil dieser Aufgabe.
  - Reverse-Proxy auf einem anderen Host wird nicht dokumentiert/unterstützt (würde ignorierte `X-Forwarded-*`-Header bedeuten).
  - Keine Verweise auf andere Repositories — insbesondere keine auf private Repositories oder die externe Pattern-Sammlung; die Dokumentation muss für sich allein stehen.
  - Windows-Produktivbetrieb (IIS `OutOfProcess`, Windows-Dienst) ist an anderer Stelle teildokumentiert und vermutlich außerhalb des Scopes — Bestätigung steht als offene Frage aus.
  - Der bestehende entwicklungsorientierte Inhalt des `GUIDE_Installation.md` bleibt erhalten; er wird ergänzt/verlinkt, nicht ersetzt.
  - `docs/INSTALL_AVAHI.md` und `docs/help/netzwerk-erkennung/*` bleiben eigenständige Dokumente; sie werden verlinkt, nicht dupliziert.

## Betroffene Klassen und Komponenten

Reine Dokumentationsaufgabe — keine Datenmodell-, Logik- oder UI-Änderungen. Betroffene bzw. neu zu erstellende Artefakte:

- **Dokumentation (zu ändern/erstellen):**
  - `docs/GUIDE_Installation.md` — entweder um Produktions-/Server-Installationsteil erweitern oder aus ihm auf ein neues Dokument verlinken.
  - Neues Dokument unter `docs/` (Alternative zur Guide-Erweiterung, z. B. `docs/INSTALL_Server.md` o. ä. — Name bei Umsetzung festlegen).
  - `docs/INDEX.md` — ggf. Eintrag des neuen Dokuments (konsistentes Gesamtbild).
  - `README.md` — ggf. Verlinkung der Produktionsanleitung (AGENTS.md verlangt Aktualisierung bei Bedarf).
  - `docs/RELEASE_NOTES.md` — Eintrag über die Dokumentationsänderung (AGENTS.md verlangt Aktualisierung bei Bedarf).
- **Zu referenzierende (nicht zu ändernde) Bestandsdokumente:**
  - `docs/INSTALL_AVAHI.md` (Avahi-Alternativweg, Doppel-Advertisement)
  - `docs/help/netzwerk-erkennung/` (u. a. `installation.md`, `einrichtung-anwender.md`, `troubleshooting.md`) — Details zu `Discovery:PublicBaseUrl`, mDNS, UDP-Broadcast hierhin verlinken statt wiederholen
  - `docs/help/updates.md` und `docs/TECH_Auto_Update.md` — Details zu `AutoUpdate:ProtectedFiles`, `Merge`-Strategie, `python3`-Abhängigkeit, `Updates/backup/`
  - `docs/SECRETS_MANAGEMENT.md` — Secret-Ablage
- **Code-Referenzen, deren Verhalten die Doku abbilden muss (nur lesend, keine Änderung):**
  - `VideoWebPlayer/Extensions/WebApplicationExtensions.cs` (`UseVideoWebPlayer`): `PathBase` → `UsePathBase` (Zeilen 47–51); `UseForwardedHeaders` mit `XForwardedProto|XForwardedFor` und `KnownProxies` fest auf Loopback (Zeilen 58–64); `UseHsts`/`UseExceptionHandler` außerhalb Development; `UseHttpsRedirection` (Zeile 91).
  - `VideoWebPlayer/appsettings.Production.json`: Bindung `http://*:5002` (`Kestrel:Endpoints:Http:Url`).
  - `VideoWebPlayer/appsettings.json`: Defaults `Mdns:*`, `Discovery:PublicBaseUrl`, `AutoUpdate:*` inkl. `ProtectedFiles`/`JsonKeys` (ohne `PathBase`), `AutoUpdate:ServiceName`, `AutoUpdate:DownloadPath` = `Updates`.
  - `VideoWebPlayer/Services/MdnsServiceProfileBuilder.cs`: TXT-Record `path=/` fest verdrahtet (Zeile 40), kein Schema in DNS-SD; Port-Auflösung `Mdns:Port` → gebundene Adresse → `Kestrel` → `Host:Port` → 5000.
  - `VideoWebPlayer/Extensions/LocalConfigurationExtensions.cs` (o. ä., Aufruf in `Program.cs`): optionale `appsettings.Local.json`, nie paketiert, übersteht Updates.
- **Tests:** Keine neuen Code-Tests zu erwarten (keine Code-Änderung). Qualitätssicherung läuft über den Markdown-Link-Check (`tools/MarkdownLinkCheck`) und das Akzeptanzkriterium „gedanklich/real mit frischem Server durchgespielt".

## Implementierungsansatz

- **Strukturentscheidung (bei Umsetzung zu treffen):** Entweder `docs/GUIDE_Installation.md` um einen Abschnitt „Produktions-/Server-Installation" ergänzen oder ein eigenes Dokument unter `docs/` anlegen und aus dem Guide verlinken. Kriterium: Lesbarkeit und konsistentes Gesamtbild; der Guide bleibt als Entwicklungsanleitung erhalten.
- **Aufbau der Anleitung:** Schritt-für-Schritt von „leerer Server" bis „läuft": .NET-Runtime-Voraussetzung (Publish-Artefakte — Runtime vs. SDK klären), `dotnet publish` bzw. Deployment der Publish-Ausgabe, dedizierter Service-Account, Ablageorte und Rechte (`Data/` inkl. SQLite-DB, `Logs/`, `Updates/`, `Backups/` sowie Schreibrecht auf dem Anwendungsverzeichnis für Self-Updates), gehärtete systemd-Unit, Firewall (`App-Port` Standard 5002, UDP 5353 mDNS, UDP 5001 Broadcast-Discovery), Erstlauf/Health-Check (`/api/health`), Discovery-Konfiguration.
- **Zwei vollständige Pfade:**
  - *Ohne Proxy:* Bindung erklären (`http://*:5002` aus `appsettings.Production.json` ist direkt erreichbar — gewollt in dieser Variante), Discovery-Automatik (Ableitung aus LAN-Adresse funktioniert, `PublicBaseUrl` optional), mDNS aktiv nutzbar, Hinweis zur `UseHttpsRedirection`-Laufzeitwarnung im reinen HTTP-Betrieb (kein Redirect, weil kein HTTPS-Endpunkt).
  - *Mit Proxy:* Bindung auf `http://127.0.0.1:5002` (Loopback-Vertrauen ist die einzige Vertrauensbasis — Proxy muss auf demselben Host laufen, App am Proxy vorbei nicht erreichbar), Proxy-Beispiel mit TLS-Terminierung und `X-Forwarded-Proto`/`X-Forwarded-For`-Weitergabe, Unterpfad-Variante über `PathBase` (in `appsettings.Local.json`/Umgebungsvariable, da nicht in `JsonKeys`) und/oder eigene (Sub-)Domain, Pflicht-`Discovery:PublicBaseUrl` (Admin-Feld „Öffentliche Basis-URL" oder Konfiguration — Verweis auf `docs/help/netzwerk-erkennung/`), dokumentierter Umgang mit mDNS (festes `path=/`, kein Schema → z. B. `Mdns:Enabled=false` + Broadcast-Discovery oder `Mdns:Port`-Override), `X-Forwarded-Proto` macht `UseHttpsRedirection` hinter Proxy unproblematisch, HSTS-Hinweis.
- **Erweiterungspunkte/Hooks:** Keine — keine Code-Änderung. Relevant ist ausschließlich, dass die Doku exakt das vorhandene Verhalten abbildet (siehe Code-Referenzen oben).
- **Abhängigkeiten:** Verlinkung zu `INSTALL_AVAHI.md`, `docs/help/netzwerk-erkennung/*`, `docs/help/updates.md`, `TECH_Auto_Update.md`, `SECRETS_MANAGEMENT.md` statt Inhaltsduplikation; Markdown-Link-Check (`tools/MarkdownLinkCheck`) muss über die geänderten Dokumente weiterlaufen.

## Konfiguration

Die Dokumentation muss folgende, im Code vorhandene Konfigurationsebenen korrekt erklären (alles Betreiber-Ebene, update-sicher über `appsettings.Local.json` im Anwendungsverzeichnis oder Umgebungsvariablen `__`-Notation — `appsettings.Local.json` überlagert Paket-`appsettings*.json`, verliert aber gegen Umgebungsvariablen/Kommandozeile):

| Schlüssel | Rolle in der Anleitung |
|---|---|
| `Kestrel:Endpoints:Http:Url` | Bindung: `http://*:5002` (eigenständig) vs. `http://127.0.0.1:5002` (hinter Loopback-Proxy); in `JsonKeys` geschützt |
| `PathBase` | URL-Unterpfad hinter Proxy; **nicht** in `AutoUpdate:ProtectedFiles`/`JsonKeys` → zwingend `appsettings.Local.json`/Umgebungsvariable |
| `Discovery:PublicBaseUrl` bzw. Admin-Feld „Öffentliche Basis-URL" (`Einrichtung` → `Allgemein`) | Explizite öffentliche Basis-URL der UDP-Broadcast-Discovery-Antwort; Pflicht hinter TLS-Proxy; in `JsonKeys` geschützt |
| `Mdns:Enabled`, `Mdns:Port` (sowie `Mdns:InstanceName`, `Mdns:ServiceType`) | Umgang mit mDNS im Proxy-Betrieb; in `JsonKeys` geschützt |
| `AutoUpdate:*` (`Enabled`, `ServiceName`, `DownloadPath`, `ProtectedFiles`) | Self-Update über `msTools.Updater` (`systemd-run` unter Linux); `ServiceName` muss zum Unit-Namen passen; `python3` für `Merge`; in `JsonKeys` geschützt — oder bewusste Deaktivierung mit manuellem Update-Pfad |
| `Host:Address`/`Host:Port` | Steuerung der automatischen Discovery-Ableitung (eigenständiger Betrieb) |
| `Jwt:*` (Key, Issuer, ApiToken:Web/Maui) | Produktive Secrets — Umgebungsvariablen/Secret-Management, Verweis auf `SECRETS_MANAGEMENT.md` |

## Offene Fragen

Aus dem Issue übernommen — vor oder während der Bearbeitung zu klären:

1. **Proxy-Implementierung:** Welche konkrete Reverse-Proxy-Software soll das Beispiel verwenden (z. B. nginx + certbot, Caddy, Apache)? „Automatisches TLS" ist implementierungsabhängig.
2. **Routing-Variante:** Pfadbasiert mit `PathBase`, eigene (Sub-)Domain — oder beide Varianten dokumentieren? Dokumentationsaufwand unterscheidet sich deutlich.
3. **AutoUpdate unter gehärteter Unit:** Selbst-Update-Mechanismus unterstützen (Rechte/Härtung entsprechend) oder bewusst manuelle Updates beschreiben?
4. **Betriebssystem-Scope:** Ist Linux/systemd das alleinige Ziel? Windows-Produktivbetrieb (IIS `OutOfProcess`, Windows-Dienst) ist teildokumentiert und vermutlich außerhalb — bitte bestätigen.
5. **`PathBase` in `AutoUpdate:ProtectedFiles`/`JsonKeys`:** Ergänzende Code-Änderung als eigenes Issue gewünscht?
6. **mDNS-`path`-TXT-Record:** Soll er bei gesetztem `PathBase` konfigurierbar werden (eigenes Issue), oder reicht die dokumentierte Einschränkung?

Zusätzlich bei der Umsetzung zu entscheiden (nicht blockierend, Ergebnis muss konsistent sein): Guide-Erweiterung vs. eigenes Dokument; konkrete Wahl des dokumentierten mDNS-Umgangs im Proxy-Betrieb.

## Akzeptanzkriterien (aus dem Issue)

- [ ] Produktions-/Server-Installationsschritte ergänzt: `dotnet publish` bzw. Deployment der Publish-Artefakte, dedizierter Service-Account, systemd-Unit inkl. Härtung, Verzeichnisrechte für `Data/`, `Logs/`, `Updates/`, `Backups/`.
- [ ] Pfad „ohne Reverse-Proxy" vollständig und für sich lauffähig dokumentiert (Bindung/Port, Firewall inkl. UDP 5353/5001, Discovery-Verhalten).
- [ ] Pfad „mit Reverse-Proxy" vollständig dokumentiert, inkl. TLS-Terminierung, `PathBase` (bei Unterpfad), `Discovery:PublicBaseUrl`/Admin-Feld, Loopback-Proxy-Vertrauen und dokumentiertem Umgang mit mDNS.
- [ ] Betreiber-Konfigurationswerte update-sicher platziert (`appsettings.Local.json` oder Umgebungsvariablen), wo sie nicht in `AutoUpdate:ProtectedFiles` stehen.
- [ ] `INSTALL_AVAHI.md` und `docs/help/netzwerk-erkennung/*` verlinkt statt dupliziert.
- [ ] Keine Verweise auf andere bzw. private Repositories.
- [ ] Gedanklich (oder real) mit einem frischen Server durchgespielt, ob die Schritte zu einer funktionierenden Installation führen.
