# Bestandsaufnahme: `msTools.Updater` 0.11.0-rc.4 → 0.11.0-rc.5

Bestandsaufnahme der `msTools.Updater`-Einbindung im VideoWebPlayer bezogen auf die Anforderung
`docs/features/task/b21855dd641a4ab49465023eeaee361d-mstoolsupdater0110-rc4/requirement.md`
(reines Bibliotheks-Upgrade des Self-Update-Subsystems).

## Zusammenfassung

- `msTools.Updater` `0.11.0-rc.4` ist per `PackageReference` in **zwei** Projektdateien referenziert:
  `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 59) und `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22).
- Der NuGet-Feed ist lokal: `NuGet.config` (Repo-Root) definiert `local` → `lib/packages` neben
  `nuget.org`; `<clear/>` deaktiviert alle anderen Quellen. CI und lokale Builds nutzen denselben
  Feed. `msTools.Updater.0.11.0-rc.5.nupkg` liegt bereits (untracked) in `lib/packages/`,
  `msTools.Updater.0.11.0-rc.4.nupkg` ist getrackt.
- Paketvergleich rc.4 ↔ rc.5 (verifiziert): nuspec unterscheidet sich nur in `version` und dem
  `repository`-Commit (`bebedcf0862def9211361eda83d2f078e7977c1f`); Lizenz MIT, Dependencies
  identisch, `README.md` identisch. API-Delta rein **additiv**: neuer Enum-Wert
  `AutoUpdateErrorCode.InstallationVerificationInconclusive` und neue Methode
  `AutoUpdateOrchestrator.IsInstallWithinGracePeriod(AutoUpdateStatusSnapshot)` — letztere nur auf
  der **konkreten Klasse**, nicht auf `IAutoUpdateOrchestrator`; der eigene Code konsumiert
  ausschließlich das Interface.
- Alle im eigenen Code/Tests verwendeten Updater-Typen sind in der rc.5-API-Dokumentation
  (`lib/net10.0/msTools.Updater.xml`) weiterhin vorhanden; keine entfernten Member.
- Eigenes Update-Subsystem vollständig vorhanden: Registrierung (`AutoUpdateExtensions`),
  Admin-Fassade (`UpdateAdminService`), Settings-Persistenz (`UpdateSettingsService` +
  `UpdateSettings`-Entity + Migration `AddUpdateSettings`), Pre-Install-Backup
  (`UpdateBackupEventBinder`/`UpdateBackupCoordinator`/`IUpdateBackupService`), IIS-Erkennung
  (`UpdateHostEnvironment`), Admin-UI (`Updates.razor` + `UpdatesController`, serverseitig per
  `AdminOnly`-Policy abgesichert). Keine Datenmodell- oder Interface-Änderung durch das Upgrade
  zu erwarten.
- Risikopunkt aus den Vor-Upgrades: `AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder`
  assertiert Marker im generierten Installationsskript — beim rc.4-Upgrade musste der Marker auf
  `Copy-AutoUpdateStagedFiles` angepasst werden. Ob das rc.5-Skript die Marker unverändert enthält,
  ist aus dem nupkg nicht direkt ablesbar (Skript ist in der DLL eingebettet).
- Dokumentations-Befund (bestehend, nicht durch diese Anforderung entstanden):
  `docs/TECH_Auto_Update.md` beschreibt in den Abschnitten „Einbindung der Bibliothek" (Z. 10–20)
  und „Release-Artefakte" (Z. 194–201) noch den alten Stand — DLL-Dateireferenz unter
  `lib/msTools.Updater/` statt nupkg-Feed `lib/packages`, sowie `.github/workflows/main-release.yml`
  und `.github/scripts/create-update-manifest.sh`, die im Repo nicht mehr existieren (tatsächlich:
  `.github/actions/build-and-package` + `release.yml`). Im Aufgaben-Dokument des letzten
  Upgrade-Projekts bereits als offener optionaler Punkt vermerkt.
- Test-Ausgangszustand: Builds in Debug **und** Release fehlerfrei (je 0 Fehler, 140 vorhandene
  Warnungen). Volle Testsuite grün: 2592 Nicht-E2E-Tests (2591 bestanden, 1 übersprungen) +
  248 E2E-Tests (247 bestanden, 1 übersprungen), **0 Fehlschläge**. Beide Überspringungen sind
  dokumentierte Absicht (Unix-only-Test auf Windows; bekanntes Alt-Problem mit `Skip`-Begründung).
  Nachweis: [inventory/tests.md](inventory/tests.md), Logs und TRX-Reports unter
  [inventory/test-results/](inventory/test-results/).

## Details

- [Pakete und NuGet-Feed](inventory/packages.md) — Feed-Konfiguration, Referenzen, nupkg-Vergleich rc.4↔rc.5, Upgrade-Historie
- [Datenmodell](inventory/models.md) — `UpdateSettings`-Entity, EF-Konfiguration, Backup-Registrierung, Daten-Records
- [Logik](inventory/logic.md) — konsumierende Klassen, DI-Registrierungen, Controller, Admin-UI, Konfiguration
- [Interfaces](inventory/interfaces.md) — `IUpdateSettingsService`, `IUpdateBackupService`, `IUpdateHostEnvironment`
- [Tests](inventory/tests.md) — Test-Ausgangszustand mit Läufen/Nachweisen und Übersicht der Update-Testklassen
