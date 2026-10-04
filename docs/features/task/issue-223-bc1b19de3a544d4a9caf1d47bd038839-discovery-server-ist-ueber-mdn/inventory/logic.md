# Bestandsaufnahme: Logik (Discovery / Start / Konfiguration)

## `UdpDiscoveryListener`
Datei: `VideoWebPlayer/Services/UdpDiscoveryListener.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `UdpDiscoveryListener(int port, string serverAddress)` | public | Konstruktor; speichert UDP-Port und die als Antwort zu sendende Serveradresse (Zeile 23). |
| `Start()` | public | Erzeugt ein `CancellationTokenSource` und startet `ListenAsync` via `Task.Run` im Hintergrund (Zeile 32). |
| `Stop()` | public | Bricht die Hintergrundaufgabe über `_cts.Cancel()` ab; wird aktuell nirgends aufgerufen (kein Shutdown-Handling, Zeile 41). |
| `ListenAsync(CancellationToken)` | private | Bindet `UdpClient` auf den übergebenen Port; beantwortet exakt die Anfrage `VIDEOWEBPLAYER_DISCOVERY` (UTF-8) mit `VIDEOWEBPLAYER_SERVER:<serverAddress>` an den Absender-Endpunkt; alle Exceptions werden still verschluckt (Zeilen 46–63). |

Abonnierte Events: keine
Publizierte Events: keine

Bemerkungen:
- Wird nur in `Program.cs` (Zeilen 53–56) instanziiert — nicht über DI, nicht als `IHostedService`.
- Kein Interface, keine Konfiguration per Options-Muster; Port und Adresse werden als Konstruktorparameter übergeben.
- `Stop()` wird nicht mit dem Application-Shutdown verknüpft (kein `IHostApplicationLifetime`-Hook).

## `Program` (Top-Level-Statements)
Datei: `VideoWebPlayer/Program.cs`

| Bereich | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `builder.AddLocalJsonConfiguration()` | 20 | Registriert die optionale `appsettings.Local.json`. |
| `builder.AddVideoWebPlayerServices()` | 31 | Registriert alle Dienste und Hintergrunddienste (siehe `ServiceCollectionExtensions`). |
| `builder.AddVideoWebPlayerAutoUpdate()` | 32 | Registriert das Update-Subsystem (`msTools.Updater`). |
| `builder.WebHost.ConfigureKestrel(...)` | 36–43 | Wendet `Kestrel:Limits:MaxRequestBodySize` manuell an (Kestrel bindet `Limits` nicht aus der Konfiguration). |
| `app.MigrateDatabase()` / `app.UseVideoWebPlayer()` | 47–48 | EF-Migrationen und Middleware-Pipeline. |
| UDP-Discovery-Start | 50–57 | `if (!app.Environment.IsEnvironment("Testing"))`: erzeugt `UdpDiscoveryListener(5001, "http://{Host:Address ?? localhost}:{Host:Port ?? 5000}")` und ruft `Start()`. Der `Testing`-Guard verhindert, dass E2E-Tests den festen Port 5001 belegen. |

Bemerkungen:
- `Host:Address` und `Host:Port` sind in `appsettings.json` **nicht definiert** — nur in der `AutoUpdate:ProtectedFiles`-Merge-Liste aufgeführt. Ohne Konfiguration gilt der Fallback `localhost:5000` (für mDNS-Adress-Announcements ungeeignet).
- `Kestrel:Endpoints:Http:Url` wird von Kestrel selbst ausgewertet; in `appsettings.Production.json` steht `http://*:5002` — der tatsächlich gebundene Port kann daher von `Host:Port` abweichen.
- `builder.Environment.ContentRootPath/Logs` wird vorab angelegt (Zeile 23).

## `ServiceCollectionExtensions`
Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddVideoWebPlayerServices(this WebApplicationBuilder)` | public static | Registriert sämtliche Dienste (Zeile 41). Hintergrunddienste per `AddHostedService<>`: `MediaSourceScanService` (Z. 284), `ContinueWatchingWorker` (Z. 290), `ActorBackfillWorker` (Z. 291), `PlaylistBackfillWorker` (Z. 292). Optionsbindung-Beispiele: `PlaylistSettings` über `services.Configure<>(configuration.GetSection("Playlists"))` (Z. 237), `EpisodeBackgroundImageOptions` mit `ValidateOnStart()` (Z. 277–279). |

Bemerkungen:
- Erweiterungspunkt für einen mDNS-`BackgroundService`; es gibt hier derzeit **keinen** `Testing`-Guard — die Registrierung ist umgebungsunabhängig (ein Hosted Service müsste die `Testing`-Abschaltung selbst berücksichtigen oder die Registrierung müsste konditional erfolgen).
- Weitere `AddHostedService`-Aufrufe außerhalb dieser Methode: `UpdateSettingsInitializer` und `UpdateBackupEventBinder` in `AutoUpdateExtensions.AddVideoWebPlayerAutoUpdate`.

## `LocalConfigurationExtensions`
Datei: `VideoWebPlayer/Extensions/LocalConfigurationExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddLocalJsonConfiguration(this WebApplicationBuilder)` | public static | Fügt `appsettings.Local.json` (optional, `reloadOnChange`) direkt nach der letzten `appsettings*.json`-Quelle ein — vor User Secrets, Umgebungsvariablen und Kommandozeile (Zeilen 26–64). |
| `IsAppSettingsFileName(string?)` | private static | Erkennt `appsettings*.json`-Quellen case-insensitiv (Zeile 66). |

## `AutoUpdateExtensions`
Datei: `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddVideoWebPlayerAutoUpdate(this WebApplicationBuilder)` | public static | Bindet `UpdateBackupOptions` aus `AutoUpdate:Backup`, ruft `builder.UseAutoUpdate(...)` (msTools.Updater) mit Update-Quelle und systemd-Unit-Name `VideoWebPlayer-AutoUpdate` auf; registriert `UpdateSettingsInitializer` und `UpdateBackupEventBinder` als Hosted Services. |

Bemerkung: Die `AutoUpdate:ProtectedFiles`-Merge-Regeln (u. a. `JsonKeys` für `appsettings*.json`) werden von `msTools.Updater` aus der Konfiguration gelesen — neue Konfigurationsschlüssel müssen dort eingetragen werden (siehe `configuration.md`).

## `KestrelLimits`
Datei: `VideoWebPlayer/Extensions/KestrelLimits.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ParseMaxRequestBodySize(string?)` | internal static | Parst `Kestrel:Limits:MaxRequestBodySize`; 0/negativ = unbegrenzt (`null`), sonst Byte-Wert; wirft `InvalidOperationException` bei ungültigem Wert. |

## `ProgramSettingsService`
Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetOrCreateSetupAsync(CancellationToken)` | public | Liefert die einzige `Setup`-Zeile (legt sie mit Defaults an, korrigiert ungültige Werte). |
| `GetScanIntervalsAsync(CancellationToken)` | public | Abgeleitete Scan-Intervalle aus `Setup`. |
| `UpdateScanIntervalsAsync(...)` | public | Persistiert Scan-Intervalle. |
| `GetApplicationTitleAsync(CancellationToken)` | public | Liefert `Setup.ApplicationTitle` (Default „Martins Videosammlung"). |
| `GetContinueWatchingEndThresholdAsync(...)` | public | Liefert `Setup.ContinueWatchingEndThresholdSeconds`. |
| `UpdateGeneralSettingsAsync(...)` | public | Persistiert Titel, Scan-Intervalle und Continue-Watching-Schwelle. |

Bemerkung: Derzeit keine Discovery-/Netzwerk-Einstellung — relevant nur für die offene Frage, ob das mDNS-Feature adminseitig über `Setup` schaltbar sein soll (würde EF-Migration und Backup-Erweiterung erfordern).

## Nicht vorhanden

- Keine mDNS-/Zeroconf-/DNS-SD-Implementierung, kein `*Advertiser`/`*Announcer`/`*Responder` im gesamten Repository (Volltextsuche nach `mDNS`, `mdns`, `zeroconf`, `avahi`, `bonjour`, `5353` in Quellcode ohne Treffer; nur Dokumentation).
- Kein mDNS-/Zeroconf-NuGet-Paket in `VideoWebPlayer/VideoWebPlayer.csproj`, `VideoWebPlayer.Client/` oder `VideoWebPlayer.Tests/`.
- Keine Discovery-Client-Implementierung im Repo — die MAUI-App `VideoPlayer.Maui` liegt in einem separaten Repository und sucht laut Anforderung `_http._tcp.local.`.
