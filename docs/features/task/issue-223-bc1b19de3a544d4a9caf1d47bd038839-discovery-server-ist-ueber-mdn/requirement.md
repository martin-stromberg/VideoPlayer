# Übersetzte Anforderung: mDNS-Discovery für den Server (Issue #223)

## Fachliche Zusammenfassung

Der `VideoWebPlayer`-Server soll sich per mDNS/DNS-SD (Multicast DNS, UDP-Port 5353) im lokalen
Netzwerk selbst als Dienst announcen, damit Clients (MAUI-App `VideoPlayer.Maui`, Alt-App) ihn per
Dienstsuche automatisch finden können. Bisher ist nur die zweite Hälfte des Discovery-Protokolls
implementiert: `UdpDiscoveryListener` beantwortet UDP-Broadcasts auf Port 5001
(`VIDEOWEBPLAYER_DISCOVERY` → `VIDEOWEBPLAYER_SERVER:<adresse>`); eine mDNS-Advertisement-Seite
existiert im Server nicht. Der announcete Dienst muss eindeutig als VideoPlayer-Server erkennbar
sein — idealerweise über einen dedizierten Diensttyp (z. B. `_videoplayer._tcp.local.`) oder
mindestens über einen erkennbaren Instanznamen/TXT-Record unter `_http._tcp.local.`, damit Clients
nicht beliebige HTTP-Dienste fremder Geräte als Server anbieten.

## Betroffene Klassen und Komponenten

- **Neu: mDNS-Advertisement-Komponente** in `VideoWebPlayer/Services/` (Namenskonvention im Projekt:
  `*Service` für Logikklassen, `*Worker` für `BackgroundService`, `UdpDiscoveryListener` als
  Referenz für den Discovery-Bereich). Denkbar als einfache Start/Stop-Klasse analog zu
  `UdpDiscoveryListener` oder als `IHostedService`/`BackgroundService`.
- **`VideoWebPlayer/Program.cs`** (Zeilen 50–57): Der `UdpDiscoveryListener` wird hier direkt
  gestartet, abgeschirmt durch `!app.Environment.IsEnvironment("Testing")`, mit Serveradresse aus
  `Host:Address`/`Host:Port` (Fallback `localhost:5000`). Der mDNS-Start muss dieselbe
  Testing-Abschaltung berücksichtigen (E2E-Tests dürfen keine festen Ports belegen).
- **Alternativ `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`**
  (`AddVideoWebPlayerServices`, Zeilen 284–292): Dort werden Hintergrunddienste per
  `AddHostedService<>` registriert (`MediaSourceScanService`, `ContinueWatchingWorker`,
  `ActorBackfillWorker`, `PlaylistBackfillWorker`) — der sauberere Erweiterungspunkt, falls das
  Advertisement als Hosted Service umgesetzt wird.
- **`VideoWebPlayer/appsettings.json`**: neuer Konfigurationsabschnitt für das Advertisement.
  Achtung: Neue Konfigurationsschlüssel müssen in die `JsonKeys`-Merge-Liste unter
  `AutoUpdate:ProtectedFiles` (Zeilen 117–164) aufgenommen werden, damit sie beim Auto-Update
  erhalten bleiben — dort stehen bereits `Host:Address` und `Host:Port`.
- **`VideoWebPlayer/VideoWebPlayer.csproj`**: Derzeit ist kein mDNS-/Zeroconf-Paket referenziert;
  eine managed Implementierung erfordert ein neues NuGet-Paket (Lizenz-, Release-Build- und
  CI-Prüfung laut Repo-Regeln erforderlich).
- **Tests** in `VideoWebPlayer.Tests/` (Namenskonvention `*Tests.cs`, `*E2ETests.cs`): Unit-Tests
  für die Ableitung von Diensttyp, Instanzname, Port und TXT-Records aus der Konfiguration; echte
  mDNS-Überprüfung manuell per `avahi-browse`/`dns-sd`.
- **Dokumentation**: `docs/INSTALL_AVAHI.md` (beschreibt heute die manuelle Avahi-Einrichtung auf
  Linux als einzigen mDNS-Weg — durch In-App-Advertisement zu aktualisieren), `docs/API.md`
  (Discovery-/Basis-Abschnitt), `docs/GUIDE_Installation.md` (Zeile 53: Discovery-Adresse),
  `docs/help/einrichtung.md`, `README.md`, `docs/RELEASE_NOTES.md`.
- **Keine Datenmodell- oder EF-Migrationsänderung** erkennbar — außer das Verhalten soll
  datenbankseitig über `Setup`/`ProgramSettingsService` administrierbar sein (siehe Offene Fragen).

## Implementierungsansatz

- Advertisement beim Anwendungsstart registrieren und beim Shutdown sauber deregistrieren
  (Goodbye-Pakete). Zwei Integrationswege stehen zur Wahl: (a) direkter Start in `Program.cs`
  analog zu `UdpDiscoveryListener` mit `Testing`-Guard; (b) als `IHostedService` über
  `AddVideoWebPlayerServices` — dann muss die `Testing`-Umgebung die Registrierung überspringen.
- Port- und Adressermittlung an die bestehende Konvention anlehnen (`Host:Address`/`Host:Port`,
  Fallback 5000, siehe `Program.cs` Zeile 54); für mDNS ist vor allem der announced Port relevant.
  Annahme: Der announced Port entspricht dem HTTP-Endpunkt (`Kestrel:Endpoints:Http:Url`
  berücksichtigen, sofern konfiguriert).
- Diensttyp/Identifikation: dedizierter Typ (z. B. `_videoplayer._tcp.local.` bzw.
  `_videowebplayer._tcp` — letzterer wird in `docs/INSTALL_AVAHI.md` Zeile 65 bereits als Option
  genannt) und/oder erkennbarer Instanzname plus TXT-Records unter `_http._tcp` (die Avahi-Vorlage
  nutzt Instanzname `VideoWebPlayer` und `path=/`). Hinweis: Die App durchsucht laut Anforderung
  aktuell `_http._tcp.local.` — ein dedizierter Typ erfordert eine Abstimmung bzw. Erweiterung der
  Client-Suche, ist vom Server her aber bereits vorzubereiten.
- Implementierungsvarianten (Entscheidung offen): rein managed mDNS-Responder (z. B.
  `Makaretu.Dns`, plattformübergreifend ohne OS-Daemon — der Server läuft unter Windows
  einschließlich IIS/`OutOfProcess` und unter Linux) oder Delegation an OS-Daemons (Avahi unter
  Linux, Bonjour/mDNSResponder unter Windows). Beim Avahi-Weg ist Doppel-Advertisement zu
  vermeiden, falls der Administrator bereits einen statischen Dienst nach `INSTALL_AVAHI.md`
  eingerichtet hat.
- `UdpDiscoveryListener` (Port 5001) bleibt unverändert als Fallback-Kanal bestehen; mDNS ergänzt,
  ersetzt ihn nicht.
- Testbarkeit: Advertiser so kapseln, dass Diensttyp, Instanzname, Port und TXT-Records ohne echtes
  Netzwerk/Multicast prüfbar sind (Interface oder reine Ableitungslogik). Der eigentliche
  mDNS-Versand ist in Unit-Tests nicht sinnvoll verifizierbar; End-to-End-Prüfung erfolgt manuell.
- Keine serverseitige Berechtigungsprüfung nötig: mDNS ist bewusst unauthentifizierte
  Netzwerk-Präsenz (wie der bestehende UDP-Listener); sensible Daten dürfen nicht in TXT-Records
  landen.

## Konfiguration

Vorschlag auf Anwendungsebene (`appsettings.json`, übersteuerbar via `appsettings.Local.json`,
Umgebungsvariablen), z. B. Abschnitt `Discovery` bzw. `Mdns`:

- `Enabled` — Advertisement abschaltbar (Default offen, siehe Offene Fragen)
- `ServiceType` — z. B. `_videoplayer._tcp` (Default in Code)
- `InstanceName` — z. B. `VideoWebPlayer` bzw. Hostname-abgeleitet
- `Port` — Override des announced Ports (Default: `Host:Port` bzw. Kestrel-Endpunkt)
- ggf. `TxtRecords` für Kennung/Pfad

Neue Schlüssel sind in `AutoUpdate:ProtectedFiles[1].JsonKeys` zu ergänzen, damit lokale
Anpassungen ein Update überstehen. Ob zusätzlich eine Admin-Einstellung über `Setup`/
`ProgramSettingsService` (datenbankgestützt, mit EF-Migration) gewünscht ist, ist offen — für ein
reines Netzwerk-Feature erscheint die Datei-/Umgebungskonfiguration ausreichend (Annahme).

## Offene Fragen

1. **Diensttyp-Abstimmung mit dem Client:** Sucht `VideoPlayer.Maui` künftig einen dedizierten Typ
   (z. B. `_videoplayer._tcp.local.`) oder weiterhin `_http._tcp.local.` mit Erkennung über
   Instanzname/TXT-Record? Soll der Server beide Typen parallel announcen (dediziert + generisch)?
   Erfordert Koordination mit dem App-Repository.
2. **Implementierungstechnologie:** Managed mDNS-Responder (neues NuGet-Paket, plattformübergreifend)
   vs. OS-Daemons (Avahi unter Linux; unter Windows ohne Bonjour-Installation keine native
   Advertisement-API in .NET). Lizenz- und Wartungsfrage des Pakets klären.
3. **Verhältnis zur Avahi-Anleitung:** Wird `docs/INSTALL_AVAHI.md` durch die In-App-Lösung
   obsolet (dann Doku anpassen und Doppel-Advertisement bei vorhandener Avahi-Konfiguration
   vermeiden) oder bleibt sie als Alternative dokumentiert?
4. **Default-Verhalten:** Soll das Advertisement standardmäßig aktiv sein? In Umgebungen ohne
   funktionierendes Multicast (Container, VMs, VLANs) schlägt mDNS still fehl — ist ein explizites
   Opt-in sinnvoller?
5. **Announced Adresse/Port:** `Host:Address` fällt derzeit auf `localhost` zurück — für mDNS
   ungeeignet. Soll der Advertiser Hostname/IPs automatisch auflösen und der Port aus den
   tatsächlich gebundenen Kestrel-Endpunkten stammen, statt aus `Host:Port`?
6. **Konfigurierbarkeit:** Reicht die Datei-/Umgebungskonfiguration, oder soll das Feature in der
   Admin-Oberfläche (`Setup`/`ProgramSettingsService`) schaltbar sein (würde EF-Migration und
   Backup-Erweiterung nach sich ziehen)?
