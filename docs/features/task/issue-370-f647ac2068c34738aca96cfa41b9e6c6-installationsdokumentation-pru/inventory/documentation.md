# Dokumentations-Bestand

Struktur, Stil und Verlinkungskonventionen der bestehenden Dokumentation sowie der
bereits vorhandene Installations-/Betriebs-Inhalt. Reine Faktenlage, keine Bewertung.

## Stil- und Verlinkungskonventionen

- Sprache der Doku: **Deutsch** (Ausnahme: `docs/RELEASE_NOTES.md` ist zweisprachig —
  englische Sektionen `## Important Notes Before Update`/`## What's New` zuerst,
  deutsche Spiegelung `## Wichtige Hinweise vor dem Update`/`## Neuerungen` darunter).
- `docs/GUIDE_Installation.md`, `docs/SECRETS_MANAGEMENT.md`, `docs/PUBLICATION_CHECKLIST.md`
  beginnen mit einem Metadaten-Blockquote: `> **Dokumenttyp** … > **Zielgruppe** …
  > **Version** … > **Letzte Aktualisierung** …` — `INSTALL_AVAHI.md` und
  `TECH_Auto_Update.md` haben keinen solchen Block.
- Links sind **relativ** (`./GUIDE_Installation.md`, `./help/updates.md`,
  `help/netzwerk-erkennung/index.md`); Code-/Konfigurationsnamen in Backticks.
- Lokale Links werden von `tools/MarkdownLinkCheck` inkl. Fragment-Ankern
  (`#überschrift`) und exakter Groß-/Kleinschreibung geprüft — siehe
  [ci-quality.md](ci-quality.md).
- `docs/help/*` folgt einem Feature-Ordner-Muster (`index.md` + `beschreibung.md`,
  `installation.md`, `einrichtung-anwender.md`, `troubleshooting.md`, `ablauf-technisch.md`,
  `api.md`, `architektur.md`, `datenmodell.md`, `business-rules.md` — je Thema
  unterschiedlich vollständig) mit „← Zurück zur Übersicht"-Fußzeilen.

## `docs/GUIDE_Installation.md` (250 Zeilen)

Aktueller einziger Installationsleitfaden; **entwicklungsorientiert**
(`git clone`, `dotnet restore`/`build`, `dotnet run`, User Secrets). Aufbau:

| Abschnitt | Inhalt |
|-----------|--------|
| Voraussetzungen | .NET 10 **SDK**, Git, NuGet.org, lokale Paketquelle `lib/packages/` aus `NuGet.config` — und **veraltet**: „Lokale DLL `lib/msTools.Updater/msTools.Updater.dll`" (Zeile 16); Schreibrechte für SQLite, Logs, Backups, Updates, generierte Bilddateien |
| Linux: Web installieren | `git clone` → `dotnet restore`/`build` → `dotnet user-secrets` → `dotnet run` → `curl http://localhost:5039/api/health` |
| Windows: Web installieren | PowerShell-Pendants, gleiche Struktur |
| Produktive Konfiguration | Umgebungsvariablen `Jwt__*`; langer Hinweisabsatz zu IIS-`web.config`-`environmentVariables`, `ProtectedFiles`-Merge, `python3`-Bedarf unter Linux, `appsettings.Local.json` (Zeilen 88–108) — bisher der einzige „Produktions"-Abschnitt, aber ohne Installationsweg |
| Geräte-Pairing | Admin-Ablauf + optionale `Pairing:*`-Konfiguration |
| Netzwerk-Erkennung (Discovery) | Beide Kanäle: UDP-Broadcast 5001 (Vorrangkette Admin-Feld → `Discovery:PublicBaseUrl` → Ableitung; IIS/Proxy-Hinweis „explizite URL Pflicht", Zeile 153) und mDNS (`Mdns:*`-Tabelle, Doppel-Advertisement-Hinweis mit Link auf `INSTALL_AVAHI.md`, IIS-Hinweis `Mdns:Port` auf Site-Port, Zeile 175) |
| Lokale Git-Hooks aktivieren | `core.hooksPath .githooks`, Hook-Inhalt |
| Tests | `dotnet test VideoWebPlayer.Tests/…`, `tools/MarkdownLinkCheck.Tests/…`, `ApiDocumentationContractTests`-Filter |
| Häufige Fehler | NuGet-Quelle, `msTools.Updater.dll` (veraltet), Port belegt, Health-Check `401` |
| Nächste Schritte | Links auf `API.md`, `SECRETS_MANAGEMENT.md`, `PUBLICATION_CHECKLIST.md` |

**Nicht enthalten:** `dotnet publish`/Deployment von Publish- oder Release-Artefakten,
Service-Account, systemd-Unit, Dateirechte je Verzeichnis, Firewall-Freigaben,
Reverse-Proxy/TLS, `PathBase`, Windows-Dienst/IIS-Installationsschritte.

## `docs/INDEX.md` (47 Zeilen)

- Tabelle „Einstieg": `GUIDE_Installation.md` („Linux-/Windows-Einrichtung, Start, Tests
  und Fehlerbehebung"), `API.md`, `SECRETS_MANAGEMENT.md`, `PUBLICATION_CHECKLIST.md`,
  `PUBLICATION_AUDIT.md`.
- Tabelle „Technische Dokumentation": u. a. `TECH_Auto_Update.md`, `INSTALL_AVAHI.md`.
- Tabelle „Benutzer- und Admin-Hilfen": Links in `docs/help/`.
- Abschnitt „Lokale Qualitätsprüfungen" listet Build, beide Testsuiten und den
  MarkdownLinkCheck-Aufruf.

## `README.md` (136 Zeilen)

- „Schnellstart" = Entwicklungsbefehle, verweist auf `docs/GUIDE_Installation.md` (Zeile 65).
- „Konfiguration" (Zeilen 77–93) beschreibt bereits: Secrets über User Secrets/Umgebungsvariablen,
  `appsettings.Local.json` als update-sichere Ablage, `Backups:MaxUploadSizeBytes` +
  `Kestrel:Limits:MaxRequestBodySize` (IIS-OutOfProcess-Hinweis), Pairing,
  mDNS/Discovery inkl. „unter IIS/Reverse-Proxy/TLS-Terminierung ist eine explizite URL Pflicht",
  ffmpeg/ffprobe-Nachinstallation nach `tools/ffmpeg/`.
- „Dokumentation" (Zeilen 95–110): Linkliste u. a. auf Installationsanleitung,
  `docs/help/netzwerk-erkennung/index.md`, `docs/help/updates.md`.
- „Entwicklung": Testbefehle inkl. Zwei-Lanes-Erklärung und Hook-Aktivierung.

## `docs/RELEASE_NOTES.md` (326 Zeilen)

Zweisprachige Bullet-Listen (EN zuerst: `## Important Notes Before Update`,
`## What's New`; danach DE: `## Wichtige Hinweise vor dem Update`, `## Neuerungen`).
Neue Einträge werden als Bullets den bestehenden **vorangestellt** (neueste oben).

## Bereits dokumentierte Discovery-/Betriebs-Inhalte (verlinken, nicht duplizieren)

- `docs/INSTALL_AVAHI.md` (101 Zeilen): Avahi-**Alternativ**weg — seit In-App-mDNS nicht
  mehr Standard; Doppel-Advertisement-Vermeidung (`Mdns:Enabled=false` als
  Betreiber-Master-Switch statt nur Admin-Schalter), Avahi-Installation
  (apt/dnf), statische Dienstdatei `/etc/avahi/services/videowebplayer.service`,
  `systemctl`-Beispiele für `avahi-daemon`, Verifikation per `avahi-browse`,
  Firewall-Hinweis UDP 5353 + Fallback UDP 5001. Enthält die einzigen vorhandenen
  `systemctl`-Befehlszeilen der Doku — nur für den Avahi-Daemon, nicht für die App.
- `docs/help/netzwerk-erkennung/` (10 Dateien):
  - `installation.md`: `Discovery:PublicBaseUrl`/`Host:Address`/`Host:Port`/Kestrel-Tabelle,
    Umgebungsvariable `Discovery__PublicBaseUrl`, `JsonKeys`-Schutz; Firewall UDP 5001 (+5353);
    Abschnitt „IIS OutOfProcess / Reverse-Proxy / TLS-Terminierung" = explizite URL Pflicht;
    Hinweis auf automatische EF-Migration `AddSetupDiscoveryPublicBaseUrl`.
  - `einrichtung-anwender.md`: Admin-Feld „Öffentliche Basis-URL" (`Einrichtung` → `Allgemein`),
    Vorrang vor Betreiber-Konfiguration, sofort wirksam, Backup-/Restore-Hinweis.
  - `troubleshooting.md`: unbrauchbare/keine Discovery-Antwort, Validierungsfehler,
    Admin-Wert-Vorrang, Restore auf neuem Host.
- `docs/help/einrichtung.md` (Zeilen 27–39): Admin-Schalter „Server per mDNS …" und
  „Öffentliche Basis-URL" kompakt.
- `docs/help/updates.md` (128 Zeilen): Update-Admin-UI; Absatz „deployment-seitige
  Dateien" (Zeilen 44–46) mit `ProtectedFiles`, `Updates/backup/`, `python3` unter
  Linux, `appsettings.Local.json`-Regeln; „Dienstname für Neustart" (Zeile 70 —
  muss zum tatsächlichen Dienst/systemd-Unit-Namen passen); IIS-Abschnitt;
  Fehlerdiagnose `Updates/update.log`, `update-failed.ps1`.
- `docs/TECH_Auto_Update.md` (224 Zeilen): gesamte Update-Technik — `ProtectedFiles`-Merge
  (Sicherung → Kopieren → Merge, `Updates/backup/` ohne Retention), `python3`-Bedarf
  unter Linux, Linux-Installation als transienter `systemd-run`-Service (Unit
  `VideoWebPlayer-AutoUpdate`), `StopHostAfterScriptStart`, `update.failed`-Marker,
  `appsettings.Local.json`-Abschnitt, Release-Artefakte (`release-metadata.json`,
  `update.json`). **Veraltet:** Zeilen 10–12 beschreiben die Einbindung als
  Dateireferenz `lib/msTools.Updater/` — heute `PackageReference` aus `lib/packages`
  (siehe [code-facts.md](code-facts.md)).
- `docs/SECRETS_MANAGEMENT.md` (101 Zeilen): Secret-Tabelle (`Jwt:Key`,
  `Jwt:ApiToken(:Web/:Maui)`, `Tmdb:ApiKey`), Produktions-Ablage als Umgebungsvariablen,
  `appsettings.Local.json`-Einschränkungen (keine Kommentare, Secrets landen in
  `Updates/backup/`), IIS-`web.config`-Hinweise, Vorgabe „synthetische Platzhalter".
- `docs/Anforderung_msTools_Updater_Dateierhalt.md`: Anforderungsdokument zur
  `ProtectedFiles`-Thematik (Hintergrund, kein Betreiber-Guide).

## Nicht vorhanden

- Keine systemd-Unit für den VideoWebPlayer selbst, kein `systemctl enable/…` für die App,
  kein `dotnet publish`-/Release-ZIP-Deployment, kein Service-Account, keine
  Verzeichnisrechte-Tabelle, keine Firewall-Anleitung (`ufw`/`firewalld`/`nftables`),
  kein nginx-/Apache-/Caddy-Beispiel, kein certbot-/TLS-Abschnitt.
- `nginx`, `certbot`, `caddy` kommen im gesamten Repo **nur** in der Anforderung
  (`requirement.md`) vor — im Code und in der Bestandsdoku kein einziger Treffer.
- Kein Dokument unter `docs/` behandelt `PathBase` (nur `requirement.md` und der Code).
- Windows-Produktivbetrieb (IIS `OutOfProcess`) ist verstreut teildokumentiert
  (`web.config`, `Kestrel:Limits`, Discovery-/mDNS-IIS-Hinweise, Update-under-IIS in
  `help/updates.md`), aber ebenfalls ohne vollständigen Installationsweg.
