# Bestandsaufnahme: CI-Workflows

Analyse der drei relevanten Workflows für Issue #368 (Lane-Trennung OS-Interface-Tests).

## `.github/workflows/staging-ci.yml` („Pre-Release", Push auf `staging`)

`concurrency: staging-ci`, `cancel-in-progress: true`. Permissions: `contents: write`, `checks: write`.

### Job-Struktur und `needs:`-Ketten

```
detect-backmerge (ubuntu, kein timeout)
  ├─ static-checks        needs: detect-backmerge   timeout: 15 min
  ├─ build-and-test       needs: detect-backmerge   timeout: 30 min
  └─ version              needs: [detect-backmerge, static-checks, build-and-test]  timeout: 10 min
        └─ prerelease     needs: [detect-backmerge, version]  timeout: 20 min
```

Alle Folgejobs: `if: needs.detect-backmerge.outputs.is_backmerge != 'true'`.

### `build-and-test` (Z. 94–164) — Schritte

1. Checkout (`actions/checkout@v7`)
2. Setup .NET 10 (`actions/setup-dotnet@v6`, `10.0.x`)
3. `dotnet restore VideoPlayer.sln`
4. `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore -c Release -p:NoWarn=NU1903` (Z. 113)
5. `pwsh VideoWebPlayer.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium` (Z. 116)
6. **Run unit tests** (Z. 119): `dotnet test … --filter "Category!=E2E" --collect:"XPlat Code Coverage" --logger "trx;LogFileName=test-results.trx"` — blockierend, speist Coverage-Gate
7. **Run integration tests** (`run:` Z. 122): `--filter "Category=Integration"`, `integration-test-results.trx` — **matcht 0 Tests** (kein `Integration`-Trait im Projekt, siehe `test-infrastructure.md`)
8. **Run E2E tests** (Z. 125): `--filter "Category=E2E"`, `e2e-test-results.trx` — blockierend, fängt die 8 ungetaggten Playwright-Klassen **nicht** (die laufen in Schritt 6)
9. ReportGenerator 5.5.11 installieren + `coverage-report/Summary.txt` erzeugen (Z. 128–131)
10. **Enforce coverage threshold** (Z. 133–148): `COVERAGE_THRESHOLD: '70'` — Zeilenabdeckung < 70 % → `exit 1`. Datenbasis: ausschließlich der `Category!=E2E`-Lauf
11. Upload `coverage-report-staging` (`if: always()`, Z. 150–156)
12. Upload `test-results-staging` = `VideoWebPlayer.Tests/TestResults/*.trx` (`if: always()`, Z. 158–164, 14 Tage Retention) — sammelt alle drei TRX-Dateien

### `version` (Z. 166–233) und `prerelease` (Z. 235–261)

`version` bestimmt via `semantic-release --dry-run --branches staging` die nächste Version und die RC-Nummer (`git tag --list "v${version}-rc.*" | wc -l` + 1). `prerelease` ruft `./.github/actions/build-and-package` und erstellt das GitHub-Pre-Release per `gh release create`. **`version` hängt direkt an `build-and-test`** — ein OS-Lane-Job muss außerhalb dieser `needs:`-Kette bleiben (Blaupausen-Muster bestätigt das).

## `.github/workflows/pr-staging-ci.yml` („PR CI for Staging", PRs gegen `staging`)

`concurrency: pr-staging-${{ github.event.pull_request.number }}`, `cancel-in-progress: true`. Permissions: `contents: read`, `checks: write`, `pull-requests: read`.

### Job-Struktur

```
detect-backmerge
  ├─ static-checks          needs: detect-backmerge   timeout: 15 min
  ├─ build-and-test         needs: detect-backmerge   timeout: 30 min   (Schrittfolge identisch zu staging-ci; Filter `run:` Z. 141/144/147)
  ├─ no-pending-backmerge   needs: detect-backmerge   timeout: 5 min    (blockt Merge, solange Back-Merge-PR main→staging offen)
  └─ back-merge-skip        needs: detect-backmerge   if: is_backmerge == 'true'
```

Unterschiede zu `staging-ci.yml`: Artefaktnamen `coverage-report-pr`/`test-results-pr` (statt `-staging`), keine `version`/`prerelease`-Jobs, zusätzlich `no-pending-backmerge` (Z. 190–213) und `back-merge-skip` (Z. 215–221). Der `build-and-test`-Job ist **identisch** aufgebaut (gleiche Schritte, Filter, TRX-Namen, Coverage-Gate) — Lane-Umbau muss an beiden Stellen parallel geschehen.

## `.github/workflows/release.yml` („Release", Push auf `main` / Tags `v*.*.*`)

`concurrency: release`, `cancel-in-progress: false`. Ein einziger Job `release` (ubuntu, `timeout-minutes: 30`).

Relevante Schritte:

- `Resolve release version` via `scripts/resolve-release-version.mjs` (Z. 54) — klassifiziert `automatic`/`manual`/`upload-existing`
- `Restore solution` + `Build test project` + `Install Playwright browsers` — nur wenn `release_action == 'create'` (Z. 75–85)
- **Release gate — full test suite** (`run:` Z. 95): `dotnet test … -c Release` **ohne `--filter`** — die komplette Suite inkl. aller E2E-/Playwright-Tests, blockierend, `final-test-results.trx`. Läuft nicht beim `upload-existing`-Pfad.
- `Upload release-gate test results` `test-results-release` (`if: always()`, Z. 97–103)
- `Build and package` (composite action) + Release-Erstellung (semantic-release oder `gh release create`) bzw. Asset-Repair-Upload

**Befund:** Das Release-Gate läuft die volle Suite ungefiltert und blockierend — Flakes können hier ein Release verhindern. Ob die Lane-Trennung auch dort gelten soll, ist eine offene Frage der Requirement; aktuell gibt es dort keinen `OsInterface`-/Filter-Mechanismus.

## TRX-Artefakt-Namenskonvention (heute)

| Lauf | TRX-Datei | Artefakt |
|------|-----------|----------|
| Unit (`Category!=E2E`) | `test-results.trx` | `test-results-{staging,pr}` |
| Integration (`Category=Integration`, 0 Tests) | `integration-test-results.trx` | dto. |
| E2E (`Category=E2E`) | `e2e-test-results.trx` | dto. |
| Release-Gate (ungefiltert) | `final-test-results.trx` | `test-results-release` |

Alle TRX landen in `VideoWebPlayer.Tests/TestResults/`; Upload via `*.trx`-Glob — neue `*-flaky-retry.trx` würden automatisch mitgesammelt, aber die Unterscheidung Primär/Retry in `Analyze-FlakyTestResults.ps1` funktioniert über das `-flaky-retry`-Suffix (Blaupause nutzt `test-results-os-interface*.trx`).

## Weitere Workflows (nicht testrelevant für dieses Issue)

`review-architecture-security.yml`, `review-compliance-yearly.yml`, `review-nuget-weekly.yml`, `security-scan.yml`, `staging-to-main-promotion.yml`, `sync-staging-with-main.yml`, `verify-pr-source.yml` — keine Testausführung mit Filtern; `security-scan` läuft als Composite Action im `static-checks`-Job.
