# Umsetzungsplan: UDP-Discovery-Antwort soll die erreichbare Basis-URL melden (Issue #224)

## Übersicht

Die Antwort des `UdpDiscoveryListener` auf `VIDEOWEBPLAYER_DISCOVERY`-Broadcasts (UDP 5001)
soll künftig die aus Clientsicht erreichbare öffentliche Basis-URL enthalten — **vollständig
inklusive Pfadanteil** (z. B. `https://host/videoplayer/`), statt der heute fest gebauten
`http://{Host:Address ?? "localhost"}:{Host:Port ?? "5000"}`. Umgesetzt wird eine dreistufige
Auflösung pro Anfrage: ein admin-seitig gepflegter Wert (`Setup.DiscoveryPublicBaseUrl`,
neue nullable Spalte mit EF-Migration, UI in `/admin/program-settings`) hat oberste Priorität;
danach gilt die Datei-/Umgebungskonfiguration `Discovery:PublicBaseUrl` (neue Options-Klasse
`DiscoveryOptions` mit Validator); erst wenn beide leer sind, wird die URL zur Laufzeit aus
`Host:Address`, den tatsächlich gebundenen Serveradressen, der Kestrel-Konfiguration,
`Host:Port` und der primären LAN-Adresse des Hosts abgeleitet — Loopback/Wildcard-Adressen
werden dabei nie gemeldet. Betroffen sind `VideoWebPlayer/Services/` (neuer Builder, geänderter
Listener, `ProgramSettingsService`), `VideoWebPlayer/Configuration/`, `VideoWebPlayer/Data/Setup.cs`,
`VideoWebPlayer/Migrations/`, `VideoWebPlayerBackupData` (Backup-Kompatibilität),
`Components/Pages/Admin/ProgramSettings.razor`, `Program.cs`, `appsettings.json` und die
Dokumentation. Das Feature ist durch die Admin-Oberfläche UI-relevant: E2E-Tests für den
konkreten Benutzerfluss sind Pflicht.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Client-Vertrag | Die `VIDEOWEBPLAYER_SERVER`-Antwort meldet die **vollständige Basis-URL inklusive Pfadanteil** (z. B. `https://host/videoplayer/`) | Anwender-Entscheidung (geklärt): Nur so ist die IIS-Unterpfad-Anforderung lösbar. Die Verifikation/Umstellung der MAUI-App `VideoPlayer.Maui` ist eine Folgeaufgabe im externen App-Repository und nicht Teil dieses Plans. |
| Vorrangregel | `Setup.DiscoveryPublicBaseUrl` (Admin-Wert) → `Discovery:PublicBaseUrl` (Datei-/Env-Konfiguration) → automatische Ableitung | Anwender-Entscheidung: Der Administrator soll den Betreiber-Default übersteuern können. `Discovery:PublicBaseUrl` bleibt parallel als Datei-/Env-Konfiguration bestehen (Betreiber-Default). Beide leer → Ableitung. |
| Admin-Einstellbarkeit | Neue nullable Spalte `Setup.DiscoveryPublicBaseUrl` (`string?`), gepflegt über `ProgramSettingsService` und die Admin-Seite `/admin/program-settings` | Anwender-Entscheidung gegen die ursprüngliche Empfehlung (reine Betreiberkonfiguration). Direktes Vorbild `Setup.MdnsAdvertisementEnabled` (Issue #223): EF-Migration, `OptionalRestoreColumns`-Eintrag samt Alt-Backup-Regressionstest, atomares Speichern über `UpdateGeneralSettingsAsync`. |
| Konfigurationsschlüssel | Neuer Schlüssel `Discovery:PublicBaseUrl` (eigene `Discovery`-Sektion), **keine** Wiederverwendung von `App:BaseUrl` | `App:BaseUrl` ist die serverinterne Basis-URL für den scoped `HttpClient` (`ServiceCollectionExtensions.cs` Zeile 206) und darf legitim eine interne/Loopback-Adresse enthalten. Die Discovery-Antwort hat die umgekehrte Anforderung: aus Clientsicht erreichbar, nie Loopback. Getrennte Schlüssel verhindern, dass ein für den HttpClient gedachter Wert unbrauchbare LAN-Antworten erzeugt. `App:BaseUrl` bleibt unverändert. |
| Ableitungslogik | Neue `internal static`-Klasse `DiscoveryResponseBuilder` mit reiner, netzwerkfreier `Build`-Methode (PoEAA: kein spezifisches Muster nötig — reine Ableitungsfunktion nach Projekt-`*Builder`-Konvention) | Direktes Vorbild `MdnsServiceProfileBuilder`: netzwerkfreie Testbarkeit von Vorrangregel, Loopback-Vermeidung und Port-/Schema-Ableitung ohne Sockets oder DNS. Alle externen Eingaben (Admin-Wert, gebundene Adressen, Host-Adressliste) werden als Parameter übergeben. |
| Übergabe an den Listener | `UdpDiscoveryListener` erhält ein Resolver-Delegate `Func<CancellationToken, Task<string>>` statt eines fertigen `string`; die URL wird **pro Anfrage** aufgelöst | Der Listener startet vor `app.Run()`, gebundene Adressen (`IServerAddressesFeature.Addresses`) stehen erst nach `ApplicationStarted` zur Verfügung. Per-Anfrage-Auflösung umgeht die Startreihenfolge vollständig und stellt sicher, dass der jeweils **aktuelle Admin-Wert** gilt (keine einmalige Auflösung beim Start — Admin-Änderungen wirken sofort beim nächsten Broadcast, ohne Neustart und ohne 60-s-Intervall wie beim mDNS-Worker). Discovery-Broadcasts sind selten — der Auflösungsaufwand (Scope + DB-Lesung + `Dns`-Aufruf) pro Anfrage ist vernachlässigbar. Umbau zu `BackgroundService` wäre ein größerer Eingriff ohne Mehrwert. |
| Lesen des Admin-Werts zur Laufzeit | Pro Discovery-Anfrage in eigenem DI-Scope über `ProgramSettingsService.GetDiscoveryPublicBaseUrlAsync`; Fehler → fail-open (`null`, weiter mit Dateikonfiguration/Ableitung) | `ProgramSettingsService` ist scoped (`ServiceCollectionExtensions.cs` Zeile 251); der Scope-Aufbau pro Lesung folgt dem Muster `MdnsAdvertiserWorker.RefreshAdminSwitchAsync` (`IServiceScopeFactory`, Zeilen 154–172). Es gibt kein Caching in `ProgramSettingsService` — jede Lesung holt die `Setup`-Zeile frisch aus der DB, der aktuelle Admin-Wert gilt damit sofort. Fail-open statt hartem Fehler: Ein transienter DB-Ausfall soll die Discovery-Präsenz nicht kippen (die Empfangsschleife verschluckt Fehler ohnehin still — eine bewusst geloggte/fehlertolerante Fortsetzung mit dem nächsten Auflösungsschritt ist korrekter als gar keine Antwort). |
| Host-Ableitung ohne konfigurierten/admin-gepflegten Wert | Primäre LAN-IPv4 des Hosts aus `Dns.GetHostEntry(Dns.GetHostName())`-Adressliste (als Parameter injiziert); letzte Stufe `Dns.GetHostName()` | Die lokale Empfangsadresse eines Broadcasts ist über `UdpClient.ReceiveAsync` nicht bestimmbar; Socket-Ebene mit `IP_PKTINFO` wäre deutlich komplexer und löst das Problem auch nur bei direktem Empfang der Anfrage über genau eine NIC. Die primäre LAN-Adresse deckt den Normalfall ab; Mehr-NIC-/VLAN-Sonderfälle adressieren Admin-Wert bzw. `Discovery:PublicBaseUrl`. Konsistent zum `Dns.GetHostName()`-Muster in `MdnsServiceProfileBuilder`. |
| Konfigurationsmodell | `DiscoveryOptions` + `DiscoveryOptionsValidator` (`IValidateOptions<>`), Registrierung mit `Bind` + `ValidateOnStart()` in `ServiceCollectionExtensions` | Exaktes Muster `MdnsOptions`/`MdnsOptionsValidator` (Zeilen 280–283): typsichere Bindung, Fail-fast bei ungültiger URL beim Start statt stiller Fehlantwort. |
| Gemeinsame URL-Regel | Neue `internal static`-Klasse `DiscoveryUrlRules` (`IsValidPublicBaseUrl`, `NormalizePublicBaseUrl`), genutzt von `DiscoveryOptionsValidator`, `ProgramSettingsService` und dem Formular-Attribut `AbsoluteHttpUrlAttribute` | Dieselbe Regel („leer zulässig, sonst absolute http(s)-URI") gilt an drei Stellen: Dateikonfiguration beim Start, Admin-Eingabe im Formular, serverseitige Absicherung beim Speichern. Eine gemeinsame Regel verhindert divergierende Validierung. |
| Serverseitige Validierung der Admin-Eingabe | `UpdateGeneralSettingsAsync` normalisiert (`Trim`, leer → `null`) und verwirft ungültige Werte mit `ArgumentException`, bevor geschrieben wird | Das Formular-Attribut allein ist keine Absicherung — der Service ist die serverseitige Durchsetzungsstelle (analog zur AGENTS.md-Regel, dass Schutz nicht nur in der UI sitzt). `ArgumentException` statt stiller Korrektur, damit Fehlbedienung sichtbar bleibt; `SaveAsync` fängt sie ab und zeigt die Meldung an. |
| `Host:Address` / `Host:Port` | Bleiben als Ableitungsbausteine bestehen (Rückwärtskompatibilität) | Bestandsinstallationen mit gesetztem `Host:Address` (LAN-IP/Hostname) melden weiterhin diesen Host. Neu: Loopback-/Wildcard-Werte werden ignoriert statt unbrauchbar gemeldet. `Host:Port` bleibt in der Port-Kette (wird auch von `MdnsServiceProfileBuilder` gelesen). |
| IPv6 / APIPA | Ableitung bevorzugt IPv4 (`AddressFamily.InterNetwork`, nicht Loopback); IPv6-Adressen werden für den Host-Teil nicht gewählt | `scheme://[v6]:port`-Notation und Link-Local-Handling sind für LAN-Clients fehleranfällig; der übliche Server im Heimnetz hat eine IPv4-Adresse. Reine IPv6-Netze sind ein Randfall, den Admin-Wert/`Discovery:PublicBaseUrl` abdecken. APIPA-Adressen (169.254.x.x) werden nicht bevorzugt, aber als letzte IP-Stufe vor dem Hostnamen akzeptiert (auf dem Link erreichbar). |

## Programmabläufe

### Start: Options-Validierung und Listener-Start

1. `ServiceCollectionExtensions.AddVideoWebPlayerServices` registriert
   `IValidateOptions<DiscoveryOptions>` (`DiscoveryOptionsValidator`) und bindet die
   `Discovery`-Sektion mit `ValidateOnStart()`.
2. Beim Host-Start validiert der Startup-Validator `Discovery:PublicBaseUrl`: gesetzt →
   muss eine absolute `http`-/`https`-URI sein; ungültig → Start schlägt mit klarer
   Fehlermeldung fehl (Fail-fast, Muster `MdnsOptionsValidator`).
3. `Program.cs` (hinter der unveränderten `!IsEnvironment("Testing")`-Abschirmung)
   instanziiert `UdpDiscoveryListener` mit Port `5001` und einem Resolver-Delegate,
   das über `app.Services` auf `IOptions<DiscoveryOptions>`, `IServer` und die
   `IServiceScopeFactory` zugreift.

Beteiligte Klassen/Komponenten: `DiscoveryOptions`, `DiscoveryOptionsValidator`,
`DiscoveryUrlRules`, `ServiceCollectionExtensions`, `Program`, `UdpDiscoveryListener`.

### UDP-Discovery-Anfrage beantworten

1. `UdpDiscoveryListener.ListenAsync` empfängt per `UdpClient.ReceiveAsync` ein Datagramm.
2. Entspricht der Text `VIDEOWEBPLAYER_DISCOVERY`, ruft der Listener das Resolver-Delegate
   mit dem laufenden `CancellationToken` auf.
3. Das Delegate (in `Program.cs` verdrahtet):
   a. öffnet einen DI-Scope (`IServiceScopeFactory`/`CreateAsyncScope`) und liest den
      Admin-Wert über `ProgramSettingsService.GetDiscoveryPublicBaseUrlAsync` — der
      Aufruf ist fehlerabgeschirmt (`try/catch` → `null`, fail-open);
   b. liest `IOptions<DiscoveryOptions>.Value` und
      `IServer.Features.Get<IServerAddressesFeature>()?.Addresses`;
   c. ruft — in einem `try/catch` abgeschirmt — `Dns.GetHostEntry(Dns.GetHostName()).AddressList`
      (Fehler → `null`-Adressliste);
   d. wertet `DiscoveryResponseBuilder.Build` aus.
4. Der Listener sendet `VIDEOWEBPLAYER_SERVER:{url}` an `result.RemoteEndPoint`.
5. Fehler im Delegate oder beim Versand werden wie bisher vom `catch { }` der
   Empfangsschleife verschluckt — die Schleife läuft weiter.

Beteiligte Klassen/Komponenten: `UdpDiscoveryListener`, `DiscoveryResponseBuilder`,
`DiscoveryOptions`, `ProgramSettingsService`, `IServiceScopeFactory`,
`IServer`/`IServerAddressesFeature`, `System.Net.Dns`.

### Ableitung der Antwort-URL in `DiscoveryResponseBuilder.Build`

Eingaben: `DiscoveryOptions`, `IConfiguration`, `IEnumerable<string>? boundAddresses`
(gebundene Serveradressen), `IReadOnlyList<IPAddress>? hostAddresses` (DNS-Adressliste
des Hosts), `string? adminPublicBaseUrl` (aktueller Admin-Wert aus `Setups`).

1. **Admin-Wert `adminPublicBaseUrl` gesetzt** (nach `NormalizePublicBaseUrl` nicht leer)
   und gültig (`DiscoveryUrlRules.IsValidPublicBaseUrl`) → wird unverändert zurückgegeben
   (vollständige URL inkl. Schema, Host, Port und Pfad). Ein defensiv als ungültig
   erkannter Admin-Wert (theoretisch nur über manipuliertes Backup erreichbar — das
   Speichern validiert bereits) fällt auf die nächste Stufe durch.
2. **`Discovery:PublicBaseUrl` gesetzt** → wird unverändert zurückgegeben (vollständige
   URL inkl. Schema, Host, Port und Pfad, z. B. `https://example.com/videoplayer/`).
   Startvalidierung durch `DiscoveryOptionsValidator` ist bereits erfolgt.
3. **Ableitung** (Ergebnis immer `scheme://host:port`, Pfad `/`):
   - **Schema + Port** aus der ersten Quelle mit gültiger `http`/`https`-URI, in dieser
     Reihenfolge: `boundAddresses` → `Kestrel:Endpoints:Http:Url` →
     `Kestrel:Endpoints:Https:Url` → `Host:Port` (Schema `http`) → `5000` (Schema `http`).
     Port-Extraktion wie `MdnsServiceProfileBuilder.TryGetPort` (Wildcard-Hosts `*`/`+`
     für die URI-Prüfung zu `localhost` normalisiert). Bindet der Server ausschließlich
     `https`, liefert die Kette Schema `https`.
   - **Host** in dieser Reihenfolge:
     a. `Host:Address`, sofern gesetzt und **nicht** Loopback (`localhost`, `127.*`,
        `::1`) oder Wildcard/Any (`*`, `+`, `0.0.0.0`, `[::]`);
     b. erster Host aus `boundAddresses` bzw. den Kestrel-URLs, der ein literal,
        nicht-loopback, nicht-wildcard Ziel ist (z. B. `http://192.168.1.5:5000`);
     c. erste `IPAddress` aus `hostAddresses` mit `AddressFamily.InterNetwork` und nicht
        Loopback (APIPA 169.254.x.x nur, wenn keine andere IPv4 existiert);
     d. `Dns.GetHostName()` (letzte Stufe; leer → defensiver Fallback `localhost`,
        praktisch unerreichbar, da ein Host ohne jede LAN-Identität auch keine
        LAN-Clients hat).
   - **Loopback-Verbot:** `localhost`/`127.0.0.1`/`::1`/`0.0.0.0`/Wildcards werden in
     keiner Stufe gemeldet — ein loopback-artiges `Host:Address` wird ignoriert
     (Verhaltensänderung gegenüber heute, gewollt).
4. Rückgabe: die URL als `string`; der Listener setzt das Protokollpräfix
   `VIDEOWEBPLAYER_SERVER:` unverändert davor.

Beteiligte Klassen/Komponenten: `DiscoveryResponseBuilder`, `DiscoveryOptions`,
`DiscoveryUrlRules`, `IConfiguration`.

### Administrator pflegt die öffentliche Basis-URL

1. Admin öffnet `/admin/program-settings` (`ProgramSettings.razor`,
   `@rendermode InteractiveServer`, `IsAdmin`-Claim-Prüfung wie bisher).
2. `OnInitializedAsync` lädt `setup.DiscoveryPublicBaseUrl` in das Formular-Model
   (`SettingsModel.DiscoveryPublicBaseUrl`, `string?`).
3. Neue `admin-card` „Öffentliche Basis-URL" zeigt ein `InputText`-Feld
   (id `discoveryPublicBaseUrl`, Label „Öffentliche Basis-URL") mit
   `form-text`-Erläuterung (Vorrang vor `Discovery:PublicBaseUrl`, leer = automatische
   Ableitung, Beispiel `https://videos.example.com/videoplayer/`).
4. `EditForm` + `DataAnnotationsValidator` prüfen das Feld über das neue
   `AbsoluteHttpUrlAttribute` — `OnValidSubmit` (`SaveAsync`) feuert nur bei gültigem
   Wert; die Meldung erscheint in `ValidationSummary`/per `ValidationMessage` am Feld.
5. `SaveAsync` ruft `SettingsService.UpdateGeneralSettingsAsync(..., model.DiscoveryPublicBaseUrl)`
   — atomar mit den übrigen Einstellungen in einem `SaveChangesAsync`. Der Service
   normalisiert (`Trim`, leer → `null`) und wirft `ArgumentException` bei ungültiger
   URL; `SaveAsync` fängt sie und zeigt die Meldung in einer `alert-danger`-Box.
6. Nach „Gespeichert." gilt der Wert sofort für die nächste Discovery-Anfrage
   (pro-Anfrage-Auflösung, kein Neustart). Die bisherige `Testing`-Abschirmung des
   Listeners bleibt unberührt — die Admin-Einstellung ist unabhängig davon persistiert.

Beteiligte Klassen/Komponenten: `ProgramSettings.razor`, `AbsoluteHttpUrlAttribute`,
`ProgramSettingsService`, `Setup`, `DiscoveryUrlRules`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `VideoWebPlayer/Configuration/DiscoveryUrlRules.cs` → `DiscoveryUrlRules` | `internal static` Klasse | Gemeinsame Regel für die öffentliche Basis-URL: `IsValidPublicBaseUrl(string?)` (`null`/leer zulässig; sonst `Uri.TryCreate(..., UriKind.Absolute)` mit Schema `http`/`https`) und `NormalizePublicBaseUrl(string?)` (`Trim`, leer → `null`). Genutzt von `DiscoveryOptionsValidator`, `ProgramSettingsService`, `AbsoluteHttpUrlAttribute` und `DiscoveryResponseBuilder`. |
| `VideoWebPlayer/Configuration/DiscoveryOptions.cs` → `DiscoveryOptions` | Options-Klasse (`public sealed`) | Typsichere Bindung der `Discovery`-Sektion; einzige Eigenschaft `PublicBaseUrl` (`string?`, Standard `null` = Ableitung). Muster `MdnsOptions`. |
| `VideoWebPlayer/Configuration/DiscoveryOptionsValidator.cs` → `DiscoveryOptionsValidator` | `IValidateOptions<DiscoveryOptions>` (`public sealed`) | Validiert `PublicBaseUrl` per `DiscoveryUrlRules.IsValidPublicBaseUrl`. Muster `MdnsOptionsValidator`. |
| `VideoWebPlayer/Services/DiscoveryResponseBuilder.cs` → `DiscoveryResponseBuilder` | `internal static` Klasse | Reine, netzwerkfreie Ableitung der Antwort-URL aus Admin-Wert, Optionen, Konfiguration, gebundenen Adressen und Host-Adressliste. Muster `MdnsServiceProfileBuilder`. |
| `VideoWebPlayer/Components/Shared/AbsoluteHttpUrlAttribute.cs` → `AbsoluteHttpUrlAttribute` | `ValidationAttribute` (`public sealed`) | DataAnnotations-Attribut für das Admin-Formular: leer/`null` zulässig, sonst absolute `http`/`https`-URI (delegiert an `DiscoveryUrlRules`). Namespace `VideoWebPlayer.Components.Shared` ist bereits über `_Imports.razor` eingebunden. |

## Änderungen an bestehenden Klassen

### `Setup` (Klasse, `VideoWebPlayer/Data/Setup.cs`)

- **Neue Eigenschaft:** `public string? DiscoveryPublicBaseUrl { get; set; }` — nullable,
  `null` = kein Admin-Override (Vorrangkette greift auf `Discovery:PublicBaseUrl`/Ableitung
  zurück). XML-Doc mit Verweis auf die Vorrangregel und das Discovery-Verhalten.
  Keine Fluent-API-Konfiguration nötig (`Setup` hat keine `IEntityTypeConfiguration`,
  `string?` → nullable `TEXT`-Spalte per Konvention).

### `ProgramSettingsService` (Klasse, `VideoWebPlayer/Services/ProgramSettingsService.cs`)

- **Neue Methode:** `Task<string?> GetDiscoveryPublicBaseUrlAsync(CancellationToken)` —
  liest `GetOrCreateSetupAsync` und liefert den normalisierten Admin-Wert (`null` bei
  leer/unset). Muster `GetMdnsAdvertisementEnabledAsync` (Zeilen 167–171).
- **Geänderte Methode:** `UpdateGeneralSettingsAsync` erhält einen zusätzlichen Parameter
  `string? discoveryPublicBaseUrl` (vor `CancellationToken`, nach
  `mdnsAdvertisementEnabled`); normalisiert per `DiscoveryUrlRules.NormalizePublicBaseUrl`,
  wirft `ArgumentException` bei nicht leerem, ungültigem Wert, schreibt
  `setup.DiscoveryPublicBaseUrl` im selben `SaveChangesAsync` wie die übrigen Felder.

### `UdpDiscoveryListener` (Klasse, `VideoWebPlayer/Services/UdpDiscoveryListener.cs`)

- **Geänderter Konstruktor:** `UdpDiscoveryListener(int port, Func<CancellationToken, Task<string>> responseFactory)` — ersetzt den bisherigen Parameter `string serverAddress`. Das Delegate liefert pro Anfrage die zu meldende Basis-URL.
- **Geänderte Methode:** `ListenAsync` — ruft vor dem Versand `_responseFactory(cancellationToken)` auf und sendet `VIDEOWEBPLAYER_SERVER:{ergebnis}`. Fehler im Delegate fallen in den bestehenden `catch { }`-Block der Schleife.
- **Unverändert:** `Start()`/`Stop()`, fester Antworttext-Aufbau, Fehlerverschlucken, kein DI/Logging.

### `Program` (Top-Level-Statements, `VideoWebPlayer/Program.cs`, Zeilen 50–57)

- **Geändert:** Der String-Aufbau `serverAddress = $"http://{...}"` entfällt. Stattdessen
  wird der Listener mit einem Resolver-Delegate instanziiert, das pro Anfrage:
  `IServiceScopeFactory`/`CreateAsyncScope` über `app.Services` →
  `ProgramSettingsService.GetDiscoveryPublicBaseUrlAsync` (try/catch → `null`),
  `IOptions<DiscoveryOptions>` und `IServer`/`IServerAddressesFeature.Addresses` lesen,
  `Dns.GetHostEntry(Dns.GetHostName())` fehlerabgeschirmt aufrufen und
  `DiscoveryResponseBuilder.Build` auswerten.
- **Neue Usings:** `Microsoft.Extensions.Options`, `Microsoft.Extensions.DependencyInjection`
  (für Scope/`GetRequiredService`), `Microsoft.AspNetCore.Hosting.Server`,
  `Microsoft.AspNetCore.Hosting.Server.Features`, `System.Net`, `VideoWebPlayer.Configuration`.
- **Unverändert:** `!app.Environment.IsEnvironment("Testing")`-Abschirmung, Port `5001`,
  Start vor `app.Run()`.

### `ServiceCollectionExtensions` (`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`)

- **Neu:** Registrierung `services.AddSingleton<IValidateOptions<DiscoveryOptions>, DiscoveryOptionsValidator>()` und `services.AddOptions<DiscoveryOptions>().Bind(configuration.GetSection("Discovery")).ValidateOnStart()` — direkt neben dem `MdnsOptions`-Block (Zeilen 280–283).
- **Unverändert:** `App:BaseUrl`-Lesestelle (Zeile 206) bleibt wie sie ist.

### `appsettings.json` (`VideoWebPlayer/appsettings.json`)

- **Neu:** Abschnitt `"Discovery": { "PublicBaseUrl": null }` (analog `"Mdns"`, Zeilen 42–47).
- **Neu:** `AutoUpdate:ProtectedFiles[1].JsonKeys` erhält den Eintrag `Discovery:PublicBaseUrl`
  (damit ein deployment-seitig gesetzter Wert Updates übersteht; `Host:Address`/`Host:Port`
  stehen bereits in der Liste und bleiben).

### `ProgramSettings.razor` (`VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor`)

- **Neue `admin-card`** „Öffentliche Basis-URL" (hinter der mDNS-Karte): `InputText`
  mit id `discoveryPublicBaseUrl`, `@bind-Value="model.DiscoveryPublicBaseUrl"`,
  `ValidationMessage` fürs Feld und `form-text`-Erläuterung (Zweck, Vorrang vor
  `Discovery:PublicBaseUrl`, leer = Ableitung, Beispiel-URL).
- **Geändert:** `OnInitializedAsync` lädt `setup.DiscoveryPublicBaseUrl` ins Model;
  `SaveAsync` übergibt den Wert an `UpdateGeneralSettingsAsync` und fängt
  `ArgumentException` der Service-Validierung ab (Anzeige als `alert-danger`,
  getrennt von der bestehenden `alert-success`-Statusmeldung).
- **`SettingsModel`:** neue Eigenschaft `public string? DiscoveryPublicBaseUrl { get; set; }`
  mit `[AbsoluteHttpUrl]`; `@using System.ComponentModel.DataAnnotations` ist bereits
  vorhanden (Zeile 2).
- **Unverändert:** `IsAdmin`-Prüfung, `EditForm`/`DataAnnotationsValidator`/
  `ValidationSummary`-Gerüst, atomarer Speichern-Button für alle Einstellungen.

### `VideoWebPlayerBackupData` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`)

- **Neu:** `OptionalRestoreColumns` (Zeilen 41–75) erhält
  `$"{nameof(ApplicationDbContext.Setups)}.{nameof(Setup.DiscoveryPublicBaseUrl)}"`.
- **Kein Eintrag** in `OptionalRestoreBoolDefaults`/`OptionalRestoreIntDefaults`/…:
  die Spalte ist `string?` — fehlt sie im Alt-Backup, bleibt sie `NULL` (= unset, Kette
  fällt auf `Discovery:PublicBaseUrl`/Ableitung zurück). Analoger Präzedenzfall:
  `Setups.PlaylistBackfillLastSweepAt` (nullable, Zeile 70, kein Defaults-Eintrag).
  Der Sonder-Default für `ApplicationTitle` (Zeilen 873–879) ist nicht übertragbar, da
  er eine Pflicht-Spalte mit fachlichem Standard betrifft.

## Datenbankmigrationen

- **Neue EF-Migration `AddSetupDiscoveryPublicBaseUrl`** via `dotnet ef migrations add`
  (Namens-/Ortsschema wie `20261004173814_AddSetupMdnsAdvertisementEnabled`):
  `migrationBuilder.AddColumn<string>(name: "DiscoveryPublicBaseUrl", table: "Setups",
  nullable: true)`; `Down` entfernt die Spalte. `ApplicationDbContextModelSnapshot.cs`
  wird durch das Tooling aktualisiert.
- **Reihenfolge-Regel (AGENTS.md):** Kollidiert die Migration beim Merge mit einer
  anderen Setup-/Schema-Migration, wird sie nach dem Rebase neu erzeugt statt
  Snapshot-Konflikte von Hand zu lösen.
- **Backup-Kompatibilität (DoD):** `OptionalRestoreColumns`-Eintrag (siehe oben) plus
  Regressionstest, der ein Alt-Backup ohne die Spalte wiederherstellt.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `DiscoveryOptions.PublicBaseUrl` (`Discovery:PublicBaseUrl`) | `null`/leer zulässig (= Ableitung); gesetzt → `DiscoveryUrlRules.IsValidPublicBaseUrl` (absolute URI, Schema `http`/`https`) | Ungültiger Wert → `ValidateOptionsResult.Fail` mit klarer Meldung; Start schlägt per `ValidateOnStart()` fehl |
| `SettingsModel.DiscoveryPublicBaseUrl` (Admin-Formular) | `[AbsoluteHttpUrl]`: leer/`null` zulässig; sonst absolute `http`/`https`-URI | `OnValidSubmit` feuert nicht; Meldung über `ValidationSummary`/`ValidationMessage` am Feld, nichts wird gespeichert |
| `ProgramSettingsService.UpdateGeneralSettingsAsync` (serverseitig) | Normalisierung `Trim`/leer → `null`; nicht leer → `IsValidPublicBaseUrl` Pflicht | `ArgumentException` mit klarer Meldung; kein `SaveChangesAsync`, alle Felder des Formulars bleiben ungeschrieben (atomar) |
| `DiscoveryResponseBuilder.Build` (defensiv) | Admin-Wert nur verwenden, wenn `IsValidPublicBaseUrl` gilt | Ungültiger Admin-Wert (nur über manipuliertes Backup denkbar) fällt auf `Discovery:PublicBaseUrl`/Ableitung durch — nie ungültige URL in der Antwort |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Discovery:PublicBaseUrl` | `string?` | `null` | Explizite öffentliche Basis-URL für die Discovery-Antwort (Schema, Host, Port, Pfad vollständig), z. B. `https://example.com/videoplayer/`; `null` = Ableitung bzw. Admin-Wert |
| `AutoUpdate:ProtectedFiles[1].JsonKeys` | Liste | – | Ergänzung um `Discovery:PublicBaseUrl` |
| `Setup.DiscoveryPublicBaseUrl` (DB) | `string?` | `NULL` | Admin-gepflegte öffentliche Basis-URL (`/admin/program-settings`); Vorrang vor `Discovery:PublicBaseUrl`; wird pro Discovery-Anfrage gelesen |
| `Host:Address` / `Host:Port` | bestehend | – | Unverändert vorhanden; neue dokumentierte Rolle als Ableitungsbausteine (`Host:Address` = expliziter Host-Override ohne Loopback/Wildcard, `Host:Port` = Port-Fallback) |

## Seiteneffekte und Risiken

- **Verhaltensänderung `Host:Address`:** Ein auf `localhost`/Loopback gesetztes
  `Host:Address` wird künftig ignoriert statt unbrauchbar gemeldet — gewollt; Installationen
  mit echter LAN-Adresse funktionieren identisch weiter.
- **Mehrere NICs/VLANs:** Die „primäre LAN-IPv4" kann bei mehreren NICs die für den
  fragenden Client falsche Adresse liefern; dokumentierter Ausweg ist der Admin-Wert
  oder `Discovery:PublicBaseUrl`. Begrenztes Risiko, da Broadcasts ohnehin nur aus dem
  lokalen Segment eintreffen.
- **IPv6-only-Netze:** Die Ableitung wählt keine IPv6-Adresse; Ergebnis ist der Hostname
  aus `Dns.GetHostName()` — abhängig von dessen clientseitiger Auflösbarkeit. Ausweg:
  Admin-Wert/`Discovery:PublicBaseUrl`.
- **IIS `OutOfProcess` / Reverse-Proxy / TLS-Terminierung:** Die Ableitung sieht nur das
  Backend (`http`, interner Port, kein Pfad) — für diese Deployments ist eine explizite
  URL (Admin-Wert oder `Discovery:PublicBaseUrl`) Pflichtkonfiguration, analog zum
  dokumentierten `Mdns:Port`-Hinweis. Wird in `GUIDE_Installation.md` dokumentiert.
- **Kestrel-Wildcard in `appsettings.Production.json`** (`http://*:5002`): Port `5002`
  wird korrekt abgeleitet, der Host kommt aus der DNS-Adressliste — deutliche Verbesserung
  gegenüber heute (`localhost`).
- **Backup-Übertragbarkeit des Admin-Werts:** `Setups.DiscoveryPublicBaseUrl` wird wie
  jede `Setups`-Spalte ins Backup geschrieben und beim Restore mit übernommen — auf einem
  anderen Host kann damit eine maschinenspezifische URL landen. Konsistent zum Verhalten
  aller übrigen `Setups`-Einstellungen; der Administrator kann den Wert dort anpassen
  oder leeren. Wird in `einrichtung.md` erwähnt.
- **DB-Lesung pro Discovery-Anfrage:** Zusätzlicher SQLite-Read je Broadcast —
  vernachlässigbar (Broadcasts sind selten, `Setup` ist eine Ein-Zeilen-Tabelle);
  Fehler sind fail-open (Ableitung greift).
- **Client-Vertrag:** `VIDEOWEBPLAYER_SERVER:`-Antworten enthalten künftig die
  vollständige Basis-URL inkl. Pfadanteil — verbindlich so entschieden; die
  Verarbeitung/Verifikation liegt beim MAUI-Client (externes Repository, Folgeaufgabe).
- **Bestehende Features:** `MdnsAdvertiserWorker`, der scoped `HttpClient` (`App:BaseUrl`)
  und alle anderen Konfigurationsleser bleiben unberührt.

## Umsetzungsreihenfolge

1. **`DiscoveryUrlRules` + `DiscoveryOptions` + `DiscoveryOptionsValidator` + DI-Registrierung + `appsettings.json`**
   - Voraussetzungen: Keine (Muster `MdnsOptions`/`MdnsOptionsValidator` liegen vor;
     `AddOptions`/`ValidateOnStart`/`IValidateOptions` sind bereits im Einsatz).
   - Beschreibung: `DiscoveryUrlRules` (Validierungs-/Normalisierungsregel),
     Options-Klasse und Validator in `VideoWebPlayer/Configuration/` anlegen;
     Registrierung in `ServiceCollectionExtensions` neben dem `MdnsOptions`-Block;
     `Discovery`-Sektion (`"PublicBaseUrl": null`) und `JsonKeys`-Eintrag
     `Discovery:PublicBaseUrl` in `appsettings.json` ergänzen.

2. **`Setup.DiscoveryPublicBaseUrl` + EF-Migration**
   - Voraussetzungen: Keine (unabhängig von Schritt 1).
   - Beschreibung: Property `DiscoveryPublicBaseUrl` (`string?`) in `Setup` ergänzen;
     Migration `AddSetupDiscoveryPublicBaseUrl` via `dotnet ef migrations add` erzeugen
     (nullable `TEXT`-Spalte); Snapshot aktualisiert sich mit.

3. **Backup-Kompatibilität**
   - Voraussetzungen: Schritt 2 (Spalte existiert im Modell).
   - Beschreibung: `OptionalRestoreColumns` in `VideoWebPlayerBackupData` um
     `Setups.DiscoveryPublicBaseUrl` ergänzen.

4. **`ProgramSettingsService` erweitern**
   - Voraussetzungen: Schritte 1–2 (`DiscoveryUrlRules`, `Setup`-Property).
   - Beschreibung: `GetDiscoveryPublicBaseUrlAsync` neu;
     `UpdateGeneralSettingsAsync` um Parameter `discoveryPublicBaseUrl` erweitern
     (Normalisierung, `ArgumentException` bei ungültigem Wert, atomares Speichern);
     bestehende Call-Sites (`ProgramSettings.razor`, `ProgramSettingsServiceTests`)
     werden im gleichen Schritt angepasst.

5. **`DiscoveryResponseBuilder` anlegen**
   - Voraussetzungen: Schritt 1 (`DiscoveryOptions`, `DiscoveryUrlRules`).
   - Beschreibung: `internal static`-Klasse in `VideoWebPlayer/Services/` mit
     `Build(DiscoveryOptions, IConfiguration, IEnumerable<string>? boundAddresses, IReadOnlyList<IPAddress>? hostAddresses, string? adminPublicBaseUrl)`;
     Vorrangkette (Admin-Wert → `PublicBaseUrl` → Ableitung), Schema-/Port-Kette,
     Host-Kette, Loopback-/Wildcard-Verbot — Port-Extraktion analog
     `MdnsServiceProfileBuilder.TryGetPort`.

6. **`UdpDiscoveryListener` auf Resolver-Delegate umstellen**
   - Voraussetzungen: Keine harte; fachlich sinnvoll nach Schritt 5, damit das Delegate
     direkt den Builder aufrufen kann.
   - Beschreibung: Konstruktorparameter `string serverAddress` →
     `Func<CancellationToken, Task<string>> responseFactory`; `ListenAsync` löst die URL
     pro beantworteter Anfrage auf.

7. **`Program.cs`-Verdrahtung**
   - Voraussetzungen: Schritte 1, 4–6 (`DiscoveryOptions`, `ProgramSettingsService`,
     Builder, neuer Konstruktor).
   - Beschreibung: Resolver-Delegate im `Testing`-geschirmten Block anlegen
     (Scope + `GetDiscoveryPublicBaseUrlAsync` fail-open, `IOptions<DiscoveryOptions>`,
     `IServer`/`IServerAddressesFeature`, fehlerabgeschirmte `Dns`-Abfrage →
     `DiscoveryResponseBuilder.Build`); alte String-Interpolation entfernen; Usings ergänzen.

8. **Admin-UI (`AbsoluteHttpUrlAttribute` + `ProgramSettings.razor`)**
   - Voraussetzungen: Schritte 1 und 4 (`DiscoveryUrlRules`, erweiterter
     `UpdateGeneralSettingsAsync`-Aufruf).
   - Beschreibung: `AbsoluteHttpUrlAttribute` in `Components/Shared/` anlegen;
     neue `admin-card` mit `InputText`-Feld, `ValidationMessage`, `form-text`;
     `SettingsModel`-Property, Laden in `OnInitializedAsync`, Speichern in `SaveAsync`
     inkl. `ArgumentException`-Abfangen mit `alert-danger`-Anzeige.

9. **Unit-Tests**
   - Voraussetzungen: Schritte 1–8.
   - Beschreibung: `DiscoveryUrlRulesTests`, `DiscoveryResponseBuilderTests`,
     `DiscoveryOptionsValidatorTests`, `DiscoveryConfigurationTests`,
     `UdpDiscoveryListenerTests`, `ProgramSettingsServiceTests`-Erweiterung und
     `VideoWebPlayerBackupDataTests`-Regressionstest anlegen — siehe Testabschnitt.

10. **E2E-Tests**
    - Voraussetzungen: Schritte 4 und 8 (Service-Signatur, Formularfeld final).
    - Beschreibung: `ProgramSettingsE2ETests` um den Benutzerfluss „Admin trägt
      öffentliche Basis-URL ein und speichert" sowie den Validierungsfall erweitern —
      siehe E2E-Abschnitt.

11. **Dokumentation + Verifikation**
    - Voraussetzungen: Schritte 1–10 (Verhalten final).
    - Beschreibung: `docs/API.md`, `docs/GUIDE_Installation.md`, `docs/help/einrichtung.md`,
      `README.md`, `docs/RELEASE_NOTES.md` aktualisieren; `dotnet test
      tools/MarkdownLinkCheck.Tests`; Build Debug + Release und voller Testlauf.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `IsValidPublicBaseUrl_*` (Facts/Theories) | `DiscoveryUrlRulesTests` | `null`/leer/Whitespace gültig; absolute `http`/`https`-URIs inkl. Pfad/Port gültig; relative URIs, Nicht-URI-Strings, Nicht-HTTP-Schemata (`ftp`, `dns`, `file`) ungültig. `NormalizePublicBaseUrl`: trimmt, Whitespace → `null` |
| `Build_ReturnsAdminBaseUrl_WhenSet` | `DiscoveryResponseBuilderTests` | Admin-Wert schlägt `Discovery:PublicBaseUrl` und alle Ableitungsquellen — inkl. `https`-Schema und Pfad (`https://videos.example.com/videoplayer/`) |
| `Build_UsesPublicBaseUrl_WhenAdminEmpty` (Theory: `null`/`""`/`" "`) | `DiscoveryResponseBuilderTests` | Leerer Admin-Wert → `Discovery:PublicBaseUrl` greift |
| `Build_IgnoresInvalidAdminBaseUrl` | `DiscoveryResponseBuilderTests` | Ungültiger Admin-Wert (`notaurl`, `ftp://x`) → Durchfall auf `Discovery:PublicBaseUrl`/Ableitung |
| `Build_ReturnsPublicBaseUrl_WhenConfigured` | `DiscoveryResponseBuilderTests` | Konfigurierte `PublicBaseUrl` wird unverändert geliefert — inkl. `https`-Schema, Port und Pfad; schlägt alle Ableitungsquellen |
| `Build_DerivesLanAddress_WhenNothingConfigured` | `DiscoveryResponseBuilderTests` | Ohne Admin-Wert und Konfiguration: `http://<lan-ipv4>:<port>` aus `hostAddresses` + Port-Kette; Ergebnis enthält nie `localhost`/Loopback |
| `Build_UsesHostAddress_WhenLanAddress` | `DiscoveryResponseBuilderTests` | `Host:Address=192.168.1.10` wird als Host gemeldet (Bestandsschutz) |
| `Build_IgnoresLoopbackHostAddress` (Theory) | `DiscoveryResponseBuilderTests` | `Host:Address` ∈ {`localhost`, `127.0.0.1`, `::1`, `0.0.0.0`, `*`} wird ignoriert → Host aus DNS-Adressliste |
| `Build_UsesBoundLiteralAddress` | `DiscoveryResponseBuilderTests` | Gebunden an `http://192.168.1.5:5000` → Host und Port aus der gebundenen Adresse |
| `Build_UsesBoundWildcardPort_ButDnsHost` | `DiscoveryResponseBuilderTests` | Gebunden an `http://0.0.0.0:5002` (Wildcard): Port `5002`, Host aus `hostAddresses` (Produktionsfall `http://*:5002`) |
| `Build_UsesKestrelEndpointUrl` / `Build_UsesKestrelHttpsScheme` | `DiscoveryResponseBuilderTests` | Port/Schema aus `Kestrel:Endpoints:Http:Url` bzw. `Https:Url`, wenn keine gebundenen Adressen vorliegen; `https`-Endpunkt liefert Schema `https` |
| `Build_UsesHostPortFallback` / `Build_UsesDefaultPort5000` | `DiscoveryResponseBuilderTests` | Port-Kette `Host:Port` → `5000` |
| `Build_SkipsLoopbackBoundAddress_ForHost` | `DiscoveryResponseBuilderTests` | Gebunden an `http://localhost:5000` (typisch `dotnet run`): Port `5000`, Host aus `hostAddresses` — kein `localhost` in der Antwort |
| `Build_UsesHostName_WhenNoLanAddress` | `DiscoveryResponseBuilderTests` | `hostAddresses` leer/`null` → Hostname als letzte Stufe |
| `Build_SkipsIpv6Addresses` | `DiscoveryResponseBuilderTests` | Reine IPv6-/Link-Local-Liste → keine IPv6-Adresse im Ergebnis (Hostname- oder APIPA-Fallback) |
| `Validate_AcceptsNullOrEmpty` / `Validate_AcceptsAbsoluteHttpUrl` / `Validate_AcceptsUrlWithPathAndPort` / `Validate_RejectsRelativeUrl` / `Validate_RejectsNonHttpScheme` | `DiscoveryOptionsValidatorTests` | `null`/leer zulässig; absolute `http`/`https`-URIs inkl. Pfad/Port gültig; relative URIs, Nicht-URI-Strings und andere Schemata (`ftp`, `dns`) → `Fail` |
| `DiscoverySection_BindsToOptions` | `DiscoveryConfigurationTests` | Binding `Discovery:PublicBaseUrl` auf `DiscoveryOptions` per `AddInMemoryCollection` + `AddOptions` (Muster `MdnsConfigurationTests`) |
| `Start_AnswersDiscoveryRequest_WithResolvedAddress` | `UdpDiscoveryListenerTests` | Echter Loopback-UDP-Test auf freiem Port: Listener mit Delegate `() => Task.FromResult("http://192.168.1.5:5000/")`, Senden von `VIDEOWEBPLAYER_DISCOVERY` an `127.0.0.1:{port}`, Antwort muss `VIDEOWEBPLAYER_SERVER:http://192.168.1.5:5000/` lauten; Empfang per `UdpClient` mit Timeout (`WaitAsync`), `Stop()` im `finally`. Nicht Port 5001 — sonst Kollision mit laufender Instanz. |
| `Start_ResolvesAddress_PerRequest` | `UdpDiscoveryListenerTests` | Zwei Discovery-Anfragen, zwischen denen das Delegate einen anderen Wert liefert → jede Antwort enthält den jeweils aktuellen Wert (Nachweis der pro-Anfrage-Auflösung, Voraussetzung dafür, dass Admin-Änderungen ohne Neustart wirken) |
| `GetDiscoveryPublicBaseUrlAsync_DefaultsToNull` | `ProgramSettingsServiceTests` | Standard-`Setup`-Zeile → `null`. Echtes SQLite per `PairingTestDb`-Muster (nicht EF InMemory) |
| `UpdateGeneralSettingsAsync_PersistsDiscoveryPublicBaseUrl` | `ProgramSettingsServiceTests` | Wert wird persistiert und normalisiert (Trim); leer/Whitespace → `NULL`; Löschen über leeren Wert funktioniert; atomar mit den übrigen Feldern (ein `SaveChangesAsync` wie bei `MdnsAdvertisementEnabled`) |
| `UpdateGeneralSettingsAsync_RejectsInvalidDiscoveryPublicBaseUrl` (Theory) | `ProgramSettingsServiceTests` | `notaurl`, `/relativ`, `ftp://x` → `ArgumentException`, `Setup`-Zeile unverändert |
| `ReadFromAsync_LegacyBackupWithoutDiscoveryPublicBaseUrlColumn_RestoresWithNull` | `VideoWebPlayerBackupDataTests` | Alt-Backup ohne `Setups.DiscoveryPublicBaseUrl` (per `LegacyBackupArchiveBuilder.RemoveColumnsAsync` real aus dem Archiv entfernt) wird wiederhergestellt; Spalte bleibt `NULL` (Muster `ReadFromAsync_LegacyBackupWithoutPlaylistBackfillLastSweepAtColumn_RestoresWithNull`); echtes SQLite |
| `CreateConfiguration(Dictionary<string,string?>)` | `DiscoveryResponseBuilderTests` (lokal) | `ConfigurationBuilder().AddInMemoryCollection(...)` — Hilfsmethode, Muster `MdnsServiceProfileBuilderTests` |

### Betroffene bestehende Tests

- `ProgramSettingsServiceTests.UpdateGeneralSettingsAsync_PersistsMdnsAdvertisementEnabled`
  (Zeilen 31, 38): Signatur von `UpdateGeneralSettingsAsync` ändert sich — Aufrufe werden
  um den neuen Parameter ergänzt (Kompilieranpassung, kein Verhaltensänderung).
- `ProgramSettingsE2ETests`: Klasse wird um neue Tests erweitert;
  `Admin_TogglesMdnsAdvertisement_AndSettingPersists` und
  `NonAdmin_GetsNotAuthorized_OnProgramSettings` müssen unverändert grün bleiben
  (der Nicht-Admin-Test erhält eine zusätzliche Assertion, dass das neue Feld
  `#discoveryPublicBaseUrl` nicht gerendert wird).
- `AutoUpdateProtectedFilesTests.ShippedAppsettingsProtectedFiles_AreValid` validiert die
  geänderte `appsettings.json` automatisch mit (keine Anpassung nötig — der neue
  `JsonKeys`-Eintrag muss nur formal gültig sein, was der Test sichert).

### E2E-Tests (primärer Funktionsnachweis)

Das Feature ist durch die Admin-Pflegbarkeit UI-relevant — der konkrete Benutzerfluss wird
per Playwright-E2E in der bestehenden Klasse `ProgramSettingsE2ETests` nachgewiesen
(Muster `Admin_TogglesMdnsAdvertisement_AndSettingPersists`:

| Test | Ablauf / Nachweis |
|------|-------------------|
| `Admin_SavesDiscoveryPublicBaseUrl_AndSettingPersists` | Admin-Login → `GET /admin/program-settings` → Feld „Öffentliche Basis-URL" (`#discoveryPublicBaseUrl`) mit `https://videos.example.com/videoplayer/` befüllen → `Speichern` → „Gespeichert." sichtbar; `ProgramSettingsService.GetDiscoveryPublicBaseUrlAsync` in eigenem Scope liefert den Wert; Reload → Feld gefüllt; Feld leeren + Speichern → Dienst liefert `null` (Löschpfad) |
| `Admin_EntersInvalidDiscoveryPublicBaseUrl_ValidationBlocksSave` | Ungültige Eingabe (z. B. `keine-url` bzw. `ftp://x`, Theory nicht nötig — eine nicht-absolute bzw. eine Nicht-HTTP-Eingabe) → `Speichern` → Validierungsmeldung sichtbar, „Gespeichert." erscheint **nicht**, `GetDiscoveryPublicBaseUrlAsync` bleibt `null` |
| `NonAdmin_GetsNotAuthorized_OnProgramSettings` (bestehend, erweitert) | Zusätzliche Assertion: `#discoveryPublicBaseUrl` hat Count 0 — Nicht-Admins sehen das Feld nicht |

Der UDP-Versand selbst bleibt — wie beim mDNS-Feature — nur manuell verifizierbar
(`Testing`-Abschirmung, fester Port 5001); der Netzwerkteil ist über
`UdpDiscoveryListenerTests` (echter Loopback-UDP-Test) und die netzwerkfreien
Builder-Tests abgedeckt.

## Offene Punkte

Keine.
