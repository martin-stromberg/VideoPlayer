# Code-Fakten (nur lesend analysiert)

Verhalten, das die Produktions-Installationsdokumentation abbilden muss. Alle
Angaben aus dem aktuellen Stand des Branches (Commit `5fc611e`).

## HTTP-Pipeline — `VideoWebPlayer/Extensions/WebApplicationExtensions.cs`

`UseVideoWebPlayer` (Zeilen 39–137), Reihenfolge der Middleware:

| Zeilen | Mechanismus | Verhalten |
|--------|-------------|-----------|
| 47–51 | `PathBase` → `app.UsePathBase(pathBase)` | Konfigurationsschlüssel `PathBase` (direkt aus `IConfiguration`, kein Options-Binding). Leer = unveränderter Betrieb im Domain-Root. Läuft als eine der ersten Middlewares, damit Routing den bereinigten Pfad sieht. |
| 58–64 | `app.UseForwardedHeaders` | `ForwardedHeaders = XForwardedProto \| XForwardedFor`. `KnownProxies` enthält **fest** nur `IPAddress.Loopback` (`127.0.0.1`) und `IPAddress.IPv6Loopback` (`::1`); `KnownNetworks` bleibt leer, `ForwardLimit` nicht gesetzt. Konsequenz: `X-Forwarded-*`-Header werden nur von einem Proxy auf demselben Host akzeptiert — Header anderer Absender werden ignoriert. |
| 69–78 | Host-Header-Prüfung | Requests mit leerem/unparsbarem `Host`-Header werden mit `400` abgelehnt. |
| 81–89 | Fehler/HSTS | Nur außerhalb `Development`: `UseExceptionHandler("/Error")` und `UseHsts()`. In `Production` ist HSTS damit aktiv (Standard-Parameter der Middleware: `max-age=30 days`, keine Subdomains, kein Preload). |
| 91 | `app.UseHttpsRedirection()` | Immer in der Pipeline. Ohne gebundenen HTTPS-Port kann die Middleware kein Ziel ermitteln (ASP.NET-Core-Standardverhalten: einmalige Warnung `Failed to determine the https port for redirect`, kein Redirect — reiner HTTP-Betrieb läuft trotzdem). Hinter einem TLS-terminierenden Proxy setzt `X-Forwarded-Proto` das Schema; die Redirect-Middleware entscheidet dann auf Basis des weitergegebenen Schemas. |
| 94 | `app.UseWhitelistIp()` | Eigene IP-Whitelist-Middleware (Admin-Einstellung), für die Installationsdoku nicht weiter relevant. |
| 102–114 | `UseStaticFiles` | `Cache-Control: no-cache` für `.css`/`.js`/`.html`. |
| 116–121 | Auth/Backups/Restore/FirstUser/Antiforgery | Standard-Reihenfolge. |
| 124–134 | Endpunkte | Razor Components (interaktiv Server), Controller, SignalR-Hub `/hubs/mediaupdate` (JWT), Identity-Endpunkte. |

## Startsequenz — `VideoWebPlayer/Program.cs`

| Zeilen | Mechanismus |
|--------|-------------|
| 20 | `builder.AddLocalJsonConfiguration()` — registriert `appsettings.Local.json`. |
| 23 | `Directory.CreateDirectory(ContentRootPath/"Logs")` — `Logs/` wird beim Start angelegt. |
| 26–29 | Serilog aus Konfiguration (File-Sink `Logs/web-.log`, täglich rollierend, 3 Dateien). |
| 31–32 | `AddVideoWebPlayerServices()`, `AddVideoWebPlayerAutoUpdate()`. |
| 36–43 | `ConfigureKestrel`: `Kestrel:Limits:MaxRequestBodySize` explizit gebunden (`0`/negativ = unbegrenzt), weil Kestrel `Limits` nicht aus der Konfiguration lädt. |
| 47 | `app.MigrateDatabase()` — EF-Migrationen laufen bei jedem Start automatisch (Service-Account braucht Schreibrecht auf der SQLite-DB). |
| 51–62 | `UdpDiscoveryListener` auf UDP-Port **5001**, fest kodiert (`var udpPort = 5001`); startet nicht unter Umgebung `Testing`. Antwortet auf `VIDEOWEBPLAYER_DISCOVERY` mit `VIDEOWEBPLAYER_SERVER:<basis-url>`; die Basis-URL wird pro Anfrage über `DiscoveryBaseUrlResolver` aufgelöst. |

## Lokale Konfiguration — `VideoWebPlayer/Extensions/LocalConfigurationExtensions.cs`

- Dateiname `appsettings.Local.json` relativ zum Content-Root (Zeile 14), `Optional=true`, `ReloadOnChange=true` (Zeilen 55–60).
- Einfügung direkt nach der letzten `appsettings*.json`-Quelle (Zeilen 30–53): überlagert Paket-`appsettings*.json`, verliert gegen User Secrets, Umgebungsvariablen und Kommandozeile.
- Wird **nie paketiert**: `VideoWebPlayer.csproj` Zeilen 64–67 (`CopyToOutputDirectory`/`CopyToPublishDirectory` = `Never`); zusätzlich `.gitignore`-Eintrag `/VideoWebPlayer/appsettings.Local.json`.
- Seiteneffekt laut `docs/TECH_Auto_Update.md` (Zeilen 173–177): der `appsettings*.json`-Wildcard der `ProtectedFiles`-Schutzliste erfasst sie — bei Updates Sicherung nach `Updates/backup/` und Neuformatierung durch den JSON-Merge; Kommentare sind zu vermeiden.

## Discovery — `VideoWebPlayer/Services/`

- `DiscoveryBaseUrlResolver` (Zeilen 43–79+): pro Anfrage Auflösung in der Reihenfolge
  1. Admin-Wert `Setups.DiscoveryPublicBaseUrl` („Öffentliche Basis-URL", `Einrichtung` → `Allgemein`, wirkt ohne Neustart; Lesefehler → fail-open mit Warning),
  2. `Discovery:PublicBaseUrl` aus der Konfiguration,
  3. automatische Ableitung durch `DiscoveryResponseBuilder` aus gebundenen Adressen, `Kestrel:Endpoints`, `Host:Address`/`Host:Port` und primärer LAN-IPv4; Loopback- (`localhost`, `127.*`, `::1`) und Wildcard-Adressen werden nie gemeldet.
  Validierung: `Discovery:PublicBaseUrl` muss eine absolute `http`-/`https`-URL sein, sonst schlägt der Start fehl (`ValidateOnStart`, dokumentiert in `docs/help/netzwerk-erkennung/troubleshooting.md`).
- `MdnsServiceProfileBuilder` (intern, rein ableitend):
  - TXT-Records **fest verdrahtet** `path=/` und `app=VideoWebPlayer` (Zeilen 38–42) — kein Schema und kein Pfad-Anteil konfigurierbar; bei `PathBase`-Betrieb bleibt `path=/` falsch.
  - Port-Auflösung (Zeilen 46–69): `Mdns:Port` (1–65535) → gebundene Adressen → `Kestrel:Endpoints:Http:Url` → `Host:Port` → Fallback `5000`.
- `MdnsAdvertiserWorker`:
  - Zweistufige Konjunktion (Zeilen 66–67): `Mdns:Enabled` (Betreiber) **und** `Setup.MdnsAdvertisementEnabled` (Admin-Schalter). `Mdns:Enabled=false` ist der Master-Switch — der Admin kann ihn nicht überstimmen (Zeilen 82–87).
  - Admin-Schalter wird alle 60 s neu gelesen (`ReEvaluationInterval`, Zeile 18) — wirkt ohne Neustart.
  - Diensttyp `_videowebplayer._tcp.local.` (Default `Mdns:ServiceType`), UDP-Multicast 5353.

## AutoUpdate — `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs`

- Fixer systemd-Unit-Name der Installations-Unit im Code: `VideoWebPlayer-AutoUpdate` (Zeile 15).
- Registrierung `builder.UseAutoUpdate(...)` aus `msTools.Updater` mit GitHub-Quelle `martin-stromberg/VideoPlayer` (`VideoWebPlayerUpdateSourceFactory`, Zeilen 29–35).
- Update-Mechanik unter Linux laut `docs/TECH_Auto_Update.md` (Zeilen 117–129): Installationsskript als transiente systemd-Unit via `systemd-run` (ohne `--wait`), `StopHostAfterScriptStart`, `update.failed`-Marker, Log in `Updates/update.log`. Der `Merge` der `ProtectedFiles` benötigt `python3` (Zeile 158–160); `ServiceName` muss zum Neustart-Unit-Namen passen (`docs/help/updates.md` Zeile 70 — „Dienstname für Neustart" ist ein Admin-Feld, persistiert in `UpdateSettings.ServiceName`, zusätzlich als `AutoUpdate:ServiceName` in `JsonKeys`).

## Konfigurations-Defaults

### `VideoWebPlayer/appsettings.json`

- `ConnectionStrings:DefaultConnection` = `Data Source=Data/WebVideoPlayer.db` (relativ zum Content-Root).
- `Backups:Path` = `Data/Backups` (manuelle/automatische Daten-Backups, Retention GVS 7/4/12).
- `Mdns`: `Enabled=true`, `InstanceName=VideoWebPlayer`, `ServiceType=_videowebplayer._tcp.local.`, `Port=null`.
- `Discovery:PublicBaseUrl` = `null`.
- `AutoUpdate`: `Enabled=true`, `EnableAutomaticDownload=true`, `EnableAutomaticInstallation=true`, `ScheduledInstallTime=02:00`, `AllowPrereleaseUpdates=false`, `StopHostAfterScriptStart=true`, `DownloadPath=Updates`, `SourceCheck:Interval=360`, `Backup:Path=Backups`, `RetainedBackupCount=5`, `CancelInstallationOnFailure=true`.
- `AutoUpdate:ProtectedFiles`: `web.config` (`Merge`, `XmlElements`/`XmlAttributes`), `appsettings*.json` (`Merge`, `JsonKeys` mit ~60 deployment-eigenen Schlüsseln, u. a. `Kestrel:Endpoints:Http:Url`, `Host:Address`/`Host:Port`, `Discovery:PublicBaseUrl`, `Mdns:*`, `AutoUpdate:*` inkl. `ServiceName`, `Jwt:*`, `Backups:*`). **`PathBase` ist nicht enthalten.**
- `Serilog`/`Logging`: Datei-Sink `Logs/web-.log`, Default-Level `Warning`.

### `VideoWebPlayer/appsettings.Production.json`

- `Kestrel:Endpoints:Http:Url` = `http://*:5002` (Wildcard-Bindung, direkt erreichbar).
- `Kestrel:Limits:MaxRequestBodySize` = `0` (unbegrenzt, für große Backup-Uploads).
- Serilog/Logging Default-Level `Information`.

### `VideoWebPlayer/appsettings.Development.json` / `appsettings.Testing.json`

- Development: `AutoUpdate:Enabled=false`, `EnableAutomaticInstallation=false`.
- Testing: `Mdns:Enabled=false`, `Kestrel` auf `http://127.0.0.1:0`.

### `VideoWebPlayer/VideoWebPlayer.csproj` und `web.config`

- `TargetFramework` `net10.0` (Zeile 3); `AspNetCoreHostingModel` `OutOfProcess` (Zeile 7).
- `msTools.Backup 1.1.0-RC.4` und `msTools.Updater 0.11.0-rc.6` als **`PackageReference`** aus der lokalen Quelle `lib/packages` (`NuGet.config`) — **nicht** mehr als Dateireferenz `lib/msTools.Updater/msTools.Updater.dll` (Abweichung: `docs/GUIDE_Installation.md` Zeile 16/232 und `docs/TECH_Auto_Update.md` Zeilen 10–12 beschreiben noch den alten DLL-Weg).
- MSBuild-Targets schreiben beim Publish `release-metadata.json` ins Publish-Verzeichnis (Zeilen 113–122) und kopieren `StandardSources.json` nach `DemoDataSets/` (Zeilen 93–106).
- `web.config` (OutOfProcess): `requestLimits maxAllowedContentLength=4294967295`, `aspNetCore processPath="dotnet"`.

## Laufzeit-Verzeichnisse (Schreibzugriff des Service-Accounts)

Relativ zum Content-Root/Anwendungsverzeichnis, im Code verifiziert:

| Verzeichnis | Zweck | Beleg |
|-------------|-------|-------|
| `Logs/` | Serilog-Datei-Sink; beim Start angelegt | `Program.cs` Zeile 23; `appsettings.json` `Serilog:WriteTo:File` |
| `Data/` | SQLite-DB `WebVideoPlayer.db` (+ `-wal`/`-shm`) | `appsettings.json` `ConnectionStrings:DefaultConnection`; `.gitignore` Zeile 374. `Data/` ist zugleich Quellcode-Verzeichnis (`VideoWebPlayer/Data/*.cs`) — im Publish-Output nur Laufzeitinhalt. |
| `Data/Backups/` | Daten-Backups (manuell, GVS, Update-Sicherung der Anwendungsdaten) | `appsettings.json` `Backups:Path`; `BackupSettings.StoragePath` Default `Data/Backups` (`VideoWebPlayer/Data/BackupSettings.cs` Zeile 16) |
| `Data/LiveStream/` | HLS-Segmente des TV-Senders | `LiveStreamSegmentStore.cs` Zeile 23 (`Path.Combine("Data","LiveStream")`, admin-übersteuerbar) |
| `Data/LiveStreamStandby/` | Cache des Standby-Clips | `LiveStreamStandbyProvider.cs` Zeile 85; `.gitignore` Zeile 398 |
| `Updates/` | Update-Pakete, Status-/Lock-Dateien, `update.log`, `backup/` (Sicherungen der `ProtectedFiles`) | `appsettings.json` `AutoUpdate:DownloadPath`; `docs/TECH_Auto_Update.md` Zeilen 120/133–139 |
| `Backups/` | Update-Sicherungen der Generation `ProgramUpdate` | `appsettings.json` `AutoUpdate:Backup:Path`; `.gitignore` Zeile 381 |
| `tools/ffmpeg/` | nachladbare ffmpeg/ffprobe-Binaries (Admin-Download) | `FfmpegToolDownloadService.cs` Zeilen 14–18, 160 |
| `wwwroot/images/genres/` | vom Betreiber ablegbare Genre-Icons — **nur lesend** genutzt | `SourceGenresController.cs` Zeile 185; `.gitignore` Zeile 383 |
| `%TEMP%` bzw. `$TMPDIR` | Temporäre Upload-Chunks der Backup-Uploads | `BackupUploadSessionService.cs` Zeilen 36/59 (`Path.GetTempPath()`) |

Zusätzlich schreibt ein Self-Update in das **Anwendungsverzeichnis selbst** (Paketkopiervorgang
durch das Installationsskript) — für AutoUpdate braucht der ausführende Kontext Schreibrecht
auf dem ganzen Anwendungsverzeichnis, nicht nur auf den Daten-Unterverzeichnissen.

## Publish-/Release-Artefakte

- `.github/actions/build-and-package/action.yml`: `dotnet publish VideoWebPlayer/VideoWebPlayer.csproj -c Release -r {win-x64|linux-x64} --self-contained true`, gepackt als `release-win-x64.zip` bzw. `release-linux-x64.zip`, dazu `update.json` (Update-Manifest). Self-contained ⇒ auf dem Zielsystem ist **keine** .NET-Runtime nötig (nur die vom self-contained Build benötigten OS-Bibliotheken).
- `release.yml` lädt beide ZIPs plus `update.json` in den GitHub-Release.
