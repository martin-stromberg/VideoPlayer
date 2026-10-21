# Tasks: Genre-Material — Mehrfachauswahl, Quell-Filter, Status/Alternativnamen (N1+N2+N3)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ProgramRuleGenre` Entität anlegen (ProgramRuleId, GenreId) | Offen | — |
| 2 | Datenmodell | `ProgramRuleMediaSource` Entität anlegen (ProgramRuleId, MediaSourceId) | Offen | — |
| 3 | Datenmodell | EF-Konfigurationen für beide Join-Tabellen (Composite-PK, FKs, Kaskade) | Offen | — |
| 4 | Datenmodell | `ProgramRule` um `Genres`/`MediaSourceFilters`-Navigationen erweitern | Offen | — |
| 5 | Datenmodell | `ApplicationDbContext` um `ProgramRuleGenres`/`ProgramRuleMediaSources` erweitern | Offen | — |
| 6 | Migration | `AddProgramRuleGenreAndSourceLinks` inkl. Backfill-SQL aus `MaterialId` | Offen | — |
| 7 | Logik | Kanonische Genre-Auflösung (Alternativnamen rekursiv/quellbezogen, IsHidden, saisonales Fenster, Zyklenschutz) | Offen | — |
| 8 | Logik | `ProgramRuleInput` um `GenreIds`/`MediaSourceIds` erweitern | Offen | — |
| 9 | Logik | `ProgramRuleService`: Validierung Genre-Mehrfachauswahl + Quell-Filter | Offen | — |
| 10 | Logik | `ProgramRuleService`: CRUD schreibt/ersetzt Join-Zeilen, `GetRulesAsync` inkludiert Joins | Offen | — |
| 11 | Logik | `GetGenreMaterialOptionsAsync` (Admin-geschützt, quellabhängige kanonische Optionen) | Offen | — |
| 12 | Logik | `ProgramScheduleService.ExpandGenreAsync`: Union, Auflösung, Quell-Filter, Dedupe | Offen | — |
| 13 | UI | `TVSenderAdmin.razor`: Genre-Mehrfachauswahl (Checkboxen + Chips) | Offen | — |
| 14 | UI | `TVSenderAdmin.razor`: Quell-Mehrfachauswahl bei Genre-Material, Optionen-Neuladen | Offen | — |
| 15 | UI | `TVSenderAdmin.razor`: `StartEdit`/`MaterialLabel` für Mehrfach-Genres + Quellen | Offen | — |
| 16 | Backup | `OptionalRestoreTables` um `ProgramRuleGenres`/`ProgramRuleMediaSources` ergänzen | Offen | — |
| 17 | Tests | `ProgramScheduleTestDb`-Hilfsmethode `CreateGenreAsync` (+ Join-Seeding in `CreateRuleAsync`) | Offen | — |
| 18 | Tests | Expansion-Tests: Union-Dedupe, Quell-Filter, Alternativnamen-Rollup, Hidden, Fenster | Offen | — |
| 19 | Tests | Options-Tests: Hidden+Alt → nur Ziel; Hidden ohne Alt → weg; Quell-Differenzierung | Offen | — |
| 20 | Tests | Validierungs-/CRUD-/Kaskade-Tests (echtes SQLite) | Offen | — |
| 21 | Tests | Alt-Backup-Regressionstest für beide neue Tabellen | Offen | — |
| 22 | E2E-Tests | `TVSenderAdminE2ETests`: Genre-Mehrfachauswahl + Quell-Filter im Regel-Lebenszyklus | Offen | — |
| 23 | Doku | `docs/help/tv-sender.md` aktualisieren | Offen | — |
| 24 | Doku | `docs/RELEASE_NOTES.md` (+ README falls relevant) aktualisieren | Offen | — |
