# Tests

## Test-Ausgangszustand vor der Umsetzung (Runde 2)

- Zeitpunkt (mit Zeitzone): 2026-10-03, Läufe ca. 21:15–21:23 +0200 (MESZ)
- Branch und Commit-ID: `task/issue-226-ca48a532dd7347a6b5e56388a105ef7f-update-ueberschreibt-webconfig` @ `b319a938928545f62565aefcf2755607192d959d` („docs: Anforderung an msTools.Updater fuer update-sicheren Dateierhalt (Issue #226)")
- Uncommittete Änderungen im getesteten Stand: nur untracked Dateien — `lib/packages/msTools.Updater.0.11.0-rc.1.nupkg` (noch nicht referenziert; Build/Test liefen gegen `0.10.4-rc.1`) und `docs/features/task/issue-226-…/` (requirement.md, todo.md, diese Bestandsaufnahme). Kein Produktivcode, keine Teständerungen.
- Testumgebung und Runtime-/SDK-Versionen: Windows 10.0.26200 (win-x64), .NET SDK 10.0.401 (Projekt `net10.0`), xUnit v3, Playwright-Chromium vorhanden — unverändert zu Runde 1.
- Ermittelte Testsuiten und Quellen der Testbefehle: wie Runde 1 — `VideoWebPlayer.Tests` (xUnit v3), CI-Filter aus `staging-ci.yml` (`Category!=E2E`, `Category=E2E`); DoD-Befehl `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (AGENTS.md §7). `tools/MarkdownLinkCheck.Tests` erneut nicht gelaufen (Doku-Suite, thematisch erst in V5 relevant).

### Build-Nachweis

`dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release -p:NoWarn=NU1903` — **0 Fehler, 140 Warnungen** (v. a. `BL0008` in Razor-Seiten; identisch zu Runde 1). Der Build lief gegen `msTools.Updater` `0.10.4-rc.1` (Referenzen noch unverändert — korrekt, da der Test-Ausgangszustand den Stand **vor** V1 beschreibt).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=inventory-r2-non-e2e.trx" --results-directory docs/features/task/issue-226-…/inventory/test-results` | Repo-Root | 0 | 1402 | 0 | 0 | [TRX](test-results/inventory-r2-non-e2e.trx), [Konsole](test-results/non-e2e-console.log) |
| 2 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E" --logger "trx;LogFileName=inventory-r2-e2e.trx" --results-directory docs/features/task/issue-226-…/inventory/test-results` | Repo-Root | 0 | 204 | 0 | 1 | [TRX](test-results/inventory-r2-e2e.trx), [Konsole](test-results/e2e-console.log) |

Gesamt: **1607 Tests, 1606 bestanden, 1 übersprungen, 0 fehlgeschlagen** — exakt das Runde-1-Ergebnis (Läufe decken zusammen die volle Suite ab).

### Nachgewiesene bestehende Testfehler

**Keine.** Beide Läufe ohne Fehlschlag.

### Testlücken und Ausführungsprobleme

- **1 absichtlich übersprungener E2E-Test** (unverändert, nicht auftragsbezogen): `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — statischer `[Fact(Skip = …)]`, dokumentierter Produktfehler `api/continue-watching` → 500 (siehe Runde-1-`tests.md` für Details).
- **`Category=Integration` bleibt eine tote CI-Stufe** (kein Test trägt das Trait) — unverändert.
- **`tools/MarkdownLinkCheck.Tests` nicht ausgeführt** — unverändert; bei V5-Dokuänderungen relevant.
- E2E-Lauf stabil, keine Flakes beobachtet.

## Testklassen (auftragsrelevant, Delta)

Details aller Updater-Testklassen in Runde 1 (`inventory/tests.md` @ `8aafb9f`). Delta/Verifikation für V4:

### `MsToolsUpdaterIntegrationTests` (`VideoWebPlayer.Tests/Services/MsToolsUpdaterIntegrationTests.cs`, 146 Zeilen)

- **Direktes Muster für V4:** `Host.CreateApplicationBuilder` mit `ContentRootPath` = Temp-Dir + `builder.UseAutoUpdate(cfg => cfg.WithDownloadPath(…).UseLocalFolderSource(…).DisableHostedServices())` (Z. 70–81), danach `builder.Services.BuildServiceProvider()` und Auflösung von `AutoUpdateStatusService`, `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler` (Z. 83–86).
- Für einen **Bindungstest** (`AutoUpdate:ProtectedFiles` → `AutoUpdateOptions.ProtectedFiles`): `builder.Configuration.AddInMemoryCollection(...)` vor `UseAutoUpdate` und `GetRequiredService<AutoUpdateOptions>()` — der Options-Singleton steht nach `UseAutoUpdate` in DI.
- Für einen **Skript-Generierungstest** (`IAutoUpdateScriptGenerator`): `GetRequiredService<IAutoUpdateScriptGenerator>()` und `GenerateAsync(descriptor, zipPath, target)` — `AutoUpdateInstallationTarget` ist ein öffentliches Record (`platform`/`serviceName`/`executablePath`/`appPoolName`/`siteName`); Skript landet unter `<DownloadPath>/pending/update.ps1` (Windows) bzw. `update.sh` (Linux) und ist als Text assertbar (Backup-Block vor `Copy-Item`, Merge-Block danach, `$backup`-Pfad). Plattform ist `AutoUpdatePlatformResolver.CurrentPlatform` (laufendes OS) — für ein plattformübergreifendes Windows-Skript-Assert ist `IAutoUpdatePlatformResolver` vor `UseAutoUpdate` durch eine Testimplementierung zu ersetzen (TryAdd → vorherige Registrierung gewinnt, Muster aus Runde 1 dokumentiert).
- Windows-Gating-Muster im Repo: `OperatingSystem.IsWindows()`-Early-Return/Verzweigung (`LocalMediaSourceReaderTests.cs:300`, `TestHelpers.cs:263/275`, `MsToolsUpdaterIntegrationTests.cs:126`) — kein `SkippableFact`-Trait.
- Ausführungstest des generierten `update.ps1` (optional in V4): bräuchte `AutoUpdateInstallationTarget` mit `ExecutablePath`/`ServiceName` — das Skript erwartet gestoppten/ startbaren Dienst oder Exe; Vollausführung im Test ist aufwendig (realer Service/AppPool), eher Skript-**Inhalts**test als Ausführung.

### `UpdateSettingsServiceTests` (`VideoWebPlayer.Tests/Services/UpdateSettingsServiceTests.cs`)

- In-Memory-Konfiguration per `AddInMemoryCollection` mit `AutoUpdate:*`-Schlüsseln (Z. 22–31, 198–201) + `new AutoUpdateOptions()` — Muster für reine Binding-Asserts ohne Host.
- **Prüfpunkt für V4:** `ApplyToRuntimeOptions`-Regression — ein Test könnte festhalten, dass `ProtectedFiles` durch `UpdateSettingsService` unberührt bleibt (z. B. Options mit gefüllter Liste → `ApplyToRuntimeOptionsAsync` → Liste unverändert).

### `ApiTokenConfigurationTests` (`VideoWebPlayer.Tests/ApiTokenConfigurationTests.cs`)

- `WebApplication.CreateBuilder` + `AddInMemoryCollection` + `AddVideoWebPlayerServices` — Muster, falls der Bindungstest die echte `AddVideoWebPlayerAutoUpdate`-Verdrahtung (statt nacktem `UseAutoUpdate`) prüfen soll.

## Neue Testbereiche für V4 (Einordnung)

| Prüfung | Ansatz | Ort |
|---------|--------|-----|
| `AutoUpdate:ProtectedFiles`-Bindung (Strategy/XmlElements/XmlAttributes/JsonKeys landen in `AutoUpdateOptions`) | `Host.CreateApplicationBuilder` + `AddInMemoryCollection` + `UseAutoUpdate` → `GetRequiredService<AutoUpdateOptions>()` | neue Testklasse, z. B. `VideoWebPlayer.Tests/Services/AutoUpdateProtectedFilesTests.cs` |
| Schutzliste aus der echten `appsettings.json` des Projekts bindet fehlerfrei (Validierung, keine `OptionsValidationException`) | `AddVideoWebPlayerAutoUpdate` über `WebApplication.CreateBuilder` mit Repo-Config oder JSON-Datei parsen + `Bind` | ebenda |
| Skriptinhalt: Backup- vor Kopiervorgang, Merge/Restore danach, Backup-Pfad `…/backup`, Einträge serialisiert | `IAutoUpdateScriptGenerator` per DI (ggf. `IAutoUpdatePlatformResolver`-Testdouble für Windows-Skript unter Windows) | ebenda |
| Validator-Verhalten (Merge ohne Regeln → Fehler) | `AutoUpdateOptionsValidator().Validate(...)` direkt | ebenda |
| Windows-Ausführungstest `update.ps1` | `OperatingSystem.IsWindows()`-Gating; aufwendig, optional | ebenda |

## Hilfsmethoden (unverändert)

Siehe Runde 1 (`PairingWebApplicationFactory`, `TestHelpers`, `ListLogger<T>`, `PairingTestDb`, `TestWebHostEnvironment`). Für `IAutoUpdateEnvironment`/`IAutoUpdatePackageStore` existieren Bibliotheks-Implementierungen (`HostAutoUpdateEnvironment(IHostEnvironment)`, `FileSystemAutoUpdatePackageStore(environment, options, TimeProvider)`), die direkt instanziierbar sind — alternativ DI-Auflösung nach `UseAutoUpdate`.
