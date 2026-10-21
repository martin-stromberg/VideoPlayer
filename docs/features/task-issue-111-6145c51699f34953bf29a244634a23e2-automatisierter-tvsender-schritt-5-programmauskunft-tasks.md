# Tasks: Programmauskunft (Schritt 5)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | DTO | `DtoProgramGuideEntry` anlegen (`VideoWebPlayer.Client/Models`) | Erledigt | `ProgramGuideServiceTests.GetNowAsync_RunningSlot_ReturnsEntryWithPositionAndRemaining` |
| 2 | DTO | `DtoProgramGuideNow` anlegen (`VideoWebPlayer.Client/Models`) | Erledigt | `ProgramGuideE2ETests.PairedDevice_RequestNow_ReturnsCurrentAndNext` |
| 3 | Logik | `IProgramGuideService` Interface anlegen | Erledigt | `ProgramGuideServiceTests` (alle) |
| 4 | Logik | `ProgramGuideService` implementieren (`GetNowAsync`, `GetUpcomingAsync`, `MaxPreviewHours`, Titelauflösung, nur `Scheduled`) | Erledigt | `ProgramGuideServiceTests` (12 Tests) |
| 5 | API | `ProgramGuideController` anlegen (`api/program-guide`, `BearerTokenCheck`, `CheckLoggedIn`, `ExecuteAsync`-Muster) | Erledigt | `ProgramGuideControllerTests`, `ProgramGuideE2ETests` |
| 6 | DI | `IProgramGuideService` in `ServiceCollectionExtensions` registrieren | Erledigt | `ProgramGuideE2ETests` (löst den Service über echtes DI auf) |
| 7 | Tests | `ProgramScheduleTestDb.CreateSlotAsync` Hilfsmethode | Erledigt | `ProgramGuideServiceTests` (alle) |
| 8 | Tests | `ProgramGuideServiceTests` (Now: Position/Rest, Displaced, Lücke, Grenze `End`, Episodentitel, unbekannter Titel, kein Sender; Upcoming: Fenster, Sortierung, Displaced, `preview`-Validierung) | Erledigt | 12 Tests, gezielter Lauf grün |
| 9 | Tests | `ProgramGuideControllerTests` (anonym→401, `hours`→400, Ok→DTO) | Erledigt | 8 Tests, gezielter Lauf grün |
| 10 | E2E-Tests | `ProgramGuideE2ETests` (401 anonym, 200 Header-Token, `access_token`-Query, begrenzte Vorschau + 400) | Erledigt | 6 Tests, gezielter Lauf grün |
| 11 | Verifikation | `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei | Erledigt | Build-Protokolle: 0 Fehler beide Konfigurationen |
| 12 | Verifikation | Volle Testsuite `dotnet test VideoWebPlayer.Tests` grün | Erledigt | Endlauf: 1892/1893 bestanden, 0 Fehler, 1 Alt-Skip |
