# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `.github/actions/create-review-issue/action.yml` (Composite Action) — angelegt; alle sechs Inputs vorhanden (`label-name`, `label-color`, `label-description`, `issue-title`, `issue-body`, `title-suffix-format` mit `required: false`/`default: ''`); Ablauf gemäß Programmablauf Schritte 4–7: `gh label create ... --force -R ${{ github.repository }}` (idempotent), Duplikat-Guard `gh issue list --state open --label "$LABEL_NAME" --json number --jq 'length' -R`, Skip-Fall mit `::notice::` + `exit 0`, `gh issue create -R` mit Titel + Perioden-Suffix aus `date -u +"$TITLE_SUFFIX_FORMAT"`; `GH_TOKEN: ${{ github.token }}` auf beiden Schritten; `description` dokumentiert Zweck und die drei Aufrufstellen (Konvention aus `security-scan/action.yml`).
- [x] `.github/workflows/review-architecture-security.yml` (Workflow) — angelegt; `name: Architecture and Security Review`, Cron `0 4 1 * *` + `workflow_dispatch`, `permissions: contents: read` + `issues: write`, `concurrency`-Gruppe `review-architecture-security` mit `cancel-in-progress: false`, Header-Kommentar mit Trigger-Erklärung; Job `create-review-issue` (`ubuntu-latest`, `timeout-minutes: 10`) beginnt mit `actions/checkout@v7`; Inputs: Label `automated-review-architecture-security`, Farbe `5319E7`, deutsche Label-Beschreibung, Titel `Architektur- und Sicherheitsreview`, `title-suffix-format: %Y-%m`, `issue-body` wörtlich aus der Anforderung mit `@softwarewerkstatt` am Anfang.
- [x] `.github/workflows/review-nuget-weekly.yml` (Workflow) — angelegt; `name: NuGet Dependency Review`, Cron `0 5 * * 1` + `workflow_dispatch`, analoger Permissions-/Concurrency-/Job-Aufbau (Gruppe `review-nuget-weekly`); Label `automated-review-nuget`, Farbe `D93F0B`, Titel `Wöchentliche NuGet-Paketprüfung`, `title-suffix-format: %G-W%V`, Body wörtlich inkl. Anweisung zur direkten PR-Umsetzung unbedenklicher Updates.
- [x] `.github/workflows/review-compliance-yearly.yml` (Workflow) — angelegt; `name: Compliance and Copyright Review`, Cron `0 4 1 1 *` + `workflow_dispatch`, analoger Aufbau (Gruppe `review-compliance-yearly`); Label `automated-review-compliance`, Farbe `FBCA04`, Titel `Jährliches Compliance- und Copyright-Review`, `title-suffix-format: %Y`, Body wörtlich aus der Anforderung.
- [x] `docs/REVIEW_AUTOMATION.md` (Dokumentation) — angelegt; enthält Zeitpläne (alle drei Cron-Ausdrücke), Issue-Inhalte, Label-/Duplikatmechanismus inkl. `::notice::`-Skip-Fall, externe `@softwarewerkstatt`-Voraussetzung, Verhältnis zum Security-Scan und den Hinweis auf Aktivierung erst nach Promotion auf `main`.
- [x] `docs/STAGING_WORKFLOW.md` (Dokumentation) — erweitert: Tabelle „CI Workflow Files" um die drei neuen Workflows mit Display-Name, Cron-Hinweis und Typ-Label ergänzt; Security-Abschnitt („Quality Gates → Security") um den Klärungssatz ergänzt, dass der Vulnerability-Scan unverändert bleibt und das NuGet-Review-Issue ein ergänzender Analyse-Auftrag ist.
- [x] `README.md` (Dokumentation) — erweitert: Hinweis „Review-Automatisierung" mit Link auf `docs/REVIEW_AUTOMATION.md`.
- [x] `docs/RELEASE_NOTES.md` (Dokumentation) — erweitert: Infrastruktur-Hinweis als erster Punkt im Abschnitt „Important Notes Before Update" (drei Workflows, Zeitpläne, Duplikatverbot, Aktivierung erst auf `main`).
- [x] `docs/INDEX.md` (Dokumentation) — erweitert: `REVIEW_AUTOMATION.md` im Dokumentations-Index eingetragen.
- [x] Scope-Ausschlüsse — eingehalten: `security-scan.yml` unverändert (nicht im Diff), kein Anwendungscode/Datenmodell/UI angefasst (Diff nur `.github/` + Dokumentation), keine Assignees/Milestones/Projekte auf den Issues, kein Auto-Schließen/Kommentieren vorhandener Issues, kein Dependabot-/Renovate-Setup.
- [x] Validierungs-/Robustheitsregeln — eingehalten: Duplikat-Guard wird vor jedem `gh issue create` ausgewertet; `set -euo pipefail` im mehrzeiligen Guard-/Create-Step (der einzeilige `gh label create`-Step folgt der Bestandskonvention aus `sync-staging-with-main.yml`/`staging-to-main-promotion.yml`, wo einzeilige `run:`-Kommandos ebenfalls ohne `set`-Zeile stehen); `-R ${{ github.repository }}` auf allen `gh`-Aufrufen.

## Offene Aufgaben

— (Status `Vollständig umgesetzt`)

## Hinweise

- Die drei Issue-Texte wurden stichprobenartig und im Abgleich mit `_anforderung.txt` wörtlich verifiziert — inklusive `@softwarewerkstatt`-Erwähnung am Anfang und der PR-Umsetzungsanweisung im wöchentlichen NuGet-Text.
- Verbleibende, nicht implementierbare bzw. im Test-Schritt zu erbringende Nachweise (Tasks-Datei 10/11, bewusst `Offen` belassen):
  - Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` steht noch aus (eigener Lifecycle-Schritt „Tests ausführen"). Im Rahmen dieses Reviews bereits erbracht: `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei (0 Fehler, nur preexisting Warnungen) und `MarkdownLinkCheck` grün (435 Dateien, 295 Links — deckt die neuen Links aus `README.md` und `docs/INDEX.md` auf `REVIEW_AUTOMATION.md` mit ab).
  - `workflow_dispatch`-Verifikation je Workflow (Erst- und Zweitlauf mit Skip-Fall) ist planmäßig erst nach der Promotion `staging` → `main` möglich, da `schedule`/`workflow_dispatch` nur auf dem Default-Branch wirken.
- Die Workflow-Funktion selbst (Label-Anlage, Guard, Issue-Erzeugung auf GitHub) ist vor der Promotion nicht real prüfbar — bekannte, im Plan dokumentierte Restriktion, keine Implementierungslücke.
