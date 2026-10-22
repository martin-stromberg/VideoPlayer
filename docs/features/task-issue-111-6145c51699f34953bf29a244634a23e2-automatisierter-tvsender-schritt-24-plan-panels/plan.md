# Umsetzungsplan: Sendeplan-Einträge als Panels (Bild + Titel + Beschreibung)

## Übersicht

Sendeplan-Einträge werden in beiden Ansichten als `program-card`-Panels nach dem freigegebenen
Prototyp `sendeplan-vorschau.html` dargestellt: volle Karten im Admin-Sendeplan
(`TVSenderAdmin.razor`, Poster 2:3, Uhrzeit, Genre-Badge, Titel, Episoden-Info, 3-zeilige
Beschreibung, LIVE-Badge+Fortschritt beim laufenden Slot) und kompakte Karten in der
Live-TV-Vorschau (`LiveTv.razor`, kleines Poster, Uhrzeit, Titel, 2-zeilige Beschreibung).
Dafür liefert `api/program-guide` zusätzlich Poster-Verweis, Beschreibung, Genre und strukturierte
Episoden-Angaben je `DtoProgramGuideEntry`.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| DTO-Erweiterung | Strukturierte Felder (`PosterPictureId`, `Description`, `GenreName`, `ShowName`, `SeasonName`, `SeasonNumber`, `EpisodeNumber`, `EpisodeName`) statt vorformatierter Anzeige-Strings | Beide Ansichten formatieren die Episoden-Info unterschiedlich („Staffel 6, Episode 42 — „Titel"" vs. „S6 E42"); strukturierte Felder bleiben auch für externe API-Clients nutzbar. `Title` bleibt unverändert abwärtskompatibel. |
| Poster-Quelle | `PosterPictureId` der gesendeten Entität; bei Episoden Fallback-Kette Episode → Staffel → Serie; Bare-MediaItems → `null` → `/images/placeholder.png` | `PosterPictureId` ist der kanonische NFO-Poster-Verweis (`MediaBaseEntry`); `GeneratedBackgroundPictureId` ist Querformat und lazy-generiert — ungeeignet für 2:3-Poster. |
| Poster-Auslieferung | Bestehender `GET /api/pictures/{id}` mit `?access_token=` via `MediaPictureUrlResolver.Resolve` | Einheitliches Muster aller Kartenbilder; kein neuer Endpunkt, keine Tokens in Logs. |
| Beschreibungs-Quelle | `Movie.Plot` bzw. `TVShowEpisode.Plot`, bei Episoden Fallback auf `TVShow.Plot` | NFO-`plot`; Serienbeschreibung als Fallback ist übliche EPG-Praxis und vermeidet überflüssige Platzhalter. |
| Genre-Badge | Erster Eintrag des kommagetrennten `GenreNames` (Movie bzw. TVShow der Episode) | `GenreNames` ist denormalisiert aus NFO — kein Join auf `MovieGenres`/`TVShowGenres` nötig. |
| Episoden-Info-Formatierung | Neuer statischer Helper `ProgramGuideEntryText` (`VideoWebPlayer/Services/`): `SeasonNumberFromName`, `Subtitle`, `ShortSubtitle`, `FirstGenre`, Konstante `NoDescriptionText` | Formatierung liegt an zwei Stellen an (DTO-Service + Admin-Razor, das direkt per EF lädt); gemeinsamer Helper verhindert Divergenz. |
| Admin-Datenbeschaffung | Direkter EF-Zugriff in `ReloadScheduleAsync` (bestehendes Muster), kein Umweg über `api/program-guide` | Die Seite arbeitet bereits so; zusätzlich ist der Admin-Plan unbegrenzt (200 Slots) und enthält `Displaced`-Slots, die die API bewusst nicht liefert. |
| Karten-CSS | `.program-card*` zentral in `app.css` (`--vp-*`-Tokens), `.admin-console`-Overrides auf `--admin-*`; `livetv-layout`-Grid in `LiveTv.razor.css` | Beide Seiten teilen sich das Design; Prototyp-Farben werden auf App-Tokens gemappt (`badge-live`/Fortschritt → `--vp-primary` rot, Akzente → `--vp-secondary` blau). |
| „Gleich"-Block in `LiveTv` | Bleibt als kompakte `program-card` mit eigener `#live-tv-next`-Kachel | Prototyp hat keinen dedizierten „Gleich"-Block, aber bestehendes Feature + E2E-Vertrag bleiben erhalten. |
| Tages-Pills (Mo–Fr) aus Prototyp A | Werden **nicht** umgesetzt | Anforderung nennt nur „volle Karten"; die vorhandene Toolbar/Sortierung bleibt. |

## Programmabläufe

### Programmauskunft liefert Karten-Daten

1. `GetNowAsync`/`GetUpcomingAsync` laden wie bisher die `ProgramSlot`s.
2. `LoadDetailsAsync` (umbenannte/erweiterte `LoadTitlesAsync`) löst gebatcht pro Gruppe auf:
   Movies (`Name`, `Plot`, `PosterPictureId`, `GenreNames`), Episoden (`Name`, `Number`, `Plot`,
   `PosterPictureId`, `TVShowSeason.Name`, `TVShowSeason.PosterPictureId`, `TVShow.Name`,
   `TVShow.Plot`, `TVShow.PosterPictureId`, `TVShow.GenreNames`), Bare-Items (`Name`).
3. `ToEntry` befüllt zusätzlich: `PosterPictureId` (Episode → Staffel → Serie / Film),
   `Description` (Episoden-`Plot` → Serien-`Plot` / Film-`Plot`), `GenreName` (erster
   `GenreNames`-Eintrag), bei Episoden `ShowName`, `SeasonName`, `SeasonNumber`,
   `EpisodeNumber`, `EpisodeName`. `Title` bleibt wie bisher.

Beteiligte Klassen: `ProgramGuideService`, `DtoProgramGuideEntry`, `ProgramGuideEntryText`.

### Admin-Sendeplan rendert volle Karten

1. `ReloadScheduleAsync` baut zusätzlich `slotCards: Dictionary<long, ScheduleCardContent>`
   (Titel-Fallback-Kette wie `SlotTitle` + `ShowName`/`SeasonName`/`EpisodeNumber`/`EpisodeName`/
   `Description`/`GenreName`/`PosterPictureId`).
2. Markup: `table` → `div.program-grid#tvsender-schedule-cards`; je Slot
   `div.program-card.tvsender-slot-row` (`.tvsender-slot-displaced` bei `Displaced`,
   `.slot-now` bei laufendem geplanten Slot).
3. Karte: `.program-poster` (`<img>` via `MediaPictureUrlResolver`), `.program-body` mit
   Badge-Zeile (Genre-Badge, LIVE-Badge bei laufendem Slot, Status-Chip
   „Geplant"/„Verdrängt"), `.program-time` (`dd.MM. HH:mm – HH:mm`), `.program-title`
   (`ShowName ?? Title`), `.program-sub` (Episoden-Info via `Subtitle` + „Regel: {Name}"),
   `.program-desc` (Beschreibung oder `NoDescriptionText`), `.progress-track` beim laufenden Slot.
4. Toolbar, Leer-Zustand und `#tvsender-schedule-status` bleiben.

Beteiligte: `TVSenderAdmin.razor`, `MediaPictureUrlResolver`, `ProgramGuideEntryText`.

### Live-TV rendert kompakte Karten

1. `.livetv-page` erhält `.livetv-layout`-Grid: Player-Spalte + `.livetv-side`-Spalte.
2. „Jetzt läuft": `.program-card.compact.now-card#live-tv-now` mit LIVE-Badge,
   `.program-time` (`HH:mm – HH:mm` + Restlaufzeit), `.program-title#live-tv-now-title`,
   `.program-sub` (`ShortSubtitle`), `.program-desc`, `.progress-track`.
3. „Gleich": `.program-card.compact#live-tv-next` mit Kicker „Gleich", Uhrzeit, Titel.
4. „Die nächsten Stunden": `#live-tv-upcoming`; je Eintrag `li.livetv-upcoming-row` als
   `.program-card.compact` (Poster 56 px, `.program-time`, Titel, `.program-desc`).
5. Sendelücke/Player/Fehlerhinweise unverändert.

Beteiligte: `LiveTv.razor`, `LiveTv.razor.css`, `MediaPictureUrlResolver`, `ProgramGuideEntryText`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ProgramGuideEntryText` | statische Klasse (`VideoWebPlayer/Services/`) | Formatierung der Episoden-Unterzeilen und Genre-Auswahl, geteilt von `ProgramGuideService`, `TVSenderAdmin` und `LiveTv` |

## Änderungen an bestehenden Klassen

### `DtoProgramGuideEntry` (Client-DTO)

- **Neue Eigenschaften:**
  - `PosterPictureId` (`long?`) — Bild-Id für `/api/pictures/{id}`
  - `Description` (`string?`) — Handlungsbeschreibung (NFO-`plot`, Serien-Fallback)
  - `GenreName` (`string?`) — erster Genre-Name (Badge)
  - `ShowName` (`string?`) — Serientitel bei Episoden-Slots (Karten-Titel)
  - `SeasonName` (`string?`) — Staffelname (z. B. „Staffel 6", „Specials")
  - `SeasonNumber` (`int?`) — aus `SeasonName` gelesene Ziffern, sonst `null`
  - `EpisodeNumber` (`int?`) — Episodennummer
  - `EpisodeName` (`string?`) — Episodentitel

### `ProgramGuideService` (Service)

- **Geänderte Methoden:** `LoadTitlesAsync` → `LoadDetailsAsync` (Rückgabe `GuideDetails`
  mit Film-/Episoden-/Item-Records statt reiner Titel-Wörterbücher); `ToEntry` befüllt die
  neuen DTO-Felder; `TitleLookup` → `GuideDetails`-Record.

### `TVSenderAdmin.razor` (Blazor)

- **Geändert:** Sendeplan-Tab — Tabelle durch `program-grid`/`program-card` ersetzt;
  `ReloadScheduleAsync` lädt Karten-Daten (Record `ScheduleCardContent` im `@code`-Block);
  `SlotTitle`/`RuleName` bleiben als Text-Quellen erhalten; neue Helfer `IsRunning(slot)`,
  `ProgressPercent(slot)`, `PosterUrl(card)`.

### `LiveTv.razor` + `LiveTv.razor.css` (Blazor)

- **Geändert:** Jetzt/Gleich/Vorschau-Bereiche als kompakte Karten in `.livetv-side`;
  `.livetv-layout`-Grid; `FormatTime`/`ProgressPercent` wiederverwendet; neue Helfer
  `CardTitle(entry)`, `PosterUrl(entry)`, `CardSub(entry)`.

### `app.css`

- **Neu:** `.program-card`, `.program-poster`, `.program-body`, `.program-badge`,
  `.badge-live`, `.badge-genre`, `.program-time`, `.program-title`, `.program-sub`,
  `.program-desc`, `.program-grid`, `.slot-now`, `.now-card`, `.progress-track`,
  `.progress-fill`, `.program-card.compact`; `.admin-console`-Farb-Overrides und
  `.tvsender-slot-displaced`-Kartenvariante.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — reine Anzeige-Erweiterung, keine neuen Eingaben.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **API-Vertrag:** `DtoProgramGuideEntry` erhält nur zusätzliche Felder — abwärtskompatibel
  für externe Clients.
- **E2E-Selektoren:** `.livetv-upcoming-row`, `#live-tv-now-title`, `#live-tv-next`,
  `.tvsender-slot-row`, `.tvsender-slot-displaced`, Texte „Geplant"/„Verdrängt" bleiben im
  neuen Markup erhalten; `#tvsender-schedule-table` wird durch `#tvsender-schedule-cards`
  ersetzt (kein Test bezieht sich auf die Tabellen-Id — verifiziert).
- **Performance:** `LoadDetailsAsync` bleibt gebatcht (3 Queries); `ReloadScheduleAsync`
  wächst analog um dieselben Felder — kein N+1.
- **Zugriffsschutz:** Programmauskunft liefert weiterhin nur `PictureId`s; die eigentliche
  Auslieferung bleibt `[BearerTokenCheck]`-geschützt (Playlist-Cover-Regel unberührt).
- **Token im Markup:** `access_token` in `<img src>` ist das etablierte Muster; Tokens werden
  nicht geloggt.

## Umsetzungsreihenfolge

1. **`ProgramGuideEntryText` anlegen**
   - Voraussetzungen: keine.
   - Beschreibung: statische Klasse in `VideoWebPlayer/Services/` mit `SeasonNumberFromName`,
     `Subtitle`, `ShortSubtitle`, `FirstGenre`, `NoDescriptionText`.
2. **`DtoProgramGuideEntry` erweitern**
   - Voraussetzungen: keine.
   - Beschreibung: 8 neue nullable Felder mit XML-Dokumentation.
3. **`ProgramGuideService` umstellen (`LoadDetailsAsync`/`ToEntry`)**
   - Voraussetzungen: 1, 2.
   - Beschreibung: gebatchte Detail-Queries, Fallback-Ketten Poster/Beschreibung, `SeasonNumber`
     via `SeasonNumberFromName`, `GenreName` via `FirstGenre`.
4. **Service-Tests (`ProgramGuideServiceTests`)**
   - Voraussetzungen: 3.
   - Beschreibung: neue Felder für Movie-/Episoden-/Bare-/gelöschte Inhalte.
5. **`program-card`-CSS in `app.css`**
   - Voraussetzungen: keine.
   - Beschreibung: Prototyp-Design auf App-Tokens gemappt, beide Varianten + Admin-Overrides.
6. **`TVSenderAdmin.razor` umbauen**
   - Voraussetzungen: 1, 5.
   - Beschreibung: `ScheduleCardContent`-Record, `ReloadScheduleAsync`-Erweiterung, Karten-Markup
     inkl. LIVE/Status/Episoden-Info.
7. **`LiveTv.razor` + `LiveTv.razor.css` umbauen**
   - Voraussetzungen: 1, 2, 5 (DTO-Felder kommen über die API).
   - Beschreibung: `.livetv-layout`/`.livetv-side`, kompakte Karten.
8. **bUnit-Tests (`TVSenderAdminTests`, `LiveTvTests`)**
   - Voraussetzungen: 6, 7.
   - Beschreibung: Karten-Markup, Badges, Fortschritt, Platzhalter.
9. **E2E-Ergänzungen (`LiveTvE2ETests`, `TVSenderAdminE2ETests`)**
   - Voraussetzungen: 6, 7.
   - Beschreibung: bestehende konsolidierte Methoden um `.program-card`-Assertions erweitern;
     Seeds um `Picture`/`Plot` ergänzen.
10. **Client-Contract-Test (`VideoWebPlayerClientTests_ProgramGuide`)**
    - Voraussetzungen: 3.
    - Beschreibung: Seed mit Plot/Poster/Genre → Assertions auf neue DTO-Felder.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetNowAsync_MovieSlot_ReturnsCardMetadata` | `ProgramGuideServiceTests` | `PosterPictureId`, `Description`, `GenreName` aus Movie; `ShowName`/`SeasonName`/`Episode*` `null` |
| `GetNowAsync_EpisodeSlot_ReturnsCardMetadata` | `ProgramGuideServiceTests` | `ShowName`, `SeasonName`, `SeasonNumber`, `EpisodeNumber`, `EpisodeName`; Poster-Fallback Episode→Staffel→Serie; `Description` aus Episode-`Plot` |
| `GetNowAsync_EpisodeWithoutPlot_FallsBackToShowPlot` | `ProgramGuideServiceTests` | Serien-`Plot`-Fallback der Beschreibung |
| `GetNowAsync_BareItemSlot_ReturnsNoCardMetadata` | `ProgramGuideServiceTests` | neue Felder alle `null` |
| `SeasonNumberFromName_ParsesStaffel` (parametrisiert) | `ProgramGuideServiceTests` o. eigene kleine Klasse | „Staffel 06"→6, „Specials"→null, null→null |
| `Sendeplan_RendersProgramCards_WithBadgesAndProgress` | `TVSenderAdminTests` | `.program-card.tvsender-slot-row`, Poster-`<img>` mit `access_token`, LIVE-Badge+`.progress-track` beim laufenden Slot, `.tvsender-slot-displaced`+„Verdrängt", Genre-Badge, Episoden-Sub |
| `Upcoming_RendersCompactProgramCards` | `LiveTvTests` | `.program-card.compact` je `livetv-upcoming-row`, Poster-`access_token`, `.program-desc`, fehlendes Bild → `placeholder.png`, Now-Karte `.now-card`+`.badge-live`+`.progress-fill` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `LiveTvTests` (bestehende Methoden) | Selektoren bleiben weitgehend gleich; evtl. minimale Anpassung, falls Markup-Knoten wandern (`.livetv-upcoming-row` bleibt) |
| `VideoWebPlayerClientTests_ProgramGuide` | Seed um Plot/Poster/Genre ergänzen + neue Feld-Assertions |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Live-TV-Seite zeigt kompakte Karten (Jetzt/Vorschau) mit Poster | `LiveTvE2ETests.LiveTv_ViaNavMenu_ShowsPlayerAndProgram` (erweitert) | 3, 4, 5 | Benutzerfluss: Seite laden → Karten im Browser gerendert |
| Pflicht | Admin-Sendeplan zeigt volle Karten inkl. Status | `TVSenderAdminE2ETests` (bestehender Sendeplan-Teil, erweitert) | 1, 2, 8 | Benutzerfluss: Admin öffnet Tab → Karten statt Tabelle |

Bestehende E2E-Anpassungen: `.livetv-upcoming-row`/`.tvsender-slot-row`-Selektoren bleiben —
keine Umbenennung nötig; zusätzlich `.program-card`-Assertions und Seeds mit `Picture`/`Plot`.

## Offene Punkte

Keine.
