# Tasks: Fortschrittsanzeige Laufzeitermittlung (N6)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell/Service | Records `MediaDurationProbeProgress` + `MediaDurationProbeRecentItem` in `Services/MediaDurationProbeProgress.cs` anlegen | Erledigt | `MediaDurationProbeServiceTests_Progress.GetProgressAsync_CountsStatesSeparately` u. a. (Record wird überall verwendet) |
| 2 | Logik | `MediaDurationProbeService.GetProgressAsync(userId, recentCount, ct)` mit Admin-Gate, Zählern, `LastActivityAt`, `ProbedLastHour`, Rolling-Liste | Erledigt | `GetProgressAsync_CountsStatesSeparately`, `_ReportsLastActivityAndHourlyRate`, `_RecentItems_OrderedNewestFirstAndLimited`, `_EmptyLibrary_ReturnsZeros` |
| 3 | Logik | `RequireAdminAsync` in `MediaDurationProbeService` (Muster aus `ProgramRuleService`) | Erledigt | `GetProgressAsync_NonAdmin_Throws` |
| 4 | Tests | `MediaDurationProbeServiceTests_Progress`: Zähler, Reihenfolge/Limit, Aktivität/Rate, Leerstand, Admin-Erzwingung | Erledigt | Alle 5 Tests grün (Release-Lauf) |
| 5 | UI | `TVSenderAdmin.razor`: Karte „Laufzeitermittlung" (Status, Zähler, Balken, letzte Aktivität, Rate, Rolling-Tabelle) | Erledigt | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` |
| 6 | UI | `TVSenderAdmin.razor`: `PeriodicTimer`-Refresh (15 s) + Abbruch in `Dispose` | Erledigt | Kein direkter Test (Timer-Verhalten wäre E2E-instabil); indirekt über E2E-Seitenaufbau |
| 7 | E2E-Tests | `Admin_CreateEditDeactivateDeleteRule` um Karten-/Listen-Nachweis erweitern | Erledigt | Test grün (Release-Lauf) |
| 8 | Dokumentation | `docs/help/tv-sender.md` + `docs/help/videolaufzeit.md` um die Statusanzeige ergänzen | Erledigt | Kein direkter Test |
| 9 | Dokumentation | `docs/RELEASE_NOTES.md` (EN+DE) ergänzen | Erledigt | Kein direkter Test |
