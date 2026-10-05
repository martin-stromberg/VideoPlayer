# Logik

## `UdpDiscoveryListener`
Datei: `VideoWebPlayer/Services/UdpDiscoveryListener.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `UdpDiscoveryListener(int port, string serverAddress)` | public (Konstruktor) | Nimmt die Antwortadresse als fertige Zeichenkette entgegen (`_serverAddress`); keine Konfigurations- oder DI-Abhängigkeit |
| `Start()` | public | Startet `ListenAsync` fire-and-forget per `Task.Run` mit eigenem `CancellationTokenSource` |
| `Stop()` | public | Cancelled das Token; wartet die Hintergrundaufgabe nicht ab |
| `ListenAsync(CancellationToken)` | private | `UdpClient` auf dem übergebenen Port; beantwortet exakt die Textanfrage `VIDEOWEBPLAYER_DISCOVERY` mit `VIDEOWEBPLAYER_SERVER:{_serverAddress}` an `result.RemoteEndPoint`; alle Fehler werden verschluckt (`catch { }`) |

- Wird einmalig in `Program.cs` (Zeilen 51–56) instanziiert und gestartet — vor `app.Run()`,
  abgeschirmt durch `!app.Environment.IsEnvironment("Testing")`, UDP-Port fest `5001`.
- Keine Events, kein Logging, kein Interface, keine Registrierung im DI-Container.
- `UdpClient.ReceiveAsync` liefert nur `RemoteEndPoint` (Absenderadresse); die lokale
  Empfangsadresse des Broadcasts steht über diese API nicht zur Verfügung.

## `Program` (Top-Level-Statements)
Datei: `VideoWebPlayer/Program.cs` (Zeilen 50–57)

| Stelle | Kurzbeschreibung |
|--------|------------------|
| Zeile 53 | Discovery-Port hart `5001` |
| Zeile 54 | `serverAddress = $"http://{Configuration["Host:Address"] ?? "localhost"}:{Configuration["Host:Port"] ?? "5000"}"` — einzige Lesestelle von `Host:Address`; `Host:Port` wird zusätzlich von `MdnsServiceProfileBuilder` gelesen |
| Zeilen 55–56 | `new UdpDiscoveryListener(udpPort, serverAddress)` + `Start()` — Auswertung einmalig beim Start, nicht pro Anfrage |
| Zeile 20 | `builder.AddLocalJsonConfiguration()` lädt optionales `appsettings.Local.json` (siehe `LocalConfigurationExtensions`) |
| Zeilen 36–43 | `ConfigureKestrel` liest `Kestrel:Limits:MaxRequestBodySize` (Muster für explizites Auslesen von Kestrel-Konfiguration) |

## `MdnsServiceProfileBuilder`
Datei: `VideoWebPlayer/Services/MdnsServiceProfileBuilder.cs`

`internal static` — reine, netzwerkfreie Ableitungslogik (Vorbild für die geforderte
Discovery-URL-Ableitung).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DefaultPort` (Konstante) | public const | Fallback-Port `5000` |
| `Build(MdnsOptions, IConfiguration, IEnumerable<string>? boundAddresses)` | public static | Erzeugt `MdnsAdvertisement`; `HostName` per `Dns.GetHostName()` (Zeile 37), TXT-Records fest `path=/`, `app=VideoWebPlayer` |
| `ResolvePort(MdnsOptions, IConfiguration, IEnumerable<string>?)` | private static | Port-Kette: `Mdns:Port` → erste gebundene Adresse mit Port → `Kestrel:Endpoints:Http:Url` → `Host:Port` → `5000` |
| `TryGetPort(string? address)` | private static | Port-Extraktion per `Uri.TryCreate`; Kestrel-Wildcards `*`/`+` werden zu `localhost` normalisiert; nur `http`/`https` akzeptiert |

- Aufgerufen von `MdnsAdvertiserWorker.TryAdvertise` (Zeile 179).

## `MdnsAdvertiserWorker`
Datei: `VideoWebPlayer/Services/MdnsAdvertiserWorker.cs`

`sealed class : BackgroundService` — Laufzeit-Gegenstück zum Builder.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `MdnsAdvertiserWorker(...)` | public (Konstruktor) | DI: `IOptions<MdnsOptions>`, `IConfiguration`, `IServer`, `IHostApplicationLifetime`, `IServiceScopeFactory`, `ILogger` |
| `IsAdvertisementEnabled(bool, bool)` | internal static | Konjunktion Operatorschalter × Admin-Schalter |
| `ExecuteAsync(CancellationToken)` | protected override | Wartet auf `ApplicationStarted`, prüft `Mdns:Enabled`, liest Admin-Schalter aus DB, advertised/unadvertised in 60-s-Schleife |
| `StopAsync(CancellationToken)` | public override | Goodbye-Deregistrierung beim Shutdown |
| `WaitForApplicationStartedAsync` | private | Muster, um gebundene Adressen erst nach Serverstart zu nutzen (Zeilen 144–152) |
| `RefreshAdminSwitchAsync` | private | Liest `ProgramSettingsService.GetMdnsAdvertisementEnabledAsync` in eigenem Scope |
| `TryAdvertise` | private | Liest `IServer.Features.Get<IServerAddressesFeature>()?.Addresses` (Zeile 178), ruft `MdnsServiceProfileBuilder.Build`, startet `ServiceDiscovery` (Paket `Makaretu.Dns`) |
| `TryUnadvertise` / `ToServiceName` | private | Deregistrierung / `.local`-Suffix-Normalisierung |

- Registriert in `ServiceCollectionExtensions.cs` Zeilen 297–301, ebenfalls abgeschirmt
  durch `!env.IsEnvironment("Testing")`.

## `ServiceCollectionExtensions`
Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`

| Stelle | Kurzbeschreibung |
|--------|------------------|
| `AddVideoWebPlayerServices` (Zeile 41) | Zentrale Service-Registrierung |
| Zeilen 171–177 | Named `HttpClient` `"Internal"` |
| Zeilen 180–213 | Scoped `HttpClient`-Factory: `BaseAddress` aus `NavigationManager` → `IHttpContextAccessor` (inkl. `PathBase`) → Fallback `IConfiguration["App:BaseUrl"]` (Zeile 206). `App:BaseUrl` steht in keiner `appsettings*.json`-Datei und nicht in `AutoUpdate:ProtectedFiles[1].JsonKeys` — einzige Lesestelle |
| Zeilen 280–283 | Options-Bindung `Mdns` + `ValidateOnStart()` (Muster für neue Options-Typen) |
| Zeilen 297–301 | `AddHostedService<MdnsAdvertiserWorker>()` nur außerhalb `Testing` |

## `LocalConfigurationExtensions`
Datei: `VideoWebPlayer/Extensions/LocalConfigurationExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddLocalJsonConfiguration(WebApplicationBuilder)` | public static | Fügt `appsettings.Local.json` (optional, reloadOnChange) direkt hinter dem letzten `appsettings*.json`-Provider ein — übersteuert Paket-Defaults, verliert gegen Secrets/Env/Cmdline |

## `ProgramSettingsService` (Ausschnitt)
Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `UpdateGeneralSettingsAsync(...)` | public | Persistiert u. a. `MdnsAdvertisementEnabled` (Zeile 141–160) |
| `GetMdnsAdvertisementEnabledAsync` | public | Admin-Schalter lesen (Zeile 167) |
| `UpdateMdnsAdvertisementEnabledAsync` | public | Admin-Schalter schreiben (Zeile 178) |

- Aufgerufen von `MdnsAdvertiserWorker.RefreshAdminSwitchAsync`.

## `MdnsOptionsValidator`
Datei: `VideoWebPlayer/Configuration/MdnsOptionsValidator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Validate(string?, MdnsOptions)` | public | `IValidateOptions<MdnsOptions>`; Port-Range 1–65535, `ServiceType`-Regex (RFC 6335), `InstanceName` Pflicht/Länge nur bei `Enabled` |

Abonnierte Events: keine im betroffenen Bereich (nur `IHostApplicationLifetime.ApplicationStarted`
als Registrierung in `MdnsAdvertiserWorker`; SignalR/`JwtBearerEvents.OnMessageReceived` sind
fachlich nicht betroffen).

Publizierte Events: keine.
