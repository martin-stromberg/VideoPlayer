# Bestandsaufnahme: mDNS-Discovery für den Server (Issue #223)

Bestandsaufnahme des Discovery-Bereichs im `VideoWebPlayer`-Server (UDP-Listener, Startintegration, Konfiguration, Tests, Dokumentation) bezogen auf die Anforderung in `requirement.md`: Der Server soll sich per mDNS/DNS-SD (UDP 5353) selbst announcen, damit die MAUI-App `VideoPlayer.Maui` ihn automatisch findet.

## Zusammenfassung

- **Vorhanden:** `UdpDiscoveryListener` (`VideoWebPlayer/Services/UdpDiscoveryListener.cs`) beantwortet UDP-Broadcasts auf Port **5001** (`VIDEOWEBPLAYER_DISCOVERY` → `VIDEOWEBPLAYER_SERVER:<adresse>`). Er wird direkt in `Program.cs` Zeilen 50–57 gestartet — ohne DI, ohne `IHostedService`, ohne sauberes Shutdown-Handling (`Stop()` wird nirgends aufgerufen) — abgeschirmt durch `!app.Environment.IsEnvironment("Testing")`. Die Antwortadresse kommt aus `Host:Address`/`Host:Port` mit Fallback `localhost:5000`.
- **Fehlt komplett:** Jede Form von mDNS-/Zeroconf-/DNS-SD-Advertisement — keine Implementierung, kein NuGet-Paket (Volltextsuche `mdns`/`zeroconf`/`avahi`/`5353` im Code ohne Treffer), kein Konfigurationsabschnitt, kein Interface, kein Enum. Die MAUI-App (separates Repo) findet den Server heute nur über UDP oder generische `_http._tcp`-Dienste fremder Geräte.
- **Konfiguration:** Es existiert kein `Discovery`-/`Mdns`-Abschnitt in `appsettings.json`. `Host:Address`/`Host:Port` sind dort ebenfalls **nicht gesetzt** — nur in `AutoUpdate:ProtectedFiles[1].JsonKeys` (Zeilen 127–128) als merge-geschützte Schlüssel gelistet. Neue Schlüssel müssen in diese Liste aufgenommen werden. Achtung: `appsettings.Production.json` bindet Kestrel an `http://*:5002` — der tatsächliche Port kann vom `Host:Port`-Fallback abweichen.
- **Erweiterungspunkte:** (a) `Program.cs` analog zum UDP-Listener mit `Testing`-Guard; (b) `AddHostedService<>` in `ServiceCollectionExtensions.AddVideoWebPlayerServices` (Zeilen 284–292) — dort gibt es **keinen** Umgebungs-Guard, die `Testing`-Abschaltung müsste konditional erfolgen.
- **Test-Ausgangszustand:** Alle ausgeführten Suiten grün — 1416 Unit-Tests, 204/205 E2E-Tests (1 absichtlich übersprungen, dokumentierter Produktfehler außerhalb dieser Anforderung), 6 Tools-Tests, 0 Fehlschläge. Die CI-Filter-Kategorie `Integration` trifft keinen einzigen Test. **Es gibt keinerlei Tests für den Discovery-Bereich.** Details und Nachweise: [Tests](inventory/tests.md).

## Details

- [Logik](inventory/logic.md) — `UdpDiscoveryListener`, `Program.cs`-Start, `ServiceCollectionExtensions`, `LocalConfigurationExtensions`, `AutoUpdateExtensions`, `KestrelLimits`, `ProgramSettingsService`
- [Datenmodell](inventory/models.md) — `Setup` (einziger denkbarer Anknüpfungspunkt; keine Discovery-Entität vorhanden)
- [Konfiguration](inventory/configuration.md) — `appsettings*.json`, `ProtectedFiles`/`JsonKeys`, `launchSettings.json`, `web.config`, `NuGet.config`
- [Dokumentation](inventory/documentation.md) — `INSTALL_AVAHI.md` (manueller Avahi-Weg), `API.md`, `GUIDE_Installation.md`, Benutzerdoku ohne Discovery-Abschnitt
- [Tests](inventory/tests.md) — Test-Ausgangszustand mit TRX-Nachweisen, relevante Testklassen und Hilfsmethoden

Keine relevanten Enums und keine projekteigenen Interfaces im Discovery-Bereich gefunden — entsprechende Detaildateien wurden nicht angelegt.
