# Abnahme Schritt 24 — Sendeplan-Panels mit Bild/Titel/Beschreibung (Admin + Live-TV)

Prüfer: Devin (unabhängig, ohne Implementierungskontext)
Geprüfter Stand: `fd2a70e` auf `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-24-plan-panels`
Gegenstand: Sendeplan-Einträge als Panels (Poster 2:3, Badge, Titel, Episoden-Info, Beschreibung)
im Admin-Sendeplan und in der Live-TV-Vorschau; Design aus freigegebenem Prototyp
`docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/preview/sendeplan-vorschau.html`.

## Ergebnis

**Status:** Abweichungen gefunden

Die fachliche Umsetzung ist verifiziert und vollständig; der einzige Befund betrifft die
Prozesspflege (`handover.md`), nicht den Code.

## Verifikation

- **Diff-Review** `task/...-automatisisierter-tvsender...HEAD`: 17 Dateien, +919/−194.
  `DtoProgramGuideEntry` (+8 nullable Felder: `PosterPictureId`, `Description`, `GenreName`,
  `ShowName`, `SeasonName`, `SeasonNumber`, `EpisodeNumber`, `EpisodeName` — exakt wie berichtet),
  `ProgramGuideService` (`LoadTitlesAsync` → `LoadDetailsAsync` mit `MovieDetails`/`EpisodeDetails`-Projektion,
  `ToEntry` befüllt alle neuen Felder), neuer Helper `ProgramGuideEntryText`,
  `TVSenderAdmin.razor` (Tabelle → `program-grid`-Karten), `LiveTv.razor` (+`.livetv-layout`),
  `LiveTv.razor.css`, `app.css` (+198 Zeilen `.program-card`-Familie), Tests, README/Release-Notes/Hilfe.
- **Build**: `dotnet build VideoPlayer.sln` Debug **und** Release je 0 Fehler, identisch 144
  Warnungen — alle pre-existing (BL0008/CS86xx in unverwandten Dateien). Die einzige Warnung in
  einer geänderten Datei (CS9113 `LiveTvTests.cs:20`, Parameter `_` ungelesen) existierte bereits
  in HEAD~1. Keine neuen Warnungen.
- **Tests**: `dotnet test VideoWebPlayer.Tests` synchron, Dauer ~4 min:
  **2178 grün, 1 Alt-Skip, 0 fehlgeschlagen** (gesamt 2179). Deckt sich mit dem
  Implementierer-Bericht (~2175).
- **Gegenprobe**: beide `entry.PosterPictureId = …`-Zuweisungen in `ToEntry` entfernt ⇒ genau die
  beiden neuen Karten-Tests schlagen fehl
  (`GetNowAsync_MovieSlot_ReturnsCardMetadata` und
  `GetNowAsync_EpisodeSlot_ReturnsCardMetadataAndSeasonPosterFallback`, je `Expected: 1, Actual: null`);
  `GetNowAsync_BareItemSlot_ReturnsNoCardMetadata` bleibt erwartungsgemäß grün (assertiert null),
  der bUnit-Test bleibt grün (mockt das DTO selbst — er prüft Rendering, nicht die Befüllung).
  Danach `git checkout --` — Arbeitsbaum sauber.
- **Poster-Fallback-Kette**: Episode `PosterPictureId ?? SeasonPosterPictureId ?? ShowPosterPictureId`,
  Plot `episode.Plot ?? show.Plot`, Genre aus `show.GenreNames`; Bare-Clips ohne
  Inhaltsreferenz bekommen nur den Item-Namen, `PosterPictureId` bleibt null ⇒
  `MediaPictureUrlResolver` liefert `/images/placeholder.png` (Datei existiert in `wwwroot/images`).
  Alles per E2E-/Service-Tests belegt.
- **access_token**: Bild-URL `/api/pictures/{id}?access_token=…` kommt ausschließlich aus dem
  bestehenden `MediaPictureUrlResolver` (img kann keinen Auth-Header) und landet nur im
  `src`-Attribut — nirgends geloggt (LiveTv loggt nur Exceptions ohne URL; `StreamUrl`-Muster
  unverändert).
- **SeasonNumber-Best-Effort**: `SeasonNumberFromName` sammelt Ziffern zeichenweise —
  null/leer/nicht-numerisch (`"Specials"`) ⇒ null, kein Crash; `"Staffel 06"` ⇒ 6; per
  `[Theory]` abgedeckt (6 Fälle). `ShortSubtitle` behandelt null sauber (`E42` statt `S? E42`).
- **Prototyp-Treue**: `.program-grid`/`.program-card`/`.program-poster` (92 px, aspect-ratio 2/3)/
  `program-badges`/`badge-live`/`badge-genre`/`program-time`/`program-title`/`program-sub`/
  `program-desc` (3-Zeilen-Clamp)/`progress-track`/`progress-fill`/`.slot-now`/`.now-card`/
  `.program-card.compact` (56 px Poster, 2-Zeilen-Clamp) — Klassennamen und Layoutwerte 1:1 aus
  `sendeplan-vorschau.html`. Unterzeilen-Formate identisch: „Staffel 6, Episode 42 — „Titel"",
  „Folge 112", „S6 E42"; Fallback-Text wortgleich „— keine Beschreibung in den Metadaten hinterlegt —".
- **Admin**: `program-grid` statt Tabelle; alle bisherigen Tabellen-Infos erhalten (Start/Ende als
  `program-time`, Inhalt als Titel+Sub, `Regel:`-Zeile, Status-Chip Geplant/Verdrängt). Laufender
  Slot (`Status == Scheduled`, `Start <= now < End`) mit `slot-now`-Rahmen, LIVE-Badge,
  Fortschrittsbalken (clamped 0–100); Verdrängte bleiben durchgestrichen ohne Live-Badge.
  `tvsender-slot-row`/`data-slot-id` als Test-/Selektor-Anker erhalten, alte `tvsender-schedule-table`-Id
  nirgends mehr referenziert.
- **Live-TV**: `.livetv-layout` = Grid `minmax(0,1fr) 340px` (Prototyp `.livetv-frame`), responsive
  `@media (max-width: 900px) → 1fr`; `.livetv-side` mit kompakten Karten „Jetzt läuft"
  (now-card + LIVE-Badge + Restdauer + Fortschritt), „Gleich" und „Die nächsten Stunden".
  `.livetv-upcoming-row` bleibt als Test-Selektor bestehen.
- **API-Abwärtskompatibilität**: nur additive nullable DTO-Felder, keine neuen/geänderten
  Endpunkte — alte Clients ignorieren die Felder. Keine neue Tabelle/Spalte ⇒ keine Migration,
  kein Backup-Eintrag nötig (korrekt abwesend).
- **EF-Queries**: `LoadDetailsAsync` projiziert Season/Show per Join in einer Query — kein N+1;
  `Admin`-Pfad spiegelt dieselbe Logik bewusst direkt per EF (zeigt auch verdrängte Slots, die
  `api/program-guide` filtert) — im Kommentar begründet, Wortlaut über `ProgramGuideEntryText`
  gemeinsam.
- **DoD**: README.md, `docs/RELEASE_NOTES.md`, `docs/help/tv-sender.md` und die Tasks-Datei
  aktualisiert/konsistent; Commit-Historie sauber (plan → step → feat), keine Fremd-Dateien.

## Abweichungen

- [ ] **`handover.md` nicht gepflegt für Schritt 24.** Weder ein Eintrag in der
  Bearbeiter-Tabelle (AGENTS.md §2 verlangt Registrierung vor Beginn) noch ein
  Übergabestand-Abschnitt `### Schritt 24` existiert. `steps.md` steht korrekt auf „In Arbeit".
  Nachzutragen: Tabellenzeile + Übergabestand (erledigt/offen/Entscheidungen/Teststand).

## Hinweise

- **„Gleich"-Karte ohne `program-desc`**: Die Kompakt-Karte für `guide.Next` zeigt nur Zeit, Titel
  und Sub — Beschreibung haben nur Jetzt- und Vorschau-Karten. Der Abschnitt steht nicht im
  Prototyp (Bestandsschutz aus Schritt 7) und rendert konditional — kein totes Markup, aber die
  geringste Informationsdichte aller Karten. Bewusste Design-Entscheidung annehmbar.
- **„Die nächsten Stunden" enthält den laufenden Slot erneut**: `GetUpcomingAsync` filtert
  `End > now`, also erscheint `Now` als erste Vorschau-Zeile zusätzlich zur Jetzt-Karte —
  Altverhalten (bUnit-Test setzt es explizit so), kein neuer Fehler.
- **Kultur-Risiko `style="width:{double}%"`** in `LiveTv.razor` (`ProgressPercent` liefert
  `double`): unter Komma-Kulturen (de-DE) entsteht `width:34,5%` → ungültiges CSS. Bereits in
  Schritt 7 so eingeführt (`.livetv-progress-fill`), keine Regression dieses Schritts; Admin
  nutzt `int` und ist unbetroffen. Wäre einen separaten Fix (InvariantCulture/ToString("0.##"))
  wert.
- **`.livetv-main` ohne CSS-Regel**: rein struktureller Grid-Wrapper, ungestylt — harmlos.
- **Admin-`program-time` mit Datum** (`dd.MM. HH:mm` statt nur `HH:mm` im Prototyp): nötig, weil
  der Admin-Plan mehrtägig ist (Prototyp zeigt ein Tagesraster) — sachgerechte Anpassung.
- **`MediaPictureUrlResolver` escaped das Token nicht** (anders als `StreamUrl` mit
  `Uri.EscapeDataString`): geteilter Altbestand aller Thumbnail-Stellen, JWT-artige Tokens sind
  URL-sicher — konsistent, kein neues Risiko.
- **Aufräumen**: `artifacts/schritt24-bin` + `artifacts/schritt24-rel` (2,8 GB git-ignorierte
  Scratch-Build-Outputs des Implementierers) gelöscht — Platte war zu 98 % voll (2,3 GB frei).
  Diesmal lief der Normal-Build ohne Locks; kein `VideoWebPlayer.exe`-Prozess aktiv.
- Episoden ohne Nummer (`Number = 0`) zeigen „E0"/„Episode 0" — identisch zum bisherigen
  Titel-Format, kosmetisch.
