# Bausteine für die Issue-Automatisierung

Artefakte unter `.github/` und `scripts/`, die für die drei neuen zeitgesteuerten
Workflows relevant sind (wiederverwendbare Logik, CLI-Muster, Labels, Vorlagen).

## Composite Actions unter `.github/actions/`

Konvention: gemeinsame Logik liegt als Composite Action (`runs: using: composite`)
vor und wird von mehreren Workflows mit `uses: ./.github/actions/<name>` eingebunden.

### `security-scan` — `.github/actions/security-scan/action.yml`

- `description` erklärt Zweck und beide Aufrufstellen (`pr-staging-ci.yml`/
  `staging-ci.yml` Gate 2 und `security-scan.yml`).
- Inputs: `solution-path` (default `VideoPlayer.sln`), `artifact-name`
  (default `vulnerable-packages-scan`).
- Steps: `dotnet list package --vulnerable --include-transitive` via `tee` in
  `vulnerable-packages-scan.log`, `grep` auf `has the following vulnerable packages|Severity`
  → `::error::` + `exit 1`; danach `actions/upload-artifact@v7` (`if: always()`,
  `retention-days: 14`).

### `build-and-package` — `.github/actions/build-and-package/action.yml`

- Inputs: `release-version`, `release-tag`.
- Steps: `setup-dotnet@v6`, `dotnet publish` für `win-x64`/`linux-x64`,
  `jq`-generierte `release-metadata.json`/`update.json`, ZIP-Erzeugung.

## Skripte

- `scripts/resolve-release-version.mjs` — einziges CI-Skript (Node ESM); wird in
  `release.yml` via `node scripts/resolve-release-version.mjs` aufgerufen.
- **Kein** `.github/scripts/`-Verzeichnis vorhanden — Skripte leben im Repo-Root
  unter `scripts/`.

## `gh`-CLI-Nutzung im Bestand

| Stelle | Befehl | Zweck |
|--------|--------|-------|
| `sync-staging-with-main.yml:55` | `gh label create automated-backmerge ... --force` | Label idempotent sicherstellen |
| `sync-staging-with-main.yml:62` | `gh pr list --base staging --head main --state open --json number --jq 'length'` | Duplikat-Guard |
| `sync-staging-with-main.yml:67` | `gh pr create --base ... --title ... --body ... --label` | PR erzeugen |
| `staging-to-main-promotion.yml:60` | `gh label create automated-promotion ... --force` | Label sicherstellen |
| `staging-to-main-promotion.yml:67-75` | `gh pr list` / `gh pr create --draft` | Duplikat-Guard + Erzeugung |
| `pr-staging-ci.yml:208` | `gh pr list -R ${{ github.repository }} ...` | `gh` ganz ohne Checkout nutzbar |
| `staging-ci.yml:259` | `gh release create --prerelease --generate-notes` | Pre-Release anlegen |
| `release.yml:139,150` | `gh release create/upload` | Release/Asset-Repair |

- `gh issue`-Aufrufe (`issue list`, `issue create`, `issue comment`) kommen im
  Bestand **nirgends** vor — das Muster muss analog zum `gh pr`-Guard neu aufgebaut
  werden.
- Umgebungsvariable für `gh`: `GH_TOKEN: ${{ github.token }}` (außer
  semantic-release: `GITHUB_TOKEN`).

## Labels

Zur Laufzeit erzeugt (nicht versionskontrolliert im Repo):

| Label | Farbe | Erzeugt in | Zweck |
|-------|-------|-----------|-------|
| `automated-backmerge` | `1D76DB` | `sync-staging-with-main.yml:55` | Back-Merge-PRs `main` → `staging` |
| `automated-promotion` | `0E8A16` | `staging-to-main-promotion.yml:60` | Promotion-PRs `staging` → `main` |

Für die Duplikaterkennung je Review-Typ existiert noch kein Label — die
`gh label create ... --force`-Konvention deckt das aber zur Laufzeit ab.

## Issue-Templates und Agenten-Dateien

- `.github/ISSUE_TEMPLATE/`: `bug_report.md`, `feature_request.md`, `custom.md` —
  Standard-GitHub-Templates (englisch), keine Review-/Automatisierungs-Templates.
- `.github/agents/`: `CodeReviewAgent.agent.md`, `DarkPattern.agent.md` —
  GitHub-Agent-Definitionen (Repo-Kontext für Coding-Agenten).
- `.github/skills/`: Skill-Katalog (u. a. `dotnet-design-pattern-review`,
  `architecture-blueprint-generator`, `documentation-writer`).
- `docs/improvements/CodeReviewAgent-Workflow.md` — beschreibt den
  CodeReviewAgent-Workflow (Trigger, Analyse, `docs/improvements/` als Ablage).
- Der Account `@softwarewerkstatt` wird im Repo nicht referenziert — die
  Erwähnung in erzeugten Issues ist eine externe Voraussetzung (verifizierbar nur
  über `issue.md` Zeile 107: „@softwarewerkstatt füge bitte diese Prüfung hinzu").

## Paket-Automatisierung

- **Kein** Dependabot (`dependabot.yml`) und kein Renovate vorhanden —
  NuGet-Updates erfolgen aktuell manuell; die wöchentliche Issue-Erzeugung wäre
  der erste automatisierende Schritt in diese Richtung.
- `NuGet.config` im Repo-Root (Paketquellen).

## Lokale Qualitätsprüfungen (relevant für neue Markdown-Dateien)

Aus `docs/INDEX.md` Abschnitt „Lokale Qualitätsprüfungen" und `.githooks/pre-commit`:

- `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj` —
  zweite Testsuite des Repos (6 Tests, s. [tests.md](tests.md)).
- `dotnet run --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .` —
  prüft lokale Links in **allen** Markdown-Dateien (425 Dateien, ignoriert
  `bin`/`obj`/`node_modules`/`.git` u. a.; case-sensitiv auch unter Windows).
  Neue/umbenannte Doku-Dateien müssen diese Prüfung bestehen.
- `tools/SecretScan` — läuft im `pre-commit`-Hook über das Repo.
- Weitere `pre-commit`-Checks (Python): `translation-check.py`,
  `csproj-xmldoc-check.py`, `razor-l10n-check.py` (blockierend) sowie
  `razor-usage-check.py`, `no-notimplemented-check.py`, `enum-coverage-check.py`
  (nur Warnungen) — für YAML-/Markdown-Änderungen ohne C#-/Razor-Bezug nicht
  betroffen, aber beim Commit aktiv.
