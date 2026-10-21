# Tasks: Programmauskunft (Schritt 5)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | DTO | `DtoProgramGuideEntry` anlegen (`VideoWebPlayer.Client/Models`) | Offen | — |
| 2 | DTO | `DtoProgramGuideNow` anlegen (`VideoWebPlayer.Client/Models`) | Offen | — |
| 3 | Logik | `IProgramGuideService` Interface anlegen | Offen | — |
| 4 | Logik | `ProgramGuideService` implementieren (`GetNowAsync`, `GetUpcomingAsync`, `MaxPreviewHours`, Titelauflösung, nur `Scheduled`) | Offen | — |
| 5 | API | `ProgramGuideController` anlegen (`api/program-guide`, `BearerTokenCheck`, `CheckLogedIn`, `ExecuteAsync`-Muster) | Offen | — |
| 6 | DI | `IProgramGuideService` in `ServiceCollectionExtensions` registrieren | Offen | — |
| 7 | Tests | `ProgramScheduleTestDb.CreateSlotAsync` Hilfsmethode | Offen | — |
| 8 | Tests | `ProgramGuideServiceTests` (Now: Position/Rest, Displaced, Lücke, Grenze `End`, Episodentitel, unbekannter Titel, kein Sender; Upcoming: Fenster, Sortierung, Displaced, `preview`-Validierung) | Offen | — |
| 9 | Tests | `ProgramGuideControllerTests` (anonym→401, `hours`→400, Ok→DTO) | Offen | — |
| 10 | E2E-Tests | `ProgramGuideE2ETests` (401 anonym, 200 Header-Token, `access_token`-Query, begrenzte Vorschau + 400) | Offen | — |
| 11 | Verifikation | `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei | Offen | — |
| 12 | Verifikation | Volle Testsuite `dotnet test VideoWebPlayer.Tests` grün | Offen | — |
