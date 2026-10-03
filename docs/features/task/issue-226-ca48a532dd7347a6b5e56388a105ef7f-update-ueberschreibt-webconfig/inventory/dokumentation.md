# Dokumentationsstand

Welche bestehenden Dokumente das Thema Secrets/`web.config`/Update-Verhalten bereits beschreiben — und wo Lücken oder veraltete Angaben bestehen.

## `docs/GUIDE_Installation.md` (Version 2.0, Stand 2026-08-25)

- Abschnitt „Produktive Konfiguration" (Zeilen 88–106): empfiehlt produktive `Jwt:*`-Werte als **Umgebungsvariablen** (`export Jwt__Key=…` / `$env:Jwt__*`), benennt `Jwt:ApiToken:Maui` als Pflicht in Produktion.
- Der Weg über `<environmentVariables>` in der IIS-`web.config` wird **nirgends** in der Dokumentation erwähnt — obwohl `issue.md` ihn als die deployed Variante beschreibt (Reproduktionsschritt 1 verweist auf die Installationsdoku).
- **Veraltet:** „Voraussetzungen" (Zeile 16) und „Häufige Fehler" (Zeilen 180–182) nennen noch die DLL-Datei-Referenz `lib/msTools.Updater/msTools.Updater.dll`; tatsächlich wird `msTools.Updater` als nupkg über `lib/packages`/`NuGet.config` eingebunden (`VideoWebPlayer.csproj:53`).
- Testbefehle (Zeilen 157–168): `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`, `MarkdownLinkCheck.Tests`, Filter `ApiDocumentationContractTests`.

## `docs/SECRETS_MANAGEMENT.md` (Version 2.0, Stand 2026-08-25)

- Tabelle (Zeilen 12–21): alle `Jwt:*`-Werte als geheim markiert, empfohlene Quelle „User Secrets, Umgebungsvariable, Secret Store".
- Produktion (Zeilen 45–63): Umgebungsvariablen-Exporte (Linux/Windows PowerShell); Hinweis auf Secret-Management-Systeme. Keine Erwähnung der IIS-`web.config`-Variante und kein Hinweis, dass diese beim Update verloren geht.
- „Was nicht ins Repository gehört" (Zeilen 65–73) nennt u. a. `secrets.json` und `.env`-Dateien — für eine evtl. neue lokale Konfigurationsdatei wäre hier ein Eintrag nötig.

## `docs/TECH_Auto_Update.md`

- Zeilen 8–20: **veralteter Einbindungsabschnitt** — beschreibt `lib/msTools.Updater/msTools.Updater.dll` als Datei-Referenz und ein `gh release download`-Update-Verfahren; tatsächlich: nupkg unter `lib/packages/`, `PackageReference` Version `0.10.4-rc.1`.
- Zeilen 22–25: `AddVideoWebPlayerAutoUpdate` + `AutoUpdateExtensions.cs` korrekt beschrieben (GitHub-Quelle, Unit-Name).
- Konfigurationstabelle (Zeilen 77–90): dokumentiert `Enabled`, `EnableAutomaticDownload/Installation`, `AllowPrereleaseUpdates`, `DownloadPath`, `SourceCheck.Interval`, `Backup.*`; Zeile 89–90 verweist generisch auf „`ServiceName`, `ExecutablePath`, `ScheduledInstallTime`, `StopHostAfterScriptStart`, `MaxAssetBytes`" — **`AppPoolName`/`SiteName` fehlen**, obwohl sie für IIS-Deployments der entscheidende Schlüssel sind.
- „Sicherung vor der Installation" (Zeilen 92–112): beschreibt `BeforeInstall`-Backup über `UpdateBackupEventBinder`/`UpdateBackupCoordinator` — sichert nur Daten (DB), keine Konfigurationsdateien.
- „Release-Artefakte" (Zeilen 114–123): `release-metadata.json` + `update.json`; nennt `.github/workflows/main-release.yml` und `.github/scripts/create-update-manifest.sh` — **veraltet**, aktuell `.github/actions/build-and-package/action.yml` (+ `release.yml`/`staging-ci.yml`).

## `docs/help/backups.md`

- Zeile 61: IIS-Abschnitt beschreibt `OutOfProcess`-Hosting, das `requestFiltering`-Problem, die mitgelieferte `web.config` mit `maxAllowedContentLength=4294967295` und `requestTimeout=02:00:00` sowie den manuellen `appcmd`-Workaround, „wird die `web.config` beim Deployment überschrieben". **Kein Hinweis, dass das Programmupdate die Datei regelmäßig überschreibt** — der Absatz adressiert nur manuelles Deployment.

## `docs/help/updates.md`

- beschreibt die Admin-UI `/admin/updates` (Status, manuelle Prüfung/Installation, Konfiguration inkl. `Dienstname für Neustart`, Backup-vor-Installation).
- **Kein** Hinweis darauf, dass eine Installation deployment-seitige Dateien (`web.config`, `appsettings*.json`) ersetzt, und kein `AppPoolName`-Feld in der UI-Konfiguration (nur `ServiceName`).

## `docs/RELEASE_NOTES.md`

- Zeilen 52/120: weist auf `OutOfProcess`-Hosting und das zu erhöhende `requestFiltering`-Limit hin (Einführung der `web.config`).
- Kein Hinweis auf den Verlust deployment-seitiger `web.config`-Einträge beim Update.

## `README.md`

- Keine `web.config`-/Update-/Secrets-spezifischen Abschnitte gefunden (kein Treffer für `web.config`).

## `docs/Anforderung_msTools_Updater_Installationsskript.md` (Präzedenzfall)

- Anforderung an die Bibliothek (A1–A6): deterministische Paketreferenz, `update.log`-Logging, Paket/Staging-Erhalt im Fehlerfall, persistenter Fehlerstatus, sichere Beendigungsreihenfolge, neue Events `BeforeStartUpdateScript`/`AfterStartUpdateScript`.
- Abschnitt „Nicht durch VideoWebPlayer lösbar" (Zeilen 65–68): stellt fest, dass Skriptgenerierung und Installationsablauf **vollständig in `msTools.Updater`** liegen; VideoWebPlayer kann nur `release-metadata.json` korrekt befüllen und Update-Events loggen.
- Gegenprobe zum aktuellen Stand (`0.10.4-rc.1`): `BeforeStartUpdateScript`/`AfterStartUpdateScript` existieren inzwischen in `IAutoUpdateEventAggregator`; `update.log`-Logging ist aber nur im **Linux**-Skript implementiert — die beiden Windows-Skriptvarianten schreiben kein `update.log` (nur `Write-Host`/`Write-Warning`).

## `issue.md` (Repo-Root, Original-Issue)

- Bestätigt das Fehlerbild: Update auf `v1.13.0-rc.2` (Installationszeitpunkt 2026-09-23 laut `Updates/status.json`) ersetzte die `web.config` ohne Backup; `Jwt:Key` und `Jwt:ApiToken:Web` mussten neu generiert werden → alle Tokens invalidiert.
- Reproduktion beschreibt die deployment-seitige `<environmentVariables>`-Pflege „so wie in `docs/GUIDE_Installation.md` für produktive Secrets vorgesehen" — die Doku selbst enthält diese Variante jedoch nicht explizit.

## Zusammenfassung der Doku-Lücken

1. Die IIS-`environmentVariables`-Variante für Secrets ist nicht dokumentiert, obwohl sie deployed wird.
2. `AppPoolName`/`SiteName` (`AutoUpdate`-Konfiguration bzw. `WithIisApplicationPool`) fehlen in `TECH_Auto_Update.md` und `help/updates.md` — für IIS-Deployments ist `AutoUpdate:AppPoolName` der einzige Weg zum IIS-Skript, da der Probe nur Windows-Dienste erkennt.
3. `TECH_Auto_Update.md` enthält zwei veraltete Abschnitte (DLL-Einbindung, Artefakt-Erzeugung via `main-release.yml`/`create-update-manifest.sh` statt `build-and-package/action.yml`).
4. `help/backups.md` Zeile 61 erwähnt das Überschreiben der `web.config` nur im Kontext manueller Deployments, nicht beim Programmupdate.
