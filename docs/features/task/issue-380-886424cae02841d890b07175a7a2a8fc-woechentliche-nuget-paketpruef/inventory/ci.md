# CI-/Security-Workflows und Quality Gates

Bestand der `.github/workflows/`- und `.github/actions/`-Dateien, soweit für die
wöchentliche NuGet-Paketprüfung relevant. Workflow-Referenz im Detail:
`docs/STAGING_WORKFLOW.md` Abschnitt „CI Workflow Files" (Z. 246–259) und
`docs/REVIEW_AUTOMATION.md`.

## Der wöchentliche Scan: `security-scan.yml`

- Datei `.github/workflows/security-scan.yml` (Display-Name „Security Scan").
- Trigger: `schedule` cron `'0 4 * * 1'` (montags 04:00 UTC) + `workflow_dispatch`;
  läuft vom Default-Branch `main`. Concurrency-Gruppe mit `cancel-in-progress: true`.
- Job `security-scan` (ubuntu-latest, 20 min Timeout): checkout → setup-dotnet
  `10.0.x` → `dotnet restore VideoPlayer.sln` → Composite Action
  `./.github/actions/security-scan` (Artefakt `vulnerable-packages-scan`).
- Composite Action `.github/actions/security-scan/action.yml` (Z. 23–34):
  `dotnet list <sln> package --vulnerable --include-transitive`, geloggt nach
  `vulnerable-packages-scan.log`; `grep` auf `has the following vulnerable
  packages|Severity` → `::error::` + Exit 1. Artefakt-Upload `if: always()`,
  Retention 14 Tage.
- **Was der Scan liefert:** eine Ja/Nein-Aussage „bekannte Schwachstelle in direkten
  oder transitiven NuGet-Paketen" plus Scan-Log-Artefakt. Er liefert **keine**
  Update-Analyse, keinen Pflegestatus, kein Issue — dafür existiert das
  Review-Issue (requirement.md Z. 73–75: referenzieren statt doppelt analysieren).
- **Letzte Läufe** (`gh run list --workflow security-scan.yml`):

  | Run | Datum | Ergebnis | Dauer |
  |---|---|---|---|
  | 45192953736 | 2026-11-23 | success | 38 s |
  | 43867955515 | 2026-11-16 | success | 55 s |
  | 42619354007 | 2026-11-09 | success | 28 s |
  | 41442909085 | 2026-11-02 | success | 34 s |
  | 40303759716 | 2026-10-26 | success | 41 s |

  Der Lauf vom 2026-11-23 (14:19 UTC) liegt ~1 Stunde vor der Issue-#380-Erzeugung
  (15:41 UTC) und kann direkt referenziert werden.

## Issue-Erzeuger: `review-nuget-weekly.yml`

- Trigger: `schedule` `'0 5 * * 1'` (Mo 05:00 UTC) + `workflow_dispatch`;
  `permissions: contents: read, issues: write`.
- Nutzt die geteilte Composite Action `.github/actions/create-review-issue`:
  Label `automated-review-nuget` idempotent anlegen → Duplikat-Guard
  (`gh issue list --state open --label … --jq 'length'`, max. 1 offenes Issue) →
  `gh issue create` mit Titel „Wöchentliche NuGet-Paketprüfung (%G-W%V)" und dem
  Prüfauftrag-Body (beginnt mit `@softwarewerkstatt`).
- Lauf-Historie: 2026-11-23 success (erzeugte #380), 2026-11-16 success
  (workflow_dispatch; erzeugte #358, geschlossen 2026-11-22 — der Vorgängerzyklus
  wurde ohne begleitenden Paket-PR abgeschlossen; kein „nuget"-PR seitdem im
  `git log`/`gh pr list` auffindbar).
- Dokumentiert in `docs/REVIEW_AUTOMATION.md` (alle drei Review-Workflows und die
  Duplikatlogik).

## Quality Gates für einen PR gegen `staging`: `pr-staging-ci.yml`

Trigger `pull_request` → `staging` (opened/synchronize/reopened). Jobs:

| Job | Blockierend | Inhalt |
|---|---|---|
| `detect-backmerge` | – | erkennt reine Back-Merges main→staging; dann werden alle Gates übersprungen |
| `static-checks` | **ja** | Gate 1 `dotnet format --verify-no-changes --no-restore --severity error`; Gate 2 Security-Scan (dieselbe Composite Action wie der Wochenscan, Artefakt `vulnerable-packages-pr`); Gate 3 `dotnet build -c Debug -p:TreatWarningsAsErrors=true` mit `WarningsNotAsErrors=NU1903;CS8601;CS8602;CS8603;CS8604;CS8618;CS8620;CS8629;CS0436;CS0414;BL0008` |
| `build-and-test` | **ja** | `dotnet build Tests -c Release -p:NoWarn=NU1903`; `dotnet test --filter "Category!=E2E&Category!=OsInterface"` mit XPlat-Coverage; ReportGenerator 5.5.11; **70 %-Line-Coverage-Gate** |
| `os-interface-tests` | nein (`continue-on-error: true`) | Playwright-Chromium-Install, `--filter "Category=OsInterface"`, einmaliger `Flaky`-Retry (`scripts/Retry-FlakyTests.ps1`), Flake-Analyse (`scripts/Analyze-FlakyTestResults.ps1`, immer Exit 0) |
| `no-pending-backmerge` | **ja** | schlägt fehl, solange ein offener Back-Merge-PR `main`→`staging` existiert (`gh pr list --base staging --head main`) |
| `back-merge-skip` | – | reine Anzeige bei Back-Merge-PRs |

Hinweise für Paket-PRs:

- **NU1903** (NuGet-Audit-„high"-Warnung beim Restore) ist in Gate 3 und den
  Test-Builds explizit unterdrückt — der harte Schwachstellen-Check bleibt Gate 2.
- Ein Paket-Bump, der **Release-Build** bricht (Präzedenzfall `SixLabors.ImageSharp`
  4.x), fällt nicht in der PR-CI auf (dort läuft kein Publish) — sondern erst im
  `build-and-package`-Publish oder lokal. Deshalb requirement.md/AGENTS.md:
  Debug **und** Release vor dem PR lokal prüfen.
- Während ein Back-Merge-PR offen ist, kann der Ergebnis-PR nicht mergen —
  vor PR-Erstellung `gh pr list --base staging --head main` prüfen.

## Staging-Push: `staging-ci.yml` („Pre-Release")

- Gleiche `detect-backmerge`/`static-checks`/`build-and-test`/`os-interface-tests`-Gates
  (Artefakte `…-staging`), danach `version` (semantic-release-Dry-Run + RC-Zähler) und
  `prerelease` (Composite Action `build-and-package`: `dotnet publish` win-x64 +
  linux-x64 self-contained, ZIPs, `update.json`, `gh release create --prerelease`).
- Relevanz: der Publish-Job ist die Stelle, an der ein Paket-Bump mit
  RID-/Publish-Inkompatibilität platzen würde — ein grüner PR-Check allein reicht
  als Update-Freigabe nicht aus (AGENTS.md §7 verlangt lokalen Release-Build).

## Promotion/Release: `release.yml`, `verify-pr-source.yml`, `staging-to-main-promotion.yml`, `sync-staging-with-main.yml`

- `verify-pr-source.yml` (PR→main): erzwingt `head_ref == "staging"` —
  Paket-Updates erreichen `main` nur über den Staging-Pfad (Scope-Ausschluss
  „keine PRs gegen main" ist damit auch technisch erzwungen).
- `staging-to-main-promotion.yml`: erzeugt nach erfolgreichem „Pre-Release"-Lauf
  den Draft-PR staging→main.
- `release.yml` (push→main / `v*.*.*`-Tag): Release-Gate = deterministische
  Testsuite `--filter "Category!=OsInterface"`, dann `build-and-package` +
  GitHub-Release (`semantic-release` automatisch bzw. `gh release create` manuell;
  Asset-Repair über `scripts/resolve-release-version.mjs`).
- `sync-staging-with-main.yml`: erzeugt bei jedem Push auf `main` den Back-Merge-PR
  `main`→`staging` (Label `automated-backmerge`, Merge-Commit-Pflicht).

## Weitere Review-Workflows (Kontext)

`review-architecture-security.yml` (monatlich, `0 4 1 * *`, Label
`automated-review-architecture-security`) und `review-compliance-yearly.yml`
(jährlich, `0 4 1 1 *`, Label `automated-review-compliance`) nutzen dieselbe
`create-review-issue`-Action. Für den NuGet-Zyklus nicht maßgeblich, aber dieselbe
Duplikat-Semantik: solange #380 offen ist, entsteht kein W49-Issue.

## Externe Referenz

Mehrere Workflow-Kommentare verweisen auf `ci-target-schema.md` bzw.
`ci-inventory.md` — beide Dateien sind **nicht im Repository** (externe
Prozessbeschreibung, vgl. AGENTS.md §9 zu `~/.claude/commands/`). Die Kommentare
sind die einzige Belegstelle dafür.
