# Automatisierte Review-Issues

Drei zeitgesteuerte GitHub-Actions-Workflows erzeugen wiederkehrend Issues, die den
Account `@softwarewerkstatt` erwähnen und damit die externe automatische Analyse und
Bearbeitung anstoßen. Jeder Prüfungstyp bildet einen eigenen Review-Zyklus: Solange
ein Issue mit dem Typ-Label im Zustand `open` existiert, wird kein neues Issue
erzeugt (Duplikatverbot) — erst das Schließen des Issues beendet den Zyklus und gibt
die nächste Erzeugung frei.

## Workflows und Zeitpläne

| Workflow-Datei | Anzeigename | Zeitplan (UTC) | Issue-Titel | Typ-Label |
|---|---|---|---|---|
| `.github/workflows/review-architecture-security.yml` | Architecture and Security Review | Monatlich am 1., 04:00 (`0 4 1 * *`) | `Architektur- und Sicherheitsreview (JJJJ-MM)` | `automated-review-architecture-security` |
| `.github/workflows/review-nuget-weekly.yml` | NuGet Dependency Review | Wöchentlich montags, 05:00 (`0 5 * * 1`) | `Wöchentliche NuGet-Paketprüfung (JJJJ-KWww)` | `automated-review-nuget` |
| `.github/workflows/review-compliance-yearly.yml` | Compliance and Copyright Review | Jährlich am 1. Januar, 04:00 (`0 4 1 1 *`) | `Jährliches Compliance- und Copyright-Review (JJJJ)` | `automated-review-compliance` |

Alle drei Workflows können zusätzlich manuell über `workflow_dispatch` gestartet
werden (Actions-UI oder `gh workflow run`). Das Perioden-Suffix im Titel wird zur
Laufzeit per `date` erzeugt (`%Y-%m` monatlich, `%G-W%V` ISO-Kalenderwoche
wöchentlich, `%Y` jährlich).

## Ablauf je Lauf

Die gemeinsame Logik liegt in der Composite Action
`.github/actions/create-review-issue` und wird von jedem Workflow mit eigenem
Label, Titel und Issue-Text aufgerufen:

1. Das Typ-Label wird idempotent sichergestellt
   (`gh label create <label> --color ... --description ... --force`).
2. Duplikat-Guard:
   `gh issue list --state open --label <label> --json number --jq 'length'` —
   bei einem Treffer endet der Lauf erfolgreich mit einer `::notice::`-Ausgabe,
   ohne ein neues Issue anzulegen.
3. Sonst erzeugt `gh issue create` das Issue mit Titel plus Perioden-Suffix, dem
   Prüfauftrag als Body und dem Typ-Label. Erzeugte Issues erscheinen unter dem
   Urheber `github-actions[bot]` und sind damit als automatisiert erkennbar.

## Issue-Inhalte

Die Issue-Texte stehen wörtlich in den jeweiligen Workflow-Dateien als
`issue-body`-Input der Composite Action (versionskontrolliert). Jeder Body beginnt
mit der Erwähnung `@softwarewerkstatt` und enthält die Prüfliste des jeweiligen
Zyklus; der wöchentliche NuGet-Text enthält zusätzlich die Anweisung, unbedenkliche
Update-Empfehlungen direkt als Pull Request umzusetzen.

## Verhältnis zum Security-Scan

Der wöchentliche `security-scan.yml`-Lauf (Dependency-Vulnerability-Scan als
Quality-Gate, montags 04:00 UTC) bleibt unverändert. Das wöchentliche
NuGet-Review-Issue ist ein ergänzender Analyse-Auftrag, kein Ersatz: Der Scan
erkennt bekannte Schwachstellen und bricht den Build, das Issue beauftragt die
analytische Auswertung (Updates, Pflegezustand, Breaking Changes) außerhalb des
Builds.

## Aktivierung

`schedule`- und `workflow_dispatch`-Trigger wirken nur auf dem Default-Branch
`main`. Die Workflows werden daher erst nach der Promotion `staging` → `main`
aktiv; bis dahin entsteht keine Wirkung. Funktionsprobe nach der Promotion: je
Workflow einen manuellen Lauf auslösen — das Issue wird mit korrektem Titel, Body
und Label erzeugt; ein sofortiger zweiter Lauf muss den Skip-Fall (`::notice::`,
kein Duplikat) zeigen.

## Externe Voraussetzung

Die Erwähnung `@softwarewerkstatt` im Issue-Body ist der vorgesehene Auslöser der
externen automatischen Bearbeitung. Der Account und seine Reaktion auf Erwähnungen
liegen außerhalb des Repositories und werden vom Workflow weder erzeugt noch
geprüft. Reagiert die externe Automatisierung nicht, entstehen Issues ohne
Folgebearbeitung — erkennbar nur durch Beobachtung nach dem ersten realen Lauf.
