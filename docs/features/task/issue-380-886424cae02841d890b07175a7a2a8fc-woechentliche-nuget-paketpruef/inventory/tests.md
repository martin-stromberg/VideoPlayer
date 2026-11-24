# Testinfrastruktur und bekannte Flakes

## Teststack (aus `VideoWebPlayer.Tests.csproj`)

| Paket | Version | Rolle |
|---|---|---|
| xunit.v3 | 3.2.2 | Testframework (v3-Linie, `xunit.runner.visualstudio` 3.1.5) |
| Microsoft.NET.Test.Sdk | 18.10.0 | Testhost |
| bunit | 2.11.3 | Blazor-Komponententests |
| Moq | 4.20.72 | Mocking |
| Microsoft.Playwright | 1.62.0 | Browser-E2E (chromium; CI installiert via `playwright.ps1 install --with-deps chromium`) |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 | `WebApplicationFactory`-Tests |
| Microsoft.EntityFrameworkCore.InMemory / .Sqlite | 10.0.12 | Test-DBs; Constraint-/FK-/Löschverhalten-Tests laufen per AGENTS.md §7 gegen echtes SQLite, nicht InMemory |
| SQLitePCLRaw.lib.e_sqlite3 / msTools.Updater | 3.53.3 / 0.11.0-rc.6 | Laufzeitabhängigkeiten für Testbarkeit |
| coverlet.collector | 10.0.1 | XPlat-Code-Coverage (füttert das 70-%-Gate) |

Zweite Suite `tools/MarkdownLinkCheck.Tests` (xunit.v3, 6 Tests laut früherem
Inventory) gehört nicht zum PR-Test-Gate — dort läuft sie über den
pre-commit-/Hilfsprozess, nicht über `dotnet test VideoWebPlayer.Tests`.

## Lane-Trennung (seit Flake-Härtung, Issue #368)

Kategorien als Konstanten in `VideoWebPlayer.Tests/Infrastructure/Testing/TestCategories.cs`
(`E2E`, `OsInterface`, `Flaky`); Attribute `OsInterfaceAttribute`, `FlakyAttribute`,
`OsInterfaceFact/Theory` im selben Ordner.

- **Reguläre Lane (blockierend):** `--filter "Category!=E2E&Category!=OsInterface"`
  — aktuell **2368** Tests (`--list-tests`, Release-Build vom 2026-11-23). Keine
  echten Browser/Prozesse/Ports/Datei-DBs; retry-frei; liefert die Coverage.
- **OS-Interface-Lane (best-effort):** `--filter "Category=OsInterface"` — aktuell
  **424** Tests (Playwright-E2E, echte Prozessstarts, Ports, SQLite-Dateibanken,
  Rechte-Operationen). `continue-on-error`, nicht in `needs:` der Gates.
- **`[Flaky]`:** nur in Kombination mit `OsInterface` erlaubt — aktuell **1**
  markierter Test: `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage`.
- `FlakyConventionTests` (untraited, reguläre Lane) erzwingt `E2E ⊆ OsInterface`
  und `Flaky ⇒ OsInterface`.
- Kategorie `Integration` existiert nicht (ehemaliger CI-Schritt matchte 0 Tests,
  entfernt).

Referenzdoku: `docs/help/stabilitaet/os-interface-tests.md` (Klassifizierung,
lokale Befehle, CI-Verhalten, Quarantäne-/Re-Entry-Regeln, `TempDirectoryCleanup`).

## Flake-Handling in der CI

- `scripts/Retry-FlakyTests.ps1` (nur OS-Lane, `if: failure()`): sammelt
  Fehlschläge aus dem Primär-TRX, schneidet mit `Category=Flaky`-Liste, genau
  **ein** gefilterter Re-Run → `test-results-os-interface-flaky-retry.trx`;
  recovered = `::notice::`, persistent = `::warning::` + Exit 1 (Job bleibt
  durch `continue-on-error` nicht-blockierend).
- `scripts/Analyze-FlakyTestResults.ps1` (`if: always()`): Flake-Statistik über
  TRX-Verzeichnisse (Fenster 20 Läufe; Quarantäne-Kandidat ≥2 Flakes oder
  ≥20 % Fehlrate) — immer Exit 0, nur Annotationen.
- Rerun-Konvention: max. **ein** `gh run rerun --job <id>` für den
  `os-interface-tests`-Job; niemals `gh run rerun --failed` und niemals Reruns
  der regulären Lane.

## Bekannte Fehlerbilder (`os-interface-tests.md`, „Bekannte Fehlerbilder")

- `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage`
  (`[Flaky]`): Timeout beim Warten auf `#live-tv-video` unter CI-Last.
- Browser-E2E-Timeouts unter CI-Last (Playwright-Locator > 30 s).
- Langsam startende Kindprozesse (ffmpeg unter CI-Last) überziehen Pollfenster.
- SQLite-Dateibanken unter `Path.GetTempPath()` zeitweise nicht löschbar
  (`TempDirectoryCleanup.DeleteFileBestEffort`).
- Testhost-/Build-Artefakt-Rennen = Infrastrukturfehler, separat bewertet.

Letzter vollständiger lokaler Lauf (Inventory vom 2026-11, Issue #354):
**2964 Tests — 2960 grün, 2 fehlgeschlagen (beide `TvSenderOnAirE2ETests`, unter
Volllast; einzeln wiederholt grün), 2 übersprungen.** Aktuelle Lane-Zählung siehe
oben (2368 + 424 = 2792; Abweichung zum älteren Stand durch seitherige
Teständerungen, kein verifizierter Vergleich).

## DoD-Bezug für Paket-Updates (AGENTS.md §7 + requirement.md)

- `dotnet build VideoPlayer.sln` in **Debug und Release** fehlerfrei —
  der lokale Release-Build 2026-11-23 (Testprojekt-Release-Build) lief:
  0 Fehler, 139 Warnungen (alles bekannte, in CI per `WarningsNotAsErrors`
  freigestellte Codes: CS86xx-Nullable, CS0436, CS0414, BL0008).
- Volle Testsuite `dotnet test VideoWebPlayer.Tests` grün; einmaliger
  Einzel-Ausfall wird mehrfach einzeln wiederholt und ehrlich berichtet —
  kein Deaktivieren/Abschwächen.
- Neue Hauptversionen nur nach Lizenz-, Release-Build- und CI-Skript-Prüfung
  (Präzedenzfall `SixLabors.ImageSharp` 4.x brach den Release-Build — damals
  in `acceptance-korrektur-nuget-aktualisierung.md` entdeckt, zurückgesetzt
  und später durch `SkiaSharp` ersetzt, Commit `5617d2e`).
- Paket-PRs erzeugen keine DB-Änderungen → `VideoWebPlayerBackupData`/
  Migration-Regeln normalerweise nicht berührt; bei EFCore-Updates dennoch
  Snapshot-Diff aufmerksam prüfen.
