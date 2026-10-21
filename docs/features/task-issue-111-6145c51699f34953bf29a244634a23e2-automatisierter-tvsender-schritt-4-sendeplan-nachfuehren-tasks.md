# Tasks: Sendeplan bei Änderungen nachführen (Schritt 4)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ProgramSlotStatus`-Enum anlegen (`Scheduled=0`, `Displaced=1`) | Offen | — |
| 2 | Datenmodell | `ProgramSlot.Status` + `ProgramSlotConfiguration.IsRequired()` + Migration `AddProgramSlotStatus` | Offen | — |
| 3 | Backup | `OptionalRestoreColumns` um `ProgramSlots.Status` ergänzen | Offen | — |
| 4 | Signal | `IProgramScheduleSignal` + `ProgramScheduleSignal` (Notify/BeginScan/WaitAsync) | Offen | — |
| 5 | DI | Signal-Registrierung in `ServiceCollectionExtensions` | Offen | — |
| 6 | Logik | `ProgramScheduleReconcileSummary` in `IProgramScheduleService` | Offen | — |
| 7 | Logik | `ReconcileAsync` (Sweep, Suffix-Verdrängung, Cursor-Rewind, Eviction, Materialisierung, FillGaps) | Offen | — |
| 8 | Logik | `Status == Scheduled`-Filter in `EnsureCoverageAsync` (`lastEnd`, `blocked`) | Offen | — |
| 9 | Logik | `GetScheduleAsync` für Admin-Ansicht | Offen | — |
| 10 | Trigger | `ProgramRuleService` optionaler Signal-Parameter + Notify in CRUD | Offen | — |
| 11 | Trigger | `ApplicationDbContext` optionaler Signal-Parameter + Notify (Marker-Hook, `DeleteMediaSourceAsync`) | Offen | — |
| 12 | Trigger | `MediaSourceScanService` `BeginScan`-Scope | Offen | — |
| 13 | Worker | `ProgramScheduleWorker`: Signal/Timer-`WhenAny`, Reconcile+Ensure pro Lauf | Offen | — |
| 14 | UI | Sendeplan-Karte in `TVSenderAdmin.razor` (Titel/Regel/Status-Chip) | Offen | — |
| 15 | Tests | `ProgramScheduleServiceTests_Reconcile` (alle Reconcile-Szenarien) | Offen | — |
| 16 | Tests | `ProgramScheduleSignalTests` | Offen | — |
| 17 | Tests | Signal-Auslösung in `ProgramRuleServiceTests` + DbContext-Hook-Test | Offen | — |
| 18 | Tests | `ProgramScheduleWorkerTests` Signal-Weckung | Offen | — |
| 19 | Tests | Alt-Backup-Regressionstest `ProgramSlots.Status` in `VideoWebPlayerBackupDataTests` | Offen | — |
| 20 | E2E | `TVSenderAdminE2ETests` Sendeplan-Karte mit „Verdrängt"-Chip | Offen | — |
