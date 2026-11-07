# Tests: Bestandsaufnahme und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-07 ~16:20–16:45 +01:00
- Branch und Commit-ID: `task/d6abca7c7d384715999522f7e835ca22-testhaertung-sendeplan` @ `8e77eb5`
- Uncommittete Änderungen im getesteten Stand: nur Lifecycle-Artefakte unter
  `docs/features/task/d6abca7c7d384715999522f7e835ca22-testhaertung-sendeplan/` (kein Code)
- Testumgebung: Windows, .NET SDK 10.0.403, Testziel `net10.0`, xUnit v3, Playwright 1.62
- Testsuiten und Quellen der Testbefehle: `VideoWebPlayer.Tests` (einzige Suite);
  Filter-Kategorien `E2E`/`Integration` aus `.github/workflows/pr-staging-ci.yml`
  (CI testet in Release je Kategorie separat). Hinweis: `TVSenderAdminE2ETests` trägt
  offenbar kein `Category=E2E`-Trait und lief daher auch im `Category!=E2E`-Filter.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Basis-Build | `dotnet build VideoPlayer.sln -c Debug` | Repo-Root | 0 (139 Warnungen, 0 Fehler) | – | – | – | [build-log](test-results/baseline-build.log) |
| Basis-Tests | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Debug --filter "Category!=E2E"` | Repo-Root | 1 | 2668 | 1 | 1 | [test-log](test-results/baseline-test-none2e.log) |
| Retry Einzeltest | `dotnet test ... --filter "FullyQualifiedName~TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule"` | Repo-Root | 0 | 1 | 0 | 0 | Konsole (unten zitiert) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs:324` | Playwright strict-mode violation: Locator `.tvsender-slot-row.tvsender-slot-displaced` fand 2 Elemente (erwartet 1) — timingabhängig | Basis-Testlauf; **im isolierten Retry grün (18,9 s)** → als bekanntes Playwright-Flackern eingestuft (vgl. AGENTS.md: gelegentliche Flakes unter Last) |

### Übersprungene Tests

- `Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` —
  begründet übersprungen: „Unix-Dateirechte lassen sich nur unter Unix prüfen." (Windows-Host)

### Testlücken und Ausführungsprobleme

- Der Basis-Lauf nutzte `--filter "Category!=E2E"`; darunter liefen dennoch Playwright-
  E2E-Tests der Klassen `TVSenderAdminE2ETests*` (kein E2E-Trait). Ein `Category=E2E`-
  Komplett-Lauf erfolgt im Schritt „Tests ausführen"; die Playwright-Suite braucht
  gebaute Anwendung + Browser (vorhanden — Tests liefen).
- Der Auftrag verzichtet dokumentiert auf die reale End-to-End-Abnahme gegen die
  Installation `http://192.168.10.230:83/` (Anwender, 2026-11-09, via Auftragstext).

## Betroffene Testklassen (Befund-Mapping: siehe [findings.md](findings.md))

### `ProgramScheduleServiceTests_*` (Reconcile, RingOrder, CollectionOrder, Interleaving, Trailer, ContentScope, Filler, Generation, ClipGroups, ClipSource, StrandRotation, AgeWindow, PrimetimeGrace)

- Einzelne Testmethoden je Befund B2–B13, B15 — konkrete Methoden- und Zeilenangaben in
  [findings.md](findings.md).

### `ProgramScheduleWorkerTests`

- `Worker_RunsEnsureCoverage_OnStartup` — Worker erzeugt Slots beim Start.
- `Worker_IdlesWithoutSchedule_WhenFeatureNotReleased` — Feature-Flag aus → keine Slots.
- `Worker_Run_PurgesExpiredRules` / `Worker_DisabledFeature_SkipsPurge` — Ablauf-Bereinigung.
- `Worker_Run_CleansSelfDisplacedDuplicates` — E2E-artig: ein Worker-Takt bereinigt ein
  bewiesenes Zwillingpaar.

## Hilfsmethoden

### `ProgramScheduleTestDb` — siehe [testhelper.md](testhelper.md)

### `PrimetimeSeed` (`VideoWebPlayer.Tests/Helpers/PrimetimeSeed.cs`)

- `SetHorizonDaysAsync`, `LocalUtc`, `LocalMinutesOfDay`, `AiringOf`, `Start`, `Grace`,
  `HorizonDays`, `AddClipGroupAsync`, `CreateRuleAsync`, `AssertStartsInsidePeriod` —
  Seeds für Primetime-/Strang-Rotation-Szenarien.
