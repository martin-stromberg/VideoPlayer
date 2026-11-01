# Tasks: `msTools.Updater` 0.11.0-rc.4 → 0.11.0-rc.5

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Pakete | `msTools.Updater` `0.11.0-rc.4` → `0.11.0-rc.5` in `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 59) | Offen | — |
| 2 | Pakete | `msTools.Updater` `0.11.0-rc.4` → `0.11.0-rc.5` in `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22) | Offen | — |
| 3 | Pakete | `lib/packages/msTools.Updater.0.11.0-rc.5.nupkg` committen (derzeit untracked) und `lib/packages/msTools.Updater.0.11.0-rc.4.nupkg` entfernen | Offen | — |
| 4 | Verifikation | `dotnet restore VideoPlayer.sln`, dann `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei | Offen | — |
| 5 | Tests | Update-bezogene Tests gefiltert ausführen: `MsToolsUpdaterIntegrationTests`, `AutoUpdateProtectedFilesTests`, `UpdateAdminServiceTests`, `UpdateSettingsServiceTests`, `UpdateSettingsServiceTests_Iis`, `UpdateBackupCoordinatorTests`, `UpdateBackupEventBinderTests`, `UpdatesControllerAuthorizationTests`, `UpdatesPageE2ETests` | Offen | — |
| 6 | Tests | Sehr wahrscheinlich: `AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder` an das rc.5-Installationsskript anpassen (Skript substanziell geändert: Metadaten-Fallback, Staging-Leer-Abbruch, Versionsprüfung, `update.failed`-Trap; Reihenfolge-Absicherung *Backup → Copy → Merge* erhalten; Präzedenzfall rc.4: `Copy-AutoUpdateStagedFiles`) | Offen | — |
| 7 | Verifikation | Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Nicht-E2E + E2E) grün — Ausgangszustand 2838/2840, 2 dokumentierte Skips | Offen | — |
| 8 | Dokumentation | `docs/RELEASE_NOTES.md` in beiden Sprachabschnitten („What's New"/„Neuerungen") um einen kurzen Eintrag zum rc.5-Bump ergänzen (Update-Verifikation + Linux-Monitoring robuster; interner Robustheitsfix; Verweis auf Upstream-Issues #76/#78) | Offen | — |
| 9 | Dokumentation | `docs/TECH_Auto_Update.md` minimal im betroffenen Umfang nachziehen: `release-metadata.json`-Beschreibung (Z. 197–199) um den neuen Metadaten-Fallback und `InstallationVerificationInconclusive` ergänzen sowie `update.failed`-Marker/`systemd-run --wait` beim Linux-Monitoring erwähnen (übriger preexisting-veralteter Bestand bleibt unangetastet); `docs/help/updates.md` bleibt unverändert | Offen | — |
| 10 | Dokumentation | Nach den Doku-Änderungen `dotnet test tools/MarkdownLinkCheck.Tests` ausführen | Offen | — |
