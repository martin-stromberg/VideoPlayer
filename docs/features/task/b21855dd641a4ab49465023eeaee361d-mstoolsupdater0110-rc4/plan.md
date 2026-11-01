# Umsetzungsplan: `msTools.Updater` 0.11.0-rc.4 → 0.11.0-rc.5

## Übersicht

Reines Bibliotheks-Upgrade des Self-Update-Subsystems: `msTools.Updater` wird von `0.11.0-rc.4`
auf `0.11.0-rc.5` angehoben. Betroffen sind die zwei `PackageReference`-Einträge
(`VideoWebPlayer/VideoWebPlayer.csproj` Z. 59, `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
Z. 22) und der Bestand des lokalen NuGet-Feeds `lib/packages`. Der Änderungsinhalt von rc.5 ist
inzwischen geklärt (Upstream-Issues #76/#78, PRs #77/#79 — Update-Verifikation und
Linux-Update-Monitoring robuster, siehe „Programmabläufe"); das API-Delta bleibt rein additiv —
keine Code-, Datenmodell- oder Interface-Änderung im eigenen Code. Erwartete Nacharbeit: eine
sehr wahrscheinliche Anpassung des skript-assertierenden Tests (das generierte
Installationsskript hat sich substanziell geändert) sowie ein kurzer Eintrag in
`docs/RELEASE_NOTES.md`.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Nutzung der neuen rc.5-API | Reiner Bump — keine gezielte Verwendung von `AutoUpdateErrorCode.InstallationVerificationInconclusive` im eigenen Code | Anwender-Entscheidung. `AutoUpdateOrchestrator.IsInstallWithinGracePeriod` ist ohnehin `private` (interner Bestandteil der Reconciliation, keine öffentliche API). Der neue Fehlercode wird in `Updates.razor` (Z. 86–88) bereits generisch als `Code: @status.LastErrorCode` angezeigt — verifiziert, kein Mapping nötig. |

## Programmabläufe

Es werden keine eigenen Programmabläufe neu implementiert oder geändert — das Upgrade ändert nur
bibliotheksinterne Abläufe. Zur Einordnung der Auswirkungen (verifizierter Inhalt von rc.5, Tag
`v0.11.0-rc.5` im Repo `martin-stromberg/msTools.Updater`):

### Bibliotheksinterne Änderungen rc.5

**PR #77 (Issue #76) — „Update-Verifikation robuster bei fehlender `release-metadata.json`":**

1. Metadaten-Fallback im Installationsskript (Windows und Linux): Pakete ohne
   `release-metadata.json` erhalten eine aus dem Paket-Deskriptor generierte Datei — die
   installierte Version bleibt damit verifizierbar.
2. Neuer Fehlercode `AutoUpdateErrorCode.InstallationVerificationInconclusive` (einziger neuer
   öffentlicher API-Bestandteil): Das Installationsskript lief fehlerfrei durch, die installierte
   Version konnte aber nicht ermittelt werden. Er erscheint als `LastErrorCode`/`LastError` im
   `AutoUpdateStatusSnapshot` und wird in `Updates.razor` (Z. 86–88) generisch angezeigt.
3. Schleifenschutz: Der persistierte Paket-Deskriptor wird bei Inconclusive verworfen; ein
   Warn-Log meldet, wenn die Pending-Version nicht neuer als die installierte Version ist.

**PR #79 (Issue #78) — „Linux-Update-Monitoring robust":**

1. `systemd-run --wait`: Der Starter-Prozess läuft bis zum Unit-Ende mit und propagiert den
   Exit-Code; der Monitor räumt Lock/Deskriptor erst nach Skriptende auf (gleiches Verhalten wie
   der Windows-WMI-Pfad).
2. Karenzfenster in `ReconcileStatusAsync`/`RecoverWorkspaceIfSafeAsync` über die neue `private`
   Hilfsmethode `IsInstallWithinGracePeriod` (`AutoUpdateOrchestrator.cs` Z. 565): Solange
   `State=Installing` und `LastInstallResult.StartedAt` jünger als `HealthTimeoutSeconds` ist,
   erfolgt kein Workspace-Cleanup und kein Fehlverdikt; ein `update.failed`-Marker führt sofort
   zu `Failed`.
3. `update.failed`-Marker neu auch unter Linux (via cleanup-Trap im Skript).
4. Skript-Härtung beider Plattformen: Abbruch, wenn `staging` nach dem Entpacken leer ist;
   Versionsprüfung der installierten `release-metadata.json` gegen die Paketversion vor dem
   Dienst-Start.

Beteiligte Klassen/Komponenten: nur Bibliothek (`AutoUpdateOrchestrator`, `AutoUpdateErrorCode`,
generiertes Installationsskript). Der eigene Code konsumiert ausschließlich
`IAutoUpdateOrchestrator` — `UpdateAdminService`, `Updates.razor` und `FakeUpdateBackend` bleiben
unverändert.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

Keine — das Upgrade ändert nur Projektdateien (`PackageReference`-Versionen) und den Feed-Bestand,
keine C#-Klassen.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `PackageReference` `msTools.Updater` in `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 59) | Projektdatei | `0.11.0-rc.5` (statt `0.11.0-rc.4`) | Versions-Bump der eingebundenen Bibliothek |
| `PackageReference` `msTools.Updater` in `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22) | Projektdatei | `0.11.0-rc.5` (statt `0.11.0-rc.4`) | Versions-Bump im Testprojekt (Integrationstests gegen die echte Bibliothek) |
| `lib/packages/msTools.Updater.0.11.0-rc.5.nupkg` | lokaler Feed | committen (derzeit untracked) | Paketquelle für die neue Version |
| `lib/packages/msTools.Updater.0.11.0-rc.4.nupkg` | lokaler Feed | entfernen | Alt-Paket ersetzen (bewährtes Vorgehen der Vor-Upgrades) |

Keine `appsettings`- oder Konfigurationsklassen-Änderung. Der lokale Feed `lib/packages`
(`NuGet.config`, Quelle `local`) wird unverändert weitergenutzt; CI-Workflows benötigen keine
Anpassung (bewährt seit Upgrade V1). Lizenz MIT unverändert, Paket-Dependencies identisch zu rc.4
(aus `inventory/packages.md` übernommen) — die AGENTS.md-Regel für neue NuGet-Hauptversionen ist
nicht betroffen.

## Seiteneffekte und Risiken

- **Skript-Marker-Test (sehr wahrscheinlicher Bruch):**
  `AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder`
  (`VideoWebPlayer.Tests/Services/AutoUpdateProtectedFilesTests.cs` Z. 96–161) assertiert Marker
  im von der Bibliothek generierten Windows-Installationsskript. Das Skript hat sich in rc.5
  substanziell geändert (Metadaten-Fallback, Abbruch bei leerem `staging`, Versionsprüfung der
  installierten `release-metadata.json` vor dem Dienst-Start, `update.failed`-Trap). Gefährdet
  sind insbesondere die drei Positionsmarker `Backup-AutoUpdateProtectedFiles -Files`,
  `Copy-AutoUpdateStagedFiles -Source $staging -Destination $app` und
  `Invoke-AutoUpdateProtectedFiles -Files` (Z. 154–156) — die neuen Skriptabschnitte dürfen die
  assertierte Reihenfolge *Sicherung → Paketkopiervorgang → Merge* nicht verändern. Der Test
  erzeugt nur das Windows-Skript; die Linux-spezifischen Änderungen (`systemd-run --wait`,
  `update.failed`-Trap) werden von keinem Test assertiert. Präzedenzfall: beim rc.4-Upgrade
  musste der Kopiermarker auf `Copy-AutoUpdateStagedFiles` angepasst werden.
- **Neuer Fehlercode anwendersichtbar:** `InstallationVerificationInconclusive` kann im
  Statusbereich von `Updates.razor` als generischer `Code:`-Text erscheinen — verifiziert bereits
  abgedeckt (Z. 86–88), aber neu für Anwender sichtbar, falls er auftritt.
- **Feed-Vollständigkeit:** Wird das rc.5-nupkg nicht committet oder das rc.4-nupkg nicht entfernt,
  schlägt der CI-Restore fehl bzw. bleibt Alt-Bestand liegen — alle Workflows
  (`staging-ci.yml`, `pr-staging-ci.yml`, `release.yml`) ziehen denselben lokalen Feed.
- **Positionskonstruktor `AutoUpdateStatusSnapshot`:** Wird in `FakeUpdateBackend`
  (`UpdatesPageE2ETests`), `UpdateAdminServiceTests` und `UpdatesControllerAuthorizationTests`
  direkt aufgebaut. Da das API-Delta additiv ist, ist kein Bruch zu erwarten; eine Form-Änderung
  wäre kompilierbrechend und sofort im Build sichtbar.
- **Dokumentations-Altbestand:** `docs/TECH_Auto_Update.md` beschreibt weiterhin die veraltete
  DLL-Dateireferenz (`lib/msTools.Updater/`) und nicht mehr existente Workflow-Dateien —
  bestehender Befund, nicht Teil dieser Anforderung. Zusätzlich ist die Aussage zu
  `release-metadata.json` (Z. 197–199: „Ohne diese Datei kann der Updater die installierte
  Version nicht ermitteln und lädt kein Update") durch den rc.5-Metadaten-Fallback überholt —
  diese Stelle wird im Rahmen des Auftrags minimal nachgezogen (siehe Schritt 5).

## Umsetzungsreihenfolge

1. **Versions-Bump und Feed-Tausch**
   - Voraussetzungen: `lib/packages/msTools.Updater.0.11.0-rc.5.nupkg` liegt vor (erfüllt — derzeit
     untracked im Arbeitsverzeichnis).
   - Beschreibung: `Version="0.11.0-rc.5"` in beiden csproj-Dateien setzen; neues nupkg committen,
     `msTools.Updater.0.11.0-rc.4.nupkg` löschen; `dotnet restore VideoPlayer.sln`.

2. **Builds in Debug und Release**
   - Voraussetzungen: Schritt 1 (Paket aufgelöst).
   - Beschreibung: `dotnet build VideoPlayer.sln -c Debug` und `-c Release` — beide müssen
     fehlerfrei sein (AGENTS.md: Release verhält sich anders, wurde bei früheren Paket-Problemen
     entscheidend).

3. **Update-bezogene Tests gefiltert ausführen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `dotnet test VideoWebPlayer.Tests` mit Filter auf die Update-Testklassen
     (`MsToolsUpdaterIntegrationTests`, `AutoUpdateProtectedFilesTests`, `UpdateAdminServiceTests`,
     `UpdateSettingsServiceTests`, `UpdateSettingsServiceTests_Iis`, `UpdateBackupCoordinatorTests`,
     `UpdateBackupEventBinderTests`, `UpdatesControllerAuthorizationTests`, `UpdatesPageE2ETests`).
     Schlägt `GeneratedScript_ContainsBackupAndMergeInOrder` fehl (nach dem rc.5-Skriptumbau sehr
     wahrscheinlich), das erzeugte Skript gegen die bisherigen Marker prüfen und den Test auf das
     rc.5-Skript anpassen (selbes Muster wie beim rc.4-Upgrade; kein Abschwächen des Tests — die
     Reihenfolge-Absicherung *Backup → Copy → Merge* muss erhalten bleiben; die neuen
     Skript-Härtungen wie Staging-Leer-Abbruch und Versionsprüfung sind bei der Marker-Wahl zu
     berücksichtigen).

4. **Volle Testsuite**
   - Voraussetzungen: Schritt 3 (inkl. etwaiger Marker-Anpassung).
   - Beschreibung: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig
     (Nicht-E2E + E2E) grün. Ausgangszustand: 2838/2840 grün, 2 dokumentierte Skips — jeder neue
     Fehlschlag ist dem Bump zuzuordnen und zu untersuchen.

5. **Dokumentation**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: `docs/RELEASE_NOTES.md` in beiden Sprachabschnitten („What's New" ab Z. 37
     und „Neuerungen" ab Z. 174) um einen kurzen Eintrag ergänzen: Updater-Bibliothek auf
     0.11.0-rc.5 angehoben — Update-Verifikation bei fehlender `release-metadata.json` robuster
     (Metadaten-Fallback, neuer Fehlercode `InstallationVerificationInconclusive`) und
     Linux-Update-Monitoring zuverlässiger (`systemd-run --wait`, `update.failed`-Marker nun
     auch unter Linux, Karenzfenster gegen Fehlverdikte); für Anwender kaum sichtbar, interner
     Robustheitsfix — mit Verweis auf die Upstream-Issues
     [martin-stromberg/msTools.Updater#76](https://github.com/martin-stromberg/msTools.Updater/issues/76)
     und [#78](https://github.com/martin-stromberg/msTools.Updater/issues/78).
     Zusätzlich `docs/TECH_Auto_Update.md` minimal im betroffenen Umfang nachziehen: die
     `release-metadata.json`-Beschreibung (Z. 197–199) um den neuen Fallback ergänzen
     (Pakete ohne die Datei erhalten eine aus dem Paket-Deskriptor generierte Datei; ist die
     Version danach nicht ermittelbar, gilt `InstallationVerificationInconclusive` statt eines
     stillen Erfolgs) und den `update.failed`-Marker bzw. `systemd-run --wait` beim
     Linux-Monitoring erwähnen. Der übrige preexisting-veraltete Bestand der Datei bleibt
     unangetastet (außerhalb des Auftrags). `docs/help/updates.md` bleibt unverändert — der Fix
     ist für Anwender nicht sichtbar und die generische Fehlercode-Anzeige ist dort bereits
     beschrieben. Danach `dotnet test tools/MarkdownLinkCheck.Tests` ausführen.

## Tests

### Neue Tests

Keine — das Upgrade bringt kein neues eigenes Feature. Die vorhandene Suite (Integrationstests
gegen die echte Bibliothek, Skript-Marker-Test, E2E der Update-Seite) ist die Absicherung des
Bumps.

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder` (`VideoWebPlayer.Tests/Services/AutoUpdateProtectedFilesTests.cs` Z. 96–161) | Assertiert Marker (`$protectedFiles = @(`, `Path = 'web.config'`, `Path = 'appsettings*.json'`, `Strategy = 'Merge'`, XPath-/JsonKey-Inhalte, `PackageStore.RootDirectory/backup`) und die Reihenfolge `Backup-AutoUpdateProtectedFiles -Files` → `Copy-AutoUpdateStagedFiles -Source $staging -Destination $app` → `Invoke-AutoUpdateProtectedFiles -Files` im generierten Windows-Skript. Das rc.5-Skript ist substanziell umgebaut (Metadaten-Fallback, Staging-Leer-Abbruch, Versionsprüfung, `update.failed`-Trap) — Anpassung sehr wahrscheinlich, konkrete Marker nach dem ersten Lauf gegen das tatsächliche Skript festlegen (Präzedenzfall rc.4). Die Reihenfolge-Absicherung muss erhalten bleiben. |

Alle übrigen Update-Testklassen laufen unverändert als Regression mit; ein Bruch ist nicht zu
erwarten (additives API-Delta, alle im eigenen Code verwendeten Updater-Typen in rc.5 vorhanden).

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich. Begründung: Die Anforderung berührt keinen Benutzerfluss — es
handelt sich um einen reinen Bibliotheks-Versionswechsel ohne Änderung an UI, Endpunkten oder
eigenem Verhalten. Der einzige potenziell anwendersichtbare Unterschied (neuer Fehlercode in der
Statusanzeige) ist kein eigener Benutzerfluss und wird bereits generisch dargestellt (verifiziert:
`Updates.razor` Z. 86–88). Die bestehenden `UpdatesPageE2ETests` (6 Playwright-Tests:
Seitenstruktur, Check, Settings-Roundtrip, Install, Sperrlogik, Mobile) decken den
Update-Admin-Fluss ab und laufen im vollen Lauf als Regression mit.

Bestehende E2E-Tests anzupassen: Keine.

## Offene Punkte

Keine.
