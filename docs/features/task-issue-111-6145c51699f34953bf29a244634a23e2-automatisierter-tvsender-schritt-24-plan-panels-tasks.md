# Tasks: Sendeplan-Einträge als Panels (Bild + Titel + Beschreibung)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `ProgramGuideEntryText` anlegen (`SeasonNumberFromName`, `Subtitle`, `ShortSubtitle`, `FirstGenre`, `NoDescriptionText`) | Offen | — |
| 2 | API/DTO | `DtoProgramGuideEntry` um `PosterPictureId`, `Description`, `GenreName`, `ShowName`, `SeasonName`, `SeasonNumber`, `EpisodeNumber`, `EpisodeName` erweitern | Offen | — |
| 3 | Logik | `ProgramGuideService`: `LoadTitlesAsync` → `LoadDetailsAsync`, `ToEntry` befüllt neue Felder inkl. Fallback-Ketten | Offen | — |
| 4 | Tests | `ProgramGuideServiceTests`: neue Feld-Assertions (Movie/Episode/Bare/Show-Fallback/SeasonNumber) | Offen | — |
| 5 | UI/CSS | `.program-card`-Design in `app.css` (Voll-/Kompaktvariante, Admin-Overrides, Verdrängt-Stil) | Offen | — |
| 6 | UI/Admin | `TVSenderAdmin.razor` Sendeplan-Tab: Tabelle → `program-grid`-Karten, `ScheduleCardContent`-Record, `ReloadScheduleAsync`-Erweiterung | Offen | — |
| 7 | UI/LiveTV | `LiveTv.razor`: `.livetv-layout`-Grid + kompakte Karten (Jetzt/Gleich/nächste Stunden) | Offen | — |
| 8 | UI/LiveTV | `LiveTv.razor.css`: Layout-/Spalten-Stile | Offen | — |
| 9 | Tests | `TVSenderAdminTests`: Karten-Markup, Badges, Fortschritt, Verdrängt | Offen | — |
| 10 | Tests | `LiveTvTests`: kompakte Karten, Poster-`access_token`, Platzhalter | Offen | — |
| 11 | Tests | `VideoWebPlayerClientTests_ProgramGuide`: neue DTO-Felder über HTTP | Offen | — |
| 12 | E2E | `LiveTvE2ETests`/`TVSenderAdminE2ETests`: `.program-card`-Assertions + Seeds mit Bild/Plot | Offen | — |
