# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `PackageReference` `msTools.Updater` in `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 59) auf `0.11.0-rc.5` heben | Schritt 1 + Konfigurationstabelle (verifiziert: Z. 59 enthält tatsächlich `0.11.0-rc.4`) | Build Debug+Release (Schritt 2), volle Suite (Schritt 4) | Abgedeckt |
| `PackageReference` in `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22) analog heben | Schritt 1 + Konfigurationstabelle (verifiziert: Z. 22) | Build + volle Suite | Abgedeckt |
| `lib/packages/msTools.Updater.0.11.0-rc.5.nupkg` committen, `...rc.4.nupkg` entfernen | Schritt 1 + Konfigurationstabelle (verifiziert: rc.5-nupkg liegt untracked vor, rc.4 getrackt) | `dotnet restore` in Schritt 1; CI-Risiko „Feed-Vollständigkeit" explizit benannt | Abgedeckt |
| Verbrauchender eigener Code unverändert (`AutoUpdateExtensions`, `Services/Updates/*`, `Updates.razor`) | „Änderungen an bestehenden Klassen: Keine"; begründet über additives API-Delta (aus Inventory verifiziert: `IsInstallWithinGracePeriod` nur auf konkreter Klasse, eigener Code nutzt nur `IAutoUpdateOrchestrator`) | Kompilieren als Nachweis + alle konsumierenden Testklassen im gefilterten Lauf (Schritt 3) | Abgedeckt |
| Update-bezogene Tests nach dem Bump laufen lassen, ggf. anpassen | Schritt 3: gefilterter Lauf über alle acht genannten Testklassen (`MsToolsUpdaterIntegrationTests`, `AutoUpdateProtectedFilesTests`, `UpdateAdminServiceTests`, `UpdateSettingsServiceTests`, `UpdateSettingsServiceTests_Iis`, `UpdateBackupCoordinatorTests`, `UpdateBackupEventBinderTests`, `UpdatesControllerAuthorizationTests`, `UpdatesPageE2ETests`) — vollständig gegen Requirement + `inventory/tests.md` | Gefilterter Lauf ist der Testnachweis | Abgedeckt |
| Skript-assertierender Test `GeneratedScript_ContainsBackupAndMergeInOrder` besonders prüfen | Schritt 3 + „Seiteneffekte und Risiken" + Tabelle „Betroffene bestehende Tests" mit konkreten Markern (verifiziert gegen `AutoUpdateProtectedFilesTests.cs` Z. 96–161: Marker `$protectedFiles = @(`, `Copy-AutoUpdateStagedFiles -Source $staging -Destination $app` etc. stimmen). Vorgabe: an rc.5-Skript anpassen, Reihenfolge-Absicherung Backup→Copy→Merge nicht abschwächen, neue Skript-Härtungen bei Marker-Wahl berücksichtigen | Der Test selbst ist der Nachweis; Anpassungsvorgehen konkret beschrieben | Abgedeckt |
| Keine Datenmodell-/EF-Migrations-/Interface-Änderung | Explizit „Keine" in allen drei Abschnitten | — (Nicht-Anforderung, kein Nachweis nötig) | Abgedeckt |
| `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei (AGENTS.md DoD) | Schritt 2 mit explizitem AGENTS.md-Verweis | Build-Läufe | Abgedeckt |
| Volle Testsuite `dotnet test VideoWebPlayer.Tests` grün (AGENTS.md DoD) | Schritt 4, inkl. Ausgangszustand 2838/2840 + 2 dokumentierte Skips aus `inventory/tests.md`; jeder neue Fehlschlag ist zu untersuchen | Voller Lauf | Abgedeckt |
| Keine neuen NuGet-Hauptversionen ohne Lizenz-/Release-/CI-Prüfung (AGENTS.md) | Konfigurationsabschnitt: MIT unverändert, Dependencies identisch, gleicher lokaler Feed/CI — Regel nicht betroffen (verifiziert gegen `inventory/packages.md`) | — | Abgedeckt |
| Offene Frage „Änderungsinhalt rc.5" klären | „Programmabläufe": verifiziert über Upstream-Tag `v0.11.0-rc.5`, Issues #76/#78, PRs #77/#79 (Metadaten-Fallback, `systemd-run --wait`, Karenzfenster, Skript-Härtung) | — | Abgedeckt |
| Offene Frage „Nutzung der neuen API" entscheiden | Designentscheidung: reiner Bump, keine gezielte Nutzung von `InstallationVerificationInconclusive`/`IsInstallWithinGracePeriod` (Anwender-Entscheidung); generische Anzeige in `Updates.razor` verifiziert (Z. 86–88, `Code: @status.LastErrorCode` gegen Repo geprüft) | `UpdatesPageE2ETests` laufen als Regression mit | Abgedeckt |
| Doku-Bedarf prüfen (`TECH_Auto_Update.md`, `docs/help/updates.md`, `RELEASE_NOTES.md`) | Schritt 5: `RELEASE_NOTES.md` in beiden Sprachabschnitten (verifiziert: „What's New" und „Neuerungen" existieren); `TECH_Auto_Update.md` minimal im rc.5-betroffenen Umfang (verifiziert: überholte `release-metadata.json`-Aussage steht Z. 197–199); `docs/help/updates.md` bewusst unverändert mit Begründung; danach `dotnet test tools/MarkdownLinkCheck.Tests` (Projekt existiert) | MarkdownLinkCheck nach Doku-Änderung | Abgedeckt |
| Keine Konfigurations-/CI-Änderung nötig | Konfigurationsabschnitt: `NuGet.config`/Feed unverändert (verifiziert), Workflows ziehen denselben Feed | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine (Status: Plan vollständig).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kein neuer oder geänderter Benutzerfluss — reiner Bibliotheks-Versionswechsel ohne Änderung an UI, Endpunkten oder eigenem Verhalten | Keine neuen E2E-Tests; die 6 bestehenden `UpdatesPageE2ETests` (Seitenstruktur, Check, Settings-Roundtrip, Install, Sperrlogik, Mobile) laufen im vollen Lauf als Regression mit | Nicht erforderlich — nachvollziehbare Begründung im Plan vorhanden; der einzige potenziell sichtbare Unterschied (neuer Fehlercode `InstallationVerificationInconclusive` in der Statusanzeige) ist kein eigener Benutzerfluss und wird bereits generisch dargestellt (verifiziert: `Updates.razor` Z. 86–88) |

## Fehlende oder unvollständige Planbestandteile

Keine (Status: Plan vollständig).

## Hinweise

- **Preexisting-Dokubefund außerhalb der Inventory-Liste:** `docs/GUIDE_Installation.md` beschreibt an Z. 16 und Z. 230–232 ebenfalls noch die veraltete DLL-Dateireferenz `lib/msTools.Updater/msTools.Updater.dll` (vor der Umstellung auf `PackageReference` + nupkg-Feed, Commit `20660cf`). Gleicher Befund-Typ wie der in der Inventory genannte `TECH_Auto_Update.md`-Altbestand und ebenfalls nicht Teil dieser Anforderung — kann bei Bedarf in Schritt 5 mit erwähnt oder als separater Doku-Befund beim Anwender gemeldet werden.
- **Flaky-E2E-Regel (AGENTS.md):** Schritt 4 formuliert „jeder neue Fehlschlag ist dem Bump zuzuordnen" — bei einem einmaligen Playwright-Ausfall gilt AGENTS.md: einzeln mehrfach wiederholen und ehrlich im Bericht nennen, nicht pauschal dem Bump zuschreiben und keinesfalls Tests abschwächen.
- **`BackupSettingsServiceTests` und Hosts mit `AutoUpdate:HostedServicesEnabled=false`** (aus `inventory/tests.md` „Weitere Dateien mit Updater-Bezug") stehen nicht im gefilterten Lauf von Schritt 3, werden aber durch den vollen Lauf in Schritt 4 abgedeckt — kein Handlungsbedarf.
- **Marker-Anpassung in Schritt 3:** Die im Plan verankerte Vorgabe, die Reihenfolge-Absicherung *Backup → Copy → Merge* nicht abzuschwächen, ist der entscheidende Punkt — bei der Marker-Wahl auf das tatsächliche rc.5-Skript achten (Präzedenzfall rc.4, dort musste der Kopiermarker auf `Copy-AutoUpdateStagedFiles` angepasst werden).
