# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| AK-1: Seite unter `https://martin-stromberg.github.io/VideoPlayer/` öffentlich erreichbar (Live-Nachweis erst nach `main`) | Szenario 1: `pages.yml` deployt bei `push` auf `main` mit `pages/**`; `configure-pages` aktiviert Pages, Fallback dokumentiert; Basis-Pfad-Festigkeit via relativer Pfade | Workflow-Definition + dokumentierter Live-Nachweis beim main-Lauf; zuvor Szenario 2 (lokale Abnahme) — Scope-Ausschluss 7 deckt die zeitliche Grenze ab | Abgedeckt (lokal belegbar, Live-Nachweis verbleibt als externer Endpunkt) |
| AK-2: Design responsiv/modern; hell + dunkel; Umschalter persistent; System-Standard | `site.css` mit zwei Token-Sätzen (dunkel = `--vp-*`, hell abgeleitet); `theme.js` + Inline-Bootstrap; responsive Grid-Layouts | Szenarien 2+3: Playwright-Checks (beide `colorScheme`-Emulationen, `localStorage`-Persistenz über Reload, 375px ohne horizontalen Überlauf) | Abgedeckt |
| AK-3: Alle Inhalte (Zweck, Features, echte Screenshots, Release-Verweis, Repo+Doku-Verweise) | `index.html` mit Anker-Abschnitten; Galerie aus `pages/assets/screenshots/` (echte Aufnahmen aus Demoinstallation, Schritte 3/5/6 der Umsetzungsreihenfolge); statischer Releases-Link; Repo-/Doku-Links | Szenarien 2+4: Abschnitts-Präsenz + `naturalWidth`-Check + Sichtprüfung der Abnahme-Screenshots | Abgedeckt |
| AK-4: Workflow deployt bei push `main` + manuell; Pages-Aktivierung automatisiert bzw. Fallback dokumentiert | `pages.yml` (Trigger, Jobs `build`/`deploy`, `permissions`, `concurrency`, `environment`); `docs/GITHUB_PAGE.md` dokumentiert Aktivierung + Fallback | Workflow-YAML-Prüfung; Fallback-Pfad dokumentiert (extern nicht ausführbar vor main) | Abgedeckt |
| AK-5: Pflege-Doku vorhanden und verlinkt; Markdown-Link-Prüfung und Repo-Checks grün | `docs/GITHUB_PAGE.md` + Verlinkung `INDEX.md`/`README.md`/`STAGING_WORKFLOW.md`; `RELEASE_NOTES.md`-Eintrag | Pre-Commit MarkdownLinkCheck + SecretScan; DoD-Suite-Lauf | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Seite laden → alle Inhalte + echte Galerie (AK-3) | Playwright-Abnahme Szenario 2/4 (Abschnitte, `naturalWidth`, Screenshots) | Abgedeckt (reale E2E-Abnahme, nicht Solution-Test) |
| Theme-Umschalter + Persistenz + System-Default (AK-2) | Playwright-Abnahme Szenario 3 (`colorScheme`-Emulation, Toggle, Reload, `localStorage`) | Abgedeckt (reale E2E-Abnahme) |
| Responsives Layout schmaler Viewport (AK-2) | Playwright-Abnahme Szenario 2 (375px, `scrollWidth`-Check, Screenshot) | Abgedeckt (reale E2E-Abnahme) |
| Deployment-Flow (AK-1/AK-4) | Kein lokaler E2E-Test möglich — externe GitHub-Infrastruktur; Nachweis erst beim `main`-Lauf | Nicht erforderlich mit Begründung (externer Endpunkt, Scope-Ausschluss 7); Workflow-Definition statisch geprüft |

## Hinweise

- Die E2E-Abdeckung der Seite erfolgt bewusst als reale Playwright-Abnahme statt
  als Solution-Test: `pages/` besitzt bewusst keine Test-Infrastruktur
  (Scope-Ausschluss 8, anwender-bestätigt). Das ist im Plan begründet.
- Szenario 1 und 5 hängen an externer GitHub-Infrastruktur und sind vor dem
  `main`-Stand nicht ausführbar — im Plan als solche markiert, mit
  dokumentiertem Fallback und verbleibendem Live-Nachweis. Kein Ersatz für die
  spätere Live-Prüfung nötig, da die Auftragsvorgabe die Pages-Aktivierung
  explizit auf den main-Lauf verschiebt.
