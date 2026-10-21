# Tasks: Fortschrittsanzeige Laufzeitermittlung (N6)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell/Service | Records `MediaDurationProbeProgress` + `MediaDurationProbeRecentItem` in `Services/MediaDurationProbeProgress.cs` anlegen | Offen | — |
| 2 | Logik | `MediaDurationProbeService.GetProgressAsync(userId, recentCount, ct)` mit Admin-Gate, Zählern, `LastActivityAt`, `ProbedLastHour`, Rolling-Liste | Offen | — |
| 3 | Logik | `RequireAdminAsync` in `MediaDurationProbeService` (Muster aus `ProgramRuleService`) | Offen | — |
| 4 | Tests | `MediaDurationProbeServiceTests_Progress`: Zähler, Reihenfolge/Limit, Aktivität/Rate, Leerstand, Admin-Erzwingung | Offen | — |
| 5 | UI | `TVSenderAdmin.razor`: Karte „Laufzeitermittlung" (Status, Zähler, Balken, letzte Aktivität, Rate, Rolling-Tabelle) | Offen | — |
| 6 | UI | `TVSenderAdmin.razor`: `PeriodicTimer`-Refresh (15 s) + Abbruch in `Dispose` | Offen | — |
| 7 | E2E-Tests | `Admin_CreateEditDeactivateDeleteRule` um Karten-/Listen-Nachweis erweitern | Offen | — |
| 8 | Dokumentation | `docs/help/tv-sender.md` + `docs/help/videolaufzeit.md` um die Statusanzeige ergänzen | Offen | — |
| 9 | Dokumentation | `docs/RELEASE_NOTES.md` (EN+DE) ergänzen | Offen | — |
