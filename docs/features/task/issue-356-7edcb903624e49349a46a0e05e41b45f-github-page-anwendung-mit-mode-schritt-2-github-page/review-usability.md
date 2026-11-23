# Usability-Review: GitHub-Page (Schritt 2, Issue #356)

**Status:** Keine Befunde

Geprüft wurde die lokal ausgelieferte Seite (`python -m http.server`, Basispfad `/VideoPlayer/` simuliert) mit Playwright, Viewports 1440×900 und 375×700, Farbschema hell und dunkel.

## Benutzerfluss

| Aspekt | Beobachtung |
|--------|-------------|
| Orientierung | Klare Einstiegs-Hierarchie: Hero mit App-Name, Tagline, zwei Handlungsaufrufen (Releases, Repository); Navigations-Anker zu Features/Screenshots/Download/Links. |
| Verständlichkeit | Zweck der Anwendung wird in zwei Sätzen verständlich erklärt (selbst gehostete Videobibliothek, kein Cloud-Dienst, kein Abo); Feature-Karten beschreiben die Kernfunktionen in einfacher Sprache. |
| Screenshots | 5 echte App-Ansichten mit beschreibenden `alt`-Texten und Bildunterschriften; Klick öffnet Vollbild. |
| Theme | Umschalter oben rechts mit Sonne/Mond-Icon, `aria-label` + `title`; Wahl bleibt nach Reload erhalten; ohne manuelle Wahl folgt die Seite der Systemeinstellung. |
| Responsivität | Bei 375 px kein horizontaler Überlauf (`scrollWidth == clientWidth`), Nav/Grids stapeln sauber. |
| Links | Releases-Übersicht, Repository, Doku-Verzeichnis, Issues — statische, korrekte Ziele. |
| Barrierefreiheit | Semantische Landmarken (`header`/`main`/`section`/`footer`), `alt`-Texte, `aria-label` am Umschalter, `loading="lazy"` in der Galerie, Fokus-Ring per `--focus-ring`. |
| Sprache | Durchgängig deutsch (`lang="de"`), konsistente Terminologie. |

## Ergebnis

Keine Usability-Befunde. Die Seite erfüllt ihren Zweck als öffentliche Einstiegsseite nachvollziehbar und barrierearm.
