# UI — Bestandsaufnahme

## Prototyp (maßgeblich, freigegeben)
Datei: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/preview/sendeplan-vorschau.html`

Zwei Varianten derselben `program-card`:
- **Voll** (`program-grid`, Admin): `.program-card` + `.program-poster` (92 px, 2:3) +
  `.program-body` mit `.program-badge` (`badge-live`/`badge-genre`), `.program-time`,
  `.program-title`, `.program-sub`, `.program-desc` (3-Zeilen-Clamp);
  laufender Slot `.slot-now` mit `.progress-track`/`.progress-fill`.
- **Kompakt** (`upcoming-col`, Live-TV): `.program-card.compact` (Poster 56 px,
  `.program-desc` 2-Zeilen-Clamp), laufende Karte `.now-card`;
  „Jetzt läuft" mit LIVE-Badge+Fortschritt, „Die nächsten Stunden" mit Uhrzeit+Titel+Beschreibung.
- Platzhalter-Poster: Verlauf + Text „Kein Bild (Platzhalter)".

## `TVSenderAdmin.razor` — Sendeplan-Tab (Zeilen 307–366)
- Toolbar: `#tvsender-generate-now`, `#tvsender-cleanup-displaced`, `#tvsender-reset-schedule`,
  Zähler-Chip `@schedule.Count Einträge`.
- Leer: `.admin-empty-state`.
- Inhalt: `table#tvsender-schedule-table` mit `tr.tvsender-slot-row` / `.tvsender-slot-displaced`;
  Spalten Start, Ende, Inhalt (`SlotTitle`), Regel (`RuleName`), Status-Chip
  („Geplant" `admin-chip-success` / „Verdrängt" `admin-chip-warning`).
- Status-Text `#tvsender-schedule-status`.

## `LiveTv.razor` (Zeilen 37–117)
- `.livetv-page` → Header, `.livetv-player-wrap` (Video `#live-tv-video` oder Sendelücke
  `#live-tv-no-program`), danach gestapelt:
  - `#live-tv-now` (`.livetv-card`, Kicker „Jetzt läuft", `#live-tv-now-title`, Zeiten,
    Restlaufzeit, `.livetv-progress`/`.livetv-progress-fill`),
  - `#live-tv-next` („Gleich", nächster Titel+Uhrzeit),
  - `#live-tv-upcoming` („Die nächsten Stunden", `li.livetv-upcoming-row[data-slot-id]` mit
    `.livetv-upcoming-time` + `.livetv-upcoming-title`).
- `LiveTv.razor.css`: `livetv-*`-Stile, `--vp-*`-Tokens.

## Bild-URL-Muster
`/api/pictures/{pictureId}?access_token={Client.AuthorizationToken}` — Hilfsmethode
`MediaPictureUrlResolver.Resolve` (Playlist-Kontext), identische Inline-Muster in
`ContinueWatchingList.razor`, `FavoritesList.razor`, `MediaSourceDetails.razor`.
Platzhalter-Datei `/images/placeholder.png` vorhanden (`VideoWebPlayer/wwwroot/images/`).

## Vergleichbare Karten-Muster im Projekt
- Playlist-/Medien-Kacheln (`media-box`/`media-card` in `app.css`) — Poster-`img` mit
  `object-fit: cover`, Token-Farben.
- `MediaSearchSelector` — Such-/Auswahlkomponente (für diese Anforderung nicht relevant,
  reine Anzeige).
