# Bestandsaufnahme: Produktions-/Server-Installationsdokumentation für Linux/systemd

Bestandsaufnahme der bestehenden Dokumentation (`docs/`, `README.md`), der im Code
verankerten Betriebsmechanismen (Middleware, Discovery, AutoUpdate, Konfigurations-
ebenen) und der Qualitätssicherung (Markdown-Link-Check, Git-Hooks, CI) bezogen auf
`requirement.md`: eine reine Dokumentationsaufgabe — zwei vollständige
Produktions-Installationspfade unter Linux/systemd (ohne und mit Reverse-Proxy).
Es sind keine Datenmodell-, Logik- oder UI-Änderungen betroffen; daher gibt es keine
`models.md`/`logic.md`/`enums.md`/`interfaces.md`.

## Zusammenfassung

- **Vorhanden — Mechanismen, die die Anforderung dokumentieren will, existieren alle
  im Code:** `PathBase` → `UsePathBase` (`WebApplicationExtensions.cs` Z. 47–51);
  `UseForwardedHeaders` mit `XForwardedProto|XForwardedFor` und `KnownProxies` fest
  auf Loopback `127.0.0.1`/`::1` (Z. 58–64); `UseHsts`/`UseExceptionHandler` außerhalb
  Development (Z. 81–89); `UseHttpsRedirection` (Z. 91); Produktions-Bindung
  `http://*:5002` (`appsettings.Production.json`); update-sichere Betreiber-Ebene
  `appsettings.Local.json` (`LocalConfigurationExtensions.cs`, nie paketiert);
  UDP-Discovery auf Port 5001 mit Vorrangkette Admin-Feld → `Discovery:PublicBaseUrl`
  → automatische Ableitung; mDNS-Advertisement mit festem TXT `path=/` und
  Port-Ableitung `Mdns:Port` → gebundene Adresse → `Kestrel` → `Host:Port` → 5000;
  AutoUpdate über `msTools.Updater` mit Linux-Installation als transienter
  `systemd-run`-Unit (`VideoWebPlayer-AutoUpdate`), `ProtectedFiles`/`JsonKeys`-
  Schutzliste (ohne `PathBase`), `python3`-Bedarf des Merge.
- **Vorhanden — verlinkbarer Dokubestand:** Discovery-Details in
  `docs/help/netzwerk-erkennung/` (inkl. Reverse-Proxy-/TLS-Pflicht-URL-Hinweis),
  Avahi-Alternativweg in `docs/INSTALL_AVAHI.md` (einzige `systemctl`-Beispiele, nur
  für den Avahi-Daemon), Update-/`ProtectedFiles`-/`python3`-Details in
  `docs/help/updates.md` + `docs/TECH_Auto_Update.md`, Secret-Ablage in
  `docs/SECRETS_MANAGEMENT.md`, `appsettings.Local.json` bereits in `README.md` und
  `GUIDE_Installation.md` beschrieben.
- **Fehlend — was die Anforderung neu verlangt:** kein `dotnet publish`-/Release-ZIP-
  Deployment, kein dedizierter Service-Account, keine systemd-Unit für die App
  (Härtung, `systemctl enable` o. ä.), keine Verzeichnisrechte-Tabelle, keine
  Firewall-Anleitung, kein Reverse-Proxy-/TLS-/certbot-Inhalt — `nginx`/`certbot`/
  `caddy` kommen im gesamten Repo außerhalb der Anforderung nicht vor; `PathBase`
  ist in keinem Doku-Dokument erwähnt. `GUIDE_Installation.md` ist komplett
  entwicklungsorientiert.
- **Nebenbefund (Doku-Drift, vorhanden):** `GUIDE_Installation.md` verlangt noch die
  Dateireferenz `lib/msTools.Updater/msTools.Updater.dll` und benennt deren Fehlen
  als Fehlerbild — tatsächlich wird `msTools.Updater` seit Version 0.11.0-rc.6 als
  `PackageReference` aus `lib/packages` bezogen; `TECH_Auto_Update.md` beschreibt
  denselben überholten Einbindungsweg.
- **Zu beachten:** Release-Pipeline publiziert **self-contained**
  `release-linux-x64.zip` — ein Produktions-Host braucht kein .NET-Runtime/-SDK;
  die Anforderung „Runtime vs. SDK klären" ist damit belegbar. `Data/` ist im
  Quellbaum zugleich Code-Verzeichnis; im Publish-Output nur Laufzeitverzeichnis
  (SQLite-DB, `Data/Backups`, `Data/LiveStream`, `Data/LiveStreamStandby`).
  `wwwroot/images/genres/` wird nur gelesen. `Logs/` legt der Start selbst an;
  `Updates/`/`Backups/` entstehen durch den Updater (`update.log`, `Updates/backup/`).

Test-Ausgangszustand: volle Suite `VideoWebPlayer.Tests` in beiden CI-Lanes
(`Category!=E2E&Category!=OsInterface` + `Category=OsInterface`) — **2971 ausgeführte
Tests: 2971 bestanden, 0 fehlgeschlagen, 2 übersprungen** (1 bewusst geskipptes
E2E-Befund-Dokument, 1 Unix-only-Test unter Windows); `MarkdownLinkCheck.Tests`
6/6 grün; `MarkdownLinkCheck` über das Repo grün (445 Dateien, 292 lokale Links).
Nachweis: [tests.md](inventory/tests.md).

## Details

- [Code-Fakten](inventory/code-facts.md) — Middleware-Pipeline, Startsequenz,
  `appsettings.Local.json`, Discovery/mDNS, AutoUpdate, Konfigurations-Defaults,
  Laufzeit-Verzeichnisse, Publish-/Release-Artefakte
- [Dokumentations-Bestand](inventory/documentation.md) — Aufbau/Stil von
  `GUIDE_Installation.md`, `INDEX.md`, `README.md`, `RELEASE_NOTES.md`; verlinkbare
  Bestandsdokumente; nicht vorhandene Inhalte
- [Qualitätssicherung und CI](inventory/ci-quality.md) — `tools/MarkdownLinkCheck`
  (Mechanik, Aufruf), Pre-Commit-Hook, PR-/Staging-Workflows, Release-Artefakte,
  Relevanz für diese Aufgabe
- [Tests](inventory/tests.md) — Test-Ausgangszustand, Läufe, Übersprungene,
  bereichsrelevante Bestandstests
