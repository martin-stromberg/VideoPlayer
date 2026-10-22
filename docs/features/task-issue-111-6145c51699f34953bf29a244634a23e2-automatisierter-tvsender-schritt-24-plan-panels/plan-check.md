# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Admin-Sendeplan: volle Karten (Poster 2:3, Uhrzeit, Genre-Badge, Titel, Episoden-Info, 3-zeilige Beschreibung) | Schritt 6: `program-grid`/`program-card` mit allen Elementen; `ScheduleCardContent` liefert Poster/Plot/Genre/Episoden-Daten | `TVSenderAdminTests.Sendeplan_RendersProgramCards_WithBadgesAndProgress` + E2E `TVSenderAdminE2ETests` | Abgedeckt |
| 2. Laufender Slot: LIVE-Badge + Fortschrittsbalken | `.slot-now` + `badge-live` + `.progress-track` (Admin) | bUnit-Test prüft Badge/Track; E2E-Markup | Abgedeckt |
| 3. Live-TV „Jetzt läuft": kompakte Karte mit Poster, LIVE, Fortschritt | `.program-card.compact.now-card#live-tv-now` | `LiveTvTests.Upcoming_RendersCompactProgramCards` + E2E | Abgedeckt |
| 4. „Die nächsten Stunden": kompakte Karten neben/unter Player | `.livetv-layout`-Grid + `.livetv-side`, `li.livetv-upcoming-row` als `.program-card.compact` | bUnit + `LiveTvE2ETests` | Abgedeckt |
| 5. Platzhalter bei fehlendem Bild/Beschreibung | `MediaPictureUrlResolver` → `placeholder.png`; `NoDescriptionText` | bUnit-Test „fehlendes Bild → placeholder.png", `Bare`-Service-Test | Abgedeckt |
| 6. `api/program-guide` liefert Poster-Verweis, Beschreibung, Genre, Episoden-Info; Bild via `/api/pictures/{id}`+`access_token`; kein neuer Endpunkt | Schritte 2–3 (DTO-Felder + `LoadDetailsAsync`); `MediaPictureUrlResolver` | `ProgramGuideServiceTests` neue Feld-Tests; `VideoWebPlayerClientTests_ProgramGuide` HTTP-Nachweis | Abgedeckt |
| 7. Prototyp-Optik in App-Konvention | Schritt 5: `program-card`-CSS in `app.css`, Tokens statt Prototyp-Farben | E2E-Sichtprüfung der gerenderten Karten | Abgedeckt |
| 8. Keine Regression (Toolbar, Verdrängt, Player, Polling) | Toolbar/Leer-Status unverändert; `.tvsender-slot-row`/`.tvsender-slot-displaced`/„Geplant"/„Verdrängt" bleiben; Player/Polling unberührt; `#tvsender-schedule-table` wird in keinem Test referenziert (verifiziert per grep) | Bestehende E2E/bUnit laufen weiter | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Live-TV-Seite mit kompakten Karten aufrufen (AK 3–5) | `LiveTvE2ETests.LiveTv_ViaNavMenu_ShowsPlayerAndProgram` (erweitert: `.program-card`-Assertion, Seed mit `Picture`/`Plot`) | Abgedeckt |
| Admin öffnet Sendeplan-Tab, sieht Karten inkl. Status (AK 1, 2, 8) | `TVSenderAdminE2ETests` bestehender Sendeplan-Teil (erweitert) | Abgedeckt |

## Hinweise

- `SeasonNumber` wird aus `SeasonName` gelesen (Entity hat kein Zahlenfeld); `Specials`-Namen
  liefern `null` → kompakte Anzeige fällt auf „E{n}" zurück — im Helper kapseln.
- Neue DTO-Felder sind nullable → keine Breaking Change für API-Clients.
