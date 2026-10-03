# Deployment-Pipeline und Update-Mechanik

Untersucht: Wie `release-win-x64.zip` entsteht, welche Dateien es enthält, und was das von `msTools.Updater` generierte Installationsskript beim Update macht. Erkenntnisse zur Bibliothek stammen aus der Decompilierung von `lib/packages/msTools.Updater.0.10.4-rc.1.nupkg` (`lib/net10.0/msTools.Updater.dll`, `ilspycmd`) sowie der mitgelieferten XML-Doku.

## Release-Paket-Bau: `.github/actions/build-and-package/action.yml`

Composite-Action, verwendet vom `prerelease`-Job in `staging-ci.yml` und vom `release`-Job in `release.yml`. Schritte:

| Schritt | Zeilen | Inhalt |
|---------|--------|--------|
| Setup .NET 10 | 23–26 | `actions/setup-dotnet@v6`, `10.0.x` |
| Publish (win-x64) | 28–30 | `dotnet publish VideoWebPlayer/VideoWebPlayer.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64 -p:Version=… -p:NoWarn=NU1903` |
| Publish (linux-x64) | 32–34 | analog `-o publish/linux-x64` |
| Verify publish output | 36–45 | prüft nur, dass beide Verzeichnisse nicht leer sind |
| Write release metadata | 47–74 | schreibt `release-metadata.json` per `jq` in beide Publish-Verzeichnisse (überschreibt die vom csproj-Target `GenerateReleaseMetadata` erzeugte Variante mit `commitSha`/`repository`) |
| Create release ZIPs | 76–85 | `(cd publish/win-x64 && zip -r ../../release-win-x64.zip .)` bzw. `linux-x64` — **packt das gesamte Publish-Verzeichnis rekursiv, ohne Ausschlüsse** |
| Create update manifest | 87–142 | erzeugt `update.json` (Rezepte-Format: `assets[]` mit `platform`, `runtimeIdentifier`, `assetName`, `assetUrl`, `sha256`, `sizeBytes`) |

**Befund:** `web.config`, `appsettings.json`, `appsettings.Production.json` liegen im Publish-Output und landen damit unverändert im Zip. Es existiert weder ein `-x`-Ausschluss beim `zip`-Schritt noch ein Template-/Rename-Mechanismus.

## `VideoWebPlayer/web.config` (17 Zeilen)

Datei: `VideoWebPlayer/web.config` — wurde mit Commit `46423ad` („fix: ship web.config with raised requestFiltering limit for IIS uploads", 2026-09-22) eingeführt.

| Element | Inhalt |
|---------|--------|
| `system.webServer/security/requestFiltering/requestLimits` | `maxAllowedContentLength="4294967295"` (Kommentar: IIS-Default ~30 MB würde 128-MiB-Upload-Chunks ablehnen; greift auch im OutOfProcess-Modus) |
| `system.webServer/handlers` | `AspNetCoreModuleV2`, `path="*" verb="*"` |
| `system.webServer/aspNetCore` | `processPath="dotnet"`, `arguments=".\VideoWebPlayer.dll"`, `stdoutLogEnabled="false"`, `hostingModel="outofprocess"`, `requestTimeout="02:00:00"` (Kommentar: `processPath`/`arguments`/`hostingModel` werden durch die Publish-Transformation gesetzt) |

Die Repository-Version enthält **kein** `<environmentVariables>`-Element — dieses wird deployment-seitig in der IIS-Site ergänzt und ist der verloren gehende Bestandteil.

## `VideoWebPlayer/VideoWebPlayer.csproj` — Publish-relevante Metadaten

- Zeile 7: `<AspNetCoreHostingModel>OutOfProcess</AspNetCoreHostingModel>` → Publish-Transformation erzeugt `hostingModel="outofprocess"`.
- Zeile 53: `<PackageReference Include="msTools.Updater" Version="0.10.4-rc.1" />` — lokale Quelle `lib/packages` (`NuGet.config`).
- Zeilen 80–93: Target `CopyStandardSourcesDemoData` (AfterTargets `Build;Publish`) — kopiert `StandardSources.json` in `DemoDataSets\` des Output/Publish-Verzeichnisses.
- Zeilen 100–109: Target `GenerateReleaseMetadata` — schreibt `release-metadata.json` ins Publish-Verzeichnis (mit leerem `commitSha`/`repository`; wird in CI überschrieben).
- **Keine** `web.config`- oder `appsettings`-spezifischen `Content`-/`CopyToPublishDirectory`-Metadaten vorhanden; es gelten die Web-SDK-Defaults (alle `appsettings*.json` und die transformierte `web.config` werden publiziert).
- `<InternalsVisibleTo Include="VideoWebPlayer.Tests" />` (Zeile 15) — macht das implizit generierte `Program` für `WebApplicationFactory<Program>`-Tests sichtbar.

## `msTools.Updater` 0.10.4-rc.1 — Einbindung und interne Mechanik

- **Einbindung:** NuGet-Paket `lib/packages/msTools.Updater.0.10.4-rc.1.nupkg`, Quelle `local` in `NuGet.config`. nuspec-Repository: `github.com/martin-stromberg/msTools.Updater`, Commit `680cc7ec…`. Lizenz MIT. (Hinweis: `docs/TECH_Auto_Update.md` beschreibt noch den älteren DLL-Pfad `lib/msTools.Updater/` — veraltet.)
- **Registrierung:** `builder.UseAutoUpdate(cfg => …)` in `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs:29-35` (aufgerufen aus `Program.cs:30`).

### DI-Registrierung in `UseAutoUpdate` (decompiliert)

Alle Updater-Verträge werden mit `TryAddSingleton` registriert — u. a. `IAutoUpdateScriptGenerator` → `AutoUpdateScriptGenerator`, `IAutoUpdateInstaller` → `AutoUpdateInstaller`, `IAutoUpdateEventAggregator` → `AutoUpdateEvents`, `IAutoUpdateServiceResolver` → `AutoUpdateServiceResolver`, `IAutoUpdatePackageStore` → `FileSystemAutoUpdatePackageStore`. `AutoUpdateOptions` wird als Singleton-Instanz registriert (`AddSingleton(instance)`), die Hosted Services (`AutoUpdateWorkspaceInitializationService`, `AutoUpdateCheckerService`, `AutoUpdateSchedulerService`) nur bei `HostedServicesEnabled`.

**Konsequenz (verifiziert, keine Annahme):** Eine `IAutoUpdateScriptGenerator`-Registrierung der Anwendung, die **vor** `builder.UseAutoUpdate(...)` erfolgt, gewinnt — `TryAddSingleton` überschreibt vorhandene Registrierungen nicht. `AutoUpdateScriptGenerator` selbst ist `sealed`, kann also nicht abgeleitet werden.

### Generiertes Installationsskript (`AutoUpdateScriptGenerator`)

`GenerateAsync` wählt nach Plattform (`IAutoUpdatePlatformResolver.CurrentPlatform`): Windows → `.ps1` unter `IAutoUpdatePackageStore.ScriptPath("ps1")` = `Updates/pending/update.ps1` (bei `AutoUpdate:DownloadPath = "Updates"`); Linux → `update.sh`; andere Plattformen → `InvalidOperationException`.

Windows erzeugt eine von zwei Varianten:
- **IIS-App-Pool** (`GenerateWindowsAppPoolAsync`, wenn `target.AppPoolName` gesetzt): `WebAdministration`-Modul, `Stop-WebAppPool`, bis zu 60 s auf w3wp-Exit warten, dann Dateiwechsel, `Start-WebAppPool`.
- **Dienst/Executable** (`GenerateWindowsServiceOrExecutableAsync`): `Stop-Service`/`Start-Service` bzw. `Start-Process`.

**Beide Varianten wechseln Dateien identisch und ohne Ausnahmen:**

```powershell
Expand-Archive -LiteralPath $zip -DestinationPath $staging -Force
Get-ChildItem -LiteralPath $staging -Force | Copy-Item -Destination $app -Recurse -Force
```

Kein Ausschluss, keine Sicherung vorhandener Dateien, kein XML-Merge — `$app` ist `IAutoUpdateEnvironment.ApplicationDirectory` (Content-Root). Linux analog: `unzip -o "$zip" -d "$staging"` + `cp -fr "$staging"/. "$app"/`.

Weitere Befunde zum Skript:
- Das Windows-Skript schreibt **kein** `update.log` (nur `Write-Host`/`Write-Warning`); `update.log`-Logging existiert nur im Linux-Skript (`write_log`/`capture`, `IAutoUpdatePackageStore.LogPath` = `Updates/update.log`). Der Generator loggt den Skriptinhalt zusätzlich per `ILogger` (`LogDebug`).
- `LogScriptGenerated` schreibt Skriptinhalt und -pfad ins Anwendungslog.

### Installationsziel-Auflösung (`AutoUpdateServiceResolver`)

Reihenfolge Windows: `AutoUpdateOptions.AppPoolName` → `ServiceName` → `ExecutablePath` → Probe (`FindWindowsServicesForCurrentProcess`). Der Windows-Probe gleicht nur Windows-**Dienste** per `sc.exe queryex` + PID ab — IIS-App-Pools werden **nicht** erkannt. Für ein IIS-Deployment muss `AutoUpdate:AppPoolName` konfiguriert sein (per `AutoUpdate`-Konfigurationsabschnitt oder `AutoUpdateBuilder.WithIisApplicationPool(appPool, site)`), sonst schlägt `Resolve` mit „Configure AppPoolName, ServiceName or ExecutablePath…" fehl. Im VideoWebPlayer-Code wird `WithIisApplicationPool` nicht aufgerufen; `appsettings.json` enthält kein `AutoUpdate:AppPoolName`; `UpdateSettingsService.ApplyToRuntimeOptions` setzt nur `ServiceName` (DB-Einstellung).

### `AutoUpdateInstaller` (decompiliert)

Löst `IAutoUpdateScriptGenerator` per Konstruktor-Injektion auf: `InstallAsync` → `serviceResolver.Resolve()` → `_scriptGenerator.GenerateAsync(package, zipPath, target, ct)` → `_processRunner.StartScript(scriptPath, zipPath)`.

### `AutoUpdateOptions` — relevante Eigenschaften (kein Dateierhalt!)

`Enabled`, `EnableAutomaticDownload`, `DownloadPath` (Default `"updates"`), `EnableAutomaticInstallation`, `AllowPrereleaseUpdates`, `Source`, `SourceCheck`, `MaxAssetBytes`, `HostedServicesEnabled`, `ScheduledInstallTime`, `ServiceName`, `ExecutablePath`, **`AppPoolName`**, **`SiteName`**, `WorkspaceUser`/`WorkspaceGroup` (Linux), `StopHostAfterScriptStart`, `HealthTimeoutSeconds`, `UpdateUnitName`. **Es existiert keine Option zum Schutz/Ausschluss einzelner Dateien beim Entpacken.**

### `FileSystemAutoUpdatePackageStore` — Pfade

`RootDirectory` = `DownloadPath` (relativ → Content-Root), `PendingDirectory` = `Updates/pending`, `StagingDirectory` = `Updates/staging`, `LockPath` = `Updates/update.lock`, `LogPath` = `Updates/update.log`, `DescriptorPath` = `Updates/pending/.descriptor.json`, `ScriptPath(ext)` = `Updates/pending/update.{ext}`.

### `AutoUpdateBuilder` — Fluent-API (Auszug)

`UseSource`, `UseGithubSource`, `UseLocalFolderSource`, `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `EnablePrereleaseUpdates`, `WithSourceCheck`, `WithUpdateUnitName`, **`WithIisApplicationPool`**, `WithDownloadPath`, `BindConfiguration`, `DisableHostedServices`. Explizit gesetzte Werte werden nach dem Konfigurations-Binding erneut angewendet (`ReapplyExplicitValues`).

## Betroffene Dateien im Überblick

| Datei | Rolle | Im Paket | Beim Update |
|-------|-------|----------|-------------|
| `web.config` | ANCM/`requestFiltering`/deployment-seitige `environmentVariables` | ja (Publish-Transform) | wird ersetzt |
| `appsettings.json` | Basiskonfiguration inkl. `AutoUpdate`-Abschnitt | ja | wird ersetzt |
| `appsettings.Production.json` | Kestrel-Limits/Endpoints, Serilog | ja | wird ersetzt |
| `release-metadata.json` | installierte Version für `ReleaseMetadataInstalledVersionProvider` | ja | wird ersetzt (gewollt) |
| `Updates/` (pending/staging/lock/log/status) | Updater-Arbeitsverzeichnis | nein | Arbeitsdaten des Updaters |
| `Data/`, `Logs/`, `Backups/` | Laufzeitdaten | nein (werden zur Laufzeit erzeugt) | bleiben, sofern nicht im Zip |
