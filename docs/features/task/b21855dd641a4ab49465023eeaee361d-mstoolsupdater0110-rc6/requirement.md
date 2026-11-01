# Übersetzte Anforderung: `msTools.Updater` 0.11.0-rc.5 → 0.11.0-rc.6

## Fachliche Zusammenfassung

Die im Projekt eingebundene Self-Update-Bibliothek `msTools.Updater` soll von Version `0.11.0-rc.5` auf `0.11.0-rc.6` angehoben werden. Das neue Paket liegt bereits als `lib/packages/msTools.Updater.0.11.0-rc.6.nupkg` im lokalen NuGet-Feed des Repositories vor und ersetzt die bisherige Datei `msTools.Updater.0.11.0-rc.5.nupkg`. Es handelt sich um ein reines Bibliotheks-Upgrade ohne eigenes fachliches Feature im VideoWebPlayer; Ziel ist die Übernahme der Korrekturen der neuen Updater-Version — dasselbe Muster wie beim heutigen Upgrade rc.4→rc.5 (PR #324) und den früheren Upgrades (dokumentiert in `docs/features/task/issue-226-ca48a532dd7347a6b5e56388a105ef7f-update-ueberschreibt-webconfig-tasks.md` und `docs/features/task/b21855dd641a4ab49465023eeaee361d-mstoolsupdater0110-rc4-tasks.md`).

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 59): `PackageReference` `msTools.Updater` `0.11.0-rc.5` → `0.11.0-rc.6`
- `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22): `PackageReference` analog erhöhen
- Lokaler NuGet-Feed `lib/packages` (Quelle `local` in `NuGet.config`): `msTools.Updater.0.11.0-rc.6.nupkg` committen (liegt derzeit untracked im Arbeitsverzeichnis), `msTools.Updater.0.11.0-rc.5.nupkg` entfernen
- Verbrauchender eigener Code (keine Änderung erwartet): `Extensions/AutoUpdateExtensions.cs`, `Services/Updates/` (`UpdateAdminService`, `UpdateBackupEventBinder`, `UpdateSettingsService`, `VideoWebPlayerUpdateSourceFactory`), `Components/Pages/Admin/Updates.razor`
- Update-bezogene Tests (nach dem Bump laufen lassen, ggf. anpassen): `MsToolsUpdaterIntegrationTests`, `UpdateBackupCoordinatorTests`, `UpdateBackupEventBinderTests`, `UpdateSettingsServiceTests(_Iis)`, `AutoUpdateProtectedFilesTests`, `UpdatesPageE2ETests`, `UpdatesControllerAuthorizationTests`. Besondere Aufmerksamkeit für Tests, die Marker im generierten Installationsskript assertieren (`AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder`), weil das generierte Skript Teil der Bibliothek ist und sich zwischen Paketversionen ändern kann.
- Keine Datenmodell-, EF-Migrations- oder Interface-Änderungen im eigenen Code

## Implementierungsansatz

- Bewährtes Vorgehen der bisherigen Upgrades: Versionsangabe in beiden csproj-Dateien erhöhen, Paketdatei im lokalen Feed tauschen, `dotnet restore`, `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei, volle Testsuite `dotnet test VideoWebPlayer.Tests` grün.
- API-Delta rc.5 → rc.6 (verifiziert durch Vergleich der `lib/net10.0/msTools.Updater.xml` beider Pakete): rein additiv — genau ein neuer Member, die Methode `DefaultAutoUpdateProcessRunner.MonitorStarterAsync(Process, bool)`; keine entfernten Member → kein Breaking Change, Codeanpassung voraussichtlich nicht nötig. Der Typ `DefaultAutoUpdateProcessRunner` wird im eigenen Code nicht direkt referenziert.
- Inhaltlicher Fokus von rc.6 (aus der XML-Dokumentation des neuen Members ableitbar): Korrektur des Linux-Update-Monitorings. `MonitorStarterAsync` überwacht den kurzlebigen `systemd-run`-Starter separat — im Gegensatz zu `MonitorAsync` (Windows-Pfad) berührt er weder Installations-Lock noch persistierten Deskriptor, weil der Exit des Starters nur die Registrierung der transienten Unit widerspiegelt, nicht den Ausgang des Skripts; Fehlschläge werden weiterhin über den `update.failed`-Marker und die Reconciliation erkannt. Damit setzt rc.6 die in rc.5 eingeführte Linux-Härtung (`systemd-run --wait`, Karenzfenster, `update.failed`-Marker, siehe `docs/TECH_Auto_Update.md` Z. 117–125) fort — vermutlich wird ein Fehlverhalten behoben, bei dem der Starter-Prozess wie der Installationsprozess selbst überwacht wurde.
- Lizenz/Dependencies: `msTools.Updater` 0.11.0-rc.6 ist MIT-lizenziert (nuspec, unverändert zu rc.5); die Paket-Dependencies sind zu rc.5 identisch — die AGENTS.md-Regel für neue NuGet-Hauptversionen ist nicht betroffen.
- Dokumentation: je nach bestätigtem Änderungsinhalt minimal nachziehen — `docs/TECH_Auto_Update.md` (Abschnitt „Update-Installation unter Linux (systemd)", Z. 117–125, beschreibt derzeit `systemd-run --wait` so, als warte der Starter bis zum Unit-Ende), `docs/RELEASE_NOTES.md` (kurzer Eintrag zum rc.6-Bump in beiden Sprachabschnitten); `docs/help/updates.md` voraussichtlich unverändert.

## Konfiguration

Keine Konfigurationsänderung erforderlich. Der bestehende lokale Feed `lib/packages` wird weiter genutzt; die CI zieht denselben Feed, eine Workflow-Änderung ist nicht nötig (bewährt seit Upgrade V1). Es werden keine neuen `AutoUpdate`-Optionen eingeführt (der API-Vergleich zeigt keinen neuen Member in `AutoUpdateOptions`).

## Offene Fragen

- **Änderungsinhalt von rc.6:** Welcher konkrete Befund/Upstream-Issue mit rc.6 behoben wird, ist der Anforderung nicht zu entnehmen; die API-Erweiterung `MonitorStarterAsync` deutet auf eine Korrektur des in rc.5 eingeführten `systemd-run --wait`-Monitorings hin (Starter-Exit ≠ Skript-Ausgang). Zur Klärung: Release-Notes/Issues von `martin-stromberg/msTools.Updater` (im nuspec vermerkter Commit `8ff437a4320ecf1d0ea72176db6d82bdffbbd6f1`) heranziehen — davon hängt ab, ob die Doku-Aussage in `docs/TECH_Auto_Update.md` Z. 117–119 („Der Starter-Prozess wartet bis zum Unit-Ende und propagiert den Exit-Code") angepasst werden muss.
- **Dokumentations-Umfang:** Abhängig von der ersten Frage: nur Release-Notes-Eintrag oder auch Präzisierung des systemd-Abschnitts in `TECH_Auto_Update.md`.
