# Umsetzungsplan: Redesign der Webanwendung

## Ziel und Scope

Das Redesign wird im primaeren Frontend `VideoWebPlayer/` umgesetzt. Das aeltere
Frontend unter `WebPlayer/` wird in dieser Anforderung nicht angepasst. Fachlogik,
Datenmodell, Services, Authentifizierung und vorhandene Navigation bleiben
funktional unveraendert.

Der erste Lieferumfang umfasst die fuenf im Stitch-Referenzpaket vorhandenen
Ansichten:

- Dashboard
- Serien
- Filme
- Detailansicht
- Video-Player beziehungsweise Seriendetails

Login-, Konto- und Admin-Seiten werden nur insoweit angepasst, wie globale
Layout- oder Design-Token sie beeinflussen. Neue Workflows und Datenmodell-
aenderungen sind nicht Bestandteil des Plans.

## Verbindliche Designgrundlage

Die Dateien in `stitch_private_media_library.zip` sind die visuelle Referenz.
Aus `cinematic_noir/DESIGN.md` und den fuenf Screenshots werden die folgenden
Tokens und Layoutregeln abgeleitet:

- Grundflaeche: Deep Charcoal um `#131313` beziehungsweise `#121212`
- Primaerakzent: Rot um `#E50914`; Sekundaerakzent: Blau um `#00A8E8`
- Display-Schrift: Montserrat; UI- und Fliesstext: Inter, nur sofern die
  Einbindung im Projekt zulaessig ist; andernfalls lokale System-Fallbacks
- 8-Pixel-Abstandsrhythmus, 24 px Desktop-Gutter, 20 px mobiler Seitenabstand
- 12-Spalten-Raster auf Desktop, 4-Spalten-Raster auf Mobile
- Poster im Verhaeltnis 2:3, Episoden im Verhaeltnis 16:9
- Tonale Flaechen und dezente 1-Pixel-Rahmen statt starker Schatten; Radien
  grundsaetzlich bis 8 px
- Dunkle, transluzente Overlays fuer Player und Detailbereiche

Die Referenz-HTML wird nicht direkt uebernommen. Blazor-Komponenten, bestehende
Services, Ereignisbindungen und Autorisierungslogik bleiben die technische Basis.

## Umsetzungsschritte

### 1. Referenz und betroffene Oberflaechen abgrenzen

1. Stitch-Screenshots und `DESIGN.md` aus dem ZIP waehrend der Umsetzung
   verfuegbar machen und je Referenzansicht eine Zuordnung zu den bestehenden
   Blazor-Seiten dokumentieren.
2. Betroffene Einstiegspunkte pruefen: `Home/Home.razor`,
   `Components/Shared/Home/`, `Components/Shared/Media/`,
   `Pages/Movies/`, `Pages/TV/` und `Components/Shared/Media/VideoPlayer.razor`.
3. Sicherstellen, dass das Redesign auf `VideoWebPlayer` begrenzt bleibt und
   `WebPlayer` nicht versehentlich durch globale oder gemeinsame Dateien
   veraendert wird.

### 2. Globale Design-Tokens und Layout-Rahmen einfuehren

1. `VideoWebPlayer/wwwroot/app.css` und `VideoWebPlayer/wwwroot/css/site.css`
   auf konsistente CSS-Variablen fuer Farben, Flaechen, Typografie, Abstaende,
   Rahmen und Radien umstellen.
2. Bestehende uneinheitliche Farben, Bootstrap-Overrides und Schatten nur in
   den betroffenen `VideoWebPlayer`-Bereichen ersetzen.
3. `Components/Layout/MainLayout.razor`, `NavMenu.razor` und
   `NavMenu.razor.css` an den Stitch-Navigationsrahmen anpassen. Aktive Links,
   Quellenlinks, Rollen-/Claim-Sichtbarkeit, Sidebar-Steuerung und
   `StatusTicker.razor` muessen erhalten bleiben.
4. Breakpoints fuer Desktop und Mobile festlegen. Feste Hoehen und Breiten nur
   dort behalten, wo sie fuer Player oder Medienverhaeltnisse erforderlich sind;
   Inhalte muessen bei langen Titeln und kleinen Viewports wachsen koennen.

### 3. Gemeinsame Medienbausteine ueberarbeiten

1. `Components/Shared/Media/MediaBox.razor` sowie zugehoerige Styles fuer
   Poster-, Episoden- und Metadatenkarten vereinheitlichen.
2. Hover-, Fokus-, Quick-Play-, deaktivierte-, Lade- und Fehlerzustaende
   sichtbar und tastaturbedienbar umsetzen. Bildfehler, fehlende Metadaten und
   lange Titel duerfen das Raster nicht verschieben.
3. `MediaBase.razor` und gemeinsame Home-Komponenten nur strukturell/stilistisch
   aendern; Datenabrufe, Navigation und Aktionen unveraendert anbinden.

### 4. Stitch-Ansichten in bestehende Seiten uebertragen

1. Dashboard: Hero-/Kopfbereich, Continue-Watching, Favoriten, zuletzt
   hinzugefuegte Medien und Genre-Abschnitte an Raster, Hierarchie und Karten-
   Darstellung der Referenz angleichen.
2. Serien- und Filmansichten: Filter-/Quellenbereich, Listen, Raster und
   leere beziehungsweise ladende Zustaende angleichen.
3. Detailansichten in `Pages/Movies/` und `Pages/TV/`: Header, Poster,
   Metadaten, Zurueck-/Play-Aktionen und Episoden-/Inhaltsbereiche auf die
   Referenz abbilden, ohne bestehende Ereignisse oder Routen zu entfernen.
4. `VideoPlayer.razor` und `wwwroot/js/continueWatching.js`: Player-Overlay,
   Fortschrittsanzeige und Continue-Watching-Interaktion visuell integrieren.
   Vorhandene JavaScript-Aufrufe und Fortschrittsspeicherung bleiben bestehen.

### 5. Responsive und zugangsbezogene Anpassungen

1. Desktop-Abnahme bei 1440 px und 1280 px Breite, Mobile-Abnahme bei 390 px
   und 768 px Breite vorsehen. Die Werte gelten als verbindliche interne
   Abnahmematrix; der Browser fuer die visuelle Abnahme ist Chromium.
2. Navigation, Karten, Detailbereiche, StatusTicker und Player auf Mobile ohne
   Ueberlappung, horizontales Abschneiden oder unbedienbare Overlays pruefen.
3. Semantische Elemente, sichtbare Fokuszustaende, Tastaturbedienung,
   ausreichende Kontraste und Formular-/Fehlerzustaende beibehalten oder
   verbessern. Mangels gesonderter Vorgabe gilt WCAG 2.1 AA als Zielprofil.
4. Montserrat und Inter nur lokal beziehungsweise ueber bereits erlaubte
   Projektmechanismen einbinden; bei fehlender Freigabe werden System-Fallbacks
   mit gleicher visueller Hierarchie verwendet.

### 6. Verifikation

1. Bestehende .NET-Tests fuer `VideoWebPlayer` und relevante Integrationen
   ausfuehren; mindestens Build sowie die vorhandenen Tests der betroffenen
   Anwendung muessen erfolgreich sein.
2. Einen manuellen oder automatisierten Chromium-Durchlauf fuer alle fuenf
   Stitch-Ansichten bei den vier Viewports durchfuehren.
3. Je Hauptansicht Standard-, Hover-, Fokus-, deaktivierte-, Lade- und
   Fehlerzustaende pruefen; Navigation angemeldet, abgemeldet und mit
   eingeschraenkter Rolle pruefen.
4. Mit den Stitch-Screenshots vergleichen und besonders Farben, Typografie,
   Abstaende, Raster, Kartenverhaeltnisse, Player-Overlay und aktive Navigation
   dokumentieren.

## Geplante Aenderungsbereiche

- `VideoWebPlayer/wwwroot/app.css`
- `VideoWebPlayer/wwwroot/css/site.css`
- `VideoWebPlayer/Components/Layout/MainLayout.razor`
- `VideoWebPlayer/Components/Layout/NavMenu.razor`
- `VideoWebPlayer/Components/Layout/NavMenu.razor.css`
- `VideoWebPlayer/Components/Shared/StatusTicker.razor`
- `VideoWebPlayer/Components/Shared/Media/`
- `VideoWebPlayer/Components/Shared/Home/`
- `VideoWebPlayer/Components/Pages/Home/Home.razor`
- `VideoWebPlayer/Components/Pages/Movies/`
- `VideoWebPlayer/Components/Pages/TV/`
- `VideoWebPlayer/wwwroot/js/continueWatching.js`

Die konkrete Dateiliste darf waehrend der Umsetzung auf tatsaechlich
betroffene komponentenspezifische `.razor.css`-Dateien erweitert werden. API-,
Service-, Datenbank-, Authentifizierungs- und Routingvertraege duerfen nur
angepasst werden, wenn die bestehende UI sonst nicht funktionsfaehig bleibt.

## Abnahmekriterien

- Alle fuenf Stitch-Referenzansichten sind im `VideoWebPlayer` konsistent
  umgesetzt.
- Bestehende fachliche Aktionen, Routen, Rollen-/Quellenlogik und
  Continue-Watching-Funktionen bleiben nutzbar.
- Desktop- und Mobile-Viewports zeigen keine abgeschnittenen oder
  ueberlappenden Inhalte.
- Farben, Typografie, Abstaende, Raster, Komponenten und visuelle Hierarchie
  entsprechen der Referenz innerhalb der bestehenden Blazor-Struktur.
- Interaktive, Fokus-, deaktivierte-, Lade- und Fehlerzustaende sind erkennbar.
- Build, relevante bestehende Tests und die visuelle Viewport-Abnahme sind
  erfolgreich dokumentiert.

## Offene Punkte

Keine. Die im Anforderungs- und Inventardokument offenen Entscheidungen sind
fuer diesen Plan als verbindliche Annahmen festgelegt: Ziel ist `VideoWebPlayer`,
der erste Lieferumfang sind die fuenf Stitch-Ansichten, die Abnahme erfolgt auf
Chromium bei 1440/1280/768/390 px, WCAG 2.1 AA ist das Zielprofil und die
vorhandene dunkle Produktidentitaet wird in das Stitch-Charcoal-/Rot-/Blau-
Design ueberfuehrt. Webfonts werden nur bei vorhandener Projektfreigabe
eingebunden.
