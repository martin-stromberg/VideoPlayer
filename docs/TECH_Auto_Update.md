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
| `AppPoolName` | Name des IIS-Anwendungspools — wählt die **privilegierte** IIS-Variante der Update-Installation (`WebAdministration`, erfordert erhöhte Rechte für die Pool-Identität). Nur setzen, wenn das bewusst gewollt ist; ohne `AppPoolName` läuft unter IIS automatisch der nicht privilegierte Pfad (siehe unten). Alternativ per Fluent-API `WithIisApplicationPool(appPool, site)`. |
| `SiteName` | Name der IIS-Site (optional, ergänzt `AppPoolName`). |
| `ProtectedFiles` | Schutzliste für deployment-seitig angepasste Dateien. Array von Einträgen mit `Path` (relativ zum Anwendungsverzeichnis, Wildcards `*`/`?` erlaubt), `Strategy` (`Preserve` = Bestandsdatei komplett zurückschreiben, `Merge` = nur konfigurierte Bereiche übernehmen) sowie den `Merge`-Regeln `XmlElements` (XPath-Pfade), `XmlAttributes` (`{element-xpath}@{attribut}`) und `JsonKeys` (`:`-getrennte Schlüsselpfade). Wird mit `Merge`-Einträgen für `web.config` und `appsettings*.json` ausgeliefert. Vorrang: Explizite Fluent-Einträge (`PreserveFile`/`MergeFile`/`ProtectFile`) ersetzen die gesamte gebundene Liste (all-or-nothing) — der VideoWebPlayer nutzt sie bewusst nicht. Deployment-seitige Ergänzungen der Liste sind über Umgebungsvariablen möglich (`AutoUpdate__ProtectedFiles__{n}__Path` usw.), da `appsettings.json`-Änderungen an der Liste selbst kein Update überstehen. |

Zusätzlich unterstützt die Bibliothek u. a. `ServiceName`, `ExecutablePath`, `ScheduledInstallTime`,
`StopHostAfterScriptStart` und `MaxAssetBytes` – siehe Updater-README. `ServiceName` und
`ExecutablePath` werden unter IIS ignoriert (mit Warnung im Anwendungslog), weil sie dort
wirkungslos bzw. schädlich sind; im VideoWebPlayer ist das Dienstname-Feld unter IIS ohnehin
gesperrt und ein persistierter Wert wird bereinigt.

**Update-Installation unter IIS:** Erkennt der Updater IIS-Hosting anhand der
ANCM-Umgebungsvariablen (`ASPNETCORE_PORT`/`ASPNETCORE_TOKEN` für Out-of-Process,
`ASPNETCORE_IIS_*` für In-Process), generiert er ein Skript, das ohne erhöhte Rechte auskommt:
`app_offline.htm` legt die Site kontrolliert still (Requests erhalten den Offline-Content statt
Verbindungsfehler), das Skript wartet auf das Ende des Backend-Prozesses bzw. terminiert bei
In-Process den `w3wp` des eigenen Pools, tauscht die Dateien (inkl. `ProtectedFiles`-Ablauf) und
entfernt `app_offline.htm` — ANCM/WAS startet die neue Version beim nächsten Request. Voraussetzung
bei In-Process: ein dedizierter App-Pool je Anwendung, weil der `w3wp`-Abbruch alle Sites des Pools
mitnimmt. `app_offline.htm` wird auch auf Fehlerpfaden entfernt. Das Installationsskript läuft
unter Windows grundsätzlich als entkoppelter Prozess (WMI `Win32_Process.Create`), der den Stopp
des Hosts überlebt; schlägt der entkoppelte Start fehl, meldet die Installation
`InstallationFailed` statt still zu scheitern. Diagnose: Alle Windows-Skripte schreiben jeden
Schritt nach `Updates/update.log`; nach einem Fehlschlag liegt zusätzlich eine Archivkopie des
ausgeführten Skripts als `update-failed.ps1` vor.

**Dateierhalt bei Updates:** Die Schutzliste `AutoUpdate:ProtectedFiles` steuert, welche deployment-seitig
angepassten Dateien eine Update-Installation überstehen. Das generierte Installationsskript arbeitet strikt
in der Reihenfolge *Sicherung → Paketkopiervorgang → Merge/Restore*: Jede Trefferdatei wird vor dem
Kopiervorgang nach `Updates/backup/<relativer Pfad>` gesichert; danach wird entweder die gesamte Bestandsdatei
über die Paketdatei zurückkopiert (`Preserve`) oder es werden nur die konfigurierten Bereiche aus der
Bestandsdatei in die Paketdatei übernommen (`Merge` per `XmlElements`/`XmlAttributes`/`JsonKeys`). Fehler im
Backup- oder Merge-Schritt brechen die Installation nicht ab (`Write-Warning` unter Windows, `update.log`-
Eintrag unter Linux); die Sicherungen bleiben zur manuellen Wiederherstellung unter `Updates/backup/` liegen
und werden nicht automatisch aufgeräumt (keine Retention). Deployment-seitig zusätzlich angelegte Dateien,
die nicht im Paket enthalten sind, bleiben unverändert erhalten. Randbedingungen:

- **Migrationslücke:** Das Installationsskript erzeugt die laufende (alte) Version — der Schutz greift erst
  ab dem ersten Update **nach** der Version, die diese Liste ausliefert. Bestandsinstallationen müssen
  deployment-seitige `web.config`-/`appsettings`-Anpassungen einmalig nach dem Update auf diese Version
  nachtragen.
- **Elternknoten-Abhängigkeit:** Fehlt ein `XmlElements`-Ziel in der Paketdatei, wird es an den Elternknoten
  angehängt — existiert auch der Elternpfad nicht, wird der Eintrag still übersprungen. Deshalb schützt die
  ausgelieferte Liste `//system.webServer/httpProtocol` (Eltern `//system.webServer` existiert immer) und
  `//security/ipSecurity` (Eltern `//security` via `requestFiltering`). Unter Linux unterstützt der XML-Merge
  nur die ElementTree-XPath-Teilmenge (keine Attributprädikate wie `//add[@name='x']`).
- **`JsonKeys` friert aufgezählte Werte ein:** Jeder `JsonKeys`-Eintrag übernimmt den Bestandswert bei jedem
  Update — Paket-Verbesserungen am selben Schlüssel werden dauerhaft blockiert. Die ausgelieferte Liste ist
  deshalb auf deployment-eigene Schlüssel beschränkt; `AutoUpdate:ProtectedFiles` selbst ist bewusst nicht
  enthalten, damit künftige Releases Schutz nachliefern können.
- **JSON-Merge schreibt Dateien neu:** Formatierung kann sich ändern; JSON-Kommentare führen zu einem
  Parse-Fehler → pro-Datei-Warnung, Merge für diese Datei übersprungen (Update läuft weiter, die Bestandsdatei
  bleibt erhalten). Deployment-seitig angepasste `appsettings*.json` sollten kommentarfrei bleiben.
- **Linux benötigt `python3`:** Der `Merge` läuft unter Linux über ein eingebettetes Python-Skript; fehlt
  `python3`, wird der Merge mit `update.log`-Eintrag übersprungen (Update läuft weiter, Backups bleiben in
  `Updates/backup/`). `Preserve` und die Sicherungen laufen mit Bordmitteln.
- **Fail-fast bei ungültiger Liste:** Eine fehlerhafte `AutoUpdate:ProtectedFiles`-Konfiguration (leerer,
  rooted oder `..`-haltiger Pfad, `Merge` ohne Regel, fehlerhaftes `{xpath}@{attr}`-Format) verhindert den
  Anwendungsstart (`OptionsValidationException`) — bewusst Fail-fast statt still ignoriertem Schutzverlust.

### Lokale Konfigurationsdatei `appsettings.Local.json`

Zusätzlich zur Schutzliste registriert der VideoWebPlayer beim Start die optionale Konfigurationsquelle
`appsettings.Local.json` im Anwendungsverzeichnis (`optional`, `reloadOnChange`). Sie wird **nie paketiert**
(`CopyToPublishDirectory="Never"` in `VideoWebPlayer.csproj`, `.gitignore`-Eintrag) und übersteht Updates
daher vollständig — auch unabhängig von `ProtectedFiles`. Sie überlagert die Paket-`appsettings*.json`-Werte,
verliert aber gegen User Secrets, Umgebungsvariablen und die Kommandozeile. Sie ist der Auffang für
beliebige deployment-seitige Overrides außerhalb der geschlossenen `JsonKeys`-Liste und für deployment-seitige
Erweiterungen, die `appsettings.json` nicht dauerhaft tragen kann. Seiteneffekt: Der
`appsettings*.json`-Wildcard-Eintrag der Schutzliste erfasst eine deployment-seitig vorhandene
`appsettings.Local.json` ebenfalls — sie wird bei jedem Update nach `Updates/backup/` gesichert (evtl. darin
liegende Secrets landen damit auch im Backup-Verzeichnis) und vom JSON-Merge neu formatiert; Kommentare in der
Datei sind daher zu vermeiden.

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

Das `BeforeInstall`-Backup sichert ausschließlich die Anwendungsdatenbank — **keine**
Konfigurationsdateien wie `web.config` oder `appsettings*.json`. Der Schutz deployment-seitiger
Dateianpassungen läuft getrennt davon über die `AutoUpdate:ProtectedFiles`-Schutzliste im generierten
Installationsskript (siehe oben; Sicherungen unter `Updates/backup/`).

## Release-Artefakte

Damit der GitHub-Quelle ein Update erkennbar ist, erzeugt `.github/workflows/main-release.yml` zwei zusätzliche
Artefakte:

- `release-metadata.json` – liegt in jedem Release-Archiv und beschreibt die installierte Version
  (`version`, `publishedAt`, `commitSha`, `repository`, `runtimeIdentifier`). Ohne diese Datei kann der Updater
  die installierte Version nicht ermitteln und lädt kein Update.
- `update.json` – Release-Manifest-Asset, erzeugt von `.github/scripts/create-update-manifest.sh`, mit
  Plattform, Runtime-Identifier, Asset-URL, SHA256 und Größe der beiden Release-Archive.

Der Updater unterstützt Windows (IIS in beiden Hosting-Modellen, Dienst oder ausführbare Datei) und
Linux (systemd); macOS ist nicht unterstützt.
