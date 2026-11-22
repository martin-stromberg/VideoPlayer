# Vergleichsblaupeise: martin-stromberg/Softwareschmiede PR #432

Quelle: `gh pr view 432 --repo martin-stromberg/Softwareschmiede` (MERGED, „Flaky-Test-Härtung: OsInterface-Tagging, Deadline-Angleichung, Cleanup, Flaky-Retry (Issue #428)") plus Dateiinhalte via `gh api …/contents/<pfad>?ref=main` und `gh pr diff 432`.

## Übertragbare Artefakte der Blaupause

| Artefakt (Blaupause) | Inhalt | Übertragbarkeit auf dieses Repo |
|----------------------|--------|----------------------------------|
| `src/Softwareschmiede.Tests/Infrastructure/Testing/TestCategories.cs` | `public static class TestCategories { const OsInterface = "OsInterface"; const Flaky = "Flaky"; }` | 1:1 übertragbar; Zielpfad hier `VideoWebPlayer.Tests/Infrastructure/Testing/` (neu anzulegen, s. `test-infrastructure.md`) |
| `Infrastructure/Testing/OsInterfaceAttribute.cs` | `[AttributeUsage(Method\|Class)] ITraitAttribute → Category=OsInterface` | 1:1 übertragbar (xunit.v3 3.2.2 hier referenziert) |
| `OsInterfaceFactAttribute.cs` / `OsInterfaceTheoryAttribute.cs` | `FactAttribute`/`TheoryAttribute` + `ITraitAttribute`, `CallerFilePath`/`CallerLineNumber`-Konstruktor | übertragbar; vereinfacht das Taggen neuer OS-Tests. Hinweis: Blaupause hat im Diff auch `ITraitAttribute` in `[Fact]`-Kombination — die beiden Attribute sind die praktikable Schreibweise |
| `FlakyAttribute.cs` | `ITraitAttribute → Category=Flaky`, Method/Class | 1:1 übertragbar |
| `FlakyConventionTests.cs` | Reflection: jedes `Category=Flaky`-Trait (Klasse+Methode, vererbt) impliziert `Category=OsInterface`; nutzt `FluentAssertions` | anpassbar — **FluentAssertions ist hier nicht referenziert** (das Testprojekt nutzt `Assert.*` + `Microsoft.Playwright.Assertions`); Meta-Test muss auf xUnit-`Assert` umgeschrieben werden. Erweiterbar um Lane-Matrix-Regel (Requirement erwägt „jeder Test genau einer Lane") |
| `Helpers/TempDirectoryCleanup.cs` (Namespace `Softwareschmiede.TestSupport`) | `DeleteBestEffort(root, processesToKill, normalizeAttributes)`: Kill registrierter Prozesse mit 10-s-`WaitForExit`, optionale Attribut-Normalisierung, `Directory.Delete` mit 5 Versuchen + `Thread.Sleep(100*attempt)`, Restfehler → `Console.Error` statt Exception | 1:1 übertragbar (Namespace anpassen). `normalizeAttributes` ist hier weniger zentral (keine git-Repos/Read-only-Fixtures), aber `DenyReadAccess`-Tests setzen restrictive ACLs/`UnixFileMode` — Normalisierung ist für diese sinnvoll. `processesToKill`-Parameter deckt die fehlende `Kill(entireProcessTree)`-Lücke ab |
| `Helpers/TempDirectoryCleanupTests.cs` | 4 Tests: Read-only-Datei+Normalisierung, gelocktes Verzeichnis wirft nicht, fehlendes Verzeichnis, Kill-Sicherheitsnetz mit `cmd.exe ping` | übertragbar; der Kill-Test ist Windows-spezifisch (`cmd.exe /c ping -n 300 > nul`) — für ubuntu-CI anpassen (`sleep`-Prozess statt `ping`) oder `Assert.SkipWhen` nutzen. Achtung: `> nul` im Blaupausen-Test ist cmd-Syntax, funktioniert hier genauso (cmd-Aufruf), kein Bash-Redirect |
| `Helpers/TempDirectory.cs` | `IDisposable`-Wrapper → `DeleteBestEffort` | optional — hier existiert bereits `BackupUploadSessionServiceTestSupport.TempDirectory`; eher vereinheitlichen als zweite Klasse einführen |
| `Helpers/TestTempDirectoryFixture.cs` | registriert N Temp-Dirs, `Dispose` → `DeleteBestEffort` | optional; hier erfüllt `_tempDirs`-Liste in `FfmpegLiveStreamSegmenterTests_FirstOutput` dieselbe Rolle |
| `scripts/lib/Trx.ps1` | `Get-TrxDocument`, `New-TrxNamespaceManager`, `Get-TrxTestResults` (testName→outcome aus `UnitTestResult`), `Get-TrxRunStartTime` (`<Times start>`) | 1:1 übertragbar |
| `scripts/lib/TestDiscovery.ps1` | `Get-FlakyTestNames` via `dotnet test --list-tests --filter Category=Flaky` → HashSet | 1:1 übertragbar; setzt `--no-build` + vorherigen Build voraus |
| `scripts/Retry-FlakyTests.ps1` | Primär-TRX parsen, `Failed`-Namen auf Methoden-FQN normalisieren, Schnittmenge mit Flaky-Menge, genau ein gefilterter Re-Run `(Category=Flaky)&(FQN~…)` → `*-flaky-retry.trx`, recovered/persistent/indeterminate-Auswertung mit GitHub-Annotationen, Exit 1 bei persistenten Fehlern | anpassbar: Parameter `-TestProject`/`PrimaryTrx`/`Configuration` vorhanden. **Anpassung:** Blaupause-Default `Configuration = "Debug"` — hier wird in Release gebaut/getestet; Aufruf muss `-Configuration Release` übergeben. Blaupause ruft das Skript je Testprojekt auf — hier nur ein Testprojekt. TRX-Pfad `VideoWebPlayer.Tests/TestResults/<name>.trx` |
| `scripts/Analyze-FlakyTestResults.ps1` | TRX-Scan (`*-flaky-retry` getrennt), Flake-Ereignisse (fail→retry-pass, fail→next-run-pass), Fehlraten, Klassifikation stabil/beobachten/Quarantäne-Kandidat (≥2 Flakes oder ≥20 % bei ≥5 Ausführungen, Fenster `-MaxRuns 20`), Report + `::warning::` für Kandidaten, Exit immer 0 | 1:1 übertragbar; verweist in der Empfehlung auf `docs/help/stabilitaet/os-interface-tests.md` (hier neu anzulegen). Quarantäne bleibt Empfehlung, kein Auto-Issue — passt zur offenen Frage der Requirement |
| `docs/help/stabilitaet/os-interface-tests.md` | Klassifizierungskriterium (Assertion hängt am OS-Verhalten vs. triviales Fixture-Plumbing), Kategorie-Schreibweisen, lokale Befehle, Retry-/Statistik-/CI-Abschnitte, `gh run rerun --job`-Regel | Inhaltlich übertragbar, **stark anzupassen**: Windows-Heuristiken (ConPTY, FlaUI, Clipboard, NTFS-Reparse) → hier Playwright/ffmpeg/Kestrel/SQLite-Dateibanken; `Run-AllTestsIndividually.ps1` existiert hier nicht; Jobnamen `build & test` + neuer OS-Job; TRX-Namen anpassen |
| Workflow-Umbau (Diff `staging-ci.yml`/`pr-staging-ci.yml`) | Neuer Job `os-interface-tests` mit `needs: detect-backmerge`, `continue-on-error: true`, `timeout-minutes: 30`, eigene Build-/Test-/Retry-/Analyse-/Upload-Schritte; `version`/`prerelease` hängen **nicht** am OS-Job; Retry `if: failure()`, Analyse `if: always()`; Artefakt-Glob `test-results-os-interface*.trx` | Struktur übertragbar. **Anpassungen:** Blaupause baut im OS-Job Debug + 2 Testprojekte; hier Release + 1 Projekt. Blaupause hat `shell: pwsh` auf Windows-Runnern — hier ubuntu (`pwsh` verfügbar, wird bereits für `playwright.ps1` genutzt). **Der OS-Job hier braucht den Playwright-Install-Schritt** (`playwright.ps1 install --with-deps chromium`) — Laufzeitkosten; Blaupause hat keine Browser-Tests. Env-Flags `SOFTWARESCHMIEDE_SKIP_*` entfallen |

## Blaupausen-Annahmen, die hier NICHT greifen

| Blaupause | Hier |
|-----------|------|
| 2 Testprojekte (`Softwareschmiede.Tests` + `IntegrationTests`), `OsInterfaceAttribute` nur im Hauptprojekt, IntegrationTests taggen via `[Trait]`-Literal + Compile-Link auf `TempDirectoryCleanup`/`FlakyConventionTests` | **1 Testprojekt** — Compile-Link-Konstrukt entfällt; Attribute + Meta-Test + Helfer liegen direkt in `VideoWebPlayer.Tests` |
| `windows-latest` Runner, Windows-Heuristiken | `ubuntu-latest`, Blazor Server |
| Bestehende `E2E`-/`ConPTY`-Kategorien bleiben, `OsInterface` kommt **zusätzlich** | hier existiert nur `E2E` — Trait-Modell-Frage (Ersatz vs. zweite Achse) ist offen; zusätzlich gibt es 8 **ungetaggte** Playwright-Klassen |
| Debug-Builds in CI (`-c Debug`) | Release-Builds (`-c Release`, `NoWarn=NU1903`) |
| Skip-Env-Flags (`SOFTWARESCHMIEDE_SKIP_*`) für Desktop-abhängige Tests | nicht vorhanden/nicht nötig; hier stattdessen `SkipBrowser`-/`E2EBinaryLocator`-`return`-Muster (siehe Audit) |
| `FluentAssertions` | nicht referenziert — Meta-Test/Helper-Tests auf xUnit `Assert` umschreiben |
| Kein Coverage-Gate im OS-Kontext dokumentiert | 70-%-Gate hängt am `Category!=E2E`-Lauf — Verschieben von Tests in die OS-Lane ändert die Datenbasis (Offene Frage der Requirement) |
| `changes.log`-Eintrag als Changelog | hier `docs/RELEASE_NOTES.md` |
| `Softwareschmiede.slnx`, `dotnet 11` | `VideoPlayer.sln`, `dotnet 10` |

## Direkt übernehmbare Workflow-Elemente (Diff-Verifikation)

- Job `os-interface-tests`: `needs: detect-backmerge`, `if: is_backmerge != 'true'`, `continue-on-error: true`, `runs-on`, eigenes `timeout-minutes` — `version`/`prerelease` ohne `needs:` auf den OS-Job.
- Schritt `Retry flaky tests once` mit `if: failure()` (bewusst weit: bei Build-Fail/missing TRX meldet das Skript „keine Flaky-Fehlschläge" + Exit 0); hier nur ein Skript-Aufruf statt zweier (1 Testprojekt).
- Schritt `Analyze flaky test results` mit `if: always()`, rein sichtbar (Exit 0).
- Artefakt-Upload `if: always()` mit Glob `test-results-os-interface*.trx` (nimmt `*-flaky-retry.trx` mit).
- Konvention `gh run rerun --job <id>` nur für den OS-Job (Dokumentationspflicht).
