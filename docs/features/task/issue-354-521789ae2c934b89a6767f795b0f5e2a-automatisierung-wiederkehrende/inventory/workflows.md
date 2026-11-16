# Bestehende GitHub-Actions-Workflows

Alle bestehenden Workflow-Dateien liegen unter `.github/workflows/`. Es gibt aktuell
**keinen** Workflow, der Issues erzeugt, und nur **einen** zeitgesteuerten Workflow
(`security-scan.yml`). Diese Datei dokumentiert die Bestands-Workflows und die daraus
ableitbaren Konventionen, auf die die Anforderung Bezug nimmt.

## Übersicht

| Datei | `name` | Trigger | `permissions` | `concurrency` |
|-------|--------|---------|---------------|----------------|
| `verify-pr-source.yml` | Verify PR Source | `pull_request` → `main` | `permissions: {}` (leer) | – |
| `pr-staging-ci.yml` | PR CI for Staging | `pull_request` → `staging` (opened/synchronize/reopened) | `contents: read`, `checks: write`, `pull-requests: read` | `pr-staging-${{ github.event.pull_request.number }}`, `cancel-in-progress: true` |
| `staging-ci.yml` | Pre-Release | `push` → `staging` | `contents: write`, `checks: write` | `staging-ci`, `cancel-in-progress: true` |
| `staging-to-main-promotion.yml` | Staging to Main Promotion | `workflow_run` auf `["Pre-Release"]` (completed, staging) | `contents: read`, `pull-requests: write`, `issues: write` | `staging-to-main-promotion`, `cancel-in-progress: false` |
| `sync-staging-with-main.yml` | Backmerge Main to Staging | `push` → `main` | `contents: read`, `pull-requests: write`, `issues: write` | `sync-staging-with-main`, `cancel-in-progress: false` |
| `release.yml` | Release | `push` → `main` und `tags: ['v*.*.*']` | `contents: write` | `release`, `cancel-in-progress: false` |
| `security-scan.yml` | Security Scan | `schedule` cron `'0 4 * * 1'` (Mo 04:00 UTC) + `workflow_dispatch` | kein Block (Default-Read) | `security-scan-${{ github.workflow }}-${{ github.ref }}`, `cancel-in-progress: true` |

## `security-scan.yml` — einziger `schedule`-Workflow

Datei: `.github/workflows/security-scan.yml`

- Cron `'0 4 * * 1'` = wöchentlich montags 04:00 UTC; zusätzlich `workflow_dispatch`
  (genau die Kombination, die die Anforderung für die neuen Workflows vorsieht).
- Erklärender Header-Kommentar zur Trigger-Semantik (Verweis auf
  `ci-target-schema.md` Abschnitt 4.9 — nicht im Repo vorhanden, externe Referenz).
- Job `security-scan`: `runs-on: ubuntu-latest`, `timeout-minutes: 20`, Steps:
  `actions/checkout@v7` → `actions/setup-dotnet@v6` (`dotnet-version: '10.0.x'`) →
  `dotnet restore VideoPlayer.sln` → Composite Action `./.github/actions/security-scan`.
- Führt `dotnet list package --vulnerable --include-transitive` aus und schlägt bei
  Fund fehl. Erzeugt **kein** Issue — deckt damit nur die Erkennung, nicht die von
  der Anforderung geforderte wöchentliche NuGet-**Analyse** per Issue ab.

## `sync-staging-with-main.yml` — Referenzmuster für Duplikatvermeidung

Datei: `.github/workflows/sync-staging-with-main.yml`

Von der Anforderung ausdrücklich als Konventionsvorlage genannt:

- `permissions`-Block enthält bereits `issues: write` (Zeile 24) — das von den neuen
  Workflows benötigte Recht ist im Repo also schon etabliert.
- Label-Erzeugung idempotent zur Laufzeit (Zeile 55):
  `gh label create automated-backmerge --color "1D76DB" --description "..." --force`
  mit `GH_TOKEN: ${{ github.token }}`.
- Duplikat-Guard vor dem Erzeugen (Zeilen 62–66):
  `existing=$(gh pr list --base staging --head main --state open --json number --jq 'length')`,
  bei `!= "0"` nur Hinweis und `exit 0`, sonst `gh pr create ... --label "automated-backmerge"`.
- `concurrency` mit `cancel-in-progress: false` — Erzeugungs-Workflows werden nicht
  abgebrochen (gleiche Wahl in `staging-to-main-promotion.yml` und `release.yml`).

## `staging-to-main-promotion.yml` — zweites Erzeugungsmuster

Datei: `.github/workflows/staging-to-main-promotion.yml`

- Gleiches Muster wie `sync-staging-with-main.yml`: `gh label create automated-promotion
  --force` (Zeile 60), `gh pr list --state open --json number --jq 'length'` als
  Duplikat-Guard (Zeile 67), `gh pr create --draft --label "automated-promotion"`.
- `workflow_run`-Trigger referenziert den **Anzeigenamen** `"Pre-Release"`, nicht den
  Dateinamen (Header-Kommentar warnt explizit davor, den `name:` in `staging-ci.yml`
  zu ändern).

## `pr-staging-ci.yml` / `staging-ci.yml` — Test- und Quality-Gates

Dateien: `.github/workflows/pr-staging-ci.yml`, `.github/workflows/staging-ci.yml`

- Nahezu identische Job-Struktur: `detect-backmerge`, `static-checks`
  (Format-Check `dotnet format --verify-no-changes --severity error`,
  Security-Scan via Composite Action, Static Analysis mit
  `TreatWarningsAsErrors` + `WarningsNotAsErrors`-Ausnahmeliste), `build-and-test`
  (Release-Build des Testprojekts, `playwright.ps1 install --with-deps chromium`,
  drei `dotnet test`-Läufe mit Filtern `Category!=E2E`, `Category=Integration`,
  `Category=E2E`, Coverage-Schwelle 70 %).
- `pr-staging-ci.yml` hat zusätzlich den Job `no-pending-backmerge` (Zeilen 190–213):
  `gh pr list -R ${{ github.repository }} ... --jq 'length'` **ohne** Checkout —
  dokumentiert, dass `gh` ohne lokales Repo mittels `-R` arbeiten kann.
- `staging-ci.yml` zusätzlich: `version`-Job (semantic-release via `npm ci` +
  `npx semantic-release --dry-run`) und `prerelease`-Job (`gh release create --prerelease`).

## `release.yml`

Datei: `.github/workflows/release.yml`

- `gh`-Nutzung: `gh release create` (manueller Tag-Pfad, Zeile 139) und
  `gh release upload ... --clobber` (Asset-Repair, Zeile 150).
- „Release gate – full test suite" (Zeile 95): komplette `dotnet test`-Suite ohne
  Filter als letztes Gate vor der Paketierung.

## `verify-pr-source.yml`

Datei: `.github/workflows/verify-pr-source.yml`

- Minimal-Workflow ohne Checkout; `permissions: {}` (explizit leer) und reine
  `github.head_ref`-Prüfung mit `::error::`-Ausgabe.

## Workflow-Konventionen (aus allen Dateien abgeleitet)

- `runs-on: ubuntu-latest` überall; `timeout-minutes` je Job (5–30).
- Deklarierter `permissions`-Block; minimal gehalten (`verify-pr-source.yml` nutzt
  sogar `permissions: {}`).
- `concurrency`-Gruppe pro Workflow (teils mit `${{ github.workflow }}`/`${{ github.ref }}`
  bzw. PR-Nummer); `cancel-in-progress: true` für Check-Workflows, `false` für
  erzeugende/serialisierte Abläufe.
- Actions mit Major-Pinning: `actions/checkout@v7`, `actions/setup-dotnet@v6`,
  `actions/setup-node@v7`, `actions/upload-artifact@v7` — keine Third-Party-Actions
  (die Release-Notiz in `release.yml` Zeile 129 dokumentiert bewusst den Verzicht
  auf softprops).
- `gh`-CLI immer mit `env: GH_TOKEN: ${{ github.token }}`; in `staging-ci.yml`/
  `release.yml` auch `GITHUB_TOKEN: ${{ github.token }}` bzw. `secrets.GITHUB_TOKEN`.
- Mehrzeilige Bash-Steps mit `set -euo pipefail`; Workflow-Commands `::notice::`
  und `::error::` für Hinweise/Fehler.
- Englische Workflow-, Job- und Step-Namen; erklärende Header- und Inline-Kommentare
  (englisch, teils mit Verweis auf `ci-target-schema.md` — externe Referenz, nicht
  im Repo).
- Nur `secrets.GITHUB_TOKEN` bzw. `github.token` im Einsatz; keine weiteren Secrets
  in Workflows/Actions referenziert.
- `schedule`-Workflows laufen laut GitHub nur vom Default-Branch (`main`) — nach
  dem Staging-Workflow werden neue Workflows erst nach Promotion `staging` → `main`
  aktiv (in der Anforderung bereits als Hinweis erfasst).
