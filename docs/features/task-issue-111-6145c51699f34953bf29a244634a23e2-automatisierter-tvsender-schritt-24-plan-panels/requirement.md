# Übersetzte Anforderung: Sendeplan-Einträge als Panels (Bild + Titel + Beschreibung)

## Fachliche Zusammenfassung

Die Sendeplan-Anzeigen des automatisierten TV-Senders werden von reinen Textlisten auf Panel-Karten
nach dem freigegebenen Prototyp `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/preview/sendeplan-vorschau.html`
umgestellt — in **beiden** Ansichten:

1. **Admin-Sendeplan** (`TVSenderAdmin.razor`, Tab „Sendeplan"): Die bisherige Tabelle wird durch
   volle Programm-Karten ersetzt — Poster im Seitenverhältnis 2:3 links, Uhrzeit mit Genre-Badge,
   Titel, Episoden-Info (Staffel/Episode) und 3-zeilig abgeschnittene Beschreibung. Der aktuell
   laufende Sendeplatz erhält ein LIVE-Badge und einen Fortschrittsbalken. Bei fehlendem Bild oder
   fehlender Beschreibung greifen saubere Platzhalter.
2. **Live-TV-Vorschau** (`LiveTv.razor`): Neben bzw. unter dem Player erscheinen kompakte Karten —
   „Jetzt läuft" mit LIVE-Badge und Fortschrittsbalken sowie „Die nächsten Stunden" mit kleinem
   Poster, Uhrzeit, Titel und 2-zeiliger Beschreibung.

Dafür muss die Programmauskunft (`api/program-guide`) ihre Einträge um Poster, Beschreibung und
die für die Karten nötigen Zusatzdaten (Genre, Episoden-Info) erweitern.

## Betroffene Klassen und Komponenten

- `DtoProgramGuideEntry` (`VideoWebPlayer.Client/Models/DtoProgramGuideEntry.cs`) — neue Felder
  für Poster, Beschreibung, Genre und Episoden-Info (Details nach Bestandsaufnahme).
- `DtoProgramGuideNow` — indirekt betroffen (enthält `DtoProgramGuideEntry`), voraussichtlich
  keine eigenen neuen Felder.
- `ProgramGuideService` (`VideoWebPlayer/Services/ProgramGuideService.cs`) — `LoadTitlesAsync`/
  `ToEntry` laden bisher nur Titel; müssen zusätzlich `Plot`, `PosterPictureId`, Genre und
  Staffel-/Episoden-Angaben auflösen.
- `TVSenderAdmin.razor` (`VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`) — Sendeplan-Tab:
  `schedule`-Tabelle → Kartenliste; `ReloadScheduleAsync` lädt bisher nur Namen, muss Poster/Plot/
  Genre/Episoden-Info ergänzen.
- `LiveTv.razor` (`VideoWebPlayer/Components/Pages/LiveTv.razor`) — Jetzt/Gleich/Vorschau-Bereiche
  als kompakte Karten; Bild-URL mit `access_token`-Query (`MediaPictureUrlResolver`-Muster).
- `app.css` (`VideoWebPlayer/wwwroot/app.css`) — `program-card`-Design aus dem Prototyp in
  App-Konvention (`--vp-*`- bzw. `--admin-*`-Tokens), zwei Varianten (voll/kompakt).
- Bestehender Bild-Endpunkt `GET /api/pictures/{id}` (`PicturesController`) — wird für Poster
  wiederverwendet; Aufruf aus `<img>` mit `?access_token=` wie bei den übrigen Kartenbildern.
- Tests: `LiveTvTests`, `TVSenderAdminTests` (bUnit), `ProgramGuideServiceTests`,
  `VideoWebPlayerClientTests_ProgramGuide`, ggf. `LiveTvE2ETests`/`TVSenderAdminE2ETests`
  (bestehende Selektoren beachten).

## Implementierungsansatz

- `DtoProgramGuideEntry` um nullable Felder erweitern (voraussichtlich `PosterPictureId`,
  `Description`/`Plot`, `GenreName` sowie Episoden-Angaben wie `SeasonName`/`EpisodeNumber`/
  `EpisodeName` bzw. eine vorformatierte Unterzeile — genaue Feldwahl in der Planung).
- `ProgramGuideService` löst pro Slot-Gruppe (Movie / Episode / bare MediaItem) die Zusatzdaten
  gebatcht mit aus — wie bisher `LoadTitlesAsync`, kein N+1.
- Poster-Quelle: `PosterPictureId` der Movie-/Episode-Entität; für Episoden sinnvoller Fallback
  über Season-/Show-Poster; Bare-MediaItems ohne Bild → Platzhalter `/images/placeholder.png`
  bzw. CSS-Platzhalter wie im Prototyp.
- Beschreibung: `Movie.Plot` bzw. `TVShowEpisode.Plot` (NFO-`plot`); leer → Fallback-Text.
- Genre-Badge: erstes Genre (`Movie.GenreNames` / `TVShow.GenreNames`, kommagetrennte NFO-Strings).
- Episoden-Info: aus `TVShowSeason.Name` („Staffel N"), `TVShowEpisode.Number` und `TVShowEpisode.Name`.
- Admin-View lädt dieselben Daten direkt via `ApplicationDbContext` (bestehendes Muster in
  `ReloadScheduleAsync`), die Live-TV-View über die erweiterten DTO-Felder.
- CSS: `.program-card`, `.program-poster`, `.program-body`, `.program-time`, `.program-title`,
  `.program-sub`, `.program-desc`, `.program-badge`, `.badge-live`, `.badge-genre`,
  `.progress-track`/`.progress-fill`, `.slot-now` sowie `.program-card.compact`/`.now-card`
  aus dem Prototyp; Farbwerte auf die App-Tokens (`--vp-*`, `--admin-*`) mappen.
- Bestehende E2E-Selektoren erhalten bzw. Tests anpassen: `.livetv-upcoming-row`,
  `#live-tv-now-title`, `#live-tv-next`, `.tvsender-slot-row`, `.tvsender-slot-displaced`.

## Akzeptanzkriterien

1. Admin-Sendeplan-Tab zeigt jeden Sendeplatz als volle Programm-Karte: Poster 2:3 links
   (oder Platzhalter), Uhrzeit (Start–Ende), Genre-Badge, Titel, Episoden-Info
   (Staffel/Episode), 3-zeilige Beschreibung.
2. Laufender Sendeplatz im Admin-Sendeplan trägt LIVE-Badge und Fortschrittsbalken.
3. Live-TV-Seite zeigt „Jetzt läuft" als kompakte Karte mit Poster, LIVE-Badge und
   Fortschrittsbalken.
4. Live-TV-Seite zeigt „Die nächsten Stunden" als kompakte Karten mit kleinem Poster,
   Uhrzeit, Titel und 2-zeiliger Beschreibung — neben oder unter dem Player.
5. Fehlendes Bild → Platzhalter (kein Broken-Image); fehlende Beschreibung → sauberer
   Fallback-Text statt leerem Bereich.
6. `api/program-guide` liefert die für die Karten nötigen Felder (Poster-Verweis,
   Beschreibung, Genre, Episoden-Info); Poster werden über den bestehenden
   `/api/pictures/{id}`-Endpunkt mit `access_token`-Query geladen — kein neuer
   Bild-Endpunkt, keine Token im Log.
7. Optik entspricht dem freigegebenen Prototyp `sendeplan-vorschau.html` (Layout/Struktur),
   umgesetzt in App-Konvention (Tokens/statt Prototyp-Farbpalette wo Tokens existieren).
8. Keine Regression: bestehende Funktion (Generieren/Bereinigen/Zurücksetzen,
   Verdrängt-Markierung, Player-Anbindung, Polling) bleibt erhalten.

## Konfiguration

Keine — das Verhalten ist fest und nicht konfigurierbar.

## Offene Fragen

- Soll die Admin-Ansicht die Tages-Pills aus dem Prototyp (Mo–Fr-Auswahl) übernehmen? — Vorschlag:
  nein, die Anforderung nennt nur „volle Karten"; die Tabelle wird 1:1 durch Karten ersetzt,
  Toolbar und Status („Geplant"/„Verdrängt") bleiben.
- Kompaktkarten „Die nächsten Stunden" — mit oder ohne Genre-Badge? Prototyp zeigt dort nur
  Uhrzeit+Titel+Beschreibung → Vorschlag: ohne Badge, Genre nur in der vollen Admin-Karte.
