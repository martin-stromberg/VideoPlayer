# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Für jede Prüfung existiert eine separate GitHub Action | Drei getrennte Workflow-Dateien (`review-architecture-security.yml`, `review-nuget-weekly.yml`, `review-compliance-yearly.yml`) als Umsetzungsschritte 2–4; gemeinsame Logik in Composite Action `.github/actions/create-review-issue` | Kein automatisierbarer Test möglich; Nachweis über `workflow_dispatch`-Lauf je Workflow (Schritt 8, Akzeptanzszenarien 1/4) — **eingeschränkt**: Ausführbarkeit hängt am fehlenden `actions/checkout` für die lokale Composite Action (siehe Planbestandteile) | Abgedeckt |
| Die Ausführung erfolgt zeitgesteuert über einen Cron-Trigger | `schedule`-Cron je Workflow: `0 4 1 * *` (monatlich), `0 5 * * 1` (wöchentlich Mo), `0 4 1 1 *` (jährlich 1. Januar); Wochentag-Entscheidung aus offener Frage begründet getroffen | Akzeptanzszenarien 1/2/4 nennen Cron-Auslöser; reale Auslösung verifizierbar erst nach Promotion auf `main` (Schritt 8) | Abgedeckt |
| Die erzeugten Issues verwenden aussagekräftige Titel | Feste deutsche Titel je Typ plus zur Laufzeit gebildetes Perioden-Suffix `(JJJJ-MM)` / `(JJJJ-KWww)` / `(JJJJ)` (Designentscheidung „Issue-Titel") | Akzeptanzszenario 1 prüft Titel inkl. Suffix beim Dispatch-Lauf | Abgedeckt |
| Der Account `@softwarewerkstatt` wird automatisch im Issue erwähnt | Body beginnt mit `@softwarewerkstatt` (Programmablauf Schritt 6, Scope-Klärung „Erwartete Wirkung"); Auslösewirkung in „Offene Punkte" geklärt, Fallback (Assignee) als Nachverfolgung dokumentiert | Akzeptanzszenarien 1 und 6 (Issue-Liste bzw. Beobachtung nach erstem Lauf) | Abgedeckt |
| Bereits bestehende offene Issues desselben Typs werden vor der Erstellung geprüft (Duplikatverbot) | Label-basierter Duplikat-Guard `gh issue list --state open --label <typ-label> --json number --jq 'length'` vor `gh issue create`; Label zuvor idempotent per `gh label create --force` sichergestellt (Programmablauf Schritte 3–5) | Akzeptanzszenario 2 (Skip-Fall mit `::notice::`), Szenario 5 (Unabhängigkeit der Typen über je eigenes Label) | Abgedeckt |
| Die Lösung ist vollständig innerhalb des Repositories dokumentiert | Neue Datei `docs/REVIEW_AUTOMATION.md` (Schritt 5), Erweiterung `docs/STAGING_WORKFLOW.md` (CI-Workflow-Tabelle + Security-Klärung), `README.md`-Hinweis und `docs/RELEASE_NOTES.md`-Eintrag (Schritt 6); Issue-Texte versionskontrolliert inline in den Workflows | Lokale Prüfung via `MarkdownLinkCheck` in Schritt 7 | Abgedeckt |

Zusätzlich geprüfte Punkte:

- **Wörtliche Issue-Texte:** Die drei Prüfaufträge sollen wörtlich aus der Anforderung übernommen werden; Schritte 2–4 verweisen jeweils auf den „wörtlichen Prüfauftrag aus der Anforderung" (wöchentlicher Text inkl. Anweisung zur direkten PR-Umsetzung). Referenz ist eindeutig (Volltext nur in `_anforderung.txt` bzw. Issue #354).
- **Scope-Klärung:** Vollständig gefüllt (Verarbeitungseinheit, auslösende Ereignisse, erwartete Wirkung, Umgang mit fehlenden Objekten).
- **Scope-Ausschlüsse:** Sechs Ausschlüsse, alle mit Begründung und Status `bestätigt` — erfüllt.
- **Akzeptanzszenarien:** Sechs Szenarien inkl. vollständigem Happy Path (Szenario 1: Trigger → Label → Guard → Issue-Erzeugung → sichtbare Wirkung), Skip-Fall, Zyklus-Fortsetzung nach Issue-Schluss, alle drei Typen, Typ-Unabhängigkeit und fehlende externe Voraussetzung — jeweils mit benanntem Ausgangszustand.
- **Externe Abhängigkeit `@softwarewerkstatt`:** Als Risiko benannt und in Szenario 6 mit Wiederaufnahmeweg (Beobachtung/Fallback Assignee) abgedeckt.
- **Alle Offenen Fragen aus `requirement.md`** sind im Plan entschieden bzw. als geklärt dokumentiert (Wochentag, Duplikatmerkmal, Verhältnis zu `security-scan.yml`, Auslösemechanismus, Skip-Fall-Behandlung, Issue-Metadaten).
- **Seiteneffekte/Risiken:** Verzögerte Aktivierung (schedule nur auf `main`), verzögerte Verifikation, Issue-Urheber `github-actions[bot]`, Label-Seiteneffekt, MarkdownLinkCheck, Namens-/Concurrency-Konflikte — vollständig benannt.

## Fehlende oder unvollständige Testanforderungen

- [ ] Der Plan enthält keinen Schritt für die Definition-of-Done-Nachweise `dotnet build VideoPlayer.sln` (Debug **und** Release) und die volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (AGENTS.md §7). Schritt 7 umfasst nur `MarkdownLinkCheck` und YAML-Sichtprüfung. Auch wenn keine C#-Änderung geplant ist, muss der DoD-Gate explizit eingeplant oder begründet ausgeschlossen werden; mindestens der grüne Lauf in `pr-staging-ci.yml`/`staging-ci.yml` sollte als Nachweis benannt sein.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Zeitgesteuerte Issue-Erzeugung je Prüfungstyp (AK 1–5) | Kein Repo-interner E2E-Test; Funktionsnachweis über `workflow_dispatch`-Lauf auf `main` (Umsetzungsschritt 8, Akzeptanzszenarien 1–5) | Nicht erforderlich mit Begründung: Die Anforderung berührt keinen Benutzerfluss der VideoPlayer-Anwendung (keine UI, keine Nutzeraktion, kein Code-Pfad); der Systemablauf ist der GitHub-Actions-Lauf selbst, der erst nach Promotion `staging` → `main` real ausgelöst werden kann. Begründung im Plan (Abschnitt „E2E-Tests") nachvollziehbar. |
| Duplikatvermeidung / Skip-Fall (AK 5) | Zweiter Dispatch-Lauf direkt nach Erstlauf (Akzeptanzszenario 2) | Nicht erforderlich mit Begründung: gleiche Begründung; Beobachtung über GitHub-Actions-UI/Issue-Liste, nicht über Playwright/bUnit. |

## Fehlende oder unvollständige Planbestandteile

- [ ] **Fehlende Voraussetzung `actions/checkout` für die lokale Composite Action.** Programmablauf Schritt 2 sieht `uses: ./.github/actions/create-review-issue` ausdrücklich „ohne vorherigen Checkout" vor; die Designentscheidungen „Issue-Texte" und „Permissions / Concurrency" begründen den Verzicht auf Checkout bzw. auf `contents: read` genau damit. Das ist nicht ausführbar: GitHub Actions stellt repo-lokale Actions (`uses: ./...`) nur nach einem `actions/checkout` im Runner-Workspace bereit — alle Bestands-Nutzungen (`security-scan.yml` → `security-scan`, `staging-ci.yml`/`pr-staging-ci.yml` → `security-scan`, `staging-ci.yml`/`release.yml` → `build-and-package`) folgen diesem Muster mit vorherigem `actions/checkout@v7`. Ohne Checkout schlägt jeder der drei Workflows fehl, bevor die Action startet. Zu planen ist entweder (a) `actions/checkout@v7` als ersten Step in allen drei Workflows plus `contents: read` im `permissions`-Block, oder (b) ein Ansatz ohne lokale Action (z. B. Logik inline je Workflow oder Action als entfernte Referenz mit Pinning). Die korrelierten Designentscheidungen („kein Checkout nötig", „kein `contents`-Recht") und der Permissions-Entwurf sind entsprechend anzupassen.
- [ ] **DoD-Nachweise fehlen als Umsetzungsschritt** — siehe Abschnitt „Fehlende oder unvollständige Testanforderungen".

## Hinweise

- `docs/INDEX.md` ist der Dokumentations-Index; die neue Datei `docs/REVIEW_AUTOMATION.md` sollte dort ggf. eingetragen werden (`STAGING_WORKFLOW.md` ist dort aktuell ebenfalls nicht verlinkt — laut `inventory/documentation.md` keine Pflichtstelle, aber naheliegend für „vollständig im Repository dokumentiert").
- Bei Variante (a) — Checkout einführen — erbt der Workflow automatisch das `GITHUB_TOKEN`-Standardverhalten des Checkouts (`persist-credentials`); der geringe Mehraufwand ist unkritisch, da `contents: read` ohnehin das minimale Bestands-Recht ist.
- Die beiden in `inventory/tests.md` dokumentierten preexisting Playwright-Fehlschläge (`TvSenderOnAirE2ETests`) stehen in keinem Zusammenhang zur Änderung; der Plan erwähnt sie korrekt.
