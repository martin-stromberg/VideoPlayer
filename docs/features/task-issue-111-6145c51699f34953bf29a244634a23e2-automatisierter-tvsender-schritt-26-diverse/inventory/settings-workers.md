# Settings & Worker

## Settings-Persistenz

- `Setup` (`VideoWebPlayer/Data/Setup.cs`): Single-Row-Entität; bisherige Sender-Felder
  `HolidayFederalState`, `ProgramScheduleHorizonHours`, `ProgramScheduleIntervalMinutes`,
  `LiveStreamSegmentLengthSeconds`, `LiveStreamSegmentDirectory`, `LiveStreamSegmentRetentionMinutes`.
  **Kein** `TvSenderEnabled`.
- `ProgramSettingsService` (`VideoWebPlayer/Services/ProgramSettingsService.cs`):
  `GetOrCreateSetupAsync` legt Zeile lazy an; Getter/Updater je Einstellung;
  `UpdateGeneralSettingsAsync(GeneralSettingsUpdate)` schreibt atomar (Record mit 6 Parametern —
  Aufrufer: `ProgramSettings.razor` + 5 Teststellen).
- Backup: `VideoWebPlayerBackupData` — neue Spalte gehört in `OptionalRestoreColumns` +
  `OptionalRestoreBoolDefaults` (Default `false`).
- Migration: EF Core Migrations in `VideoWebPlayer/Migrations/` (dotnet-ef 10.0.10 vorhanden);
  Start wendet `db.Database.Migrate()` an; Tests nutzen `EnsureCreated`.

## Worker

| Worker | Verhalten heute | Eingriffspunkt |
|--------|-----------------|----------------|
| `ProgramScheduleWorker` | Scope → `GetProgramScheduleIntervalAsync` → Gate → `ReconcileAsync` + `EnsureCoverageAsync` → Sleep(Interval/Signal) | Flag-Check direkt nach Settings-Abruf, `interval` vorher gelesen → Sleep bleibt |
| `LiveStreamWorker` | Scope → Gate → `LiveStreamCoordinator.SynchronizeAsync` → Sleep(WakeAt/PollCap/Signal) | Flag-Check → `ILiveStreamSegmenter.StopAsync()` (Singleton, idempotent) + Skip |
| `MediaDurationProbeWorker` | Scope → `MediaDurationProbeService.ProbePendingAsync` Schleife → Sleep | Flag-Check → Skip; `ProgramSettingsService` muss in den Scope |

`MediaItem.Duration` wird ausschließlich vom Sender genutzt (Schedule + TVSenderAdmin-Diagnose) —
Probe-Pause bei deaktiviertem Sender ist fachlich korrekt.
