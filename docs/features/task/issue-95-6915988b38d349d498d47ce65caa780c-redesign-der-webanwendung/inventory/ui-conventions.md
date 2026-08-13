# Bestehende UI-Konventionen und technische Anknuepfungspunkte

## Globales Styling

`VideoWebPlayer/wwwroot/app.css` enthaelt bereits Regeln fuer dunklen Hintergrund, Medienraster, Collection-/Movie-Header, horizontale Medienlisten, Medienkarten, Player-Overlay, Navigation und responsive Sidebar. `site.css` und komponentenspezifische `.razor.css`-Dateien ergaenzen die Layoutregeln.

Die aktuelle Gestaltung ist jedoch nicht einheitlich: globale Regeln verwenden unter anderem `#181820`, `#23232a`, Gelb `#ffe066`, Bootstrap-Standardblau und verschiedene Schatten/Radien. Die Stitch-Referenz verlangt stattdessen ein konsistentes Charcoal-/Rot-/Blau-System. Eine Tokenisierung der globalen Werte ist daher wahrscheinlicher wartbarer als isolierte Einzelkorrekturen.

## Wiederverwendbare Komponenten

- `Components/Shared/Media/MediaBox.razor` ist der zentrale Kandidat fuer Poster-/Episodenkarten.
- `MediaBase.razor` und die Home-Komponenten bilden gemeinsame Daten- und Darstellungsbausteine.
- `MainLayout.razor`, `NavMenu.razor` und die zugehoerigen CSS-Dateien bilden den Navigationsrahmen.
- `StatusTicker.razor` ist ein globaler, fixierter Statusbereich und muss bei mobilen Layouts beruecksichtigt werden.
- Detailseiten enthalten eigene Header-, Zurueck-, Play- und Inhaltsbereiche, die auf gemeinsame Layoutregeln abgestimmt werden sollten.

## Vorhandene responsive Regeln

Die Styles enthalten Breakpoints um 768 px sowie flexible Raster und horizontale Scrollbereiche. Gleichzeitig existieren feste Karten-/Headerhoehen und feste Sidebar-Breiten. Diese Kombination ist ein potenzieller Ursprung fuer abgeschnittene Inhalte auf kleinen Bildschirmen und muss bei der Umsetzung mit echten mobilen Viewports geprueft werden.

## Interaktion und Zustaende

Vorhandene Interaktionen umfassen NavLink-Aktivzustaende, Sidebar oeffnen/schliessen, Player-Overlay, Hover auf Medienkarten, Formvalidierung, Admin-Badges und Continue-Watching. Fokusregeln sind global vorhanden, aber an Bootstrap-Selektoren und konkrete Farbwerte gebunden. Beim Redesign muessen sie sichtbar bleiben und auf die neue Kontrastpalette angepasst werden.

## Testbasis

Die vorhandenen .NET-Tests decken vor allem Services, Authentifizierung, Medienquellen, Backups und Updates ab. Ein dedizierter visueller Regressionstest oder Browser-Test fuer die Stitch-Ansichten ist im Bestand nicht erkennbar. Die Planung sollte deshalb mindestens Build-/Unit-Tests sowie eine manuelle oder automatisierte Viewport-Abnahme fuer Desktop und Mobile vorsehen.
