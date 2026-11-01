# Konsumierender Code und Doku-Stellen

## Paketreferenzen und Feed

| Stelle | Inhalt |
|--------|--------|
| `VideoWebPlayer/VideoWebPlayer.csproj` Z. 59 | `<PackageReference Include="msTools.Updater" Version="0.11.0-rc.5" />` |
| `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` Z. 22 | `<PackageReference Include="msTools.Updater" Version="0.11.0-rc.5" />` |
| `NuGet.config` | Quelle `local` = `lib/packages` (neben `nuget`-Quelle) |
| `lib/packages/` | `msTools.Updater.0.11.0-rc.5.nupkg` (getrackt, 130.298 B), `msTools.Updater.0.11.0-rc.6.nupkg` (**untracked**, 131.000 B), `msTools.Backup.1.1.0-RC.4.nupkg` |
| `*/obj/project.assets.json` | löst aktuell `msTools.Updater/0.11.0-rc.5` auf |

## Eigenen Code, der `msTools.Updater` nutzt

Genutzte Bibliothekstypen im Produktivcode (Häufigkeit absteigend): `AutoUpdateState` (40), `AutoUpdateStatusSnapshot` (8), `AutoUpdateOutcome` (4), `AutoUpdateOptions` (3), `AutoUpdateGithubSource` (3), `IAutoUpdateOrchestrator`, `IAutoUpdateEventAggregator`, `IAutoUpdateCommandHandler` (je 2), `AutoUpdateResult`, `AutoUpdateInstallResult`, `AutoUpdateHostBuilderExtensions`, `AutoUpdateErrorEventArgs`, `AutoUpdateDownloadResult` (je 1). `DefaultAutoUpdateProcessRunner` wird nicht referenziert.

| Datei | Rolle |
|-------|-------|
| `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs` | `AddVideoWebPlayerAutoUpdate(WebApplicationBuilder)`: bindet `UpdateBackupOptions`, registriert `UpdateBackupCoordinator`/`UpdateSettingsInitializer`/`UpdateBackupEventBinder`, ruft `builder.UseAutoUpdate(cfg => cfg.UseSource(VideoWebPlayerUpdateSourceFactory.Create(...)).WithUpdateUnitName("VideoWebPlayer-AutoUpdate"))` |
| `VideoWebPlayer/Program.cs` Z. 32 | Aufruf `builder.AddVideoWebPlayerAutoUpdate()` |
| `VideoWebPlayer/Services/Updates/UpdateAdminService.cs` | Fassade für Admin-UI-Status und manuelle Update-Aktionen (nutzt `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`) |
| `VideoWebPlayer/Services/Updates/UpdateBackupEventBinder.cs` | Hosted Service, abonniert `IAutoUpdateEventAggregator.BeforeInstall`, bricht Installation bei Backup-Fehler ab |
| `VideoWebPlayer/Services/Updates/UpdateBackupCoordinator.cs` | Koordiniert das Pre-Install-Backup (löst `IUpdateBackupService`) |
| `VideoWebPlayer/Services/Updates/IUpdateBackupService.cs`, `VideoWebPlayerUpdateBackupService.cs`, `UpdateBackupOptions.cs`, `UpdateSettingsService.cs`, `UpdateSettingsInitializer.cs`, `UpdateHostEnvironment.cs`, `VideoWebPlayerUpdateSourceFactory.cs` | Eigene Update-Infrastruktur (Backup-Adapter auf `msTools.Backup`, persistierte Einstellungen, GitHub-Quelle `martin-stromberg/VideoPlayer`) |
| `VideoWebPlayer/Controllers/UpdatesController.cs` | Serverseitige Admin-Endpunkte (`AdminOnly`-Policy, Antiforgery) |
| `VideoWebPlayer/Components/Pages/Admin/Updates.razor` | Admin-Seite `/admin/updates`, `@using msTools.Updater` |

Eine eigene Codeanpassung ist nicht erwartet (API-Delta rein additiv, siehe [packages.md](packages.md)).

## Doku-Stellen mit Update-Bezug

| Datei | Stelle | Bezug zu rc.6 |
|-------|--------|---------------|
| `docs/TECH_Auto_Update.md` Z. 117–125 (Abschnitt „Update-Installation unter Linux (systemd)") | beschreibt `systemd-run --wait`: „Der Starter-Prozess wartet bis zum Unit-Ende und propagiert den Exit-Code, sodass der Updater Lock-Datei und Paket-Deskriptor erst nach dem tatsächlichen Skriptende aufräumt" | **durch rc.6 überholt** — `--wait` entfernt, Starter ist kurzlebig, Lock/Deskriptor räumt das Skript selbst |
| `docs/TECH_Auto_Update.md` Z. 10–19 | beschreibt noch die alte Dateireferenz-Einbindung (`lib/msTools.Updater/`, `release.zip`) | preexisting-veraltet (Verzeichnis existiert nicht mehr, Paket kommt per NuGet-Feed); beim rc.5-Upgrade bewusst unangetastet gelassen |
| `docs/RELEASE_NOTES.md` | EN „What's New" Z. 39 und DE „Neuerungen" Z. 177 enthalten je den rc.5-Eintrag; darüber Z. 22–23/160–161 die „Important Notes" | neuer rc.6-Eintrag je Sprachabschnitt nach dem Muster von Z. 39/177 |
| `docs/help/updates.md` | Z. 38, 40, 72–81 — ProtectedFiles-, IIS- und Backup-Beschreibung | voraussichtlich unverändert (kein User-sichtbarer systemd-Detailtext) |
| `README.md` | keine `msTools.Updater`-Versionsnennung gefunden | voraussichtlich unverändert |

## Vorgehensmuster des letzten Upgrades (rc.4 → rc.5)

`git show 206b82b` (PR #324): genau 7 Dateien — beide csproj (Versionsstring), `lib/packages`-Tausch (rc.4 gelöscht, rc.5 hinzugefügt), `docs/TECH_Auto_Update.md` (+18 Zeilen), `docs/RELEASE_NOTES.md` (+2 Zeilen), Tasks-Dokument. Commit-Sequenz: `plan:` → `feat:` → PR-Merge; danach zwei `chore:`-Dummy-Commits (`de35de4`, `02377b5`) zum Auslösen von Prereleases für den Live-Update-Test.
