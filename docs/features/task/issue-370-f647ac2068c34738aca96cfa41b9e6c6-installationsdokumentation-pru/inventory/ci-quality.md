# Qualitätssicherung und CI

Wie die Dokumentationsänderung geprüft wird: lokale Checks, Git-Hooks und die
CI-Workflows, die auf PRs gegen `staging` laufen.

## MarkdownLinkCheck — `tools/MarkdownLinkCheck/`

- Aufruf lokal: `dotnet run --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .`
  (bzw. `--no-restore`; ein `--solution`-Argument wird vom Pre-Commit-Hook übergeben,
  vom Tool aber nicht ausgewertet — `Program.cs` Zeilen 8–15 parst nur `--root`).
- Mechanik (`Program.cs`): rekursive Suche nach `*.md` ab `--root`; ignorierte
  Verzeichnisse `.git`, `.vs`, `bin`, `obj`, `artifacts`, `node_modules`,
  `Sub-Repository` (Zeilen 37–46) — **`docs/` und `docs/features/` werden voll
  mitgeprüft**, also auch neue Anforderungs-/Inventardokumente.
- Geprüft werden ausschließlich **lokale** Links `(...)`: Ziel muss im Repository
  liegen (Verlassen des Roots = Fehler), mit exakter Groß-/Kleinschreibung existieren
  und — bei `#fragment` — eine passende Überschrift haben (GitHub-Anchor-Regeln,
  Zeilen 216–246). Links in Code-Fences werden übersprungen; absolute URLs und
  reine `#anker` werden nicht geprüft (Zeilen 156–180).
- Exit-Code `1` bei Fehlern. Eigene Suite: `tools/MarkdownLinkCheck.Tests/` (6 Tests).

## Pre-Commit-Hook — `.githooks/pre-commit`

Versioniert, per `git config core.hooksPath .githooks` aktiviert (Doku:
`GUIDE_Installation.md` Zeilen 181–205, `INDEX.md` Zeilen 46–47, `README.md` Zeile 123).
Führt aus: Branch-Blocker (`main`/`staging`), `translation-check.py`,
`csproj-xmldoc-check.py`, `razor-l10n-check.py`, `razor-usage-check.py` (nur Warnung),
`no-notimplemented-check.py` (nur Warnung), `enum-coverage-check.py` (nur Warnung),
`SecretScan` (Tool `tools/SecretScan`) und `MarkdownLinkCheck` — jeweils per
`dotnet run --no-restore`. Für eine reine Markdown-Änderung relevant sind v. a.
SecretScan und MarkdownLinkCheck; die Python-Hooks prüfen Code-/Resx-Dateien.

## CI-Workflows auf PRs gegen `staging`

### `.github/workflows/pr-staging-ci.yml` (blockierend, sofern kein Backmerge)

| Job | Gates |
|-----|-------|
| `detect-backmerge` | Überspringt alle anderen Jobs bei reinem `main`→`staging`-Backmerge. |
| `static checks` | `dotnet format --verify-no-changes --severity error`; Security-Scan (Composite-Action `.github/actions/security-scan`); `dotnet build -c Debug -p:TreatWarningsAsErrors=true` mit `WarningsNotAsErrors`-Ausnahmeliste. |
| `build & test` | `dotnet build VideoWebPlayer.Tests -c Release`; `dotnet test --filter "Category!=E2E&Category!=OsInterface"` mit Coverage; ReportGenerator + hartes Gate **70 % Zeilenabdeckung**. |
| `os-interface tests` | `Category=OsInterface` (Playwright-E2E, Prozesse, Ports, Dateibanken), `continue-on-error: true` — **best-effort, nicht blockierend**; Flaky-Einmal-Retry via `scripts/Retry-FlakyTests.ps1`, Flake-Statistik via `scripts/Analyze-FlakyTestResults.ps1`. |
| `no-pending-backmerge` | Merge-Sperre, solange ein automatischer `main`→`staging`-PR offen ist. |

Der Markdown-Link-Check läuft **nicht** in den CI-Workflows — er ist reine
lokale Absicherung über den Pre-Commit-Hook (Verifikation per `grep` über
`.github/workflows/`).

### `.github/workflows/staging-ci.yml` („Pre-Release", Push auf `staging`)

Spiegelt `static checks`, `build & test` und `os-interface tests` mit denselben
Filtern/Gates; danach Release-/Prerelease-Kette. Für die Doku-Änderung relevant:
keine Markdown-spezifischen Gates.

### `.github/workflows/release.yml` + `.github/actions/build-and-package/action.yml`

Release-Artefakte (siehe auch [code-facts.md](code-facts.md)):
`dotnet publish -c Release -r {win-x64,linux-x64} --self-contained true` →
`release-win-x64.zip`, `release-linux-x64.zip`, `update.json` als Release-Assets.

## Projektübliche Testbefehle (Quellen)

- `AGENTS.md` §7 DoD: `dotnet build VideoPlayer.sln` in Debug **und** Release;
  `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig grün.
- `README.md` Zeilen 114–121: beide `dotnet test`-Befehle + Lane-Erklärung.
- `docs/INDEX.md` Zeilen 39–44: Build, beide Testsuiten, MarkdownLinkCheck-Aufruf.
- `GUIDE_Installation.md` Zeilen 207–218: dieselben Testbefehle +
  `ApiDocumentationContractTests`-Filter.
- `docs/help/stabilitaet/os-interface-tests.md`: Lane-Konventionen
  (`Category!=E2E&Category!=OsInterface` = blockierende deterministische Lane;
  `Category=OsInterface` = best-effort).

## Relevanz für diese Aufgabe

Reine Dokumentationsänderung ⇒ der wirksame lokale Nachweis ist der
Markdown-Link-Check (muss über die geänderten Dateien grün bleiben) plus
SecretScan (keine echten Secrets in Beispielen); die C#-Gates laufen unverändert,
erwarten aber keine neuen Fehler, solange kein Code geändert wird.
