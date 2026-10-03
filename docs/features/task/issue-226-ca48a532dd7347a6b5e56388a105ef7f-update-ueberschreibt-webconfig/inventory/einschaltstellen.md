# Einschaltstellen im Repository (Delta Runde 2)

Seit dem Runde-1-Commit `8aafb9f` hat sich am Produktivcode **nichts** geändert (einziger Folgecommit `b319a93` ist das Anforderungsdokument `docs/Anforderung_msTools_Updater_Dateierhalt.md`). Die Befunde aus `inventory/logic.md`, `inventory/models.md`, `inventory/interfaces.md`, `inventory/deployment.md` (Runde 1) gelten unverändert. Hier nur die für V1–V3 konkret relevanten Stellen mit verifizierten Zeilen.

## `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs` (41 Zeilen)

- `AddVideoWebPlayerAutoUpdate` (Z. 23–40): registriert `UpdateBackupOptions` (`Configure<>` auf `UpdateBackupOptions.SectionName` = `AutoUpdate:Backup`), `UpdateBackupCoordinator`, `UpdateSettingsInitializer`, dann `builder.UseAutoUpdate(cfg => …)` (Z. 29–35), danach `UpdateBackupEventBinder`.
- `UseAutoUpdate`-Lambda (Z. 30–34): liest `AutoUpdate:AllowPrereleaseUpdates` aus `builder.Configuration` und ruft `cfg.UseSource(sourceFactory.Create(allowPrereleaseUpdates)).WithUpdateUnitName("VideoWebPlayer-AutoUpdate")`.
- **Fluent-Ankerpunkt:** `cfg.PreserveFile(...)`/`cfg.MergeFile(...)` würden hier an die Kette gehängt. Achtung: Die Fluent-Einträge hätten den Nebeneffekt, eine eventuell gebundene `AutoUpdate:ProtectedFiles`-Liste **vollständig** zu ersetzen (siehe `bibliothek-0.11.md`, „Vorrang").
- **Konfigurationsbindung passiert implizit:** `UseAutoUpdate` bindet intern `builder.Configuration.GetSection("AutoUpdate")` — `AutoUpdate:ProtectedFiles` in `appsettings.json` greift ohne jede Codeänderung.

## `VideoWebPlayer/Program.cs` (57 Zeilen)

- Z. 30: `builder.AddVideoWebPlayerAutoUpdate()` — einziger Aufruf.
- `WebApplication.CreateBuilder(args)` (Z. 18) ohne eigene `AddJsonFile`-Quellen — keine `appsettings.Local.json`-Quelle (V3-Ausgangslage unverändert).

## `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs` (277 Zeilen)

- `ApplyToRuntimeOptions` (Z. 167–181): mutiert am `AutoUpdateOptions`-Singleton nur `Enabled`, `SourceCheck.Interval`/`TimeRanges`, `AllowPrereleaseUpdates`, `EnableAutomaticInstallation`, `EnableAutomaticDownload`, `ServiceName`, `Source`. **`ProtectedFiles` wird nicht berührt** → kein Konflikt mit der DB-gestützten Laufzeitkonfiguration; die Schutzliste ist rein dateibasiert (`appsettings`/Fluent) und nicht über die Admin-UI veränderbar.
- `CreateDefaults` (Z. 183–198) liest die `AutoUpdate:*`-Defaults für die DB-Settings — ohne `ProtectedFiles`-Bezug.
- `VideoWebPlayerUpdateSourceFactory.Create(bool)` (`Services/Updates/VideoWebPlayerUpdateSourceFactory.cs:28`) wird in `ApplyToRuntimeOptions` neu aufgerufen — betrifft nur `Source`, keine ProtectedFiles-Interaktion.

## `VideoWebPlayer/appsettings.json` (95 Zeilen)

- `AutoUpdate`-Abschnitt Z. 77–94: `Enabled`, `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `ScheduledInstallTime`, `AllowPrereleaseUpdates`, `StopHostAfterScriptStart`, `DownloadPath: "Updates"`, `SourceCheck.Interval`, `Backup.*`. → `ProtectedFiles`-Array würde hier als `AutoUpdate:ProtectedFiles` landen (Projektkonvention: Bindbares in der Konfiguration). `Backup` ist eine separate Optionsbindung (`UpdateBackupOptions.SectionName = "AutoUpdate:Backup"`) — kein Namenskonflikt mit `ProtectedFiles`.
- **Kandidaten für `JsonKeys`** (deployment-seitig angepasste Schlüssel, heute in Doku/Release Notes genannt):
  - `AutoUpdate:AppPoolName`, `AutoUpdate:SiteName` (IIS-Pflichtkonfiguration, `docs/help/updates.md:67–71`) — im Repo nicht gesetzt, werden deployment-seitig ergänzt.
  - `AutoUpdate:ServiceName` (Dienstname für Neustart, deployment-seitig; hat zusätzlich DB-Settings).
  - `AutoUpdate:GitHubToken` (`VideoWebPlayerUpdateSourceFactory.cs:13`).
  - `AutoUpdate:ScheduledInstallTime`, `AutoUpdate:DownloadPath` (in `appsettings.json` paketiert, deployment-seitig geändert plausible).
  - `ConnectionStrings:DefaultConnection` (Z. 2–4; abweichender DB-Pfad deployment-seitig üblich).
  - `Kestrel:*` (Z. 18–27 in `appsettings.Production.json`: `Limits:MaxRequestBodySize`, `Endpoints:Http:Url` — Port deployment-seitig üblich angepasst).
  - `Serilog:*`/`Logging:*` (Log-Level/Pfade deployment-seitig), `Host:Address`/`Host:Port` (Program.cs Z. 52), `Backups:Path` (Z. 24), `Playlists:*`, `EpisodeBackgroundImage:*`.
  - `Jwt:*` — steht im Repo **nicht** in `appsettings*.json` (Secrets via Env/`web.config`-`environmentVariables`); als `JsonKeys` trotzdem möglich, falls deployment-seitig in `appsettings*.json` gepflegt.
- `AutoUpdate:Enabled`/`EnableAutomaticInstallation`/`EnableAutomaticDownload`/`AllowPrereleaseUpdates`/`SourceCheck:Interval` werden über `UpdateSettings` (DB) zur Laufzeit übersteuert — deployment-seitige `appsettings`-Werte haben nur Initialisierungswirkung; ob sie in `JsonKeys` gehören, ist Planungsfrage.

## `VideoWebPlayer/appsettings.Production.json` / `appsettings.Development.json`

- Beide **im Paket** (Web-SDK-Default-`Content`, keine Ausschlüsse im csproj) → vom Wildcard `appsettings*.json` erfasst. Production enthält `Kestrel`/`Serilog`/`Logging`; Development `AutoUpdate.Enabled=false`/`EnableAutomaticInstallation=false` (Z. 16–19) — Deployment-Maschinen, die `appsettings.Production.json` anpassen, profitieren vom Merge.

## `VideoWebPlayer/web.config` (17 Zeilen)

Elemente/Attribute für die `XmlElements`/`XmlAttributes`-Liste:

| Paket-Inhalt | Deployment-seitige Ergänzung (schützenswert) |
|--------------|----------------------------------------------|
| `security/requestFiltering/requestLimits@maxAllowedContentLength="4294967295"` | `//security/ipSecurity` (IP-Filter) |
| `handlers/add@modules="AspNetCoreModuleV2"` | `//aspNetCore/environmentVariables` (Jwt:* etc.) |
| `aspNetCore@processPath`/`arguments`/`hostingModel` (Publish-Transform — **nicht** übernehmen) | `//httpProtocol/customHeaders` |
| `aspNetCore@requestTimeout="02:00:00"`, `stdoutLogEnabled="false"`, `stdoutLogFile=".\logs\stdout"` | geänderte `aspNetCore`-Attribute → `//aspNetCore@requestTimeout`, `@stdoutLogEnabled`, `@stdoutLogFile` (Kandidaten) |
| Kein `<environmentVariables>` im Repo | `security`-Unterelemente neben `requestFiltering` |

Hinweis: `//security/ipSecurity` und `//httpProtocol/customHeaders` sitzen unter `system.webServer`; der PowerShell-Eltern-Fallback (`XmlElement` fehlt im Neuen → an Eltern-XPath anhängen) funktioniert nur, wenn der Elternknoten existiert — `//security` existiert im Paket (requestFiltering), `//httpProtocol` **nicht** → ein Merge von `//httpProtocol/customHeaders` würde still übersprungen, wenn deployment-seitig `httpProtocol` angelegt wurde. Alternativ `//httpProtocol` selbst als `XmlElements`-Eintrag → Eltern = `//system.webServer`? Der Fallback-XPath entsteht durch Streifen des letzten Segments (`//httpProtocol` → `//`), `SelectSingleNode("//")` liefert keinen sinnvollen Knoten → prüfen/Planungsrisiko. Sicherer: vollständige Pfade wie `//system.webServer/httpProtocol` als Merge-Ziel (Eltern `//system.webServer` existiert).

## Paketreferenzen und Paketablage

- `VideoWebPlayer/VideoWebPlayer.csproj:53` — `<PackageReference Include="msTools.Updater" Version="0.10.4-rc.1" />`.
- `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj:22` — identische Referenz (Tests nutzen `msTools.Updater`-Typen direkt, z. B. `MsToolsUpdaterIntegrationTests`).
- `lib/packages/`: `msTools.Updater.0.10.4-rc.1.nupkg` (tracked, zu entfernen) **und** `msTools.Updater.0.11.0-rc.1.nupkg` (untracked, neu) liegen nebeneinander; `msTools.Backup.1.1.0-RC.4.nupkg` unverändert.
- `NuGet.config` (Z. 6): lokale Quelle `lib/packages` — keine Änderung nötig; `dotnet restore` in allen Workflows (`staging-ci.yml:77/112`, `pr-staging-ci.yml:81/130`, `release.yml:77`, `security-scan.yml:35`) greift sie automatisch mit.
- `.github/actions/build-and-package/action.yml`: kein eigener Restore-/NuGet-Schritt, keine versionharte `msTools.Updater`-Verdrahtung (Kommentare Z. 48/53/89 beziehen sich auf `release-metadata.json`/`update.json`-Schema). Zip-Schritt packt unverändert ohne Ausschlüsse — `ProtectedFiles` ersetzt **keinen** Paketausschluss, sondern schützt beim Kopieren.

## `.gitignore` / Projektdateien (V3)

- Kein Eintrag für `appsettings.Local.json` (grep: nur generische `*.local.*`-Muster für andere Tools) — bei V3-Umsetzung zu ergänzen.
- `VideoWebPlayer.csproj` hat keine `appsettings*.json`-`Content`-Metadaten — für `CopyToPublishDirectory="Never"` müsste ein explizites `Content`-Item ergänzt werden.

## Erkenntnis zu „nur lokal vorhandenen Dateien" (V3)

Das Skript matcht Wildcards gegen das **Anwendungsverzeichnis**: Eine nur deployment-seitig existierende `appsettings.Local.json` wird vom `appsettings*.json`-Eintrag gesichert und unverändert belassen (no-op), weil das Paket sie ohnehin nicht überschreibt — sie ist **heute schon update-sicher**, solange sie nie ins Paket gelangt (`CopyToPublishDirectory="Never"`). Ihr Mehrwert läge allein darin, beliebige Override-Schlüssel ohne `JsonKeys`-Pflege zu erlauben.
