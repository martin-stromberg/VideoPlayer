# Dokumentations-Bestand (CI/Release-nahe Dateien)

Die Anforderung verlangt: „Die Lösung ist vollständig innerhalb des Repositories
dokumentiert." Zusätzlich nennen die Definition of Done (`AGENTS.md` Abschnitt 7)
und das Übersetzungsergebnis `README.md` sowie `docs/RELEASE_NOTES.md` als
Berührungspunkte. Bestand:

## `docs/STAGING_WORKFLOW.md`

- Zentrale CI/CD-Doku (englisch mit deutschen Abschnitten). Abschnitt
  „CI Workflow Files" (Zeilen 244–254) listet **jede** Workflow-Datei mit Display-Name
  und Zweck — neue Workflows gehören hier in die Liste.
- Abschnitt „Quality Gates → Security" (Zeile 161): beschreibt Dependency-Scans
  (PR-Gate + wöchentlicher Scan); Beziehung der neuen NuGet-Review-Issues zum
  `security-scan.yml` wäre hier erklärbar.
- Abschnitt „Support and Maintenance → Workflow Updates" (Zeilen 302–306):
  „Document any workflow changes" — deckt sich mit dem Akzeptanzkriterium.

## `README.md`

- Zeile 5: einziger Workflow-Verweis ist das Pre-Release-Badge
  (`staging-ci.yml`). Kein Abschnitt über Automatisierung/Workflows vorhanden —
  eine Erweiterung wäre ein neuer Inhalt, keine Pflichtstelle.

## `docs/RELEASE_NOTES.md`

- Abschnitt „Important Notes Before Update" — Aufzählung nutzerrelevanter
  Änderungen; Workflow-Automatisierung passt hier als Infrastruktur-Hinweis
  (Vorbild: Einträge erwähnen Migrations- und Verhaltenshinweise).

## `docs/INDEX.md`

- Dokumentations-Index mit Tabellen; `STAGING_WORKFLOW.md` ist dort aktuell
  **nicht** verlinkt. Enthält den Abschnitt „Lokale Qualitätsprüfungen"
  (Testbefehle, MarkdownLinkCheck, Git-Hook).

## `.github/ISSUE_TEMPLATE/`

- Drei Standard-Templates (englisch): `bug_report.md`, `feature_request.md`,
  `custom.md`. Keine Templates für Review-/Automatisierungs-Issues — die
  Issue-Texte der Anforderung wären also neuer Bestand (inline im Workflow oder
  als Template-Datei).

## `docs/help/`

- Benutzer-/Admin-Doku (`index.md` als Einstieg). Workflow-Automatisierung ist
  keine Anwenderfunktion — eine Aufnahme ist optional; die DoD verlangt
  `docs/help/` nur „bei Bedarf".

## Weitere berührte Orte

- `AGENTS.md` (Repo-Root): Definition of Done — u. a. „`dotnet build VideoPlayer.sln`
  in Debug UND Release", volle Testsuite, Doku unter `docs/help/`, `README.md` und
  Release Notes „bei Bedarf".
- `docs/improvements/CodeReviewAgent-Workflow.md` — beschreibt den internen
  CodeReviewAgent-Prozess; thematisch benachbart zur `@softwarewerkstatt`-Erwähnung,
  aber ohne technische Kopplung an die Workflows.
- `ci-target-schema.md` / `ci-inventory.md` — in Workflow-Kommentaren mehrfach
  referenziert, aber **nicht im Repo vorhanden** (externe Referenz des
  projektübergreifenden CI-Migrationsprojekts). Neue Kommentare können bei Bedarf
  darauf verweisen; Nachvollziehbarkeit im Repo erfordert eigene Doku.
