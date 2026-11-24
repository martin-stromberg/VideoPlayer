# Bestandsaufnahme: Wöchentliche NuGet-Paketprüfung (2026-W48, Issue #380)

Bestandsaufnahme der NuGet-Abhängigkeiten, der Solution-/NuGet-Infrastruktur und der
CI-/Test-Gates bezogen auf `requirement.md`: alle `PackageReference` der sechs Projekte
der `VideoPlayer.sln` analysieren, Update-Empfehlungen mit Risikobewertung erstellen und
die unbedenklichen (niedriges/mittleres Risiko) in einem PR gegen `staging` umsetzen.
Stand der Zahlen: 2026-11-23, lokale SDK-Version `11.0.100` (Projekte zielen auf
`net10.0`, CI nutzt `10.0.x`).

## Zusammenfassung

- **Paketbestand:** 40 `PackageReference`-Einträge (33 unterschiedliche Pakete)
  über 4 von 6 Projekten (`VideoWebPlayer`: 21, `VideoWebPlayer.Client`: 3,
  `VideoWebPlayer.Tests`: 13, `MarkdownLinkCheck.Tests`: 3; `MarkdownLinkCheck`
  und `SecretScan` haben keine).
  Mehrfachreferenzen mit Versionsgleichheits-Gebot: `Microsoft.EntityFrameworkCore.Sqlite`,
  `msTools.Updater`, `SQLitePCLRaw.lib.e_sqlite3`, `Microsoft.Extensions.Logging.Abstractions`
  sowie (Test-Linie) `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio`.
- **Update-Lage (`dotnet list package --outdated`, ohne `--include-transitive`):**
  - Patch/Minor-Kandidaten: `SharpCompress` 0.50.5→0.50.11, `SSH.NET` 2026.0.0→2026.0.1,
    `System.IdentityModel.Tokens.Jwt` 8.22.0→8.23.0, `QRCoder` 1.8.0→1.9.0,
    `SkiaSharp`(+NativeAssets) 4.153.1→4.157.0, `Microsoft.NET.Test.Sdk` 18.10.0→18.12.0,
    `Microsoft.Playwright` 1.62.0→1.65.1, `Moq` 4.20.72→4.21.0, `bunit` 2.11.3→2.15.2,
    `coverlet.collector` 10.0.1→10.1.1.
  - Major-Blöcke: alle `Microsoft.*`/`Serilog.*`-Pakete 10.x→**11.0.0** (net11-Linie,
    voraussichtlich Zielframework-Wechsel nötig → hohes Risiko), `xunit.v3`/`runner.visualstudio`
    3.x→4.x.
  - Lokale Prerelease-Pakete `msTools.Backup` 1.1.0-RC.4 / `msTools.Updater` 0.11.0-rc.6
    liegen nur im lokalen Feed `lib/packages` („Nicht in den Quellen gefunden" für
    „Neueste" — Updates nur manuell per nupkg-Ablage).
- **Sicherheitslage:** `dotnet list package --vulnerable --include-transitive` meldet
  **keine** anfälligen Pakete (alle 6 Projekte); `--deprecated` ebenfalls leer;
  Dependabot-Alerts via `gh api` = `[]`. Der wöchentliche `security-scan.yml`-Lauf vom
  2026-11-23 (Run 45192953736, ~eine Stunde vor der Issue-Erzeugung) ist erfolgreich
  gelaufen — fünf erfolgreiche Wochenläufe in Folge.
- **Infrastruktur:** keine zentrale Paketverwaltung (`Directory.Packages.props`
  fehlt), keine Lock-Files, kein `global.json`, kein Dependabot/Renovate; `NuGet.config`
  hängt neben nuget.org den lokalen Feed `lib/packages` an (zwei eigene nupkgs,
  versionskontrolliert). Tooling-Quirk: `--outdated --include-transitive` bricht lokal
  mit „Sequence contains no matching element" ab — ohne `--include-transitive` läuft es.
- **CI-Gates für den Ergebnis-PR gegen `staging`:** `pr-staging-ci.yml` blockiert über
  Format-Check, Security-Scan (Gate 2, dieselbe Composite Action wie der Wochenscan),
  Static Analysis (`TreatWarningsAsErrors`, NU1903 u. a. ausgenommen), blockierende
  Test-Lane (`Category!=E2E&Category!=OsInterface`) plus 70-%-Coverage-Gate und den
  `no pending backmerge`-Job; die `os-interface`-Lane ist best-effort
  (`continue-on-error`). `verify-pr-source.yml` erzwingt den Weg Feature→staging→main.
- **Tests:** xunit.v3 + bunit + Moq + Playwright (chromium) + EF InMemory/Sqlite;
  Lane-Trennung regulär (≈2368 Tests) vs. OsInterface (≈424 Tests, best-effort) mit
  `Flaky`-Retry-Skripten; aktuell genau 1 `[Flaky]`-Test. Bekannte Fehlerbilder in
  `docs/help/stabilitaet/os-interface-tests.md`.
- **Präzedenzfälle:** `SixLabors.ImageSharp` 4.1.2 brach den Release-Build (später
  komplett durch `SkiaSharp` ersetzt, Commit `5617d2e`); letzter NuGet-Prüfzyklus
  (Issue #358, W47) am 2026-11-22 geschlossen — kein begleitender PR gefunden;
  msTools-Updates laufen manuell über nupkg-Ablage in `lib/packages` +
  beidseitigem csproj-Bump.

## Details

- [Paketreferenzen und Update-Lage](inventory/packages.md)
- [Solution-/Projektstruktur und NuGet-Infrastruktur](inventory/projects.md)
- [CI-Workflows und Quality Gates](inventory/ci.md)
- [Testinfrastruktur und bekannte Flakes](inventory/tests.md)
- [Dokumentationsstellen zu Paketen/Updates](inventory/documentation.md)
