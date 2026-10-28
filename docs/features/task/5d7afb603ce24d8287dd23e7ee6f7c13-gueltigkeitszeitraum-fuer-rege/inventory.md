# Bestandsaufnahme: Gültigkeitszeitraum für Programmregeln

Analysiert wurde der Bestandscode rund um `ProgramRule` und den Sendeplan des automatischen TV-Senders: Datenmodell, Regelverwaltung (`ProgramRuleService`), Planerzeugung und Neu-Bewertung (`ProgramScheduleService`, `ProgramScheduleWorker`), die bestehende saisonale Fenster-Mechanik, die Admin-UI sowie die zugehörigen Tests — bezogen auf die Anforderung, Regeln einen von `Recurrence` unabhängigen Gültigkeitszeitraum (uneingeschränkt / einmalig Von–Bis / jährlich wiederkehrend) zu geben, abgelaufene einmalige Regeln automatisch zu löschen und aktuell ungültige wiederkehrende Regeln ans Ende der Auflistung zu sortieren.

## Zusammenfassung

- `ProgramRule` hat **keine** Gültigkeitsfelder — `IsActive` ist die einzige Schaltstelle, ob eine Regel Sendeplätze erzeugt. `ProgramRuleInput`, `ProgramRuleService.ValidateRuleInputAsync` und das UI-`RuleEditModel` enthalten ebenfalls nichts Entsprechendes; ein `ProgramRuleValidityMode`-Enum existiert nicht.
- `ProgramScheduleService.BuildOccurrences` (Z. 1615) prüft pro lokalem Tag **nur** `Recurrence` — die natürliche Stelle für eine zusätzliche Gültigkeitsprüfung; Regeln laden `EnsureCoverageAsync` (nur `IsActive`, Z. 83), `ReconcileAsync` (alle; Verdrängung bei `rule is null || !IsActive`, Z. 1117) und `GetRuleDiagnosesAsync` (alle, inkl. inaktiver).
- Die geforderte Jahresfenster-Semantik existiert bereits zweimal identisch: `SeasonalGenreWindow.GetExpiry` und `GenreCanonicalResolver.IsVisible` (inklusives Tagesfenster, `AddYears`-Rollierung, beide Grenzen erforderlich, Silvester-übergreifende Fenster abbildbar und getestet).
- Automatisches Löschen: `DeleteRuleAsync` ist der einzige Löschpfad (FK-Kaskade für `ProgramRuleCursor` + Join-Zeilen; `ProgramSlot.ProgramRuleId` ist weiche Referenz — Slots bleiben als Historie und werden vom nächsten `ReconcileAsync` auf `Displaced` gesetzt). Der `ProgramScheduleWorker`-Lauf (hinter `Setup.TvSenderEnabled`) ist der natürliche Ausführungsort; er ruft bereits `ReconcileAsync` + `EnsureCoverageAsync` und wird per `IProgramScheduleSignal` geweckt.
- Sortierung: `GetRulesAsync` sortiert nur `OrderBy(Name)` (Z. 91); die UI rendert in Lieferreihenfolge — eine End-Sortierung ungültiger wiederkehrender Regeln ist serverseitig vorzunehmen oder in `TVSenderAdmin` zu ergänzen; eine Status-Kennzeichnung müsste neu gebaut werden (Muster: `admin-chip`, `RuleMetaLabel`, `DiagnosisLabel`).
- Backup-DoD: `VideoWebPlayerBackupData` hat die Mechanik (`OptionalRestoreColumns` + typisierte Default-Arrays; `ProgramRules.PlaceholderGrace` als Vorbild); `LegacyBackupArchiveBuilder.RemoveColumnsAsync` liefert das Alt-Backup-Regressionstest-Muster. EF-Migrationen unter `VideoWebPlayer/Migrations/`.

Test-Ausgangszustand: volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` — **2624 Tests: 2622 erfolgreich, 0 fehlgeschlagen, 2 übersprungen** (bekannter Alt-Skip `PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`, plattformbedingter Unix-Skip `Run_OnUnix_SetsExecutableBit`), Debug-Build 0 Fehler. Nachweis: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [UI](inventory/ui.md)
- [Tests](inventory/tests.md)
