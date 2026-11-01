# Bestandsaufnahme: `msTools.Updater` 0.11.0-rc.5 → 0.11.0-rc.6

Bestandsaufnahme zum NuGet-Upgrade der Self-Update-Bibliothek `msTools.Updater` auf `0.11.0-rc.6` im lokalen Feed `lib/packages` — betrachtet wurden Paketreferenzen, Paketdelta rc.5→rc.6, konsumierender Code, Update-Tests und Doku-Stellen.

## Zusammenfassung

- **Paketreferenzen:** `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 59) und `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22) referenzieren `msTools.Updater` `0.11.0-rc.5`. `project.assets.json` beider Projekte bestätigt die aufgelöste Version `0.11.0-rc.5`.
- **Lokaler Feed:** `NuGet.config` deklariert die Quelle `local` = `lib/packages`. Darin liegen `msTools.Updater.0.11.0-rc.5.nupkg` (getrackt) und `msTools.Updater.0.11.0-rc.6.nupkg` (**untracked**), daneben `msTools.Backup.1.1.0-RC.4.nupkg` (unverwandt).
- **Paketdelta:** nuspec identisch bis auf `<version>` und nuspec-`commit` (`bebedcf0…` → `8ff437a4…`); Lizenz MIT und Dependencies (7 Pakete, `Microsoft.Extensions.*` 10.0.7, `System.Management` 10.0.11) unverändert → AGENTS.md-Regel für neue NuGet-Hauptversionen nicht betroffen. API-Delta rein additiv: ein neuer Member `DefaultAutoUpdateProcessRunner.MonitorStarterAsync(Process, bool)` — kein Breaking Change.
- **Inhaltliche Änderung (String-Vergleich der DLLs):** `--wait` kommt in rc.6 nicht mehr vor (in rc.5 vorhanden); neue Meldungen zum kurzlebigen `systemd-run`-Starter („…exited with code {ExitCode}; the update unit may not have started", „Update unit registered (starter exited with code {ExitCode}).", „Failed to monitor the systemd-run starter process."). Der Starter wartet nicht mehr bis zum Unit-Ende; Lock/Deskriptor räumt das Skript selbst auf — die Aussage in `docs/TECH_Auto_Update.md` Z. 117–119 ist damit für rc.6 überholt.
- **Konsumierender Code:** nutzt nur stabile API-Flächen (`UseAutoUpdate`, `AutoUpdateOptions`, `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`, `IAutoUpdateEventAggregator`, Status-Typen); `DefaultAutoUpdateProcessRunner` wird nirgends direkt referenziert → keine Codeanpassung erwartet.
- **Tests:** Die Skript-Marker, die `AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder` assertiert (`Backup-AutoUpdateProtectedFiles -Files`, `Copy-AutoUpdateStagedFiles -Source`, `Invoke-AutoUpdateProtectedFiles -Files`, `$protectedFiles = @(`), sind in der rc.6-DLL weiterhin enthalten → Test voraussichtlich unverändert grün.
- **Vorgehensmuster (rc.4→rc.5, PR #324):** plan-Commit + feat-Commit; geändert wurden genau: beide csproj (Versionsstring), nupkg-Tausch in `lib/packages`, `docs/TECH_Auto_Update.md` (+18 Zeilen), `docs/RELEASE_NOTES.md` (je 1 Eintrag in „What's New" und „Neuerungen"), Tasks-Dokument.

**Test-Ausgangszustand:** Kein lokaler Testlauf für diese Bestandsaufnahme (bewusst schlank gehalten). Nachweis „zuletzt grün": PR #324 (rc.5-Bump) CI vollständig SUCCESS (`build & test`, `static checks`, `detect-backmerge`), gemergt 2026-11-01T10:49:36Z; danach nur zwei `chore:`-Dummy-Commits für Update-Test-Prereleases. Der beim rc.5-Upgrade dokumentierte Ausgangszustand der Voll-Suite war 2591 + 247 bestanden, 2 dokumentierte Skips, 0 Fehlschläge. Details: [inventory/tests.md](inventory/tests.md).

## Details

- [Paketdelta rc.5 → rc.6 (nuspec, Lizenz, Dependencies, API)](inventory/packages.md)
- [Konsumierender Code und Doku-Stellen](inventory/code.md)
- [Tests und Test-Ausgangszustand](inventory/tests.md)
