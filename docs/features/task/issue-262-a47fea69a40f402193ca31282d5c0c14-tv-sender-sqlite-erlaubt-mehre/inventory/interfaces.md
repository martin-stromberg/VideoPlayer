# Bestandsaufnahme: Interfaces

## `IProgramScheduleService`

Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs`

Implementiert von `ProgramScheduleService`. Alle Methoden lesen/schreiben `ProgramRuleCursors`
nur indirekt über die Service-Implementierung; der Contract selbst nennt keine Cursor-Details.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnsureCoverageAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleRunSummary>` | Erweitert den Sendeplan bis zum Horizont; lädt/persistiert Cursor (Implementierung Z. 88–92, 122) |
| `ReconcileAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Bewertet den Plan neu; Cursor-Rewind für verdrängte Ausstrahlungen (Implementierung Z. 156–160, 262/295/335, 344) |
| `GetScheduleAsync` | `long channelId`, `DateTimeOffset fromUtc`, `int maxCount`, `CancellationToken` | `Task<List<ProgramSlot>>` | Liefert kommende Sendeplätze; kein Cursor-Zugriff |
| `DeleteDisplacedSlotsAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<int>` | Löscht nur `Displaced`-Slots; kein Cursor-Zugriff |
| `ResetScheduleAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<ProgramScheduleResetResult>` | Löscht Slots **und** alle Cursor der Kanal-Regeln (Implementierung Z. 425–427) |
| `GetRuleDiagnosesAsync` | `string userId`, `long channelId`, `CancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Planungsdiagnose; kein Cursor-Zugriff |

Zugehörige Resultat-Typen (in derselben Datei): `ProgramScheduleRunSummary`,
`ProgramScheduleReconcileSummary`, `ProgramRuleDiagnosis`, `ProgramScheduleResetResult`
(letzteres zählt `CursorsDeleted`).

## `IBackupData`

Nicht im Repo definiert — stammt aus dem NuGet-Paket `msTools.Backup`. `VideoWebPlayerBackupData`
implementiert `WriteToAsync(Stream, CancellationToken)` und `ReadFromAsync(Stream, CancellationToken)`.
