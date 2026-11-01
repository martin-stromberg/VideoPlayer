# Umsetzungsplan: `msTools.Updater` 0.11.0-rc.5 → 0.11.0-rc.6

## Übersicht

Die Self-Update-Bibliothek `msTools.Updater` wird im lokalen NuGet-Feed `lib/packages` und in beiden Projektdateien von `0.11.0-rc.5` auf `0.11.0-rc.6` angehoben — ein reines Bibliotheks-Upgrade ohne eigenes fachliches Feature. rc.6 korrigiert das unter Linux eingeführte `systemd-run`-Monitoring (Upstream-PR #80, Issue #78): Der kurzlebige Starter-Prozess meldet ein Self-Update nicht mehr fälschlich als fehlgeschlagen. Betroffen sind nur Paketreferenzen, der lokale Feed und die Technik-Doku bzw. Release Notes; eine Codeanpassung im eigenen Bestand ist nicht erforderlich (API-Delta rein additiv).

## Designentscheidungen

Keine — folgt bestehenden Mustern (bewährtes Vorgehen der bisherigen Upgrades rc.4→rc.5, PR #324).

## Programmabläufe

Es werden keine neuen Abläufe im eigenen Code implementiert. Das einzige geänderte Verhalten liegt innerhalb der Bibliothek und ist für die Doku-Anpassung maßgeblich:

### Update-Installation unter Linux (geändertes Bibliotheksverhalten rc.6, nur Dokumentationsgegenstand)

1. Der Updater startet das Installationsskript als transiente systemd-Unit über `systemd-run` **ohne** `--wait` — der Starter-Prozess endet bereits nach der Registrierung der Unit.
2. `DefaultAutoUpdateProcessRunner.MonitorStarterAsync(Process, bool)` (neu in rc.6) überwacht nur den Starter-Exit-Code, also ob die Unit registriert wurde — nicht den Skript-Ausgang. Im Gegensatz zu `MonitorAsync` (Windows-Pfad) berührt die Überwachung weder Installations-Lock (`update.lock`) noch persistierten Paket-Deskriptor.
3. Das Skript räumt Lock und Deskriptor selbst auf (`rm -f "$lock"` / `rm -f "$descriptor"` bzw. „Files replaced; lock and descriptor released." im Skript).
4. Skript-Ende und -Fehler laufen deterministisch über den `cleanup`-Trap, den `update.failed`-Marker im Update-Verzeichnis und die Karenz-Reconciliation (`IsInstallWithinGracePeriod`, `HealthTimeoutSeconds`) — unverändert zu rc.5.
5. Behobener Fehler: In rc.5 lief der Starter als Kind des Hosts mit `--wait` und starb beim `StopHostAfterScriptStart`-Shutdown per SIGTERM (Exit 143); `MonitorAsync` meldete das fälschlich als `InstallationFailed` und löschte das Lock vorzeitig, während die transient Unit unter PID 1 weitermachte.

Beteiligte Bibliothekstypen (nicht im eigenen Code referenziert): `DefaultAutoUpdateProcessRunner` (`MonitorStarterAsync`, `MonitorAsync`).

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

Keine. Das API-Delta rc.5 → rc.6 ist rein additiv (ein neuer öffentlicher Member `DefaultAutoUpdateProcessRunner.MonitorStarterAsync`), kein Member wurde entfernt, und der Typ wird im eigenen Code nirgends direkt referenziert. Die konsumierten API-Flächen (`UseAutoUpdate`, `AutoUpdateOptions`, `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`, `IAutoUpdateEventAggregator`, Status-Typen) sind unverändert.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine. Der lokale Feed `lib/packages` (Quelle `local` in `NuGet.config`) wird unverändert weiter genutzt; der API-Vergleich zeigt keinen neuen Member in `AutoUpdateOptions`.

## Seiteneffekte und Risiken

- **`AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder`:** Der Test assertiert Marker im bibliotheksgenerierten Windows-Skript und hängt damit indirekt an der Paketversion. Alle assertierten Marker (`$protectedFiles = @(`, `Backup-AutoUpdateProtectedFiles -Files`, `Copy-AutoUpdateStagedFiles -Source`, `Invoke-AutoUpdateProtectedFiles -Files`) sind in der rc.6-DLL verifiziert enthalten — die Anpassung entfällt voraussichtlich, ist aber über einen gefilterten Testlauf zu verifizieren.
- **Release-Build-Verhalten:** AGENTS.md §7 verlangt den Build-Nachweis in Debug **und** Release (Release weicht in der CI ab); beide Konfigurationen müssen fehlerfrei bauen.
- **NuGet-Auflösung:** Da die Versionszeichenfolge wechselt, entsteht ein neuer Cache-Eintrag — kein Risiko durch stale Caches. `project.assets.json` beider Projekte muss nach dem Restore `msTools.Updater/0.11.0-rc.6` auflösen; `MsToolsUpdaterIntegrationTests` läuft direkt gegen die aufgelöste DLL und belegt die Version im Test.
- **Paket-Hygiene:** Die alte, getrackte Datei `msTools.Updater.0.11.0-rc.5.nupkg` muss im selben Schritt entfernt werden, damit der Feed nicht zwei Updater-Versionen trägt.
- **Linux-Verhalten:** Die eigentliche Verhaltenskorrektur wirkt nur auf Linux-Installationen; Windows-/IIS-Pfade sind unverändert. Ein Live-Update-Test unter Linux kann wie beim rc.5-Upgrade über `chore:`-Prerelease-Commits nach dem Merge erfolgen (außerhalb dieses Plans).

## Umsetzungsreihenfolge

1. **Lokalen NuGet-Feed tauschen**
   - Voraussetzungen: `lib/packages/msTools.Updater.0.11.0-rc.6.nupkg` liegt bereits untracked im Arbeitsverzeichnis vor (verifiziert).
   - Beschreibung: `msTools.Updater.0.11.0-rc.6.nupkg` committen und `msTools.Updater.0.11.0-rc.5.nupkg` aus dem Feed entfernen.

2. **Paketreferenzen erhöhen**
   - Voraussetzungen: Schritt 1 (das rc.6-nupkg muss im Feed auflösbar sein).
   - Beschreibung: `Version="0.11.0-rc.5"` → `Version="0.11.0-rc.6"` in `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 59) und `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22).

3. **Restore und Build verifizieren**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: `dotnet restore VideoPlayer.sln` (muss `msTools.Updater/0.11.0-rc.6` auflösen), dann `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei (AGENTS.md §7).

4. **Gefilterte Update-Tests ausführen**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `MsToolsUpdaterIntegrationTests`, `AutoUpdateProtectedFilesTests`, `UpdateAdminServiceTests`, `UpdateSettingsServiceTests`, `UpdateSettingsServiceTests_Iis`, `UpdateBackupCoordinatorTests`, `UpdateBackupEventBinderTests`, `UpdatesControllerAuthorizationTests`, `UpdatesPageE2ETests` gefiltert laufen lassen — insbesondere den Risiko-Test `GeneratedScript_ContainsBackupAndMergeInOrder` verifizieren (Erwartung: unverändert grün, Marker in rc.6-DLL vorhanden).

5. **Volle Testsuite ausführen**
   - Voraussetzungen: Schritt 4 grün.
   - Beschreibung: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig grün (Nicht-E2E + E2E); einmalig flackernde Playwright-Tests einzeln wiederholen und im Bericht ehrlich nennen (AGENTS.md §7).

6. **`docs/TECH_Auto_Update.md` anpassen**
   - Voraussetzungen: Schritt 1–2 (fachlich unabhängig vom Testlauf, aber selbe Änderung).
   - Beschreibung: Den Absatz „Update-Installation unter Linux (systemd)" (Z. 117–125) auf die rc.6-Architektur umschreiben: `systemd-run` ohne `--wait`, Starter ist kurzlebig und endet nach Unit-Registrierung, `MonitorStarterAsync` prüft nur die Registrierung und meldet Starter-Fehler; Lock-Datei und Paket-Deskriptor räumt das Skript selbst auf; Fehlerpfad weiterhin über `cleanup`-Trap, `update.failed`-Marker und Karenz-Reconciliation. Der übrige preexisting-veraltete Bestand (Z. 10–19, alte Dateireferenz-Einbindung) bleibt wie beim rc.5-Upgrade bewusst unangetastet.

7. **`docs/RELEASE_NOTES.md` ergänzen**
   - Voraussetzungen: keine fachlichen — läuft im Dokumentations-Lifecycle-Schritt.
   - Beschreibung: Einen kurzen rc.6-Eintrag je Sprachabschnitt einfügen — im EN-Abschnitt „What's New" direkt unter der Überschrift (vor dem rc.5-Eintrag Z. 39), im DE-Abschnitt „Neuerungen" analog (vor Z. 177), nach dem Muster des rc.5-Eintrags: Bibliotheks-Bump, Korrektur des systemd-run-Starters (Self-Update nicht mehr fälschlich als fehlgeschlagen gemeldet; Lock-Handling korrigiert), Verweis auf Upstream-Issue [martin-stromberg/msTools.Updater#78](https://github.com/martin-stromberg/msTools.Updater/issues/78) (und PR #80). `docs/help/updates.md` bleibt unverändert.

8. **Markdown-Linkcheck ausführen**
   - Voraussetzungen: Schritte 6–7.
   - Beschreibung: `dotnet test tools/MarkdownLinkCheck.Tests` nach den Doku-Änderungen ausführen.

## Tests

### Neue Tests

Keine — ein reiner Versionsbump ohne eigenes Feature erfordert keine neuen Tests.

### Betroffene bestehende Tests

Keine — voraussichtlich ist keine Testanpassung erforderlich. Einziger Kandidat mit Paket-Abhängigkeit:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder` | Keine Anpassung erwartet — alle assertierten Skript-Marker sind in der rc.6-DLL verifiziert enthalten; gefilterter Testlauf (Schritt 4) verifiziert das. Nur falls der Test doch fehlschlägt: Marker-Sets an das rc.6-Skript angleichen, Reihenfolge-Absicherung *Backup → Copy → Merge* erhalten. |

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich. Begründung: Die Anforderung berührt keinen Benutzerfluss — es ist ein reiner Versionsbump einer internen Bibliothek ohne UI- oder API-Oberflächenänderung im eigenen Code. Das einzige geänderte Verhalten (Linux-`systemd-run`-Monitoring) läuft vollständig innerhalb der Bibliothek auf dem Linux-Installationspfad, der auf dem Windows-Test-Host über keinen Benutzerfluss auslösbar ist. Die bestehenden `UpdatesPageE2ETests` decken den Admin-Update-Fluss weiterhin ab und müssen unverändert grün bleiben (Teil der gefilterten Tests in Schritt 4 und der Voll-Suite in Schritt 5).

Betroffene bestehende E2E-Tests: Keine.

## Offene Punkte

Keine.
