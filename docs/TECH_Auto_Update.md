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

## Admin-Oberfläche

Administratoren verwalten Updates unter `/admin/updates`. Die Seite ist wie die übrigen Adminseiten über den
Claim `IsAdmin=True` sichtbar und die serverseitigen Aktionsendpunkte sind zusätzlich mit der Policy
`AdminOnly` geschützt.

Die Oberfläche zeigt den aktuellen `msTools.Updater`-Status mit installierter und verfügbarer Version, letzter
Prüfung, Download-/Installationsdetails, Sperrinformationen und Fehlern. Die Buttons lösen eine sofortige
Prüfung oder eine Installation der bekannten neuen Version aus. Während laufender Aktionen oder bei aktivem
Updater-Lock werden manuelle Aktionen server- und clientseitig blockiert.

Beim Aktivieren von Prerelease-Versionen muss die Sicherheitsabfrage in der Seite bestätigt werden. Ohne diese
Bestätigung wird die Einstellung nicht gespeichert.

## Persistente Update-Einstellungen

Die Tabelle `UpdateSettings` speichert die administrativ änderbaren Updatewerte. `appsettings.json` liefert nur
Initialwerte, solange noch keine DB-Zeile existiert. Änderungen aus der UI werden unmittelbar in die
runtime-mutierbaren `AutoUpdateOptions` übertragen:

- automatische Prüfung und Prüfintervall,
- Prerelease-Akzeptanz,
- automatische Installation und automatischer Download,
- Dienstname für den Neustart,
- Backup vor Installation, Abbruch bei Backupfehler und Update-Backup-Aufbewahrung.

Die EF-Migration `AddUpdateSettings` liegt regulär unter `VideoWebPlayer/Migrations/`. Die neue Programmversion
wendet sie beim Start über `app.MigrateDatabase()` an.

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
| `Backup.RetainedBackupCount` | Anzahl der aufbewahrten Sicherungen der Generation `ProgramUpdate` in der bestehenden Backup-Infrastruktur. |
| `Backup.CancelInstallationOnFailure` | Bricht die Installation ab, wenn die Sicherung fehlschlägt oder kein Backup-Dienst registriert ist. |

Zusätzlich unterstützt die Bibliothek u. a. `ServiceName`, `ExecutablePath`, `ScheduledInstallTime`,
`StopHostAfterScriptStart` und `MaxAssetBytes` – siehe Updater-README.

## Sicherung vor der Installation

`UpdateBackupEventBinder` abonniert das Pre-Install-Event des Updaters
(`IAutoUpdateEventAggregator.BeforeInstall`) und lässt über `UpdateBackupCoordinator` eine Sicherung erstellen:

1. Ist `Backup.Enabled` false, wird die Installation ohne Sicherung fortgesetzt.
2. Andernfalls wird ein optional registrierter `IUpdateBackupService` aufgelöst und mit dem konfigurierten
   Zielpfad aufgerufen. Provider, die diesen Pfad selbst beschreiben, legen das Zielverzeichnis eigenständig an;
   der Standardadapter nutzt die bestehende `msTools.Backup`-Konfiguration.
3. Die Retention erfolgt durch die verwendete Backup-Infrastruktur. Der Coordinator löscht keine Dateien pauschal
   im konfigurierten Zielverzeichnis.
4. Schlägt die Sicherung fehl (oder ist kein `IUpdateBackupService` registriert), wird die Installation bei
   `CancelInstallationOnFailure` abgebrochen (`args.Cancel = true`).

`VideoWebPlayerUpdateBackupService` ist als `IUpdateBackupService` registriert und nutzt dieselbe
`msTools.Backup.IBackupService`-Infrastruktur wie das manuelle Web-Backup. Das Backup wird mit der Generation
`ProgramUpdate` erstellt, in der Backup-Historie als `ProgramUpdateBackup` protokolliert und anschließend über
die bestehende Backup-Retention bereinigt. `RetainedUpdateBackupCount` aus den Update-Einstellungen wird dabei
auf `BackupRetentionOptions.ProgramUpdateCount` gemappt; `Manual`- und Upload-Backups bleiben davon unberührt.
Schlägt das Backup fehl, bricht die Installation bei aktivierter
Option `CancelInstallationOnFailure` ab und der Fehler wird im Updater-Status sichtbar.

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
