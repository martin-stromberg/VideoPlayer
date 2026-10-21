# Plan-Review (Schritt 7)

## Ergebnis

**Status:** Erfüllt (mit Hinweisen)

## Gegenprüfung Plan vs. Implementierung

| Planpunkt | Umgesetzt? | Nachweis |
|-----------|-----------|----------|
| `ProgramSlot` mit weichen Referenzen + `RuleType`-Snapshot | Ja | `VideoWebPlayer/Data/ProgramSlot.cs`, `Configurations/ProgramSlotConfiguration.cs` |
| `ProgramRuleCursor` (FK+Kaskade, unique) + `ProgramCursorContentType` | Ja | `ProgramRuleCursor.cs`, `ProgramCursorContentType.cs`, `ProgramRuleCursorConfiguration.cs` |
| Setup-Spalten + `ProgramSettingsService`-Methoden mit Min-Validierung | Ja | `Setup.cs` (ProgramScheduleHorizonHours/IntervalMinutes), `ProgramSettingsService` (~Z. 241–279) |
| Migration `AddProgramSchedule` | Ja | `VideoWebPlayer/Migrations/2026…_AddProgramSchedule.cs` |
| Backup-Registrierung (Tabellen, Spalten, Int-Defaults) | Ja | `VideoWebPlayerBackupData.cs` Z. 41–42, 83–84, 121–122 |
| `GermanHolidayCalendar` (Computus + Landesfeiertage) | Ja | `Services/GermanHolidayCalendar.cs`; 3 Tests grün |
| `IProgramScheduleService` + `ProgramScheduleRunSummary` + `ProgramScheduleService` | Ja | `Services/IProgramScheduleService.cs`, `Services/ProgramScheduleService.cs` |
| Expand-only, `coverageStart = max(now, lastEnd)` | Ja | `EnsureCoverageAsync` Z. 59–66 |
| Expansion je Materialart inkl. verwaister Materialien | Ja | `ExpandShow/Movie/MovieCollection/Genre/Playlist` |
| Freigabe-Filter „alle Nicht-Admins" (Quelle ODER Freischaltung) | Ja | `LoadAccessContextAsync` + `AccessContext.Allows` |
| Laufzeit-Filter (`Duration != null`) | Ja | `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` filtern; Zähler `SkippedNoDuration` |
| Prioritäts-Konfliktauflösung, komplette Ausstrahlung entfällt | Ja | `MaterializeOccurrence`; Cursor-Rollback via Peek/Commit |
| Füller-Round-Robin, fit-entirely, Overshoot nur am Horizont-Ende | Ja | `FillGaps` (inkl. `overshootAllowed` am Fensterrand) |
| `ProgramScheduleWorker` (Scope-Factory, Gate, TimeProvider, Intervall je Iteration) | Ja | `Services/ProgramScheduleWorker.cs`, DI in `ServiceCollectionExtensions` Z. 258, 309 |
| Admin-UI-Felder Horizont/Intervall | Ja | `TVSenderAdmin.razor` Z. 53–60, 320–321, 345 |
| Tests (Unit/Integration, SQLite echt, Alt-Backup) | Ja | `ProgramSchedule*Tests_*.cs`, `GermanHolidayCalendarTests`, Backup-Regressionstest |
| E2E (konsolidiert erweitert) | Ja | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` erweitert |

## Festgestellte Abweichungen vom Plan (behoben oder akzeptiert)

- **Lookback-Ausstrahlungen konsumieren Cursor-Fortschritt:** Eine Ausstrahlung, die komplett vor `coverageStart` liegt (z. B. gestern 20:00 beim Erstlauf), konsumiert ihre Inhalte (Serie fährt mit der nächsten Folge fort). Plan-Text präzisiert das nicht explizit; Verhalten ist konsistent („die Ausstrahlung ist gelaufen") und in den Tests dokumentiert.
- **`SlotsCreated` zählt nur persistierte Sendeplätze** (nicht die vor dem Coverage-Start fallengelassenen) — nach Testbefund korrigiert.
- **Overshoot am Horizont-Ende:** Der letzte Füller darf über `horizonEnd` hinauslaufen, damit die Abdeckung wirklich bis zum Horizont reicht (Restlücken-Fall). Plan nannte „fit-entirely"; Erweiterung ist minimal und zweckmäßig.
- **`SlotDuration`/`EpisodeCount` beide leer:** Robustheits-Fallback „1 Inhalt" ergänzt (Validation verlangt regulär eines davon; die direkte Test-Fixture umgeht das).

## Hinweise

- Der Freigabe-Filter ist bewusst konservativ (jeder Nicht-Admin-Benutzer muss den Inhalt sehen können). Playlist-Materialien filtern Einträge, keine Container-Freigaben nötig, weil nur abspielbare Einträge expandiert werden.
- Extend-only ist bewusst nicht rückwirkend: Regel-/Medienänderungen wirken erst ab dem freien Planungsende (Reaktion auf Änderungen = Schritt 4).
