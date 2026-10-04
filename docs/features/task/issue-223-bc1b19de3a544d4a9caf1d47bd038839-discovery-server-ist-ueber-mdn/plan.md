# Umsetzungsplan: mDNS-Discovery für den Server (Issue #223)

## Übersicht

Der `VideoWebPlayer`-Server soll sich per mDNS/DNS-SD (UDP-Port 5353) im lokalen Netzwerk selbst
announcen, damit die MAUI-App `VideoPlayer.Maui` ihn per Dienstsuche findet. Umgesetzt wird ein
neuer `BackgroundService` (`MdnsAdvertiserWorker`) mit dem managed mDNS-Paket
`Makaretu.Dns.Multicast.New`, einer Optionsklasse `MdnsOptions` inkl. Validierung, einer reinen
Ableitungslogik (`MdnsServiceProfileBuilder`) für Diensttyp, Instanzname, Port und TXT-Records sowie
dem Konfigurationsabschnitt `Mdns` in `appsettings.json`. Announced wird ausschließlich der
dedizierte Diensttyp `_videowebplayer._tcp.local.` (konfigurierbar über `Mdns:ServiceType`). Das
Feature ist zweistufig schaltbar: per Konfiguration (`Mdns:Enabled`, Default `true`) und per
Admin-Schalter in der Oberfläche (`Setup.MdnsAdvertisementEnabled` über `ProgramSettingsService`,
mit EF-Migration und Backup-Erweiterung). Der bestehende `UdpDiscoveryListener` (Port 5001) bleibt
unverändert als Fallback-Kanal bestehen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Hosting des Advertisements | `MdnsAdvertiserWorker` als `BackgroundService`, in `AddVideoWebPlayerServices` konditional registriert (`builder.Environment.IsEnvironment("Testing")` → keine Registrierung) | DI für `IOptions<MdnsOptions>`, `ILogger`, `IServer` (gebundene Kestrel-Adressen) und `IServiceScopeFactory` (DB-Zugriff); `StopAsync` ermöglicht sauberes Deregistrieren per Goodbye-Paketen — der Direktstart in `Program.cs` wie beim `UdpDiscoveryListener` hat kein Shutdown-Handling und würde die Deregistrierung verlieren. Namenskonvention `*Worker` für `BackgroundService` (`ContinueWatchingWorker`, `ActorBackfillWorker`, `PlaylistBackfillWorker`). |
| mDNS-Implementierung | `Makaretu.Dns.Multicast.New` (MIT, gewarteter Fork; `MulticastService` + `ServiceDiscovery`/`ServiceProfile`) | Plattformübergreifend ohne OS-Daemon; unter Windows gibt es ohne Bonjour-Installation keine Advertisement-API, unter Linux wäre Avahi eine zusätzliche Deployment-Voraussetzung. Vor dem Referenzieren: Lizenzdatei und transitive Abhängigkeiten prüfen, CI-`security-scan`-Step beachten, Version wählen, die älter als 7 Tage publiziert ist. |
| Testbarkeit | Reine Ableitungslogik `MdnsServiceProfileBuilder` erzeugt einen `MdnsAdvertisement`-Record; der Worker ist nur Transport | Diensttyp, Instanzname, Port und TXT-Records sind ohne Multicast-Netzwerk prüfbar; der eigentliche mDNS-Versand ist in Unit-Tests nicht sinnvoll verifizierbar und wird manuell geprüft. |
| Port-Ermittlung | Auflösungskette: `Mdns:Port` → tatsächlich gebundene Kestrel-Adressen (`IServerAddressesFeature`, nach `ApplicationStarted`) → `Kestrel:Endpoints:Http:Url` → `Host:Port` → 5000 | `Host:Port` allein läge in Produktion falsch (`appsettings.Production.json` bindet `http://*:5002`); die gebundenen Adressen decken `UseUrls`, `ASPNETCORE_URLS` und Kestrel-Endpoints ab. Der `Mdns:Port`-Override bleibt für IIS-`OutOfProcess` nötig (dort ist die gebundene Adresse der interne Backend-Port, nicht der IIS-Port). |
| Angekündigte Adresse | Hostname des Rechners (SRV/A-Records automatisch vom Responder); `Host:Address` wird nicht herangezogen | `Host:Address` fällt auf `localhost` zurück — für mDNS ungeeignet und unnötig, da der Responder die eigenen Interface-Adressen announcet. |
| Diensttyp | Genau ein `ServiceProfile` unter dem dedizierten Typ `_videowebplayer._tcp.local.` (Default von `Mdns:ServiceType`, konfigurierbar); **kein** paralleles `_http._tcp`-Advertisement | Festlegung durch den Anwender: Die MAUI-App wird auf den dedizierten Typ umgestellt (Abstimmung erfolgt im App-Repository, außerhalb dieses Repos — im Handover/Bericht vermerken). Konfigurierbarkeit über `Mdns:ServiceType` erlaubt einen späteren Wechsel ohne Codeänderung. |
| Identifikation | Feste TXT-Records im Code: `path=/` und `app=VideoWebPlayer`; Instanzname Default `VideoWebPlayer` | Die Erkennungs-Kennung kann nicht wegkonfiguriert werden; sensible Daten gelangen nicht in TXT-Records. Entspricht der bisherigen Avahi-Vorlage in `docs/INSTALL_AVAHI.md`. |
| Konfigurationsabschnitt | Eigener Top-Level-Abschnitt `Mdns` → `MdnsOptions` (statt `Discovery`) | Ein `Discovery:Enabled` wäre mehrdeutig (UDP-Listener Port 5001 bleibt unverändert aktiv); `Mdns` ist eindeutig zugeordnet. |
| Optionsbindung | `MdnsOptions` + `MdnsOptionsValidator` (`IValidateOptions<MdnsOptions>` + `ValidateOnStart()`) | Folgt der bestehenden Konvention `PlaylistSettings` bzw. `EpisodeBackgroundImageOptions`/`EpisodeBackgroundImageOptionsValidator`. |
| Schaltbarkeit — Priorisierungsregel | **Konjunktion:** Advertisement aktiv genau dann, wenn `Mdns:Enabled` (Konfiguration) **und** `Setup.MdnsAdvertisementEnabled` (Admin-Schalter, DB) beide `true` sind. Kein Schalter überstimmt den anderen | `Mdns:Enabled` ist der Betreiber-Master-Switch (Datei-/Umgebungskonfiguration): Er muss Doppel-Advertisement neben einer statischen Avahi-Dienstdatei und den IIS-`OutOfProcess`-Fall hart abschalten können — ein Admin-Schalter in der UI dürfte das nicht aufheben, sonst würde eine UI-Aktion eine bewusst gesetzte Betreiberkonfiguration aushebeln. `Setup.MdnsAdvertisementEnabled` ist der Admin-Laufzeitschalter für den Normalfall. Da beide Schalter unterschiedliche Träger (Betreiber vs. Admin) und Abschaltgründe haben, ist die Konjunktion die einzige Regel ohne Widerspruch. |
| Laufzeitverhalten des Admin-Schalters | Der Worker wertet `Setup.MdnsAdvertisementEnabled` periodisch neu aus (fester Polling-Intervall, Konstante 60 s) und advertised/unadvertised dynamisch (`Advertise`/`Unadvertise` des Pakets) — kein Neustart nötig | Ein Admin-Schalter, der nur nach Neustart wirkt, wäre in der Praxis kaum nutzbar. `Makaretu.Dns.Multicast.New` unterstützt `Unadvertise` (Goodbye-Pakete) zur Laufzeit. DB-Zugriff über `IServiceScopeFactory.CreateScope()` + `ProgramSettingsService` — Muster wie `ContinueWatchingWorker` (Scoped `ApplicationDbContext` aus Singleton-Worker). |
| Admin-Einstellung im Datenmodell | Neue `bool`-Spalte `MdnsAdvertisementEnabled` auf `Setup` (Singleton-Zeile über `ProgramSettingsService`), Default `true` | `Setup` ist der bestehende Träger admin-pflegbarer Programmeinstellungen (`ProgramSettingsService`, Seite `/admin/program-settings`). Default `true` passt zu `Mdns:Enabled = true` — das Feature ist ausgeliefert aktiv, beide Schalter stehen auf „an". Bei Wiederherstellung eines Alt-Backups ohne die Spalte greift `OptionalRestoreBoolDefaults` mit `true`. |
| Speicherung des Admin-Schalters | Eigenes Methodenpaar `GetMdnsAdvertisementEnabledAsync` / `UpdateMdnsAdvertisementEnabledAsync` in `ProgramSettingsService` (statt Erweiterung von `UpdateGeneralSettingsAsync`) | Hält die Signatur von `UpdateGeneralSettingsAsync` stabil (keine Anpassung bestehender Aufrufer/Tests nötig) und folgt dem Muster der anderen dedizierten Getter/Setter (`GetApplicationTitleAsync`, `GetContinueWatchingEndThresholdAsync`). |

## Programmabläufe

### Advertisement beim Anwendungsstart

1. `AddVideoWebPlayerServices` bindet `MdnsOptions` an den Konfigurationsabschnitt `Mdns`,
   registriert `MdnsOptionsValidator` mit `ValidateOnStart()` und — nur wenn
   `!builder.Environment.IsEnvironment("Testing")` — den `MdnsAdvertiserWorker` per
   `AddHostedService<>`. In `Testing` wird UDP 5353 nie gebunden (E2E-/Unit-Tests belegen keine
   festen Ports).
2. `MdnsAdvertiserWorker.ExecuteAsync` wartet auf `IHostApplicationLifetime.ApplicationStarted`,
   damit die gebundenen Kestrel-Adressen verfügbar sind.
3. Der Worker ermittelt den effektiven Schaltzustand (Konjunktion, siehe Designentscheidung):
   `MdnsOptions.Enabled` **und** `Setup.MdnsAdvertisementEnabled` (gelesen über einen DI-Scope mit
   `ProgramSettingsService.GetMdnsAdvertisementEnabledAsync`). Die Konjunktionslogik liegt als
   `internal` prüfbare Methode vor (Konvention: `internal` Member sind für das Testprojekt sichtbar,
   vgl. `KestrelLimits`).
4. Nur bei effektiv `true`: `MdnsServiceProfileBuilder.Build(...)` erzeugt aus `MdnsOptions`,
   `IConfiguration` (`Kestrel:Endpoints:Http:Url`, `Host:Port`) und
   `IServerAddressesFeature.Addresses` einen `MdnsAdvertisement`-Record: `InstanceName`,
   `ServiceType`, `Port`, `HostName`, `TxtRecords` (`path=/`, `app=VideoWebPlayer`).
5. Der Worker erzeugt `MulticastService` + `ServiceDiscovery`, legt das `ServiceProfile` an und ruft
   `Advertise` (und `Announce`) auf; danach Log auf Information-Ebene mit Instanzname, Diensttyp und
   Port. Bei effektiv `false` wird nichts advertised (Log-Hinweis auf Information, dass das
   Advertisement deaktiviert ist).
6. Fehler beim Socket-Bind oder Advertise (z. B. Port 5353 blockiert, kein Multicast-fähiges
   Interface) werden gefangen und als Warnung geloggt — die Anwendung läuft weiter, da das
   Advertisement ein optionales Netzwerk-Feature ist.

Beteiligte Klassen/Komponenten: `MdnsAdvertiserWorker`, `MdnsServiceProfileBuilder`,
`MdnsAdvertisement`, `MdnsOptions`, `MdnsOptionsValidator`, `ProgramSettingsService`,
`ServiceCollectionExtensions`, `IServer`/`IServerAddressesFeature`, `IHostApplicationLifetime`,
`IServiceScopeFactory`, `MulticastService`, `ServiceDiscovery`, `ServiceProfile` (NuGet-Paket).

### Laufzeit-Umschaltung über den Admin-Schalter

1. Der Worker prüft in einer Schleife alle 60 Sekunden (Konstante, nicht konfigurierbar) den
   effektiven Schaltzustand erneut (gleiche Konjunktion wie beim Start; Config-Teil ist statisch,
   DB-Teil wird pro Runde über einen neuen DI-Scope gelesen).
2. Übergang `false` → `true`: Profil erneut aufbauen (`MdnsServiceProfileBuilder`) und advertisen.
3. Übergang `true` → `false`: `Unadvertise` aufrufen (Goodbye-Pakete) und das Profil entfernen.
4. Fehler beim DB-Zugriff oder beim Umschalten werden als Warnung geloggt; der bisherige Zustand
   bleibt bestehen und die Schleife läuft weiter.
5. `Mdns:Enabled=false` bleibt statisch — ein Wechsel des Config-Flags zur Laufzeit ist nicht
   vorgesehen (Configuration-Reload ist hier nicht erforderlich; der Betreiber-Master-Switch greift
   beim nächsten Start).

Beteiligte Klassen/Komponenten: `MdnsAdvertiserWorker`, `ProgramSettingsService`,
`IServiceScopeFactory`, `ServiceDiscovery`, `ServiceProfile`, `MdnsServiceProfileBuilder`.

### Deregistrierung beim Shutdown

1. `MdnsAdvertiserWorker.StopAsync` ruft `Unadvertise`/Dispose des `ServiceDiscovery` auf
   (Goodbye-Pakete mit TTL 0) und stoppt den `MulticastService`.
2. Damit verschwindet der Dienst zeitnah aus den Browses der Clients statt per TTL auszulaufen.

Beteiligte Klassen/Komponenten: `MdnsAdvertiserWorker`, `ServiceDiscovery`, `MulticastService`.

### Port-Ableitung in `MdnsServiceProfileBuilder`

1. `Mdns:Port` gesetzt (1–65535) → dieser Port.
2. Sonst erste `http://`-/`https://`-Adresse aus `IServerAddressesFeature.Addresses` → deren Port.
3. Sonst `Kestrel:Endpoints:Http:Url` parsen → dessen Port.
4. Sonst `Host:Port` → sonst 5000.

Beteiligte Klassen/Komponenten: `MdnsServiceProfileBuilder`, `MdnsOptions`, `IConfiguration`,
`IServerAddressesFeature`.

### Admin-Schalter in den Programmeinstellungen

1. Admin öffnet `/admin/program-settings` (bestehender Guard: `IsAdmin`-Claim, sonst „Nicht
   autorisiert").
2. `OnInitializedAsync` lädt die `Setup`-Zeile über `GetOrCreateSetupAsync` und befüllt das
   Formularmodell inkl. `MdnsAdvertisementEnabled`.
3. Neue `admin-card` „Netzwerk-Erkennung (mDNS)" mit `InputCheckbox` „Server per mDNS im Netzwerk
   ankündigen" und erklärendem `form-text`: wirkt nur zusammen mit der Serverkonfiguration
   `Mdns:Enabled` (Konjunktion); wirksam spätestens nach ca. 60 Sekunden, kein Neustart nötig;
   Diensttyp `_videowebplayer._tcp.local.`, UDP-Port 5353.
4. `SaveAsync` ruft zusätzlich `ProgramSettingsService.UpdateMdnsAdvertisementEnabledAsync` auf;
   Erfolgsmeldung wie bisher („Gespeichert.").
5. Der Worker übernimmt den neuen Zustand beim nächsten 60-s-Intervall (siehe Laufzeit-Umschaltung).

Beteiligte Klassen/Komponenten: `ProgramSettings.razor`, `ProgramSettingsService`, `Setup`,
`MdnsAdvertiserWorker`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `MdnsOptions` (`VideoWebPlayer/Configuration/MdnsOptions.cs`) | Optionsklasse | Gebundene Konfiguration `Mdns`: `Enabled`, `InstanceName`, `ServiceType`, `Port` |
| `MdnsOptionsValidator` (`VideoWebPlayer/Configuration/MdnsOptionsValidator.cs`) | `IValidateOptions<MdnsOptions>` | Startzeit-Validierung der mDNS-Konfiguration (Port-Range, Diensttyp-Format, Instanzname) |
| `MdnsAdvertisement` (`VideoWebPlayer/Services/MdnsAdvertisement.cs`) | Record / Datenmodellklasse | Aufgelöstes Dienstprofil: `InstanceName`, `ServiceType`, `Port`, `HostName`, `TxtRecords` — testbare Ausgabe der Ableitung |
| `MdnsServiceProfileBuilder` (`VideoWebPlayer/Services/MdnsServiceProfileBuilder.cs`) | Logikklasse (rein, ohne Netzwerk) | Leitet `MdnsAdvertisement` aus Options, Konfiguration und gebundenen Serveradressen ab |
| `MdnsAdvertiserWorker` (`VideoWebPlayer/Services/MdnsAdvertiserWorker.cs`) | `BackgroundService` | Ermittelt effektiven Schaltzustand (Konjunktion Config × Setup), advertised/announced beim Start, re-evaluiert periodisch den Admin-Schalter (Advertise/Unadvertise zur Laufzeit), deregistriert beim Shutdown; Fehler → Warnung |

## Änderungen an bestehenden Klassen

### `Setup` (Datenmodellklasse)

- **Neue Eigenschaften:** `MdnsAdvertisementEnabled` (`bool`, Default `true`) — Admin-Schalter für
  das mDNS-Advertisement.

### `ProgramSettingsService` (Logikklasse)

- **Neue Methoden:**
  - `GetMdnsAdvertisementEnabledAsync(CancellationToken)` — liefert
    `Setup.MdnsAdvertisementEnabled` der Singleton-Zeile (über `GetOrCreateSetupAsync`); Rückgabe
    `bool`.
  - `UpdateMdnsAdvertisementEnabledAsync(bool enabled, CancellationToken)` — persistiert den
    Admin-Schalter auf der `Setup`-Zeile.

### `ProgramSettings.razor` (Blazor-Seite `/admin/program-settings`)

- **Neue UI-Elemente:** `admin-card` „Netzwerk-Erkennung (mDNS)" mit `InputCheckbox` und
  erläuterndem `form-text` (Konjunktion mit `Mdns:Enabled`, ~60 s Verzögerung, Diensttyp,
  UDP 5353). Folgt dem bestehenden Karten-Muster der Seite (`Anwendung`, `Scan-Einstellungen`,
  `Weiterschauen`).
- **Geänderte Methoden:** `OnInitializedAsync` befüllt `model.MdnsAdvertisementEnabled` aus der
  `Setup`-Zeile; `SaveAsync` ruft zusätzlich `UpdateMdnsAdvertisementEnabledAsync` auf.
- **Geändertes Formularmodell:** `SettingsModel` erhält `MdnsAdvertisementEnabled` (`bool`, Default
  `true`).

### `VideoWebPlayerBackupData` (Backup-Implementierung)

- **Erweiterte Listen:** `OptionalRestoreColumns` um
  `Setups.MdnsAdvertisementEnabled` (`nameof`-Muster wie die übrigen `Setups.*`-Einträge);
  `OptionalRestoreBoolDefaults` um `(Setups, MdnsAdvertisementEnabled, true)` — Alt-Backups ohne
  die Spalte stellen den Schalter auf `true` wieder her (Feature-Default).

### `ServiceCollectionExtensions` (Erweiterungsmethode `AddVideoWebPlayerServices`)

- **Neue Registrierungen:** `services.AddOptions<MdnsOptions>().Bind(configuration.GetSection("Mdns")).ValidateOnStart()`;
  `services.AddSingleton<IValidateOptions<MdnsOptions>, MdnsOptionsValidator>()`;
  konditional `services.AddHostedService<MdnsAdvertiserWorker>()` nur wenn
  `!builder.Environment.IsEnvironment("Testing")` — der `WebApplicationBuilder`-Parameter stellt
  `builder.Environment` bereits bereit.

### `appsettings.json` (Konfigurationsdatei)

- **Neuer Abschnitt:** `Mdns` mit `Enabled`, `InstanceName`, `ServiceType`, `Port` (siehe
  Konfigurationsänderungen).
- **Neue `JsonKeys`-Einträge** in `AutoUpdate:ProtectedFiles[1].JsonKeys`: alle skalaren
  `Mdns:*`-Schlüssel, damit lokale Anpassungen ein Update überstehen.

### `Program.cs`

- Keine Änderung — im Gegensatz zum `UdpDiscoveryListener` wird der Worker über DI gehostet; der
  `Testing`-Guard sitzt in der Registrierung.

### `UdpDiscoveryListener`

- Keine Änderung — bleibt als Fallback-Discovery-Kanal bestehen.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddSetupMdnsAdvertisementEnabled` | `Setups.MdnsAdvertisementEnabled` (`bool`/`INTEGER`, `NOT NULL`, Default `1`) | Neuer Admin-Schalter für das mDNS-Advertisement auf der `Setup`-Singleton-Tabelle. Migration via `dotnet ef migrations add` erzeugen; **nicht vor dem Merge in den Projekt-Branch** — bei parallelen Migrationen nach dem Rebase neu erzeugen, statt `ApplicationDbContextModelSnapshot.cs`-Konflikte von Hand zu lösen (AGENTS.md §3). |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `MdnsOptions.Port` | `null` oder 1–65535 | `ValidateOnStart` schlägt fehl → Startabbruch mit Klartext-Meldung |
| `MdnsOptions.ServiceType` | Muster `_{label}._tcp` bzw. `_{label}._udp`, optional mit Suffix `.local`/`.local.`; Label: Kleinbuchstaben/Ziffern/Bindestrich, max. 15 Zeichen (DNS-SD-Konvention) | Ungültiger Typ → Startabbruch mit Klartext-Meldung |
| `MdnsOptions.InstanceName` | Bei `Enabled = true` nicht leer, max. 63 Zeichen (DNS-Label-Limit) | Ungültiger Name → Startabbruch mit Klartext-Meldung |
| `Setup.MdnsAdvertisementEnabled` | `bool` — keine Validierung erforderlich | — |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Mdns:Enabled` | `bool` | `true` | Betreiber-Master-Switch (Konjunktion mit Admin-Schalter); abschaltbar via `appsettings.*`/Umgebungsvariable `Mdns__Enabled` |
| `Mdns:InstanceName` | `string` | `VideoWebPlayer` | Dienst-Instanzname im Browse |
| `Mdns:ServiceType` | `string` | `_videowebplayer._tcp.local.` | Angekündigter Diensttyp (dediziert; Wechsel ohne Codeänderung möglich) |
| `Mdns:Port` | `int?` | `null` | Override des announced Ports (z. B. IIS-Site-Port); `null` = Ableitungskette |

Neue `AutoUpdate:ProtectedFiles[1].JsonKeys`-Einträge: `Mdns:Enabled`, `Mdns:InstanceName`,
`Mdns:ServiceType`, `Mdns:Port`.

Übersteuerbar wie üblich über `appsettings.{Env}.json`, `appsettings.Local.json`,
Umgebungsvariablen (`Mdns__*`) und Kommandozeile.

## Seiteneffekte und Risiken

- **Abhängigkeit außerhalb dieses Repos:** Die MAUI-App `VideoPlayer.Maui` sucht heute
  `_http._tcp.local.` und muss auf `_videowebplayer._tcp.local.` umgestellt werden — Abstimmung und
  Umsetzung erfolgen im App-Repository. Bis dahin findet die App den Server nur über den
  UDP-Fallback (Port 5001); das ist der vereinbarte Zielzustand (kein `_http._tcp`-Advertisement).
- **Neues NuGet-Paket:** `Makaretu.Dns.Multicast.New` (MIT) — vor dem Referenzieren Lizenzdatei im
  Paket verifizieren, transitive Abhängigkeiten prüfen, eine Paketversion wählen, die älter als 7
  Tage publiziert ist, und das Verhalten des CI-`security-scan`-Steps
  (`.github/actions/security-scan`) kontrollieren.
- **Parallel laufender Avahi-/mDNS-Daemon:** Auf einem Linux-Host mit `avahi-daemon` kann das
  Binden von UDP 5353 Konflikte erzeugen (üblicherweise durch Socket-Reuse gelöst); besteht eine
  statische Avahi-Dienstdatei nach `INSTALL_AVAHI.md`, entsteht Doppel-Advertisement — Hinweis in
  Doku und Warnlog im Worker. `docs/INSTALL_AVAHI.md` bleibt als Alternativweg bestehen und wird um
  den Hinweis ergänzt, statische Dienstdatei zu entfernen **oder** `Mdns:Enabled=false` zu setzen.
- **Firewall:** UDP 5353 (eingehend/ausgehend Multicast 224.0.0.251) muss offen sein — zu
  dokumentieren.
- **IIS `OutOfProcess`:** Das Advertisement läuft im Backend-Kestrel-Prozess; die gebundene Adresse
  ist der interne Port, nicht der IIS-Site-Port → `Mdns:Port` muss dort gesetzt werden (Doku).
- **Multicast-freie Umgebungen** (Container, VLANs): mDNS schlägt still fehl; der Worker loggt,
  die App bleibt lauffähig.
- **EF-Migration / Model-Snapshot:** `ApplicationDbContextModelSnapshot.cs` kollidiert mit
  parallelen Schritten, die ebenfalls migrieren — Migration erst nach dem Rebase erzeugen
  (AGENTS.md §3).
- **Backup/Restore:** Die neue `Setups`-Spalte ist in `OptionalRestoreColumns` samt
  `OptionalRestoreBoolDefaults` (`true`) eingetragen; Regressionstest stellt ein Alt-Backup ohne
  die Spalte wieder her.
- **Tests:** `AutoUpdateProtectedFilesTests.ShippedAppsettingsProtectedFiles_AreValid` validiert
  die ausgelieferte `appsettings.json` — neue `JsonKeys` müssen den Validator-Regeln genügen.
  `ApiDocumentationContractTests` beachten, falls `docs/API.md` einen Discovery-Abschnitt erhält.
- **`Testing`-Umgebung:** Ohne konditionale Registrierung würde jeder E2E-/WebApplicationFactory-Test
  UDP 5353 binden — der Guard in `AddVideoWebPlayerServices` verhindert das. Der Admin-Schalter
  selbst ist über die UI in `Testing` erreichbar (E2E-testbar); nur das eigentliche Advertisement
  ist abgeschaltet.

## Umsetzungsreihenfolge

1. **mDNS-NuGet-Paket prüfen und referenzieren**
   - Voraussetzungen: Keine.
   - Beschreibung: `Makaretu.Dns.Multicast.New` prüfen (Lizenzdatei MIT, transitive Abhängigkeiten,
     Wartungsstand; Paketversion wählen, die älter als 7 Tage publiziert ist), `PackageReference` in
     `VideoWebPlayer.csproj` eintragen, `dotnet restore` + `dotnet build VideoPlayer.sln` in Debug
     und Release prüfen; Verhalten des CI-`security-scan`-Steps kontrollieren. Ergebnis im Handover
     dokumentieren.

2. **`MdnsOptions` und `MdnsOptionsValidator` anlegen**
   - Voraussetzungen: Keine (unabhängig vom Paket).
   - Beschreibung: Optionsklasse mit Defaults in `VideoWebPlayer/Configuration/`; Validator nach
     dem Muster `EpisodeBackgroundImageOptionsValidator` (Port-Range, Diensttyp-Muster inkl.
     optionalem `.local.`-Suffix, Instanzname-Regeln).

3. **`Setup.MdnsAdvertisementEnabled` und EF-Migration**
   - Voraussetzungen: Keine.
   - Beschreibung: `bool`-Eigenschaft mit Default `true` auf `Setup`; Migration
     `AddSetupMdnsAdvertisementEnabled` via `dotnet ef migrations add` erzeugen; Build prüfen.
     (Bei parallelen Migrationen anderer Schritte: nach dem Rebase neu erzeugen.)

4. **`ProgramSettingsService` erweitern**
   - Voraussetzungen: Schritt 3 (`Setup`-Spalte).
   - Beschreibung: `GetMdnsAdvertisementEnabledAsync` /
     `UpdateMdnsAdvertisementEnabledAsync` nach dem Muster der bestehenden Getter/Setter.

5. **`VideoWebPlayerBackupData` erweitern**
   - Voraussetzungen: Schritt 3 (`Setup`-Spalte).
   - Beschreibung: `OptionalRestoreColumns` um `Setups.MdnsAdvertisementEnabled` und
     `OptionalRestoreBoolDefaults` um den Eintrag mit Default `true` ergänzen.

6. **`MdnsAdvertisement` und `MdnsServiceProfileBuilder` anlegen**
   - Voraussetzungen: Schritt 2 (Optionsklasse).
   - Beschreibung: Record für das aufgelöste Profil; Builder mit der Port-Ableitungskette
     (`Mdns:Port` → `IServerAddressesFeature` → `Kestrel:Endpoints:Http:Url` → `Host:Port` → 5000),
     Diensttyp aus `Mdns:ServiceType`, festen TXT-Records (`path=/`, `app=VideoWebPlayer`), Hostname
     via `Dns.GetHostName()`.

7. **`MdnsAdvertiserWorker` implementieren und registrieren**
   - Voraussetzungen: Schritte 1, 2, 4, 6 (Paket, Options, Settings-Service, Builder).
   - Beschreibung: `BackgroundService` mit `IOptions<MdnsOptions>`, `IConfiguration`,
     `IServer`/`IServerAddressesFeature`, `IHostApplicationLifetime`, `IServiceScopeFactory`,
     `ILogger`; `ApplicationStarted` abwarten, effektiven Schaltzustand (Konjunktion) prüfen,
     advertisen/announcen, 60-s-Re-Evaluation des Admin-Schalters mit dynamischem
     Advertise/Unadvertise, `StopAsync` deregistriert; Fehlerbehandlung als Warnung.
     Registrierung in `AddVideoWebPlayerServices` inkl. Optionsbindung, Validator und
     `Testing`-Guard.

8. **`appsettings.json` erweitern**
   - Voraussetzungen: Schritt 2 (Schlüsselnamen stehen fest).
   - Beschreibung: `Mdns`-Abschnitt mit Defaults eintragen; alle `Mdns:*`-Schlüssel in
     `AutoUpdate:ProtectedFiles[1].JsonKeys` aufnehmen.

9. **Setup-UI erweitern (`ProgramSettings.razor`)**
   - Voraussetzungen: Schritt 4 (Settings-Service-Methoden).
   - Beschreibung: Neue `admin-card` „Netzwerk-Erkennung (mDNS)" mit `InputCheckbox` und
     `form-text`-Erklärung (Konjunktion mit `Mdns:Enabled`, ~60 s Verzögerung, Diensttyp, UDP 5353);
     `SettingsModel.MdnsAdvertisementEnabled`, Laden in `OnInitializedAsync`, Speichern in
     `SaveAsync` über `UpdateMdnsAdvertisementEnabledAsync`. Vorhandener `IsAdmin`-Guard bleibt
     unverändert (serverseitige Durchsetzung wie auf den übrigen Admin-Seiten).

10. **Unit-Tests schreiben**
    - Voraussetzungen: Schritte 2–9.
    - Beschreibung: `MdnsServiceProfileBuilderTests`, `MdnsOptionsValidatorTests`,
      Bindungstest `Mdns` → `MdnsOptions`, Registrierungs-Guard-Test (`Testing` → Worker nicht
      registriert), `ProgramSettingsService`-Tests für den neuen Schalter (SQLite),
      Konjunktionslogik-Test, Backup-Regressionstest; Bestandstests `AutoUpdateProtectedFilesTests`
      erneut laufen lassen.

11. **E2E-Tests für den Setup-Schalter schreiben**
    - Voraussetzungen: Schritt 9 (UI).
    - Beschreibung: `ProgramSettingsE2ETests` nach dem Muster `UpdatesPageE2ETests`
      (`WebApplicationFactory<Program>` + Playwright + Admin-Benutzer): Toggle speichern und über
      Reload persistiert; Nicht-Admin erhält „Nicht autorisiert".

12. **Dokumentation aktualisieren**
    - Voraussetzungen: Schritte 7–9 (Verhalten und Schlüssel final).
    - Beschreibung: `docs/INSTALL_AVAHI.md` (In-App-Advertisement als Standardweg, Avahi als
      Alternative, Doppel-Advertisement-Hinweis: statische Dienstdatei entfernen oder
      `Mdns:Enabled=false`), `docs/GUIDE_Installation.md` (Discovery-Adresse / `Mdns:*`,
      IIS-Hinweis `Mdns:Port`, Firewall UDP 5353), `docs/help/einrichtung.md` (Discovery-Abschnitt:
      mDNS `_videowebplayer._tcp.local.` + Admin-Schalter + UDP-Fallback), `docs/API.md`
      (Discovery-Abschnitt, falls Contract-Tests es zulassen), `README.md`, `docs/INDEX.md`,
      `docs/RELEASE_NOTES.md`. Hinweis auf die clientseitige Umstellung der MAUI-App auf
      `_videowebplayer._tcp.local.` (erfolgt im App-Repo) aufnehmen.

13. **Manuelle Verifikation und Gesamtlauf**
    - Voraussetzungen: Schritte 1–12.
    - Beschreibung: `dotnet build VideoPlayer.sln` in Debug und Release; volle Suite
      `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`; manueller Nachweis per
      `avahi-browse -a` bzw. `dns-sd -B _videowebplayer._tcp local.` (Instanzname, Port,
      TXT-Records; Admin-Schalter aus → Dienst verschwindet nach ≤ ~60 s; im Bericht dokumentieren
      — nicht automatisierbar).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Build_UsesDefaults_WhenNothingConfigured` | `MdnsServiceProfileBuilderTests` | Instanzname `VideoWebPlayer`, Diensttyp `_videowebplayer._tcp.local.`, Port-Fallback 5000, TXT `path=/` + `app=VideoWebPlayer` |
| `Build_PrefersMdnsPort_OverAllSources` | `MdnsServiceProfileBuilderTests` | `Mdns:Port` schlägt gebundene Adressen, Kestrel-URL und `Host:Port` |
| `Build_UsesBoundServerAddressPort` / `Build_UsesKestrelEndpointUrlPort` / `Build_UsesHostPortFallback` | `MdnsServiceProfileBuilderTests` | Ableitungskette der Port-Quellen in der richtigen Reihenfolge (u. a. `http://*:5002` aus `appsettings.Production.json` simulieren) |
| `Build_UsesConfiguredServiceType` / `Build_UsesConfiguredInstanceName` | `MdnsServiceProfileBuilderTests` | Konfigurierter Diensttyp bzw. Instanzname wird übernommen |
| `Validate_RejectsInvalidPort` / `Validate_RejectsInvalidServiceType` / `Validate_RejectsEmptyInstanceName` / `Validate_AcceptsDefaults` | `MdnsOptionsValidatorTests` | Validierungsregeln aus der Tabelle oben (Startabbruch-Fälle und Happy Path; `.local.`-Suffix erlaubt) |
| `MdnsSection_BindsToOptions` | `MdnsConfigurationTests` | `IConfiguration` (`AddInMemoryCollection`) bindet `Mdns` korrekt an `MdnsOptions` (Muster: `AutoUpdateProtectedFilesTests.ConfigurationBindsProtectedFiles`) |
| `AddVideoWebPlayerServices_DoesNotRegisterWorker_InTesting` / `..._RegistersWorker_OutsideTesting` | `MdnsRegistrationTests` | `WebApplication.CreateBuilder` mit `EnvironmentName` `Testing` vs. andere Umgebung → `IHostedService`-Auflösung enthält `MdnsAdvertiserWorker` nicht bzw. doch |
| `IsAdvertisementEnabled_RequiresBothSwitches` | `MdnsAdvertiserWorkerTests` | Konjunktionstabelle: `Mdns:Enabled` × `Setup.MdnsAdvertisementEnabled` → nur beide `true` ergibt aktiv (`internal` prüfbare Methode, Muster `KestrelLimits`) |
| `GetMdnsAdvertisementEnabledAsync_DefaultsToTrue` / `UpdateMdnsAdvertisementEnabledAsync_Persists` | `ProgramSettingsServiceTests` | Getter liefert `true` für neue/Standard-`Setup`-Zeile; Setter persistiert beide Richtungen — gegen echtes SQLite (`PairingTestDb`-Muster), nicht EF InMemory |
| `ReadFromAsync_LegacyBackupWithoutMdnsAdvertisementEnabledColumn_RestoresAsEnabled` | `VideoWebPlayerBackupDataTests` | Alt-Backup ohne die Spalte (gebaut per `LegacyBackupArchiveBuilder`, Muster `ReadFromAsync_LegacyBackupWithoutPlaylistBackfillLastSweepAtColumn_RestoresWithNull`) wird wiederhergestellt; Spalte landet auf `true` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AutoUpdateProtectedFilesTests.ShippedAppsettingsProtectedFiles_AreValid` | Neue `Mdns:*`-`JsonKeys` in der ausgelieferten `appsettings.json` — Test muss weiterhin grün sein; bei Verstoß gegen Validator-Regeln anpassen |
| `ApiDocumentationContractTests` / `ApiDocumentationContractTests_Runtime` | Nur falls `docs/API.md` um einen Discovery-Abschnitt erweitert wird — Dokumentation so formulieren, dass die Contract-Prüfung nicht bricht |

### E2E-Tests (primärer Funktionsnachweis)

Der Admin-Schalter führt einen neuen Benutzerfluss auf der Admin-Seite `/admin/program-settings`
ein — dafür ist E2E-Abdeckung Pflicht (Muster: `UpdatesPageE2ETests` /
`BackupUploadE2ETests`: `WebApplicationFactory<Program>` mit `EnvironmentName = "Testing"`,
`UseUrls("http://127.0.0.1:0")`, Playwright, Admin-Benutzer per `UserManager` angelegt).

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin deaktiviert den mDNS-Schalter, speichert, lädt die Seite neu → Schalter bleibt aus (und umgekehrt aktiviert) | `ProgramSettingsE2ETests` | Admin-Schalter ist in der UI schaltbar und persistent (Speicherung über `ProgramSettingsService` in `Setup`) | Persistenter UI-Fluss; Unit-Tests decken Formularbindung und echten Speichervorgang über die Blazor-Server-Seite nicht ab |
| Pflicht | Nicht-Admin-Benutzer öffnet `/admin/program-settings` → „Nicht autorisiert", kein Zugriff auf den Schalter | `ProgramSettingsE2ETests` | Serverseitige Berechtigungsdurchsetzung (`IsAdmin`-Claim) greift auch für den neuen Schalter | Sichtbarkeits-/Berechtigungsregel — nur über den realen UI-Pfad verlässlich nachweisbar |

Anzupassende bestehende E2E-Tests: Keine.

Das mDNS-Advertisement selbst bleibt ohne E2E-Test — Begründung: Die `Testing`-Umgebung
registriert den Worker bewusst nicht (kein fester Port 5353 in `WebApplicationFactory`-Tests), und
echtes Multicast-Verhalten lässt sich in der Testumgebung nicht verlässlich per Browser-Test
nachweisen. Der Funktionsnachweis erfolgt manuell per `avahi-browse`/`dns-sd` (Schritt 13, im
Abnahmebericht zu dokumentieren); die Ableitungslogik ist durch Unit-Tests abgesichert.

## Offene Punkte

Keine. Alle bisherigen offenen Punkte wurden durch den Anwender entschieden und sind oben
eingearbeitet: Diensttyp ausschließlich `_videowebplayer._tcp.local.` (konfigurierbar via
`Mdns:ServiceType`; Umstellung der MAUI-App erfolgt außerhalb dieses Repos), Paket
`Makaretu.Dns.Multicast.New`, `Mdns:Enabled` Default `true`, `INSTALL_AVAHI.md` bleibt als
Alternativweg mit Doppel-Advertisement-Hinweis, Port-Ableitung aus `IServerAddressesFeature` mit
`Mdns:Port`-Override, Admin-Schaltbarkeit über `Setup`/`ProgramSettingsService` mit
Konjunktions-Priorisierungsregel (beide Schalter müssen `true` sein; `Mdns:Enabled` ist der
Betreiber-Master-Switch und wird durch den Admin-Schalter nicht überstimmt — und umgekehrt).
