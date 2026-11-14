# Übersetzte Anforderung: Automatisierung wiederkehrender Architektur-, Sicherheits- und Compliance-Reviews

### Fachliche Zusammenfassung

Das Repository wird um drei eigenständige, zeitgesteuerte GitHub-Actions-Workflows unter `.github/workflows/` erweitert. Jeder Workflow prüft zum geplanten Zeitpunkt (monatlich am 1., wöchentlich, jährlich am 1. Januar), ob aktuell ein offenes Issue desselben Prüfungstyps existiert, und erzeugt nur dann ein neues Issue mit fest definiertem Titel und Text. Jedes Issue erwähnt den Account `@softwarewerkstatt`, wodurch die externe automatische Analyse und Bearbeitung angestoßen wird. Es sind keine Änderungen am Anwendungscode der VideoPlayer-Lösung vorgesehen — die Anforderung betrifft ausschließlich die Repository-Automatisierung.

### Fachlicher Scope

- **Verarbeitungseinheit:** Ein „Review-Zyklus" je Prüfungstyp. Es existieren drei voneinander unabhängige Typen: (1) monatliches Architektur- und Sicherheitsreview, (2) wöchentliche NuGet-Prüfung, (3) jährliches Compliance- und Copyright-Review. Ein Zyklus gilt als offen, solange ein zugehöriges Issue im Zustand `open` existiert; erst das Schließen des Issues beendet den Zyklus.
- **Auslösende Ereignisse:** Cron-Zeitplan des jeweiligen GitHub-Actions-Workflows (`schedule`-Trigger, UTC). Ergänzend nach Projektkonvention (vgl. `security-scan.yml`) ein manueller `workflow_dispatch`-Trigger zum Testen.
- **Erwartete Wirkung:** Ein neu erzeugtes GitHub-Issue mit aussagekräftigem Titel und dem in der Anforderung vorgegebenen Prüfauftrag als Body, das `@softwarewerkstatt` erwähnt und damit dessen automatische Bearbeitung auslöst. Existiert bereits ein offenes Issue desselben Typs, entsteht kein neues Issue — sichtbar als erfolgreicher Workflow-Lauf ohne Erzeugung (Skip-Fall, empfohlen mit `::notice::`-Ausgabe).
- **Umgang mit fehlenden Objekten:**
  - Identifikationsmerkmal für die Duplikatprüfung (empfohlen: typ-spezifisches Label, alternativ Titel-Präfix): bei Nichtvorhandensein erzeugen, Konvention `gh label create ... --force` aus `sync-staging-with-main.yml`.
  - Offenes Issue desselben Typs: wiederverwenden — kein neues Issue anlegen, der bestehende Zyklus läuft weiter.
  - Der Account `@softwarewerkstatt` und dessen Reaktion auf Erwähnungen sind externe Voraussetzung; er wird im Issue-Text erwähnt, nicht erzeugt oder geprüft.
- **Scope-Ausschlüsse:**
  - Die inhaltliche Durchführung der Reviews (Analyse, Update-PRs, Änderungsvorschläge) ist nicht Teil dieser Anforderung — sie erfolgt außerhalb des Repos durch `@softwarewerkstatt` auf Basis der erzeugten Issues.
  - Kein automatisches Schließen, Eskalieren oder Erinnern bei veralteten offenen Issues — ein dauerhaft offenes Issue unterdrückt alle Folgezyklen (ausdrücklich gewolltes Duplikatverbot).
  - Keine Änderungen an Anwendungscode, Datenmodell, EF-Migrationen oder UI.
  - Der bestehende wöchentliche `security-scan.yml`-Workflow bleibt unverändert; die neue wöchentliche NuGet-Prüfung erzeugt nur das Analyse-Issue und ersetzt den Scan nicht (Annahme — siehe Offene Fragen).
  - Kein Dependabot-/Renovate-Setup und keine direkte Paket-Aktualisierung durch den Workflow; Update-PRs entstehen erst durch die Bearbeitung des Issues.

### Betroffene Klassen und Komponenten

Keine C#-Klassen betroffen. Betroffene bzw. neu zu erstellende Artefakte:

- `.github/workflows/` — drei neue Workflow-Dateien (Vorschlag: `review-architecture-security.yml`, `review-nuget-weekly.yml`, `review-compliance-yearly.yml`), jeweils mit `schedule`-Cron und `workflow_dispatch`.
- Optional gemeinsame Logik für „Issue anlegen, falls kein offenes Issue desselben Typs existiert": als wiederverwendbare Composite Action unter `.github/actions/` oder gemeinsames Skript unter `.github/scripts/` (Konvention: `.github/actions/security-scan`, `.github/actions/build-and-package` existieren bereits).
- Issue-Textvorlagen: entweder inline im Workflow oder als Template-Dateien im Repo (z. B. unter `.github/` oder `docs/`), zwecks Wartbarkeit und Erfüllung des Dokumentations-Akzeptanzkriteriums.
- GitHub-Labels zur eindeutigen Zuordnung offener Issues je Prüfungstyp (zur Laufzeit via `gh label create --force` sichergestellt).
- Dokumentation: neue/erweiterte Beschreibung der Automatisierung unter `docs/` (z. B. eigene Datei oder Abschnitt in `STAGING_WORKFLOW.md`-nahen CI-Dokumenten), sowie `README.md` und `docs/RELEASE_NOTES.md` gemäß Definition of Done.
- Tests: keine Unit-/E2E-Tests möglich; Verifikation über `workflow_dispatch`-Lauf und Review der Workflow-Syntax.

### Implementierungsansatz

- Jeder Workflow folgt den bestehenden Konventionen: `runs-on: ubuntu-latest`, `timeout-minutes`, `concurrency`-Gruppe, deklarierter `permissions`-Block (minimal: `issues: write`, ggf. `contents: read`), aussagekräftige `name`-Angabe und erläuternde Header-Kommentare (wie in `security-scan.yml`/`staging-ci.yml` üblich).
- Duplikatprüfung und Erzeugung über die `gh`-CLI mit `GH_TOKEN: ${{ github.token }}` — identisch zum Muster in `sync-staging-with-main.yml` (`gh pr list --state open` als Guard vor `gh pr create`): zuerst `gh issue list --state open --label <typ-label>` (oder `--search` auf Titel-Präfix), bei Treffer abbrechen, sonst `gh issue create --title ... --body ... --label ...`.
- Cron-Ausdrücke (UTC): monatlich `0 <h> 1 * *`, wöchentlich `0 <h> * * <wochentag>`, jährlich `0 <h> 1 1 *`. Zu beachten: `schedule`-Workflows laufen nur auf dem Default-Branch (`main`) und werden durch den Staging-Promotion-Workflow (`staging` → `main`) erst nach Promotion aktiv.
- Die drei Issue-Texte werden wörtlich aus der Anforderung übernommen (deutsch, mit `@softwarewerkstatt`-Erwähnung am Anfang und den jeweiligen Prüflisten; der wöchentliche Text enthält zusätzlich die Anweisung, unbedenkliche Updates direkt als Pull Request umzusetzen).
- Abhängigkeit zu bestehenden Komponenten: keine. Die Workflows sind eigenständig; lediglich Konventionen (gh-CLI, Label-Erzeugung, Concurrency, Permissions) werden übernommen.

### Konfiguration

- Keine Laufzeitkonfiguration gefordert. Intervalle sind fest als Cron-Ausdrücke in den jeweiligen Workflow-Dateien verdrahtet (pro Prüfungstyp eine separate Action — Akzeptanzkriterium).
- Issue-Titel, -Texte und das Duplikat-Merkmal (Label-Name bzw. Titel-Präfix) liegen als Konstanten im Workflow bzw. in Template-Dateien im Repository und sind damit versionskontrolliert dokumentiert.

### Offene Fragen

- **Wochentag/Uhrzeit der wöchentlichen NuGet-Prüfung:** Die Anforderung nennt nur „wöchentlich". Annahme: ein fester Wochentag in UTC, z. B. Montag analog `security-scan.yml`, oder bewusst ein anderer Tag zur Last-/Aufmerksamkeitsverteilung — mit dem Anwender abzustimmen.
- **Duplikaterkennung:** Über ein eigenes Label je Prüfungstyp (empfohlen, robust bei Titeländerungen) oder über Suche im Issue-Titel? Entscheidung vor Implementierung treffen.
- **Verhältnis zu `security-scan.yml`:** Der neue wöchentliche Workflow erzeugt nur das Analyse-Issue und lässt den bestehenden Scan unverändert — ist das so gewollt, oder soll das Issue auf das Scan-Artefakt/Ergebnis Bezug nehmen?
- **Auslösemechanismus `@softwarewerkstatt`:** Reicht die Erwähnung im Issue-Body, um die automatische Bearbeitung zu starten, oder wird zusätzlich ein Kommentar/Assignee benötigt? (Externe Voraussetzung, nicht im Repo verifizierbar.)
- **Skip-Fall:** Soll bei unterdrückter Erstellung (offenes Issue vorhanden) nur ein Workflow-Hinweis erfolgen oder zusätzlich ein Kommentar auf das offene Issue gesetzt werden?
- **Metadaten der Issues:** Sollen die Issues zusätzlich Assignees, Milestones oder Projektzuordnungen erhalten?
