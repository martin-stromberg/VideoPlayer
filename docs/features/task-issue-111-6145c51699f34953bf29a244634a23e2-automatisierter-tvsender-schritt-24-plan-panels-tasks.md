# Tasks: Sendeplan-Einträge als Panels (Bild + Titel + Beschreibung)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `ProgramGuideEntryText` anlegen (`SeasonNumberFromName`, `Subtitle`, `ShortSubtitle`, `FirstGenre`, `NoDescriptionText`) | Erledigt | `ProgramGuideServiceTests.SeasonNumberFromName_ReadsDigitsFromSeasonName` |
| 2 | API/DTO | `DtoProgramGuideEntry` um `PosterPictureId`, `Description`, `GenreName`, `ShowName`, `SeasonName`, `SeasonNumber`, `EpisodeNumber`, `EpisodeName` erweitern | Erledigt | `VideoWebPlayerClientTests_ProgramGuide` (Feld-Assertions), `ProgramGuideServiceTests` |
| 3 | Logik | `ProgramGuideService`: `LoadTitlesAsync` → `LoadDetailsAsync`, `ToEntry` befüllt neue Felder inkl. Fallback-Ketten | Erledigt | `GetNowAsync_MovieSlot_ReturnsCardMetadata`, `GetNowAsync_EpisodeSlot_ReturnsCardMetadataAndSeasonPosterFallback`, `GetNowAsync_EpisodeWithoutPlot_FallsBackToShowPlot`, `GetNowAsync_BareItemSlot_ReturnsNoCardMetadata` |
| 4 | Tests | `ProgramGuideServiceTests`: neue Feld-Assertions | Erledigt | siehe #3 |
| 5 | UI/CSS | `.program-card`-Design in `app.css` (Voll-/Kompaktvariante, Admin-Overrides, Verdrängt-Stil) | Erledigt | `TVSenderAdminTests.Sendeplan_RendersProgramCards_WithBadgesPosterAndProgress`, `LiveTvTests.Upcoming_RendersCompactProgramCards_WithPosterBadgeAndDescription` |
| 6 | UI/Admin | `TVSenderAdmin.razor` Sendeplan-Tab: Tabelle → `program-grid`-Karten, `ScheduleCardContent`, `ReloadScheduleAsync`-Erweiterung | Erledigt | `TVSenderAdminTests.Sendeplan_RendersProgramCards_WithBadgesPosterAndProgress`, `TVSenderAdminE2ETests` |
| 7 | UI/LiveTV | `LiveTv.razor`: `.livetv-layout`-Grid + kompakte Karten (Jetzt/Gleich/nächste Stunden) | Erledigt | `LiveTvTests.Upcoming_RendersCompactProgramCards_WithPosterBadgeAndDescription`, `LiveTvE2ETests` |
| 8 | UI/LiveTV | `LiveTv.razor.css`: Layout-/Spalten-Stile | Erledigt | `LiveTvTests` (Markup), E2E-Sichtprüfung |
| 9 | Tests | `TVSenderAdminTests`: Karten-Markup, Badges, Fortschritt, Verdrängt | Erledigt | `Sendeplan_RendersProgramCards_WithBadgesPosterAndProgress` |
| 10 | Tests | `LiveTvTests`: kompakte Karten, Poster-`access_token`, Platzhalter | Erledigt | `Upcoming_RendersCompactProgramCards_WithPosterBadgeAndDescription` |
| 11 | Tests | `VideoWebPlayerClientTests_ProgramGuide`: neue DTO-Felder über HTTP | Erledigt | `PairedDevice_RequestProgramGuideNow_ReturnsCurrentAndNext` (erweitert) |
| 12 | E2E | `LiveTvE2ETests`/`TVSenderAdminE2ETests`: `.program-card`-Assertions + Seeds mit Bild/Plot | Erledigt | `LiveTv_ViaNavMenu_ShowsPlayerAndProgram`, `TVSenderAdminE2ETests` Sendeplan-Teil |
