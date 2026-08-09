# Plan-Review: Redesign der Webanwendung

Status: Offene Aufgaben vorhanden

## Ergebnis

Der Plan ist im Arbeitsbaum teilweise umgesetzt. Die Aenderungen sind auf
`VideoWebPlayer` begrenzt. Die zentralen Design-Tokens, das dunkle Charcoal-
Farbsystem, Rot-/Blau-Akzente, Poster-/Episodenverhaeltnisse, responsive
Raster, Navigation, Medienkarten, Home-Abschnitte, Detailseiten und das
Player-Overlay wurden teilweise bzw. weitgehend angepasst.

Die Umsetzung kann daher noch nicht als vollstaendig umgesetzt bewertet
werden. Die folgenden offenen Aufgaben verhindern die Abnahme:

## Offene Aufgaben

1. Die verbindliche visuelle Chromium-Abnahme fuer alle fuenf Ansichten bei
   1440, 1280, 768 und 390 px ist nicht dokumentiert. Es fehlen Nachweise zu
   abgeschnittenen oder ueberlappenden Inhalten und der Vergleich mit den
   Stitch-Screenshots.
2. Die geforderte Pruefung der Standard-, Hover-, Fokus-, deaktivierten-,
   Lade- und Fehlerzustaende je Hauptansicht ist nicht dokumentiert. Gleiches
   gilt fuer angemeldete, abgemeldete und rollenbeschraenkte Navigation.
3. `VideoWebPlayer/wwwroot/js/continueWatching.js` wurde gegenueber dem
   Arbeitsstand nicht geaendert. Damit ist die geplante visuelle Integration
   der Continue-Watching-Interaktion in diesem Bereich nicht nachgewiesen;
   insbesondere fehlen Aufraeumung der registrierten Event-Handler und ein
   Nachweis des Fortschrittsverhaltens im Player.
4. `StatusTicker.razor` wurde nicht angepasst und es gibt keinen Nachweis,
   dass der bestehende globale Statusbereich in den vier mobilen Viewports
   ohne Ueberlappung oder unbedienbares Overlay funktioniert.
5. Episode-Karten in `TVShowDetails.razor` erhalten zwar `role="button"` und
   `tabindex="0"`, reagieren aber nur auf `@onclick`. Eine Tastaturaktion
   ueber Enter oder Leertaste ist nicht implementiert, womit die geplante
   Tastaturbedienbarkeit fuer diese Interaktion nicht vollstaendig erfuellt
   ist.
6. Die geplante Datei `VideoWebPlayer/wwwroot/css/site.css` existiert im
   Arbeitsbaum nicht. Die globale Tokenisierung liegt stattdessen nur in
   `app.css`; es ist nicht belegt, dass damit alle vorgesehenen globalen
   Bootstrap- und Seiten-Overrides abgedeckt sind.

## Erfuellte bzw. verifizierte Teile

- Die globalen Tokens in `VideoWebPlayer/wwwroot/app.css` enthalten Charcoal,
  Rot, Blau, Montserrat-/Inter-Fallbacks, Abstaende, Radien und Fokusfarben.
- Medienkarten verwenden 2:3, Episoden-/Playerbereiche 16:9 und besitzen
  Lazy-Loading, Bildfehler-Placeholder, Hover-/Fokus-Styling und Metadaten-
  Overlays.
- Die aktive Navigation, Quellenlinks, Security-Badge-Logik und bestehende
  Routen-/Servicebindungen wurden nicht aus der sichtbaren Implementierung
  entfernt.
- `dotnet build VideoWebPlayer/VideoWebPlayer.csproj --no-restore` war
  erfolgreich. `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj
  --no-restore` war erfolgreich: 67 von 67 Tests bestanden.
- Beim Build wurde die bestehende Warnung NU1903 zu einer bekannten
  Sicherheitsanfaelligkeit in `SQLitePCLRaw.lib.e_sqlite3` ausgegeben.

## Referenzen

- Tokens und responsive Regeln: `VideoWebPlayer/wwwroot/app.css:1`
- Medienkarten: `VideoWebPlayer/Components/Shared/Media/MediaBox.razor:2`
- Player-Overlay und Fortschrittsanzeige:
  `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor:8`
- Fehlende Tastaturaktion fuer Episode-Karten:
  `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor:344`
- Verbindliche Viewport- und Zustandsabnahme:
  `docs/features/task/issue-95-6915988b38d349d498d47ce65caa780c-redesign-der-webanwendung/plan.md:98`
