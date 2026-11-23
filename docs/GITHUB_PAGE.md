# GitHub Page — Präsentationsseite

Die öffentliche Präsentationsseite von VideoWebPlayer ist eine statische
Landing-Page unter <https://martin-stromberg.github.io/VideoPlayer/>. Ihre
Quellen liegen versioniert im Ordner [`pages/`](../pages/) und werden über den
Workflow [`.github/workflows/pages.yml`](../.github/workflows/pages.yml) als
GitHub-Pages-Artefakt veröffentlicht.

## Aufbau

| Pfad | Inhalt |
|------|--------|
| `pages/index.html` | Einzige Seite (Deutsch) mit den Anker-Abschnitten `#features`, `#screenshots`, `#download`, `#links` |
| `pages/assets/css/site.css` | Design-Tokens (`data-theme` hell/dunkel) und responsives Layout |
| `pages/assets/js/theme.js` | Theme-Umschalter: `localStorage`-Persistenz (`vwp-theme`), `prefers-color-scheme` als Standard |
| `pages/assets/img/` | Logo und Favicon |
| `pages/assets/screenshots/` | Optimierte WebP-Screenshots aus der Demoinstallation |

Wichtig: Alle internen Verweise sind **relative** Pfade (`./assets/…`,
`#anker`). Die Seite wird unter dem Projekt-Unterpfad `/VideoPlayer/`
ausgeliefert; absolute Pfade (`/assets/…`) würden dorthin zeigen und brechen.

## Inhalte ändern

1. `pages/index.html` (Texte, Abschnitte, Links) bzw. `site.css` anpassen.
2. Lokal prüfen (siehe unten).
3. Änderung über den üblichen Staging-Pfad committen — die Veröffentlichung
   erfolgt erst, sobald die Änderung `main` erreicht (Trigger `push` auf `main`
   mit `paths`-Filter `pages/**`).

## Screenshots neu aufnehmen

Die Galerie zeigt echte Ansichten der Demoinstallation aus Schritt 1
(`Demobibliothek`). Vorgehen bei einer frischen Windows-/Linux-Checkout:

1. Anwendung lokal starten:

   ```bash
   dotnet run --project VideoWebPlayer
   ```

2. Im Browser unter `http://localhost:5039/Account/Register` den ersten Benutzer
   registrieren und dabei den Demodatenbestand **Demobibliothek** auswählen
   (das Auswahlfeld erscheint nur beim ersten Benutzer). Danach über den Link in
   `RegisterConfirmation` die E-Mail bestätigen und anmelden. Der Scan füllt die
   Bibliothek sofort; ggf. einige Sekunden warten, bis Poster/Metadaten stehen.
3. Mit Playwright (oder manuell im Browser) die gewünschten Ansichten aufnehmen.
   Sinnvolle Ziele: Startseite (`/`), Medienquelle (`/mediasource/{id}`),
   Filmsammlung (`/moviecollection/{id}`), Seriendetails (`/tvshow/{id}`),
   Wiedergabe (Play-Button auf einer Detailseite; der Blazor-Circuit braucht
   nach dem Seitenaufruf einige Sekunden — erst dann reagiert der Button).
   Empfohlener Viewport: ca. 1600 × 950, dunkles App-Theme.
   Hinweis: In der Demoinstallation ist der TV-Sender nicht freigeschaltet,
   `/live-tv` liefert deshalb keinen brauchbaren Screenshot.
4. PNG nach WebP konvertieren (Ziel < ~300 KB pro Bild, z. B. Qualität ~82):

   ```bash
   python -c "from PIL import Image; Image.open('shot.png').convert('RGB').save('shot.webp','WEBP',quality=82,method=6)"
   ```

5. Datei unter `pages/assets/screenshots/` ablegen und das `<figure>` in
   `index.html` (src, `alt`-Text, `figcaption`) anpassen.

## Lokale Prüfung

Die Seite ist rein statisch und läuft mit jedem HTTP-Server. Um den
Produktiv-Basispfad `/VideoPlayer/` nachzustellen:

```bash
mkdir serve && cp -r pages serve/VideoPlayer
cd serve && python -m http.server 8090
# → http://localhost:8090/VideoPlayer/
```

Checkliste: helles und dunkles Theme (Umschalter oben rechts, Wahl bleibt nach
Reload erhalten), schmaler Viewport (~375 px) ohne horizontalen Überlauf, alle
Galerie-Bilder laden.

## Deployment und Aktivierung

- **Trigger:** `push` auf `main` mit Änderungen unter `pages/**` sowie
  manueller `workflow_dispatch`. Auf Zwischenbranches läuft der Workflow
  nicht — Veröffentlichung nur ab `main` (Staging-Pfad).
- **Ablauf:** Job `build` (`checkout` → `configure-pages` →
  `upload-pages-artifact` mit `path: pages/`), Job `deploy`
  (`deploy-pages`, Environment `github-pages`, Ausgabe der Page-URL).
- **Erste Aktivierung:** `actions/configure-pages` richtet GitHub Pages beim
  ersten Lauf auf `main` automatisch ein (Source: GitHub Actions).
- **Manueller Fallback:** Sollte die automatische Aktivierung scheitern (z. B.
  Organisations-Restriktionen): Repository → **Settings → Pages → Build and
  deployment → Source: „GitHub Actions"** wählen, danach den Workflow per
  `workflow_dispatch` erneut starten.
