# Bestandsaufnahme: Sendeplan-Einträge als Panels (Bild + Titel + Beschreibung)

Analyse des TV-Sender-/Programmauskunft-Bereichs bezogen auf die Anforderung, Sendeplan-Einträge
in Admin-Sendeplan und Live-TV-Vorschau als `program-card`-Panels (Prototyp
`sendeplan-vorschau.html`) darzustellen.

## Zusammenfassung

- **Programmauskunft existiert** (`api/program-guide/now|upcoming`, `ProgramGuideService` →
  `DtoProgramGuideEntry`), liefert aber nur Titel/Zeiten/Fortschritt — **kein** Poster, keine
  Beschreibung, kein Genre, keine strukturierte Episoden-Info.
- **Metadaten vorhanden:** `Movie.Plot`/`GenreNames`/`PosterPictureId`, `TVShowEpisode.Plot`/
  `Number`/`PosterPictureId`, `TVShowSeason.Name` („Staffel n"), `TVShow.GenreNames`/
  `PosterPictureId` — aus NFO-Import. Bare `MediaItem`-Slots haben weder Bild noch Beschreibung.
- **Bild-Infrastruktur vorhanden:** `GET /api/pictures/{id}` (auth, Platzhalter-PNG-Fallback),
  `MediaPictureUrlResolver` baut `?access_token=`-URLs, `/images/placeholder.png` existiert.
  Kein neuer Endpunkt nötig.
- **Admin-Sendeplan** ist aktuell eine Tabelle (`#tvsender-schedule-table`,
  `.tvsender-slot-row`/`.tvsender-slot-displaced`); `ReloadScheduleAsync` löst nur Namen auf —
  muss um Poster/Plot/Genre/Episoden-Daten erweitert werden.
- **Live-TV-Vorschau** zeigt `livetv-card`-Textblöcke und eine Text-Liste
  (`.livetv-upcoming-row`) — wird zu kompakten `program-card`s umgebaut.
- **CSS:** `program-card`-Klassen existieren nicht; gemeinsames Karten-CSS gehört in
  `app.css` (Admin nutzt `--admin-*`-, LiveTV `--vp-*`-Tokens).
- **E2E-Verträge:** Selektoren `.livetv-upcoming-row`, `#live-tv-now-title`, `#live-tv-next`,
  `.tvsender-slot-row`, `.tvsender-slot-displaced` werden von Playwright-Tests benutzt —
  beim Umbau erhalten oder Tests anpassen.

**Test-Ausgangszustand:** Baseline-Lauf 2164 erfolgreich / 2 fehlgeschlagen / 1 übersprungen
(gesamt 2167); beide Fehlschläge im Einzel-Wiederholungslauf bestanden → Last-Flakes, effektiv
sauber (Deckung mit bekanntem Stand „~2165 grün + 1 Alt-Skip"). Details: `inventory/tests.md`.
Build läuft über `--artifacts-path artifacts/schritt24-bin`, weil `bin/Debug` von der laufenden
Benutzer-App gesperrt ist (nicht beendet).

## Details

- [Datenmodell](inventory/models.md) — `ProgramSlot`, `Movie`, `TVShowEpisode`, `TVShowSeason`,
  `TVShow`, `MediaItem`, `MediaBaseEntry`, `Picture`, `DtoProgramGuideEntry`
- [Logik](inventory/logic.md) — `ProgramGuideService`, `ProgramGuideController`,
  `PicturesController`, `EpisodesController`, `MediaPictureUrlResolver`, `TVSenderAdmin`,
  `LiveTv`, `IProgramGuideApiClient`, CSS-Konventionen
- [UI](inventory/ui.md) — Prototyp-Struktur, bestehende Markups, Bild-URL-Muster
- [Tests](inventory/tests.md) — Test-Ausgangszustand, relevante Testklassen, Hilfsmethoden
