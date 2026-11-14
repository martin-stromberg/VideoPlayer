# Umsetzungsplan: Automatisierung wiederkehrender Architektur-, Sicherheits- und Compliance-Reviews

## Übersicht

Drei neue, voneinander unabhängige zeitgesteuerte GitHub-Actions-Workflows unter `.github/workflows/` erzeugen per Cron-Trigger Issues mit `@softwarewerkstatt`-Erwähnung — jedoch nur, wenn kein offenes Issue desselben Prüfungstyps existiert. Die gemeinsame Logik „Label sicherstellen → Duplikat-Guard → Issue anlegen" wird als Composite Action `.github/actions/create-review-issue` umgesetzt. Es ist kein Anwendungscode betroffen; ergänzt wird die CI-/Repo-Dokumentation.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen aus dem Abschnitt „Fachlicher Scope" der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein „Review-Zyklus" je Prüfungstyp: (1) monatliches Architektur- und Sicherheitsreview, (2) wöchentliche NuGet-Prüfung, (3) jährliches Compliance- und Copyright-Review. Ein Zyklus gilt als offen, solange ein Issue mit dem typ-spezifischen Label im Zustand `open` existiert. |
| Auslösende Ereignisse | `schedule`-Cron je Workflow (UTC) plus manueller `workflow_dispatch`-Trigger zum Testen (Konvention aus `security-scan.yml`). |
| Erwartete Wirkung | Neues GitHub-Issue mit aussagekräftigem Titel (inkl. Perioden-Angabe), dem wörtlichen Prüfauftrag aus der Anforderung als Body, `@softwarewerkstatt`-Erwähnung am Anfang und dem Typ-Label. Skip-Fall: erfolgreicher Lauf mit `::notice::`-Ausgabe, ohne neue Erzeugung. |
| Umgang mit fehlenden Objekten | Typ-Label: zur Laufzeit idempotent erzeugen (`gh label create ... --force`, Konvention aus `sync-staging-with-main.yml`). Offenes Issue desselben Typs: wiederverwenden (kein neues Issue). Account `@softwarewerkstatt`: externe Voraussetzung, wird nur im Issue-Text erwähnt — nicht erzeugt, nicht geprüft. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine inhaltliche Durchführung der Reviews (Analyse, Update-PRs, Änderungsvorschläge) durch die Workflows | Erfolgt extern durch `@softwarewerkstatt` auf Basis der erzeugten Issues | bestätigt (Anforderung des Anwenders, transparent dokumentiert im Issue-Kommentar https://github.com/martin-stromberg/VideoPlayer/issues/354#issuecomment-6884931646) |
| 2 | Kein automatisches Schließen, Eskalieren oder Erinnern bei veralteten offenen Issues — ein dauerhaft offenes Issue unterdrückt alle Folgezyklen | Ausdrücklich gewolltes Duplikatverbot laut Anforderung | bestätigt (Anforderung des Anwenders, transparent dokumentiert im Issue-Kommentar https://github.com/martin-stromberg/VideoPlayer/issues/354#issuecomment-6884931646) |
| 3 | `security-scan.yml` bleibt unverändert; das wöchentliche NuGet-Review-Issue nimmt keinen Bezug auf Scan-Artefakte/-Ergebnisse | Scan = bestehendes Quality-Gate (Erkennung), Issue = Analyse-Auftrag; Kopplung würde die Erzeugung von Scan-Erfolg abhängig machen | bestätigt (Anforderung des Anwenders, transparent dokumentiert im Issue-Kommentar https://github.com/martin-stromberg/VideoPlayer/issues/354#issuecomment-6884931646) |
| 4 | Kein Dependabot-/Renovate-Setup, keine direkte Paket-Aktualisierung durch die Workflows | Update-PRs entstehen erst durch die Bearbeitung des Issues; gehört nicht zum Auftrag | bestätigt (Anforderung des Anwenders, transparent dokumentiert im Issue-Kommentar https://github.com/martin-stromberg/VideoPlayer/issues/354#issuecomment-6884931646) |
| 5 | Keine Änderungen an Anwendungscode, Datenmodell, EF-Migrationen oder UI | Anforderung betrifft ausschließlich Repository-Automatisierung | bestätigt (Anforderung des Anwenders, transparent dokumentiert im Issue-Kommentar https://github.com/martin-stromberg/VideoPlayer/issues/354#issuecomment-6884931646) |
| 6 | Workflow-Funktion ist vor der Promotion `staging` → `main` nicht real verifizierbar (`schedule` und `workflow_dispatch` laufen nur vom Default-Branch) | Technische GitHub-Restriktion; Verifikation erfolgt als definierter Nachweis nach der Promotion per `workflow_dispatch` | bestätigt (Anforderung des Anwenders, transparent dokumentiert im Issue-Kommentar https://github.com/martin-stromberg/VideoPlayer/issues/354#issuecomment-6884931646) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Duplikaterkennung | Eigenes Label je Prüfungstyp (`automated-review-architecture-security`, `automated-review-nuget`, `automated-review-compliance`), Guard via `gh issue list --state open --label <label> --json number --jq 'length'` | Robust gegen Titeländerungen; Label wird vom Workflow selbst vergeben, ist also ein zuverlässiges maschinelles Merkmal. Entspricht der Empfehlung in `requirement.md` und dem `gh pr list`-Guard-Muster aus `sync-staging-with-main.yml`. |
| Gemeinsame Logik | Wiederverwendbare Composite Action `.github/actions/create-review-issue` statt dreifach kopierter Steps; jeder aufrufende Job beginnt mit `actions/checkout@v7` | Repo-Konvention sieht gemeinsame Logik als Composite Action vor (`.github/actions/security-scan`, `.github/actions/build-and-package`); die drei Workflows unterscheiden sich nur in Cron, Titel, Body und Label. Repo-lokale Actions (`uses: ./.github/actions/...`) werden von GitHub Actions nur nach einem `actions/checkout` im Runner-Workspace bereitgestellt — alle Bestands-Nutzungen (`security-scan.yml`, `staging-ci.yml`, `pr-staging-ci.yml`, `release.yml`) folgen genau diesem Muster. |
| Issue-Texte | Inline in den jeweiligen Workflow-Dateien, als `issue-body`-Input an die Composite Action übergeben | Texte sind versionskontrolliert im Workflow dokumentiert — entspricht dem Inline-`--body`-Muster in `sync-staging-with-main.yml` und erfüllt das Dokumentations-Akzeptanzkriterium. Der ohnehin nötige Checkout (für die lokale Action) ändert daran nichts; separate Template-Dateien wären ein zusätzlicher Pflegeort ohne Mehrwert. |
| Repo-Bezug der `gh`-Aufrufe | `-R ${{ github.repository }}` auf allen `gh`-Befehlen | `gh` benötigt kein ausgechecktes Repo, aber `-R` macht das Ziel-Repository explizit und unabhängig vom Checkout-Kontext — belegt durch `gh pr list -R` in `pr-staging-ci.yml`. |
| Cron-Zeiten | Monatlich `0 4 1 * *` (1., 04:00 UTC); wöchentlich `0 5 * * 1` (Montag, 05:00 UTC); jährlich `0 4 1 1 *` (1. Januar, 04:00 UTC) | 04:00 UTC folgt der bestehenden `security-scan.yml`-Konvention. Wöchentlich ebenfalls Montag, aber eine Stunde nach dem Scan: bündelt die wöchentliche Wartung auf einen Tag und lässt den Scan zuerst laufen. |
| Skip-Fall | Nur `::notice::`-Ausgabe und erfolgreicher Abschluss, kein Kommentar auf dem offenen Issue | Das offene Issue repräsentiert bereits den laufenden Zyklus; ein Kommentar erzeugte nur Benachrichtigungsrauschen ohne neue Information. Empfehlung aus `requirement.md` übernommen. |
| Issue-Titel | Fester deutscher Titel je Typ plus Perioden-Suffix, das zur Laufzeit erzeugt wird: `(JJJJ-MM)` monatlich, `(JJJJ-KWww)` wöchentlich, `(JJJJ)` jährlich | „Aussagekräftige Titel" (Akzeptanzkriterium) und für Menschen unterscheidbar in der Issue-Liste; die Duplikaterkennung bleibt label-basiert und ist davon unberührt. |
| Issue-Metadaten | Nur Typ-Label und `@softwarewerkstatt`-Erwähnung im Body; keine Assignees, Milestones oder Projektzuordnungen | Anforderung verlangt nur die Erwähnung; zusätzliche Metadaten sind nicht verlangt und nicht extern verifizierbar. |
| Permissions / Concurrency | `permissions: contents: read` + `issues: write`; eigene `concurrency`-Gruppe je Workflow mit `cancel-in-progress: false` | `contents: read` ist das minimale Bestands-Recht und Voraussetzung für `actions/checkout` (ohne den die repo-lokale Composite Action nicht lädt); `issues: write` deckt Label- und Issue-Erzeugung — beide Rechte sind in `sync-staging-with-main.yml`/`staging-to-main-promotion.yml` bereits etabliert. `cancel-in-progress: false` ist die Konvention für erzeugende Workflows. |
| Workflow-Namen | Dateien `review-architecture-security.yml`, `review-nuget-weekly.yml`, `review-compliance-yearly.yml`; Display-Namen `Architecture and Security Review`, `NuGet Dependency Review`, `Compliance and Copyright Review`; Job `create-review-issue` | Englische Workflow-/Job-Namen sind Repo-Konvention; eindeutige Display-Namen vermeiden Konflikte mit `workflow_run`-Triggern (`staging-to-main-promotion.yml` referenziert `"Pre-Release"`). |
| Label-Farben | `5319E7` (Architektur/Sicherheit), `D93F0B` (NuGet), `FBCA04` (Compliance) — je Label mit deutscher Beschreibung | Farbwahl ist Konvention bei `gh label create` (Vorbilder `1D76DB`, `0E8A16`); unterscheidbare Farben erleichtern das Scannen der Issue-Liste. |

## Programmabläufe

### Zeitgesteuerte Review-Issue-Erzeugung (identisch für alle drei Workflows)

1. Der `schedule`-Cron des Workflows feuert auf dem Default-Branch `main` (oder ein manueller `workflow_dispatch`-Lauf wird gestartet).
2. Job `create-review-issue` (`runs-on: ubuntu-latest`, `timeout-minutes: 10`) beginnt mit `actions/checkout@v7` — zwingende Voraussetzung, damit die repo-lokale Composite Action im Runner-Workspace verfügbar ist (Muster aller Bestands-Workflows).
3. Der Job ruft die Composite Action `./.github/actions/create-review-issue` mit den Inputs `label-name`, `label-color`, `label-description`, `issue-title`, `issue-body`, `title-suffix-format` auf.
4. Die Action stellt das Typ-Label idempotent sicher: `gh label create <label-name> --color <label-color> --description <label-description> --force -R ${{ github.repository }}` mit `env: GH_TOKEN: ${{ github.token }}`.
5. Duplikat-Guard: `existing=$(gh issue list --state open --label <label-name> --json number --jq 'length' -R ${{ github.repository }})`.
6. Bei `existing != "0"`: `::notice::`-Ausgabe „Ein offenes Issue des Typs existiert bereits", erfolgreicher Abschluss (`exit 0`) — kein neues Issue.
7. Sonst: `gh issue create -R ${{ github.repository }} --title "<issue-title> (<periode>)" --body <issue-body> --label <label-name>`; `<periode>` wird per `date` aus `title-suffix-format` gebildet (`%Y-%m` / `%G-W%V` / `%Y`). Der Body beginnt mit `@softwarewerkstatt` und enthält den wörtlichen Prüfauftrag aus der Anforderung.
8. Workflow endet erfolgreich; das Issue ist im Repository sichtbar und löst über die Erwähnung die externe Bearbeitung aus.

Beteiligte Artefakte: `.github/workflows/review-architecture-security.yml`, `.github/workflows/review-nuget-weekly.yml`, `.github/workflows/review-compliance-yearly.yml`, `.github/actions/create-review-issue/action.yml`

### Manuelle Verifikation nach Promotion (workflow_dispatch)

1. Nach Promotion `staging` → `main` erscheinen die drei Workflows in der Actions-UI (workflow_dispatch ist nur auf dem Default-Branch registriert).
2. Manueller Lauf pro Workflow → Issue wird erzeugt (oder beim ersten Label-Lauf zusätzlich das Label angelegt).
3. Sofortiger zweiter Lauf → Skip-Fall mit `::notice::`, kein Duplikat.

Beteiligte Artefakte: dieselben wie oben; Auslöser ist der Anwender über `gh workflow run` oder die Actions-UI.

## Neue Klassen

Keine C#-Klassen — die Anforderung betrifft ausschließlich Repository-Automatisierung. Neu zu erstellende Artefakte:

| Artefakt | Typ | Zweck |
|----------|-----|-------|
| `.github/actions/create-review-issue/action.yml` | Composite Action | Kapselt Label-Sicherstellung, Duplikat-Guard und Issue-Erzeugung; Inputs: `label-name`, `label-color`, `label-description`, `issue-title`, `issue-body`, `title-suffix-format` (leer = kein Suffix) |
| `.github/workflows/review-architecture-security.yml` | Workflow | Monatliches Architektur- und Sicherheitsreview, Cron `0 4 1 * *` + `workflow_dispatch` |
| `.github/workflows/review-nuget-weekly.yml` | Workflow | Wöchentliche NuGet-Prüfung, Cron `0 5 * * 1` + `workflow_dispatch` |
| `.github/workflows/review-compliance-yearly.yml` | Workflow | Jährliches Compliance- und Copyright-Review, Cron `0 4 1 1 *` + `workflow_dispatch` |
| `docs/REVIEW_AUTOMATION.md` | Dokumentation | Beschreibt die drei Workflows: Zeitpläne, Issue-Inhalte, Label-/Duplikatmechanismus, externe `@softwarewerkstatt`-Voraussetzung, Hinweis auf Aktivierung erst nach Promotion auf `main` |

## Änderungen an bestehenden Klassen

Keine C#-Klassen betroffen. Angepasste Dateien:

### `docs/STAGING_WORKFLOW.md` (Dokumentation)

- Erweiterung der Tabelle „CI Workflow Files" um die drei neuen Workflows mit Display-Name und Zweck (Konvention: jede Workflow-Datei ist dort gelistet).
- Im Abschnitt „Quality Gates → Security" kurzer Klärungssatz: der bestehende wöchentliche Vulnerability-Scan bleibt unverändert; das wöchentliche NuGet-Review-Issue ist ein ergänzender Analyse-Auftrag, kein Ersatz.

### `README.md` (Dokumentation)

- Kurzer Hinweis auf die Review-Automatisierung mit Link auf `docs/REVIEW_AUTOMATION.md` (Definition of Done: README bei Bedarf aktualisieren).

### `docs/RELEASE_NOTES.md` (Dokumentation)

- Infrastruktur-Hinweis im Abschnitt „Important Notes Before Update": drei neue zeitgesteuerte Review-Workflows, die Issues für `@softwarewerkstatt` erzeugen.

### `docs/INDEX.md` (Dokumentation)

- Eintrag von `docs/REVIEW_AUTOMATION.md` im Dokumentations-Index (keine Pflichtstelle — `STAGING_WORKFLOW.md` ist dort ebenfalls nicht verlinkt —, aber naheliegend für das Akzeptanzkriterium „vollständig innerhalb des Repositories dokumentiert").

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — die Workflows verarbeiten keine Benutzereingaben. Als Robustheitsregel gilt intern: der Duplikat-Guard (`gh issue list ... --jq 'length'`) muss vor jedem `gh issue create` ausgewertet werden; alle `gh`-Schritte laufen unter `set -euo pipefail` (Repo-Konvention).

## Konfigurationsänderungen

Keine — keine `appsettings`- oder Konfigurationsklassen betroffen. Cron-Zeitpläne, Label-Namen, Issue-Titel und -Texte liegen als Konstanten in den Workflow-Dateien bzw. als Inputs der Composite Action und sind damit versionskontrolliert.

## Seiteneffekte und Risiken

- **Verzögerte Aktivierung:** `schedule`-Workflows laufen nur vom Default-Branch `main` — die Workflows werden erst nach der Promotion `staging` → `main` aktiv; bis dahin entsteht keine Wirkung (im Doku-File explizit zu vermerken).
- **Verifikation erst nach Merge:** `workflow_dispatch` ist nur für auf `main` vorhandene Workflows auslösbar; eine echte Funktionsprobe ist daher erst nach der Promotion möglich (definierter Akzeptanzschritt, kein Repo-Test).
- **Externe Abhängigkeit `@softwarewerkstatt`:** Ob die Erwähnung im Issue-Body die automatische Bearbeitung auslöst, ist nicht im Repo verifizierbar. Funktioniert sie nicht, entstehen Issues ohne Folgebearbeitung — erkennbar nur durch Beobachtung nach dem ersten Lauf (siehe Offene Punkte).
- **Issue-Urheber:** Erzeugte Issues laufen unter `github-actions[bot]` und sind damit als automatisiert erkennbar.
- **Label-Seiteneffekt:** Beim ersten Lauf legt jeder Workflow ein neues Repo-Label an — sichtbar und gewollt, nicht versionskontrolliert (Konvention).
- **MarkdownLinkCheck:** Neue/geänderte Markdown-Dateien (`docs/REVIEW_AUTOMATION.md`, `README.md`, `STAGING_WORKFLOW.md`, `RELEASE_NOTES.md`, `INDEX.md`) müssen den lokalen Link-Check bestehen (Pre-Commit-Hook).
- **Checkout im Job:** `actions/checkout@v7` erbt das Standardverhalten `persist-credentials` (Token im Workspace) und checkt im `schedule`-/`workflow_dispatch`-Kontext den Default-Branch aus — unkritisch, da `contents: read` das minimale Bestands-Recht ist und der Workspace nur zur Bereitstellung der lokalen Composite Action dient; alle `gh`-Aufrufe bleiben zusätzlich über `-R` am Ziel-Repository verankert.
- **Keine Konflikte mit Bestands-Workflows:** Neue `concurrency`-Gruppen und Display-Namen kollidieren weder mit `workflow_run`-Referenzen noch mit bestehenden Gruppen.

## Umsetzungsreihenfolge

1. **Composite Action `.github/actions/create-review-issue/action.yml` anlegen**
   - Voraussetzungen: Keine (Bash + `gh`-CLI sind auf `ubuntu-latest` vorhanden; `GH_TOKEN` über `github.token`).
   - Beschreibung: Action mit den sechs Inputs; Steps gemäß Programmablauf Schritte 4–7: Label sicherstellen → Duplikat-Guard → `::notice::`-Skip oder `gh issue create` mit Perioden-Suffix. Header-Kommentar mit Zweck und den drei Aufrufstellen (Konvention aus `security-scan/action.yml`).

2. **Workflow `.github/workflows/review-architecture-security.yml` anlegen**
   - Voraussetzungen: Schritt 1 (Composite Action).
   - Beschreibung: `name: Architecture and Security Review`, `schedule` `0 4 1 * *` + `workflow_dispatch`, `permissions: contents: read` + `issues: write`, `concurrency`-Gruppe `review-architecture-security` mit `cancel-in-progress: false`, Header-Kommentar mit Trigger-Erklärung. Job `create-review-issue` beginnt mit `actions/checkout@v7` (erforderlich für die repo-lokale Action) und ruft sie anschließend mit dem wörtlichen monatlichen Prüfauftrag aus der Anforderung (`issue-body`), Titel `Architektur- und Sicherheitsreview`, `title-suffix-format: %Y-%m`, Label `automated-review-architecture-security` auf.

3. **Workflow `.github/workflows/review-nuget-weekly.yml` anlegen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Analog Schritt 2; `name: NuGet Dependency Review`, Cron `0 5 * * 1`, Titel `Wöchentliche NuGet-Paketprüfung`, `title-suffix-format: %G-W%V` (ISO-Kalenderwoche), Label `automated-review-nuget`, Body wörtlich inkl. Anweisung zur direkten PR-Umsetzung unbedenklicher Updates.

4. **Workflow `.github/workflows/review-compliance-yearly.yml` anlegen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Analog Schritt 2; `name: Compliance and Copyright Review`, Cron `0 4 1 1 *`, Titel `Jährliches Compliance- und Copyright-Review`, `title-suffix-format: %Y`, Label `automated-review-compliance`.

5. **Dokumentation `docs/REVIEW_AUTOMATION.md` anlegen + `docs/STAGING_WORKFLOW.md` und `docs/INDEX.md` erweitern**
   - Voraussetzungen: Keine (inhaltlich unabhängig; sinnvoll parallel zu Schritten 2–4).
   - Beschreibung: Neue Doku-Datei mit Zeitplänen, Issue-Inhalten, Label-/Duplikatmechanismus, `@softwarewerkstatt`-Voraussetzung und Hinweis auf Aktivierung erst nach Promotion auf `main`. In `STAGING_WORKFLOW.md` die Tabelle „CI Workflow Files" um die drei Workflows ergänzen sowie den Security-Abschnitt um den Klärungssatz zur NuGet-Prüfung erweitern. `docs/REVIEW_AUTOMATION.md` zusätzlich in `docs/INDEX.md` eintragen.

6. **`README.md` und `docs/RELEASE_NOTES.md` ergänzen**
   - Voraussetzungen: Schritt 5 (Linkziel `docs/REVIEW_AUTOMATION.md` existiert).
   - Beschreibung: Kurzer README-Hinweis mit Link auf die neue Doku; Release-Notes-Eintrag als Infrastruktur-Hinweis.

7. **Lokale Qualitätsprüfung und DoD-Nachweise**
   - Voraussetzungen: Schritte 1–6 (alle Workflow- und Markdown-Änderungen abgeschlossen).
   - Beschreibung: Definition-of-Done-Gate gemäß `AGENTS.md` §7, auch wenn kein C#-Code geändert wird: (a) `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei; (b) volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün — die zwei bekannten, preexisting `TvSenderOnAirE2ETests`-Flakes werden bei Ausfall einzeln wiederholt und im Bericht benannt (siehe `inventory/tests.md`); (c) `dotnet run --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -c Release -- --root .` fehlerfrei; (d) YAML der neuen Dateien sichtprüfen (Einrückung, Cron-Syntax). Ergänzender Nachweis auf CI-Ebene: grüner `pr-staging-ci.yml`-Lauf auf dem PR nach `staging` (enthält Release-Build und volle gefilterte Suite).

8. **Verifikation nach Promotion auf `main`**
   - Voraussetzungen: Merge dieses Branches nach `staging` und Promotion `staging` → `main` (außerhalb der Implementierung).
   - Beschreibung: Je Workflow einen `workflow_dispatch`-Lauf auslösen; Issue-Erzeugung mit korrektem Titel, Body, `@softwarewerkstatt`-Erwähnung und Label prüfen; direkter Zweitlauf muss den Skip-Fall (`::notice::`, kein Duplikat) zeigen.

## Tests

### Neue Tests

Keine automatisierbaren Tests möglich — es existiert im Repo weder eine Workflow-Testsuite noch YAML-Lint-Infrastruktur (siehe `inventory/tests.md`). Der Funktionsnachweis erfolgt über die reale Verifikation per `workflow_dispatch` (Schritt 8) gemäß Akzeptanzszenarien. Unabhängig davon bleibt das DoD-Gate verbindlich: `dotnet build VideoPlayer.sln` (Debug und Release) und die volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` müssen weiterhin grün laufen — eingeplant als Umsetzungsschritt 7.

### Betroffene bestehende Tests

Keine. Die beiden in `inventory/tests.md` dokumentierten preexisting Playwright-Fehlschläge (`TvSenderOnAirE2ETests`) stehen in keinem Zusammenhang zur Änderung.

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich. Begründung: Die Anforderung berührt keinen Anwendungs-Benutzerfluss — es gibt keine UI, keine Nutzeraktion innerhalb der VideoPlayer-Anwendung und keinen Code-Pfad, den ein Playwright-/bUnit-Test erreichen könnte. Der einzige „Systemablauf" ist der GitHub-Actions-Lauf selbst; dessen Funktionsnachweis kann nur real auf GitHub (per `workflow_dispatch` nach der Promotion) erbracht werden, nicht durch einen repo-internen Test. Diese Verifikation ist als eigener Umsetzungsschritt und in den Akzeptanzszenarien festgelegt.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path — Ersterzeugung (monatlich) | Cron am 1. des Monats 04:00 UTC auf `main` bzw. manueller `workflow_dispatch` | Kein offenes Issue mit Label `automated-review-architecture-security`; Label noch nicht vorhanden | Lauf startet → Label wird erzeugt → Guard liefert `0` → `gh issue create` | Neues Issue `Architektur- und Sicherheitsreview (JJJJ-MM)` mit `@softwarewerkstatt`-Body und Typ-Label im Repo | `workflow_dispatch`-Lauf auf `main` (Schritt 8), Issue-Liste |
| 2 | Duplikatvermeidung (Skip-Fall) | Nächster Cron-Lauf oder erneuter `workflow_dispatch` | Offenes Issue mit demselben Typ-Label existiert (z. B. aus Szenario 1) | Guard liefert `> 0` → `::notice::` → `exit 0` | Workflow-Lauf grün, kein neues Issue erzeugt | Zweiter Dispatch-Lauf direkt nach Szenario 1 |
| 3 | Zyklus-Fortsetzung nach Abschluss | Cron-Lauf nach Schließen des offenen Issues | Vorheriges Issue des Typs wurde geschlossen, kein offenes mit dem Label | Guard liefert `0` → `gh issue create` | Neues Issue des Typs wird angelegt | Manuell: Issue schließen → Dispatch-Lauf → neues Issue |
| 4 | Wöchentliche und jährliche Prüfung analog | Cron Mo 05:00 UTC bzw. 1. Januar 04:00 UTC bzw. `workflow_dispatch` | Je Typ kein offenes Issue | identischer Ablauf mit eigenem Label | Issue `Wöchentliche NuGet-Paketprüfung (JJJJ-KWww)` bzw. `Jährliches Compliance- und Copyright-Review (JJJJ)` | `workflow_dispatch`-Lauf je Workflow |
| 5 | Unabhängigkeit der Typen | Gleichzeitig offene Issues unterschiedlicher Typen | Offenes NuGet-Issue existiert, aber kein Architektur-Issue | Architektur-Lauf prüft nur sein eigenes Label | Architektur-Issue wird trotz offenem NuGet-Issue erzeugt | Manuell über Dispatch beider Workflows prüfbar |
| 6 | Fehlende externe Voraussetzung | Cron-/Dispatch-Lauf bei nicht reagierendem `@softwarewerkstatt` | Externe Automatisierung ist nicht eingerichtet/inaktiv | Issue wird trotzdem korrekt erzeugt | Issue existiert mit Erwähnung; keine Folgeaktivität — sichtbar als „Issue ohne Bearbeitung" | Beobachtung nach erstem realem Lauf; kein Workflow-Fehler |

## Offene Punkte

Keine verbleibenden Blocker. Punkt 1 (Auslösebedingung der externen @softwarewerkstatt-Automatisierung) wurde am 2026-11-14 geklärt: Die Anforderung legt die Erwähnung im Issue als Auslöser fest ("Die Erwähnung soll die automatische Analyse und Bearbeitung anstoßen") — Umsetzung wie spezifiziert. Fallback (Assignee ergänzen) bleibt als Nachverfolgung nach dem ersten realen Lauf dokumentiert und wurde im Issue-Kommentar transparent gemacht.
