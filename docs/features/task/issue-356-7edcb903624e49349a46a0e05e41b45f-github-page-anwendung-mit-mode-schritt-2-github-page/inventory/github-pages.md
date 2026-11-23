# Bestandsaufnahme-Detail: GitHub-Pages-Zustand und Deployment-Pfad

## Aktueller Zustand

- Repository: `martin-stromberg/VideoPlayer` (`git remote get-url origin`:
  `https://github.com/martin-stromberg/VideoPlayer`).
- Ziel-URL der Seite: `https://martin-stromberg.github.io/VideoPlayer/`
  (Project Page — Basispfad `/VideoPlayer/`).
- GitHub Pages ist **nicht aktiviert**: In der Vorarbeit (Projekt-Inventory,
  Schritt 0) per `gh api repos/martin-stromberg/VideoPlayer` verifiziert —
  `has_pages: false`. Die Aktivierung übernimmt `actions/configure-pages`
  beim ersten Lauf des neuen Workflows (automatische Pages-Aktivierung ist
  Standardverhalten der Action, sofern das Token `pages: write` besitzt).
  Falls die automatische Aktivierung nicht greift (z. B. Enterprise-
  Restriktionen), greift der dokumentierte manuelle Fallback:
  Settings → Pages → Build and deployment → Source: „GitHub Actions".
- Das `github-pages`-Deployment-Environment existiert nicht und muss nicht
  angelegt werden — `actions/deploy-pages` erzeugt es beim ersten Deployment
  selbst.
- `.nojekyll` ist nicht erforderlich: Beim Artefakt-Deployment
  (`upload-pages-artifact`/`deploy-pages`) läuft kein Jekyll-Build; `.nojekyll`
  wird nur für den klassischen Branch-Deployment-Modus benötigt.

## Veröffentlichungspfad (Staging-Konvention)

- Schritt-Branch → Projekt-Branch → PR `staging` → (manuell freigegebene)
  Promotion `staging` → `main`.
- Der Pages-Workflow triggert ausschließlich auf `main` — die Seite geht also
  erst live, wenn der Schritt die komplette Staging-Kette durchlaufen hat.
  Es gibt kein Vorschau-Deployment (anwender-bestätigt).
- Bis dahin ist die Seite nur lokal prüfbar (statischer Server oder `file://`).

## Basis-Pfad-Festigkeit

Da die Seite unter `/VideoPlayer/` läuft, müssen alle internen Referenzen
(assets, Anker) relativ sein (`./assets/...`, `#features`) oder mit
`<base>`-freiem relativem Pfad arbeiten — absolute Pfade wie `/assets/...`
würden außerhalb des Projekt-Pfads auf `martin-stromberg.github.io` zeigen und
brechen. Externe Links (GitHub-Releases, Repository, Doku) sind absolute
`https://`-URLs und davon nicht betroffen.

## Externer Erreichbarkeits-Nachweis

AK-1 (öffentliche Erreichbarkeit unter der Ziel-URL) kann in diesem Schritt
nicht vollständig belegt werden, da der Workflow erst auf `main` läuft. Der
Schritt liefert den Workflow samt automatisierter Pages-Aktivierung; der
Nachweis der Live-Erreichbarkeit erfolgt beim main-Lauf und ist in der
Pflege-Doku beschrieben (inkl. Fallback, falls `configure-pages` die
Aktivierung nicht übernimmt).
