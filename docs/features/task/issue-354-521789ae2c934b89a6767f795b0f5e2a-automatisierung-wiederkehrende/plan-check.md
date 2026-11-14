# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Für jede Prüfung existiert eine separate GitHub Action | Drei getrennte Workflow-Dateien (`review-architecture-security.yml`, `review-nuget-weekly.yml`, `review-compliance-yearly.yml`) als Umsetzungsschritte 2–4; gemeinsame Logik in Composite Action `.github/actions/create-review-issue` (Schritt 1), je Job mit `actions/checkout@v7` als Voraussetzung für die repo-lokale Action | Kein automatisierbarer Test möglich (keine Workflow-Testsuite im Repo); Nachweis über `workflow_dispatch`-Lauf je Workflow (Schritt 8, Akzeptanzszenarien 1/4) | Abgedeckt |
| Die Ausführung erfolgt zeitgesteuert über einen Cron-Trigger | `schedule`-Cron je Workflow: `0 4 1 * *` (monatlich am 1.), `0 5 * * 1` (wöchentlich Montag 05:00 UTC), `0 4 1 1 *` (jährlich 1. Januar) — jeweils plus `workflow_dispatch` | Akzeptanzszenarien 1/2/4 nennen Cron-Auslöser; reale Auslösung nach Promotion auf `main` (Schritt 8); Restriktion „schedule nur auf Default-Branch" als Risiko und Scope-Ausschluss 6 dokumentiert | Abgedeckt |
| Die erzeugten Issues verwenden aussagekräftige Titel | Feste deutsche Titel je Typ plus zur Laufzeit gebildetes Perioden-Suffix (`%Y-%m` / `%G-W%V` / `%Y`) (Designentscheidung „Issue-Titel", Programmablauf Schritt 7) | Akzeptanzszenarien 1 und 4 prüfen Titel inkl. Suffix beim Dispatch-Lauf | Abgedeckt |
| Der Account `@softwarewerkstatt` wird automatisch im Issue erwähnt | Body beginnt mit `@softwarewerkstatt` (Programmablauf Schritt 7, Scope-Klärung „Erwartete Wirkung"); Auslösewirkung in „Offene Punkte" als am 2026-11-14 geklärt dokumentiert, Fallback (Assignee) als Nachverfolgung benannt | Akzeptanzszenarien 1 und 6 (Issue-Liste bzw. Beobachtung nach erstem Lauf) | Abgedeckt |
| Bereits bestehende offene Issues desselben Typs werden vor der Erstellung geprüft (Duplikatverbot) | Label-basierter Duplikat-Guard `gh issue list --state open --label <typ-label> --json number --jq 'length'` vor `gh issue create`; Label zuvor idempotent per `gh label create --force` sichergestellt (Programmablauf Schritte 4–6, Designentscheidung „Duplikaterkennung") | Akzeptanzszenario 2 (Skip-Fall mit `::notice::`, Zweitlauf), Szenario 5 (Unabhängigkeit der Typen über je eigenes Label), Szenario 3 (Fortsetzung nach Issue-Schluss) | Abgedeckt |
| Die Lösung ist vollständig innerhalb des Repositories dokumentiert | Neue Datei `docs/REVIEW_AUTOMATION.md` (Schritt 5), Erweiterung `docs/STAGING_WORKFLOW.md` (CI-Workflow-Tabelle + Security-Klärung), Eintrag in `docs/INDEX.md`, `README.md`-Hinweis und `docs/RELEASE_NOTES.md`-Eintrag (Schritt 6); Issue-Texte versionskontrolliert inline in den Workflows | `MarkdownLinkCheck`-Lauf in Schritt 7; YAML-Sichtprüfung | Abgedeckt |
| Wörtliche Issue-Texte aus der Anforderung (deutsch, `@softwarewerkstatt` am Anfang, wöchentlicher Text inkl. Anweisung zur direkten PR-Umsetzung) | Schritte 2–4 verweisen jeweils auf den „wörtlichen Prüfauftrag aus der Anforderung" als `issue-body`-Input; Schritt 3 nennt explizit die PR-Umsetzungs-Anweisung | Sichtprüfung beim Dispatch-Lauf (Szenarien 1/4: korrekter Body geprüft) | Abgedeckt |

Zusätzlich geprüfte Punkte:

- **Scope-Klärung:** Vollständig gefüllt (Verarbeitungseinheit, auslösende Ereignisse, erwartete Wirkung, Umgang mit fehlenden Objekten inkl. Label-Neuzeugung, Wiederverwendung offener Issues und externem Account).
- **Scope-Ausschlüsse:** Sechs Ausschlüsse, alle mit Begründung und Status `bestätigt` — erfüllt.
- **Akzeptanzszenarien:** Sechs Szenarien inkl. vollständigem Happy Path (Szenario 1: Trigger → Label-Erzeugung → Guard → Issue-Erzeugung → sichtbare Wirkung), Skip-Fall, Zyklus-Fortsetzung, alle drei Typen, Typ-Unabhängigkeit und fehlende externe Voraussetzung — jeweils mit benanntem Ausgangszustand.
- **Checkout-Voraussetzung (Lücke aus Runde 1):** Behoben — `actions/checkout@v7` als erster Step je Job (Programmablauf Schritt 2, Umsetzungsschritte 2–4), `permissions: contents: read` + `issues: write` (Designentscheidung „Permissions / Concurrency"), Konsequenzen des Checkouts (`persist-credentials`, Default-Branch-Checkout) als Risiko benannt.
- **DoD-Nachweise (Lücke aus Runde 1):** Behoben — Umsetzungsschritt 7 umfasst `dotnet build VideoPlayer.sln` in Debug und Release, die volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (mit benannten preexisting `TvSenderOnAirE2ETests`-Flakes und Wiederholungsregel), `MarkdownLinkCheck` und YAML-Sichtprüfung; ergänzend grüner `pr-staging-ci.yml`-Lauf als CI-Nachweis.
- **Externe Abhängigkeit `@softwarewerkstatt`:** Als Risiko benannt und in Szenario 6 mit Wiederaufnahmeweg (Beobachtung, Fallback Assignee) abgedeckt.
- **Offene Fragen aus `requirement.md`:** Alle im Plan entschieden bzw. als geklärt dokumentiert (Wochentag, Duplikatmerkmal Label, Verhältnis zu `security-scan.yml`, Auslösemechanismus, Skip-Fall nur `::notice::`, keine Issue-Metadaten).
- **Seiteneffekte/Risiken:** Verzögerte Aktivierung, verzögerte Verifikation, Issue-Urheber `github-actions[bot]`, Label-Seiteneffekt, MarkdownLinkCheck, Checkout-Erblast, Namens-/Concurrency-Konflikte — vollständig benannt.

## Fehlende oder unvollständige Testanforderungen

—

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Zeitgesteuerte Issue-Erzeugung je Prüfungstyp (AK 1–5, 7) | Kein Repo-interner E2E-Test; Funktionsnachweis über `workflow_dispatch`-Lauf auf `main` (Umsetzungsschritt 8, Akzeptanzszenarien 1–5) | Nicht erforderlich mit Begründung: Die Anforderung berührt keinen Benutzerfluss der VideoPlayer-Anwendung (keine UI, keine Nutzeraktion, kein erreichbarer Code-Pfad); der Systemablauf ist der GitHub-Actions-Lauf selbst, der erst nach Promotion `staging` → `main` real ausgelöst werden kann. Begründung im Plan (Abschnitt „E2E-Tests") nachvollziehbar. |
| Duplikatvermeidung / Skip-Fall (AK 5) | Zweiter Dispatch-Lauf direkt nach Erstlauf (Akzeptanzszenario 2), Zyklus-Fortsetzung nach Issue-Schluss (Szenario 3) | Nicht erforderlich mit Begründung: gleiche Begründung; Beobachtung über GitHub-Actions-UI/Issue-Liste, nicht über Playwright/bUnit. |

## Fehlende oder unvollständige Planbestandteile

—

## Hinweise

- Das Titel-Suffix für die wöchentliche Prüfung ist als `date`-Format `%G-W%V` geplant und erzeugt `JJJJ-Www` (z. B. `2026-W07`); Designentscheidung und Szenario 4 schreiben `(JJJJ-KWww)`. Rein kosmetische Inkonsistenz — bei der Umsetzung Format oder Dokumentation angleichen (z. B. `%G-KW%V` für `2026-KW07`).
- Composite-Action-Steps benötigen bei `runs: using: composite` explizit `shell: bash` je Run-Step — Implementierungsdetail, das in Schritt 1 beachtet werden sollte (Repo-Konvention).
