# Übersetzte Anforderung: `msTools.Updater` 0.11.0-rc.4 → 0.11.0-rc.5

## Fachliche Zusammenfassung

Die im Projekt eingebundene Self-Update-Bibliothek `msTools.Updater` soll von Version `0.11.0-rc.4` auf `0.11.0-rc.5` angehoben werden. Das neue Paket liegt bereits als `lib/packages/msTools.Updater.0.11.0-rc.5.nupkg` im lokalen NuGet-Feed des Repositories vor und ersetzt die bisherige Datei `msTools.Updater.0.11.0-rc.4.nupkg`. Es handelt sich um ein reines Bibliotheks-Upgrade ohne eigenes fachliches Feature im VideoWebPlayer; Ziel ist die Übernahme der Korrekturen der neuen Updater-Version — dasselbe Muster wie bei den früheren Upgrades rc.1→rc.2→rc.3→rc.4 (dokumentiert in `docs/features/task/issue-226-ca48a532dd7347a6b5e56388a105ef7f-update-ueberschreibt-webconfig-tasks.md`, Schritte V1, K5–K7).

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 59): `PackageReference` `msTools.Updater` `0.11.0-rc.4` → `0.11.0-rc.5`
- `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22): `PackageReference` analog erhöhen
- Lokaler NuGet-Feed `lib/packages` (Quelle `local` in `NuGet.config`): `msTools.Updater.0.11.0-rc.5.nupkg` committen (liegt derzeit untracked im Arbeitsverzeichnis), `msTools.Updater.0.11.0-rc.4.nupkg` entfernen
- Verbrauchender eigener Code (keine Änderung erwartet): `Extensions/AutoUpdateExtensions.cs`, `Services/Updates/` (`UpdateAdminService`, `UpdateBackupEventBinder`, `UpdateSettingsService`, `VideoWebPlayerUpdateSourceFactory`), `Components/Pages/Admin/Updates.razor`
- Update-bezogene Tests (nach dem Bump laufen lassen, ggf. anpassen): `MsToolsUpdaterIntegrationTests`, `UpdateBackupCoordinatorTests`, `UpdateBackupEventBinderTests`, `UpdateSettingsServiceTests(_Iis)`, `AutoUpdateProtectedFilesTests`, `UpdatesPageE2ETests`, `UpdatesControllerAuthorizationTests`. Besondere Aufmerksamkeit für Tests, die Marker im generierten Installationsskript assertieren (beim rc.4-Upgrade musste `AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder` angepasst werden, weil sich das Skript geändert hatte).
- Keine Datenmodell-, EF-Migrations- oder Interface-Änderungen im eigenen Code

## Implementierungsansatz

- Bewährtes Vorgehen der bisherigen Upgrades: Versionsangabe in beiden csproj-Dateien erhöhen, Paketdatei im lokalen Feed tauschen, `dotnet restore`, `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei, volle Testsuite `dotnet test VideoWebPlayer.Tests` grün.
- API-Delta rc.4 → rc.5 (verifiziert durch Vergleich der `lib/net10.0/msTools.Updater.xml` beider Pakete): rein additiv — neuer Enum-Wert `AutoUpdateErrorCode.InstallationVerificationInconclusive` und neue Methode `AutoUpdateOrchestrator.IsInstallWithinGracePeriod(AutoUpdateStatusSnapshot)`; keine entfernten Member → kein Breaking Change, Codeanpassung voraussichtlich nicht nötig.
- Annahme (zu verifizieren): Da das generierte Installationsskript Teil der Bibliothek ist, kann sich sein Inhalt zwischen Paketversionen ändern; skript-assertierende Tests sind daher nach dem Bump gezielt zu prüfen.
- Lizenz/Dependencies: `msTools.Updater` 0.11.0-rc.5 ist MIT-lizenziert (nuspec, unverändert zu rc.4); die Paket-Dependencies sind zu rc.4 identisch — die AGENTS.md-Regel für neue NuGet-Hauptversionen ist nicht betroffen.

## Konfiguration

Keine Konfigurationsänderung erforderlich. Der bestehende lokale Feed `lib/packages` wird weiter genutzt; die CI zieht denselben Feed, eine Workflow-Änderung ist nicht nötig (bewährt seit Upgrade V1).

## Offene Fragen

- **Änderungsinhalt von rc.5:** Die im Repository dokumentierten Upgrades hingen jeweils an konkreten Produktionsbefunden bzw. Updater-Issues. Welcher Befund/Issue in rc.5 umgesetzt wurde, ist der Anforderung nicht zu entnehmen; die neuen API-Member deuten auf eine Installations-Verifikation mit „unentschiedenem" Ergebnis plus Grace-Period-Prüfung hin. Zur Klärung: Release-Notes/Issues von `martin-stromberg/msTools.Updater` (im nuspec vermerkter Commit `bebedcf0862def9211361eda83d2f078e7977c1f`) heranziehen — davon hängt ab, ob Doku (`docs/TECH_Auto_Update.md`, `docs/help/updates.md`, `docs/RELEASE_NOTES.md`) angepasst werden muss.
- **Nutzung der neuen API:** Soll `AutoUpdateOrchestrator.IsInstallWithinGracePeriod` bzw. der neue Fehlercode `InstallationVerificationInconclusive` im eigenen Code (z. B. `UpdateAdminService`, Statusanzeige in `Updates.razor`, wo der Code derzeit generisch via `@status.LastErrorCode` ausgegeben wird, Z. 88) bewusst verwendet werden — oder bleibt die Änderung reine Bibliotheks-Interna?
