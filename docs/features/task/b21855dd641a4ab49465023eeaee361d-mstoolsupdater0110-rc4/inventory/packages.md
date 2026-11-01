# Pakete und NuGet-Feed

## NuGet-Feed-Konfiguration

Datei: `NuGet.config` (Repo-Root)

```xml
<packageSources>
  <clear />
  <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  <add key="local" value="lib/packages" />
</packageSources>
```

- Zwei Quellen: `nuget.org` und der **repo-lokale Feed** `lib/packages` (relativer Pfad zum
  Repository-Root). `<clear/>` entfernt alle globalen Quellen — `msTools.Updater`/`msTools.Backup`
  werden ausschließlich aus `lib/packages` aufgelöst.
- Keine `packages.lock.json` vorhanden (Restore im Floating-Modus über die Version in den csproj).
- CI verwendet denselben Mechanismus (`dotnet restore VideoPlayer.sln` in
  `.github/workflows/staging-ci.yml`, `pr-staging-ci.yml`, `release.yml`) — eine
  Workflow-Änderung ist für den Pakettausch nicht nötig (bewährt seit dem rc.1-Upgrade).

## Inhalt des lokalen Feeds `lib/packages/`

| Datei | Git-Status | Zweck |
|-------|-----------|-------|
| `msTools.Backup.1.1.0-RC.4.nupkg` (38.168 B) | getrackt | Backup-Bibliothek (nicht Teil dieser Anforderung) |
| `msTools.Updater.0.11.0-rc.4.nupkg` (128.112 B) | getrackt | aktuell referenzierte Updater-Version |
| `msTools.Updater.0.11.0-rc.5.nupkg` (130.298 B) | **untracked** | neue Version — noch zu committen |

## Paketreferenzen

| Datei | Zeile | Referenz |
|-------|-------|----------|
| `VideoWebPlayer/VideoWebPlayer.csproj` | 59 | `<PackageReference Include="msTools.Updater" Version="0.11.0-rc.4" />` |
| `VideoWebPlayer/VideoWebPlayer.csproj` | 58 | `<PackageReference Include="msTools.Backup" Version="1.1.0-RC.4" />` (Kontext, unverändert) |
| `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` | 22 | `<PackageReference Include="msTools.Updater" Version="0.11.0-rc.4" />` |

Keine weiteren `msTools.Updater`-Referenzen im Repo (keine `Reference`-Einträge auf DLLs mehr —
die frühere Datei-Referenz unter `lib/msTools.Updater/` wurde mit Commit `20660cf` auf
`PackageReference` umgestellt).

## Paketvergleich rc.4 ↔ rc.5 (verifiziert durch Entpacken beider nupkg)

### nuspec

Einzige Unterschiede:

| Feld | rc.4 | rc.5 |
|------|------|------|
| `version` | `0.11.0-rc.4` | `0.11.0-rc.5` |
| `repository/@commit` | `3bf43f9d076cc43c3ef611b982b7b04cd1ac8648` | `bebedcf0862def9211361eda83d2f078e7977c1f` |

Identisch: `id` (`msTools.Updater`), `authors` (Softwareschmiede), `license` (**MIT**,
Expression), `readme`, `description`, Ziel-Framework `net10.0` und alle Dependencies
(`Microsoft.Extensions.*` 10.0.7, `System.Management` 10.0.11 — jeweils `exclude="Build,Analyzers"`).
`README.md` im Paket ist bytidentisch. `lib/net10.0/msTools.Updater.dll`: 292.352 B (rc.4) →
301.056 B (rc.5).

### API-Delta (`lib/net10.0/msTools.Updater.xml`, 487 → 489 dokumentierte Member)

Rein additiv, keine entfernten oder geänderten Member:

| Neues Mitglied | Art | Beschreibung (aus XML-Doc) |
|----------------|-----|----------------------------|
| `F:msTools.Updater.AutoUpdateErrorCode.InstallationVerificationInconclusive` | Enum-Wert | Installationsskript endete ohne Fehler-Marker, aber die installierte Version konnte nicht verifiziert werden, weil keine `release-metadata.json` vorliegt. |
| `M:msTools.Updater.AutoUpdateOrchestrator.IsInstallWithinGracePeriod(AutoUpdateStatusSnapshot)` | Methode | Eine in `LastInstallResult` verzeichnete Installation gilt für `AutoUpdateOptions.HealthTimeoutSeconds` nach ihrem Start als potenziell noch laufend — derselbe Horizont wie die Lock-Alterung. In diesem Fenster darf die Reconciliation weder den Workspace aufräumen noch den lock-losen `Installing`-Zustand in einen Fehlschlag umdeuten. |

**Wichtig:** `IsInstallWithinGracePeriod` ist auf der konkreten Klasse `AutoUpdateOrchestrator`
deklariert, **nicht** auf `IAutoUpdateOrchestrator`. Das Interface umfasst in rc.5 weiterhin nur
`RunUpdateAsync`, `CheckForUpdateAsync`, `DownloadAsync`, `InstallAsync`, `GetStatusAsync`. Der
eigene Code (`UpdateAdminService`, `Updates.razor`-Testdouble `FakeUpdateBackend`) konsumiert
ausschließlich `IAutoUpdateOrchestrator` — die neue Methode ist über das Interface nicht
erreichbar.

### Verwendete Updater-API — Vollständigkeitsprüfung

Alle im eigenen Code und in den Tests referenzierten `msTools.Updater`-Typen sind in der
rc.5-XML-Dokumentation weiterhin vorhanden (Stichprobe verifiziert):
`AutoUpdateHostBuilderExtensions.UseAutoUpdate` / `DefaultConfigurationSectionName`,
`AutoUpdateOptions` (inkl. `SourceCheck`, `ProtectedFiles`, `Enabled`, `AllowPrereleaseUpdates`,
`EnableAutomaticDownload`, `EnableAutomaticInstallation`, `ServiceName`), `SourceCheckOptions`,
`SourceCheckTimeRange`, `AutoUpdateGithubSource`, `AutoUpdateLocalFolderSource` /
`UseLocalFolderSource`, `AutoUpdateStatusService`, `IAutoUpdateOrchestrator`,
`IAutoUpdateCommandHandler`, `AutoUpdateStatusSnapshot`, `AutoUpdateState`, `AutoUpdateResult`,
`AutoUpdateOutcome`, `AutoUpdateErrorCode`, `AutoUpdateCheckResult`, `AutoUpdateDownloadResult`,
`AutoUpdateInstallResult`, `IAutoUpdateEventAggregator`, `AutoUpdateEvents` (inkl.
`RaiseBeforeInstall`), `BeforeInstallEventArgs`, `AutoUpdateErrorEventArgs`,
`AutoUpdateProtectedFile`, `AutoUpdateProtectedFileStrategy`, `AutoUpdateOptionsValidator`,
`IAutoUpdateScriptGenerator`, `IAutoUpdatePackageStore`, `IAutoUpdatePlatformResolver` /
`AutoUpdatePlatformResolver`, `AutoUpdatePackageDescriptor`, `AutoUpdateInstallationTarget`,
Fluent-Optionen `WithDownloadPath`, `WithUpdateUnitName`, `DisableHostedServices` sowie der
Konfigurationsschlüssel `AutoUpdate:HostedServicesEnabled`.

### Nicht in der API-Doku sichtbar: Installationsskript

Das generierte Installationsskript (PowerShell/Bash) ist in der DLL eingebettet und nicht Teil
der XML-Dokumentation. Inhaltliche Änderungen zwischen rc.4 und rc.5 sind daher aus dem Paket
nicht ablesbar. `AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder`
assertiert Skript-Marker — beim rc.4-Upgrade musste dieser Test angepasst werden (siehe
Historie unten). Die neue API deutet auf Änderungen im Bereich Installations-Verifikation
(`InstallationVerificationInconclusive`, Grace-Period für `Installing`-Zustand) hin.

## Upgrade-Historie (git log)

| Commit | Inhalt |
|--------|--------|
| `819b998` | `msTools.Updater` 0.8.1.0 rc.1 (erste Paketversion) |
| `41ad9d9` | 0.8.1 final, RC.1-Paket entfernt |
| `20660cf` | Umstellung DLL-Dateireferenz → `PackageReference` + lokaler Feed |
| `97f09ba` | 0.10.4-rc.1 |
| `20b31ff` | 0.11.0-rc.1 — `ProtectedFiles`-Schutzliste (Issue #226, Updater-Issue #66) |
| `f91f04c` | 0.11.0-rc.2 — IIS-Pfad ohne erhöhte Rechte, `update.log`, WMI-entkoppelter Skriptstart (Issues #69/#70) |
| `3232810` | 0.11.0-rc.3 — `app_offline`-Pfad-Fix unter IIS (Issue #72) |
| `7ce924b` | 0.11.0-rc.4 — Restart-Race- und Sperr-Robustheit unter IIS (Issue #74); Skript-Marker-Test angepasst |

Bewährtes Vorgehen (dokumentiert in
`docs/features/task/issue-226-ca48a532dd7347a6b5e56388a105ef7f-update-ueberschreibt-webconfig-tasks.md`,
Schritte V1, K5–K7): Versionsangabe in **beiden** csproj erhöhen, nupkg im Feed tauschen (neu
committen, alt löschen), `dotnet restore`, `dotnet build VideoPlayer.sln` in Debug **und**
Release, volle Testsuite. Code-/Doku-Anpassungen nur bei Bedarf: bei rc.2 Test-Fixtures
(`IUpdateHostEnvironment`-Mocks) + Doku; bei rc.4 Skript-Marker im Test; bei rc.3 nichts weiter.

## Dokumentations-Befunde (Bestand)

- `docs/TECH_Auto_Update.md` Z. 10–20 beschreibt noch die veraltete DLL-Dateireferenz unter
  `lib/msTools.Updater/` samt `gh release download`-Anleitung — tatsächlich nupkg-Feed
  `lib/packages` + `PackageReference`. Z. 194–201 nennt `.github/workflows/main-release.yml` und
  `.github/scripts/create-update-manifest.sh`; beide existieren nicht mehr — Release-Artefakte
  werden heute von `.github/actions/build-and-package` und `release.yml` erzeugt. Diese
  Veraltung ist im Tasks-Dokument des letzten Projekts (Zeile 15, Schritt 11) bereits als
  offener optionaler Punkt dokumentiert — kein neuer Befund dieser Anforderung.
- `docs/help/updates.md` beschreibt die Admin-UI und den IIS-Updatepfad aktuell (kein
  Versionsbezug zur Bibliothek).
- `docs/RELEASE_NOTES.md` enthält die Updater-bezogenen Einträge der Vor-Upgrades (IIS-Pfad,
  ProtectedFiles, `update.log`/Diagnose) ohne Versionsnummern der Bibliothek.
