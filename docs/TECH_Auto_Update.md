# Automatisierte Programmupdates

Der VideoWebPlayer aktualisiert sich mit der Bibliothek
[msTools.Updater](https://github.com/martin-stromberg/msTools.Updater) selbstständig auf den neuesten
GitHub-Release-Stand. Vor der Installation einer neuen Version wird ein vollständiger Datenexport als
Sicherung angefordert.

## Einbindung der Bibliothek

`msTools.Updater` ist nicht auf NuGet veröffentlicht. Die Assembly aus dem `release.zip` des Updater-Repositories
liegt daher unter `lib/msTools.Updater/` und wird von `VideoWebPlayer.csproj` und `VideoWebPlayer.Tests.csproj`
als Datei-Referenz eingebunden.

Aktualisieren auf eine neue Updater-Version:

```bash
gh release download <tag> --repo martin-stromberg/msTools.Updater --pattern release.zip
unzip -o release.zip -d /tmp/updater
cp /tmp/updater/msTools.Updater.dll /tmp/updater/msTools.Updater.xml lib/msTools.Updater/
```

Registriert wird der Updater über `builder.AddVideoWebPlayerAutoUpdate()`
(`VideoWebPlayer/Extensions/AutoUpdateExtensions.cs`). Im Code steht nur, was nicht aus der Konfiguration
gebunden werden kann: die GitHub-Quelle (`martin-stromberg/VideoPlayer`) und der systemd-Unit-Name
`VideoWebPlayer-AutoUpdate`.

## Konfiguration (appsettings.json)

```json
{
  "AutoUpdate": {
    "Enabled": true,
    "EnableAutomaticDownload": true,
    "EnableAutomaticInstallation": true,
    "AllowPrereleaseUpdates": false,
    "DownloadPath": "Updates",
    "SourceCheck": { "Interval": 360 },
    "Backup": {
      "Enabled": true,
      "Path": "Backups",
      "RetainedBackupCount": 5,
      "CancelInstallationOnFailure": true
    }
  }
}
```

| Schlüssel | Bedeutung |
|-----------|-----------|
| `Enabled` | Schaltet das Update-System komplett ein/aus. In `appsettings.Development.json` deaktiviert. |
| `EnableAutomaticDownload` | Lädt ein gefundenes Update automatisch herunter. |
| `EnableAutomaticInstallation` | Installiert ein heruntergeladenes Update automatisch (Neustart der Anwendung). |
| `AllowPrereleaseUpdates` | Berücksichtigt GitHub-Pre-Releases (z. B. RC-Builds aus `staging`). |
| `DownloadPath` | Ablage für Update-Pakete, Status- und Lock-Dateien (relativ zum Content-Root). |
| `SourceCheck.Interval` | Prüfintervall in Minuten; optional zusätzlich `SourceCheck.TimeRanges`. |
| `Backup.Path` | Ablageort der Sicherungen (relativ zum Content-Root oder absoluter Pfad). |
| `Backup.RetainedBackupCount` | Anzahl der aufbewahrten Sicherungen; ältere werden nach einer neuen Sicherung gelöscht (`0` = alle behalten). |
| `Backup.CancelInstallationOnFailure` | Bricht die Installation ab, wenn die Sicherung fehlschlägt oder kein Backup-Dienst registriert ist. |

Zusätzlich unterstützt die Bibliothek u. a. `ServiceName`, `ExecutablePath`, `ScheduledInstallTime`,
`StopHostAfterScriptStart` und `MaxAssetBytes` – siehe Updater-README.

## Sicherung vor der Installation

`UpdateBackupEventBinder` abonniert das Pre-Install-Event des Updaters
(`IAutoUpdateEventAggregator.BeforeInstall`) und lässt über `UpdateBackupCoordinator` eine Sicherung erstellen:

1. Ist `Backup.Enabled` false, wird die Installation ohne Sicherung fortgesetzt.
2. Andernfalls wird ein optional registrierter `IUpdateBackupService` aufgelöst, das Zielverzeichnis erzeugt und
   der Export angefordert.
3. Nach einer erfolgreichen Sicherung werden die ältesten Dateien im Zielverzeichnis gelöscht, bis nur noch
   `RetainedBackupCount` Dateien vorhanden sind.
4. Schlägt die Sicherung fehl (oder ist kein `IUpdateBackupService` registriert), wird die Installation bei
   `CancelInstallationOnFailure` abgebrochen (`args.Cancel = true`).

`IUpdateBackupService` ist der Anschlusspunkt für die Backup-Funktionalität aus Issue #77
(`msTools.Backup`). Solange keine Implementierung registriert ist, protokolliert die Anwendung eine Warnung und
installiert – bei Standardkonfiguration – kein Update.

```csharp
builder.Services.AddScoped<IUpdateBackupService, MyBackupService>();
```

## Release-Artefakte

Damit der GitHub-Quelle ein Update erkennbar ist, erzeugt `.github/workflows/main-release.yml` zwei zusätzliche
Artefakte:

- `release-metadata.json` – liegt in jedem Release-Archiv und beschreibt die installierte Version
  (`version`, `publishedAt`, `commitSha`, `repository`, `runtimeIdentifier`). Ohne diese Datei kann der Updater
  die installierte Version nicht ermitteln und lädt kein Update.
- `update.json` – Release-Manifest-Asset, erzeugt von `.github/scripts/create-update-manifest.sh`, mit
  Plattform, Runtime-Identifier, Asset-URL, SHA256 und Größe der beiden Release-Archive.

Der Updater unterstützt Windows (Dienst oder ausführbare Datei) und Linux (systemd); macOS ist nicht
unterstützt.
