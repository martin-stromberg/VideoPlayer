# Tasks: `msTools.Updater` 0.11.0-rc.5 → 0.11.0-rc.6

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Pakete | `msTools.Updater` `0.11.0-rc.5` → `0.11.0-rc.6` in `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 59) | Offen | — |
| 2 | Pakete | `msTools.Updater` `0.11.0-rc.5` → `0.11.0-rc.6` in `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22) | Offen | — |
| 3 | Pakete | `lib/packages/msTools.Updater.0.11.0-rc.6.nupkg` committen (derzeit untracked) und `lib/packages/msTools.Updater.0.11.0-rc.5.nupkg` entfernen | Offen | — |
| 4 | Verifikation | `dotnet restore VideoPlayer.sln`, dann `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei (`project.assets.json` muss `msTools.Updater/0.11.0-rc.6` auflösen) | Offen | — |
| 5 | Tests | Update-bezogene Tests gefiltert ausführen: `MsToolsUpdaterIntegrationTests`, `AutoUpdateProtectedFilesTests`, `UpdateAdminServiceTests`, `UpdateSettingsServiceTests`, `UpdateSettingsServiceTests_Iis`, `UpdateBackupCoordinatorTests`, `UpdateBackupEventBinderTests`, `UpdatesControllerAuthorizationTests`, `UpdatesPageE2ETests` | Offen | — |
| 6 | Tests | Risiko-Test verifizieren: `AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder` muss gegen rc.6 **unverändert** grün bleiben (Marker in der rc.6-DLL vorab verifiziert); nur bei Fehlschlag Marker-Sets an das rc.6-Skript angleichen, Reihenfolge *Backup → Copy → Merge* erhalten | Offen | — |
| 7 | Verifikation | Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Nicht-E2E + E2E) grün | Offen | — |
| 8 | Dokumentation | `docs/TECH_Auto_Update.md` Z. 117–125 (Absatz „Update-Installation unter Linux (systemd)") auf die rc.6-Architektur umschreiben: `systemd-run` ohne `--wait`, kurzlebiger Starter nur bis Unit-Registrierung, `MonitorStarterAsync` meldet nur Starter-Fehler, Lock/Deskriptor räumt das Skript selbst, Fehlerpfad über `cleanup`-Trap + `update.failed`-Marker + Karenz-Reconciliation; preexisting-veralteter Bestand (Z. 10–19) bleibt unangetastet | Offen | — |
| 9 | Dokumentation | `docs/RELEASE_NOTES.md` in beiden Sprachabschnitten („What's New" Z. 39 / „Neuerungen" Z. 177) um einen kurzen Eintrag zum rc.6-Bump ergänzen (systemd-run-Starter meldet Self-Update nicht mehr als fehlgeschlagen; Verweis auf Upstream-Issue #78 / PR #80); `docs/help/updates.md` bleibt unverändert | Offen | — |
| 10 | Dokumentation | Nach den Doku-Änderungen `dotnet test tools/MarkdownLinkCheck.Tests` ausführen | Offen | — |
