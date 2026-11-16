# Bestandsaufnahme: Automatisierung wiederkehrender Architektur-, Sicherheits- und Compliance-Reviews

Bestandsaufnahme der Repository-Automatisierung (`.github/workflows/`,
`.github/actions/`, CI-/Release-Dokumentation) bezogen auf `requirement.md`:
drei neue zeitgesteuerte Workflows, die Issues mit `@softwarewerkstatt`-Erwähnung
unter Duplikatvermeidung erzeugen. Es ist kein Anwendungscode betroffen.

## Zusammenfassung

- **Vorhanden — alle Muster, die die Anforderung vorsieht, existieren bereits:**
  - `schedule`-Cron + `workflow_dispatch` in `security-scan.yml` (einziger
    zeitgesteuerter Workflow, `'0 4 * * 1'` = Mo 04:00 UTC).
  - Duplikat-Guard + Erzeugung via `gh`-CLI in `sync-staging-with-main.yml` und
    `staging-to-main-promotion.yml` (`gh pr list --state open --json number --jq
    'length'` vor `gh pr create`, `GH_TOKEN: ${{ github.token }}`).
  - Idempotente Label-Erzeugung `gh label create ... --force`
    (`automated-backmerge`, `automated-promotion`).
  - `permissions`-Blöcke mit `issues: write` bereits etabliert
    (`sync-staging-with-main.yml`, `staging-to-main-promotion.yml`).
  - Composite-Action-Konvention unter `.github/actions/` (`security-scan`,
    `build-and-package`) für wiederverwendbare Logik; Skript-Konvention unter
    `scripts/` (kein `.github/scripts/`).
- **Fehlend — was neu entstehen muss:**
  - Kein Workflow erzeugt Issues (`gh issue` kommt im gesamten Bestand nicht vor).
  - Keine Review-/Automatisierungs-Labels und keine Issue-Templates für die
    Review-Texte; keine Dependabot-/Renovate-Konfiguration.
  - `@softwarewerkstatt` ist im Repo nirgends referenziert — externe
    Voraussetzung, nur über `issue.md` belegt.
  - Doku-Anker: `docs/STAGING_WORKFLOW.md` Abschnitt „CI Workflow Files" listet
    alle Workflows und müsste erweitert werden; `README.md` kennt nur das
    Pre-Release-Badge.
- **Zu beachten:** `schedule`-Workflows laufen nur vom Default-Branch `main` —
  durch den Staging-Prozess werden neue Workflows erst nach Promotion
  `staging` → `main` aktiv. Der `workflow_run`-Trigger in
  `staging-to-main-promotion.yml` zeigt zudem, dass Display-Namen
  (`name:`) referenzierbar sind — Namenskonflikte mit künftigen Triggern
  beachten. Neue Markdown-Dateien werden vom `MarkdownLinkCheck`
  (pre-commit-Hook) geprüft.
- **C#-Ebene:** nicht betroffen — keine Datenmodelle, Logikklassen, Enums oder
  Interfaces im Scope (daher keine `models.md`/`logic.md`/`enums.md`/`interfaces.md`).

Test-Ausgangszustand: volle Suite `dotnet test
VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Release) mit **2964 Tests:
2960 bestanden, 2 fehlgeschlagen, 2 übersprungen** — beide Fehlschläge in
`TvSenderOnAirE2ETests` (Playwright, unter Volllast), bei Einzel-Wiederholung
grün; als preexisting dokumentiert. Zweite Suite `MarkdownLinkCheck.Tests`
6/6 grün, `MarkdownLinkCheck` (425 Dateien/277 Links) und `SecretScan`
fehlerfrei. Nachweis: [tests.md](inventory/tests.md).

## Details

- [Bestehende Workflows und Konventionen](inventory/workflows.md)
- [Bausteine für die Issue-Automatisierung](inventory/automation-building-blocks.md)
- [Dokumentations-Bestand](inventory/documentation.md)
- [Tests](inventory/tests.md)
