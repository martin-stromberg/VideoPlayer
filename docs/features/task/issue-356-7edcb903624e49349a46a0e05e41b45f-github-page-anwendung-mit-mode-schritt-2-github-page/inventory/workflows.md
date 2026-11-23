# Bestandsaufnahme-Detail: Workflow-Konventionen und Pages-Deployment

## Vorhandene Workflows (`.github/workflows/`)

| Datei | Name | Trigger | Besonderheiten |
|-------|------|---------|----------------|
| `pr-staging-ci.yml` | PR CI for Staging | `pull_request` → `staging` | `concurrency: pr-staging-${{ github.event.pull_request.number }}`, `cancel-in-progress: true` |
| `staging-ci.yml` | Pre-Release | `push` → `staging` | `concurrency: staging-ci`, `cancel-in-progress: true`; Kommentar erklärt Dateiname vs. Anzeigename |
| `release.yml` | Release | `push` → `main`, Tags `v*.*.*` | `concurrency: release`, `cancel-in-progress: false`; Kommentar begründet die Serialisierung |
| `staging-to-main-promotion.yml` | Staging to Main Promotion | `push` → `staging` | `cancel-in-progress: false`; `"on":` gequotet |
| `sync-staging-with-main.yml` | Backmerge Main to Staging | `push` → `main` | `cancel-in-progress: false` |
| `verify-pr-source.yml` | Verify PR Source | `pull_request` → `main` | `permissions: {}` (minimalster Bestand) |
| `security-scan.yml` | Security Scan | `schedule` + `workflow_dispatch` | `concurrency: security-scan-${{ github.workflow }}-${{ github.ref }}` |
| `review-*.yml` (3×) | Review-Automatisierung | `schedule` + `workflow_dispatch` | `permissions: contents: read` + `issues: write`, `timeout-minutes: 10` |

## Konventionen (aus den Bestandsdateien abgeleitet)

- Erste Zeile `name:`, danach optional ein erklärender Kommentarblock (Zweck,
  Designentscheidungen, Verweis auf `ci-target-schema.md` wo relevant).
- `on:` ist in den meisten Dateien ungequotet; `staging-to-main-promotion.yml`,
  `verify-pr-source.yml`, `sync-staging-with-main.yml` nutzen `"on":` —
  beide Schreibweisen kommen vor, ungequotet ist die Mehrheit.
- `permissions:` immer explizit und minimal (nie weggelassen).
- `concurrency:` immer vorhanden; `cancel-in-progress: false` bei
  deploy-/release-artigen Läufen (kein Abbrechen laufender Veröffentlichungen),
  `true` bei PR-/Scan-Läufen.
- `runs-on: ubuntu-latest`, `timeout-minutes:` pro Job (5–30 Minuten).
- Action-Majors (Node-24-Linie): `actions/checkout@v7`,
  `actions/setup-dotnet@v6` (.NET 10), `actions/setup-node@v7` (Node 24),
  `actions/upload-artifact@v7`; repo-lokale Composite-Actions unter
  `.github/actions/` (z. B. `security-scan`, `build-and-package`).
- `timeout-minutes` wird auf Job-Ebene gesetzt, nicht auf Step-Ebene.

## GitHub-Pages-Actions (offizielle Majors, Stand Recherche)

| Action | Aktueller Major | Anmerkung |
|--------|-----------------|-----------|
| `actions/configure-pages` | v6 | Node-24-Runtime; aktiviert Pages automatisch (`enablement`, Standard) und stellt Metadaten bereit |
| `actions/upload-pages-artifact` | v5 | nutzt intern `actions/upload-artifact@v7` — deckt sich mit der Repo-Action-Linie |
| `actions/deploy-pages` | v5 | benötigt `pages: write` + `id-token: write`; erzeugt das `github-pages`-Environment selbst |

## Referenz-Gerüst (offizielle GitHub-Doku, angepasst an Repo-Konventionen)

```yaml
name: GitHub Pages

on:
  push:
    branches: [main]
    paths: ["pages/**"]
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: pages
  cancel-in-progress: false

jobs:
  build:
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v7
      - uses: actions/configure-pages@v6
      - uses: actions/upload-pages-artifact@v5
        with:
          path: pages/
  deploy:
    needs: build
    runs-on: ubuntu-latest
    timeout-minutes: 10
    permissions:
      pages: write
      id-token: write
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - id: deployment
        uses: actions/deploy-pages@v5
```

## Lücken

- Kein `pages/`-Verzeichnis, kein `pages.yml`-Workflow, kein
  `github-pages`-Environment im Repo — alles neu anzulegen.
