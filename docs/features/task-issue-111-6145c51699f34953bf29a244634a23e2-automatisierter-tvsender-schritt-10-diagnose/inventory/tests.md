# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-21, lokale Zeit (Mitteleuropa)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-10-diagnose` @ `e08a23c`
- Uncommittete Änderungen im getesteten Stand: keine (`git status` sauber; nur neue
  `docs/features/…`-Artefakte, nicht committed zum Laufzeitpunkt)
- Testumgebung: Windows, .NET SDK 10.0.402, `dotnet test` Debug
- Ermittelte Testsuiten: `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (xUnit + bUnit +
  Playwright-E2E); Quelle: AGENTS.md / bisherige Schritt-Branches

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline (betroffene Suiten) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Debug --filter "FullyQualifiedName~ProgramSchedule|FullyQualifiedName~ProgramRule|FullyQualifiedName~ProgramGuide|FullyQualifiedName~TVSenderAdmin"` | Repo-Root | 0 | 113 | 0 | 0 | Konsolenausgabe „Bestanden!: Fehler: 0, erfolgreich: 113, übersprungen: 0, gesamt: 113" |

### Nachgewiesene bestehende Testfehler

Keine — der gefilterte Baseline-Lauf ist vollständig grün. Der projektweite Volllauf
(Erwartung laut Projektstand: ~1998 grün + 1 Alt-Skip) steht für den Abschluss der Umsetzung
aus und wird in `test-results.md` dokumentiert; er wurde für die Bestandsaufnahme nicht
ausgeführt (Laufzeit der Playwright-E2E-Suite).

### Testlücken und Ausführungsprobleme

- Der gefilterte Lauf deckt die betroffenen Suiten ab; nicht ausgeführte übrige Suiten sind
  für den Ausgangszustand unbekannt (kein „preexisting"-Nachweis ohne Volllauf — Abschlusslauf
  ist eingeplant).
- Playwright-E2E läuft nur mit installiertem Browser (`SkipBrowser`-Flag in
  `PlaylistsE2ETestBase`); `TVSenderAdminE2ETests` lief in der Baseline (3 Tests erfolgreich,
  Browser vorhanden).

## Testklassen

### `ProgramScheduleServiceTests_Generation` / `_Conflicts` / `_Filler` / `_Reconcile` / `_Access` / `_Genres`
- Decken `EnsureCoverageAsync`/`ReconcileAsync` über `ProgramScheduleTestDb` ab: Lückenlosigkeit,
  Prioritäten, Cursor-Semantik, Freigabe-Filter, Genre-Union/Quell-Filter/Alternativnamen.

### `ProgramRuleServiceTests_Crud` / `_Validation` / `_GenreMaterial`
- Regel-CRUD, Validierung, Genre-Mehrfachauswahl + Quell-Filter, Admin-Erzwingung.

### `ProgramScheduleSignalTests`, `ProgramScheduleWorkerTests`
- Signal-Koaleszierung/BeginScan; Worker-Aufwachen bei Signal.

### `TVSenderAdminE2ETests` (Playwright)
- `Admin_CreateEditDeactivateDeleteRule` — konsolidierter Lebenszyklus inkl. Sendeplan-Chips.
- `Admin_GenreRule_MultiSelectAndSourceFilter` — Genre-Mehrfachauswahl + Quell-Filter.
- `NonAdmin_SeesNotAuthorized` — Admin-Gate.

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync(dbName, ct)` — Shared-In-Memory-SQLite mit FKs.
- `CreateService()` — `ProgramScheduleService` mit `ProgramRuleService`, `ProgramSettingsService`,
  `NullLogger`; **muss für `IOptions<MediaProbeOptions>` erweitert werden** (neuer
  Konstruktor-Parameter, optional mit Default).
- `CreateMediaItemAsync(sourceId, duration)`, `CreateMovieAsync`, `CreateShowAsync`,
  `CreateGenreAsync`, `LinkMovieGenreAsync`, `LinkShowGenreAsync`, `CreateRuleAsync`,
  `GrantSourceAccessAsync`, `AddUserAsync`, `GetChannelAsync`, `CreateSlotAsync`, `SlotsAsync`.
